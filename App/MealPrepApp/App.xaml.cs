using System.Windows;
using MealPrepApp.Data;
using MealPrepApp.Data.Repositories;
using MealPrepApp.Services;
using MealPrepApp.ViewModels;
using MealPrepApp.ViewModels.Auth;
using MealPrepApp.ViewModels.Ingrediente;
using MealPrepApp.ViewModels.Planificare;
using MealPrepApp.ViewModels.Rapoarte;
using MealPrepApp.ViewModels.Retete;
using MealPrepApp.ViewModels.Shell;
using MealPrepApp.Views;
using MealPrepApp.Views.Auth;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MealPrepApp;

public partial class App : Application
{
    private IServiceProvider _services = null!;

    /// <summary>Service locator for the rare spots that cannot use constructor injection (e.g. XAML-created windows).</summary>
    public static IServiceProvider Services => ((App)Current)._services;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Local.json", optional: true)
            .Build();

        var services = new ServiceCollection();
        ConfigureServices(services, config);
        _services = services.BuildServiceProvider();

        _services.GetRequiredService<LoginWindow>().Show();
    }

    private static void ConfigureServices(IServiceCollection services, IConfiguration config)
    {
        var connectionString = config.GetConnectionString("MealPrepDB")
            ?? throw new InvalidOperationException(
                "Connection string 'MealPrepDB' is missing. Set it in appsettings.Local.json.");

        // Data layer
        services.AddSingleton<IDbConnectionFactory>(new SqlConnectionFactory(connectionString));

        // Repositories — stateless, safe as singletons
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

        // Services
        services.AddSingleton<ISessionService, SessionService>();
        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<IDialogService, DialogService>();

        // Shell — one per session; ShellViewModel is also the IShellNavigator.
        services.AddSingleton<ShellViewModel>();
        services.AddSingleton<IShellNavigator>(sp => sp.GetRequiredService<ShellViewModel>());

        // ViewModels
        services.AddTransient<LoginViewModel>();
        services.AddTransient<RegisterViewModel>();
        services.AddTransient<ProfileViewModel>();
        services.AddTransient<ChangePasswordViewModel>();
        services.AddTransient<AcasaViewModel>();
        services.AddTransient<ReteteListViewModel>();
        services.AddTransient<ReteteDetailViewModel>();
        services.AddTransient<ReteteEditorViewModel>();
        services.AddTransient<IngredienteRootViewModel>();
        services.AddTransient<IngredienteListViewModel>();
        services.AddTransient<IngredientAddDialogViewModel>();
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

        // Windows / Views
        services.AddTransient<LoginWindow>();
        services.AddTransient<ChangePasswordDialog>();
        services.AddTransient<ShellWindow>();
    }
}
