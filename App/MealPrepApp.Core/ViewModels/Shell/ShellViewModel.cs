using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MealPrepApp.Services;
using MealPrepApp.ViewModels.Auth;
using MealPrepApp.ViewModels.Ingrediente;
using MealPrepApp.ViewModels.Planificare;
using MealPrepApp.ViewModels.Rapoarte;
using MealPrepApp.ViewModels.Retete;
using Microsoft.Extensions.DependencyInjection;

namespace MealPrepApp.ViewModels.Shell;

/// <summary>
/// Drives sidebar navigation, session feedback and the shared cooking session.
/// Scoped to a single signed-in user.
/// </summary>
public sealed partial class ShellViewModel : ObservableObject, IShellNavigator
{
    private readonly INavigationService _navigation;
    private readonly ISessionService _session;
    private readonly IServiceProvider _services;

    /// <summary>Raised when the user signs out; the shell window returns to the login window.</summary>
    public event EventHandler? SignOutRequested;
    public event EventHandler? SettingsRequested;
    public NotificationCenter Notifications {get;}
    public CookingSession Cooking { get; }
    [RelayCommand] private void ShowSettings() => SettingsRequested?.Invoke(this,EventArgs.Empty);
    [RelayCommand] private Task ResumeCooking() => _navigation.NavigateToAsync(new CookingViewModel(Cooking));

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
        Notifications=services.GetService<NotificationCenter>() ?? new NotificationCenter();
        Cooking=services.GetService<CookingSession>() ?? new CookingSession();
        _navigation.CurrentViewChanged += (_, _) =>
        {
            CurrentView=_navigation.CurrentView;
            ActiveTab=CurrentView switch { AcasaViewModel=>"Acasa", ReteteListViewModel or ReteteDetailViewModel or ReteteEditorViewModel or CookingViewModel=>"Retete", FrigiderViewModel=>"Frigider", ShoppingListViewModel=>"Cumparaturi", IngredienteRootViewModel or IngredienteListViewModel=>"Ingrediente", PlanificareRootViewModel=>"Planificare", RapoarteRootViewModel=>"Rapoarte", _=>"" };
        };
    }

    /// <summary>Called before the shell window is shown — opens the Acasa dashboard.</summary>
    public async Task InitializeAsync()
    {
        await ShowAcasaCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private async Task ShowAcasa()
    {
        StatusText = "Acasa";
        await _navigation.NavigateToAsync<AcasaViewModel>();
    }

    [RelayCommand]
    private async Task ShowRetete()
    {
        StatusText = "Sectiunea Retete";
        await _navigation.NavigateToAsync<ReteteListViewModel>();
    }

    [RelayCommand]
    private async Task ShowIngrediente()
    {
        StatusText = "Sectiunea Ingrediente";
        await _navigation.NavigateToAsync<IngredienteListViewModel>();
    }

    [RelayCommand] private Task ShowFrigider() => _navigation.NavigateToAsync<FrigiderViewModel>();
    [RelayCommand] private Task ShowCumparaturi() => _navigation.NavigateToAsync<ShoppingListViewModel>();
    [RelayCommand]
    private async Task ShowPlanificare()
    {
        StatusText = "Sectiunea Planificare";
        await _navigation.NavigateToAsync<PlanificareRootViewModel>();
    }

    [RelayCommand]
    private async Task ShowRapoarte()
    {
        StatusText = "Rapoarte si statistici";
        await _navigation.NavigateToAsync<RapoarteRootViewModel>();
    }

    [RelayCommand]
    private async Task ShowProfile()
    {
        StatusText = "Profil";
        var profileViewModel = _services.GetRequiredService<ProfileViewModel>();
        profileViewModel.ChangePasswordRequested += (_, _) => ChangePasswordRequested?.Invoke(this, EventArgs.Empty);
        profileViewModel.SignOutRequested += (_, _) => SignOutCommand.Execute(null);
        await _navigation.NavigateToAsync(profileViewModel);
    }

    [RelayCommand]
    private async Task SignOut()
    {
        if(CurrentView is ILeaveGuard guard && !await guard.CanLeaveAsync()) return;
        _session.SignOut();
        SignOutRequested?.Invoke(this, EventArgs.Empty);
    }

    public async Task ShowSectionAsync(string section)
    {
        var parts=section.Split(':',3);
        if(parts[0]=="Retete")
        {
            if (parts.Length > 1)
                await _navigation.NavigateToAsync<ReteteListViewModel>(recipes => recipes.PrepareDestination(
                    parts[1] == "Cauta" ? "Toate" : parts[1], parts.Length > 2 ? parts[2] : ""));
            else await ShowReteteCommand.ExecuteAsync(null);
            return;
        }
        await (parts[0] switch
        {
            "Frigider"=>ShowFrigiderCommand.ExecuteAsync(null),
            "Cumparaturi"=>ShowCumparaturiCommand.ExecuteAsync(null),
            "Ingrediente"=>ShowIngredienteCommand.ExecuteAsync(null),
            "Planificare"=>ShowPlanificareCommand.ExecuteAsync(null),
            "Rapoarte"=>ShowRapoarteCommand.ExecuteAsync(null),
            _=>ShowAcasaCommand.ExecuteAsync(null)
        });
    }
}
