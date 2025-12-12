using SkiaSharp;

namespace MauiApp.ImageEditor.Models
{
    /// <summary>
    /// Represents a single drawing stroke with its path, color, and thickness.
    /// Each stroke is independent and can be drawn separately.
    /// </summary>
    public class Stroke
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public SKPath Path { get; set; }
        public SKColor Color { get; set; }
        public float Thickness { get; set; }

        public Stroke(SKPath path, SKColor color, float thickness)
        {
            Path = path ?? throw new ArgumentNullException(nameof(path));
            Color = color;
            Thickness = thickness;
        }
    }
}


