using System.Globalization;

namespace MauiApp.ImageEditor.Converters;

public class SelectedToOpacityConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value?.ToString() == parameter?.ToString() ? 1.0 : 0.6; // selected = full white, others = slightly dimmed
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}






