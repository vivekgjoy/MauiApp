using Microsoft.Maui.Graphics;
using Microsoft.Maui.Storage;
using SkiaSharp;

namespace MauiApp.Views;

public partial class ImageCropPage : ContentPage
{
    private string? _imagePath;
    private double _left = 50, _top = 50, _right = 300, _bottom = 400;
    private double _cropWidth = 250;
    private double _cropHeight = 350;
    private bool _isDraggingFrame = false;
    private Point _lastPanPoint;

    public string? ImagePath
    {
        get => _imagePath;
        set
        {
            _imagePath = value;
            if (!string.IsNullOrEmpty(value))
                CropImage.Source = ImageSource.FromFile(value);
        }
    }

    public string? CroppedImagePath { get; private set; }

    public ImageCropPage()
    {
        InitializeComponent();
        this.Loaded += OnPageLoaded;
        CropImage.SizeChanged += OnImageSizeChanged;
    }

    private void OnPageLoaded(object sender, EventArgs e)
    {
        // Center the crop frame when page loads
        CenterCropFrame();
    }

    private void OnImageSizeChanged(object sender, EventArgs e)
    {
        // Center the crop frame when image size is available
        if (CropImage.Width > 0 && CropImage.Height > 0)
        {
            CenterCropFrame();
        }
    }

    private void CenterCropFrame()
    {
        if (CropImage.Width <= 0 || CropImage.Height <= 0)
        {
            // Image not loaded yet, will be centered when size is available
            return;
        }

        // Calculate center position
        var imageWidth = CropImage.Width;
        var imageHeight = CropImage.Height;

        // Keep the same crop size, just center it
        _cropWidth = _right - _left;
        _cropHeight = _bottom - _top;

        // Center the crop frame
        _left = (imageWidth - _cropWidth) / 2;
        _top = (imageHeight - _cropHeight) / 2;
        _right = _left + _cropWidth;
        _bottom = _top + _cropHeight;

        // Ensure it stays within bounds
        if (_left < 0) _left = 0;
        if (_top < 0) _top = 0;
        if (_right > imageWidth)
        {
            _right = imageWidth;
            _left = _right - _cropWidth;
        }
        if (_bottom > imageHeight)
        {
            _bottom = imageHeight;
            _top = _bottom - _cropHeight;
        }

        UpdateCropOverlay();
    }

