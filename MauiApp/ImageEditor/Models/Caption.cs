using SkiaSharp;

namespace MauiApp.ImageEditor.Models
{
    /// <summary>
    /// Represents a caption/text overlay that can be saved and restored.
    /// Used for persistence and editing saved captions.
    /// </summary>
    public class Caption
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Text { get; set; } = string.Empty;
        public float X { get; set; }
        public float Y { get; set; }
        public SKColor Color { get; set; }
        public SKColor? FillColor { get; set; } // Background fill color for text
        public float Size { get; set; }
        public string FontFamily { get; set; } = "Arial";
        public bool IsBold { get; set; }
        public bool IsItalic { get; set; }
        public bool IsUnderline { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime ModifiedAt { get; set; } = DateTime.Now;
        
        // Helper method to convert SKColor to string for JSON serialization
        public string ColorString
        {
            get => $"{Color.Red},{Color.Green},{Color.Blue},{Color.Alpha}";
            set
            {
                var parts = value.Split(',');
                if (parts.Length == 4 && 
                    byte.TryParse(parts[0], out var r) &&
                    byte.TryParse(parts[1], out var g) &&
                    byte.TryParse(parts[2], out var b) &&
                    byte.TryParse(parts[3], out var a))
                {
                    Color = new SKColor(r, g, b, a);
                }
            }
        }
    }
}





