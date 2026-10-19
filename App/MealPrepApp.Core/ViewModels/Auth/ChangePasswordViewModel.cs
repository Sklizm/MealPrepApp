using MealPrepApp.Data;
using MealPrepApp.Data.Repositories;
using MealPrepApp.Services;

namespace MealPrepApp.ViewModels.Auth;

public sealed partial class ChangePasswordViewModel : ViewModelBase
{
    private readonly UserRepository _users;
    private readonly IPasswordHasher _hasher;
    private readonly ISessionService _session;

    public ChangePasswordViewModel(UserRepository users, IPasswordHasher hasher, ISessionService session)
    {
        _users = users;
        _hasher = hasher;
        _session = session;
    }

    /// <summary>Returns true when the password was changed. Passwords come from the dialog's
    /// PasswordBoxes rather than data-bound properties.</summary>
    public async Task<bool> ChangeAsync(string currentPassword, string newPassword, string confirmPassword)
    {
        ClearError();

        if (string.IsNullOrEmpty(currentPassword))
        {
            ErrorMessage = "Introdu parola actuala.";
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
            // sp_ChangePassword does not verify the current password — do it here first.
            var user = await _users.GetUserForLoginAsync(_session.CurrentUsername);
            if (user is null || !_hasher.Verify(currentPassword, user.PasswordHash))
            {
                ErrorMessage = "Parola actuala este incorecta.";
                return false;
            }

            await _users.ChangePasswordAsync(_session.CurrentUserId, _hasher.Hash(newPassword));
            return true;
        }
        catch (AppDbException ex)
        {
            // 50001 → "this password was used recently".
            ErrorMessage = ex.FriendlyMessage;
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
