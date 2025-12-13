using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SkiaSharp;
using MauiApp.ImageEditor.Models;

namespace MauiApp.ImageEditor.ViewModels;

public partial class CropToolViewModel : ObservableObject
{
    private readonly ImageEditorViewModel parent;

    [ObservableProperty]
    private ObservableCollection<AspectRatioItem> aspectRatios = new();

    [ObservableProperty]
    private AspectRatioItem? selectedRatio;

    partial void OnSelectedRatioChanged(AspectRatioItem? value)
    {
        // Apply aspect ratio to crop rect when ratio is selected
        if (IsCropMode && !CropRect.IsEmpty && CropRect.Width > 10 && CropRect.Height > 10)
        {
            ApplyAspectRatioToCropRect();
        }
    }

    [ObservableProperty]
    private CropMode selectedCropMode = CropMode.None;

    [ObservableProperty]
    private SKRect cropRect = SKRect.Empty;

    [ObservableProperty]
    private bool isCropMode = false;

    [ObservableProperty]
    private bool showGrid = true; // Flag to hide grid during rotate/flip

    [ObservableProperty]
    private bool showCropOverlay = true; // Flag to hide crop frame during rotate/flip

    public CropToolViewModel(ImageEditorViewModel parent)
    {
        this.parent = parent;
        SetupAspectRatios();
        InitializeCropRect();
    }

    private void SetupAspectRatios()
    {
        AspectRatios.Add(new AspectRatioItem { Name = "Free", Ratio = null });
        AspectRatios.Add(new AspectRatioItem { Name = "1:1", Ratio = 1.0f });
        AspectRatios.Add(new AspectRatioItem { Name = "4:3", Ratio = 4.0f / 3.0f });
        AspectRatios.Add(new AspectRatioItem { Name = "3:4", Ratio = 3.0f / 4.0f });
        AspectRatios.Add(new AspectRatioItem { Name = "16:9", Ratio = 16.0f / 9.0f });
        AspectRatios.Add(new AspectRatioItem { Name = "9:16", Ratio = 9.0f / 16.0f });
        
        SelectedRatio = AspectRatios[0];
    }

    private void InitializeCropRect()
    {
        if (parent?.WorkingBitmap != null)
        {
            CropRect = new SKRect(0, 0, parent.WorkingBitmap.Width, parent.WorkingBitmap.Height);
        }
    }

    public void StartCropMode()
    {
        IsCropMode = true;
        // Initialize crop rect to full image so handles are available
        if (parent?.WorkingBitmap != null)
        {
            float width = parent.WorkingBitmap.Width;
            float height = parent.WorkingBitmap.Height;
            // Account for rotation
            if (parent.RotationAngle % 180 != 0)
            {
                (width, height) = (height, width);
            }
            CropRect = new SKRect(0, 0, width, height);
        }
        else
        {
            CropRect = SKRect.Empty;
        }
        // Don't set a default crop mode - wait for user to select one
        SelectedCropMode = CropMode.None;
    }

    [RelayCommand]
    public void SelectCropMode(string mode)
    {
        if (Enum.TryParse<CropMode>(mode, out var cropMode))
        {
            SelectedCropMode = cropMode;
            ShowGrid = true; // Ensure grid is visible when mode is selected
            ShowCropOverlay = true; // Ensure crop overlay is visible when mode is selected
            AdjustCropRectForMode();
        }
    }

