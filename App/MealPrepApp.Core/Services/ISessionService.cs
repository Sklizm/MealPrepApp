namespace MealPrepApp.Services;

/// <summary>Holds the signed-in user for the lifetime of the app session.
/// Every repository call that needs a UserID reads it from here.</summary>
public interface ISessionService
{
    int CurrentUserId { get; }
    string CurrentUsername { get; }
    bool IsAuthenticated { get; }

    void SignIn(int userId, string username);
    void SignOut();
}
