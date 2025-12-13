using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace MauiApp.Views;

// OLD IMAGE EDITOR - COMMENTED OUT (Replaced by SkiaSharpImageEditorPage)
// This class is kept for reference but disabled to avoid build errors
#if false
public partial class ImageEditPage : ContentPage
{
    private SKImage? _backgroundImage;
    private float _scale = 1f;
    private SKMatrix _matrix = SKMatrix.Identity;

    private List<DrawingElement> _elements = new();
    private Stack<DrawingElement> _undoStack = new();
    private Stack<DrawingElement> _redoStack = new();

    private DrawingMode _mode = DrawingMode.Freehand;
    private DrawingElement? _currentElement;
    private SKPath? _currentPath;
    private SKPoint _lastPoint;

    private string? _imagePath;
    private string? _imageId;
    private bool _isEditingExisting = false;

    public string? ImagePath
    {
        get => _imagePath;
        set
        {
            _imagePath = value;
            if (!string.IsNullOrEmpty(value) && File.Exists(value))
            {
                LoadImage(value);
            }
        }
    }

    public string? ImageId
    {
        get => _imageId;
        set => _imageId = value;
    }

    public bool IsEditingExisting
    {
        get => _isEditingExisting;
        set => _isEditingExisting = value;
    }

    public ImageEditPage()
    {
        InitializeComponent();
    }

