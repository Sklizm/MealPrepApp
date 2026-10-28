using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MealPrepApp.Models;
using MealPrepApp.Services;
using MealPrepApp.ViewModels;
using MealPrepApp.ViewModels.Retete;
using MealPrepApp.ViewModels.Ingrediente;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace MealPrepApp.Tests;
public sealed partial class RegressionTests
{
    [Fact]
    public void ServingScalingPreservesDecimalPrecisionAndRecipeData()
    {
        var factor=ServingCalculator.Factor(3,2);
        Assert.Equal(2m/3m,factor);
        var nutrition=new RecipeNutritionSummary {TotalCalories=900,TotalProteinGrams=45,MissingNutritionCount=1};
        var scaled=ServingCalculator.Scale(nutrition,factor);
        Assert.Equal(900m*factor,scaled.TotalCalories);
        Assert.Equal(900,nutrition.TotalCalories);
        Assert.Equal(1,scaled.MissingNutritionCount);
        Assert.False(scaled.HasCompleteNutrition);
        Assert.Throws<InvalidOperationException>(()=>ServingCalculator.Factor(null,2));
        Assert.Throws<InvalidOperationException>(()=>ServingCalculator.Factor(0,2));
        Assert.Throws<ArgumentOutOfRangeException>(()=>ServingCalculator.Factor(2,0));
    }
    [Fact]
    public void CookingProgressAndTimerSurviveNavigationAndUseMonotonicElapsedTime()
    {
        var clock=new ManualClock();var session=new CookingSession(clock);
        var recipe=new RecipeFull {RecipeID=1,Servings=2,Instructions="First paragraph.\n\nSecond paragraph.\nKeep this line.",Ingredients=new(){new(){IngredientName="Rice",Quantity=150,UnitAbbreviation="g"}}};
        session.StartRecipe(recipe,3);session.Ingredients[0].IsChecked=true;
        session.TimerMinutes=2;session.StartTimerCommand.Execute(null);
        clock.Advance(TimeSpan.FromSeconds(65));session.Tick();Assert.Equal("00:55 rămase",session.TimerText);
        var first=new CookingViewModel(session);var second=new CookingViewModel(session);
        Assert.Same(first.Session,second.Session);
        session.StartRecipe(recipe,4);
        Assert.True(session.Ingredients[0].IsChecked);Assert.Equal(300,session.Ingredients[0].Quantity);Assert.Equal(150,recipe.Ingredients[0].Quantity);
        Assert.Equal(new[]{"First paragraph.","Second paragraph.\nKeep this line."},session.Paragraphs);
        clock.Advance(TimeSpan.FromMinutes(1));session.Tick();Assert.True(session.TimerFinished);Assert.False(session.IsTimerRunning);Assert.True(session.HasTimer);
        session.StopTimerCommand.Execute(null);Assert.False(session.HasTimer);
    }
    [Fact]
    public void PreferencesPersistIndependentlyAndRecoverFromMalformedFiles()
    {
        var directory=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString());var path=Path.Combine(directory,"preferences.json");
        try
        {
            var preferences=new PresentationPreferences(path);Assert.Equal(Appearance.System,preferences.Theme);
            preferences.Theme=Appearance.Dark;preferences.RecipeListView=true;preferences.ReducedMotion=true;
            var reloaded=new PresentationPreferences(path);Assert.Equal(Appearance.Dark,reloaded.Theme);Assert.True(reloaded.RecipeListView);Assert.True(reloaded.ReducedMotion);
            Assert.DoesNotContain("Password",File.ReadAllText(path));
            File.WriteAllText(path,"{invalid");Assert.Equal(Appearance.System,new PresentationPreferences(path).Theme);
        } finally {if(Directory.Exists(directory))Directory.Delete(directory,true);}
    }
    [Fact]
    public async Task NavigationPreservesCachedPageAndRejectsDirtyDeparture()
    {
        using var provider=new ServiceCollection().AddTransient<RetainedPage>().BuildServiceProvider();
        using var navigation=new NavigationService(provider);
        await navigation.NavigateToAsync<RetainedPage>();var page=(RetainedPage)navigation.CurrentView!;page.Filter="Favorite";
        var guard=new GuardedPage();await navigation.NavigateToAsync(guard);
        await navigation.NavigateToAsync<RetainedPage>();Assert.Same(guard,navigation.CurrentView);
        guard.Allow=true;await navigation.NavigateToAsync<RetainedPage>();Assert.Same(page,navigation.CurrentView);Assert.Equal("Favorite",page.Filter);Assert.False(page.Disposed);
    }
    [Fact]
    public async Task EditorDirtyStateIncludesIngredientChangesAndFailedDraftCannotLeave()
    {
        using var provider=RedesignSamples.Services(new SaveDraftDialogs());using var scope=provider.CreateScope();
        var editor=scope.ServiceProvider.GetRequiredService<ReteteEditorViewModel>();Assert.False(editor.IsDirty);
        editor.Title="Unsaved";Assert.True(editor.IsDirty);Assert.False(await editor.CanLeaveAsync());
        editor.Title="";Assert.False(editor.IsDirty);
        editor.AddIngredientRowCommand.Execute(null);Assert.True(editor.IsDirty);editor.IngredientRows.Clear();Assert.False(editor.IsDirty);
        editor.SaveDraftCommand.Execute(null);while(editor.IsBusy)await Task.Delay(1);
        Assert.True(editor.HasError);editor.Title="Unsaved again";Assert.False(await editor.CanLeaveAsync());Assert.True(editor.IsDirty);
    }
    [AvaloniaFact]
    public void RecipeLabelsScalingAndOwnershipAreExplicitInRenderedDetail()
    {
        using var provider=RedesignSamples.Services();using var scope=provider.CreateScope();var services=scope.ServiceProvider;
        services.GetRequiredService<ISessionService>().SignIn(2,"reader");
        var detail=services.GetRequiredService<ReteteDetailViewModel>();
        detail.Recipe=new RecipeFull {UserID=1,RecipeID=1,Title="Read-only recipe",Servings=4,Instructions="Prepare",Ingredients=new(){new(){IngredientName="Flour",Quantity=200,UnitAbbreviation="g"}}};
        detail.SelectedServings=6;Assert.Equal(300,detail.ScaledIngredients[0].Quantity);Assert.Equal(200,detail.Recipe.Ingredients[0].Quantity);Assert.False(detail.CanEdit);
        Assert.Equal("Adaugă la favorite",detail.FavoriteLabel);detail.IsFavorite=true;Assert.Equal("Elimină din favorite",detail.FavoriteLabel);
        var view=new MealPrepApp.Desktop.Views.Retete.ReteteDetailView{DataContext=detail};var window=new Window{Width=860,Height=600,Content=view};window.Show();
        try {window.UpdateLayout();Assert.DoesNotContain(window.GetVisualDescendants().OfType<Button>(),b=>b.IsEffectivelyVisible && ReferenceEquals(b.Command,detail.EditCommand));Assert.Contains(window.GetVisualDescendants().OfType<Button>(),b=>ReferenceEquals(b.Command,detail.CookCommand));}
        finally{window.Close();}
    }
    [AvaloniaFact]
    public void GroupedIngredientsShoppingChecklistAndAgendaBindRealRowCommands()
    {
        using var provider=RedesignSamples.Services();using var scope=provider.CreateScope();var services=scope.ServiceProvider;
        var ingredients=services.GetRequiredService<IngredienteListViewModel>();ingredients.UseGrouping=true;
        var ingredient=new Ingredient{IngredientID=1,Name="Grouped tomato",IngredientCategoryName="Vegetables"};ingredients.Ingredients.Add(ingredient);
        var shopping=services.GetRequiredService<ShoppingListViewModel>();
        var unpurchased=new ShoppingListRow{ItemID=1,IngredientName="Checklist tomato",CategoryName="Vegetables",ToBuyQty=2};
        var purchased=new ShoppingListRow{ItemID=2,IngredientName="Purchased rice",CategoryName="Grains",ToBuyQty=3,IsPurchased=true};
        shopping.Rows.Add(unpurchased);shopping.Rows.Add(purchased);shopping.PurchaseFilter="De cumpărat";
        Assert.Single(shopping.Groups);Assert.False(unpurchased.CanImport);Assert.True(purchased.CanImport);purchased.IsImported=true;Assert.False(purchased.CanImport);
        var window=new Window{Width=860,Height=600};window.Show();
        try
        {
            window.Content=new MealPrepApp.Desktop.Views.Ingrediente.IngredienteListView{DataContext=ingredients};Dispatcher.UIThread.RunJobs();window.UpdateLayout();
            Assert.Contains(window.GetVisualDescendants().OfType<Button>(),b=>ReferenceEquals(b.Command,ingredients.EditNutritionCommand)&&ReferenceEquals(b.CommandParameter,ingredient));
            window.Content=new MealPrepApp.Desktop.Views.Ingrediente.ShoppingListView{DataContext=shopping};Dispatcher.UIThread.RunJobs();window.UpdateLayout();
            Assert.Contains(window.GetVisualDescendants().OfType<CheckBox>(),b=>ReferenceEquals(b.Command,shopping.TogglePurchasedCommand)&&ReferenceEquals(b.CommandParameter,unpurchased));
            Assert.Contains(window.GetVisualDescendants().OfType<Button>(),b=>ReferenceEquals(b.Command,shopping.ImportItemCommand)&&ReferenceEquals(b.CommandParameter,unpurchased));
            var weekly=services.GetRequiredService<MealPrepApp.ViewModels.Planificare.PlanificareWeeklyViewModel>();
            var entry=new MealPlanEntry{MealPlanEntryID=1,RecipeTitle="Agenda meal",CategoryName="Desert",CategoryID=5,PlannedDate=DateTime.Today};weekly.AgendaEntries.Add(entry);
            window.Content=new MealPrepApp.Desktop.Views.Planificare.PlanificareWeeklyView{DataContext=weekly};Dispatcher.UIThread.RunJobs();window.UpdateLayout();weekly.AgendaView=true;Dispatcher.UIThread.RunJobs();window.UpdateLayout();
            Assert.Contains(window.GetVisualDescendants().OfType<Button>(),b=>ReferenceEquals(b.Command,weekly.EditEntryCommand)&&ReferenceEquals(b.CommandParameter,entry));
            Assert.Contains(window.GetVisualDescendants().OfType<Button>(),b=>ReferenceEquals(b.Command,weekly.DuplicateEntryCommand)&&ReferenceEquals(b.CommandParameter,entry));
        }finally{window.Close();}
    }
    [AvaloniaFact]
    public void HomeFavoriteShortcutPassesExactDestinationAndLoadingHidesEmptyMessage()
    {
        using var provider=RedesignSamples.Services();using var scope=provider.CreateScope();var home=scope.ServiceProvider.GetRequiredService<AcasaViewModel>();
        var window=new Window{Width=860,Height=600,Content=new MealPrepApp.Desktop.Views.AcasaView{DataContext=home}};window.Show();
        try{window.UpdateLayout();Assert.Contains(window.GetVisualDescendants().OfType<Button>(),b=>ReferenceEquals(b.Command,home.OpenSectionCommand)&&Equals(b.CommandParameter,"Retete:Favorite"));}
        finally{window.Close();}
        var recipes=scope.ServiceProvider.GetRequiredService<ReteteListViewModel>();
        var view=new MealPrepApp.Desktop.Views.Retete.ReteteListView{DataContext=recipes};var host=new Window{Content=view,Width=860,Height=600};host.Show();
        try
        {
            var empty=view.FindControl<Border>("RecipeEmptyState")!;
            Assert.True(empty.IsVisible);recipes.IsBusy=true;Dispatcher.UIThread.RunJobs();Assert.False(empty.IsVisible);
            recipes.IsBusy=false;recipes.ErrorMessage="Load failed";Dispatcher.UIThread.RunJobs();Assert.False(empty.IsVisible);
        }finally{host.Close();}
    }
    private sealed class SaveDraftDialogs:IDialogService
    {
        public Task<LeaveDecision> ChooseLeaveAsync()=>Task.FromResult(LeaveDecision.SaveDraft);
        public Task<bool> ConfirmAsync(string title,string message)=>Task.FromResult(false);
        public Task ShowErrorAsync(string message)=>Task.CompletedTask;
        public Task ShowInfoAsync(string title,string message)=>Task.CompletedTask;
        public Task<bool> ShowDialogAsync(object viewModel)=>Task.FromResult(false);
    }
    [AvaloniaFact]
    public void ReportTablesRenderPopulatedRankingsAndCategoryDistributions()
    {
        using var provider=RedesignSamples.Services();using var scope=provider.CreateScope();
        var vm=scope.ServiceProvider.GetRequiredService<MealPrepApp.ViewModels.Rapoarte.StatisticiLunareViewModel>();
        vm.Stats=new MonthlyStats {TotalMealsPlanned=3,DistinctRecipes=1,DistinctIngredients=1};vm.HasData=true;
        vm.TopRecipes.Add(new TopRecipe{RecipeID=1,Title="Report recipe",PlannedCount=3,CategoryName="Prânz"});
        vm.TopIngredients.Add(new TopIngredient{IngredientID=1,Name="Report ingredient",UsageCount=2});
        vm.SlotCounts.Add(new MealPrepApp.ViewModels.Rapoarte.SlotCount("Prânz",3));
        var window=new Window{Width=860,Height=900,Content=new MealPrepApp.Desktop.Views.Rapoarte.StatisticiLunareView{DataContext=vm}};window.Show();
        try
        {
            window.UpdateLayout();var grids=window.GetVisualDescendants().OfType<DataGrid>().ToArray();Assert.Equal(2,grids.Length);
            Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(),t=>t.Text=="Report recipe");
            Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(),t=>t.Text=="Report ingredient");
            Assert.Contains(window.GetVisualDescendants().OfType<ProgressBar>(),p=>p.Value==3 && p.Maximum==3);
        }finally{window.Close();}
    }
    [AvaloniaFact]
    public void ReducedMotionUpdatesExistingApplicationTransitions()
    {
        var app=Avalonia.Application.Current!;var previous=app.Resources["MotionDuration"];
        var button=new Button{Content="Motion check"};var window=new Window{Content=button,Width=300,Height=200};window.Show();
        try
        {
            window.UpdateLayout();app.Resources["MotionDuration"]=TimeSpan.Zero;Dispatcher.UIThread.RunJobs();
            var presenter=button.GetVisualDescendants().OfType<Avalonia.Controls.Presenters.ContentPresenter>().First();
            Assert.NotNull(presenter.Transitions);
            Assert.All(presenter.Transitions!.OfType<Avalonia.Animation.BrushTransition>(),transition=>Assert.Equal(TimeSpan.Zero,transition.Duration));
            Assert.NotEmpty(presenter.Transitions!.OfType<Avalonia.Animation.BrushTransition>());
        }finally{app.Resources["MotionDuration"]=previous;window.Close();}
    }
    public sealed class RetainedPage:IAsyncLoadable,IDisposable {public string Filter {get;set;}="";public bool Disposed;public Task LoadAsync()=>Task.CompletedTask;public void Dispose()=>Disposed=true;}
    private sealed class GuardedPage:ILeaveGuard {public bool Allow;public Task<bool> CanLeaveAsync()=>Task.FromResult(Allow);}
    private sealed class ManualClock:TimeProvider {private long _ticks;public override long TimestampFrequency=>TimeSpan.TicksPerSecond;public override long GetTimestamp()=>_ticks;public void Advance(TimeSpan duration)=>_ticks+=duration.Ticks;}
}
