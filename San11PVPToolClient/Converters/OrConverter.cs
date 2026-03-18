using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;

namespace San11PVPToolClient.Converters;

public class OrConverter : IMultiValueConverter
{
    public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        bool a = values[0] is bool and true;
        bool b = values[1] is bool and true;
        return a || b;
    }
}