    private void LoadImage(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            _backgroundImage = SKImage.FromEncodedData(stream);
            canvasView.InvalidateSurface();
        }
        catch
        {
            // Handle error
        }
    }

    private void OnPaintSurface(object sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.White);

        canvas.Save();
        canvas.SetMatrix(_matrix);

        // Draw background image
        if (_backgroundImage != null)
        {
            var info = e.Info;
            var imageRect = new SKRect(0, 0, _backgroundImage.Width, _backgroundImage.Height);
            var destRect = FitRectToCanvas(_backgroundImage.Width, _backgroundImage.Height, info.Width, info.Height);
            canvas.DrawImage(_backgroundImage, imageRect, destRect);
        }

        // Draw all elements
        foreach (var el in _elements)
            el.Draw(canvas);

        // Highlight selected
        if (_currentElement is ShapeElement shape && shape.IsSelected)
        {
            var paint = new SKPaint { Color = SKColors.Cyan.WithAlpha(100), StrokeWidth = 6, Style = SKPaintStyle.Stroke };
            canvas.DrawRect(shape.Bounds, paint);
        }

        canvas.Restore();
    }

    private SKRect FitRectToCanvas(int imgW, int imgH, int cw, int ch)
    {
        float ratio = Math.Min((float)cw / imgW, (float)ch / imgH);
        float w = imgW * ratio, h = imgH * ratio;
        float l = (cw - w) / 2, t = (ch - h) / 2;
        return new SKRect(l, t, l + w, t + h);
    }

    private void OnTouch(object sender, SKTouchEventArgs e)
    {
        var point = e.Location;
        point = _matrix.MapPoint(new SKPoint(point.X, point.Y));

        if (e.ActionType == SKTouchAction.Pressed)
        {
            if (_mode == DrawingMode.Select)
            {
                _currentElement = HitTest(point);
                foreach (var el in _elements) el.IsSelected = false;
                if (_currentElement != null) _currentElement.IsSelected = true;
            }
            else if (_mode == DrawingMode.Freehand)
            {
                _currentPath = new SKPath();
                _currentPath.MoveTo(point);
                _currentElement = new PathElement(_currentPath, GetCurrentPaint());
                _elements.Add(_currentElement);
                _redoStack.Clear();
            }
            else
            {
                _currentElement = CreateShape(_mode, point, point);
                _elements.Add(_currentElement);
                _redoStack.Clear();
            }
            _lastPoint = point;
        }
        else if (e.ActionType == SKTouchAction.Moved && e.InContact)
        {
            if (_currentElement is PathElement pathEl && _currentPath != null)
            {
                _currentPath.LineTo(point);
            }
            else if (_currentElement is ShapeElement shape)
            {
                shape.EndPoint = point;
            }
            _lastPoint = point;
        }
        else if (e.ActionType == SKTouchAction.Released)
        {
            _currentPath = null;
            _currentElement = null;
        }

        e.Handled = true;
        canvasView.InvalidateSurface();
    }

    private SKPaint GetCurrentPaint()
    {
        var color = (Color)colorPicker.SelectedItem ?? Colors.Black;
        return new SKPaint
        {
            Color = color.ToSKColor(),
            StrokeWidth = 6,
            Style = SKPaintStyle.Stroke,
            IsAntialias = true,
            StrokeCap = SKStrokeCap.Round
        };
    }

    private DrawingElement CreateShape(DrawingMode mode, SKPoint start, SKPoint end)
    {
        return mode switch
        {
            DrawingMode.Rectangle => new RectangleElement(start, end, GetCurrentPaint()),
            DrawingMode.Circle => new CircleElement(start, end, GetCurrentPaint()),
            DrawingMode.Arrow => new ArrowElement(start, end, GetCurrentPaint()),
            DrawingMode.Text => new TextElement(start, "Tap to edit", GetCurrentPaint()),
            _ => new PathElement(new SKPath(), GetCurrentPaint())
        };
    }

    private DrawingElement? HitTest(SKPoint point)
    {
        for (int i = _elements.Count - 1; i >= 0; i--)
        {
            if (_elements[i].HitTest(point)) return _elements[i];
        }
        return null;
    }

    // Toolbar Buttons
    private void OnFreehand(object s, EventArgs e) => _mode = DrawingMode.Freehand;
    private void OnRectangle(object s, EventArgs e) => _mode = DrawingMode.Rectangle;
    private void OnCircle(object s, EventArgs e) => _mode = DrawingMode.Circle;
    private void OnArrow(object s, EventArgs e) => _mode = DrawingMode.Arrow;
    private void OnText(object s, EventArgs e) => _mode = DrawingMode.Text;
    
    private void OnDeleteSelected(object s, EventArgs e)
    {
        if (_currentElement != null)
        {
            _elements.Remove(_currentElement);
            _currentElement = null;
            canvasView.InvalidateSurface();
        }
    }
    
    private void OnUndo(object s, EventArgs e)
    {
        if (_elements.Count > 0)
        {
            var last = _elements[^1];
            _elements.RemoveAt(_elements.Count - 1);
            _undoStack.Push(last);
            canvasView.InvalidateSurface();
        }
    }
    
    private void OnRedo(object s, EventArgs e)
    {
        if (_undoStack.Count > 0)
        {
            var el = _undoStack.Pop();
            _elements.Add(el);
            canvasView.InvalidateSurface();
        }
    }

    private void OnPinch(object sender, PinchGestureUpdatedEventArgs e)
    {
        if (e.Status == GestureStatus.Running)
        {
            _scale *= (float)e.Scale;
            _scale = Math.Clamp(_scale, 0.5f, 5f);
            var centerX = canvasView.CanvasSize.Width / 2;
            var centerY = canvasView.CanvasSize.Height / 2;
            _matrix = SKMatrix.CreateScale(_scale, _scale, centerX, centerY);
            canvasView.InvalidateSurface();
        }
    }

    private async void OnSave(object s, EventArgs e)
    {
        try
        {
            if (string.IsNullOrEmpty(_imagePath))
            {
                await DisplayAlert("Error", "No image to save", "OK");
                return;
            }

            var editedImagePath = await SaveEditedImage();
            if (string.IsNullOrEmpty(editedImagePath))
            {
                await DisplayAlert("Error", "Failed to save edited image", "OK");
                return;
            }

            var imageCommentPage = new ImageCommentPage 
            { 
                ImagePath = editedImagePath,
                ImageId = _imageId,
                IsEditingExisting = _isEditingExisting
            };
            await Navigation.PushAsync(imageCommentPage);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to save image: {ex.Message}", "OK");
        }
    }

    private async Task<string?> SaveEditedImage()
    {
        try
        {
            if (_backgroundImage == null)
                return null;

            var width = _backgroundImage.Width;
            var height = _backgroundImage.Height;

            using var surface = SKSurface.Create(new SKImageInfo(width, height));
            var canvas = surface.Canvas;
            canvas.Clear(SKColors.White);

            // Draw background image
            canvas.DrawImage(_backgroundImage, new SKRect(0, 0, width, height));

            // Draw all elements (scale them to match image size)
            if (canvasView == null) return null;
            var scaleX = (float)width / canvasView.CanvasSize.Width;
            var scaleY = (float)height / canvasView.CanvasSize.Height;
            canvas.Save();
            canvas.Scale(scaleX, scaleY);

            foreach (var item in _elements)
                item.Draw(canvas);

            canvas.Restore();

            using var image = surface.Snapshot();
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);

            var cachePath = FileSystem.CacheDirectory;
            var fileName = $"edited_{DateTime.Now:yyyyMMdd_HHmmss}.png";
            var fullPath = Path.Combine(cachePath, fileName);
            
            File.WriteAllBytes(fullPath, data.ToArray());
            return fullPath;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Save error: {ex}");
            return null;
        }
    }
}
#endif

