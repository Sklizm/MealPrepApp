using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml;
using MealPrepApp.Services;

namespace MealPrepApp.Desktop.Localization;

/// <summary>Refreshes a built-in label on language change while leaving the bound identity untouched.</summary>
public sealed class DisplayExtension(string path) : MarkupExtension
{
    public DisplayExtension() : this(".") { }
    public override object ProvideValue(IServiceProvider serviceProvider) => new MultiBinding
    {
        Bindings = { new Binding(path), new Binding(nameof(LocalizationService.Language)) { Source = L.Current } },
        Converter = LabelConverter.Instance
    };
    private sealed class LabelConverter : IMultiValueConverter
    {
        public static LabelConverter Instance { get; } = new();
        public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture) =>
            BuiltInLabels.Display(values.FirstOrDefault() as string);
    }
}
