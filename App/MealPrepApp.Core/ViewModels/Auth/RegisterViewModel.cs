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
            ErrorContent = validationError;
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
            ErrorContent = ex.ErrorNumber is 2627 or 2601
                ? L.Message("Text.TheUsernameOrEmailIsAlreadyInUse")
                : ex.LocalizedMessage;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            AppDiagnostics.Record(ex);
            ErrorContent = L.Message("Text.TheOperationCouldNotBeCompletedCheckTheAppLog");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private LocalizedMessage? Validate(string password, string confirmPassword)
    {
        if (string.IsNullOrWhiteSpace(Username))
            return L.Message("Text.EnterAUsername");
        if (Username.Trim().Length > 50)
            return L.Message("Text.TheUsernameIsTooLongMaximum50Characters");
        if (string.IsNullOrWhiteSpace(Email) || !Email.Contains('@'))
            return L.Message("Text.EnterAValidEmailAddress");
        if (Email.Trim().Length > 255)
            return L.Message("Text.TheEmailIsTooLongMaximum255Characters");
        if (password.Length < 6)
            return L.Message("Text.ThePasswordMustContainAtLeast6Characters");
        if (password != confirmPassword)
            return L.Message("Text.ThePasswordsDoNotMatch");
        return null;
    }
}
