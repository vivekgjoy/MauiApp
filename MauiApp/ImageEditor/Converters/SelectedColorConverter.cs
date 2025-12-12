using System.Globalization;
using Microsoft.Maui.Graphics;

namespace MauiApp.ImageEditor.Converters;

public class SelectedColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is Color currentColor && parameter is Color selectedColor)
        {
            // Compare colors (with tolerance for floating point comparison)
            return ColorsEqual(currentColor, selectedColor) ? Colors.Yellow : Colors.Transparent;
        }
        return Colors.Transparent;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }

    private bool ColorsEqual(Color a, Color b)
    {
        if (a == null || b == null) return false;
        return Math.Abs(a.Red - b.Red) < 0.01f &&
               Math.Abs(a.Green - b.Green) < 0.01f &&
               Math.Abs(a.Blue - b.Blue) < 0.01f &&
               Math.Abs(a.Alpha - b.Alpha) < 0.01f;
    }
}

// Converter to check if a color matches the selected color
public class IsSelectedColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is Color currentColor && parameter is Color selectedColor)
        {
            return ColorsEqual(currentColor, selectedColor);
        }
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }

    private bool ColorsEqual(Color a, Color b)
    {
        if (a == null || b == null) return false;
        return Math.Abs(a.Red - b.Red) < 0.01f &&
               Math.Abs(a.Green - b.Green) < 0.01f &&
               Math.Abs(a.Blue - b.Blue) < 0.01f &&
               Math.Abs(a.Alpha - b.Alpha) < 0.01f;
    }
}

// Converter to check if a color matches the base selected color (ignoring alpha)
public class IsBaseSelectedColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is Color currentColor && parameter is Color baseSelectedColor)
        {
            return ColorsEqual(currentColor, baseSelectedColor);
        }
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }

    private bool ColorsEqual(Color a, Color b)
    {
        if (a == null || b == null) return false;
        // Compare RGB only, ignore alpha for selection comparison
        return Math.Abs(a.Red - b.Red) < 0.01f &&
               Math.Abs(a.Green - b.Green) < 0.01f &&
               Math.Abs(a.Blue - b.Blue) < 0.01f;
    }
}






