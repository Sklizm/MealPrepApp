namespace MealPrepApp.Data;

/// <summary>
/// Maps SQL Server error numbers to friendly Romanian messages.
/// The DB raises these custom codes from its stored procedures:
///   50000 — invalid input (e.g. pantry quantity must be &gt; 0)
///   50001 — password reused (sp_ChangePassword)
///   50002 — not authorized (ownership check failed)
///   50003 — entity not found
///   50004 — stale row / optimistic concurrency conflict (sp_UpdateRecipe)
///   50005 — password reset account not found (sp_ResetForgottenPassword)
/// It also translates the common *native* SQL errors so a constraint violation never reaches
/// the user as an opaque "unexpected" message (e.g. 2627 when the same ingredient is added
/// twice to one recipe).
/// </summary>
public static class DbExceptionMapper
{
    public static bool IsKnown(int errorNumber) =>
        (errorNumber is >= 50000 and <= 50005)
        || errorNumber is 2627 or 2601 or 547 or 515 or 2628 or 8152
            or -2 or 258 or 53 or 11001 or 10060 or 10061 or 18456 or 4060 or 2812;

    public static string ToFriendlyMessage(int errorNumber) => errorNumber switch
    {
        50000 => "Date invalide. Verifica valorile introduse.",
        50001 => "Aceasta parola a fost folosita recent. Alege o parola noua.",
        50002 => "Nu ai permisiunea pentru aceasta actiune.",
        50003 => "Elementul nu a fost gasit.",
        50004 => "Reteta a fost modificata in alta sesiune. Reincarca si reincearca.",
        50005 => "Nu am gasit un cont cu aceste date.",

        // Connection failures may occur before SQL Server receives the operation.
        -2 or 258 => "Conexiunea sau operatia la baza de date a depasit timpul de asteptare. " +
            "Verifica reteaua si disponibilitatea serverului.",
        53 or 11001 or 10060 or 10061 => "Nu s-a putut contacta serverul bazei de date. " +
            "Verifica reteaua si adresa serverului.",
        18456 => "Autentificarea la serverul bazei de date a esuat. Verifica configurarea aplicatiei.",
        4060 => "Baza de date configurata nu este disponibila sau accesul nu este permis.",

        2812 => "Baza de date necesita actualizare pentru aceasta versiune a aplicatiei. " +
            "Contacteaza administratorul.",

        // Native SQL Server errors
        2627 or 2601 => "Aceasta inregistrare exista deja (valoare duplicata).",
        547 => "Date invalide: o valoare incalca o regula a bazei de date.",
        515 => "Lipseste o valoare obligatorie.",
        2628 or 8152 => "Un text introdus este prea lung.",

        _ => "Eroare neasteptata la baza de date."
    };
}
