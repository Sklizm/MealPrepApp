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
            ErrorContent = L.Message("Text.EnterYourCurrentPassword");
            return false;
        }
        if (newPassword.Length < 6)
        {
            ErrorContent = L.Message("Text.TheNewPasswordMustContainAtLeast6Characters");
            return false;
        }
        if (newPassword != confirmPassword)
        {
            ErrorContent = L.Message("Text.TheNewPasswordsDoNotMatch");
            return false;
        }

        IsBusy = true;
        try
        {
            // sp_ChangePassword does not verify the current password — do it here first.
            var user = await _users.GetUserForLoginAsync(_session.CurrentUsername);
            if (user is null || !_hasher.Verify(currentPassword, user.PasswordHash))
            {
                ErrorContent = L.Message("Text.TheCurrentPasswordIsIncorrect");
                return false;
            }

            var history = await _users.GetPasswordHistoryAsync(_session.CurrentUserId);
            if (history.Any(hash => _hasher.Verify(newPassword, hash)))
            {
                ErrorContent = L.Message("Text.ThisPasswordWasUsedRecentlyChooseADifferentPassword");
                return false;
            }
            await _users.ChangePasswordAsync(_session.CurrentUserId, _hasher.Hash(newPassword), user.PasswordHash);
            return true;
        }
        catch (AppDbException ex)
        {
            // 50001 → "this password was used recently".
            ErrorContent = ex.LocalizedMessage;
            return false;
        }
        catch (OperationCanceledException) { return false; }
        catch (Exception ex)
        {
            AppDiagnostics.Record(ex);
            ErrorContent = L.Message("Text.TheOperationCouldNotBeCompletedCheckTheAppLog");
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