    private void AdjustCropRectForMode()
    {
        if (parent?.WorkingBitmap == null) return;

        var bitmap = parent.WorkingBitmap;
        float width = bitmap.Width;
        float height = bitmap.Height;

        // If rotated 90° or 270°, logical dimensions are swapped
        bool isRotated90or270 = (parent.RotationAngle % 180) != 0;
        if (isRotated90or270)
        {
            (width, height) = (height, width);
        }

        var centerX = width / 2f;
        var centerY = height / 2f;

        // Handle mode-specific resets FIRST (before preserving existing crop rect)
        // These modes should always reset to their default size regardless of current crop rect
        switch (SelectedCropMode)
        {
            case CropMode.Original:
                // Always reset to full image when Original is selected
                CropRect = new SKRect(0, 0, width, height);
                return;
            case CropMode.Square:
                // Always reset to square when Square is selected
                var size = Math.Min(width, height);
                CropRect = SKRect.Create(centerX - size / 2f, centerY - size / 2f, size, size);
                return;
            case CropMode.Circle:
                // Always reset to circle when Circle is selected
                var circleSize = Math.Min(width, height);
                CropRect = SKRect.Create(centerX - circleSize / 2f, centerY - circleSize / 2f, circleSize, circleSize);
                return;
            case CropMode.Eclipse:
                // Eclipse (ellipse) - similar to circle but uses full image bounds
                var ellipseWidth = width;
                var ellipseHeight = height;
                CropRect = SKRect.Create(0, 0, ellipseWidth, ellipseHeight);
                return;
            case CropMode.Ratio3_1:
                CreateAspectRatioCropRect(3.0f / 1.0f, width, height, centerX, centerY);
                return;
            case CropMode.Ratio3_2:
                CreateAspectRatioCropRect(3.0f / 2.0f, width, height, centerX, centerY);
                return;
            case CropMode.Ratio4_3:
                CreateAspectRatioCropRect(4.0f / 3.0f, width, height, centerX, centerY);
                return;
            case CropMode.Ratio5_4:
                CreateAspectRatioCropRect(5.0f / 4.0f, width, height, centerX, centerY);
                return;
            case CropMode.Ratio7_5:
                CreateAspectRatioCropRect(7.0f / 5.0f, width, height, centerX, centerY);
                return;
            case CropMode.Ratio16_9:
                CreateAspectRatioCropRect(16.0f / 9.0f, width, height, centerX, centerY);
                return;
        }

        // If we already have a meaningful crop rect, preserve and transform it
        // (This applies to Custom and None modes)
        if (!CropRect.IsEmpty && CropRect.Width > 10 && CropRect.Height > 10)
        {
            // Transform existing crop rect to new orientation
            CropRect = TransformCropRectForCurrentRotation(CropRect, bitmap.Width, bitmap.Height);
            return;
        }

        // Otherwise, create new one based on mode (first time initialization)
        switch (SelectedCropMode)
        {
            case CropMode.None:
                // Keep existing crop rect if valid, otherwise initialize to full image
                if (CropRect.IsEmpty)
                {
                    CropRect = new SKRect(0, 0, width, height);
                }
                break;
            case CropMode.Custom:
            default:
                CropRect = new SKRect(0, 0, width, height);
                break;
        }
    }

    private SKRect TransformCropRectForCurrentRotation(SKRect currentRect, float originalWidth, float originalHeight)
    {
        float angle = parent.RotationAngle % 360;
        if (angle == 0) return currentRect;
        
        if (angle == 180)
        {
            // 180°: invert around center
            var centerX = originalWidth / 2f;
            var centerY = originalHeight / 2f;
            return new SKRect(
                2 * centerX - currentRect.Right,
                2 * centerY - currentRect.Bottom,
                2 * centerX - currentRect.Left,
                2 * centerY - currentRect.Top
            );
        }
        
        if (angle == 90 || angle == 270)
        {
            // 90° and 270°: swap dimensions and rotate around center
            var centerX = originalWidth / 2f;
            var centerY = originalHeight / 2f;

            // Map the four corners
            SKPoint[] corners = {
                new SKPoint(currentRect.Left, currentRect.Top),
                new SKPoint(currentRect.Right, currentRect.Top),
                new SKPoint(currentRect.Right, currentRect.Bottom),
                new SKPoint(currentRect.Left, currentRect.Bottom)
            };

            // Rotate each corner 90° or -90° around center
            float targetAngle = angle == 90 ? 90 : -90;
            float rad = targetAngle * (float)Math.PI / 180f;
            float cos = (float)Math.Cos(rad);
            float sin = (float)Math.Sin(rad);

            var rotatedCorners = corners.Select(p =>
            {
                float x = p.X - centerX;
                float y = p.Y - centerY;
                float nx = x * cos - y * sin + originalHeight / 2f;  // new center is (h/2, w/2)
                float ny = x * sin + y * cos + originalWidth / 2f;
                return new SKPoint(nx, ny);
            }).ToList();

            float minX = rotatedCorners.Min(p => p.X);
            float minY = rotatedCorners.Min(p => p.Y);
            float maxX = rotatedCorners.Max(p => p.X);
            float maxY = rotatedCorners.Max(p => p.Y);

            return new SKRect(minX, minY, maxX, maxY);
        }

        return currentRect; // 0, 180, etc.
    }

