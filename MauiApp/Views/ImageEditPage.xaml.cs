using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Maui.Controls;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;
using System.Threading.Tasks;
using System.IO;

namespace MauiApp.Views
{
    public partial class ImageEditPage : ContentPage
    {
        private string? _imagePath;
        private SKColor _currentColor = SKColors.Red;
        private float _brushSize = 5f;
        private ToolMode _currentTool = ToolMode.Brush;

        private readonly List<IDrawableItem> _items = new();
        private readonly Stack<IDrawableItem> _undoStack = new();
        private readonly Stack<IDrawableItem> _redoStack = new();

        private SKPath? _currentPath;
        private SKPoint _lastPoint;
        private IDrawableItem? _tempShape;
        private PathDrawable? _activePathItem;

        // Brush behavior
        private float _currentStrokeWidth => _brushSize;
        private float _currentAlpha = 1f;

        public string? ImagePath
        {
            get => _imagePath;
            set
            {
                _imagePath = value;
                if (!string.IsNullOrEmpty(value))
                    EditImage.Source = ImageSource.FromFile(value);
            }
        }

        public ImageEditPage()
        {
            InitializeComponent();
            BrushSizeSlider.Value = _brushSize;
            BrushSizeLabel.Text = $"{_brushSize:F0}px";
            UpdateToolSelectionUI();
        }

        // ---------------- UI events ----------------
        private void OnColorButtonClicked(object sender, EventArgs e)
        {
            if (sender == RedColorButton) SetColor(SKColors.Red);
            else if (sender == BlueColorButton) SetColor(SKColors.Blue);
            else if (sender == GreenColorButton) SetColor(SKColors.Green);
            else if (sender == BlackColorButton) SetColor(SKColors.Black);
        }

        private void SetColor(SKColor color)
        {
            _currentColor = color;
            RedColorButton.Text = _currentColor == SKColors.Red ? "✓" : "";
            BlueColorButton.Text = _currentColor == SKColors.Blue ? "✓" : "";
            GreenColorButton.Text = _currentColor == SKColors.Green ? "✓" : "";
            BlackColorButton.Text = _currentColor == SKColors.Black ? "✓" : "";
        }

        private void OnBrushSizeChanged(object sender, ValueChangedEventArgs e)
        {
            _brushSize = (float)e.NewValue;
            BrushSizeLabel.Text = $"{_brushSize:F0}px";
        }

        private void OnToolSelected(object sender, EventArgs e)
        {
            if (sender == BrushToolButton) _currentTool = ToolMode.Brush;
            else if (sender == EraserToolButton) _currentTool = ToolMode.Eraser;
            else if (sender == ArrowToolButton) _currentTool = ToolMode.Arrow;
            else if (sender == CircleToolButton) _currentTool = ToolMode.Circle;
            else if (sender == SquareToolButton) _currentTool = ToolMode.Square;
            else if (sender == CheckToolButton) _currentTool = ToolMode.Check;
            else if (sender == CrossToolButton) _currentTool = ToolMode.Cross;

            _tempShape = null;
            UpdateToolSelectionUI();
        }

        private void UpdateToolSelectionUI()
        {
            foreach (var btn in new[] { BrushToolButton, EraserToolButton, ArrowToolButton, CircleToolButton, SquareToolButton, CheckToolButton, CrossToolButton })
            {
                btn.BackgroundColor = Colors.Transparent;
                btn.TextColor = Color.FromArgb("#ED1C24");
            }

            Button? active = _currentTool switch
            {
                ToolMode.Brush => BrushToolButton,
                ToolMode.Eraser => EraserToolButton,
                ToolMode.Arrow => ArrowToolButton,
                ToolMode.Circle => CircleToolButton,
                ToolMode.Square => SquareToolButton,
                ToolMode.Check => CheckToolButton,
                ToolMode.Cross => CrossToolButton,
                _ => null
            };

            if (active != null)
            {
                active.BackgroundColor = Color.FromArgb("#ED1C24");
                active.TextColor = Colors.White;
            }
            CanvasView.InvalidateSurface();
        }

        // ---------------- Undo/Redo ----------------
        private void OnUndoClicked(object sender, EventArgs e)
        {
            if (_items.Count > 0)
            {
                var last = _items.Last();
                _items.Remove(last);
                _undoStack.Push(last);
                _redoStack.Clear();
                CanvasView.InvalidateSurface();
            }
        }

        private void OnRedoClicked(object sender, EventArgs e)
        {
            if (_undoStack.Count > 0)
            {
                var redoItem = _undoStack.Pop();
                _items.Add(redoItem);
                _redoStack.Push(redoItem);
                CanvasView.InvalidateSurface();
            }
        }

        // ---------------- Save / Navigation ----------------
        private async void OnSaveClicked(object sender, EventArgs e)
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

                var imageCommentPage = new ImageCommentPage { ImagePath = editedImagePath };
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
                if (string.IsNullOrEmpty(_imagePath))
                    return null;

