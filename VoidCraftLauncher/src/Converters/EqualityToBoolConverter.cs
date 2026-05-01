using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace VoidCraftLauncher.Converters;

public class EqualityToBoolConverter : IValueConverter
{
    public static readonly EqualityToBoolConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null && parameter == null) return true;
        if (value == null || parameter == null) return false;
        return value.ToString() == parameter.ToString();
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b && b) return parameter;
        return default;
    }
}
