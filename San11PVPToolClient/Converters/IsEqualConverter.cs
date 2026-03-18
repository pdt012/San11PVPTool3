using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;

namespace San11PVPToolClient.Converters;

public class IsEqualConverter : IMultiValueConverter
{
    public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 2)
            return false;

        bool result = Equals(values[0], values[1]);

        if (parameter?.ToString() == "Not")
            result = !result;

        return result;
    }
}
