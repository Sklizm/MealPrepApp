using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml;
using MealPrepApp.Services;

namespace MealPrepApp.Desktop.Localization;

/// <summary>Format typed values in the active language without using localized text as model state.</summary>
public sealed class FormatExtension(string key, string path) : MarkupExtension
{
    public string? Style { get; set; }
    public string? UnknownKey { get; set; }
    public override object ProvideValue(IServiceProvider serviceProvider) => new MultiBinding
    {
        Bindings = { new Binding(path), new Binding(nameof(LocalizationService.Language)) { Source = L.Current } },
        Converter = new Formatter(key, Style, UnknownKey)
    };
    private sealed class Formatter(string key, string? style, string? unknownKey) : IMultiValueConverter
    {
        public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            var value = values.FirstOrDefault();
            if (value is null || value == Avalonia.AvaloniaProperty.UnsetValue)
                return unknownKey is null ? "" : L.Text(unknownKey);
            if (style is not null) return L.Text(key, NumberExtension.Format(value, style));
            return key.StartsWith("Count.", StringComparison.Ordinal) && value is IConvertible number
                ? L.Current.Plural(key, number.ToInt64(CultureInfo.InvariantCulture))
                : L.Text(key, value);
        }
    }
}
