using System.Globalization;
using Microsoft.Maui.Graphics;

namespace MauiApp.ImageEditor.Converters;

public class TextColorUnderlineConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // value is HasSelectedText (bool)
        // parameter is SelectedTextColor (Color)
        if (value is bool hasSelectedText && parameter is Color selectedColor)
        {
            if (hasSelectedText)
            {
                return selectedColor;
            }
            else
            {
                return Colors.Gray;
            }
        }
        return Colors.Gray;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}