                using var originalStream = File.OpenRead(_imagePath);
                using var originalBitmap = SKBitmap.Decode(originalStream);
                using var editedBitmap = new SKBitmap(originalBitmap.Width, originalBitmap.Height);
                using var canvas = new SKCanvas(editedBitmap);

                canvas.DrawBitmap(originalBitmap, 0, 0);
                foreach (var item in _items)
                    item.Draw(canvas);

                var cachePath = FileSystem.CacheDirectory;
                var fileName = $"edited_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";
                var fullPath = Path.Combine(cachePath, fileName);

                using var image = SKImage.FromBitmap(editedBitmap);
                using var data = image.Encode(SKEncodedImageFormat.Jpeg, 90);
                using var stream = File.Create(fullPath);
                data.SaveTo(stream);

                return fullPath;
            }
            catch
            {
                return null;
            }
        }

        private async void OnBackClicked(object sender, EventArgs e) => await Navigation.PopAsync();

        // ---------------- Touch Handling ----------------
        private void OnTouchEffectAction(object sender, SKTouchEventArgs e)
        {
            var point = e.Location;
            e.Handled = true;

            switch (e.ActionType)
            {
                case SKTouchAction.Pressed:
                    _lastPoint = point;

                    if (_currentTool == ToolMode.Brush || _currentTool == ToolMode.Eraser)
                    {
                        _currentPath = new SKPath();
                        _currentPath.MoveTo(point);

                        bool isEraser = _currentTool == ToolMode.Eraser;
                        var color = isEraser ? SKColors.Transparent : _currentColor.WithAlpha((byte)(_currentAlpha * 255));

                        _activePathItem = new PathDrawable(_currentPath, color, _currentStrokeWidth, isEraser);
                        _items.Add(_activePathItem);
                    }
                    break;

                case SKTouchAction.Moved:
                    if (_currentPath != null && e.InContact)
                    {
                        var mid = new SKPoint((_lastPoint.X + point.X) / 2, (_lastPoint.Y + point.Y) / 2);
                        _currentPath.QuadTo(_lastPoint, mid);
                        _lastPoint = point;
                        CanvasView.InvalidateSurface();
                    }
                    break;

                case SKTouchAction.Released:
                case SKTouchAction.Cancelled:
                    if (_activePathItem != null)
                    {
                        // Push the entire stroke as one undoable item
                        _redoStack.Clear();
                        _undoStack.Clear();
                    }
                    _activePathItem = null;
                    _currentPath = null;
                    _lastPoint = SKPoint.Empty;
                    break;
            }
        }

        // ---------------- Drawing ----------------
        private void OnCanvasViewPaintSurface(object sender, SKPaintSurfaceEventArgs e)
        {
            var canvas = e.Surface.Canvas;
            canvas.Clear(SKColors.White);

            if (!string.IsNullOrEmpty(ImagePath))
            {
                try
                {
                    using var stream = File.OpenRead(ImagePath);
                    using var bmp = SKBitmap.Decode(stream);
                    if (bmp != null)
                    {
                        var rect = FitRectToCanvas(bmp.Width, bmp.Height, e.Info.Width, e.Info.Height);
                        canvas.DrawBitmap(bmp, rect);
                    }
                }
                catch { }
            }

            foreach (var item in _items)
                item.Draw(canvas);

            _tempShape?.Draw(canvas);
        }

        private SKRect FitRectToCanvas(int imgW, int imgH, int cw, int ch)
        {
            float ratio = Math.Min((float)cw / imgW, (float)ch / imgH);
            float w = imgW * ratio, h = imgH * ratio;
            float l = (cw - w) / 2, t = (ch - h) / 2;
            return new SKRect(l, t, l + w, t + h);
        }

        // ---------------- Drawable Interface ----------------
        private interface IDrawableItem
        {
            void Draw(SKCanvas canvas);
            SKRect GetBounds();
            void Translate(float dx, float dy);
            void UpdateGeometry(SKPoint start, SKPoint end);
        }

        private class PathDrawable : IDrawableItem
        {
            public SKPath Path { get; }
            public SKColor Color { get; }
            public float Stroke { get; }
            public bool IsEraser { get; }

            public PathDrawable(SKPath path, SKColor color, float stroke, bool isEraser)
            {
                Path = path;
                Color = color;
                Stroke = stroke;
                IsEraser = isEraser;
            }

            public void Draw(SKCanvas c)
            {
                using var p = new SKPaint
                {
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = Stroke,
                    Color = IsEraser ? SKColors.Transparent : Color,
                    IsAntialias = true,
                    StrokeCap = SKStrokeCap.Round,
                    StrokeJoin = SKStrokeJoin.Round,
                    BlendMode = IsEraser ? SKBlendMode.Clear : SKBlendMode.SrcOver
                };
                c.DrawPath(Path, p);
            }

            public SKRect GetBounds() => Path.Bounds;
            public void Translate(float dx, float dy) => Path.Offset(dx, dy);
            public void UpdateGeometry(SKPoint s, SKPoint e) => Path.LineTo(e);
        }

        private enum ToolMode { Brush, Eraser, Arrow, Circle, Square, Check, Cross }
    }
}
