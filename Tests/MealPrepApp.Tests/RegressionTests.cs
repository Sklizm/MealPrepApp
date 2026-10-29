using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using MealPrepApp.ViewModels.Shell;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Themes.Fluent;
using MealPrepApp.Desktop.Converters;
using MealPrepApp.Data;
using MealPrepApp.Services;
using MealPrepApp.ViewModels.Auth;
using SkiaSharp;
using Xunit;

[assembly: AvaloniaTestApplication(typeof(MealPrepApp.Tests.TestHost))]

namespace MealPrepApp.Tests;

public sealed class TestApplication : Application
{
    public override void Initialize()
    {
        var applicationResources = new MealPrepApp.Desktop.App();
        applicationResources.Initialize();
        var resources = applicationResources.Resources;
        applicationResources.Resources = new ResourceDictionary();
        Resources = resources;
        var styles = applicationResources.Styles.ToArray();
        applicationResources.Styles.Clear();
        foreach (var style in styles) Styles.Add(style);
        foreach (var template in applicationResources.DataTemplates) DataTemplates.Add(template);
    }
}

public static class TestHost
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<TestApplication>().WithInterFont().UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing=false });
}

public sealed partial class RegressionTests
{
    [AvaloniaFact]
    public void NavigationSelectionDoesNotWriteBooleanIntoStringState()
    {
        var state = new NavigationState();
        var converter = new StringEqualsConverter();
        var first = new RadioButton { GroupName = "Navigation" };
        var second = new RadioButton { GroupName = "Navigation" };
        first.Bind(RadioButton.IsCheckedProperty, new Binding(nameof(state.ActiveTab))
        {
            Source = state, Converter = converter, ConverterParameter = "Acasa", Mode = BindingMode.OneWay
        });
        second.Bind(RadioButton.IsCheckedProperty, new Binding(nameof(state.ActiveTab))
        {
            Source = state, Converter = converter, ConverterParameter = "Retete", Mode = BindingMode.OneWay
        });
        var owner = new Window { Content = new StackPanel { Children = { first, second } } };
        owner.Show();
        second.IsChecked = true;
        Assert.Equal("Acasa", state.ActiveTab);
        Assert.Same(BindingOperations.DoNothing,
            converter.ConvertBack(true, typeof(string), "Retete", System.Globalization.CultureInfo.InvariantCulture));
        owner.Close();
    }

    [AvaloniaFact]
    public async Task ClosingModalWithoutConfirmationReturnsFalseAndLeavesOwnerOpen()
    {
        var owner = new Window();
        owner.Show();
        var dialog = new Window();
        var result = dialog.ShowDialog<bool>(owner);
        Assert.Same(owner, dialog.Owner);
        Assert.False(result.IsCompleted);
        dialog.Close();
        Assert.False(await result);
        Assert.True(owner.IsVisible);
        Assert.Empty(owner.OwnedWindows);
        owner.Close();
    }

    [AvaloniaFact]
    public void FeatureViewsLoadWithApplicationResources()
    {
        var types = typeof(MealPrepApp.Desktop.App).Assembly.GetTypes()
            .Where(t => !t.IsAbstract && t.Namespace?.StartsWith("MealPrepApp.Desktop.Views") == true)
            .Where(t => typeof(UserControl).IsAssignableFrom(t) || typeof(Window).IsAssignableFrom(t))
            .Where(t => t != typeof(MealPrepApp.Desktop.Views.Auth.LoginWindow));
        foreach (var type in types)
        {
            var view = Activator.CreateInstance(type);
            Assert.NotNull(view);
        }
    }

