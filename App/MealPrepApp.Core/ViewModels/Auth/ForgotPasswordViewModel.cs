using CommunityToolkit.Mvvm.ComponentModel;
using MealPrepApp.Data;
using MealPrepApp.Data.Repositories;
using MealPrepApp.Services;

namespace MealPrepApp.ViewModels.Auth;

public sealed partial class ForgotPasswordViewModel : ViewModelBase
{
    private readonly UserRepository _users;
    private readonly IPasswordHasher _hasher;

    [ObservableProperty]
    private string _identifier = "";

    [ObservableProperty]
    private string _email = "";

    public ForgotPasswordViewModel(UserRepository users, IPasswordHasher hasher)
    {
        _users = users;
        _hasher = hasher;
    }

    /// <summary>Returns true when the password was reset. Passwords come from PasswordBoxes.</summary>
    public async Task<bool> ResetAsync(string newPassword, string confirmPassword)
    {
        ClearError();

        var identifier = Identifier.Trim();
        var email = Email.Trim();

        if (string.IsNullOrWhiteSpace(identifier))
        {
            ErrorMessage = "Introdu utilizatorul sau emailul contului.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(email))
        {
            ErrorMessage = "Introdu emailul contului.";
            return false;
        }
        if (newPassword.Length < 6)
        {
            ErrorMessage = "Parola noua trebuie sa aiba cel putin 6 caractere.";
            return false;
        }
        if (newPassword != confirmPassword)
        {
            ErrorMessage = "Parolele noi nu coincid.";
            return false;
        }

        IsBusy = true;
        try
        {
            await _users.ResetForgottenPasswordAsync(identifier, email, _hasher.Hash(newPassword));
            return true;
        }
        catch (AppDbException ex)
        {
            ErrorMessage = ex.FriendlyMessage;
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
