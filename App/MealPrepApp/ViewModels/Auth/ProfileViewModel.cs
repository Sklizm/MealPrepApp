using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MealPrepApp.Data;
using MealPrepApp.Data.Repositories;
using MealPrepApp.Models;
using MealPrepApp.Services;

namespace MealPrepApp.ViewModels.Auth;

/// <summary>
/// Backs the Profile screen. v1 is read-only: the DB exposes no proc to update the email,
/// so the design spec's "editable email" is shown as display-only until such a proc exists.
/// </summary>
public sealed partial class ProfileViewModel : ViewModelBase, IAsyncLoadable
{
    private readonly UserRepository _users;
    private readonly ISessionService _session;

    /// <summary>Raised when the user clicks "Schimba parola"; the shell opens the dialog.</summary>
    public event EventHandler? ChangePasswordRequested;

    /// <summary>Raised when the user clicks "Iesi din cont"; the shell returns to the login window.</summary>
    public event EventHandler? SignOutRequested;

    [ObservableProperty]
    private UserProfile? _profile;

    public ProfileViewModel(UserRepository users, ISessionService session)
    {
        _users = users;
        _session = session;
    }

    [RelayCommand]
    private void ChangePassword() => ChangePasswordRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void SignOut() => SignOutRequested?.Invoke(this, EventArgs.Empty);

    public async Task LoadAsync()
    {
        ClearError();
        IsBusy = true;
        try
        {
            Profile = await _users.GetUserProfileAsync(_session.CurrentUserId);
        }
        catch (AppDbException ex)
        {
            ErrorMessage = ex.FriendlyMessage;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