    [AvaloniaFact]
    public void EveryShellTabExecutesItsBoundCommandWithoutConverterExceptions()
    {
        using var services=RedesignSamples.Services();
        using var scope=services.CreateScope();
        var provider=scope.ServiceProvider;
        provider.GetRequiredService<ISessionService>().SignIn(1,"regression");
        var vm=provider.GetRequiredService<ShellViewModel>();
        var shell=new MealPrepApp.Desktop.Views.ShellWindow {DataContext=vm};
        shell.Show();
        try
        {
            var buttons=shell.GetLogicalDescendants().OfType<Button>().Where(b=>b.Content is MealPrepApp.Desktop.Controls.AnimatedNavigationIcon).ToArray();
            Assert.Equal(7,buttons.Length);
            var destinations=new[]{"Acasa","Retete","Planificare","Frigider","Cumparaturi","Ingrediente","Rapoarte"};
            for(int i=0;i<buttons.Length;i++)
            {
                Assert.NotNull(buttons[i].Command);
                typeof(Button).GetMethod("OnClick",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.Invoke(buttons[i],null);
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(destinations[i],vm.ActiveTab);
                Assert.Contains("selected", buttons[i].Classes);
                Assert.NotNull(vm.CurrentView);
            }
        }
        finally {shell.Close();}
    }

    [AvaloniaFact]
    public void RealNavigationRendersPopulatedFeatureTemplatesAndBindsCommands()
    {
        var registrations = new ServiceCollection();
        var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:MealPrepDB"] = "Server=unused;Database=unused"
            }).Build();
        typeof(MealPrepApp.Desktop.App).GetMethod("ConfigureServices",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(null, new object[] { registrations, config });
        Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions.Replace(
            registrations, ServiceDescriptor.Singleton<IDbConnectionFactory>(new UnavailableDatabase()));
        using var services = registrations.BuildServiceProvider();
        using var scope = services.CreateScope();
        var provider = scope.ServiceProvider;
        provider.GetRequiredService<ISessionService>().SignIn(1, "regression");
        var shell = new MealPrepApp.Desktop.Views.ShellWindow
        {
            DataContext = provider.GetRequiredService<ShellViewModel>()
        };
        shell.Show();
        try
        {
            var navigation = provider.GetRequiredService<INavigationService>();
            void Render(object viewModel)
            {
                navigation.NavigateToAsync(viewModel).GetAwaiter().GetResult();
                Dispatcher.UIThread.RunJobs();
                shell.UpdateLayout();
            }
            void AssertCommand(System.Windows.Input.ICommand command, object parameter)
            {
                var buttons=shell.GetVisualDescendants().OfType<Button>().ToArray();
                if(buttons.Any(button=>ReferenceEquals(button.Command,command)&&ReferenceEquals(button.CommandParameter,parameter)))return;
                foreach(var button in buttons.Where(b=>b.ContextMenu!=null))
                {
                    button.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent)); Dispatcher.UIThread.RunJobs();
                    try {if(button.ContextMenu!.Items.OfType<MenuItem>().Any(item=>ReferenceEquals(item.Command,command)&&ReferenceEquals(item.CommandParameter,parameter)))return;}
                    finally {button.ContextMenu!.Close();}
                }
                Assert.Fail("Command and row parameter were not bound in a rendered control or overflow menu.");
            }

            var recipes = provider.GetRequiredService<MealPrepApp.ViewModels.Retete.ReteteListViewModel>();
            recipes.Categories.Add(new MealPrepApp.Models.Category { CategoryID = 1, Name = "Breakfast" });
            recipes.Recipes.Add(new MealPrepApp.Models.RecipeListItem
            {
                RecipeID = 1, Title = "Rendered recipe", CategoryID = 1, Servings = 2
            });
            Render(recipes);
            Assert.Contains(shell.GetVisualDescendants().OfType<TextBlock>(),
                text => text.Text == "Rendered recipe");
            AssertCommand(recipes.OpenRecipeCommand, recipes.Recipes[0]);
            AssertCommand(recipes.DeleteRecipeCommand, recipes.Recipes[0]);

            var draft = new MealPrepApp.Models.RecipeDraftListItem { DraftID = 1, Title = "Rendered draft" };
            recipes.Drafts.Add(draft);
            recipes.ActiveFilter = "Drafts";
            Dispatcher.UIThread.RunJobs();
            shell.UpdateLayout();
            AssertCommand(recipes.OpenDraftCommand, draft);
            AssertCommand(recipes.DeleteDraftCommand, draft);

            var ingredients = provider.GetRequiredService<MealPrepApp.ViewModels.Ingrediente.IngredienteListViewModel>();
            ingredients.Ingredients.Add(new MealPrepApp.Models.Ingredient
            {
                IngredientID = 1, Name = "Rendered ingredient", IngredientCategoryName = "Vegetables"
            });
            Render(ingredients);
            Assert.Contains(shell.GetVisualDescendants().OfType<TextBlock>(),
                text => text.Text == "Rendered ingredient");
            AssertCommand(ingredients.EditNutritionCommand, ingredients.Ingredients[0]);
            AssertCommand(ingredients.DeleteIngredientCommand, ingredients.Ingredients[0]);

            var pantry = provider.GetRequiredService<MealPrepApp.ViewModels.Ingrediente.FrigiderViewModel>();
            var item = new MealPrepApp.Models.PantryItem { UserPantryID = 1, IngredientName = "Pantry row", Quantity = 2 };
            pantry.Items.Add(item);
            Render(pantry);
            AssertCommand(pantry.EditItemCommand, item);
            AssertCommand(pantry.RemoveItemCommand, item);

            var entry = new MealPrepApp.Models.MealPlanEntry { MealPlanEntryID = 1, RecipeTitle = "Calendar meal", CategoryID = 1 };
            var monthly = provider.GetRequiredService<MealPrepApp.ViewModels.Planificare.PlanificareMonthlyViewModel>();
            var day = new MealPrepApp.ViewModels.Planificare.DayCellViewModel
            {
                Date = DateTime.Today, IsCurrentMonth = true, Entries = new[] { entry }
            };
            monthly.Days.Add(day);
            Render(monthly);
            AssertCommand(monthly.AddOnDayCommand, day);
            AssertCommand(monthly.EditEntryCommand, entry);

