using Avalonia.Controls;
using System.Collections;
using System.Globalization;
using System.IO;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;

namespace MealPrepApp.Desktop.Converters;

public sealed class InverseBooleanConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool b ? !b : false;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool b ? !b : false;
}

public sealed class NotNullToBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is not null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Avalonia.Data.BindingOperations.DoNothing;
}

public sealed class EmptyCollectionToBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int count) return count == 0;
        if (value is ICollection coll) return coll.Count == 0;
        if (value is IEnumerable enumVal) return !enumVal.Cast<object>().Any();
        return true;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Avalonia.Data.BindingOperations.DoNothing;
}

public sealed class NonEmptyCollectionToBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int count) return count > 0;
        if (value is ICollection coll) return coll.Count > 0;
        if (value is IEnumerable enumVal) return enumVal.Cast<object>().Any();
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Avalonia.Data.BindingOperations.DoNothing;
}

public sealed class StringEqualsConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.Ordinal);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Avalonia.Data.BindingOperations.DoNothing;
}

public sealed class DateTimeToDateTimeOffsetConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is DateTime dt)
        {
            if (dt == DateTime.MinValue) return null;
            return new DateTimeOffset(DateTime.SpecifyKind(dt.Date, DateTimeKind.Unspecified), TimeZoneInfo.Local.GetUtcOffset(dt.Date));
        }
        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is DateTimeOffset dto)
            return DateTime.SpecifyKind(dto.Date, DateTimeKind.Unspecified);
        return DateTime.Today;
    }
}

public sealed class BoolToOpacityConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? 1.0 : 0.4;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Avalonia.Data.BindingOperations.DoNothing;
}

public sealed class IsTodayToBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true && Avalonia.Application.Current?.TryFindResource("Selection",out var brush)==true ? brush : Avalonia.Media.Brushes.Transparent;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Avalonia.Data.BindingOperations.DoNothing;
}

public sealed class CategoryIdToBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key=value is int id ? id switch {1=>"MealBreakfast",2=>"MealLunch",3=>"MealDinner",4=>"MealSnack",5=>"MealDessert",6=>"MealDrink",_=>"MealLunch"} : "MealLunch";
        return Avalonia.Application.Current?.TryFindResource(key,out var brush)==true ? brush : Avalonia.Media.Brushes.DarkGreen;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Avalonia.Data.BindingOperations.DoNothing;
}

