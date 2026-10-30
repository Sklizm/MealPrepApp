using System.Globalization;
using MealPrepApp.Services;

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

    private static CultureInfo DisplayCulture => L.Current.UICulture;

    /// <summary>e.g. "mai 2026" (localized month name + year), capitalised.</summary>
    public static string MonthLabel(int year, int month)
    {
        var date = new DateTime(year, month, 1);
        var text = date.ToString("MMMM yyyy", DisplayCulture);
        return char.ToUpper(text[0], DisplayCulture) + text[1..];
    }

    /// <summary>e.g. "12 – 18 mai" for a Monday-started week.</summary>
    public static string WeekLabel(DateTime weekStart)
    {
        var end = weekStart.AddDays(6);
        var pattern = CultureInfo.CurrentCulture.DateTimeFormat.ShortDatePattern;
        var monthFirst = pattern.IndexOf('M') < pattern.IndexOf('d');
        string Month(DateTime value) => value.ToString("MMM", DisplayCulture);
        string Day(DateTime value) => monthFirst ? $"{Month(value)} {value.Day}" : $"{value.Day} {Month(value)}";
        return weekStart.Month == end.Month
            ? monthFirst ? $"{Month(weekStart)} {weekStart.Day} – {end.Day}" : $"{weekStart.Day} – {end.Day} {Month(end)}"
            : $"{Day(weekStart)} – {Day(end)}";
    }

    /// <summary>e.g. "Lu 12" — localized weekday abbreviation + day number.</summary>
    public static string DayRowLabel(DateTime date) =>
        $"{Capitalize(date.ToString("ddd", DisplayCulture))} {date.Day}";

    /// <summary>The seven weekday headers for the monthly grid, Monday-first.</summary>
    public static string[] WeekdayHeaders() =>
        Enumerable.Range(0, 7).Select(day => Capitalize(DisplayCulture.DateTimeFormat.GetAbbreviatedDayName((DayOfWeek)((day + 1) % 7)))).ToArray();

    /// <summary>Monday of the week containing <paramref name="date"/>.</summary>
    public static DateTime MondayOf(DateTime date)
    {
        int offset = ((int)date.DayOfWeek + 6) % 7; // Sunday=0 → 6, Monday=1 → 0, …
        return date.Date.AddDays(-offset);
    }

    private static string Capitalize(string s) =>
        string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0], DisplayCulture) + s[1..];
}
