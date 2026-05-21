namespace MealPrepApp.Services;

/// <summary>Hashes and verifies passwords. The DB only stores/compares the string the app
/// produces here — the hashing algorithm is entirely the app's responsibility.</summary>
public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}
