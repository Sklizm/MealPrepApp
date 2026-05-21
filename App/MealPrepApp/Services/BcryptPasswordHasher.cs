namespace MealPrepApp.Services;

/// <summary>
/// BCrypt-based password hashing. Each hash carries its own random salt, so verification
/// uses <see cref="BCrypt.Net.BCrypt.Verify"/> rather than re-hashing and string-comparing.
///
/// Note: because the salt is random, the same password hashes to a different string each
/// time. <c>sp_ChangePassword</c>'s reuse check (50001) does an exact hash-string match, so
/// it will not actually detect a reused password under this scheme — that DB feature assumes
/// deterministic hashing. The 50001 code path is still handled in the UI in case the DB or
/// hashing strategy changes later.
/// </summary>
public sealed class BcryptPasswordHasher : IPasswordHasher
{
    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password);

    public bool Verify(string password, string hash) => BCrypt.Net.BCrypt.Verify(password, hash);
}
