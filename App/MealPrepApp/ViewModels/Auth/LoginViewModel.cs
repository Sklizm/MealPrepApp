using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MealPrepApp.Data;
using MealPrepApp.Data.Repositories;
using MealPrepApp.Services;

namespace MealPrepApp.ViewModels.Auth;

public sealed partial class LoginViewModel : ViewModelBase
{
    private readonly UserRepository _users;
    private readonly IPasswordHasher _hasher;
    private readonly ISessionService _session;

    /// <summary>Raised when the user clicks the "Inregistreaza-te" link.</summary>
    public event EventHandler? SwitchToRegisterRequested;

    /// <summary>Raised when the user clicks the forgot-password link.</summary>
    public event EventHandler? ForgotPasswordRequested;

    /// <summary>Raised after a successful sign-in; the host window opens the shell.</summary>
    public event EventHandler? LoginSucceeded;

    [ObservableProperty]
    private string _identifier = "";

    public LoginViewModel(UserRepository users, IPasswordHasher hasher, ISessionService session)
    {
        _users = users;
        _hasher = hasher;
        _session = session;
    }

    [RelayCommand]
    private void SwitchToRegister() => SwitchToRegisterRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void ForgotPassword() => ForgotPasswordRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>The password is passed in from the view's PasswordBox rather than data-bound.</summary>
    public async Task LoginAsync(string password)
    {
        ClearError();

        var identifier = Identifier.Trim();
        if (string.IsNullOrEmpty(identifier) || string.IsNullOrEmpty(password))
        {
            ErrorMessage = "Introdu utilizatorul si parola.";
            return;
        }

        IsBusy = true;
        try
        {
            var user = await _users.GetUserForLoginAsync(identifier);
            if (user is null)
            {
                // Audit the attempt even though no user matched.
                await _users.RecordLoginFailureAsync(null, identifier);
                ErrorMessage = "Utilizator sau parola incorecte.";
                return;
            }

            if (user.LockedUntil is { } lockedUntil && lockedUntil > DateTime.UtcNow)
            {
                ErrorMessage = $"Cont blocat temporar. Reincearca dupa ora {lockedUntil.ToLocalTime():HH:mm}.";
                return;
            }

            if (!_hasher.Verify(password, user.PasswordHash))
            {
                await _users.RecordLoginFailureAsync(user.UserID, identifier);
                ErrorMessage = "Utilizator sau parola incorecte.";
                return;
            }

            await _users.RecordLoginSuccessAsync(user.UserID);
            _session.SignIn(user.UserID, user.Username);
            LoginSucceeded?.Invoke(this, EventArgs.Empty);
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
