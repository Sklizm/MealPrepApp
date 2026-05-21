namespace MealPrepApp.Services;

public sealed class SessionService : ISessionService
{
    private int? _userId;

    public int CurrentUserId =>
        _userId ?? throw new InvalidOperationException("No user is signed in.");

    public string CurrentUsername { get; private set; } = "";

    public bool IsAuthenticated => _userId.HasValue;

    public void SignIn(int userId, string username)
    {
        _userId = userId;
        CurrentUsername = username;
    }

    public void SignOut()
    {
        _userId = null;
        CurrentUsername = "";
    }
}
