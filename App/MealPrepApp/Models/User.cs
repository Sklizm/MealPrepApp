namespace MealPrepApp.Models;

/// <summary>Login-flow data from <c>sp_GetUserForLogin</c> — includes the hash and lockout state.
/// Reserved for the login flow only; the Profile screen uses <see cref="UserProfile"/>.</summary>
public sealed class UserLoginInfo
{
    public int UserID { get; set; }
    public string Username { get; set; } = "";
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public int FailedLoginCount { get; set; }
    public DateTime? LockedUntil { get; set; }
    public DateTime? LastLoginAt { get; set; }
}

/// <summary>Safe profile data from <c>sp_GetUserProfile</c> — no hash, no lockout state.</summary>
public sealed class UserProfile
{
    public int UserID { get; set; }
    public string Username { get; set; } = "";
    public string Email { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
}
