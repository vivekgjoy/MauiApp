using SkiaSharp;

namespace MauiApp.ImageEditor.Models;

/// <summary>
/// Represents a shape layer that can be drawn on the image.
/// </summary>
public class ShapeLayer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public SKRect Bounds { get; set; } // Shape bounds in image coordinates
    public ShapeType Type { get; set; }
    public bool IsSelected { get; set; }
    public float StrokeWidth { get; set; } = 5f;
    public SKColor StrokeColor { get; set; } = SKColors.Red;
    public SKColor? FillColor { get; set; } = null; // null = no fill
    public float Rotation { get; set; } = 0f; // Rotation in degrees
}