    [RelayCommand]
    public void ApplyCrop()
    {
        if (parent?.WorkingBitmap == null || CropRect.IsEmpty) return;

        parent.ApplyCropCommand.Execute(CropRect);
        IsCropMode = false;
        // Reset tool selection so toolbar reappears
        if (parent != null)
        {
            parent.SelectedToolType = ToolType.None;
            parent.CloseToolPanelCommand.Execute(null);
        }
    }

    [RelayCommand]
    public void CancelCrop()
    {
        IsCropMode = false;
        parent?.CloseToolPanelCommand.Execute(null);
        // Reset tool selection so toolbar reappears
        if (parent != null)
        {
            parent.SelectedToolType = ToolType.None;
        }
    }

    [RelayCommand]
    public void FlipHorizontal()
    {
        if (parent == null) return;
        
        // Hide grid and crop frame immediately when flip is triggered
        ShowGrid = false;
        ShowCropOverlay = false;
        
        // Instant preview: just toggle flip state, no bitmap recreation
        parent.FlipHorizontal = !parent.FlipHorizontal;
        
        // Transform existing crop rect for flip
        if (!CropRect.IsEmpty && CropRect.Width > 10 && CropRect.Height > 10 && parent.WorkingBitmap != null)
        {
            var width = parent.WorkingBitmap.Width;
            CropRect = new SKRect(
                width - CropRect.Right,
                CropRect.Top,
                width - CropRect.Left,
                CropRect.Bottom
            );
        }
        else
        {
            AdjustCropRectForMode();
        }
    }

    [RelayCommand]
    public void FlipVertical()
    {
        if (parent == null) return;
        
        // Hide grid and crop frame immediately when flip is triggered
        ShowGrid = false;
        ShowCropOverlay = false;
        
        // Instant preview: just toggle flip state, no bitmap recreation
        parent.FlipVertical = !parent.FlipVertical;
        
        // Transform existing crop rect for flip
        if (!CropRect.IsEmpty && CropRect.Width > 10 && CropRect.Height > 10 && parent.WorkingBitmap != null)
        {
            var height = parent.WorkingBitmap.Height;
            CropRect = new SKRect(
                CropRect.Left,
                height - CropRect.Bottom,
                CropRect.Right,
                height - CropRect.Top
            );
        }
        else
        {
            AdjustCropRectForMode();
        }
    }

    [RelayCommand]
    public void Rotate90()
    {
        if (parent == null) return;
        
        // Hide grid and crop frame immediately when rotate is triggered
        ShowGrid = false;
        ShowCropOverlay = false;
        
        // Instant preview: just update rotation angle, no bitmap recreation
        parent.RotationAngle = (parent.RotationAngle + 90) % 360;
        
        // This will now correctly preserve + transform the current crop rect
        AdjustCropRectForMode();
        
        // Optional: keep crop centered after rotation
        CenterCropRectIfNeeded();
    }

    // Optional helper to prevent crop from drifting off-screen
    private void CenterCropRectIfNeeded()
    {
        if (parent?.WorkingBitmap == null || CropRect.IsEmpty) return;

        float w = parent.WorkingBitmap.Width;
        float h = parent.WorkingBitmap.Height;
        if (parent.RotationAngle % 180 != 0) (w, h) = (h, w);

        if (CropRect.Right > w || CropRect.Bottom > h || CropRect.Left < 0 || CropRect.Top < 0)
        {
            var centerX = w / 2f;
            var centerY = h / 2f;
            var halfW = CropRect.Width / 2f;
            var halfH = CropRect.Height / 2f;
            CropRect = new SKRect(centerX - halfW, centerY - halfH, centerX + halfW, centerY + halfH);
        }
    }

