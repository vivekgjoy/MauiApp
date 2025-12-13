using System.Reflection;
using Microsoft.Maui.Controls;

namespace MauiApp.ImageEditor;

/// <summary>
/// Helper class to load icons from embedded resources.
/// This ensures icons work when the library is consumed from a NuGet package.
/// </summary>
public static class IconHelper
{
    private static readonly Assembly Assembly = typeof(IconHelper).Assembly;
    private static readonly string ResourcePrefix = "MauiApp.Resources.ImageEditorIcons.";

    /// <summary>
    /// Gets an ImageSource for an icon by name.
    /// Prioritizes file system (better quality) over embedded resources.
    /// Only uses embedded resources as fallback when file-based loading fails.
    /// </summary>
    /// <param name="iconName">Name of the icon file (e.g., "ic_crop_edit.svg")</param>
    /// <returns>ImageSource for the icon</returns>
    public static ImageSource GetIcon(string iconName)
    {
        if (string.IsNullOrEmpty(iconName))
        {
            return null!;
        }

        // First, try file system (better quality, works when library is referenced locally or icons are copied)
        // MAUI will resolve file-based sources automatically if they exist
        try
        {
            return ImageSource.FromFile(iconName);
        }
        catch
        {
            // If file-based fails, fall back to embedded resource (for NuGet packages)
            var resourceName = ResourcePrefix + iconName;
            var stream = Assembly.GetManifestResourceStream(resourceName);
            
            if (stream != null)
            {
                // Return a new stream factory (streams can't be reused)
                return ImageSource.FromStream(() => Assembly.GetManifestResourceStream(resourceName));
            }
            
            // Last resort: return file source anyway (MAUI might resolve it)
            return ImageSource.FromFile(iconName);
        }
    }

    /// <summary>
    /// Gets an ImageSource for an icon by name, with embedded resource fallback.
    /// </summary>
    public static ImageSource GetIconFromResource(string iconName)
    {
        if (string.IsNullOrEmpty(iconName))
        {
            return null!;
        }

        var resourceName = ResourcePrefix + iconName;
        var stream = Assembly.GetManifestResourceStream(resourceName);
        
        if (stream != null)
        {
            return ImageSource.FromStream(() => Assembly.GetManifestResourceStream(resourceName));
        }

        // Fallback to file
        return ImageSource.FromFile(iconName);
    }

    /// <summary>
    /// Sets the source of an Image control to load from embedded resource.
    /// Use this in code-behind if XAML icon loading fails.
    /// </summary>
    public static void SetIconSource(Image image, string iconName)
    {
        if (image != null && !string.IsNullOrEmpty(iconName))
        {
            image.Source = GetIcon(iconName);
        }
    }
}







