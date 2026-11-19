using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml;
using MealPrepApp.Services;

namespace MealPrepApp.Desktop.Localization;

public sealed class NumberExtension(string path) : MarkupExtension
{
    public string Style { get; set; } = "Quantity";
    public override object ProvideValue(IServiceProvider serviceProvider) => new MultiBinding
    {
        Bindings = { new Binding(path), new Binding(nameof(LocalizationService.Language)) { Source = L.Current } },
        Converter = new Formatter(Style)
    };

    private sealed class Formatter(string style) : IMultiValueConverter
    {
        public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture) =>
            Format(values.FirstOrDefault(), style);
    }

    internal static string Format(object? value, string style)
    {
        var number = value switch { decimal d => d, int i => i, long l => l, _ => (decimal?)null };
        return style switch
        {
            "Calories" => PresentationValues.Calories(number),
            "Calendar" => PresentationValues.Calendar(value is DateTime date ? date : null),
            "Event" => value is DateTimeOffset offset
                ? PresentationValues.Event(offset.UtcDateTime)
                : PresentationValues.Event(value is DateTime time ? time : null),
            _ => PresentationValues.Quantity(number)
        };
    }
}
