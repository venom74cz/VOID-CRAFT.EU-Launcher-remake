using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace VoidCraftLauncher.Converters;

public class RamValueConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int mb)
        {
            if (mb == 0) return "Globální";
            double gb = mb / 1024.0;
            return $"{gb:0.#} GB";
        }
        if (value is double mbDouble)
        {
            if (mbDouble == 0) return "Globální";
            double gb = mbDouble / 1024.0;
            return $"{gb:0.#} GB";
        }
        return value;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
