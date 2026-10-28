using System.Globalization;
using Avalonia.Data.Converters;
namespace MealPrepApp.Desktop.Converters;
public sealed class EmptyStateConverter:IMultiValueConverter
{
    public object Convert(IList<object?> values,Type targetType,object? parameter,CultureInfo culture)=>values.Count==3 && values[0] is int count && count==0 && values[1] is false && values[2] is false;
}
