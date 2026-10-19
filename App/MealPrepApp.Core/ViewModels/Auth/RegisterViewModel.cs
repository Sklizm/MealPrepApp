using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MealPrepApp.Data;
using MealPrepApp.Data.Repositories;
using MealPrepApp.Services;

namespace MealPrepApp.ViewModels.Auth;

public sealed partial class RegisterViewModel : ViewModelBase
{
    private readonly UserRepository _users;
    private readonly IPasswordHasher _hasher;

    /// <summary>Raised when the user clicks the "Conecteaza-te" link.</summary>
    public event EventHandler? SwitchToLoginRequested;

    /// <summary>Raised after a successful registration; the host returns to the login view.</summary>
    public event EventHandler? RegistrationSucceeded;

    [ObservableProperty]
    private string _username = "";

    [ObservableProperty]
    private string _email = "";

    public RegisterViewModel(UserRepository users, IPasswordHasher hasher)
    {
        _users = users;
        _hasher = hasher;
    }

    [RelayCommand]
    private void SwitchToLogin() => SwitchToLoginRequested?.Invoke(this, EventArgs.Empty);

    public async Task RegisterAsync(string password, string confirmPassword)
    {
        ClearError();

        var validationError = Validate(password, confirmPassword);
        if (validationError is not null)
        {
            ErrorMessage = validationError;
            return;
        }

        IsBusy = true;
        try
        {
            var hash = _hasher.Hash(password);
            await _users.RegisterUserAsync(Username.Trim(), Email.Trim(), hash);
            RegistrationSucceeded?.Invoke(this, EventArgs.Empty);
        }
        catch (AppDbException ex)
        {
            // 2627 / 2601 = unique-constraint violation on Username or Email.
            ErrorMessage = ex.ErrorNumber is 2627 or 2601
                ? "Numele de utilizator sau emailul este deja folosit."
                : ex.FriendlyMessage;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private string? Validate(string password, string confirmPassword)
    {
        if (string.IsNullOrWhiteSpace(Username))
            return "Introdu un nume de utilizator.";
        if (Username.Trim().Length > 50)
            return "Numele de utilizator este prea lung (maxim 50 de caractere).";
        if (string.IsNullOrWhiteSpace(Email) || !Email.Contains('@'))
            return "Introdu o adresa de email valida.";
        if (Email.Trim().Length > 255)
            return "Emailul este prea lung (maxim 255 de caractere).";
        if (password.Length < 6)
            return "Parola trebuie sa aiba cel putin 6 caractere.";
        if (password != confirmPassword)
            return "Parolele nu coincid.";
        return null;
    }
}
