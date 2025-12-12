using System.Globalization;
using MauiApp.ImageEditor.Models;

namespace MauiApp.ImageEditor.Converters;

public class SubToolToBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is SubToolType subTool && parameter is string param)
        {
            if (Enum.TryParse<SubToolType>(param, out var targetSubTool))
            {
                return subTool == targetSubTool;
            }
        }
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return false;
    }
}




