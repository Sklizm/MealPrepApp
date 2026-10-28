namespace MealPrepApp.Services;

/// <summary>
/// BCrypt-based password hashing. Each hash carries its own random salt, so verification
/// uses <see cref="BCrypt.Net.BCrypt.Verify"/> rather than re-hashing and string-comparing.
///
/// Password history must be verified with BCrypt.Verify, never by comparing salted hash strings.
/// /// </summary>
public sealed class BcryptPasswordHasher : IPasswordHasher
{
    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password);

    public bool Verify(string password, string hash) => BCrypt.Net.BCrypt.Verify(password, hash);
}
