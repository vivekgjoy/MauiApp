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
            SKColor selectedColor;
            if (sender == RedColorButton) selectedColor = SKColors.Red;
            else if (sender == BlueColorButton) selectedColor = SKColors.Blue;
            else if (sender == GreenColorButton) selectedColor = SKColors.Green;
            else if (sender == BlackColorButton) selectedColor = SKColors.Black;
            else return;

            SetColor(selectedColor);

            // If an item is selected, change its color
            if (_selectedItem != null)
            {
                ChangeSelectedItemColor(selectedColor);
            }
        }

        private void SetColor(SKColor color)
        {
            _currentColor = color;
            RedColorButton.Text = _currentColor == SKColors.Red ? "✓" : "";
            BlueColorButton.Text = _currentColor == SKColors.Blue ? "✓" : "";
            GreenColorButton.Text = _currentColor == SKColors.Green ? "✓" : "";
            BlackColorButton.Text = _currentColor == SKColors.Black ? "✓" : "";
        }

        private void ChangeSelectedItemColor(SKColor newColor)
        {
            if (_selectedItem == null) return;

            if (_selectedItem is PathDrawable pathDrawable)
            {
                pathDrawable.Color = newColor;
            }
            else if (_selectedItem is RectDrawable rectDrawable)
            {
                rectDrawable.Color = newColor;
            }
            else if (_selectedItem is TextDrawable textDrawable)
            {
                textDrawable.Color = newColor;
            }

            CanvasView.InvalidateSurface();
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

            // Don't clear selection when switching tools - keep it for deletion
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

        private void OnDeleteClicked(object sender, EventArgs e)
        {
            if (_selectedItem != null)
            {
                _items.Remove(_selectedItem);
                _selectedItem = null;
                CanvasView.InvalidateSurface();
            }
            else
            {
                // If nothing selected, try to select the last item
                if (_items.Count > 0)
                {
                    _selectedItem = _items.Last();
                    _items.Remove(_selectedItem);
                    _selectedItem = null;
                    CanvasView.InvalidateSurface();
                }
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

                SKEncodedOrigin orientation = SKEncodedOrigin.TopLeft;
                int rawWidth = 0, rawHeight = 0;
                SKBitmap? originalBitmap = null;

                using (var stream = File.OpenRead(_imagePath))
                {
                    using var codec = SKCodec.Create(stream);
                    if (codec == null) return null;

                    orientation = codec.EncodedOrigin;
                    var info = codec.Info;
                    rawWidth = info.Width;
                    rawHeight = info.Height;

                    stream.Position = 0;
                    originalBitmap = SKBitmap.Decode(stream);
                    if (originalBitmap == null) return null;
                }

                bool isRotated = orientation == SKEncodedOrigin.LeftTop ||
                                 orientation == SKEncodedOrigin.RightTop ||
                                 orientation == SKEncodedOrigin.LeftBottom ||
                                 orientation == SKEncodedOrigin.RightBottom;

                int finalWidth = isRotated ? rawHeight : rawWidth;
                int finalHeight = isRotated ? rawWidth : rawHeight;

                var displayedRect = GetImageBounds();
                if (!displayedRect.HasValue) return null;

                var displayRect = displayedRect.Value;

                using var finalBitmap = new SKBitmap(finalWidth, finalHeight);
                using var canvas = new SKCanvas(finalBitmap);
                canvas.Clear(SKColors.White);

                canvas.Save();
                ApplyExifOrientation(canvas, orientation, finalWidth, finalHeight);
                var imageDestRect = FitRectToCanvas(originalBitmap.Width, originalBitmap.Height, finalWidth, finalHeight);
                canvas.DrawBitmap(originalBitmap, imageDestRect);
                canvas.Restore();

                canvas.Save();

                canvas.Translate(-displayRect.Left, -displayRect.Top);
                float scaleX = imageDestRect.Width / displayRect.Width;
                float scaleY = imageDestRect.Height / displayRect.Height;
                canvas.Scale(scaleX, scaleY);
                ApplyExifOrientation(canvas, orientation, finalWidth, finalHeight);
                canvas.Translate(imageDestRect.Left, imageDestRect.Top);

                foreach (var item in _items)
                    item.Draw(canvas);

                canvas.Restore();

                var cachePath = FileSystem.CacheDirectory;
                var fileName = $"edited_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";
                var fullPath = Path.Combine(cachePath, fileName);

                using var image = SKImage.FromBitmap(finalBitmap);
                using var data = image.Encode(SKEncodedImageFormat.Jpeg, 95);
                using var fileStream = File.Create(fullPath);
                data.SaveTo(fileStream);

                return fullPath;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Save error: {ex}");
                return null;
            }
        }
        private void ApplyExifOrientation(SKCanvas canvas, SKEncodedOrigin origin, int width, int height)
        {
            float centerX = width / 2f;
            float centerY = height / 2f;
            canvas.Translate(centerX, centerY);

            switch (origin)
            {
                case SKEncodedOrigin.TopRight:
                    canvas.Scale(-1, 1);
                    break;
                case SKEncodedOrigin.BottomRight:
                    canvas.RotateDegrees(180);
                    break;
                case SKEncodedOrigin.BottomLeft:
                    canvas.RotateDegrees(180);
                    canvas.Scale(-1, 1);
                    break;
                case SKEncodedOrigin.LeftTop:
                    canvas.RotateDegrees(-90);
                    canvas.Scale(-1, 1);
                    break;
                case SKEncodedOrigin.RightTop:
                    canvas.RotateDegrees(90);
                    break;
                case SKEncodedOrigin.RightBottom:
                    canvas.RotateDegrees(90);
                    canvas.Scale(-1, 1);
                    break;
                case SKEncodedOrigin.LeftBottom:
                    canvas.RotateDegrees(-90);
                    break;
                case SKEncodedOrigin.TopLeft:
                default:
                    break;
            }

            canvas.Translate(-centerX, -centerY);
        }

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
                            // Add new text - constrain position to image bounds
                            var constrainedPosition = ConstrainPointToImage(_textPosition);
                            var newItem = new TextDrawable(text, constrainedPosition, _currentColor, _brushSize * 10, GetImageBounds);
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
                        
                        if (_currentTool == ToolMode.Brush)
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
                                CanvasView.InvalidateSurface(); // Update to show selection
                            }
                            else
                            {
                                _textPosition = pt;
                                _selectedItem = null;
                                _isDraggingItem = false;
                                _textPressPoint = pt;
                                CanvasView.InvalidateSurface(); // Clear selection
                            }
                        }
                        else
                        {
                            // For shape tools (Arrow, Circle, Square, Check, Cross)
                            if (hit != null)
                            {
                                // Select and allow dragging
                                _selectedItem = hit;
                                _isDraggingItem = true;
                                _lastDragPoint = pt;
                                CanvasView.InvalidateSurface(); // Show selection corners
                            }
                            else
                            {
                                // No item hit - clear selection and create new shape
                                _selectedItem = null;
                                _startPoint = pt;
                                _tempShape = CreateShapeItem(_currentTool, _startPoint, pt, _currentColor, _brushSize);
                                CanvasView.InvalidateSurface(); // Clear selection and show temp shape
                            }
                        }
                    }
                    else if (_activeTouches.Count == 2 && _selectedItem != null)
                    {
                        var pts = _activeTouches.Values.ToList();
                        var currentDistance = Distance(pts[0], pts[1]);
                        
                        // Initialize if this is the first time two fingers are detected
                        if (_initialDistance <= 0f)
                        {
                            _initialDistance = currentDistance != 0f ? currentDistance : 1f;
                            _originalBounds = _selectedItem.GetBounds();
                            // Get initial angle and normalize it
                            _initialRotation = GetAngle(pts[0], pts[1]);
                            // Normalize to -PI to PI range
                            while (_initialRotation > Math.PI) _initialRotation -= 2 * (float)Math.PI;
                            while (_initialRotation < -Math.PI) _initialRotation += 2 * (float)Math.PI;
                        }
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
                            
                            // Constrain the item to image bounds after translation
                            var imageBounds = GetImageBounds();
                            if (imageBounds.HasValue)
                            {
                                var bounds = _selectedItem.GetBounds();
                                var imgBounds = imageBounds.Value;
                                
                                // Constrain horizontally - check right edge first
                                if (bounds.Right > imgBounds.Right)
                                {
                                    var offsetX = imgBounds.Right - bounds.Right;
                                    _selectedItem.Translate(offsetX, 0);
                                    bounds = _selectedItem.GetBounds();
                                }
                                // Then check left edge
                                if (bounds.Left < imgBounds.Left)
                                {
                                    var offsetX = imgBounds.Left - bounds.Left;
                                    _selectedItem.Translate(offsetX, 0);
                                    bounds = _selectedItem.GetBounds();
                                }
                                
                                // Constrain vertically - check bottom edge first
                                if (bounds.Bottom > imgBounds.Bottom)
                                {
                                    var offsetY = imgBounds.Bottom - bounds.Bottom;
                                    _selectedItem.Translate(0, offsetY);
                                    bounds = _selectedItem.GetBounds();
                                }
                                // Then check top edge
                                if (bounds.Top < imgBounds.Top)
                                {
                                    var offsetY = imgBounds.Top - bounds.Top;
                                    _selectedItem.Translate(0, offsetY);
                                }
                            }
                            
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
                            
                            var imageBounds = GetImageBounds();
                            if (imageBounds.HasValue)
                            {
                                var bounds = textItem.GetBounds();
                                var imgBounds = imageBounds.Value;
                                
                                if (bounds.Right > imgBounds.Right)
                                {
                                    var offsetX = imgBounds.Right - bounds.Right;
                                    textItem.Translate(offsetX, 0);
                                    bounds = textItem.GetBounds();
                                }
                                if (bounds.Left < imgBounds.Left)
                                {
                                    var offsetX = imgBounds.Left - bounds.Left;
                                    textItem.Translate(offsetX, 0);
                                    bounds = textItem.GetBounds();
                                }
                                
                                if (bounds.Bottom > imgBounds.Bottom)
                                {
                                    var offsetY = imgBounds.Bottom - bounds.Bottom;
                                    textItem.Translate(0, offsetY);
                                    bounds = textItem.GetBounds();
                                }
                                if (bounds.Top < imgBounds.Top)
                                {
                                    var offsetY = imgBounds.Top - bounds.Top;
                                    textItem.Translate(0, offsetY);
                                }
                            }
                            
                            _lastDragPoint = only;
                            CanvasView.InvalidateSurface();
                        }
                        else if (_tempShape != null)
                        {
                            // Constrain the end point to image bounds
                            var constrainedEnd = ConstrainPointToImage(only);
                            _tempShape.UpdateGeometry(_startPoint, constrainedEnd);
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
                            
                            var imageBounds = GetImageBounds();
                            if (imageBounds.HasValue)
                            {
                                var bounds = textItem.GetBounds();
                                var imgBounds = imageBounds.Value;
                                
                                if (bounds.Right > imgBounds.Right)
                                {
                                    var offsetX = imgBounds.Right - bounds.Right;
                                    textItem.Translate(offsetX, 0);
                                    bounds = textItem.GetBounds();
                                }
                                if (bounds.Left < imgBounds.Left)
                                {
                                    var offsetX = imgBounds.Left - bounds.Left;
                                    textItem.Translate(offsetX, 0);
                                    bounds = textItem.GetBounds();
                                }
                                
                                if (bounds.Bottom > imgBounds.Bottom)
                                {
                                    var offsetY = imgBounds.Bottom - bounds.Bottom;
                                    textItem.Translate(0, offsetY);
                                    bounds = textItem.GetBounds();
                                }
                                if (bounds.Top < imgBounds.Top)
                                {
                                    var offsetY = imgBounds.Top - bounds.Top;
                                    textItem.Translate(0, offsetY);
                                }
                            }
                            
                            CanvasView.InvalidateSurface();
                        }
                        else if (_selectedItem is RotatableDrawable rot)
                        {
                            var newAngle = GetAngle(pts[0], pts[1]);
                            var rotationDelta = newAngle - _initialRotation;
                            
                            // Apply smoothing to rotation for smoother experience
                            // Limit rotation delta to prevent sudden jumps
                            if (Math.Abs(rotationDelta) > Math.PI)
                            {
                                // Handle angle wrap-around
                                if (rotationDelta > Math.PI)
                                    rotationDelta -= 2 * (float)Math.PI;
                                else if (rotationDelta < -Math.PI)
                                    rotationDelta += 2 * (float)Math.PI;
                            }
                            
                            // Apply smoothing factor to make rotation slower and smoother
                            // Use a damping factor (0.4 = 40% of rotation applied per frame, making it smoother)
                            const float rotationSmoothingFactor = 0.4f;
                            
                            // Only apply rotation if it's significant enough to avoid jitter
                            if (Math.Abs(rotationDelta) > 0.005f)
                            {
                                // Apply smoothed rotation
                                var smoothedDelta = rotationDelta * rotationSmoothingFactor;
                                
                                rot.ScaleFromBounds(_originalBounds, scale);
                                rot.ApplyRotation(smoothedDelta);
                                
                                // Update initial rotation to track the smoothed position
                                // This creates a lagged response that feels smoother
                                _initialRotation += smoothedDelta;
                                
                                // Normalize initial rotation to prevent accumulation
                                while (_initialRotation > Math.PI) _initialRotation -= 2 * (float)Math.PI;
                                while (_initialRotation < -Math.PI) _initialRotation += 2 * (float)Math.PI;
                            }
                            else
                            {
                                // Still apply scaling even if rotation is too small
                                rot.ScaleFromBounds(_originalBounds, scale);
                            }
                            
                            // Constrain the shape to image bounds after scaling
                            var imageBounds = GetImageBounds();
                            if (imageBounds.HasValue)
                            {
                                var bounds = rot.GetBounds();
                                var imgBounds = imageBounds.Value;
                                
                                // Constrain horizontally - check right edge first
                                if (bounds.Right > imgBounds.Right)
                                {
                                    var offsetX = imgBounds.Right - bounds.Right;
                                    rot.Translate(offsetX, 0);
                                    bounds = rot.GetBounds();
                                }
                                // Then check left edge
                                if (bounds.Left < imgBounds.Left)
                                {
                                    var offsetX = imgBounds.Left - bounds.Left;
                                    rot.Translate(offsetX, 0);
                                    bounds = rot.GetBounds();
                                }
                                
                                // Constrain vertically - check bottom edge first
                                if (bounds.Bottom > imgBounds.Bottom)
                                {
                                    var offsetY = imgBounds.Bottom - bounds.Bottom;
                                    rot.Translate(0, offsetY);
                                    bounds = rot.GetBounds();
                                }
                                // Then check top edge
                                if (bounds.Top < imgBounds.Top)
                                {
                                    var offsetY = imgBounds.Top - bounds.Top;
                                    rot.Translate(0, offsetY);
                                }
                            }
                            
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
                                    _textPosition = ConstrainPointToImage(pt);
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
                            // Constrain the shape to image bounds before adding
                            var imageBounds = GetImageBounds();
                            if (imageBounds.HasValue)
                            {
                                var bounds = _tempShape.GetBounds();
                                var imgBounds = imageBounds.Value;
                                
                                // Constrain horizontally - check right edge first
                                if (bounds.Right > imgBounds.Right)
                                {
                                    var offsetX = imgBounds.Right - bounds.Right;
                                    _tempShape.Translate(offsetX, 0);
                                    bounds = _tempShape.GetBounds();
                                }
                                // Then check left edge
                                if (bounds.Left < imgBounds.Left)
                                {
                                    var offsetX = imgBounds.Left - bounds.Left;
                                    _tempShape.Translate(offsetX, 0);
                                    bounds = _tempShape.GetBounds();
                                }
                                
                                // Constrain vertically - check bottom edge first
                                if (bounds.Bottom > imgBounds.Bottom)
                                {
                                    var offsetY = imgBounds.Bottom - bounds.Bottom;
                                    _tempShape.Translate(0, offsetY);
                                    bounds = _tempShape.GetBounds();
                                }
                                // Then check top edge
                                if (bounds.Top < imgBounds.Top)
                                {
                                    var offsetY = imgBounds.Top - bounds.Top;
                                    _tempShape.Translate(0, offsetY);
                                }
                            }
                            
                            _items.Add(_tempShape);
                            _tempShape = null;
                            // Clear selection after adding new shape
                            _selectedItem = null;
                        }

                        _initialDistance = 0f;
                        _initialRotation = 0f;
                        // Don't clear _selectedItem here - keep it for deletion
                        // Only clear if we just added a new shape (handled above)

                        CanvasView.InvalidateSurface();
                    }
                    else if (_activeTouches.Count == 1)
                    {
                        // One finger released but another still active - update original bounds to current state
                        // and reset initial distance so the next resize starts from the current size
                        if (_selectedItem != null)
                        {
                            _originalBounds = _selectedItem.GetBounds();
                            _initialDistance = 0f; // Will be recalculated when second finger is pressed
                            _initialRotation = 0f;
                        }
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
            // Constrain points to image bounds
            p1 = ConstrainPointToImage(p1);
            p2 = ConstrainPointToImage(p2);
            
            var rect = new SKRect(Math.Min(p1.X, p2.X), Math.Min(p1.Y, p2.Y),
                                  Math.Max(p1.X, p2.X), Math.Max(p1.Y, p2.Y));
            
            // Constrain the rectangle to image bounds
            rect = ConstrainRectToImage(rect);
            
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
                    using var codec = SKCodec.Create(stream);
                    if (codec != null)
                    {
                        var info = codec.Info;
                        var orientation = codec.EncodedOrigin;
                        
                        // Determine the actual dimensions after rotation
                        int width = info.Width;
                        int height = info.Height;
                        
                        // Swap dimensions if rotated 90 or 270 degrees
                        if (orientation == SKEncodedOrigin.LeftTop || orientation == SKEncodedOrigin.RightBottom ||
                            orientation == SKEncodedOrigin.LeftBottom || orientation == SKEncodedOrigin.RightTop)
                        {
                            (width, height) = (height, width);
                        }
                        
                        using var bmp = SKBitmap.Decode(codec);
                        if (bmp != null)
                        {
                            var rect = FitRectToCanvas(width, height, e.Info.Width, e.Info.Height);
                            
                            canvas.Save();
                            
                            // Apply rotation based on EXIF orientation
                            var centerX = rect.MidX;
                            var centerY = rect.MidY;
                            canvas.Translate(centerX, centerY);
                            
                            switch (orientation)
                            {
                                case SKEncodedOrigin.TopRight:
                                    canvas.Scale(-1, 1);
                                    break;
                                case SKEncodedOrigin.BottomRight:
                                    canvas.RotateDegrees(180);
                                    break;
                                case SKEncodedOrigin.BottomLeft:
                                    canvas.RotateDegrees(180);
                                    canvas.Scale(-1, 1);
                                    break;
                                case SKEncodedOrigin.LeftTop:
                                    canvas.RotateDegrees(-90);
                                    canvas.Scale(-1, 1);
                                    break;
                                case SKEncodedOrigin.RightTop:
                                    canvas.RotateDegrees(90);
                                    break;
                                case SKEncodedOrigin.RightBottom:
                                    canvas.RotateDegrees(90);
                                    canvas.Scale(-1, 1);
                                    break;
                                case SKEncodedOrigin.LeftBottom:
                                    canvas.RotateDegrees(-90);
                                    break;
                                case SKEncodedOrigin.TopLeft:
                                default:
                                    // No rotation needed
                                    break;
                            }
                            
                            canvas.Translate(-centerX, -centerY);
                            
                            // Draw the bitmap
                            canvas.DrawBitmap(bmp, rect);
                            canvas.Restore();
                        }
                    }
                }
                catch 
                {
                    // Fallback to simple decode if codec fails
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
            }

            foreach (var item in _items)
                item.Draw(canvas);

            _tempShape?.Draw(canvas);

            // Draw selection indicators (corners) for selected item
            if (_selectedItem != null)
            {
                DrawSelectionCorners(canvas, _selectedItem.GetBounds());
            }
        }

        private SKRect FitRectToCanvas(int imgW, int imgH, int cw, int ch)
        {
            float ratio = Math.Min((float)cw / imgW, (float)ch / imgH);
            float w = imgW * ratio, h = imgH * ratio;
            float l = (cw - w) / 2, t = (ch - h) / 2;
            return new SKRect(l, t, l + w, t + h);
        }

        private SKRect? GetImageBounds()
        {
            if (string.IsNullOrEmpty(ImagePath) || CanvasView.CanvasSize.Width <= 0 || CanvasView.CanvasSize.Height <= 0)
                return null;

            try
            {
                using var stream = File.OpenRead(ImagePath);
                using var codec = SKCodec.Create(stream);
                if (codec != null)
                {
                    var info = codec.Info;
                    var orientation = codec.EncodedOrigin;
                    
                    // Determine the actual dimensions after rotation
                    int width = info.Width;
                    int height = info.Height;
                    
                    // Swap dimensions if rotated 90 or 270 degrees
                    if (orientation == SKEncodedOrigin.LeftTop || orientation == SKEncodedOrigin.RightBottom ||
                        orientation == SKEncodedOrigin.LeftBottom || orientation == SKEncodedOrigin.RightTop)
                    {
                        (width, height) = (height, width);
                    }
                    
                    return FitRectToCanvas(width, height, (int)CanvasView.CanvasSize.Width, (int)CanvasView.CanvasSize.Height);
                }
            }
            catch 
            {
                // Fallback to simple decode if codec fails
                try
                {
                    using var stream = File.OpenRead(ImagePath);
                    using var bmp = SKBitmap.Decode(stream);
                    if (bmp != null)
                    {
                        return FitRectToCanvas(bmp.Width, bmp.Height, (int)CanvasView.CanvasSize.Width, (int)CanvasView.CanvasSize.Height);
                    }
                }
                catch { }
            }
            return null;
        }

        private SKPoint ConstrainPointToImage(SKPoint point)
        {
            var imageBounds = GetImageBounds();
            if (!imageBounds.HasValue)
                return point;

            var bounds = imageBounds.Value;
            return new SKPoint(
                Math.Max(bounds.Left, Math.Min(bounds.Right, point.X)),
                Math.Max(bounds.Top, Math.Min(bounds.Bottom, point.Y))
            );
        }

        private SKRect ConstrainRectToImage(SKRect rect)
        {
            var imageBounds = GetImageBounds();
            if (!imageBounds.HasValue)
                return rect;

            var bounds = imageBounds.Value;
            var constrained = rect;
            
            // First, ensure the rectangle doesn't extend beyond the right edge
            if (constrained.Right > bounds.Right)
            {
                var offsetX = bounds.Right - constrained.Right;
                constrained = new SKRect(
                    constrained.Left + offsetX,
                    constrained.Top,
                    bounds.Right,
                    constrained.Bottom
                );
            }
            
            // Then, ensure it doesn't extend beyond the bottom edge
            if (constrained.Bottom > bounds.Bottom)
            {
                var offsetY = bounds.Bottom - constrained.Bottom;
                constrained = new SKRect(
                    constrained.Left,
                    constrained.Top + offsetY,
                    constrained.Right,
                    bounds.Bottom
                );
            }
            
            // Ensure it doesn't extend beyond the left edge
            if (constrained.Left < bounds.Left)
            {
                var offsetX = bounds.Left - constrained.Left;
                constrained = new SKRect(
                    bounds.Left,
                    constrained.Top,
                    constrained.Right + offsetX,
                    constrained.Bottom
                );
            }
            
            // Ensure it doesn't extend beyond the top edge
            if (constrained.Top < bounds.Top)
            {
                var offsetY = bounds.Top - constrained.Top;
                constrained = new SKRect(
                    constrained.Left,
                    bounds.Top,
                    constrained.Right,
                    constrained.Bottom + offsetY
                );
            }
            
            // If the rectangle is still larger than the image bounds after positioning, scale it down
            if (constrained.Width > bounds.Width || constrained.Height > bounds.Height)
            {
                var scaleX = bounds.Width / constrained.Width;
                var scaleY = bounds.Height / constrained.Height;
                var scale = Math.Min(scaleX, scaleY);
                
                var newWidth = constrained.Width * scale;
                var newHeight = constrained.Height * scale;
                var centerX = constrained.MidX;
                var centerY = constrained.MidY;
                
                // Ensure the scaled rectangle stays within bounds
                var scaledLeft = centerX - newWidth / 2f;
                var scaledTop = centerY - newHeight / 2f;
                var scaledRight = centerX + newWidth / 2f;
                var scaledBottom = centerY + newHeight / 2f;
                
                // Constrain the scaled rectangle
                if (scaledLeft < bounds.Left)
                {
                    scaledRight += bounds.Left - scaledLeft;
                    scaledLeft = bounds.Left;
                }
                if (scaledRight > bounds.Right)
                {
                    scaledLeft -= scaledRight - bounds.Right;
                    scaledRight = bounds.Right;
                }
                if (scaledTop < bounds.Top)
                {
                    scaledBottom += bounds.Top - scaledTop;
                    scaledTop = bounds.Top;
                }
                if (scaledBottom > bounds.Bottom)
                {
                    scaledTop -= scaledBottom - bounds.Bottom;
                    scaledBottom = bounds.Bottom;
                }
                
                constrained = new SKRect(scaledLeft, scaledTop, scaledRight, scaledBottom);
            }

            // Ensure width and height are valid
            if (constrained.Width < 0)
            {
                constrained.Left = constrained.Right = (constrained.Left + constrained.Right) / 2;
            }
            if (constrained.Height < 0)
            {
                constrained.Top = constrained.Bottom = (constrained.Top + constrained.Bottom) / 2;
            }

            return constrained;
        }

        private void DrawSelectionCorners(SKCanvas canvas, SKRect bounds)
        {
            const float cornerRadius = 14f; // Larger radius for better visibility
            
            using var paint = new SKPaint
            {
                Color = SKColors.Blue,
                Style = SKPaintStyle.Fill, // Solid fill instead of stroke
                IsAntialias = true
            };

            // Expand bounds slightly for better visibility
            var expanded = bounds;
            expanded.Inflate(2, 2);

            // Top-left corner
            canvas.DrawCircle(expanded.Left, expanded.Top, cornerRadius, paint);
            
            // Top-right corner
            canvas.DrawCircle(expanded.Right, expanded.Top, cornerRadius, paint);
            
            // Bottom-left corner
            canvas.DrawCircle(expanded.Left, expanded.Bottom, cornerRadius, paint);
            
            // Bottom-right corner
            canvas.DrawCircle(expanded.Right, expanded.Bottom, cornerRadius, paint);
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
            public SKColor Color { get; set; }
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
            public SKColor Color { get; set; }
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

                using var p = new SKPaint 
                { 
                    Style = SKPaintStyle.Stroke, 
                    StrokeWidth = Stroke, 
                    Color = Color, 
                    IsAntialias = true,
                    StrokeCap = SKStrokeCap.Round,
                    StrokeJoin = SKStrokeJoin.Round
                };

                // Draw the arrow shaft
                var start = new SKPoint(Rect.Left, Rect.MidY);
                var end = new SKPoint(Rect.Right, Rect.MidY);
                c.DrawLine(start, end, p);

                // Calculate arrow head size - proportional to arrow length but with min/max bounds
                var arrowLength = Rect.Width;
                var headLength = Math.Min(arrowLength * 0.25f, Math.Max(arrowLength * 0.2f, Stroke * 2.5f));
                var headWidth = headLength * 0.7f; // Width of arrow head base
                
                // Arrow points to the right, so tip is at end.X
                var tip = new SKPoint(end.X, end.Y);
                
                // Calculate arrow head points - create a sharp triangle pointing right
                // The base of the triangle is perpendicular to the arrow direction
                var baseCenterX = end.X - headLength;
                var baseCenterY = end.Y;
                
                // Top point of arrow head base
                var topPoint = new SKPoint(baseCenterX, baseCenterY - headWidth / 2f);
                
                // Bottom point of arrow head base
                var bottomPoint = new SKPoint(baseCenterX, baseCenterY + headWidth / 2f);
                
                // Draw arrow head as a filled triangle for sharp appearance
                using var headPaint = new SKPaint
                {
                    Style = SKPaintStyle.Fill,
                    Color = Color,
                    IsAntialias = true
                };
                
                var arrowPath = new SKPath();
                arrowPath.MoveTo(tip);
                arrowPath.LineTo(topPoint);
                arrowPath.LineTo(bottomPoint);
                arrowPath.Close();
                c.DrawPath(arrowPath, headPaint);
                
                // Also draw outline for better visibility
                using var outlinePaint = new SKPaint
                {
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = Stroke,
                    Color = Color,
                    IsAntialias = true,
                    StrokeCap = SKStrokeCap.Round,
                    StrokeJoin = SKStrokeJoin.Round
                };
                c.DrawPath(arrowPath, outlinePaint);

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

                using var p = new SKPaint
                {
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = Stroke * 1.2f, // Slightly thicker
                    Color = Color,
                    IsAntialias = true,
                    StrokeCap = SKStrokeCap.Round
                };

                float padding = Stroke * 2;
                var left = Rect.Left + padding;
                var top = Rect.Top + padding;
                var right = Rect.Right - padding;
                var bottom = Rect.Bottom - padding;

                c.DrawLine(left, top, right, bottom, p);
                c.DrawLine(right, top, left, bottom, p);
                c.Restore();
            }
        }

        private class TextDrawable : RotatableDrawable
        {
            public string Text 
            { 
                get => _text;
                set 
                { 
                    _text = value;
                    _wrappedLines = null; // Invalidate cache when text changes
                }
            }
            private string _text;
            public SKPoint Position { get; private set; }
            public SKColor Color { get; set; }
            public float FontSize { get; private set; }
            private readonly float _originalFontSize;
            private const float MinTextScale = 0.3f;
            private const float MaxTextScale = 4.0f;
            private List<string>? _wrappedLines;
            private float? _maxWidth;
            private readonly Func<SKRect?> _getImageBounds;

            public TextDrawable(string text, SKPoint position, SKColor color, float fontSize, Func<SKRect?> getImageBounds)
            {
                _text = text;
                Position = position;
                Color = color;
                _originalFontSize = fontSize;
                FontSize = fontSize;
                _getImageBounds = getImageBounds;
                _wrappedLines = null; // Will be calculated when needed
            }

            private List<string> GetWrappedLines(float maxWidth)
            {
                if (_wrappedLines != null && _maxWidth.HasValue && Math.Abs(_maxWidth.Value - maxWidth) < 0.1f)
                    return _wrappedLines;

                _maxWidth = maxWidth;
                _wrappedLines = new List<string>();

                if (string.IsNullOrEmpty(Text))
                {
                    _wrappedLines.Add("");
                    return _wrappedLines;
                }

                using var paint = new SKPaint
                {
                    TextSize = FontSize,
                    IsAntialias = true,
                    TextAlign = SKTextAlign.Left
                };

                var words = Text.Split(' ');
                var currentLine = "";

                foreach (var word in words)
                {
                    var testLine = string.IsNullOrEmpty(currentLine) ? word : currentLine + " " + word;
                    var bounds = new SKRect();
                    paint.MeasureText(testLine, ref bounds);

                    if (bounds.Width <= maxWidth || string.IsNullOrEmpty(currentLine))
                    {
                        currentLine = testLine;
                    }
                    else
                    {
                        _wrappedLines.Add(currentLine);
                        currentLine = word;
                    }
                }

                if (!string.IsNullOrEmpty(currentLine))
                {
                    _wrappedLines.Add(currentLine);
                }

                if (_wrappedLines.Count == 0)
                {
                    _wrappedLines.Add("");
                }

                return _wrappedLines;
            }

            public override void Draw(SKCanvas c)
            {
                if (string.IsNullOrEmpty(Text))
                    return;

                using var paint = new SKPaint
                {
                    Color = Color,
                    IsAntialias = true,
                    TextSize = FontSize,
                    IsStroke = false,
                    TextAlign = SKTextAlign.Left
                };

                // Get image bounds to calculate max width for wrapping
                var imageBounds = GetImageBoundsForText();
                float maxWidth;
                
                if (imageBounds.HasValue)
                {
                    var bounds = imageBounds.Value;
                    // Calculate available width from current position to right edge of image
                    var availableWidth = bounds.Right - Position.X;
                    // Use 95% of available width or full image width, whichever is smaller
                    maxWidth = Math.Min(availableWidth * 0.95f, bounds.Width * 0.95f);
                    
                    // Ensure minimum width
                    if (maxWidth < 50)
                        maxWidth = bounds.Width * 0.9f; // Fallback to 90% of image width
                }
                else
                {
                    maxWidth = 500; // Fallback width
                }

                var lines = GetWrappedLines(maxWidth);
                var fontMetrics = paint.FontMetrics;
                var lineHeight = Math.Abs(fontMetrics.Ascent) + Math.Abs(fontMetrics.Descent);
                var lineSpacing = lineHeight * 0.2f; // 20% spacing between lines
                var totalLineHeight = lineHeight + lineSpacing;

                c.Save();
                c.Translate(Position.X, Position.Y);
                c.RotateRadians(rotation);

                float yOffset = 0;
                foreach (var line in lines)
                {
                    if (!string.IsNullOrEmpty(line))
                    {
                        c.DrawText(line, 0, yOffset, paint);
                    }
                    yOffset += totalLineHeight;
                }

                c.Restore();
            }

            private SKRect? GetImageBoundsForText()
            {
                return _getImageBounds?.Invoke();
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

                // Get image bounds to calculate max width for wrapping
                var imageBounds = GetImageBoundsForText();
                float maxWidth;
                
                if (imageBounds.HasValue)
                {
                    var bounds = imageBounds.Value;
                    // Calculate available width from current position to right edge of image
                    var availableWidth = bounds.Right - Position.X;
                    // Use 95% of available width or full image width, whichever is smaller
                    maxWidth = Math.Min(availableWidth * 0.95f, bounds.Width * 0.95f);
                    
                    // Ensure minimum width
                    if (maxWidth < 50)
                        maxWidth = bounds.Width * 0.9f; // Fallback to 90% of image width
                }
                else
                {
                    maxWidth = 500; // Fallback width
                }

                var lines = GetWrappedLines(maxWidth);
                var fontMetrics = paint.FontMetrics;
                var lineHeight = Math.Abs(fontMetrics.Ascent) + Math.Abs(fontMetrics.Descent);
                var lineSpacing = lineHeight * 0.2f;
                var totalLineHeight = lineHeight + lineSpacing;

                // Calculate the maximum width of all lines
                float maxLineWidth = 0;
                foreach (var line in lines)
                {
                    if (!string.IsNullOrEmpty(line))
                    {
                        var bounds = new SKRect();
                        paint.MeasureText(line, ref bounds);
                        maxLineWidth = Math.Max(maxLineWidth, bounds.Width);
                    }
                }

                var totalHeight = lines.Count * totalLineHeight - lineSpacing; // Subtract last line spacing
                
                var result = new SKRect(
                    Position.X,                    // Left
                    Position.Y - Math.Abs(fontMetrics.Ascent),
                    Position.X + maxLineWidth,     // Right
                    Position.Y - Math.Abs(fontMetrics.Ascent) + totalHeight
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

                var originalCenterX = original.MidX;
                var originalCenterY = original.MidY;

                FontSize = _originalFontSize * scale;
                _wrappedLines = null;

                var imageBounds = GetImageBoundsForText();
                
                using var paint = new SKPaint
                {
                    TextSize = FontSize,
                    IsAntialias = true,
                    TextAlign = SKTextAlign.Left
                };

                var tempPositionX = originalCenterX;
                
                float maxWidth;
                if (imageBounds.HasValue)
                {
                    var bounds = imageBounds.Value;
                    var availableWidth = bounds.Right - tempPositionX;
                    maxWidth = Math.Min(availableWidth * 0.95f, bounds.Width * 0.95f);
                    
                    if (maxWidth < 50)
                        maxWidth = bounds.Width * 0.9f;
                }
                else
                {
                    maxWidth = 500f;
                }
                
                var lines = GetWrappedLines(maxWidth);
                var metrics = paint.FontMetrics;
                var ascentAbs = Math.Abs(metrics.Ascent);
                var descentAbs = Math.Abs(metrics.Descent);
                var lineHeight = ascentAbs + descentAbs;
                var lineSpacing = lineHeight * 0.2f;
                var totalLineHeight = lineHeight + lineSpacing;
                var totalHeight = lines.Count * totalLineHeight - lineSpacing;

                float maxLineWidth = 0;
                foreach (var line in lines)
                {
                    if (!string.IsNullOrEmpty(line))
                    {
                        var bounds = new SKRect();
                        paint.MeasureText(line, ref bounds);
                        maxLineWidth = Math.Max(maxLineWidth, bounds.Width);
                    }
                }

                var newCenterX = originalCenterX;
                var newCenterY = originalCenterY;
                
                var newBaselineY = newCenterY - totalHeight / 2f + ascentAbs;
                var newLeftX = newCenterX - maxLineWidth / 2f;

                if (imageBounds.HasValue)
                {
                    var bounds = imageBounds.Value;
                    
                    if (newLeftX < bounds.Left || newLeftX + maxLineWidth > bounds.Right)
                    {
                        newLeftX = Math.Max(bounds.Left, newLeftX);
                        
                        var constrainedAvailableWidth = bounds.Right - newLeftX;
                        var constrainedMaxWidth = Math.Min(constrainedAvailableWidth * 0.95f, bounds.Width * 0.95f);
                        
                        if (constrainedMaxWidth < 50)
                            constrainedMaxWidth = bounds.Width * 0.9f;
                        
                        _wrappedLines = null;
                        lines = GetWrappedLines(constrainedMaxWidth);
                        
                        maxLineWidth = 0;
                        foreach (var line in lines)
                        {
                            if (!string.IsNullOrEmpty(line))
                            {
                                var lineBounds = new SKRect();
                                paint.MeasureText(line, ref lineBounds);
                                maxLineWidth = Math.Max(maxLineWidth, lineBounds.Width);
                            }
                        }
                        
                        totalHeight = lines.Count * totalLineHeight - lineSpacing;
                        newBaselineY = newCenterY - totalHeight / 2f + ascentAbs;
                        
                        if (newLeftX + maxLineWidth > bounds.Right)
                        {
                            newLeftX = bounds.Right - maxLineWidth;
                        }
                    }
                    
                    var textBounds = new SKRect(
                        newLeftX,
                        newBaselineY - ascentAbs,
                        newLeftX + maxLineWidth,
                        newBaselineY - ascentAbs + totalHeight
                    );
                    
                    if (textBounds.Right > bounds.Right)
                    {
                        newLeftX = bounds.Right - maxLineWidth;
                        textBounds.Left = newLeftX;
                        textBounds.Right = bounds.Right;
                    }
                    if (textBounds.Left < bounds.Left)
                    {
                        newLeftX = bounds.Left;
                    }
                    
                    textBounds = new SKRect(
                        newLeftX,
                        newBaselineY - ascentAbs,
                        newLeftX + maxLineWidth,
                        newBaselineY - ascentAbs + totalHeight
                    );
                    
                    if (textBounds.Bottom > bounds.Bottom)
                    {
                        newBaselineY = bounds.Bottom - totalHeight + ascentAbs;
                        textBounds.Bottom = bounds.Bottom;
                        textBounds.Top = newBaselineY - ascentAbs;
                    }
                    if (textBounds.Top < bounds.Top)
                    {
                        newBaselineY = bounds.Top + ascentAbs;
                    }
                }

                Position = new SKPoint(newLeftX, newBaselineY);
            }
        }

        private enum ToolMode { Brush, Eraser, Arrow, Circle, Square, Check, Cross, Text }
    }
}

