using Microsoft.Maui.Graphics;
using Microsoft.Maui.Storage;
using SkiaSharp;

namespace MauiApp.Views;

public partial class ImageCropPage : ContentPage
{
    private string? _imagePath;
    private double _left = 50, _top = 50, _right = 300, _bottom = 400;

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
    }

    private void OnHandlePanUpdated(object sender, PanUpdatedEventArgs e)
    {
        if (e.StatusType != GestureStatus.Running || sender is not Frame frame)
                return;

        double dx = e.TotalX;
        double dy = e.TotalY;

        if (frame == TopLeftHandle)
        {
            _left = Math.Max(0, _left + dx);
            _top = Math.Max(0, _top + dy);
        }
        else if (frame == TopRightHandle)
        {
            _right = Math.Max(_left + 50, _right + dx);
            _top = Math.Max(0, _top + dy);
        }
        else if (frame == BottomLeftHandle)
        {
            _left = Math.Max(0, _left + dx);
            _bottom = Math.Max(_top + 50, _bottom + dy);
        }
        else if (frame == BottomRightHandle)
        {
            _right = Math.Max(_left + 50, _right + dx);
            _bottom = Math.Max(_top + 50, _bottom + dy);
        }

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
                // Fallback to full image if display dimensions unavailable
                actualLeft = 0;
                actualTop = 0;
                actualRight = imageWidth;
                actualBottom = imageHeight;
            }
            else
            {
                var scaleX = imageWidth / displayWidth;
                var scaleY = imageHeight / displayHeight;

                actualLeft = (int)(_left * scaleX);
                actualTop = (int)(_top * scaleY);
                actualRight = (int)(_right * scaleX);
                actualBottom = (int)(_bottom * scaleY);

                // Clamp to image bounds
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

            var cachePath = FileSystem.CacheDirectory;
            var croppedFileName = $"cropped_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";
            var croppedImagePath = Path.Combine(cachePath, croppedFileName);

        using var image = SKImage.FromBitmap(croppedBitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 90);
            using var stream = File.Create(croppedImagePath);
        data.SaveTo(stream);

            return croppedImagePath;
        }
        catch
        {
            return null;
        }
    }
}