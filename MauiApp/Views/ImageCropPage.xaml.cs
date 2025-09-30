using SkiaSharp;
using SkiaSharp.Views.Maui;
using System.Windows.Input;

namespace MauiApp.Views;

public partial class ImageCropPage : ContentPage
{
    private SKBitmap? _originalImage;
    private SKBitmap? _displayImage;
    private SKRect _cropRect;
    private bool _isDragging = false;
    private string? _draggedHandle;
    private Point _lastTouchPoint;
    private double _canvasWidth;
    private double _canvasHeight;
    private double _imageScaleX;
    private double _imageScaleY;
    private double _imageOffsetX;
    private double _imageOffsetY;

    public string? ImagePath { get; set; }
    public string? CroppedImagePath { get; private set; }

    public ImageCropPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        
        if (!string.IsNullOrEmpty(ImagePath))
        {
            await LoadImage();
        }
    }

    private async Task LoadImage()
    {
        try
        {
            if (string.IsNullOrEmpty(ImagePath) || !File.Exists(ImagePath))
            {
                await DisplayAlert("Error", "Image file not found", "OK");
                await Navigation.PopAsync();
                return;
            }

            using var stream = File.OpenRead(ImagePath);
            _originalImage = SKBitmap.Decode(stream);
            
            if (_originalImage == null)
            {
                await DisplayAlert("Error", "Failed to load image", "OK");
                await Navigation.PopAsync();
                return;
            }

            // Create display image that fits the canvas
            _displayImage = ResizeImageToFit(_originalImage, 400, 400);
            
            // Initialize crop rectangle (center 60% of the image)
            var imageWidth = _displayImage.Width;
            var imageHeight = _displayImage.Height;
            var cropWidth = imageWidth * 0.6;
            var cropHeight = imageHeight * 0.6;
            var cropX = (imageWidth - cropWidth) / 2;
            var cropY = (imageHeight - cropHeight) / 2;
            
            _cropRect = new SKRect((float)cropX, (float)cropY, (float)(cropX + cropWidth), (float)(cropY + cropHeight));
            
            ImageCanvas.InvalidateSurface();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to load image: {ex.Message}", "OK");
            await Navigation.PopAsync();
        }
    }

    private SKBitmap ResizeImageToFit(SKBitmap original, int maxWidth, int maxHeight)
    {
        var scaleX = (double)maxWidth / original.Width;
        var scaleY = (double)maxHeight / original.Height;
        var scale = Math.Min(scaleX, scaleY);
        
        var newWidth = (int)(original.Width * scale);
        var newHeight = (int)(original.Height * scale);
        
        return original.Resize(new SKImageInfo(newWidth, newHeight), SKFilterQuality.High);
    }

    private void OnCanvasPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        if (_displayImage == null) return;

        _canvasWidth = e.Info.Width;
        _canvasHeight = e.Info.Height;

        // Calculate image position and scale
        var imageWidth = _displayImage.Width;
        var imageHeight = _displayImage.Height;
        
        _imageScaleX = _canvasWidth / imageWidth;
        _imageScaleY = _canvasHeight / imageHeight;
        _imageOffsetX = 0;
        _imageOffsetY = 0;

        // Draw the image
        var imageRect = new SKRect(0, 0, (float)_canvasWidth, (float)_canvasHeight);
        canvas.DrawBitmap(_displayImage, imageRect);

        // Draw crop overlay
        DrawCropOverlay(canvas);
    }

    private void DrawCropOverlay(SKCanvas canvas)
    {
        // Draw semi-transparent overlay outside crop area
        var overlayPaint = new SKPaint
        {
            Color = SKColors.Black.WithAlpha(128),
            Style = SKPaintStyle.Fill
        };

        // Top
        canvas.DrawRect(0, 0, (float)_canvasWidth, (float)(_cropRect.Top * _imageScaleY), overlayPaint);
        // Bottom
        canvas.DrawRect(0, (float)(_cropRect.Bottom * _imageScaleY), (float)_canvasWidth, (float)_canvasHeight, overlayPaint);
        // Left
        canvas.DrawRect(0, (float)(_cropRect.Top * _imageScaleY), (float)(_cropRect.Left * _imageScaleX), (float)(_cropRect.Bottom * _imageScaleY), overlayPaint);
        // Right
        canvas.DrawRect((float)(_cropRect.Right * _imageScaleX), (float)(_cropRect.Top * _imageScaleY), (float)_canvasWidth, (float)(_cropRect.Bottom * _imageScaleY), overlayPaint);

        // Draw crop border
        var borderPaint = new SKPaint
        {
            Color = SKColors.Red,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 3
        };

        var cropDisplayRect = new SKRect(
            (float)(_cropRect.Left * _imageScaleX),
            (float)(_cropRect.Top * _imageScaleY),
            (float)(_cropRect.Right * _imageScaleX),
            (float)(_cropRect.Bottom * _imageScaleY));

        canvas.DrawRect(cropDisplayRect, borderPaint);
    }

    private void OnCanvasTouch(object? sender, SKTouchEventArgs e)
    {
        if (_displayImage == null) return;

        var touchPoint = new Point(e.Location.X, e.Location.Y);

        switch (e.ActionType)
        {
            case SKTouchAction.Pressed:
                HandleTouchDown(touchPoint);
                break;
            case SKTouchAction.Moved:
                HandleTouchMove(touchPoint);
                break;
            case SKTouchAction.Released:
                HandleTouchUp(touchPoint);
                break;
        }

        e.Handled = true;
    }

    private void HandleTouchDown(Point touchPoint)
    {
        _lastTouchPoint = touchPoint;
        
        // Check if touch is on any corner handle
        var handleSize = 40; // Increased handle size for easier touch
        var tolerance = 25; // Increased tolerance for easier touch
        
        var cropDisplayRect = new SKRect(
            (float)(_cropRect.Left * _imageScaleX),
            (float)(_cropRect.Top * _imageScaleY),
            (float)(_cropRect.Right * _imageScaleX),
            (float)(_cropRect.Bottom * _imageScaleY));

        // Check corner handles first
        if (IsPointInHandle(touchPoint, cropDisplayRect.Left, cropDisplayRect.Top, handleSize, tolerance))
        {
            _draggedHandle = "TopLeft";
            _isDragging = true;
        }
        else if (IsPointInHandle(touchPoint, cropDisplayRect.Right, cropDisplayRect.Top, handleSize, tolerance))
        {
            _draggedHandle = "TopRight";
            _isDragging = true;
        }
        else if (IsPointInHandle(touchPoint, cropDisplayRect.Left, cropDisplayRect.Bottom, handleSize, tolerance))
        {
            _draggedHandle = "BottomLeft";
            _isDragging = true;
        }
        else if (IsPointInHandle(touchPoint, cropDisplayRect.Right, cropDisplayRect.Bottom, handleSize, tolerance))
        {
            _draggedHandle = "BottomRight";
            _isDragging = true;
        }
        else if (cropDisplayRect.Contains((float)touchPoint.X, (float)touchPoint.Y))
        {
            // Touch is inside the crop rectangle - allow moving the entire rectangle
            _draggedHandle = "Move";
            _isDragging = true;
        }
        else
        {
            // Touch is outside crop area - reset crop rectangle to center
            ResetCropRectangle();
        }
    }

    private bool IsPointInHandle(Point touchPoint, float handleX, float handleY, float handleSize, float tolerance)
    {
        return touchPoint.X >= handleX - tolerance && touchPoint.X <= handleX + handleSize + tolerance &&
               touchPoint.Y >= handleY - tolerance && touchPoint.Y <= handleY + handleSize + tolerance;
    }

    private void HandleTouchMove(Point touchPoint)
    {
        if (!_isDragging || _draggedHandle == null) return;

        var deltaX = touchPoint.X - _lastTouchPoint.X;
        var deltaY = touchPoint.Y - _lastTouchPoint.Y;

        // Convert screen coordinates to image coordinates
        var imageDeltaX = (float)(deltaX / _imageScaleX);
        var imageDeltaY = (float)(deltaY / _imageScaleY);

        // Update crop rectangle based on dragged handle
        switch (_draggedHandle)
        {
            case "TopLeft":
                _cropRect.Left = Math.Max(0, Math.Min(_cropRect.Right - 50, _cropRect.Left + imageDeltaX));
                _cropRect.Top = Math.Max(0, Math.Min(_cropRect.Bottom - 50, _cropRect.Top + imageDeltaY));
                break;
            case "TopRight":
                _cropRect.Right = Math.Min(_displayImage!.Width, Math.Max(_cropRect.Left + 50, _cropRect.Right + imageDeltaX));
                _cropRect.Top = Math.Max(0, Math.Min(_cropRect.Bottom - 50, _cropRect.Top + imageDeltaY));
                break;
            case "BottomLeft":
                _cropRect.Left = Math.Max(0, Math.Min(_cropRect.Right - 50, _cropRect.Left + imageDeltaX));
                _cropRect.Bottom = Math.Min(_displayImage!.Height, Math.Max(_cropRect.Top + 50, _cropRect.Bottom + imageDeltaY));
                break;
            case "BottomRight":
                _cropRect.Right = Math.Min(_displayImage!.Width, Math.Max(_cropRect.Left + 50, _cropRect.Right + imageDeltaX));
                _cropRect.Bottom = Math.Min(_displayImage!.Height, Math.Max(_cropRect.Top + 50, _cropRect.Bottom + imageDeltaY));
                break;
            case "Move":
                // Move the entire crop rectangle
                var newLeft = _cropRect.Left + imageDeltaX;
                var newTop = _cropRect.Top + imageDeltaY;
                var newRight = _cropRect.Right + imageDeltaX;
                var newBottom = _cropRect.Bottom + imageDeltaY;
                
                // Ensure the crop rectangle stays within image bounds
                if (newLeft >= 0 && newRight <= _displayImage!.Width && newTop >= 0 && newBottom <= _displayImage!.Height)
                {
                    _cropRect.Left = newLeft;
                    _cropRect.Top = newTop;
                    _cropRect.Right = newRight;
                    _cropRect.Bottom = newBottom;
                }
                break;
        }

        _lastTouchPoint = touchPoint;
        ImageCanvas.InvalidateSurface();
    }

    private void HandleTouchUp(Point touchPoint)
    {
        _isDragging = false;
        _draggedHandle = null;
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }

    private async void OnCropClicked(object sender, EventArgs e)
    {
        try
        {
            if (_originalImage == null)
            {
                await DisplayAlert("Error", "No image to crop", "OK");
                return;
            }

            // Calculate crop rectangle in original image coordinates
            var originalWidth = _originalImage.Width;
            var originalHeight = _originalImage.Height;
            var displayWidth = _displayImage!.Width;
            var displayHeight = _displayImage.Height;

            var scaleX = (double)originalWidth / displayWidth;
            var scaleY = (double)originalHeight / displayHeight;

            var originalCropRect = new SKRectI(
                (int)(_cropRect.Left * scaleX),
                (int)(_cropRect.Top * scaleY),
                (int)(_cropRect.Right * scaleX),
                (int)(_cropRect.Bottom * scaleY));

            // Ensure crop rectangle is within bounds
            originalCropRect.Left = Math.Max(0, originalCropRect.Left);
            originalCropRect.Top = Math.Max(0, originalCropRect.Top);
            originalCropRect.Right = Math.Min(originalWidth, originalCropRect.Right);
            originalCropRect.Bottom = Math.Min(originalHeight, originalCropRect.Bottom);

            // Crop the image
            var croppedBitmap = new SKBitmap(originalCropRect.Width, originalCropRect.Height);
            using var canvas = new SKCanvas(croppedBitmap);
            canvas.DrawBitmap(_originalImage, originalCropRect, new SKRect(0, 0, originalCropRect.Width, originalCropRect.Height));

            // Save cropped image
            var croppedImagePath = await SaveCroppedImage(croppedBitmap);
            CroppedImagePath = croppedImagePath;

            // Navigate back to AddReportPage with cropped image
            await Navigation.PopAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to crop image: {ex.Message}", "OK");
        }
    }

    private async Task<string> SaveCroppedImage(SKBitmap croppedBitmap)
    {
        var fileName = $"cropped_{Guid.NewGuid()}.jpg";
        var filePath = Path.Combine(FileSystem.CacheDirectory, fileName);

        using var image = SKImage.FromBitmap(croppedBitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 90);
        using var stream = File.OpenWrite(filePath);
        data.SaveTo(stream);

        return filePath;
    }

    private void ResetCropRectangle()
    {
        if (_displayImage != null)
        {
            // Set initial crop rectangle to center 60% of image
            var imageWidth = _displayImage.Width;
            var imageHeight = _displayImage.Height;
            var cropWidth = imageWidth * 0.6;
            var cropHeight = imageHeight * 0.6;
            var cropX = (imageWidth - cropWidth) / 2;
            var cropY = (imageHeight - cropHeight) / 2;
            
            _cropRect = new SKRect((float)cropX, (float)cropY, (float)(cropX + cropWidth), (float)(cropY + cropHeight));
            ImageCanvas.InvalidateSurface();
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        
        // Clean up resources
        _originalImage?.Dispose();
        _displayImage?.Dispose();
    }
}
