using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml;
using MealPrepApp.Services;

namespace MealPrepApp.Desktop.Localization;

/// <summary>Explicit source keeps localization independent of a control's DataContext.</summary>
public sealed class LocExtension(string key) : MarkupExtension
{
    public override object ProvideValue(IServiceProvider serviceProvider) =>
        new Binding(nameof(LocalizationService.Language)) { Source = L.Current, Mode = BindingMode.OneWay, Converter = new Translation(key) };
    private sealed class Translation(string key) : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => L.Text(key);
        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => BindingOperations.DoNothing;
    }
}
