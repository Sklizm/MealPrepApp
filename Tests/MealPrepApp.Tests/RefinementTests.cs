using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MealPrepApp.Data.Repositories;
using MealPrepApp.Desktop.Controls;
using MealPrepApp.Desktop.Views;
using MealPrepApp.Models;
using MealPrepApp.Services;
using MealPrepApp.ViewModels;
using MealPrepApp.ViewModels.Ingrediente;
using MealPrepApp.ViewModels.Retete;
using MealPrepApp.ViewModels.Shell;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MealPrepApp.Tests;

public sealed partial class RegressionTests
{
    [Fact]
    public async Task FailedRecipeLookupRetriesInitializationAndDestinationQueriesOnce()
    {
        var lookups = new RefinementLookups { FailRecipes = true };
        var recipes = new RefinementRecipes();
        using var provider = RefinementServices(lookups, recipes); using var scope = provider.CreateScope();
        var vm = scope.ServiceProvider.GetRequiredService<ReteteListViewModel>();
        await vm.LoadAsync(); Assert.True(vm.HasError); Assert.Empty(vm.Categories); Assert.Empty(recipes.Queries);
        lookups.FailRecipes = false; await vm.RetryCommand.ExecuteAsync(null);
        Assert.False(vm.HasError); Assert.Equal(2, lookups.RecipeCalls); Assert.Equal(2, vm.Categories.Count);
        var shell = scope.ServiceProvider.GetRequiredService<ShellViewModel>();
        await shell.ShowSectionAsync("Retete:Favorite");
        Assert.Equal("Favorite", recipes.Queries.Last().Mode);
        vm = (ReteteListViewModel)scope.ServiceProvider.GetRequiredService<INavigationService>().CurrentView!;
        var before = recipes.Queries.Count;
        vm.SelectedCategory = vm.Categories.Last();
        before = recipes.Queries.Count;
        await shell.ShowSectionAsync("Retete:Cauta:supă:roșii");
        Assert.Equal(before + 1, recipes.Queries.Count);
        Assert.Equal(("Toate", "supă:roșii", (int?)null), recipes.Queries.Last());
        vm.Dispose();
    }

    [Fact]
    public async Task FailedIngredientLookupRetriesAndPopulationRegroupsOnlyOnce()
    {
        var lookups = new RefinementLookups { FailIngredients = true }; var ingredients = new RefinementIngredients();
        using var provider = RefinementServices(lookups, ingredients: ingredients); using var scope = provider.CreateScope();
        var vm = scope.ServiceProvider.GetRequiredService<IngredienteListViewModel>(); vm.UseGrouping = true;
        var resets = 0; vm.GroupedIngredients.CollectionChanged += (_, e) => { if(e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset) resets++; };
        await vm.LoadAsync(); Assert.True(vm.HasError); Assert.Empty(vm.Categories);
        lookups.FailIngredients = false; await vm.RetryCommand.ExecuteAsync(null);
        Assert.False(vm.HasError); Assert.Equal(2, lookups.IngredientCalls); Assert.Equal(100, vm.Ingredients.Count);
        Assert.Single(vm.GroupedIngredients); Assert.Equal(1, resets);
        vm.Dispose();
    }

    [Fact]
    public async Task RecipeRequestsCancelDraftAndPantryReadsAndNeverClearNewBusyState()
    {
        var drafts = new RefinementDrafts(); var pantry = new RefinementPantry(); var recipes = new RefinementRecipes();
        using var provider = RefinementServices(recipes: recipes, drafts: drafts, pantry: pantry); using var scope = provider.CreateScope();
        var vm = scope.ServiceProvider.GetRequiredService<ReteteListViewModel>(); await vm.LoadAsync();
        drafts.Pending = new(); var old = vm.ShowDraftsCommand.ExecuteAsync(null);
        vm.PrepareDestination("Frigider", ""); pantry.Pending = new(); var current = vm.LoadAsync();
        Assert.True(drafts.LastToken.IsCancellationRequested); Assert.True(vm.IsBusy);
        drafts.Pending.SetResult(new[] { new RecipeDraftListItem { Title = "stale" } }); await old;
        Assert.Empty(vm.Drafts); Assert.True(vm.IsBusy);
        vm.PrepareDestination("Toate", "latest"); await vm.LoadAsync();
        Assert.True(pantry.LastToken.IsCancellationRequested);
        pantry.Pending.SetResult(Array.Empty<PantryItem>()); await current;
        Assert.Equal("latest", recipes.Queries.Last().Term); Assert.False(vm.IsBusy);
        // Matching itself also gets the same cancellation token.
        pantry.Pending = null; recipes.PendingMatch = new(); vm.PrepareDestination("Frigider", ""); var matching = vm.LoadAsync();
        vm.PrepareDestination("Toate", "newer"); await vm.LoadAsync();
        Assert.True(recipes.MatchToken.IsCancellationRequested);
        recipes.PendingMatch.SetResult(new[] { new RecipeFindResult { Title = "old match" } }); await matching;
        Assert.DoesNotContain(vm.Recipes, r => r.Title == "old match");
        vm.Dispose();
    }

    [Fact]
    public void RapidSnapshotSelectionsApplyOnlyLatestAndKeepItsBusyState()
    {
        var shopping = new RefinementShopping();
        using var provider = RefinementServices(shopping: shopping); using var scope = provider.CreateScope();
        var vm = scope.ServiceProvider.GetRequiredService<ShoppingListViewModel>();
        var one = new ShoppingSnapshot { SnapshotID = 1 }; var two = new ShoppingSnapshot { SnapshotID = 2 };
        vm.SelectedSnapshot = one; vm.SelectedSnapshot = two;
        Assert.True(shopping.Tokens[1].IsCancellationRequested);
        shopping.Pending[1].SetResult(new[] { new ShoppingListRow { IngredientName = "Old" } });
        Assert.True(vm.IsBusy); Assert.Empty(vm.Rows);
        shopping.Pending[2].SetResult(new[] { new ShoppingListRow { IngredientName = "Latest" } });
        Assert.False(vm.IsBusy); Assert.Equal("Latest", Assert.Single(vm.Rows).IngredientName);
        vm.SelectedSnapshot = one;
        vm.SelectedSnapshot = null; Assert.False(vm.IsBusy); Assert.Empty(vm.Rows);
        shopping.Pending[1].SetResult(new[] { new ShoppingListRow { IngredientName = "Obsolete" } });
        Assert.Empty(vm.Rows); vm.Dispose();
    }

