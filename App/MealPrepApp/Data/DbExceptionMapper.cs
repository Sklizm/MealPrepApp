namespace MealPrepApp.Data;

/// <summary>
/// Maps SQL Server error numbers to friendly Romanian messages.
/// The DB raises these custom codes from its stored procedures:
///   50000 — invalid input (e.g. pantry quantity must be &gt; 0)
///   50001 — password reused (sp_ChangePassword)
///   50002 — not authorized (ownership check failed)
///   50003 — entity not found
///   50004 — stale row / optimistic concurrency conflict (sp_UpdateRecipe)
/// </summary>
public static class DbExceptionMapper
{
    public static bool IsKnown(int errorNumber) => errorNumber is >= 50000 and <= 50004;

    public static string ToFriendlyMessage(int errorNumber) => errorNumber switch
    {
        50000 => "Date invalide. Verifica valorile introduse.",
        50001 => "Aceasta parola a fost folosita recent. Alege o parola noua.",
        50002 => "Nu ai permisiunea pentru aceasta actiune.",
        50003 => "Elementul nu a fost gasit.",
        50004 => "Reteta a fost modificata in alta sesiune. Reincarca si reincearca.",
        _ => "Eroare neasteptata la baza de date."
    };
}
