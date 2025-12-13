using System.Globalization;
using Microsoft.Maui.Graphics;
using MauiApp.ImageEditor.Models;

namespace MauiApp.ImageEditor.Converters;

public class CropModeToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value?.ToString() == parameter?.ToString())
        {
            // All modes use the same dark gray background when selected
            return Color.FromArgb("#2C2C2C"); // Selected fill color
        }

        return Colors.Transparent; // Unselected = no background
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

