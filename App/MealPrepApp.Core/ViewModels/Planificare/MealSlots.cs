using System.Globalization;

namespace MealPrepApp.ViewModels.Planificare;

/// <summary>
/// Meal-slot conventions shared by the monthly and weekly planners. Slots are the first four
/// Categories (Breakfast / Lunch / Dinner / Snack, CategoryID 1–4); the weekly grid renders
/// these as its four columns. Dessert / Drink (5–6) can still be planned and show in the
/// monthly view, but get no weekly column.
/// </summary>
public static class MealSlots
{
    /// <summary>The four meal slots rendered as weekly columns, in display order.</summary>
    public static readonly int[] WeeklyColumnIds = { 1, 2, 3, 4 };

    /// <summary>Default slot when none is supplied (Breakfast).</summary>
    public const int DefaultSlotId = 1;

    private static readonly CultureInfo Ro = CultureInfo.GetCultureInfo("ro-RO");

    /// <summary>e.g. "mai 2026" (Romanian month name + year), capitalised.</summary>
    public static string MonthLabel(int year, int month)
    {
        var date = new DateTime(year, month, 1);
        var text = date.ToString("MMMM yyyy", Ro);
        return char.ToUpper(text[0], Ro) + text[1..];
    }

    /// <summary>e.g. "12 – 18 mai" for a Monday-started week.</summary>
    public static string WeekLabel(DateTime weekStart)
    {
        var end = weekStart.AddDays(6);
        return weekStart.Month == end.Month
            ? $"{weekStart.Day} – {end.Day} {end.ToString("MMMM", Ro)}"
            : $"{weekStart.Day} {weekStart.ToString("MMM", Ro)} – {end.Day} {end.ToString("MMM", Ro)}";
    }

    /// <summary>e.g. "Lu 12" — two-letter Romanian weekday abbreviation + day number.</summary>
    public static string DayRowLabel(DateTime date) =>
        $"{Capitalize(date.ToString("ddd", Ro))} {date.Day}";

    /// <summary>The seven weekday headers for the monthly grid, Monday-first: Lu Ma Mi Jo Vi Sâ Du.</summary>
    public static string[] WeekdayHeaders() =>
        new[] { "Lu", "Ma", "Mi", "Jo", "Vi", "Sâ", "Du" };

    /// <summary>Monday of the week containing <paramref name="date"/>.</summary>
    public static DateTime MondayOf(DateTime date)
    {
        int offset = ((int)date.DayOfWeek + 6) % 7; // Sunday=0 → 6, Monday=1 → 0, …
        return date.Date.AddDays(-offset);
    }

    private static string Capitalize(string s) =>
        string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0], Ro) + s[1..];
}
