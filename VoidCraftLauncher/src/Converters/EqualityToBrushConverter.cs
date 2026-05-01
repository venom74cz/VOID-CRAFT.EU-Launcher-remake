using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using VoidCraftLauncher.Services;

namespace VoidCraftLauncher.Converters;

public class EqualityToBrushConverter : IValueConverter
{
    public static readonly EqualityToBrushConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var match = false;
        if (value == null && parameter == null) match = true;
        else if (value != null && parameter != null) match = value.ToString() == parameter.ToString();

        var brushKey = match ? "PrimaryBBrush" : "TextSecondaryBrush";
        if (Application.Current?.Styles.TryGetResource(brushKey, null, out var resource) == true && resource is IBrush brush)
        {
            return brush;
        }

        return new SolidColorBrush(match ? Colors.White : Colors.Gray);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return default;
    }
}