    private void ApplyAspectRatioToCropRect()
    {
        if (parent?.WorkingBitmap == null || CropRect.IsEmpty) return;
        
        // If no aspect ratio is selected (Free), don't modify
        if (SelectedRatio == null || !SelectedRatio.Ratio.HasValue) return;
        
        float targetRatio = SelectedRatio.Ratio.Value;
        float currentRatio = CropRect.Width / CropRect.Height;
        
        // If already at target ratio (within tolerance), don't change
        if (Math.Abs(currentRatio - targetRatio) < 0.01f) return;
        
        // Preserve center and adjust size to match aspect ratio
        float centerX = CropRect.MidX;
        float centerY = CropRect.MidY;
        
        float newWidth, newHeight;
        
        // Determine which dimension to keep (use the smaller one to ensure it fits)
        if (currentRatio > targetRatio)
        {
            // Current is wider - adjust width to match height
            newHeight = CropRect.Height;
            newWidth = newHeight * targetRatio;
        }
        else
        {
            // Current is taller - adjust height to match width
            newWidth = CropRect.Width;
            newHeight = newWidth / targetRatio;
        }
        
        // Ensure it fits within image bounds
        float maxWidth = parent.WorkingBitmap.Width;
        float maxHeight = parent.WorkingBitmap.Height;
        if (parent.RotationAngle % 180 != 0)
        {
            (maxWidth, maxHeight) = (maxHeight, maxWidth);
        }
        
        if (newWidth > maxWidth)
        {
            newWidth = maxWidth;
            newHeight = newWidth / targetRatio;
        }
        if (newHeight > maxHeight)
        {
            newHeight = maxHeight;
            newWidth = newHeight * targetRatio;
        }
        
        // Update crop rect centered on previous center
        var newRect = new SKRect(
            centerX - newWidth / 2f,
            centerY - newHeight / 2f,
            centerX + newWidth / 2f,
            centerY + newHeight / 2f
        );
        
        // Clamp to image bounds
        newRect.Left = Math.Max(0, newRect.Left);
        newRect.Top = Math.Max(0, newRect.Top);
        newRect.Right = Math.Min(maxWidth, newRect.Right);
        newRect.Bottom = Math.Min(maxHeight, newRect.Bottom);
        
        CropRect = newRect;
    }

    /// <summary>
    /// Creates a crop rect with the specified aspect ratio, centered and fitting within image bounds
    /// </summary>
    private void CreateAspectRatioCropRect(float aspectRatio, float imageWidth, float imageHeight, float centerX, float centerY)
    {
        float cropWidth, cropHeight;
        
        // Calculate dimensions that fit within image bounds while maintaining aspect ratio
        if (imageWidth / imageHeight > aspectRatio)
        {
            // Image is wider than target ratio - fit to height
            cropHeight = imageHeight;
            cropWidth = cropHeight * aspectRatio;
        }
        else
        {
            // Image is taller than target ratio - fit to width
            cropWidth = imageWidth;
            cropHeight = cropWidth / aspectRatio;
        }
        
        // Center the crop rect and clamp to image bounds
        float left = Math.Max(0, centerX - cropWidth / 2f);
        float top = Math.Max(0, centerY - cropHeight / 2f);
        float right = Math.Min(imageWidth, centerX + cropWidth / 2f);
        float bottom = Math.Min(imageHeight, centerY + cropHeight / 2f);
        
        // Create the final crop rect with clamped bounds
        CropRect = new SKRect(left, top, right, bottom);
    }

    // FlipBitmap and RotateBitmap methods removed - transformations are now applied
    // via canvas matrix in OnPaintSurface for instant preview.
    // The actual bitmap transformation is handled by ImageEditorViewModel.ApplyTransform()
    // when user wants to "bake" the transforms into the image.
}







