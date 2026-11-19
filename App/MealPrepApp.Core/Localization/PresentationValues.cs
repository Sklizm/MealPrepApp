using System.Globalization;

namespace MealPrepApp.Services;

public static class PresentationValues
{
    public static decimal? ReadDecimal(string? text) =>
        decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint |
            NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite, CultureInfo.CurrentCulture, out var parsed)
            ? parsed : null;

    public static string Quantity(decimal? value, CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentCulture;
        if (value is not { } quantity) return L.Text("Value.Unknown");
        if (quantity != 0 && Math.Abs(quantity) < 0.01m)
        {
            var threshold = 0.01m.ToString("0.##", culture);
            return quantity < 0 ? $"{culture.NumberFormat.NegativeSign}(< {threshold})" : $"< {threshold}";
        }
        return decimal.Round(quantity, 2, MidpointRounding.AwayFromZero).ToString("0.##", culture);
    }

    public static string Calories(decimal? value, CultureInfo? culture = null) => value is { } calories
        ? decimal.Round(calories, 0, MidpointRounding.AwayFromZero).ToString("0", culture ?? CultureInfo.CurrentCulture)
        : L.Text("Value.Unknown");

    public static string Calendar(DateTime? value) => value?.ToString("d", CultureInfo.CurrentCulture)
        ?? L.Text("Value.Unknown");

    public static string Event(DateTime? value)
    {
        if (value is null || value == DateTime.MinValue) return L.Text("Value.Unknown");
        var time = value.Value;
        if (time.Kind == DateTimeKind.Unspecified) time = DateTime.SpecifyKind(time, DateTimeKind.Utc);
        return time.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
    }
}
