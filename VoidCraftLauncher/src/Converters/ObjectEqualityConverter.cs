using System;
using System.Globalization;
using System.Collections.Generic;
using Avalonia.Data.Converters;

namespace VoidCraftLauncher.Converters;

public class ObjectEqualityConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values == null || values.Count < 2)
            return false;

        return ReferenceEquals(values[0], values[1]) || Equals(values[0], values[1]);
    }
}
