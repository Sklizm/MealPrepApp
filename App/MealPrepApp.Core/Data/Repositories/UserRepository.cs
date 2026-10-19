using MealPrepApp.Models;

namespace MealPrepApp.Data.Repositories;

/// <summary>Users &amp; auth: wraps sp_RegisterUser, sp_GetUserForLogin, sp_RecordLoginSuccess/Failure,
/// sp_GetUserProfile, sp_ChangePassword, sp_ResetForgottenPassword.</summary>
public sealed class UserRepository : RepositoryBase
{
    public UserRepository(IDbConnectionFactory factory) : base(factory) { }

    /// <summary>Registers a user and returns the new UserID. The caller supplies an already-hashed password.</summary>
    public Task<int> RegisterUserAsync(string username, string email, string passwordHash) =>
        ExecuteScalarProcAsync<int>("sp_RegisterUser",
            new { Username = username, Email = email, PasswordHash = passwordHash });

    /// <summary>Returns the credential + lockout data for a login attempt, or null if no such user.</summary>
    public Task<UserLoginInfo?> GetUserForLoginAsync(string usernameOrEmail) =>
        QuerySingleOrDefaultProcAsync<UserLoginInfo>("sp_GetUserForLogin",
            new { UsernameOrEmail = usernameOrEmail });

    public Task RecordLoginSuccessAsync(int userId) =>
        ExecuteProcAsync("sp_RecordLoginSuccess", new { UserID = userId });

    /// <summary><paramref name="userId"/> may be null when the identifier matched no user.</summary>
    public Task RecordLoginFailureAsync(int? userId, string? attemptedIdentifier) =>
        ExecuteProcAsync("sp_RecordLoginFailure",
            new { UserID = userId, AttemptedIdentifier = attemptedIdentifier });

    public Task<UserProfile?> GetUserProfileAsync(int userId) =>
        QuerySingleOrDefaultProcAsync<UserProfile>("sp_GetUserProfile", new { UserID = userId });

    /// <summary>Changes the password. Throws <see cref="AppDbException"/> with error 50001 if the
    /// new hash matches any of the last five passwords.</summary>
    public Task ChangePasswordAsync(int userId, string newPasswordHash) =>
        ExecuteProcAsync("sp_ChangePassword", new { UserID = userId, NewPasswordHash = newPasswordHash });

    /// <summary>Resets a forgotten password from the login window. Throws <see cref="AppDbException"/>
    /// with error 50005 if the identifier/email pair does not match an account.</summary>
    public Task ResetForgottenPasswordAsync(string usernameOrEmail, string email, string newPasswordHash) =>
        ExecuteProcAsync("sp_ResetForgottenPassword",
            new { UsernameOrEmail = usernameOrEmail, Email = email, NewPasswordHash = newPasswordHash });
}
