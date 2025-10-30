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
        private string? _imageId;
        private bool _isEditingExisting = false;
        private SKColor _currentColor = SKColors.Red;
        private float _brushSize = 5f;
        private ToolMode _currentTool = ToolMode.Brush;

        private readonly List<IDrawableItem> _items = new();
        private readonly Stack<IDrawableItem> _undoStack = new();
        private readonly Stack<IDrawableItem> _redoStack = new();

        private SKPath? _currentPath;
        private SKPoint _startPoint;
        private IDrawableItem? _tempShape;
        private IDrawableItem? _selectedItem;

        private bool _isDraggingItem = false;
        private SKPoint _lastDragPoint;

        // Multi-touch tracking
        private readonly Dictionary<long, SKPoint> _activeTouches = new();
        private float _initialDistance = 0f;
        private float _initialRotation = 0f;
        private SKRect _originalBounds;

        // Brush behavior
        private float _currentStrokeWidth => _brushSize;
        private float _currentAlpha = 1f;

        // Text editing state
        private TextDrawable? _editingTextItem = null;
        private SKPoint _textPosition;
        private SKPoint _textPressPoint; // Track where text tool press started

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
            else if (sender == TextToolButton) _currentTool = ToolMode.Text;
            else if (sender == ArrowToolButton) _currentTool = ToolMode.Arrow;
            else if (sender == CircleToolButton) _currentTool = ToolMode.Circle;
            else if (sender == SquareToolButton) _currentTool = ToolMode.Square;
            else if (sender == CheckToolButton) _currentTool = ToolMode.Check;
            else if (sender == CrossToolButton) _currentTool = ToolMode.Cross;

            _selectedItem = null;
            UpdateToolSelectionUI();
        }

        private void UpdateToolSelectionUI()
        {
            foreach (var btn in new[] { BrushToolButton, EraserToolButton, TextToolButton, ArrowToolButton, CircleToolButton, SquareToolButton, CheckToolButton, CrossToolButton })
            {
                btn.BackgroundColor = Colors.Transparent;
                btn.TextColor = Color.FromArgb("#ED1C24");
            }

            Button? active = _currentTool switch
            {
                ToolMode.Brush => BrushToolButton,
                ToolMode.Eraser => EraserToolButton,
                ToolMode.Text => TextToolButton,
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
                CanvasView.InvalidateSurface();
            }
        }

        private void OnRedoClicked(object sender, EventArgs e)
        {
            if (_undoStack.Count > 0)
            {
                var redo = _undoStack.Pop();
                _items.Add(redo);
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
                if (string.IsNullOrEmpty(_imagePath))
                    return null;

                using var originalStream = File.OpenRead(_imagePath);
                using var originalBitmap = SKBitmap.Decode(originalStream);

                // Create output bitmap at original image resolution
                using var editedBitmap = new SKBitmap(originalBitmap.Width, originalBitmap.Height);
                using var canvas = new SKCanvas(editedBitmap);
                canvas.Clear(SKColors.White);
                canvas.DrawBitmap(originalBitmap, 0, 0);

                // Calculate the scaling ratio between CanvasView and original image
                var viewWidth = (float)CanvasView.CanvasSize.Width;
                var viewHeight = (float)CanvasView.CanvasSize.Height;

                float scaleX = originalBitmap.Width / viewWidth;
                float scaleY = originalBitmap.Height / viewHeight;

                // Apply the same transformation to each drawn item
                canvas.Save();
                canvas.Scale(scaleX, scaleY);

                foreach (var item in _items)
                    item.Draw(canvas);

                canvas.Restore();

                // Save as JPEG
                var cachePath = FileSystem.CacheDirectory;
                var fileName = $"edited_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";
                var fullPath = Path.Combine(cachePath, fileName);

                using var image = SKImage.FromBitmap(editedBitmap);
                using var data = image.Encode(SKEncodedImageFormat.Jpeg, 90);
                using var stream = File.Create(fullPath);
                data.SaveTo(stream);

                return fullPath;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving edited image: {ex.Message}");
                return null;
            }
        }

        //private async Task<string?> SaveEditedImage()
        //{
        //    try
        //    {
        //        if (string.IsNullOrEmpty(_imagePath))
        //            return null;

        //        using var originalStream = File.OpenRead(_imagePath);
        //        using var originalBitmap = SKBitmap.Decode(originalStream);
        //        using var editedBitmap = new SKBitmap(originalBitmap.Width, originalBitmap.Height);
        //        using var canvas = new SKCanvas(editedBitmap);

        //        canvas.DrawBitmap(originalBitmap, 0, 0);
        //        foreach (var item in _items)
        //            item.Draw(canvas);

        //        var cachePath = FileSystem.CacheDirectory;
        //        var fileName = $"edited_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";
        //        var fullPath = Path.Combine(cachePath, fileName);

        //        using var image = SKImage.FromBitmap(editedBitmap);
        //        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 90);
        //        using var stream = File.Create(fullPath);
        //        data.SaveTo(stream);

        //        return fullPath;
        //    }
        //    catch
        //    {
        //        return null;
        //    }
        //}

        private async void OnBackClicked(object sender, EventArgs e)
        {
            await HandleBackNavigation();
        }

        protected override bool OnBackButtonPressed()
        {
            // Handle hardware back button
            Device.BeginInvokeOnMainThread(async () =>
            {
                await HandleBackNavigation();
            });
            return true; // Prevent default back behavior
        }

        private async Task HandleBackNavigation()
        {
            if (_items.Count > 0)
            {
                var result = await DisplayAlert(
                    "Unsaved Changes", 
                    "You have unsaved changes. Are you sure you want to go back? You will lose your edits.", 
                    "Yes, Go Back", 
                    "Cancel");
                
                if (!result)
                {
                    return;
                }
            }
            
            await Navigation.PopAsync();
        }

        private async Task ShowTextPopup(string title, string initialText = "")
        {
            PopupTitle.Text = title;
            PopupEntry.Text = initialText;

            TextPopup.IsVisible = true;
            PopupFrame.Scale = 0.8;
            await Task.WhenAll(
                PopupFrame.ScaleTo(1, 200, Easing.CubicOut),
                TextPopup.FadeTo(1, 200, Easing.CubicInOut)
            );
        }

        private async Task HideTextPopup()
        {
            await Task.WhenAll(
                PopupFrame.ScaleTo(0.8, 150, Easing.CubicIn),
                TextPopup.FadeTo(0, 150, Easing.CubicInOut)
            );
            TextPopup.IsVisible = false;
        }

        private async void OnPopupBackgroundTapped(object sender, TappedEventArgs e)
        {
            await HideTextPopup();
            _editingTextItem = null;
        }

        private async void OnPopupCancelClicked(object sender, EventArgs e)
        {
            await HideTextPopup();
            _editingTextItem = null;
        }

        private async void OnPopupOkClicked(object sender, EventArgs e)
        {
            var text = PopupEntry.Text?.Trim();
            if (!string.IsNullOrEmpty(text))
            {
                if (_editingTextItem != null)
                {
                    // Edit existing text
                    _editingTextItem.Text = text;
                    _editingTextItem = null;
                }
                else
                {
                    // Add new text
                    var newItem = new TextDrawable(text, _textPosition, _currentColor, _brushSize * 10);
                    _items.Add(newItem);
                    _undoStack.Clear(); 
                    _redoStack.Clear();
                }
                CanvasView.InvalidateSurface();
            }
            await HideTextPopup();
        }
        private void OnTouchEffectAction(object sender, SKTouchEventArgs e)
        {
            var id = e.Id;
            var pt = e.Location;

            switch (e.ActionType)
            {
                case SKTouchAction.Pressed:
                    _activeTouches[id] = pt;

                    if (_activeTouches.Count == 1)
                    {
                        var hit = HitTestItem(pt);
                        if (hit != null && (_currentTool != ToolMode.Brush && _currentTool != ToolMode.Eraser && _currentTool != ToolMode.Text))
                        {
                            _selectedItem = hit;
                            _isDraggingItem = true;
                            _lastDragPoint = pt;
                        }
                        else if (_currentTool == ToolMode.Brush)
                        {
                            _currentPath = new SKPath();
                            _currentPath.MoveTo(pt);
                            var p = new PathDrawable(new SKPath(_currentPath), _currentColor, _brushSize);
                            _items.Add(p);
                            _undoStack.Clear(); _redoStack.Clear();
                        }
                        else if (_currentTool == ToolMode.Eraser)
                    {
                        _currentPath = new SKPath();
                            _currentPath.MoveTo(pt);
                            var p = new PathDrawable(new SKPath(_currentPath), SKColors.Transparent, _brushSize, true);
                            _items.Add(p);
                            _undoStack.Clear(); _redoStack.Clear();
                        }
                        else if (_currentTool == ToolMode.Text)
                        {
                            var tappedItem = HitTestItem(pt);
                            if (tappedItem is TextDrawable textItem)
                            {
                                _selectedItem = textItem;
                                _lastDragPoint = pt;
                                _isDraggingItem = true;
                                _textPressPoint = pt;
                            }
                            else
                            {
                                _textPosition = pt;
                                _selectedItem = null;
                                _isDraggingItem = false;
                                _textPressPoint = pt;
                            }
                        }
                        else
                        {
                            _startPoint = pt;
                            _tempShape = CreateShapeItem(_currentTool, _startPoint, pt, _currentColor, _brushSize);
                        }
                    }
                    else if (_activeTouches.Count == 2 && _selectedItem != null)
                    {
                        var pts = _activeTouches.Values.ToList();
                        _initialDistance = Distance(pts[0], pts[1]);
                        _initialRotation = GetAngle(pts[0], pts[1]);
                        _originalBounds = _selectedItem.GetBounds();
                    }
                    break;

                case SKTouchAction.Moved:
                    if (_activeTouches.ContainsKey(id))
                        _activeTouches[id] = pt;

                    if (_activeTouches.Count == 1)
                    {
                        var only = _activeTouches.Values.First();
                        if (_isDraggingItem && _selectedItem != null)
                        {
                            var dx = only.X - _lastDragPoint.X;
                            var dy = only.Y - _lastDragPoint.Y;
                            _selectedItem.Translate(dx, dy);
                            _lastDragPoint = only;
                            CanvasView.InvalidateSurface();
                        }
                        else if (_currentTool == ToolMode.Brush && _currentPath != null)
                        {
                            _currentPath.LineTo(only);
                            if (_items.LastOrDefault() is PathDrawable lastPath)
                                lastPath.Path.LineTo(only);
                            CanvasView.InvalidateSurface();
                        }
                        else if (_currentTool == ToolMode.Eraser && _currentPath != null)
                        {
                            _currentPath.LineTo(only);
                            if (_items.LastOrDefault() is PathDrawable lastPath)
                                lastPath.Path.LineTo(only);
                            CanvasView.InvalidateSurface();
                        }
                        else if (_currentTool == ToolMode.Text && _isDraggingItem && _selectedItem is TextDrawable textItem)
                        {
                            var dx = only.X - _lastDragPoint.X;
                            var dy = only.Y - _lastDragPoint.Y;
                            textItem.Translate(dx, dy);
                            _lastDragPoint = only;
                            CanvasView.InvalidateSurface();
                        }
                        else if (_tempShape != null)
                        {
                            _tempShape.UpdateGeometry(_startPoint, only);
                            CanvasView.InvalidateSurface();
                        }
                    }
                    else if (_activeTouches.Count == 2 && _selectedItem != null)
                    {
                        var pts = _activeTouches.Values.ToList();
                        var newDist = Distance(pts[0], pts[1]);

                        if (_initialDistance <= 0f) _initialDistance = newDist != 0f ? newDist : 1f;

                        var scale = newDist / _initialDistance;
                        
                        if (_selectedItem is TextDrawable)
                        {
                            scale = Math.Max(0.5f, Math.Min(3f, scale));
                        }

                        if (_selectedItem is TextDrawable textItem)
                        {
                            textItem.ScaleFromBounds(_originalBounds, scale);
                            CanvasView.InvalidateSurface();
                        }
                        else if (_selectedItem is RotatableDrawable rot)
                        {
                            var newAngle = GetAngle(pts[0], pts[1]);
                            var rotationDelta = newAngle - _initialRotation;
                            rot.ScaleFromBounds(_originalBounds, scale);
                            rot.ApplyRotation(rotationDelta);
                            CanvasView.InvalidateSurface();
                        }
                    }
                    break;

                case SKTouchAction.Released:
                case SKTouchAction.Cancelled:
                    if (_currentTool == ToolMode.Text && _activeTouches.Count == 1)
                    {
                        if (_isDraggingItem && _selectedItem != null)
                        {
                            _isDraggingItem = false;
                        }
                        else
                        {
                            Device.BeginInvokeOnMainThread(async () =>
                            {
                                var tappedItem = HitTestItem(pt);
                                if (tappedItem is TextDrawable textItem)
                                {
                                    _editingTextItem = textItem;
                                    await ShowTextPopup("Edit Text", textItem.Text);
                                }
                                else
                                {
                                    _editingTextItem = null;
                                    _textPosition = pt;
                                    await ShowTextPopup("Add Text");
                                }
                            });
                        }
                    }
                    
                    _activeTouches.Remove(id);

                    if (_activeTouches.Count == 0)
                    {
                        _isDraggingItem = false;

                        _currentPath = null;

                        if (_tempShape != null)
                        {
                            _items.Add(_tempShape);
                            _tempShape = null;
                        }

                        _initialDistance = 0f;
                        _initialRotation = 0f;
                        _selectedItem = null;

                        CanvasView.InvalidateSurface();
                    }
                    else if (_activeTouches.Count == 1)
                    {
                        var remaining = _activeTouches.Values.First();
                        _initialDistance = 0f;
                        _initialRotation = 0f;
                    }
                    break;
            }

            e.Handled = true;
        }

        private float Distance(SKPoint a, SKPoint b) =>
            (float)Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));

        private float GetAngle(SKPoint a, SKPoint b) =>
            (float)Math.Atan2(b.Y - a.Y, b.X - a.X);

        // ---------------- Shapes & interfaces ----------------
        private IDrawableItem? HitTestItem(SKPoint p)
        {
            // Loop through items in reverse (top-most first)
            for (int i = _items.Count - 1; i >= 0; i--)
            {
                var item = _items[i];
                var bounds = item.GetBounds();
                
                // Expand bounds for easier tapping
                // Use larger expansion for text items
                float expansion = item is TextDrawable ? 20 : 10;
                var expanded = bounds;
                expanded.Inflate(expansion, expansion);
                
                if (expanded.Contains(p))
                    return item;
            }
            return null;
        }

        private IDrawableItem CreateShapeItem(ToolMode mode, SKPoint p1, SKPoint p2, SKColor color, float stroke)
        {
            var rect = new SKRect(Math.Min(p1.X, p2.X), Math.Min(p1.Y, p2.Y),
                                  Math.Max(p1.X, p2.X), Math.Max(p1.Y, p2.Y));
            return mode switch
            {
                ToolMode.Circle => new CircleDrawable(rect, color, stroke),
                ToolMode.Square => new RectDrawable(rect, color, stroke),
                ToolMode.Arrow => new ArrowDrawable(rect, color, stroke),
                ToolMode.Check => new CheckDrawable(rect, color, stroke),
                ToolMode.Cross => new CrossDrawable(rect, color, stroke),
                _ => new RectDrawable(rect, color, stroke)
            };
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

        private abstract class RotatableDrawable : IDrawableItem
        {
            protected float rotation = 0f;

            public abstract void Draw(SKCanvas canvas);
            public abstract SKRect GetBounds();
            public abstract void Translate(float dx, float dy);
            public abstract void UpdateGeometry(SKPoint start, SKPoint current);
            public abstract void ScaleFromBounds(SKRect original, float scale);
            public void ApplyRotation(float delta) => rotation += delta;
        }

        private class PathDrawable : IDrawableItem
        {
            public SKPath Path { get; private set; }
            public SKColor Color { get; private set; }
            public float Stroke { get; private set; }
            public bool IsEraser { get; private set; }

            public PathDrawable(SKPath p, SKColor c, float s, bool isEraser = false)
            {
                Path = p; Color = c; Stroke = s; IsEraser = isEraser;
            }

            public void Draw(SKCanvas c)
            {
                using var p = new SKPaint
                {
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = Stroke,
                    Color = Color,
                    IsAntialias = true,
                    StrokeCap = SKStrokeCap.Round,
                    StrokeJoin = SKStrokeJoin.Round
                };

                if (IsEraser)
                    p.BlendMode = SKBlendMode.Clear;

                c.DrawPath(Path, p);
            }

            public SKRect GetBounds() => Path.Bounds;

            public void Translate(float dx, float dy) => Path.Offset(dx, dy);

            public void UpdateGeometry(SKPoint start, SKPoint current) => Path.LineTo(current);
        }

        private class RectDrawable : RotatableDrawable
        {
            public SKRect Rect;
            public SKColor Color;
            public float Stroke;

            public RectDrawable(SKRect r, SKColor c, float s) { Rect = r; Color = c; Stroke = s; }

            public override void Draw(SKCanvas c)
            {
                c.Save();
                var center = new SKPoint(Rect.MidX, Rect.MidY);
                c.Translate(center.X, center.Y);
                c.RotateRadians(rotation);
                c.Translate(-center.X, -center.Y);

                using var p = new SKPaint { Style = SKPaintStyle.Stroke, StrokeWidth = Stroke, Color = Color, IsAntialias = true };
                c.DrawRect(Rect, p);

                c.Restore();
            }

            public override SKRect GetBounds() => Rect;

            public override void Translate(float dx, float dy) => Rect = new SKRect(Rect.Left + dx, Rect.Top + dy, Rect.Right + dx, Rect.Bottom + dy);

            public override void UpdateGeometry(SKPoint s, SKPoint e) =>
                Rect = new SKRect(Math.Min(s.X, e.X), Math.Min(s.Y, e.Y), Math.Max(s.X, e.X), Math.Max(s.Y, e.Y));

            public override void ScaleFromBounds(SKRect original, float scale)
            {
                var cx = (original.Left + original.Right) / 2f;
                var cy = (original.Top + original.Bottom) / 2f;
                var halfW = (original.Width / 2f) * scale;
                var halfH = (original.Height / 2f) * scale;
                Rect = new SKRect(cx - halfW, cy - halfH, cx + halfW, cy + halfH);
            }
        }

        private class CircleDrawable : RectDrawable
        {
            public CircleDrawable(SKRect r, SKColor c, float s) : base(r, c, s) { }

            public override void Draw(SKCanvas c)
            {
                c.Save();
                var center = new SKPoint(Rect.MidX, Rect.MidY);
                c.Translate(center.X, center.Y);
                c.RotateRadians(rotation);
                c.Translate(-center.X, -center.Y);

                using var p = new SKPaint { Style = SKPaintStyle.Stroke, StrokeWidth = Stroke, Color = Color, IsAntialias = true };
                c.DrawOval(Rect, p);

                c.Restore();
            }
        }

        private class ArrowDrawable : RectDrawable
        {
            public ArrowDrawable(SKRect r, SKColor c, float s) : base(r, c, s) { }

            public override void Draw(SKCanvas c)
            {
                c.Save();
                var center = new SKPoint(Rect.MidX, Rect.MidY);
                c.Translate(center.X, center.Y);
                c.RotateRadians(rotation);
                c.Translate(-center.X, -center.Y);

                using var p = new SKPaint { Style = SKPaintStyle.Stroke, StrokeWidth = Stroke, Color = Color, IsAntialias = true };

                var start = new SKPoint(Rect.Left, Rect.MidY);
                var end = new SKPoint(Rect.Right, Rect.MidY);
                c.DrawLine(start, end, p);

                var len = Math.Min(20, Math.Max(8, Rect.Width / 6));
                var angle = 0f;
                var left = new SKPoint(end.X - len * (float)Math.Cos(angle - Math.PI / 6), end.Y - len * (float)Math.Sin(angle - Math.PI / 6));
                var right = new SKPoint(end.X - len * (float)Math.Cos(angle + Math.PI / 6), end.Y - len * (float)Math.Sin(angle + Math.PI / 6));
                c.DrawLine(end, left, p);
                c.DrawLine(end, right, p);

                c.Restore();
            }
        }

        private class CheckDrawable : RectDrawable
        {
            public CheckDrawable(SKRect r, SKColor c, float s) : base(r, c, s) { }

            public override void Draw(SKCanvas c)
            {
                c.Save();
                var center = new SKPoint(Rect.MidX, Rect.MidY);
                c.Translate(center.X, center.Y);
                c.RotateRadians(rotation);
                c.Translate(-center.X, -center.Y);

                using var p = new SKPaint { Style = SKPaintStyle.Stroke, StrokeWidth = Stroke, Color = Color, IsAntialias = true };
                var path = new SKPath();
                path.MoveTo(Rect.Left + Rect.Width * 0.15f, Rect.Top + Rect.Height * 0.55f);
                path.LineTo(Rect.Left + Rect.Width * 0.40f, Rect.Top + Rect.Height * 0.80f);
                path.LineTo(Rect.Left + Rect.Width * 0.85f, Rect.Top + Rect.Height * 0.25f);
                c.DrawPath(path, p);

                c.Restore();
            }
        }

        private class CrossDrawable : RectDrawable
        {
            public CrossDrawable(SKRect r, SKColor c, float s) : base(r, c, s) { }

            public override void Draw(SKCanvas c)
            {
                c.Save();
                var center = new SKPoint(Rect.MidX, Rect.MidY);
                c.Translate(center.X, center.Y);
                c.RotateRadians(rotation);
                c.Translate(-center.X, -center.Y);

                using var p = new SKPaint { Style = SKPaintStyle.Stroke, StrokeWidth = Stroke, Color = Color, IsAntialias = true };
                c.DrawLine(Rect.Left, Rect.Top, Rect.Right, Rect.Bottom, p);
                c.DrawLine(Rect.Right, Rect.Top, Rect.Left, Rect.Bottom, p);

                c.Restore();
            }
        }

        private class TextDrawable : RotatableDrawable
        {
            public string Text { get; set; }
            public SKPoint Position { get; private set; }
            public SKColor Color { get; private set; }
            public float FontSize { get; private set; }
            private readonly float _originalFontSize;
            private const float MinTextScale = 0.3f;
            private const float MaxTextScale = 4.0f;

            public TextDrawable(string text, SKPoint position, SKColor color, float fontSize)
            {
                Text = text;
                Position = position;
                Color = color;
                _originalFontSize = fontSize;
                FontSize = fontSize;
            }

            public override void Draw(SKCanvas c)
            {
                using var paint = new SKPaint
                {
                    Color = Color,
                    IsAntialias = true,
                    TextSize = FontSize,
                    IsStroke = false,
                    TextAlign = SKTextAlign.Left
                };

                c.Save();
                c.Translate(Position.X, Position.Y);
                c.RotateRadians(rotation);
                c.DrawText(Text, 0, 0, paint);
                c.Restore();
            }

            public override SKRect GetBounds()
            {
                if (string.IsNullOrEmpty(Text))
                    return new SKRect(Position.X, Position.Y, Position.X + 10, Position.Y + 10);
                
                using var paint = new SKPaint 
                { 
                    TextSize = FontSize,
                    IsAntialias = true,
                    TextAlign = SKTextAlign.Left
                };
               
                var bounds = new SKRect();
                paint.MeasureText(Text, ref bounds);
                
                var fontMetrics = paint.FontMetrics;
                
                var textWidth = bounds.Width;
                var textHeight = Math.Abs(fontMetrics.Ascent) + Math.Abs(fontMetrics.Descent);
                
                var result = new SKRect(
                    Position.X,                    // Left
                    Position.Y - Math.Abs(fontMetrics.Ascent),
                    Position.X + textWidth,        // Right
                    Position.Y + Math.Abs(fontMetrics.Descent)
                );
                
                if (result.Width < 20) result.Right = result.Left + 20;
                if (result.Height < 20) result.Bottom = result.Top + 20;
                
                return result;
            }

            public override void Translate(float dx, float dy)
            {
                Position = new SKPoint(Position.X + dx, Position.Y + dy);
            }

            public override void UpdateGeometry(SKPoint start, SKPoint end)
            {
                Position = end;
            }

            public override void ScaleFromBounds(SKRect original, float scale)
            {
                scale = Math.Max(MinTextScale, Math.Min(MaxTextScale, scale));

                FontSize = _originalFontSize * scale;

                using var paint = new SKPaint
                {
                    TextSize = FontSize,
                    IsAntialias = true,
                    TextAlign = SKTextAlign.Left
                };

                var textBounds = new SKRect();
                paint.MeasureText(Text ?? string.Empty, ref textBounds);
                var metrics = paint.FontMetrics;
                var ascentAbs = Math.Abs(metrics.Ascent);
                var descentAbs = Math.Abs(metrics.Descent);
                var newWidth = textBounds.Width;
                var newHeight = ascentAbs + descentAbs;

                var centerX = original.MidX;
                var centerY = original.MidY;
                var left = centerX - newWidth / 2f;
                var top = centerY - newHeight / 2f;

                if (left < 0) left = 0;
                if (top < 0) top = 0;

                Position = new SKPoint(left, top + ascentAbs);
            }
        }

        private enum ToolMode { Brush, Eraser, Arrow, Circle, Square, Check, Cross, Text }
    }
}
