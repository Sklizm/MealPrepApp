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
    public PresentationPreferences Preferences { get; private set; }=null!;
    private ServiceProvider _services = null!;
    internal IServiceScope? SessionScope { get; set; }

    public static IServiceProvider Services => ((App)Current!).SessionScope?.ServiceProvider ?? ((App)Current!)._services;

    public override void Initialize()
    {
        var culture=System.Globalization.CultureInfo.GetCultureInfo("ro-RO");
        System.Globalization.CultureInfo.DefaultThreadCurrentCulture=culture;
        System.Globalization.CultureInfo.DefaultThreadCurrentUICulture=culture;
        AvaloniaXamlLoader.Load(this);
        var preferences=new PresentationPreferences();
        void ApplyTheme() => RequestedThemeVariant=preferences.Theme switch { Appearance.Light=>Avalonia.Styling.ThemeVariant.Light, Appearance.Dark=>Avalonia.Styling.ThemeVariant.Dark, _=>Avalonia.Styling.ThemeVariant.Default };
        void ApplyMotion()
        {
            Resources["MotionDuration"]=preferences.ReducedMotion ? TimeSpan.Zero : TimeSpan.FromMilliseconds(100);
            Resources["MotionEnabled"]=!preferences.ReducedMotion;
        }
        ApplyTheme(); ApplyMotion();
        preferences.PropertyChanged += (_,e) => { if(e.PropertyName==nameof(preferences.Theme)) ApplyTheme(); if(e.PropertyName==nameof(preferences.ReducedMotion))ApplyMotion(); };
        Preferences=preferences;
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var configBuilder = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false);

        // Probe multiple locations for appsettings.Local.json so it loads reliably
        // whether running via 'dotnet run' from root, IDE, or compiled bundle.
        string[] localSettingsCandidates =
        [
            Path.Combine(AppContext.BaseDirectory, "appsettings.Local.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "appsettings.Local.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "App", "MealPrepApp.Desktop", "appsettings.Local.json")
        ];

        var localPath = localSettingsCandidates.FirstOrDefault(File.Exists);
        if (localPath != null)
        {
            configBuilder.AddJsonFile(localPath, optional: true, reloadOnChange: false);
        }

        var userSettings = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MealPrepApp", "appsettings.Local.json");
        configBuilder.AddJsonFile(userSettings, optional: true);
        var config = configBuilder.Build();

        var services = new ServiceCollection();
        ConfigureServices(services, config);
        _services = services.BuildServiceProvider();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            WindowCoordinator.ShowLogin();
        }

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime)
            lifetime.Exit += (_, _) => { SessionScope?.Dispose(); _services.Dispose(); };
        base.OnFrameworkInitializationCompleted();
    }

    private static void ConfigureServices(IServiceCollection services, IConfiguration config)
    {
        var connectionString = config.GetConnectionString("MealPrepDB")
            ?? throw new InvalidOperationException(
                "Connection string 'MealPrepDB' is missing. Set it in appsettings.Local.json.");

        // SqlClient validates aliases, IPv6 and quoting without regex rewriting.
        _ = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connectionString);

        services.AddSingleton(config);
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
        services.AddSingleton(Current is App app ? app.Preferences : new PresentationPreferences());
        services.AddScoped<CookingSession>();
        services.AddScoped<NotificationCenter>();
        services.AddScoped<ISessionService, SessionService>();
        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
        services.AddScoped<INavigationService, NavigationService>();
        services.AddSingleton<IDialogService, DesktopDialogService>();
        services.AddSingleton<IFilePickerService, DesktopFilePickerService>();

        // Shell
        services.AddScoped<ShellViewModel>();
        services.AddScoped<IShellNavigator>(sp => sp.GetRequiredService<ShellViewModel>());

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
