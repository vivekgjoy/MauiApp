using System.Globalization;
using MauiApp.ImageEditor.Models;

namespace MauiApp.ImageEditor.Converters;

public class FontFamilySelectedConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string selectedFontFamily && parameter is FontStyleItem fontStyleItem)
        {
            return selectedFontFamily == fontStyleItem.FontFamily;
        }
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}