    private void OnHandlePanUpdated(object sender, PanUpdatedEventArgs e)
    {
        if (sender == CropRectangle)
        {
            if (e.StatusType == GestureStatus.Started)
            {
                _isDraggingFrame = true;
                _lastPanPoint = new Point(e.TotalX, e.TotalY);
            }
            else if (e.StatusType == GestureStatus.Running && _isDraggingFrame)
            {
                var imageWidth = CropImage.Width;
                var imageHeight = CropImage.Height;

                if (imageWidth <= 0 || imageHeight <= 0)
                    return;

                double dx = e.TotalX - _lastPanPoint.X;
                double dy = e.TotalY - _lastPanPoint.Y;

                _lastPanPoint = new Point(e.TotalX, e.TotalY);

                if (Math.Abs(dx) < 0.5 && Math.Abs(dy) < 0.5)
                    return;

                double newLeft = _left + dx;
                double newTop = _top + dy;
                double newRight = _right + dx;
                double newBottom = _bottom + dy;

                if (newLeft < 0)
                {
                    double offset = 0 - newLeft;
                    newLeft = 0;
                    newRight = Math.Min(imageWidth, newRight + offset);
                }
                if (newTop < 0)
                {
                    double offset = 0 - newTop;
                    newTop = 0;
                    newBottom = Math.Min(imageHeight, newBottom + offset);
                }
                if (newRight > imageWidth)
                {
                    double offset = newRight - imageWidth;
                    newRight = imageWidth;
                    newLeft = Math.Max(0, newLeft - offset);
                }
                if (newBottom > imageHeight)
                {
                    double offset = newBottom - imageHeight;
                    newBottom = imageHeight;
                    newTop = Math.Max(0, newTop - offset);
                }

                double cropWidth = newRight - newLeft;
                double cropHeight = newBottom - newTop;
                
                if (cropWidth < 50)
                {
                    if (newLeft == 0)
                        newRight = 50;
                    else if (newRight == imageWidth)
                        newLeft = imageWidth - 50;
                    else
                    {
                        double adjust = (50 - cropWidth) / 2;
                        newLeft = Math.Max(0, newLeft - adjust);
                        newRight = Math.Min(imageWidth, newRight + adjust);
                    }
                }
                if (cropHeight < 50)
                {
                    if (newTop == 0)
                        newBottom = 50;
                    else if (newBottom == imageHeight)
                        newTop = imageHeight - 50;
                    else
                    {
                        double adjust = (50 - cropHeight) / 2;
                        newTop = Math.Max(0, newTop - adjust);
                        newBottom = Math.Min(imageHeight, newBottom + adjust);
                    }
                }

                _left = newLeft;
                _top = newTop;
                _right = newRight;
                _bottom = newBottom;

                UpdateCropOverlay();
            }
            else if (e.StatusType == GestureStatus.Canceled || e.StatusType == GestureStatus.Completed)
            {
                _isDraggingFrame = false;
            }
            return;
        }
        if (sender is not Frame frame || e.StatusType != GestureStatus.Running)
            return;

        double handleDx = e.TotalX;
        double handleDy = e.TotalY;

        if (frame == TopLeftHandle)
        {
            _left = Math.Max(0, _left + handleDx);
            _top = Math.Max(0, _top + handleDy);
            // Ensure minimum size
            if (_right - _left < 50) _left = _right - 50;
            if (_bottom - _top < 50) _top = _bottom - 50;
        }
        else if (frame == TopRightHandle)
        {
            _right = Math.Max(_left + 50, _right + handleDx);
            _top = Math.Max(0, _top + handleDy);
            if (_right > CropImage.Width) _right = CropImage.Width;
            if (_bottom - _top < 50) _top = _bottom - 50;
        }
        else if (frame == BottomLeftHandle)
        {
            _left = Math.Max(0, _left + handleDx);
            _bottom = Math.Max(_top + 50, _bottom + handleDy);
            if (_left < 0) _left = 0;
            if (_bottom > CropImage.Height) _bottom = CropImage.Height;
            if (_right - _left < 50) _left = _right - 50;
        }
        else if (frame == BottomRightHandle)
        {
            _right = Math.Max(_left + 50, _right + handleDx);
            _bottom = Math.Max(_top + 50, _bottom + handleDy);
            if (_right > CropImage.Width) _right = CropImage.Width;
            if (_bottom > CropImage.Height) _bottom = CropImage.Height;
        }

        // Update crop dimensions
        _cropWidth = _right - _left;
        _cropHeight = _bottom - _top;

        UpdateCropOverlay();
    }

    private void UpdateCropOverlay()
    {
        // Update rectangle bounds
        AbsoluteLayout.SetLayoutBounds(CropRectangle, new Rect(_left, _top, _right - _left, _bottom - _top));

        // Update handle positions
        AbsoluteLayout.SetLayoutBounds(TopLeftHandle, new Rect(_left - 15, _top - 15, 30, 30));
        AbsoluteLayout.SetLayoutBounds(TopRightHandle, new Rect(_right - 15, _top - 15, 30, 30));
        AbsoluteLayout.SetLayoutBounds(BottomLeftHandle, new Rect(_left - 15, _bottom - 15, 30, 30));
        AbsoluteLayout.SetLayoutBounds(BottomRightHandle, new Rect(_right - 15, _bottom - 15, 30, 30));
    }

