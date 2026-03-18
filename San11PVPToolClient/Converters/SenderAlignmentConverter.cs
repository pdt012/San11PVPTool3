using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Layout;

namespace San11PVPToolClient.Converters;

public class SenderAlignmentConverter : IMultiValueConverter
{
    public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 2)
            return HorizontalAlignment.Left;

        if (values[0] is not string senderId || values[1] is not string currentUserId)
            return HorizontalAlignment.Left;

        return senderId == currentUserId
            ? HorizontalAlignment.Right
            : HorizontalAlignment.Left;
    }
}