            var weekly = provider.GetRequiredService<MealPrepApp.ViewModels.Planificare.PlanificareWeeklyViewModel>();
            weekly.SlotHeaders.Add("Breakfast");
            var cell = new MealPrepApp.ViewModels.Planificare.WeekCellViewModel
            {
                Date = DateTime.Today, CategoryId = 1, Entries = new[] { entry }
            };
            weekly.Rows.Add(new MealPrepApp.ViewModels.Planificare.WeekRowViewModel
            {
                Date = DateTime.Today, DayLabel = "Today", Cells = new[] { cell }
            });
            Render(weekly);
            AssertCommand(weekly.AddInCellCommand, cell);
            AssertCommand(weekly.EditEntryCommand, entry);
        }
        finally { shell.Close(); }
    }

    [AvaloniaFact]
    public void DataGridRendersAndSelectsRowsWithTheUpdatedRuntime()
    {
        var rows = new[] { new NavigationState { ActiveTab = "Rendered ingredient" },
            new NavigationState { ActiveTab = "Second ingredient" } };
        var grid = new DataGrid { AutoGenerateColumns = true, ItemsSource = rows };
        var window = new Window { Width = 600, Height = 400, Content = grid };
        window.Show();
        try
        {
            window.UpdateLayout();
            Assert.Single(grid.Columns);
            Assert.Contains(grid.GetVisualDescendants().OfType<TextBlock>(),
                text => text.Text == "Rendered ingredient");
            grid.SelectedItem = rows[1];
            Assert.Same(rows[1], grid.SelectedItem);
        }
        finally { window.Close(); }
    }

    [Fact]
    public void ConnectionTimeoutHasActionableMessageAndUnknownCodesKeepFallback()
    {
        foreach (var number in new[] { -2, 258 })
        {
            Assert.True(DbExceptionMapper.IsKnown(number));
            var message = DbExceptionMapper.ToFriendlyMessage(number);
            Assert.Contains("timpul de asteptare", message);
            Assert.Contains("reteaua", message);
        }
        Assert.True(DbExceptionMapper.IsKnown(2812));
        Assert.Contains("necesita actualizare", DbExceptionMapper.ToFriendlyMessage(2812));
        Assert.False(DbExceptionMapper.IsKnown(123456));
        Assert.Equal("Eroare neasteptata la baza de date.",
            DbExceptionMapper.ToFriendlyMessage(123456));
        Assert.Contains("Autentificarea", DbExceptionMapper.ToFriendlyMessage(18456));
    }

    [Fact]
    public async Task RecoveryCannotResetCredentials()
    {
        var vm = new ForgotPasswordViewModel { Identifier = "someone", Email = "someone@example.com" };
        Assert.False(await vm.ResetAsync("new-password", "new-password"));
        Assert.True(vm.HasError);
    }

    [Fact]
    public void BCryptUsesSaltAndVerifiesReusedPlaintext()
    {
        var hasher = new BcryptPasswordHasher();
        var one = hasher.Hash("old-password");
        var two = hasher.Hash("old-password");
        Assert.NotEqual(one, two);
        Assert.True(hasher.Verify("old-password", one));
        Assert.False(hasher.Verify("different-password", one));
    }

    [Fact]
    public async Task ImagePipelineProducesBoundedJpegAndThumbnail()
    {
        var path = Path.GetTempFileName();
        try
        {
            using (var image = new SKBitmap(3000, 1500))
            using (var encoded = image.Encode(SKEncodedImageFormat.Png, 100))
                await File.WriteAllBytesAsync(path, encoded.ToArray());
            var processed = await RecipeImageProcessor.LoadAsync(path);
            using var full = SKBitmap.Decode(processed.Image);
            using var thumbnail = SKBitmap.Decode(processed.Thumbnail);
            Assert.Equal(2048, full.Width);
            Assert.Equal(1024, full.Height);
            Assert.Equal(320, thumbnail.Width);
            Assert.Equal(160, thumbnail.Height);
            Assert.Equal(0xff, processed.Image[0]);
            Assert.Equal(0xd8, processed.Image[1]);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task InvalidImageIsRejectedBeforeDatabaseUpload()
    {
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, "this is not an image");
            await Assert.ThrowsAnyAsync<Exception>(() => RecipeImageProcessor.LoadAsync(path));
        }
        finally { File.Delete(path); }
    }

    private sealed class UnavailableDatabase : IDbConnectionFactory
    {
        public Microsoft.Data.SqlClient.SqlConnection Create() =>
            throw new InvalidOperationException("Database unavailable in view-binding regression.");
    }

    private sealed class RecordingNavigation : INavigationService
    {
        public object? CurrentView { get; private set; }
        public event EventHandler? CurrentViewChanged;
        public List<Type> Routes { get; } = new();
        public Task NavigateToAsync<TViewModel>(Action<TViewModel>? prepare = null) where TViewModel : class =>
            NavigateToAsync(typeof(TViewModel));
        public Task NavigateToAsync(object viewModel)
        {
            CurrentView = viewModel;
            Routes.Add((Type)viewModel);
            CurrentViewChanged?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }
    }

    public sealed class NavigationState { public string ActiveTab { get; set; } = "Acasa"; }
}