    [Fact]
    public async Task SnapshotMutationDisablesConflictsAndRetainsPurchaseAndImportState()
    {
        var shopping = new RefinementShopping { Immediate = true };
        using var provider = RefinementServices(shopping: shopping); using var scope = provider.CreateScope();
        var vm = scope.ServiceProvider.GetRequiredService<ShoppingListViewModel>(); await vm.LoadAsync();
        shopping.Mutation = new(); var purchase = vm.TogglePurchasedCommand.ExecuteAsync(vm.Rows[0]);
        Assert.True(vm.IsMutating); Assert.True(vm.IsBusy);
        Assert.False(vm.GenerateCommand.CanExecute(null)); Assert.False(vm.ImportItemCommand.CanExecute(vm.Rows[0]));
        shopping.Mutation.SetResult(); await purchase;
        Assert.True(Assert.Single(vm.Rows).IsPurchased); Assert.False(vm.IsBusy); Assert.False(vm.IsMutating);
        // The server remains responsible for idempotency; reload reflects its imported state.
        shopping.Imported = true; await vm.LoadAsync(); Assert.False(Assert.Single(vm.Rows).CanImport);
        vm.StartDate = DateTime.Today; vm.EndDate = DateTime.Today.AddDays(-1);
        await vm.GenerateCommand.ExecuteAsync(null); Assert.True(vm.HasError); Assert.False(vm.IsBusy);
        vm.EndDate = vm.StartDate.AddDays(7); await vm.GenerateCommand.ExecuteAsync(null);
        Assert.False(vm.HasError); Assert.Equal(1, shopping.Created); Assert.True(Assert.Single(vm.Rows).IsPurchased);
        vm.Dispose();
    }

    [Fact]
    public async Task FridgeCategoriesSearchAndSortingAreLocalAndKeepSelectionAfterRefresh()
    {
        var pantry = new RefinementPantry { Stock = new[] {
            new PantryItem { IngredientID=1, IngredientName="Salată", CategoryName="Produse", Quantity=2 },
            new PantryItem { IngredientID=2, IngredientName="Roșii", CategoryName="Produse", Quantity=5 },
            new PantryItem { IngredientID=3, IngredientName="Vită", CategoryName="Carne si peste", Quantity=1 },
            new PantryItem { IngredientID=4, IngredientName="Special", CategoryName="Custom", Quantity=1 } } };
        using var provider = RefinementServices(pantry: pantry); using var scope = provider.CreateScope();
        var vm = scope.ServiceProvider.GetRequiredService<FrigiderViewModel>(); await vm.LoadAsync();
        Assert.Equal(9, vm.Categories.Count); Assert.Equal(2, vm.Categories.Single(c => c.Name=="Produse").Count);
        Assert.Equal("Other", vm.Categories.Single(c => c.Name=="Custom").Illustration);
        Assert.Equal(3, vm.DisplayRows.Count(r => r.StartsCategory));
        vm.SelectCategoryCommand.Execute("Produse"); Assert.Equal(2, vm.VisibleItems.Count);
        vm.SearchTerm="roș"; Assert.Equal("Roșii", Assert.Single(vm.VisibleItems).IngredientName);
        vm.SearchTerm=""; vm.SortOrder="Cantitate"; Assert.Equal(2, vm.VisibleItems[0].Quantity);
        Assert.Equal(1, pantry.Calls);
        await vm.LoadAsync(); Assert.Equal("Produse", vm.SelectedCategory);
        vm.SelectCategoryCommand.Execute("Conserve"); Assert.True(vm.IsEmpty);
        vm.SelectCategoryCommand.Execute(null); Assert.Equal(4, vm.VisibleItems.Count);
    }

    [Fact]
    public async Task FridgeRefreshRejectsStaleResponsesAndRecoversAfterLookupFailure()
    {
        var pantry = new RefinementPantry(); var lookups = new RefinementLookups { FailIngredients=true };
        using var provider = RefinementServices(pantry: pantry, lookups: lookups); using var scope = provider.CreateScope();
        var vm = scope.ServiceProvider.GetRequiredService<FrigiderViewModel>();
        await vm.LoadAsync(); Assert.True(vm.HasError); Assert.Empty(vm.Items);
        lookups.FailIngredients=false; await vm.RetryCommand.ExecuteAsync(null); Assert.Equal(100, vm.Items.Count);
        pantry.Pending=new(); var oldResponse=pantry.Pending; var old=vm.LoadAsync(); var token=pantry.LastToken;
        pantry.Pending=new(); var latestResponse=pantry.Pending; var latest=vm.LoadAsync(); Assert.True(token.IsCancellationRequested);
        oldResponse.SetResult(Array.Empty<PantryItem>()); await old; Assert.True(vm.IsBusy); Assert.Equal(100, vm.Items.Count);
        latestResponse.SetResult(new[] {new PantryItem {IngredientID=1, IngredientName="Latest"}}); await latest;
        Assert.Equal("Latest", Assert.Single(vm.Items).IngredientName); Assert.False(vm.IsBusy);
        pantry.Pending=new(); var disposed=vm.LoadAsync(); vm.Dispose(); Assert.True(pantry.LastToken.IsCancellationRequested);
        pantry.Pending.SetResult(Array.Empty<PantryItem>()); await disposed; Assert.Single(vm.Items);
    }

