using System.Globalization;
using MauiApp.ImageEditor.Models;

namespace MauiApp.ImageEditor.Converters;

public class ToolToBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ToolType toolType && parameter is string param)
        {
            if (Enum.TryParse<ToolType>(param, out var targetTool))
            {
                return toolType == targetTool;
            }
        }
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return false;
    }
}