// Supporting Classes
public enum DrawingMode { Select, Freehand, Rectangle, Circle, Arrow, Text }

public abstract class DrawingElement
{
    public bool IsSelected { get; set; }
    public abstract void Draw(SKCanvas canvas);
    public abstract bool HitTest(SKPoint point);
    public SKRect Bounds => CalculateBounds();
    protected virtual SKRect CalculateBounds() => SKRect.Empty;

    public SKPoint StartPoint { get; protected set; }
    public SKPoint EndPoint { get; set; }
}

public class PathElement : DrawingElement
{
    public SKPath Path { get; }
    public SKPaint Paint { get; }

    public PathElement(SKPath path, SKPaint paint)
    {
        Path = path; 
        Paint = paint;
    }

    public override void Draw(SKCanvas canvas) => canvas.DrawPath(Path, Paint);
    public override bool HitTest(SKPoint point) => Path.Contains(point.X, point.Y);
    protected override SKRect CalculateBounds() => Path.Bounds;
}

public class RectangleElement : ShapeElement
{
    public RectangleElement(SKPoint start, SKPoint end, SKPaint paint) : base(start, end, paint) { }

    public override void Draw(SKCanvas canvas)
    {
        var rect = SKRect.Create(StartPoint.X, StartPoint.Y,
            EndPoint.X - StartPoint.X, EndPoint.Y - StartPoint.Y);
        canvas.DrawRect(rect, Paint);
    }

    public override bool HitTest(SKPoint p)
    {
        var rect = SKRect.Create(StartPoint.X, StartPoint.Y,
            EndPoint.X - StartPoint.X, EndPoint.Y - StartPoint.Y);
        return rect.Contains(p.X, p.Y);
    }

    protected override SKRect CalculateBounds()
    {
        return SKRect.Create(StartPoint.X, StartPoint.Y,
            EndPoint.X - StartPoint.X, EndPoint.Y - StartPoint.Y);
    }
}

public class CircleElement : ShapeElement
{
    public CircleElement(SKPoint start, SKPoint end, SKPaint paint) : base(start, end, paint) { }

    public override void Draw(SKCanvas canvas)
    {
        var rect = SKRect.Create(StartPoint.X, StartPoint.Y,
            EndPoint.X - StartPoint.X, EndPoint.Y - StartPoint.Y);
        canvas.DrawOval(rect, Paint);
    }

    public override bool HitTest(SKPoint p)
    {
        var rect = SKRect.Create(StartPoint.X, StartPoint.Y,
            EndPoint.X - StartPoint.X, EndPoint.Y - StartPoint.Y);
        var center = new SKPoint(rect.MidX, rect.MidY);
        var radius = Math.Max(rect.Width, rect.Height) / 2f;
        var distance = (float)Math.Sqrt(Math.Pow(p.X - center.X, 2) + Math.Pow(p.Y - center.Y, 2));
        return distance <= radius;
    }

    protected override SKRect CalculateBounds()
    {
        return SKRect.Create(StartPoint.X, StartPoint.Y,
            EndPoint.X - StartPoint.X, EndPoint.Y - StartPoint.Y);
    }
}

public class ArrowElement : ShapeElement
{
    public ArrowElement(SKPoint start, SKPoint end, SKPaint paint) : base(start, end, paint) { }

