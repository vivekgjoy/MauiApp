using System.Globalization;
using MauiApp.ImageEditor.Models;

namespace MauiApp.ImageEditor.Converters;

public class ToolTypeToVisibilityConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ToolType toolType && parameter is string param)
        {
            var targetTool = Enum.Parse<ToolType>(param);
            return toolType == targetTool;
        }
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return null;
    }
}






