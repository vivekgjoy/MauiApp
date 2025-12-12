using System.Globalization;

namespace MauiApp.ImageEditor.Converters;

public class BoolToOpacityConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return boolValue ? 1.0 : 0.4; // Enabled = full opacity, disabled = dimmed
        }
        return 0.4; // Default to dimmed if not a boolean
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}