    private async void OnCropClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrEmpty(_imagePath))
        {
            await DisplayAlert("Error", "No image selected", "OK");
            return;
        }

        try
        {
            var croppedImagePath = await CropImageAsync(_imagePath);

            if (string.IsNullOrEmpty(croppedImagePath))
            {
                await DisplayAlert("Error", "Failed to crop image", "OK");
                return;
            }

            CroppedImagePath = croppedImagePath;

            // Navigate to ImageEditPage with the cropped image
            var imageEditPage = new ImageEditPage { ImagePath = croppedImagePath };
            await Navigation.PushAsync(imageEditPage);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed: {ex.Message}", "OK");
        }
    }

    private async Task<string?> CropImageAsync(string imagePath)
    {
        try
        {
            if (!File.Exists(imagePath))
                return null;

            // Read EXIF orientation first
            SKEncodedOrigin orientation = SKEncodedOrigin.TopLeft;
            int rawImageWidth = 0;
            int rawImageHeight = 0;
            int displayImageWidth = 0;
            int displayImageHeight = 0;

            using (var orientationStream = File.OpenRead(imagePath))
            {
                using var codec = SKCodec.Create(orientationStream);
                if (codec != null)
                {
                    var info = codec.Info;
                    orientation = codec.EncodedOrigin;
                    rawImageWidth = info.Width;
                    rawImageHeight = info.Height;

                    // Calculate display dimensions (accounting for rotation)
                    if (orientation == SKEncodedOrigin.LeftTop || orientation == SKEncodedOrigin.RightBottom ||
                        orientation == SKEncodedOrigin.LeftBottom || orientation == SKEncodedOrigin.RightTop)
                    {
                        displayImageWidth = rawImageHeight;
                        displayImageHeight = rawImageWidth;
                    }
                    else
                    {
                        displayImageWidth = rawImageWidth;
                        displayImageHeight = rawImageHeight;
                    }
                }
            }

            using var originalImage = SKBitmap.Decode(imagePath);
            if (originalImage == null)
                return null;

            var imageWidth = originalImage.Width;
            var imageHeight = originalImage.Height;

            var displayWidth = CropImage.Width;
            var displayHeight = CropImage.Height;

            int actualLeft, actualTop, actualRight, actualBottom;

            if (displayWidth <= 0 || displayHeight <= 0)
            {
                actualLeft = 0;
                actualTop = 0;
                actualRight = imageWidth;
                actualBottom = imageHeight;
            }
            else
            {
                float ratio = Math.Min((float)displayWidth / displayImageWidth, (float)displayHeight / displayImageHeight);
                float displayedImageWidth = displayImageWidth * ratio;
                float displayedImageHeight = displayImageHeight * ratio;
                float displayedImageLeft = (float)(displayWidth - displayedImageWidth) / 2f;
                float displayedImageTop = (float)(displayHeight - displayedImageHeight) / 2f;
                float displayedImageRight = displayedImageLeft + displayedImageWidth;
                float displayedImageBottom = displayedImageTop + displayedImageHeight;

                double cropLeftRelative = _left - displayedImageLeft;
                double cropTopRelative = _top - displayedImageTop;
                double cropRightRelative = _right - displayedImageLeft;
                double cropBottomRelative = _bottom - displayedImageTop;

                cropLeftRelative = Math.Max(0, Math.Min(cropLeftRelative, displayedImageWidth));
                cropTopRelative = Math.Max(0, Math.Min(cropTopRelative, displayedImageHeight));
                cropRightRelative = Math.Max(cropLeftRelative, Math.Min(cropRightRelative, displayedImageWidth));
                cropBottomRelative = Math.Max(cropTopRelative, Math.Min(cropBottomRelative, displayedImageHeight));

                double scaleX = (double)displayImageWidth / displayedImageWidth;
                double scaleY = (double)displayImageHeight / displayedImageHeight;

                double scaledLeft = cropLeftRelative * scaleX;
                double scaledTop = cropTopRelative * scaleY;
                double scaledRight = cropRightRelative * scaleX;
                double scaledBottom = cropBottomRelative * scaleY;

                double rawLeft, rawTop, rawRight, rawBottom;

                switch (orientation)
                {
                    case SKEncodedOrigin.TopRight:
                        rawLeft = displayImageWidth - scaledRight;
                        rawTop = scaledTop;
                        rawRight = displayImageWidth - scaledLeft;
                        rawBottom = scaledBottom;
                        break;
                    case SKEncodedOrigin.BottomRight:
                        rawLeft = displayImageWidth - scaledRight;
                        rawTop = displayImageHeight - scaledBottom;
                        rawRight = displayImageWidth - scaledLeft;
                        rawBottom = displayImageHeight - scaledTop;
                        break;
                    case SKEncodedOrigin.BottomLeft:
                        rawLeft = scaledLeft;
                        rawTop = displayImageHeight - scaledBottom;
                        rawRight = scaledRight;
                        rawBottom = displayImageHeight - scaledTop;
                        break;
                    case SKEncodedOrigin.LeftTop:
                        rawLeft = displayImageHeight - scaledBottom;
                        rawTop = scaledLeft;
                        rawRight = displayImageHeight - scaledTop;
                        rawBottom = scaledRight;
                        break;
                    case SKEncodedOrigin.RightTop:
                        rawLeft = scaledTop;
                        rawTop = displayImageWidth - scaledRight;
                        rawRight = scaledBottom;
                        rawBottom = displayImageWidth - scaledLeft;
                        break;
                    case SKEncodedOrigin.RightBottom:
                        rawLeft = displayImageHeight - scaledTop;
                        rawTop = displayImageWidth - scaledRight;
                        rawRight = displayImageHeight - scaledBottom;
                        rawBottom = displayImageWidth - scaledLeft;
                        break;
                    case SKEncodedOrigin.LeftBottom:
                        rawLeft = displayImageHeight - scaledBottom;
                        rawTop = displayImageWidth - scaledRight;
                        rawRight = displayImageHeight - scaledTop;
                        rawBottom = displayImageWidth - scaledLeft;
                        break;
                    case SKEncodedOrigin.TopLeft:
                    default:
                        rawLeft = scaledLeft;
                        rawTop = scaledTop;
                        rawRight = scaledRight;
                        rawBottom = scaledBottom;
                        break;
                }

                actualLeft = (int)rawLeft;
                actualTop = (int)rawTop;
                actualRight = (int)rawRight;
                actualBottom = (int)rawBottom;

                actualLeft = Math.Max(0, Math.Min(actualLeft, imageWidth - 1));
                actualTop = Math.Max(0, Math.Min(actualTop, imageHeight - 1));
                actualRight = Math.Max(actualLeft + 1, Math.Min(actualRight, imageWidth));
                actualBottom = Math.Max(actualTop + 1, Math.Min(actualBottom, imageHeight));
            }

            var cropWidth = actualRight - actualLeft;
            var cropHeight = actualBottom - actualTop;

            if (cropWidth <= 0 || cropHeight <= 0)
                return null;

            using var croppedBitmap = new SKBitmap(cropWidth, cropHeight);
            using (var canvas = new SKCanvas(croppedBitmap))
            {
                var srcRect = new SKRect(actualLeft, actualTop, actualRight, actualBottom);
                var destRect = new SKRect(0, 0, cropWidth, cropHeight);
                canvas.DrawBitmap(originalImage, srcRect, destRect);
            }

            // The cropped image is already in the correct orientation (TopLeft)
            // No need to rotate it again - save it as-is
            var cachePath = FileSystem.CacheDirectory;
            var croppedFileName = $"cropped_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";
            var croppedImagePath = Path.Combine(cachePath, croppedFileName);

            using var image = SKImage.FromBitmap(croppedBitmap);
            using var data = image.Encode(SKEncodedImageFormat.Jpeg, 90);
            using var stream = File.Create(croppedImagePath);
            data.SaveTo(stream);

            return croppedImagePath;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Crop error: {ex.Message}");
            return null;
        }
    }

}