using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MealPrepApp.Services;
using MealPrepApp.ViewModels.Auth;
using MealPrepApp.ViewModels.Ingrediente;
using MealPrepApp.ViewModels.Planificare;
using MealPrepApp.ViewModels.Retete;
using Microsoft.Extensions.DependencyInjection;

namespace MealPrepApp.ViewModels.Shell;

/// <summary>
/// Drives the application shell: the top tab strip, the content region, the status bar,
/// and the user dropdown. Singleton — there is exactly one shell per session.
/// </summary>
public sealed partial class ShellViewModel : ObservableObject, IShellNavigator
{
    private readonly INavigationService _navigation;
    private readonly ISessionService _session;
    private readonly IServiceProvider _services;

    /// <summary>Raised when the user signs out; the shell window returns to the login window.</summary>
    public event EventHandler? SignOutRequested;

    /// <summary>Raised when the user asks to change their password; the shell window opens the dialog.</summary>
    public event EventHandler? ChangePasswordRequested;

    [ObservableProperty]
    private object? _currentView;

    [ObservableProperty]
    private string _activeTab = "Acasa";

    [ObservableProperty]
    private string _statusText = "Acasa";

    public string CurrentUsername => _session.CurrentUsername;

    public ShellViewModel(INavigationService navigation, ISessionService session, IServiceProvider services)
    {
        _navigation = navigation;
        _session = session;
        _services = services;
        _navigation.CurrentViewChanged += (_, _) => CurrentView = _navigation.CurrentView;
    }

    /// <summary>Called once when the shell window loads — opens the Acasa dashboard.</summary>
    public Task InitializeAsync() => ShowAcasaCommand.ExecuteAsync(null);

    [RelayCommand]
    private async Task ShowAcasa()
    {
        ActiveTab = "Acasa";
        StatusText = "Acasa";
        await _navigation.NavigateToAsync<AcasaViewModel>();
    }

    [RelayCommand]
    private async Task ShowRetete()
    {
        ActiveTab = "Retete";
        StatusText = "Sectiunea Retete";
        await _navigation.NavigateToAsync<ReteteListViewModel>();
    }

    [RelayCommand]
    private async Task ShowIngrediente()
    {
        ActiveTab = "Ingrediente";
        StatusText = "Sectiunea Ingrediente";
        await _navigation.NavigateToAsync<IngredienteRootViewModel>();
    }

    [RelayCommand]
    private async Task ShowPlanificare()
    {
        ActiveTab = "Planificare";
        StatusText = "Sectiunea Planificare";
        await _navigation.NavigateToAsync<PlanificareRootViewModel>();
    }

    [RelayCommand]
    private async Task ShowRapoarte()
    {
        ActiveTab = "Rapoarte";
        StatusText = "Rapoarte si statistici";
        await _navigation.NavigateToAsync(new PlaceholderViewModel("Rapoarte"));
    }

    [RelayCommand]
    private async Task ShowProfile()
    {
        ActiveTab = "";
        StatusText = "Profil";
        var profileViewModel = _services.GetRequiredService<ProfileViewModel>();
        profileViewModel.ChangePasswordRequested += (_, _) => ChangePasswordRequested?.Invoke(this, EventArgs.Empty);
        profileViewModel.SignOutRequested += (_, _) => SignOutCommand.Execute(null);
        await _navigation.NavigateToAsync(profileViewModel);
    }

    [RelayCommand]
    private void SignOut()
    {
        _session.SignOut();
        SignOutRequested?.Invoke(this, EventArgs.Empty);
    }

    public Task ShowSectionAsync(string section) => section switch
    {
        "Retete" => ShowReteteCommand.ExecuteAsync(null),
        "Ingrediente" => ShowIngredienteCommand.ExecuteAsync(null),
        "Planificare" => ShowPlanificareCommand.ExecuteAsync(null),
        "Rapoarte" => ShowRapoarteCommand.ExecuteAsync(null),
        _ => ShowAcasaCommand.ExecuteAsync(null),
    };
}