    [Fact]
    public async Task FridgeCookingShortcutPreparesPantryDestinationOnce()
    {
        var pantry = new RefinementPantry(); var recipes = new RefinementRecipes();
        using var provider = RefinementServices(pantry: pantry, recipes: recipes); using var scope=provider.CreateScope();
        var vm=scope.ServiceProvider.GetRequiredService<FrigiderViewModel>();
        await vm.CookCommand.ExecuteAsync(null);
        var destination=Assert.IsType<ReteteListViewModel>(scope.ServiceProvider.GetRequiredService<INavigationService>().CurrentView);
        Assert.Equal("Frigider", destination.ActiveFilter); Assert.Equal(1, pantry.Calls);
    }

    [AvaloniaFact]
    public async Task NavigationActualClockCompletionNeverExposesTheOldPose()
    {
        var icon = new AnimatedNavigationIcon();
        var window = new Window { Width=100, Height=100, Content=icon }; window.Show();
        try
        {
            var changes = new List<double>();
            icon.PropertyChanged += (_, e) => { if(e.Property==AnimatedNavigationIcon.ProgressProperty) changes.Add(icon.Progress); };
            async Task CompleteAnimationAsync()
            {
                var completion = icon.AnimationTask;
                var timeout = System.Diagnostics.Stopwatch.StartNew();
                // Yield to the real headless dispatcher between ticks. Blocking this thread
                // starves clock scheduling in xUnit's per-test application/session.
                while (!completion.IsCompleted && timeout.Elapsed < TimeSpan.FromSeconds(5))
                {
                    AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                    await Task.Delay(5);
                }
                Assert.True(completion.IsCompleted, "Avalonia animation did not complete within five seconds.");
                await completion;
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            }
            icon.SetEngagement(true, false); await CompleteAnimationAsync();
            Assert.Equal(1, icon.Progress); Assert.False(icon.HasActiveAnimation);
            Assert.All(changes.Zip(changes.Skip(1)), pair => Assert.True(pair.Second >= pair.First, "Completion rolled back"));
            changes.Clear(); icon.SetEngagement(false, false); await CompleteAnimationAsync();
            Assert.Equal(0, icon.Progress); Assert.False(icon.HasActiveAnimation);
            Assert.All(changes.Zip(changes.Skip(1)), pair => Assert.True(pair.Second <= pair.First, "Reset rolled back"));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void FridgeCompactLayoutKeyboardAndLargeStockAreUsable()
    {
        var pantry=new RefinementPantry { Stock=Enumerable.Range(1,2000).Select(i => new PantryItem { IngredientID=i, IngredientName=$"Item {i:0000}", CategoryName="Produse", Quantity=1 }).ToArray() };
        using var provider=RefinementServices(pantry:pantry); using var scope=provider.CreateScope();
        var vm=scope.ServiceProvider.GetRequiredService<FrigiderViewModel>(); vm.LoadAsync().GetAwaiter().GetResult();
        var view=new MealPrepApp.Desktop.Views.Ingrediente.FrigiderView {DataContext=vm};
        var window=new Window {Width=776,Height=600,Content=view}; window.Show();
        try
        {
            window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            var grid=view.GetVisualDescendants().OfType<Avalonia.Controls.Primitives.UniformGrid>().Single();
            Assert.Equal(4,grid.Columns);
            Assert.All(view.GetVisualDescendants().OfType<FridgeFoodIllustration>(), food=>Assert.Equal(36,food.Height));
            Assert.InRange(view.GetVisualDescendants().OfType<ListBoxItem>().Count(),1,40);
            var category=view.GetVisualDescendants().OfType<Button>().Single(b=>Equals(b.CommandParameter,"Produse"));
            category.Focus(); window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None); window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None); Dispatcher.UIThread.RunJobs();
            Assert.Equal("Produse",vm.SelectedCategory);
            window.Width=1196; window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            Assert.Equal(2,grid.Columns);
        }
        finally {window.Close();}
    }

    [Fact]
    public async Task FridgeStockActionsValidateRecoverAndDisableConflicts()
    {
        var pantry=new RefinementPantry {Stock=Array.Empty<PantryItem>()}; var dialogs=new PantryWorkflowDialogs();
        using var provider=RefinementServices(pantry:pantry,dialogs:dialogs); using var scope=provider.CreateScope();
        var vm=scope.ServiceProvider.GetRequiredService<FrigiderViewModel>(); await vm.LoadAsync();
        await vm.AddItemCommand.ExecuteAsync(null); var item=Assert.Single(vm.Items); Assert.Equal(2,item.Quantity);
        await vm.EditItemCommand.ExecuteAsync(item); Assert.Equal(3,Assert.Single(vm.Items).Quantity);
        dialogs.Confirm=false; await vm.RemoveItemCommand.ExecuteAsync(item); Assert.Single(vm.Items);
        dialogs.Confirm=true; pantry.FailRemove=true; await vm.RemoveItemCommand.ExecuteAsync(item);
        Assert.True(vm.HasError); Assert.Single(vm.Items); Assert.False(vm.IsBusy);
        pantry.FailRemove=false; pantry.Mutation=new(); var remove=vm.RemoveItemCommand.ExecuteAsync(item);
        Assert.True(vm.IsMutating); Assert.True(vm.IsBusy); var calls=pantry.Calls;
        await vm.AddItemCommand.ExecuteAsync(null); await vm.RetryCommand.ExecuteAsync(null); Assert.Equal(calls,pantry.Calls);
        pantry.Mutation.SetResult(); await remove; Assert.Empty(vm.Items); Assert.False(vm.IsBusy); Assert.False(vm.HasError);
    }

    [Fact]
    public async Task PantryPopulationInvalidatesItsViewsOnce()
    {
        var pantry = new RefinementPantry();
        using var provider = RefinementServices(pantry: pantry); using var scope = provider.CreateScope();
        var vm = scope.ServiceProvider.GetRequiredService<FrigiderViewModel>(); var invalidations = 0;
        vm.PropertyChanged += (_, e) => { if(e.PropertyName == nameof(vm.Groups)) invalidations++; };
        await vm.LoadAsync(); Assert.Equal(100, vm.Items.Count); Assert.Equal(1, invalidations);
    }

