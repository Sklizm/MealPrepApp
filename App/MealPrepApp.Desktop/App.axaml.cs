using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using MealPrepApp.Data;
using MealPrepApp.Data.Repositories;
using MealPrepApp.Desktop.Services;
using MealPrepApp.Desktop.Views;
using MealPrepApp.Desktop.Views.Auth;
using MealPrepApp.Services;
using MealPrepApp.ViewModels;
using MealPrepApp.ViewModels.Auth;
using MealPrepApp.ViewModels.Ingrediente;
using MealPrepApp.ViewModels.Planificare;
using MealPrepApp.ViewModels.Rapoarte;
using MealPrepApp.ViewModels.Retete;
using MealPrepApp.ViewModels.Shell;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MealPrepApp.Desktop;

public partial class App : Application
{
    private IServiceProvider _services = null!;

    public static IServiceProvider Services => ((App)Current!)._services;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Local.json", optional: true)
            .Build();

        var services = new ServiceCollection();
        ConfigureServices(services, config);
        _services = services.BuildServiceProvider();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var loginWindow = _services.GetRequiredService<LoginWindow>();
            desktop.MainWindow = loginWindow;
            loginWindow.Show();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void ConfigureServices(IServiceCollection services, IConfiguration config)
    {
        var connectionString = config.GetConnectionString("MealPrepDB")
            ?? throw new InvalidOperationException(
                "Connection string 'MealPrepDB' is missing. Set it in appsettings.Local.json.");

        // Data layer
        services.AddSingleton<IDbConnectionFactory>(new SqlConnectionFactory(connectionString));

        // Repositories
        services.AddSingleton<UserRepository>();
        services.AddSingleton<RecipeRepository>();
        services.AddSingleton<IngredientRepository>();
        services.AddSingleton<LookupRepository>();
        services.AddSingleton<MealPlanRepository>();
        services.AddSingleton<FavoriteRepository>();
        services.AddSingleton<PantryRepository>();
        services.AddSingleton<ShoppingListRepository>();
        services.AddSingleton<DashboardRepository>();
        services.AddSingleton<ReportRepository>();
        services.AddSingleton<DraftRepository>();
        services.AddSingleton<NutritionRepository>();

        // Services
        services.AddSingleton<ISessionService, SessionService>();
        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<IDialogService, DesktopDialogService>();
        services.AddSingleton<IFilePickerService, DesktopFilePickerService>();

        // Shell
        services.AddSingleton<ShellViewModel>();
        services.AddSingleton<IShellNavigator>(sp => sp.GetRequiredService<ShellViewModel>());

        // ViewModels
        services.AddTransient<LoginViewModel>();
        services.AddTransient<RegisterViewModel>();
        services.AddTransient<ProfileViewModel>();
        services.AddTransient<ChangePasswordViewModel>();
        services.AddTransient<ForgotPasswordViewModel>();
        services.AddTransient<AcasaViewModel>();
        services.AddTransient<ReteteListViewModel>();
        services.AddTransient<ReteteDetailViewModel>();
        services.AddTransient<ReteteEditorViewModel>();
        services.AddTransient<IngredienteRootViewModel>();
        services.AddTransient<IngredienteListViewModel>();
        services.AddTransient<IngredientAddDialogViewModel>();
        services.AddTransient<IngredientNutritionDialogViewModel>();
        services.AddTransient<FrigiderViewModel>();
        services.AddTransient<PantryItemDialogViewModel>();
        services.AddTransient<ShoppingListViewModel>();
        services.AddTransient<PlanificareRootViewModel>();
        services.AddTransient<PlanificareMonthlyViewModel>();
        services.AddTransient<PlanificareWeeklyViewModel>();
        services.AddTransient<PlanMealDialogViewModel>();
        services.AddTransient<RapoarteRootViewModel>();
        services.AddTransient<StatisticiLunareViewModel>();
        services.AddTransient<PlanSaptamanalPrintViewModel>();
        services.AddTransient<ListaCumparaturiPrintViewModel>();

        // Windows
        services.AddTransient<LoginWindow>();
        services.AddTransient<ShellWindow>();
    }
}
