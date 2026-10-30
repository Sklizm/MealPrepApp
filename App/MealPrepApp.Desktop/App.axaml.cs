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
    private ServiceProvider? _services;
    internal ConnectionSettingsStore ConnectionSettings { get; } = new();
    internal IServiceScope? SessionScope { get; set; }

    public static IServiceProvider Services => ((App)Current!).SessionScope?.ServiceProvider ?? ((App)Current!)._services!;

    public override void Initialize()
    {
        var preferences = new PresentationPreferences();
        L.Current.Language = preferences.Language;
        AvaloniaXamlLoader.Load(this);
        void ApplyTheme() => RequestedThemeVariant=preferences.Theme switch { Appearance.Light=>Avalonia.Styling.ThemeVariant.Light, Appearance.Dark=>Avalonia.Styling.ThemeVariant.Dark, _=>Avalonia.Styling.ThemeVariant.Default };
        void ApplyMotion()
        {
            Resources["MotionDuration"]=preferences.ReducedMotion ? TimeSpan.Zero : TimeSpan.FromMilliseconds(100);
            Resources["MotionEnabled"]=!preferences.ReducedMotion;
            Resources["ReducedMotion"]=preferences.ReducedMotion;
        }
        ApplyTheme(); ApplyMotion();
        preferences.PropertyChanged += (_,e) => { if(e.PropertyName==nameof(preferences.Language)) L.Current.Language=preferences.Language; if(e.PropertyName==nameof(preferences.Theme)) ApplyTheme(); if(e.PropertyName==nameof(preferences.ReducedMotion))ApplyMotion(); };
        Preferences=preferences;
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var loaded = ConnectionSettings.Load();
        if (loaded.NeedsSetup)
        {
            var setup = new ConnectionSetupWindow(ConnectionSettings, connection =>
            {
                ApplyConnection(connection);
                return Task.CompletedTask;
            });
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                desktop.MainWindow = setup;
        }
        else StartServices(loaded.Configuration);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime)
            lifetime.Exit += (_, _) => { SessionScope?.Dispose(); _services?.Dispose(); };
        base.OnFrameworkInitializationCompleted();
    }

    internal void ApplyConnection(string connection)
    {
        var configuration = new ConfigurationBuilder()
            .AddConfiguration(ConnectionSettings.Load().Configuration)
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:MealPrepDB"] = connection })
            .Build();
        StartServices(configuration);
    }

    private void StartServices(IConfiguration configuration)
    {
        var services = new ServiceCollection();
        ConfigureServices(services, configuration);
        var previousServices = _services;
        var previousScope = SessionScope;
        _services = services.BuildServiceProvider();
        SessionScope = null;
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime)
            WindowCoordinator.ShowLogin();
        previousScope?.Dispose();
        previousServices?.Dispose();
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
        services.AddSingleton<ILocalizationService>(L.Current);
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