    [Fact]
    public async Task DashboardStartsFourReadsTogetherAndDisposalRejectsStaleCompletion()
    {
        var dashboard = new RefinementDashboard(); var mealPlan = new RefinementMealPlan(); var drafts = new RefinementDrafts { Pending = new() };
        using var provider = RefinementServices(drafts: drafts, dashboard: dashboard, mealPlan: mealPlan); using var scope = provider.CreateScope();
        var vm = scope.ServiceProvider.GetRequiredService<AcasaViewModel>();
        var load = vm.LoadAsync();
        Assert.Equal(2, dashboard.Calls); Assert.Equal(1, mealPlan.Calls); Assert.Equal(1, drafts.Calls);
        Assert.Equal(dashboard.UserId, mealPlan.UserId); Assert.Equal(mealPlan.UserId, drafts.UserId);
        vm.Dispose(); Assert.True(dashboard.Token.IsCancellationRequested); Assert.True(mealPlan.Token.IsCancellationRequested);
        dashboard.Counts.SetResult(new DashboardCounts()); dashboard.Recent.SetResult(new[] { new RecentRecipe { Title = "Obsolete" } });
        mealPlan.Pending.SetResult(Array.Empty<MealPlanEntry>()); drafts.Pending.SetResult(Array.Empty<RecipeDraftListItem>());
        await load; Assert.Null(vm.Counts); Assert.Empty(vm.RecentRecipes);
    }

    [Fact]
    public async Task ClientRecipeCookingPlanningShoppingPantryFlowRecoversFromValidationAndMutationFailures()
    {
        var pantry = new RefinementPantry { Stock = new[] { new PantryItem { IngredientID = 1, IngredientName = "Roșii", Quantity = 100, UnitAbbreviation = "g" } } };
        var shopping = new RefinementShopping { Immediate = true, FailNextPurchase = true };
        shopping.OnImport = () => pantry.Stock[0].Quantity += 200;
        var meals = new RefinementMealPlan { Immediate = true };
        using var provider = RefinementServices(pantry: pantry, shopping: shopping, mealPlan: meals, dialogs: new ImportDialogs());
        using var scope = provider.CreateScope(); var services = scope.ServiceProvider;
        var shell = services.GetRequiredService<ShellViewModel>();
        await shell.ShowSectionAsync("Retete:Cauta:Salată");
        var list = Assert.IsType<ReteteListViewModel>(shell.CurrentView); var recipe = Assert.Single(list.Recipes);
        var detail = services.GetRequiredService<ReteteDetailViewModel>();
        detail.Recipe = new RecipeFull { RecipeID = recipe.RecipeID, Title = recipe.Title, Servings = 2, Instructions = "Pregătește ingredientele.\n\nAmestecă salata.", Ingredients = new() { new() { IngredientName = "Roșii", Quantity = 200, UnitAbbreviation = "g" } } };
        detail.SelectedServings = 3; await detail.CookCommand.ExecuteAsync(null);
        var cooking = services.GetRequiredService<CookingSession>();
        Assert.IsType<CookingViewModel>(shell.CurrentView); Assert.Equal(300, Assert.Single(cooking.Ingredients).Quantity);
        cooking.Ingredients[0].IsChecked = true; cooking.StartTimerCommand.Execute(null);
        var plan = services.GetRequiredService<MealPrepApp.ViewModels.Planificare.PlanMealDialogViewModel>();
        await plan.InitForAddAsync(DateTime.Today, 2, recipe);
        plan.Servings = 0; await plan.SaveCommand.ExecuteAsync(null); Assert.True(plan.HasError); Assert.Empty(meals.Entries);
        plan.Servings = 3; await plan.SaveCommand.ExecuteAsync(null); Assert.False(plan.HasError); Assert.Equal(3, Assert.Single(meals.Entries).Servings);
        await shell.ShowSectionAsync("Planificare");
        Assert.True(cooking.Ingredients[0].IsChecked); Assert.True(cooking.IsTimerRunning);
        await shell.ShowSectionAsync("Cumparaturi");
        var checklist = Assert.IsType<ShoppingListViewModel>(shell.CurrentView);
        await checklist.GenerateCommand.ExecuteAsync(null); Assert.Equal(1, shopping.Created);
        await checklist.TogglePurchasedCommand.ExecuteAsync(checklist.Rows[0]); Assert.True(checklist.HasError); Assert.False(checklist.IsBusy);
        await checklist.RetryCommand.ExecuteAsync(null); Assert.False(checklist.HasError);
        await checklist.TogglePurchasedCommand.ExecuteAsync(checklist.Rows[0]); Assert.True(checklist.Rows[0].IsPurchased); Assert.Equal(100, pantry.Stock[0].Quantity);
        await checklist.ImportItemCommand.ExecuteAsync(checklist.Rows[0]); Assert.True(checklist.Rows[0].IsImported); Assert.Equal(300, pantry.Stock[0].Quantity);
        await checklist.ImportItemCommand.ExecuteAsync(checklist.Rows[0]); Assert.Equal(300, pantry.Stock[0].Quantity);
        await shell.ShowSectionAsync("Frigider"); Assert.Equal(300, Assert.Single(Assert.IsType<FrigiderViewModel>(shell.CurrentView).Items).Quantity);
        await shell.ShowSectionAsync("Cumparaturi"); Assert.True(Assert.Single(checklist.Rows).IsPurchased); Assert.True(checklist.Rows[0].IsImported);
        Assert.True(cooking.Ingredients[0].IsChecked); Assert.True(cooking.IsTimerRunning); plan.Dispose();
    }

