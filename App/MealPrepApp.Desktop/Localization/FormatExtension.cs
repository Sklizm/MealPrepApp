using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml;
using MealPrepApp.Services;

namespace MealPrepApp.Desktop.Localization;

/// <summary>Format typed values in the active language without using localized text as model state.</summary>
public sealed class FormatExtension(string key, string path) : MarkupExtension
{
    public override object ProvideValue(IServiceProvider serviceProvider) => new MultiBinding
    {
        Bindings = { new Binding(path), new Binding(nameof(LocalizationService.Language)) { Source = L.Current } },
        Converter = new Formatter(key)
    };
    private sealed class Formatter(string key) : IMultiValueConverter
    {
        public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            var value = values.FirstOrDefault();
            if (value is null || value == Avalonia.AvaloniaProperty.UnsetValue) return "";
            return key.StartsWith("Count.", StringComparison.Ordinal) && value is IConvertible number
                ? L.Current.Plural(key, number.ToInt64(CultureInfo.InvariantCulture))
                : L.Text(key, value);
        }
    }
}
