using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Text.Json;

namespace MealPrepApp.Services;

public interface ILocalizationService : INotifyPropertyChanged
{
    string Language { get; set; }
    CultureInfo UICulture { get; }
    string this[string key] { get; }
    string Format(string key, params object?[] arguments);
    string Plural(string key, long count);
}

/// <summary>Immutable message identity. Arguments retain their original types across language changes.</summary>
public sealed record LocalizedMessage(string Key, object?[] Arguments, bool IsPlural = false)
{
    public override string ToString() => IsPlural ? L.Current.Plural(Key, (long)Arguments[0]!) : L.Current.Format(Key, Arguments);
}

public sealed class LocalizationService : ILocalizationService
{
    public static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Catalogs { get; } = LoadCatalogs();
    private string _language = "en";
    public event PropertyChangedEventHandler? PropertyChanged;
    public string Language
    {
        get => _language;
        set
        {
            var normalized = Normalize(value);
            var changed = _language != normalized;
            _language = normalized;
            // UI language changes never alter regional numeric input or date order.
            CultureInfo.DefaultThreadCurrentUICulture = UICulture;
            CultureInfo.CurrentUICulture = UICulture;
            var regional = (CultureInfo)CultureInfo.CurrentCulture.Clone();
            var names = UICulture.DateTimeFormat;
            regional.DateTimeFormat.DayNames = names.DayNames;
            regional.DateTimeFormat.AbbreviatedDayNames = names.AbbreviatedDayNames;
            regional.DateTimeFormat.ShortestDayNames = names.ShortestDayNames;
            regional.DateTimeFormat.MonthNames = names.MonthNames;
            regional.DateTimeFormat.AbbreviatedMonthNames = names.AbbreviatedMonthNames;
            regional.DateTimeFormat.MonthGenitiveNames = names.MonthGenitiveNames;
            regional.DateTimeFormat.AbbreviatedMonthGenitiveNames = names.AbbreviatedMonthGenitiveNames;
            CultureInfo.CurrentCulture = regional;
            CultureInfo.DefaultThreadCurrentCulture = regional;
            if (!changed) return;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Language)));
        }
    }
    public static string Normalize(string? code) => code is "en" or "ru" or "ro" ? code : "en";
    public CultureInfo UICulture => CultureInfo.GetCultureInfo(Language switch { "ru" => "ru-RU", "ro" => "ro-RO", _ => "en-US" });
    public string this[string key] => Catalogs[Language].TryGetValue(key, out var text) ? text : throw new KeyNotFoundException($"Missing localization key: {key}");
    public string Format(string key, params object?[] arguments) => string.Format(CultureInfo.CurrentCulture, this[key], arguments);
    public string Plural(string key, long count)
    {
        var n = Math.Abs(count);
        var form = Language switch
        {
            "ru" => n % 10 == 1 && n % 100 != 11 ? "One" : n % 10 is >= 2 and <= 4 && n % 100 is not (>= 12 and <= 14) ? "Few" : "Many",
            "ro" => n == 1 ? "One" : n == 0 || n % 100 is >= 1 and <= 19 ? "Few" : "Many",
            _ => n == 1 ? "One" : "Many"
        };
        return Format(key + "." + form, count);
    }
    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> LoadCatalogs()
    {
        var result = new Dictionary<string, IReadOnlyDictionary<string, string>>();
        foreach (var language in new[] { "en", "ru", "ro" })
        {
            using var stream = typeof(LocalizationService).Assembly.GetManifestResourceStream($"MealPrepApp.Localization.Catalogs.{language}.json")!;
            result[language] = JsonSerializer.Deserialize<Dictionary<string, string>>(stream)!;
        }
        return result;
    }
}

/// <summary>Process language used by shared presentation code. Legacy WPF explicitly selects Romanian.</summary>
public static class L
{
    public static LocalizationService Current { get; } = new();
    public static string Text(string key, params object?[] arguments) => Current.Format(key, arguments);
    public static LocalizedMessage Count(string key, long count) => new(key, new object?[] { count }, true);
    public static LocalizedMessage Message(string key, params object?[] arguments) => new(key, arguments);

    // Subscribers are weakly held, so cached or abandoned presenters cannot be rooted by the process service.
    public static IDisposable Subscribe<T>(T target, Action<T> changed) where T : class
    {
        var weak = new WeakReference<T>(target);
        PropertyChangedEventHandler? handler = null;
        handler = (_, _) => { if (weak.TryGetTarget(out var live)) changed(live); else Current.PropertyChanged -= handler; };
        Current.PropertyChanged += handler;
        return new Subscription(() => Current.PropertyChanged -= handler);
    }
    private sealed class Subscription(Action unsubscribe) : IDisposable { public void Dispose() => unsubscribe(); }
}