    [Fact]
    public async Task IngredientStaleResponseCannotRepopulateRowsOrClearLatestBusyState()
    {
        var ingredients = new RefinementIngredients();
        using var provider = RefinementServices(ingredients: ingredients); using var scope = provider.CreateScope();
        var vm = scope.ServiceProvider.GetRequiredService<IngredienteListViewModel>(); await vm.LoadAsync();
        var oldResponse = ingredients.Pending = new(); var old = vm.LoadAsync(); var oldToken = ingredients.LastToken;
        var latestResponse = ingredients.Pending = new(); var latest = vm.LoadAsync();
        Assert.True(oldToken.IsCancellationRequested);
        oldResponse.SetResult(new[] {new Ingredient {Name="Old"}}); await old; Assert.True(vm.IsBusy);
        latestResponse.SetResult(new[] {new Ingredient {Name="Latest"}}); await latest;
        Assert.False(vm.IsBusy); Assert.Equal("Latest", Assert.Single(vm.Ingredients).Name); vm.Dispose();
    }

    [Fact]
    public async Task CancelledRepositoryReadsDoNotOpenConnections()
    {
        var factory = new RedesignSamples.NoDatabase(); using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new DraftRepository(factory).GetDraftsAsync(1, cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new PantryRepository(factory).GetPantryAsync(1, cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new RecipeRepository(factory).FindRecipesByIngredientsAsync(new[] { 1 }, cancellationToken: cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new ShoppingListRepository(factory).GetSnapshotItemsAsync(1, 2, cancellation.Token));
    }

    [AvaloniaFact]
    public void NavigationAnimationEntryHoldInterruptionResetAndReducedMotionAreFinite()
    {
        var icon = new AnimatedNavigationIcon(); var driver = new IllustrationDriver(icon); icon.AnimationRunner = driver.Run;
        icon.SetEngagement(true, false); Assert.Equal(780, driver.Duration.TotalMilliseconds); Assert.True(icon.HasActiveAnimation);
        driver.Advance(.5); Assert.InRange(icon.Progress, .4, .6);
        icon.SetEngagement(true, true); Assert.Equal(1, driver.Starts);
        driver.Advance(1); Dispatcher.UIThread.RunJobs(); Assert.Equal(1, icon.Progress); Assert.False(icon.HasActiveAnimation);
        icon.SetEngagement(false, true); Assert.False(icon.HasActiveAnimation);
        icon.SetEngagement(false, false); Assert.Equal(180, driver.Duration.TotalMilliseconds);
        driver.Advance(.5); var halfway = icon.Progress;
        icon.SetEngagement(true, false); Assert.Equal(1, driver.Cancellations); Assert.Equal(halfway, icon.Progress);
        driver.Advance(.5); icon.ReducedMotion = true; Assert.Equal(2, driver.Cancellations); Assert.False(icon.HasActiveAnimation); Assert.Equal(0, icon.Progress);
        icon.SetEngagement(false, false); icon.SetEngagement(true, true); Assert.False(icon.HasActiveAnimation);
        icon.ReducedMotion = false; icon.SetEngagement(false, false); driver.Advance(1); Dispatcher.UIThread.RunJobs();
        Assert.Equal(0, icon.Progress); Assert.False(icon.HasActiveAnimation);
    }

    [AvaloniaFact]
    public void ReducedMotionResourceAndDetachCancelExistingIconAnimationImmediately()
    {
        var app = Application.Current!; var previous = app.Resources["ReducedMotion"];
        app.Resources["ReducedMotion"] = false;
        var window = new ShellWindow(); window.Show();
        try
        {
            window.UpdateLayout(); var icon = window.GetVisualDescendants().OfType<AnimatedNavigationIcon>().Skip(1).First();
            var driver = new IllustrationDriver(icon); icon.AnimationRunner = driver.Run;
            icon.SetEngagement(true, false); driver.Advance(.5); Assert.True(icon.HasActiveAnimation);
            app.Resources["ReducedMotion"] = true; Dispatcher.UIThread.RunJobs();
            Assert.True(icon.ReducedMotion); Assert.False(icon.HasActiveAnimation); Assert.Equal(0, icon.Progress);
            app.Resources["ReducedMotion"] = false; Dispatcher.UIThread.RunJobs();
            icon.SetEngagement(false, false); driver.Advance(.5); icon.SetEngagement(true, false); Assert.True(icon.HasActiveAnimation);
            window.Close(); Assert.False(icon.HasActiveAnimation); Assert.Equal(0, icon.Progress);
        }
        finally { window.Close(); app.Resources["ReducedMotion"] = previous; }
    }

    [AvaloniaFact]
    public void PlaceholderAndErrorDialogUseContrastingThemeResources()
    {
        var app = Application.Current!; var previous = app.RequestedThemeVariant;
        try
        {
            foreach(var theme in new[] {ThemeVariant.Light, ThemeVariant.Dark})
            {
                app.RequestedThemeVariant = theme;
                var box = new TextBox {Watermark="Caută ingrediente",Width=240}; var window = new Window {Content=box,Width=300,Height=100};
                window.Show();window.UpdateLayout();
                try
                {
                    var watermark = box.GetVisualDescendants().OfType<TextBlock>().First(t => t.Text == "Caută ingrediente" && t.IsEffectivelyVisible);
                    Assert.True(app.TryFindResource("TextSecondary", theme, out var expected));
                    Assert.Equal(((ISolidColorBrush)expected!).Color,((ISolidColorBrush)watermark.Foreground!).Color);
                    Assert.Equal(1,watermark.Opacity);
                }
                finally {window.Close();}
                var dialog = new MealPrepApp.Desktop.Views.Shared.MessageDialog();
                dialog.Configure(MealPrepApp.Desktop.Views.Shared.MessageDialogKind.Error,null,null);dialog.Show();dialog.UpdateLayout();
                try
                {
                    var header = dialog.FindControl<TextBlock>("HeaderText")!;
                    Assert.True(app.TryFindResource("OnDanger",theme,out var expected));
                    Assert.Equal(((ISolidColorBrush)expected!).Color,((ISolidColorBrush)header.Foreground!).Color);
                }
                finally {dialog.Close();}
            }
        }
        finally {app.RequestedThemeVariant=previous;}
    }

    [AvaloniaFact]
    public void RailSupportsKeyboardActivationArrowFocusAndFixedWidthAtMinimumSize()
    {
        using var provider = RefinementServices(); using var scope = provider.CreateScope();
        var vm = scope.ServiceProvider.GetRequiredService<ShellViewModel>();
        var window = new ShellWindow { Width = 860, Height = 600, DataContext = vm }; window.Show();
        try
        {
            Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            var rail = window.FindControl<Grid>("NavigationPanel")!; Assert.Equal(84, rail.Bounds.Width);
            var buttons = rail.GetVisualDescendants().OfType<Button>().ToArray(); Assert.Equal(9, buttons.Length);
            Assert.All(buttons, b => { Assert.Equal(52, b.Bounds.Width); Assert.Equal(52, b.Bounds.Height); });
            Assert.Equal(7, rail.GetVisualDescendants().OfType<AnimatedNavigationIcon>().Count());
            buttons[0].Focus(NavigationMethod.Tab); window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None); window.KeyReleaseQwerty(PhysicalKey.Tab, RawInputModifiers.None);
            Assert.True(buttons[1].IsKeyboardFocusWithin);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None); window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None); Dispatcher.UIThread.RunJobs();
            Assert.Equal("Retete", vm.ActiveTab); Assert.Contains("selected", buttons[1].Classes);
            window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None); window.KeyReleaseQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
            window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None); window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None); Dispatcher.UIThread.RunJobs(); Assert.Equal("Acasa", vm.ActiveTab);
            Assert.True(buttons[^1].Bounds.Height > 0); Assert.DoesNotContain(rail.GetVisualDescendants().OfType<TextBlock>(), t => t.IsEffectivelyVisible && t.Text == "MealPrep");
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task RejectedNavigationKeepsCommittedRailSelectionAndDoesNotPrepareDestination()
    {
        using var provider = RefinementServices(); using var scope = provider.CreateScope(); var services = scope.ServiceProvider;
        var navigation = services.GetRequiredService<INavigationService>(); var shell = services.GetRequiredService<ShellViewModel>();
        var editor = services.GetRequiredService<ReteteEditorViewModel>();
        // A parameterized editor already shown in the shell; unsaved state rejects departure.
        await navigation.NavigateToAsync(editor); editor.Title = "Unsaved";
        var prepared = false; await navigation.NavigateToAsync<ReteteListViewModel>(_ => prepared = true);
        Assert.False(prepared); Assert.Same(editor, navigation.CurrentView); Assert.Equal("Retete", shell.ActiveTab);
        var window = new ShellWindow { DataContext = shell }; window.Show();
        try
        {
            Dispatcher.UIThread.RunJobs();
            var homeButton = window.GetVisualDescendants().OfType<Button>().First(b => ReferenceEquals(b.Command, shell.ShowAcasaCommand));
            homeButton.Focus(); window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None); window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None); Dispatcher.UIThread.RunJobs();
            Assert.Equal("Retete", shell.ActiveTab); Assert.DoesNotContain("selected", homeButton.Classes);
        }
        finally { editor.Title = ""; window.Close(); }
    }

    [AvaloniaFact]
    public void ButtonRolesKeepTheirOwnHoverAndPressedColorsInBothThemes()
    {
        var app = Application.Current!; var previous = app.RequestedThemeVariant;
        var motion = app.Resources["MotionDuration"]; app.Resources["MotionDuration"] = TimeSpan.Zero;
        foreach(var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
        {
            app.RequestedThemeVariant = theme;
            foreach(var role in new[] { "Primary", "Secondary", "Danger", "DestructiveLink" })
            {
                var button = new Button { Content = "Action", Width = 140, Height = 50 }; button.Classes.Add(role);
                var window = new Window { Width = 200, Height = 100, Content = button }; window.Show();
                try
                {
                    window.UpdateLayout(); var point = button.TranslatePoint(new Point(20,20), window)!.Value;
                    window.MouseMove(point); Dispatcher.UIThread.RunJobs();
                    var presenter = button.GetVisualDescendants().OfType<ContentPresenter>().First();
                    var key = role == "Primary" ? "ActionHover" : role == "Danger" ? "Danger" : role == "DestructiveLink" ? "Surface" : "Selection";
                    Assert.True(app.TryFindResource(key, theme, out var expected)); Assert.Equal(((ISolidColorBrush)expected!).Color, ((ISolidColorBrush)presenter.Background!).Color);
                    window.MouseDown(point, MouseButton.Left); Dispatcher.UIThread.RunJobs();
                    key = role == "Primary" ? "ActionPressed" : role == "Danger" ? "Danger" : role == "DestructiveLink" ? "Surface" : "Selection";
                    Assert.True(app.TryFindResource(key, theme, out expected)); Assert.Equal(((ISolidColorBrush)expected!).Color, ((ISolidColorBrush)presenter.Background!).Color);
                    var foreground = role is "Danger" or "Primary" ? role == "Danger" ? "OnDanger" : "OnPrimary" : role == "DestructiveLink" ? "Danger" : "TextPrimary";
                    Assert.True(app.TryFindResource(foreground,theme,out var ink));
                    Assert.Equal(((ISolidColorBrush)ink!).Color,((ISolidColorBrush)presenter.Foreground!).Color);
                    window.MouseUp(point, MouseButton.Left);
                }
                finally { window.Close(); }
            }
        }
        app.RequestedThemeVariant = previous; app.Resources["MotionDuration"] = motion;
    }

    private static ServiceProvider RefinementServices(RefinementLookups? lookups = null, RefinementRecipes? recipes = null,
        RefinementIngredients? ingredients = null, RefinementShopping? shopping = null, RefinementDrafts? drafts = null,
        RefinementPantry? pantry = null, RefinementDashboard? dashboard = null, RefinementMealPlan? mealPlan = null, IDialogService? dialogs = null) =>
        RedesignSamples.Services(dialogs: dialogs, configure: s =>
        {
            s.AddSingleton<ISessionService>(_ => { var session = new SessionService(); session.SignIn(1, "Codrin"); return session; });
            s.AddSingleton<LookupRepository>(lookups ?? new RefinementLookups()); s.AddSingleton<RecipeRepository>(recipes ?? new RefinementRecipes());
            s.AddSingleton<IngredientRepository>(ingredients ?? new RefinementIngredients()); s.AddSingleton<ShoppingListRepository>(shopping ?? new RefinementShopping());
            s.AddSingleton<DraftRepository>(drafts ?? new RefinementDrafts()); s.AddSingleton<PantryRepository>(pantry ?? new RefinementPantry());
            if(dashboard != null) s.AddSingleton<DashboardRepository>(dashboard); if(mealPlan != null) s.AddSingleton<MealPlanRepository>(mealPlan);
        });

    private sealed class RefinementLookups() : LookupRepository(new RedesignSamples.NoDatabase())
    {
        public override Task<IReadOnlyList<Unit>> GetUnitsAsync(CancellationToken cancellationToken=default) => Task.FromResult<IReadOnlyList<Unit>>(new[] {new Unit {UnitID=1,Abbreviation="g"}});
        public bool FailRecipes, FailIngredients; public int RecipeCalls, IngredientCalls;
        public override Task<IReadOnlyList<Category>> GetCategoriesAsync(CancellationToken cancellationToken = default)
        { RecipeCalls++; if(FailRecipes) throw new InvalidOperationException("Temporary lookup failure"); return Task.FromResult<IReadOnlyList<Category>>(new[] { new Category { CategoryID = 2, Name = "Prânz" } }); }
        public override Task<IReadOnlyList<IngredientCategory>> GetIngredientCategoriesAsync(CancellationToken cancellationToken = default)
        { IngredientCalls++; if(FailIngredients) throw new InvalidOperationException("Temporary lookup failure"); return Task.FromResult<IReadOnlyList<IngredientCategory>>(Array.Empty<IngredientCategory>()); }
    }
    private sealed class RefinementRecipes() : RecipeRepository(new RedesignSamples.NoDatabase())
    {
        public List<(string Mode, string Term, int? Category)> Queries = new(); public TaskCompletionSource<IReadOnlyList<RecipeFindResult>>? PendingMatch; public CancellationToken MatchToken;
        public override Task<IReadOnlyList<RecipeListItem>> BrowseAsync(int userId, string mode, string term, int? categoryId, int pageNumber, int pageSize, CancellationToken cancellationToken = default, string sort = "Cele mai noi")
        { Queries.Add((mode, term, categoryId)); return Task.FromResult<IReadOnlyList<RecipeListItem>>(new[] { new RecipeListItem { RecipeID = 1, Title = term, TotalCount = 1 } }); }
        public override Task<IReadOnlyList<RecipeFindResult>> FindRecipesByIngredientsAsync(IReadOnlyCollection<int> ingredientIds, int? minMatchCount = null, CancellationToken cancellationToken = default)
        { MatchToken = cancellationToken; return PendingMatch?.Task ?? Task.FromResult<IReadOnlyList<RecipeFindResult>>(Array.Empty<RecipeFindResult>()); }
    }
    private sealed class RefinementIngredients() : IngredientRepository(new RedesignSamples.NoDatabase())
    {
        public TaskCompletionSource<IReadOnlyList<Ingredient>>? Pending; public CancellationToken LastToken;
        public override Task<IReadOnlyList<Ingredient>> GetIngredientsAsync(int? ingredientCategoryId = null, CancellationToken cancellationToken = default) { LastToken = cancellationToken; return Pending?.Task ?? Task.FromResult<IReadOnlyList<Ingredient>>(Enumerable.Range(1,100).Select(i => new Ingredient { IngredientID = i, Name = $"Ingredient {i}", IngredientCategoryName = "Legume" }).ToArray()); }
    }
    private sealed class RefinementPantry() : PantryRepository(new RedesignSamples.NoDatabase())
    {
        public TaskCompletionSource<IReadOnlyList<PantryItem>>? Pending; public CancellationToken LastToken; public PantryItem[]? Stock; public int Calls; public bool FailRemove; public TaskCompletionSource? Mutation;
        public override Task AddPantryItemAsync(int userId,int ingredientId,int unitId,decimal quantity) { Stock=[new PantryItem { UserPantryID=1,IngredientID=ingredientId,UnitID=unitId,Quantity=quantity,IngredientName="Ingredient 1"}]; return Task.CompletedTask; }
        public override Task UpdatePantryQuantityAsync(int id,int userId,decimal quantity) { Stock!.Single(i=>i.UserPantryID==id).Quantity=quantity; return Task.CompletedTask; }
        public override async Task RemovePantryItemAsync(int id,int userId) { if(FailRemove) throw new InvalidOperationException("Temporary failure"); if(Mutation!=null) await Mutation.Task; Stock=Stock!.Where(i=>i.UserPantryID!=id).ToArray(); }
        public override Task<IReadOnlyList<PantryItem>> GetPantryAsync(int userId, CancellationToken cancellationToken = default)
        { Calls++; LastToken = cancellationToken; return Pending?.Task ?? Task.FromResult<IReadOnlyList<PantryItem>>(Stock ?? Enumerable.Range(1,100).Select(i => new PantryItem { IngredientID = i, IngredientName = $"Ingredient {i}" }).ToArray()); }
    }
    private sealed class RefinementDrafts() : DraftRepository(new RedesignSamples.NoDatabase())
    {
        public TaskCompletionSource<IReadOnlyList<RecipeDraftListItem>>? Pending; public CancellationToken LastToken; public int Calls, UserId;
        public override Task<IReadOnlyList<RecipeDraftListItem>> GetDraftsAsync(int userId, CancellationToken cancellationToken = default)
        { Calls++; UserId = userId; LastToken = cancellationToken; return Pending?.Task ?? Task.FromResult<IReadOnlyList<RecipeDraftListItem>>(Array.Empty<RecipeDraftListItem>()); }
    }
    private sealed class RefinementShopping() : ShoppingListRepository(new RedesignSamples.NoDatabase())
    {
        public Dictionary<int, TaskCompletionSource<IReadOnlyList<ShoppingListRow>>> Pending = new(); public Dictionary<int, CancellationToken> Tokens = new();
        public bool Immediate, Purchased, Imported, FailNextPurchase; public Action? OnImport; public int Created; public TaskCompletionSource? Mutation;
        public override Task<IReadOnlyList<ShoppingSnapshot>> GetSnapshotsAsync(int userId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ShoppingSnapshot>>(new[] { new ShoppingSnapshot { SnapshotID = 1 } });
        public override Task<IReadOnlyList<ShoppingListRow>> GetSnapshotItemsAsync(int userId, int snapshotId, CancellationToken cancellationToken = default)
        {
            Tokens[snapshotId] = cancellationToken;
            if(Immediate) return Task.FromResult<IReadOnlyList<ShoppingListRow>>(new[] { new ShoppingListRow { ItemID = 1, IngredientName = "Roșii", IsPurchased = Purchased, IsImported = Imported } });
            return (Pending[snapshotId] = new()).Task;
        }
        public override async Task SetPurchasedAsync(int userId, int itemId, bool purchased) { if(FailNextPurchase) { FailNextPurchase=false; throw new InvalidOperationException("Temporary mutation failure"); } if(Mutation != null) await Mutation.Task; Purchased = purchased; }
        public override Task ImportItemAsync(int userId, int itemId) { if(!Imported) { Imported=true; OnImport?.Invoke(); } return Task.CompletedTask; }
        public override Task<int> CreateSnapshotAsync(int userId, DateTime start, DateTime end) { Created++; return Task.FromResult(1); }
    }
    private sealed class RefinementDashboard() : DashboardRepository(new RedesignSamples.NoDatabase())
    {
        public TaskCompletionSource<DashboardCounts> Counts = new(); public TaskCompletionSource<IReadOnlyList<RecentRecipe>> Recent = new(); public int Calls, UserId; public CancellationToken Token;
        public override Task<DashboardCounts> GetDashboardCountsAsync(int userId, CancellationToken cancellationToken = default) { Calls++; UserId=userId; Token=cancellationToken; return Counts.Task; }
        public override Task<IReadOnlyList<RecentRecipe>> GetRecentRecipesAsync(int userId, int topN = 12, CancellationToken cancellationToken = default) { Calls++; return Recent.Task; }
    }
    private sealed class RefinementMealPlan() : MealPlanRepository(new RedesignSamples.NoDatabase())
    {
        public bool Immediate; public List<MealPlanEntry> Entries = new();
        public int Calls, UserId; public CancellationToken Token; public TaskCompletionSource<IReadOnlyList<MealPlanEntry>> Pending = new();
        public override Task<int> PlanMealAsync(int userId, int recipeId, int categoryId, DateTime plannedDate, int? servings, string? notes)
        { Entries.Add(new MealPlanEntry {RecipeID=recipeId,CategoryID=categoryId,PlannedDate=plannedDate,Servings=servings,RecipeTitle="Salată"}); return Task.FromResult(1); }
        public override Task<IReadOnlyList<MealPlanEntry>> GetWeeklyPlanAsync(int userId, DateTime startDate, CancellationToken cancellationToken = default) { Calls++; UserId=userId; Token=cancellationToken; return Immediate ? Task.FromResult<IReadOnlyList<MealPlanEntry>>(Entries) : Pending.Task; }
    }
    private sealed class PantryWorkflowDialogs : IDialogService
    {
        public bool Confirm=true;
        public Task<bool> ConfirmAsync(string title,string message)=>Task.FromResult(Confirm);
        public Task ShowErrorAsync(string message)=>Task.CompletedTask;
        public Task ShowInfoAsync(string title,string message)=>Task.CompletedTask;
        public async Task<bool> ShowDialogAsync(object viewModel)
        {
            var editor=Assert.IsType<PantryItemDialogViewModel>(viewModel);
            editor.SelectedIngredient=editor.Ingredients[0]; editor.SelectedUnit=editor.Units[0];
            editor.Quantity=0; await editor.SaveCommand.ExecuteAsync(null); Assert.True(editor.HasError);
            editor.Quantity=editor.IsEdit ? 3 : 2; await editor.SaveCommand.ExecuteAsync(null);
            return !editor.HasError;
        }
    }
    private sealed class ImportDialogs : IDialogService
    {
        public Task<bool> ConfirmAsync(string title, string message) => Task.FromResult(true);
        public Task ShowErrorAsync(string message) => Task.CompletedTask;
        public Task ShowInfoAsync(string title, string message) => Task.CompletedTask;
        public Task<bool> ShowDialogAsync(object viewModel) => Task.FromResult(false);
    }
    private sealed class IllustrationDriver(AnimatedNavigationIcon icon)
    {
        private TaskCompletionSource? _pending; private double _from, _to; public int Starts, Cancellations; public TimeSpan Duration;
        public Task Run(Animation animation, CancellationToken token)
        {
            Starts++; Duration = animation.Duration;
            _from = (double)((Setter)animation.Children[0].Setters[0]).Value!; _to = (double)((Setter)animation.Children[1].Setters[0]).Value!;
            var completion = _pending = new(); token.Register(() => { Cancellations++; completion.TrySetCanceled(token); });
            return completion.Task;
        }
        public void Advance(double progress) { icon.Progress = _from + (_to-_from)*progress; if(progress >= 1) _pending!.TrySetResult(); }
    }
}