    public override void Draw(SKCanvas canvas)
    {
        var start = StartPoint;
        var end = EndPoint;
        
        // Draw arrow line
        canvas.DrawLine(start, end, Paint);
        
        // Draw arrowhead
        var angle = (float)Math.Atan2(end.Y - start.Y, end.X - start.X);
        var arrowLength = 20f;
        var arrowAngle = (float)(Math.PI / 6); // 30 degrees
        
        var arrowPoint1 = new SKPoint(
            end.X - arrowLength * (float)Math.Cos(angle - arrowAngle),
            end.Y - arrowLength * (float)Math.Sin(angle - arrowAngle)
        );
        var arrowPoint2 = new SKPoint(
            end.X - arrowLength * (float)Math.Cos(angle + arrowAngle),
            end.Y - arrowLength * (float)Math.Sin(angle + arrowAngle)
        );
        
        canvas.DrawLine(end, arrowPoint1, Paint);
        canvas.DrawLine(end, arrowPoint2, Paint);
    }

    public override bool HitTest(SKPoint p)
    {
        // Simple line hit test
        var distance = DistanceToLine(p, StartPoint, EndPoint);
        return distance < 10f; // 10 pixel tolerance
    }

    private float DistanceToLine(SKPoint point, SKPoint lineStart, SKPoint lineEnd)
    {
        var A = point.X - lineStart.X;
        var B = point.Y - lineStart.Y;
        var C = lineEnd.X - lineStart.X;
        var D = lineEnd.Y - lineStart.Y;

        var dot = A * C + B * D;
        var lenSq = C * C + D * D;
        var param = lenSq != 0 ? dot / lenSq : -1;

        float xx, yy;

        if (param < 0)
        {
            xx = lineStart.X;
            yy = lineStart.Y;
        }
        else if (param > 1)
        {
            xx = lineEnd.X;
            yy = lineEnd.Y;
        }
        else
        {
            xx = lineStart.X + param * C;
            yy = lineStart.Y + param * D;
        }

        var dx = point.X - xx;
        var dy = point.Y - yy;
        return (float)Math.Sqrt(dx * dx + dy * dy);
    }

    protected override SKRect CalculateBounds()
    {
        return SKRect.Create(
            Math.Min(StartPoint.X, EndPoint.X) - 20,
            Math.Min(StartPoint.Y, EndPoint.Y) - 20,
            Math.Abs(EndPoint.X - StartPoint.X) + 40,
            Math.Abs(EndPoint.Y - StartPoint.Y) + 40
        );
    }
}

public class TextElement : ShapeElement
{
    public string Text { get; set; }

    public TextElement(SKPoint start, string text, SKPaint paint) : base(start, start, paint)
    {
        Text = text;
    }

    public override void Draw(SKCanvas canvas)
    {
        var textPaint = new SKPaint
        {
            Color = Paint.Color,
            TextSize = 24,
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };
        canvas.DrawText(Text, StartPoint.X, StartPoint.Y, textPaint);
    }

    public override bool HitTest(SKPoint p)
    {
        var textPaint = new SKPaint { TextSize = 24 };
        var bounds = new SKRect();
        textPaint.MeasureText(Text, ref bounds);
        bounds.Offset(StartPoint.X, StartPoint.Y - bounds.Height);
        return bounds.Contains(p.X, p.Y);
    }

    protected override SKRect CalculateBounds()
    {
        var textPaint = new SKPaint { TextSize = 24 };
        var bounds = new SKRect();
        textPaint.MeasureText(Text, ref bounds);
        bounds.Offset(StartPoint.X, StartPoint.Y - bounds.Height);
        return bounds;
    }
}

public abstract class ShapeElement : DrawingElement
{
    protected SKPaint Paint { get; }

    protected ShapeElement(SKPoint start, SKPoint end, SKPaint paint)
    {
        StartPoint = start;
        EndPoint = end;
        Paint = paint;
    }

    // ShapeElement is abstract, so concrete classes must implement Draw and HitTest
    public abstract override void Draw(SKCanvas canvas);
    public abstract override bool HitTest(SKPoint point);
}

public static class ColorExtensions
{
    public static SKColor ToSKColor(this Color color)
    {
        return new SKColor(
            (byte)(color.Red * 255),
            (byte)(color.Green * 255),
            (byte)(color.Blue * 255),
            (byte)(color.Alpha * 255)
        );
    }
}

