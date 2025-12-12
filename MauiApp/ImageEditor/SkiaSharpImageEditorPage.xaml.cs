using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;
using Microsoft.Maui.Storage;
using Microsoft.Maui.Media;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices;
using MauiApp.ImageEditor.ViewModels;
using MauiApp.ImageEditor.Models;
using MauiApp.ImageEditor.Views.ToolPanels;
using System.Windows.Input;
#if ANDROID
using Android.Util;
using Android.Views;
using AndroidX.AppCompat.App;
using Android.OS;
#endif
namespace MauiApp.ImageEditor
{
    public partial class SkiaSharpImageEditorPage : ContentPage
    {
        private ImageEditorViewModel? viewModel;

        // Drawing state
        private bool isDrawing = false;
        private SKPoint lastPoint;
        private SKPoint? previousPoint; // Track previous point for smooth cubic curves
        private SKPath? currentPath;
        // Temporary strokes being drawn (not yet saved to bitmap)
        private List<Stroke> temporaryStrokes = new();
        // Saved strokes that have been permanently drawn on the bitmap
        private List<Stroke> savedStrokes = new();

        // Text overlays - temporary overlay being edited (only one at a time)
        private TextOverlay? currentEditingTextOverlay = null;
        // Finalized captions that have been saved
        private List<Caption> savedCaptions = new();
        // Legacy support - keep textOverlays for rendering finalized captions
        private List<TextOverlay> textOverlays = new();
        private TextOverlay? selectedTextOverlay = null;
        // Track which saved caption is being edited (null = new caption)
        private Guid? editingCaptionId = null;
        
        // Flag to track if +Add button should be enabled
        private bool canAddNewText = true;

        // Shape overlays - temporary shape being edited (only one at a time)
        private ShapeLayer? currentEditingShape = null;
        // Finalized shapes that have been saved
        private List<ShapeLayer> savedShapes = new();
        // Track which saved shape is being edited (null = new shape)
        private Guid? editingShapeId = null;
        
        // Shape dragging/resizing state
        private bool isDraggingShape = false;
        private bool shapeDragStarted = false; // Track if drag has actually started (after movement threshold)
        private SKPoint initialShapePosition; // Initial shape position when dragging started
        private SKPoint initialShapeTouch; // Initial touch point when dragging shape started
        private SKRect initialShapeBounds; // Initial shape bounds when resizing started
        private float initialRotation = 0f; // Initial rotation when rotation started
        private int? draggingShapeHandle = null; // 0=top-left, 1=top-right, 2=bottom-left, 3=bottom-right, 4=move, 5=rotation
        private const float ShapeDragThreshold = 10f; // Minimum movement to start dragging (in image pixels)
        private const float HandleSize = 20f; // Size of resize handles in image pixels

        // Crop state
        private bool isCropMode = false;
        private SKRect? cropRect;
        private SKPoint? cropStart;
        private SKPoint? lastTouchPoint = null; // For incremental delta calculation
        private int? draggingHandle = null; // 0=top-left, 1=top-right, 2=bottom-left, 3=bottom-right, 4=move

        // Simplified crop dragging state
        private SKPoint initialTouch; // Initial touch point in image space
        private SKRect initialCropRect; // Initial crop rect when dragging started

        // Text dragging state
        private bool isDraggingText = false;
        private bool textDragStarted = false;
        private SKPoint initialTextPosition;
        private SKPoint initialTextTouch;
        private const float TextDragThreshold = 10f;
        
        // Text resizing state
        private int? draggingTextHandle = null; // 0=top-left, 1=top-right, 2=bottom-left, 3=bottom-right, 4=move
        private float initialTextSize = 0f; // Initial text size when resize started
        private SKRect initialTextBounds = SKRect.Empty; // Initial text bounds when resize started
        public Action<string>? OnImageSaved { get; set; }
        public event EventHandler? ImageCancelled;

        public SkiaSharpImageEditorPage(Action<string>? onImageSaved = null)
        {
            InitializeComponent();
            OnImageSaved = onImageSaved;
            
            // Set up navigation bar back command
            if (NavigationBar != null)
            {
                NavigationBar.BackCommand = new Command(async () => await OnBackClicked());
            }
            
            // Ensure CanvasView is enabled for touch events
            if (CanvasView != null)
            {
                CanvasView.IsEnabled = true;
                CanvasView.EnableTouchEvents = true;
                CanvasView.InputTransparent = false;
                // Log to verify initialization
            }
            
            // Attach platform-specific touch handlers for Android
            AttachPlatformTouchHandlers();
            
            // Initialize +Add button state
            canAddNewText = true;
            // UpdateAddButtonState will be called after page loads, but we can call it here too
            // However, visual tree might not be ready yet, so we'll call it in SetImageSource
            
            // Fix icon sources after page loads (for NuGet package compatibility)
            Loaded += OnPageLoaded;
        }

        private void OnPageLoaded(object? sender, EventArgs e)
        {
            // Fix icon sources to load from embedded resources if file-based loading fails
            // This ensures icons work when library is consumed from NuGet package
            FixIconSources();
            
            // Set status bar color to match the header theme
#if ANDROID
            if (DeviceInfo.Platform == DevicePlatform.Android)
            {
                var activity = Platform.CurrentActivity as AppCompatActivity;
                if (activity != null && Build.VERSION.SdkInt >= BuildVersionCodes.Lollipop)
                {
                    activity.Window.SetStatusBarColor(Android.Graphics.Color.ParseColor("#FF6B5A"));
                }
            }
#endif
        }

        private async Task OnBackClicked()
        {
            await HandleBackNavigation();
        }

        private async Task HandleBackNavigation()
        {
            // Cancel editing and navigate back
            ImageCancelled?.Invoke(this, EventArgs.Empty);
            await Navigation.PopAsync();
        }

        /// <summary>
        /// Fixes icon sources to load from embedded resources ONLY if file-based loading fails.
        /// This ensures icons work when the library is consumed from a NuGet package,
        /// while preserving quality when file-based sources work correctly.
        /// </summary>
        private async void FixIconSources()
        {
            try
            {
                // Wait a bit for the visual tree to be ready and icons to load
                await Task.Delay(500);
                
                // Find all Image controls by searching through the page's content
                var allImages = new List<Image>();
                var rootElement = Content as Element;
                if (rootElement != null)
                {
                    CollectImages(rootElement, allImages);
                }

                foreach (var image in allImages)
                {
                    if (image.Source is FileImageSource fileSource && !string.IsNullOrEmpty(fileSource.File))
                    {
                        var iconPath = fileSource.File; // e.g., "Resources/Images/ic_brush_edit.svg"
                        
                        // Extract just the filename from the full path
                        // Handle both "Resources/Images/ic_brush_edit.svg" and "ic_brush_edit.svg"
                        var iconName = iconPath;
                        if (iconPath.Contains("/"))
                        {
                            iconName = Path.GetFileName(iconPath); // Extract "ic_brush_edit.svg"
                        }
                        else if (iconPath.Contains("\\"))
                        {
                            iconName = Path.GetFileName(iconPath); // Handle Windows paths
                        }
                        
                        // Check if the file-based source is actually working
                        // Wait a bit more and check if image has loaded (has dimensions)
                        await Task.Delay(300);
                        
                        // If image still has no size after delay, it likely failed to load
                        // Always try embedded resource fallback for NuGet packages
                        bool needsFallback = image.Width <= 0 && image.Height <= 0 && 
                                            image.IsVisible && image.Opacity > 0;
                        
                        // For consuming apps, always try embedded resource as fallback
                        // This ensures icons work even if file-based loading fails
                        if (needsFallback)
                        {
                            try
                            {
                                var embeddedSource = IconHelper.GetIconFromResource(iconName);
                                if (embeddedSource != null)
                                {
                                    image.Source = embeddedSource;
                                }
                                else
                                {
                                }
                            }
                            catch (Exception ex)
                            {
                            }
                        }
                        else
                        {
                        }
                    }
                }
            }
            catch (Exception ex)
            {
            }
        }

        /// <summary>
        /// Recursively collects all Image controls from the visual tree
        /// </summary>
        private void CollectImages(Element element, List<Image> images)
        {
            if (element is Image image)
            {
                images.Add(image);
            }
            
            if (element is Layout layout)
            {
                foreach (var child in layout.Children.OfType<Element>())
                {
                    CollectImages(child, images);
                }
            }
            else if (element is Microsoft.Maui.Controls.View view && view is IContentView contentView)
            {
                var content = contentView.Content as Element;
                if (content != null)
                {
                    CollectImages(content, images);
                }
            }
        }
        public void SetImageSource(string imagePath)
        {
            if (!string.IsNullOrEmpty(imagePath) && File.Exists(imagePath))
            {
                // Ensure CanvasView is ready for touch
                if (CanvasView != null)
                {
                    CanvasView.IsEnabled = true;
                    CanvasView.EnableTouchEvents = true;
                    CanvasView.InputTransparent = false;
                    // Verify Touch event handler is attached
                    CanvasView.Touch += OnTouch;
                }
                
                viewModel = new ImageEditorViewModel(imagePath, CanvasView);
                BindingContext = viewModel;

                // Subscribe to image saved event
                viewModel.ImageSaved += (sender, path) =>
                {
                    OnImageSaved?.Invoke(path);
                };

                // Subscribe to tool selection
                viewModel.PropertyChanged += ViewModel_PropertyChanged;

                // Subscribe to crop VM property changes to invalidate canvas
                if (viewModel.CropVM != null)
                {
                    viewModel.CropVM.PropertyChanged += (s, e) =>
                    {
                        // Invalidate canvas when crop properties change
                        if (e.PropertyName == nameof(CropToolViewModel.CropRect) ||
                            e.PropertyName == nameof(CropToolViewModel.SelectedCropMode) ||
                            e.PropertyName == nameof(CropToolViewModel.ShowGrid) ||
                            e.PropertyName == nameof(CropToolViewModel.ShowCropOverlay) ||
                            e.PropertyName == nameof(CropToolViewModel.IsCropMode))
                        {
                            CanvasView.InvalidateSurface();
                        }
                    };
                }

                // Subscribe to text VM property changes to update selected text color and text
                if (viewModel.TextVM != null)
                {
                    viewModel.TextVM.PropertyChanged += (s, e) =>
                    {
                        // Only update if it's the current editing overlay (not saved captions)
                        if (selectedTextOverlay != currentEditingTextOverlay)
                        {
                            return; // Don't update saved captions
                        }
                        
                        if (e.PropertyName == nameof(TextToolViewModel.SelectedTextColor))
                        {
                            // Update selected text overlay color
                            if (currentEditingTextOverlay != null)
                            {
                                currentEditingTextOverlay.Color = ConvertColor(viewModel.TextVM.SelectedTextColor);
                                CanvasView.InvalidateSurface();
                            }
                        }
                        else if (e.PropertyName == nameof(TextToolViewModel.InputText))
                        {
                            // Update selected text overlay text when user types
                            if (currentEditingTextOverlay != null)
                            {
                                // Limit to 100 characters
                                string newText = viewModel.TextVM.InputText;
                                if (newText.Length > 100)
                                {
                                    newText = newText.Substring(0, 100);
                                    viewModel.TextVM.InputText = newText;
                                }
                                currentEditingTextOverlay.Text = newText;
                                CanvasView.InvalidateSurface();
                            }
                        }
                        else if (e.PropertyName == nameof(TextToolViewModel.IsBold))
                        {
                            // Update selected text overlay bold style
                            if (currentEditingTextOverlay != null)
                            {
                                currentEditingTextOverlay.IsBold = viewModel.TextVM.IsBold;
                                CanvasView.InvalidateSurface();
                            }
                        }
                        else if (e.PropertyName == nameof(TextToolViewModel.IsItalic))
                        {
                            // Update selected text overlay italic style
                            if (currentEditingTextOverlay != null)
                            {
                                currentEditingTextOverlay.IsItalic = viewModel.TextVM.IsItalic;
                                CanvasView.InvalidateSurface();
                            }
                        }
                        else if (e.PropertyName == nameof(TextToolViewModel.SelectedFillColor))
                        {
                            // Update selected text overlay fill color
                            if (currentEditingTextOverlay != null)
                            {
                                currentEditingTextOverlay.FillColor = viewModel.TextVM.SelectedFillColor != null 
                                    ? ConvertColor(viewModel.TextVM.SelectedFillColor) 
                                    : null;
                                CanvasView.InvalidateSurface();
                            }
                        }
                    };
                }

                // Setup tool panel visibility
                SetupToolPanels();
                
                // Setup callbacks for clearing overlays on reset
                viewModel.ClearOverlaysCallback = () =>
                {
                    // Clear all saved strokes
                    foreach (var stroke in savedStrokes)
                    {
                        stroke.Path?.Dispose();
                    }
                    savedStrokes.Clear();
                    
                    // Clear temporary strokes
                    foreach (var stroke in temporaryStrokes)
                    {
                        stroke.Path?.Dispose();
                    }
                    temporaryStrokes.Clear();
                    
                    // Clear current path
                    currentPath?.Dispose();
                    currentPath = null;
                    isDrawing = false;
                    
                    // Clear shapes
                    currentEditingShape = null;
                    savedShapes.Clear();
                    
                    // Clear text overlays and captions
                    currentEditingTextOverlay = null;
                    selectedTextOverlay = null;
                    editingCaptionId = null;
                    textOverlays.Clear();
                    savedCaptions.Clear();
                    canAddNewText = true;
                    
                    // Clear text mode state
                    if (viewModel.TextVM != null)
                    {
                        viewModel.TextVM.HasSelectedText = false;
                        viewModel.TextVM.IsTextMode = false;
                        viewModel.TextVM.ShowFontFamilyPanel = false;
                        viewModel.TextVM.ShowTextColorPanel = false;
                        viewModel.TextVM.ShowFillColorPanel = false;
                    }
                    
                    // Update Add button state
                    UpdateAddButtonState();
                    
                    // Invalidate canvas to reflect cleared overlays
                    CanvasView?.InvalidateSurface();
                };
                
                // Setup callback to check if there are overlays (for Reset button visibility)
                viewModel.HasOverlaysCallback = () =>
                {
                    // Check for draw strokes
                    bool hasDrawStrokes = savedStrokes.Count > 0 || temporaryStrokes.Count > 0 || currentPath != null;
                    
                    // Check for text overlays
                    bool hasTextOverlays = currentEditingTextOverlay != null || textOverlays.Count > 0;
                    
                    // Check for shapes
                    bool hasShapes = currentEditingShape != null || savedShapes.Count > 0;
                    
                    return hasDrawStrokes || hasTextOverlays || hasShapes;
                };
                
                // Setup callback to apply overlays (shapes, text) to bitmap when saving
                viewModel.ApplyOverlaysCallback = (bitmap) =>
                {
                    if (bitmap == null) return null;
                    
                    try
                    {
                        // Create a new bitmap with the same dimensions
                        var resultBitmap = new SKBitmap(bitmap.Width, bitmap.Height);
                        using (var surface = SKSurface.Create(new SKImageInfo(bitmap.Width, bitmap.Height)))
                        {
                            var canvas = surface.Canvas;
                            
                            // Draw the base bitmap first
                            canvas.DrawBitmap(bitmap, 0, 0);
                            
                            // Draw all saved shapes
                            foreach (var shape in savedShapes)
                            {
                                DrawShapeToBitmap(canvas, shape, bitmap);
                            }
                            
                            // Draw current editing shape (if any - should be saved by now, but include it just in case)
                            if (currentEditingShape != null)
                            {
                                DrawShapeToBitmap(canvas, currentEditingShape, bitmap);
                            }
                            
                            // Draw all saved text overlays (TextOverlay type)
                            foreach (var textOverlay in textOverlays)
                            {
                                DrawTextOverlayToBitmap(canvas, textOverlay, bitmap);
                            }
                            
                            // Draw all saved captions (Caption type)
                            foreach (var caption in savedCaptions)
                            {
                                DrawTextOverlayToBitmap(canvas, caption, bitmap);
                            }
                            
                            // Draw current editing text overlay (if any)
                            if (currentEditingTextOverlay != null)
                            {
                                DrawTextOverlayToBitmap(canvas, currentEditingTextOverlay, bitmap);
                            }
                            
                            // Draw all saved strokes on top
                            foreach (var stroke in savedStrokes)
                            {
                                if (stroke?.Path == null) continue;
                                
                                using (var paint = new SKPaint
                                {
                                    Color = stroke.Color,
                                    StrokeWidth = stroke.Thickness,
                                    Style = SKPaintStyle.Stroke,
                                    StrokeCap = SKStrokeCap.Round,
                                    StrokeJoin = SKStrokeJoin.Round,
                                    IsAntialias = true
                                })
                                {
                                    canvas.DrawPath(stroke.Path, paint);
                                }
                            }
                            
                            // Get the final bitmap
                            using (var image = surface.Snapshot())
                            {
                                resultBitmap = SKBitmap.FromImage(image);
                            }
                        }
                        
                        return resultBitmap;
                    }
                    catch (Exception ex)
                    {
                        return null;
                    }
                };
                
                // Setup callbacks for undo/redo actions
                viewModel.AddStrokeCallback = (stroke) =>
                {
                    if (stroke == null || stroke.Path == null) return;
                    
                    // Check if stroke already exists (shouldn't happen, but safety check)
                    if (!savedStrokes.Any(s => s.Id == stroke.Id))
                    {
                        savedStrokes.Add(stroke);
                        CanvasView?.InvalidateSurface();
                        viewModel.NotifyCanResetChanged();
                    }
                };
                
                viewModel.RemoveStrokeCallback = (stroke) =>
                {
                    if (stroke == null) return;
                    
                    // Find and remove stroke by ID
                    var strokeToRemove = savedStrokes.FirstOrDefault(s => s.Id == stroke.Id);
                    if (strokeToRemove != null)
                    {
                        savedStrokes.Remove(strokeToRemove);
                        // Don't dispose the path here - it might be needed for redo
                        // The path will be disposed when the redo stack is cleared or when a new action is registered
                        CanvasView?.InvalidateSurface();
                        viewModel.NotifyCanResetChanged();
                    }
                };
                
                viewModel.AddShapeCallback = (shape) =>
                {
                    if (shape == null) return;
                    
                    // Check if shape already exists (shouldn't happen, but safety check)
                    if (!savedShapes.Any(s => s.Id == shape.Id))
                    {
                        savedShapes.Add(shape);
                        CanvasView?.InvalidateSurface();
                        viewModel.NotifyCanResetChanged();
                    }
                };
                
                viewModel.RemoveShapeCallback = (shape) =>
                {
                    if (shape == null) return;
                    
                    // Find and remove shape by ID
                    var shapeToRemove = savedShapes.FirstOrDefault(s => s.Id == shape.Id);
                    if (shapeToRemove != null)
                    {
                        savedShapes.Remove(shapeToRemove);
                        CanvasView?.InvalidateSurface();
                        viewModel.NotifyCanResetChanged();
                    }
                };
                
                viewModel.AddCaptionCallback = (caption) =>
                {
                    if (caption == null) return;
                    
                    // Check if caption already exists (shouldn't happen, but safety check)
                    if (!savedCaptions.Any(c => c.Id == caption.Id))
                    {
                        savedCaptions.Add(caption);
                        
                        // Convert to TextOverlay for rendering
                        var savedOverlay = new TextOverlay
                        {
                            Text = caption.Text,
                            X = caption.X,
                            Y = caption.Y,
                            Color = caption.Color,
                            FillColor = caption.FillColor,
                            Size = caption.Size,
                            IsSelected = false,
                            IsBold = caption.IsBold,
                            IsItalic = caption.IsItalic
                        };
                        textOverlays.Add(savedOverlay);
                        
                        CanvasView?.InvalidateSurface();
                        viewModel.NotifyCanResetChanged();
                    }
                };
                
                viewModel.RemoveCaptionCallback = (caption) =>
                {
                    if (caption == null) return;
                    
                    // Find and remove caption by ID
                    var captionToRemove = savedCaptions.FirstOrDefault(c => c.Id == caption.Id);
                    if (captionToRemove != null)
                    {
                        savedCaptions.Remove(captionToRemove);
                        
                        // Remove corresponding TextOverlay by matching properties
                        var overlayToRemove = textOverlays.FirstOrDefault(o => 
                            Math.Abs(o.X - captionToRemove.X) < 0.1f && 
                            Math.Abs(o.Y - captionToRemove.Y) < 0.1f &&
                            o.Text == captionToRemove.Text);
                        if (overlayToRemove != null)
                        {
                            textOverlays.Remove(overlayToRemove);
                        }
                        
                        CanvasView?.InvalidateSurface();
                        viewModel.NotifyCanResetChanged();
                    }
                };
                
                // Subscribe to shape selection events
                if (viewModel.ShapesVM != null)
                {
                    viewModel.ShapesVM.OnShapeSelectedForCreation += OnShapeSelectedForCreation;
                    viewModel.ShapesVM.OnShapePropertyChanged += OnShapePropertyChanged;
                }
                
                // Initialize +Add button state
                UpdateAddButtonState();
            }
        }
        private void SetupToolPanels()
        {
            if (viewModel == null) return;

            // Crop panel is now directly in XAML, no need to set BindingContext here
            DrawPanel.BindingContext = viewModel.DrawVM;
            TextPanel.BindingContext = viewModel.TextVM;
            ShapesPanel.BindingContext = viewModel.ShapesVM;
        }
        private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ImageEditorViewModel.SelectedToolType))
            {
                UpdateToolPanelVisibility();
            }
        }
        private void UpdateToolPanelVisibility()
        {
            if (viewModel == null) return;

            // Crop panel visibility is now handled directly in XAML via CropVM.IsCropMode binding
            DrawPanel.IsVisible = viewModel.SelectedToolType == ToolType.Draw;
            TextPanel.IsVisible = viewModel.SelectedToolType == ToolType.Text;
            // Hide shapes panel if a shape is being edited (only one shape at a time)
            ShapesPanel.IsVisible = viewModel.SelectedToolType == ToolType.Shapes && currentEditingShape == null;
        }
        private void OnToolTapped(object? sender, TappedEventArgs e)
        {
            if (sender is Image image && viewModel != null)
            {
                var tapGesture = image.GestureRecognizers.OfType<TapGestureRecognizer>().FirstOrDefault();
                if (tapGesture != null)
                {
                    var toolName = tapGesture.CommandParameter?.ToString();
                    if (!string.IsNullOrEmpty(toolName))
                    {
                        viewModel.SelectToolByNameCommand.Execute(toolName);
                        UpdateToolPanelVisibility();

                        // Start crop mode if crop tool selected
                        if (toolName == "Crop" && viewModel.CropVM != null)
                        {
                            viewModel.CropVM.StartCropMode();
                        }

                        // Start draw mode if draw tool selected
                        if (toolName == "Draw" && viewModel.DrawVM != null)
                        {
                            viewModel.DrawVM.StartDrawMode();
                        }

                        // Start shape mode if shapes tool selected
                        if (toolName == "Shapes" && viewModel.ShapesVM != null)
                        {
                            viewModel.ShapesVM.StartShapeMode();
                        }

                        // Start text mode if text tool selected
                        if (toolName == "Text" && viewModel.TextVM != null)
                        {
                            viewModel.TextVM.StartTextMode();
                        }
                    }
                }
            }
        }
        private void OnBackTapped(object? sender, EventArgs e)
        {
            if (viewModel != null)
            {
                // Cancel text mode if active
                if (viewModel.TextVM != null && viewModel.TextVM.IsTextMode)
                {
                    viewModel.TextVM.IsTextMode = false;
                    viewModel.TextVM.ShowFontFamilyPanel = false;
                    viewModel.TextVM.ShowTextColorPanel = false;
                    viewModel.TextVM.ShowFillColorPanel = false;
                    viewModel.SelectedToolType = ToolType.None;
                    viewModel.CloseToolPanelCommand.Execute(null);
                }
                // Cancel draw mode if active
                else if (viewModel.DrawVM != null && viewModel.DrawVM.IsDrawMode)
                {
                    // Clear all temporary strokes before exiting (they weren't saved, so discard them)
                    foreach (var stroke in temporaryStrokes)
                    {
                        stroke.Path?.Dispose();
                    }
                    temporaryStrokes.Clear();
                    
                    // Clear current path and reset drawing state
                    currentPath?.Dispose();
                    currentPath = null;
                    isDrawing = false;
                    
                    viewModel.DrawVM.IsDrawMode = false;
                    viewModel.DrawVM.ShowFillColorPanel = false;
                    viewModel.DrawVM.ShowStrokePanel = false;
                    viewModel.SelectedToolType = ToolType.None;
                    viewModel.CloseToolPanelCommand.Execute(null);
                    
                    // Invalidate canvas to clear the temporary strokes from display
                    CanvasView.InvalidateSurface();
                    
                    // Notify that Reset button state may have changed
                    viewModel.NotifyCanResetChanged();
                }
                // Cancel shape mode if active
                else if (viewModel.ShapesVM != null && viewModel.ShapesVM.IsShapeMode)
                {
                    viewModel.ShapesVM.IsShapeMode = false;
                    viewModel.ShapesVM.SelectedShapeType = Models.ShapeType.None;
                    viewModel.SelectedToolType = ToolType.None;
                    viewModel.CloseToolPanelCommand.Execute(null);
                }
                else
                {
                    viewModel.CloseSubPanel();
                }
            }
        }
        private void OnShapeSelectedForCreation(ShapeType shapeType)
        {
            if (viewModel?.WorkingBitmap == null || viewModel.ShapesVM == null)
                return;

            // If there's a shape being edited, clear it completely (don't save it)
            // This allows selecting a new shape type to replace the current one
            if (currentEditingShape != null)
            {
                // If it's an existing shape being edited, remove it from saved shapes
                if (editingShapeId.HasValue)
                {
                    var existingShape = savedShapes.FirstOrDefault(s => s.Id == editingShapeId.Value);
                    if (existingShape != null)
                    {
                        savedShapes.Remove(existingShape);
                    }
                }
                
                // Clear editing state completely
                currentEditingShape = null;
                editingShapeId = null;
                draggingShapeHandle = null;
                isDraggingShape = false;
                shapeDragStarted = false;
            }

            // Clear any editing state (we're creating a new shape, not editing)
            editingShapeId = null;

            var bitmap = viewModel.WorkingBitmap;
            
            // Create default shape at center of image with default size
            float defaultSize = Math.Min(bitmap.Width, bitmap.Height) * 0.3f; // 30% of smaller dimension
            float centerX = bitmap.Width / 2f;
            float centerY = bitmap.Height / 2f;
            
            var newShape = new Models.ShapeLayer
            {
                Bounds = new SKRect(
                    centerX - defaultSize / 2f,
                    centerY - defaultSize / 2f,
                    centerX + defaultSize / 2f,
                    centerY + defaultSize / 2f
                ),
                Type = shapeType,
                IsSelected = true,
                StrokeWidth = viewModel.ShapesVM.StrokeWidth, // Use current stroke width from view model
                StrokeColor = ConvertColor(viewModel.ShapesVM.SelectedStrokeColor),
                FillColor = null // No fill by default
            };

            currentEditingShape = newShape;
            
            // Hide shapes panel when shape is being edited
            UpdateToolPanelVisibility();
            
            // Notify that Reset button state may have changed
            viewModel.NotifyCanResetChanged();
            
            
            CanvasView.InvalidateSurface();
        }

        private void OnShapePropertyChanged()
        {
            // Update current editing shape when properties change
            if (currentEditingShape != null && viewModel?.ShapesVM != null)
            {
                currentEditingShape.StrokeColor = ConvertColor(viewModel.ShapesVM.SelectedStrokeColor);
                currentEditingShape.StrokeWidth = viewModel.ShapesVM.StrokeWidth;
                // Always keep fill color as null (no fill)
                currentEditingShape.FillColor = null;
                
                CanvasView.InvalidateSurface();
            }
        }

        private void OnShapeBackTapped(object? sender, EventArgs e)
        {
            if (viewModel != null && viewModel.ShapesVM != null)
            {
                // If there's a current editing shape, discard it and show shapes panel
                if (currentEditingShape != null)
                {
                    currentEditingShape = null;
                    draggingShapeHandle = null;
                    isDraggingShape = false;
                    shapeDragStarted = false;
                    
                    // Show shapes panel again
                    UpdateToolPanelVisibility();
                }
                
                // Close any open panels first
                viewModel.ShapesVM.ShowFillColorPanel = false;
                viewModel.ShapesVM.ShowStrokePanel = false;
                viewModel.ShapesVM.IsShapeMode = false;
                viewModel.ShapesVM.SelectedShapeType = Models.ShapeType.None;
                viewModel.SelectedToolType = ToolType.None;
                viewModel.CloseToolPanelCommand.Execute(null);
                CanvasView.InvalidateSurface();
            }
        }
        private void OnShapeDoneTapped(object? sender, EventArgs e)
        {
            if (viewModel != null && viewModel.ShapesVM != null)
            {
                // Save current editing shape to saved shapes array
                if (currentEditingShape != null)
                {
                    // Check if we're editing an existing shape or creating a new one
                    if (editingShapeId.HasValue)
                    {
                        // Updating existing shape - preserve the ID
                        currentEditingShape.Id = editingShapeId.Value;
                    }
                    else
                    {
                    }
                    
                    // Deselect the shape before saving
                    currentEditingShape.IsSelected = false;
                    
                    // Add to saved shapes
                    savedShapes.Add(currentEditingShape);
                    
                    // Register action for undo/redo (only for new shapes, not edits)
                    if (!editingShapeId.HasValue)
                    {
                        // Use the actual shape object that's now in the collection
                        viewModel.RegisterAction(new AddShapeAction(currentEditingShape));
                    }
                    
                    // Clear current editing shape and editing state
                    currentEditingShape = null;
                    editingShapeId = null; // Clear editing ID
                    draggingShapeHandle = null;
                    isDraggingShape = false;
                    shapeDragStarted = false;
                    
                    // Show shapes panel again
                    UpdateToolPanelVisibility();
                    
                    // Notify that Reset button state may have changed (shape finalized)
                    viewModel.NotifyCanResetChanged();
                }
                
                // Close any open panels first
                viewModel.ShapesVM.ShowFillColorPanel = false;
                viewModel.ShapesVM.ShowStrokePanel = false;
                viewModel.ShapesVM.IsShapeMode = false;
                viewModel.ShapesVM.SelectedShapeType = Models.ShapeType.None;
                viewModel.SelectedToolType = ToolType.None;
                CanvasView.InvalidateSurface();
            }
        }
        private void OnShapeFillColorTapped(object? sender, EventArgs e)
        {
            if (viewModel?.ShapesVM != null)
            {
                // Toggle fill color panel
                if (viewModel.ShapesVM.ShowFillColorPanel)
                {
                    viewModel.ShapesVM.CloseFillColorPanelCommand.Execute(null);
                }
                else
                {
                    viewModel.ShapesVM.ShowFillColorOptionsCommand.Execute(null);
                }
            }
        }
        private void OnShapeStrokeTapped(object? sender, EventArgs e)
        {
            if (viewModel?.ShapesVM != null)
            {
                // Toggle stroke panel
                if (viewModel.ShapesVM.ShowStrokePanel)
                {
                    viewModel.ShapesVM.CloseStrokePanelCommand.Execute(null);
                }
                else
                {
                    viewModel.ShapesVM.ShowStrokeOptionsCommand.Execute(null);
                }
            }
        }
        private void OnShapeDeleteTapped(object? sender, EventArgs e)
        {
            if (viewModel?.ShapesVM != null)
            {
                // If editing an existing shape, delete it from saved shapes
                if (currentEditingShape != null && editingShapeId.HasValue)
                {
                    // Find the shape in saved shapes
                    var shapeToDelete = savedShapes.FirstOrDefault(s => s.Id == editingShapeId.Value);
                    if (shapeToDelete != null)
                    {
                        // Create a copy for the delete action (with cloned properties)
                        var shapeCopy = new Models.ShapeLayer
                        {
                            Id = shapeToDelete.Id,
                            Bounds = shapeToDelete.Bounds,
                            Type = shapeToDelete.Type,
                            IsSelected = false,
                            StrokeWidth = shapeToDelete.StrokeWidth,
                            StrokeColor = shapeToDelete.StrokeColor,
                            FillColor = shapeToDelete.FillColor,
                            Rotation = shapeToDelete.Rotation
                        };
                        
                        // Remove from saved shapes
                        savedShapes.Remove(shapeToDelete);
                        
                        // Register delete action for undo/redo
                        viewModel.RegisterAction(new DeleteShapeAction(shapeCopy));
                        
                        // Clear editing state
                        currentEditingShape = null;
                        editingShapeId = null;
                        draggingShapeHandle = null;
                        isDraggingShape = false;
                        shapeDragStarted = false;
                        
                        // Close shape mode
                        viewModel.ShapesVM.IsShapeMode = false;
                        viewModel.ShapesVM.SelectedShapeType = Models.ShapeType.None;
                        viewModel.SelectedToolType = ToolType.None;
                        viewModel.CloseToolPanelCommand.Execute(null);
                        
                        CanvasView.InvalidateSurface();
                        viewModel.NotifyCanResetChanged();
                        return;
                    }
                }
                
                // If creating a new shape, just clear it
                if (currentEditingShape != null && !editingShapeId.HasValue)
                {
                    currentEditingShape = null;
                    editingShapeId = null;
                    draggingShapeHandle = null;
                    isDraggingShape = false;
                    shapeDragStarted = false;
                    UpdateToolPanelVisibility();
                    CanvasView.InvalidateSurface();
                    return;
                }
                
                // Fallback: just clear selection
                viewModel.ShapesVM.SelectedShapeType = Models.ShapeType.None;
                CanvasView.InvalidateSurface();
            }
        }
        private void OnDrawBackTapped(object? sender, EventArgs e)
        {
            if (viewModel != null && viewModel.DrawVM != null)
            {
                // Clear all temporary strokes before exiting (they weren't saved, so discard them)
                foreach (var stroke in temporaryStrokes)
                {
                    stroke.Path?.Dispose();
                }
                temporaryStrokes.Clear();
                
                // Clear current path and reset drawing state
                currentPath?.Dispose();
                currentPath = null;
                isDrawing = false;
                
                // Close any open panels first
                viewModel.DrawVM.ShowFillColorPanel = false;
                viewModel.DrawVM.ShowStrokePanel = false;
                viewModel.DrawVM.IsDrawMode = false;
                viewModel.SelectedToolType = ToolType.None;
                viewModel.CloseToolPanelCommand.Execute(null);
                
                // Invalidate canvas to clear the temporary strokes from display
                CanvasView.InvalidateSurface();
                
                // Notify that Reset button state may have changed
                viewModel.NotifyCanResetChanged();
            }
        }
        private void OnDrawFillColorTapped(object? sender, EventArgs e)
        {
            if (viewModel?.DrawVM != null)
            {
                // Toggle fill color panel
                if (viewModel.DrawVM.ShowFillColorPanel)
                {
                    viewModel.DrawVM.CloseFillColorPanelCommand.Execute(null);
                }
                else
                {
                    viewModel.DrawVM.ShowFillColorOptionsCommand.Execute(null);
                }
            }
        }
        private void OnDrawStrokeTapped(object? sender, EventArgs e)
        {
            if (viewModel?.DrawVM != null)
            {
                // Toggle stroke panel
                if (viewModel.DrawVM.ShowStrokePanel)
                {
                    viewModel.DrawVM.CloseStrokePanelCommand.Execute(null);
                }
                else
                {
                    viewModel.DrawVM.ShowStrokeOptionsCommand.Execute(null);
                }
            }
        }
        private void OnDrawDeleteTapped(object? sender, EventArgs e)
        {
            // Delete only temporary strokes (not saved ones)
            if (viewModel?.DrawVM != null)
            {
                // Dispose paths before clearing
                foreach (var stroke in temporaryStrokes)
                {
                    stroke.Path?.Dispose();
                }
                temporaryStrokes.Clear();
                currentPath?.Dispose();
                currentPath = null;
                isDrawing = false;
                CanvasView.InvalidateSurface();
            }
        }
        private void OnDrawDoneTapped(object? sender, EventArgs e)
        {
            if (viewModel != null && viewModel.DrawVM != null && viewModel.WorkingBitmap != null)
            {
                // Save all temporary strokes to saved strokes (don't bake into bitmap - render on top)
                if (temporaryStrokes.Count > 0)
                {
                    // Move temporary strokes to saved strokes and register each as an action for undo/redo
                    // Register in order (first to last) so undo removes last first
                    // Clone the path for the action to avoid disposal issues
                    foreach (var stroke in temporaryStrokes)
                    {
                        savedStrokes.Add(stroke);
                        // Create a stroke with cloned path for the action (so it survives disposal)
                        var strokeForAction = new Stroke(new SKPath(stroke.Path), stroke.Color, stroke.Thickness)
                        {
                            Id = stroke.Id
                        };
                        viewModel.RegisterAction(new AddStrokeAction(strokeForAction));
                    }
                    temporaryStrokes.Clear();
                    
                    // Notify that Reset button state may have changed
                    viewModel.NotifyCanResetChanged();
                }
                
                // Close any open panels
                viewModel.DrawVM.ShowFillColorPanel = false;
                viewModel.DrawVM.ShowStrokePanel = false;
                viewModel.DrawVM.IsDrawMode = false;
                viewModel.SelectedToolType = ToolType.None;
                CanvasView.InvalidateSurface();
            }
        }
        private bool isDrawStrokeSliderDragging = false;
        private void OnDrawStrokeSliderValueChanged(object? sender, ValueChangedEventArgs e)
        {
            if (sender is Slider slider && DrawStrokeTooltip != null && DrawStrokeTooltipLabel != null)
            {
                // Update tooltip text
                DrawStrokeTooltipLabel.Text = e.NewValue.ToString("F2");

                // Only show tooltip if user is actively dragging
                if (isDrawStrokeSliderDragging)
                {
                    if (!DrawStrokeTooltip.IsVisible)
                    {
                        DrawStrokeTooltip.IsVisible = true;
                    }
                    // Calculate tooltip position based on slider value
                    UpdateDrawStrokeTooltipPosition(slider, e.NewValue);
                }
            }
        }
        private void OnDrawStrokeSliderDragStarted(object? sender, EventArgs e)
        {
            isDrawStrokeSliderDragging = true;
            if (DrawStrokeTooltip != null)
            {
                DrawStrokeTooltip.IsVisible = true;
            }
        }
        private void OnDrawStrokeSliderDragCompleted(object? sender, EventArgs e)
        {
            if (DrawStrokeTooltip != null)
            {
                // Hide tooltip after a short delay
                Task.Delay(300).ContinueWith(_ =>
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        if (DrawStrokeTooltip != null)
                        {
                            DrawStrokeTooltip.IsVisible = false;
                        }
                    });
                });
            }
        }
        private bool isShapeStrokeSliderDragging = false;
        private void OnShapeStrokeSliderValueChanged(object? sender, ValueChangedEventArgs e)
        {
            if (sender is Slider slider && ShapeStrokeTooltip != null && ShapeStrokeTooltipLabel != null)
            {
                // Update tooltip text
                ShapeStrokeTooltipLabel.Text = e.NewValue.ToString("F2");

                // Only show tooltip if user is actively dragging
                if (isShapeStrokeSliderDragging)
                {
                    if (!ShapeStrokeTooltip.IsVisible)
                    {
                        ShapeStrokeTooltip.IsVisible = true;
                    }
                    // Calculate tooltip position based on slider value
                    UpdateShapeStrokeTooltipPosition(slider, e.NewValue);
                }
            }
        }

        private void OnShapeStrokeSliderDragStarted(object? sender, EventArgs e)
        {
            isShapeStrokeSliderDragging = true;
            if (ShapeStrokeTooltip != null)
            {
                ShapeStrokeTooltip.IsVisible = true;
            }
        }

        private void OnShapeStrokeSliderDragCompleted(object? sender, EventArgs e)
        {
            isShapeStrokeSliderDragging = false;
            // Hide tooltip after a delay when dragging ends
            if (ShapeStrokeTooltip != null)
            {
                Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(300), () =>
                {
                    if (ShapeStrokeTooltip != null && !isShapeStrokeSliderDragging)
                    {
                        ShapeStrokeTooltip.IsVisible = false;
                    }
                });
            }
        }

        private void UpdateShapeStrokeTooltipPosition(Slider slider, double value)
        {
            if (ShapeStrokeTooltip == null || slider == null) return;
            try
            {
                // Use Dispatcher to ensure layout is complete
                Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(50), () =>
                {
                    if (ShapeStrokeTooltip == null || slider == null) return;
                    // Get slider bounds
                    var sliderBounds = slider.Bounds;
                    if (sliderBounds.Width <= 0) return;
                    // Calculate position based on slider value
                    // Normalize value to 0-1 range
                    double normalizedValue = (value - slider.Minimum) / (slider.Maximum - slider.Minimum);

                    // Calculate X position (accounting for thumb width)
                    // Thumb is typically 20-24 pixels wide on most platforms
                    double thumbWidth = 24;
                    double availableWidth = sliderBounds.Width - thumbWidth;
                    double tooltipX = normalizedValue * availableWidth;

                    // Center tooltip above thumb
                    double tooltipCenterX = tooltipX + (thumbWidth / 2);

                    // Measure tooltip to get actual width
                    double tooltipWidth = 60; // Default width
                    if (ShapeStrokeTooltip.Width > 0)
                    {
                        tooltipWidth = ShapeStrokeTooltip.Width;
                    }
                    else if (ShapeStrokeTooltipLabel != null)
                    {
                        // Estimate based on text length
                        tooltipWidth = Math.Max(50, ShapeStrokeTooltipLabel.Text?.Length * 8 ?? 50);
                    }

                    // Set tooltip position (center tooltip above thumb)
                    double translationX = tooltipCenterX - (tooltipWidth / 2);

                    // Clamp to slider bounds
                    translationX = Math.Max(0, Math.Min(translationX, sliderBounds.Width - tooltipWidth));

                    ShapeStrokeTooltip.TranslationX = translationX;
                });
            }
            catch
            {
                // Ignore errors during layout calculations
            }
        }

        private void UpdateDrawStrokeTooltipPosition(Slider slider, double value)
        {
            if (DrawStrokeTooltip == null || slider == null) return;
            try
            {
                // Use Dispatcher to ensure layout is complete
                Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(50), () =>
                {
                    if (DrawStrokeTooltip == null || slider == null) return;
                    // Get slider bounds
                    var sliderBounds = slider.Bounds;
                    if (sliderBounds.Width <= 0) return;
                    // Calculate position based on slider value
                    // Normalize value to 0-1 range
                    double normalizedValue = (value - slider.Minimum) / (slider.Maximum - slider.Minimum);

                    // Calculate X position (accounting for thumb width)
                    // Thumb is typically 20-24 pixels wide on most platforms
                    double thumbWidth = 24;
                    double availableWidth = sliderBounds.Width - thumbWidth;
                    double tooltipX = normalizedValue * availableWidth;

                    // Center tooltip above thumb
                    double tooltipCenterX = tooltipX + (thumbWidth / 2);

                    // Measure tooltip to get actual width
                    double tooltipWidth = 60; // Default width
                    if (DrawStrokeTooltip.Width > 0)
                    {
                        tooltipWidth = DrawStrokeTooltip.Width;
                    }
                    else if (DrawStrokeTooltipLabel != null)
                    {
                        // Estimate based on text length
                        tooltipWidth = Math.Max(50, DrawStrokeTooltipLabel.Text?.Length * 8 ?? 50);
                    }

                    // Set tooltip position (center tooltip above thumb)
                    double translationX = tooltipCenterX - (tooltipWidth / 2);

                    // Clamp to slider bounds
                    translationX = Math.Max(0, Math.Min(translationX, sliderBounds.Width - tooltipWidth));

                    DrawStrokeTooltip.TranslationX = translationX;
                });
            }
            catch
            {
                // Ignore errors during layout calculations
            }
        }
        private void OnTextBackTapped(object? sender, EventArgs e)
        {
            if (viewModel != null && viewModel.TextVM != null)
            {
                // If there's a current editing overlay, discard it and re-enable +Add
                if (currentEditingTextOverlay != null)
                {
                    currentEditingTextOverlay = null;
                    selectedTextOverlay = null;
                    viewModel.TextVM.HasSelectedText = false;
                    canAddNewText = true;
                    UpdateAddButtonState();
                }
                
                // Close any open panels first
                viewModel.TextVM.ShowFontFamilyPanel = false;
                viewModel.TextVM.ShowTextColorPanel = false;
                viewModel.TextVM.ShowFillColorPanel = false;
                viewModel.TextVM.IsTextMode = false;
                viewModel.SelectedToolType = ToolType.None;
                viewModel.CloseToolPanelCommand.Execute(null);
                CanvasView.InvalidateSurface();
            }
        }
        private void OnTextFontFamilyTapped(object? sender, EventArgs e)
        {
            if (viewModel?.TextVM != null)
            {
                // Always open font family panel and close others
                viewModel.TextVM.ShowFontFamilyOptionsCommand.Execute(null);
            }
        }
        private void OnTextColorTapped(object? sender, EventArgs e)
        {
            if (viewModel?.TextVM != null)
            {
                // Always open text color panel and close others
                viewModel.TextVM.ShowTextColorOptionsCommand.Execute(null);
            }
        }
        private void OnTextFillColorTapped(object? sender, EventArgs e)
        {
            if (viewModel?.TextVM != null)
            {
                // Always open fill color panel and close others
                viewModel.TextVM.ShowFillColorOptionsCommand.Execute(null);
            }
        }
        private void OnTextFontAttributesTapped(object? sender, EventArgs e)
        {
            if (viewModel?.TextVM != null)
            {
                // Always open font attributes panel and close others
                viewModel.TextVM.ShowFontAttributesOptionsCommand.Execute(null);
            }
        }
        private void OnTextAddTapped(object? sender, EventArgs e)
        {

            // Don't allow adding if already editing a text
            if (!canAddNewText || currentEditingTextOverlay != null)
            {
                return;
            }

            if (viewModel == null)
            {
                return;
            }

            // Ensure text tool is selected
            if (viewModel.SelectedToolType != ToolType.Text)
            {
                viewModel.SelectToolByNameCommand.Execute("Text");
            }

            if (viewModel.TextVM == null)
            {
                return;
            }

            if (viewModel.WorkingBitmap == null)
            {
                return;
            }

            // Ensure text mode is active
            if (!viewModel.TextVM.IsTextMode)
            {
                viewModel.TextVM.StartTextMode();
            }

            // Clear any editing state (we're creating a new text, not editing)
            editingCaptionId = null;

            // Add new text overlay at center of image
            var bitmap = viewModel.WorkingBitmap;

            // Use a fixed standard text size (60f) for "Enter Text" to maintain consistent size across all images
            // This size is in image coordinates and will be scaled appropriately during rendering
            float textSize = 60f;

            // Use the selected text color
            var textColor = viewModel.TextVM.SelectedTextColor;
            var newTextOverlay = new TextOverlay
            {
                Text = "Enter Text", // Start with placeholder text
                X = bitmap.Width / 2f,
                Y = bitmap.Height / 2f,
                Color = ConvertColor(textColor),
                Size = textSize,
                IsSelected = true,
                IsBold = viewModel.TextVM.IsBold,
                IsItalic = viewModel.TextVM.IsItalic,
                FillColor = viewModel.TextVM.SelectedFillColor != null ? ConvertColor(viewModel.TextVM.SelectedFillColor) : null
            };
            
            // Set InputText to "Enter Text" for new text overlay
            viewModel.TextVM.InputText = "Enter Text";

            // Set as current editing overlay (only one at a time)
            currentEditingTextOverlay = newTextOverlay;
            selectedTextOverlay = newTextOverlay;
            viewModel.TextVM.HasSelectedText = true;
            
            // Disable +Add button
            canAddNewText = false;
            UpdateAddButtonState();
            
            // Notify that Reset button state may have changed (text overlay added)
            viewModel.NotifyCanResetChanged();

            // Sync text entry with overlay text
            viewModel.TextVM.SyncTextFromOverlay(newTextOverlay.Text);

            // Ensure TextPanel is visible
            if (TextPanel != null && !TextPanel.IsVisible)
            {
                TextPanel.IsVisible = true;
            }

            // Force canvas redraw first
            CanvasView.InvalidateSurface();

            // Open keyboard immediately
            if (TextPanel != null)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    TextPanel.FocusEntry();
                });
            }

            // Debug: Verify text overlay was added

            // Force canvas redraw
            if (CanvasView != null)
            {
                CanvasView.InvalidateSurface();
            }
            else
            {
            }
        }
        private void OnTextDeleteTapped(object? sender, EventArgs e)
        {
            if (viewModel?.TextVM != null && currentEditingTextOverlay != null)
            {
                // If editing an existing caption, delete it from saved captions
                if (editingCaptionId.HasValue)
                {
                    // Find the caption in saved captions
                    var captionToDelete = savedCaptions.FirstOrDefault(c => c.Id == editingCaptionId.Value);
                    if (captionToDelete != null)
                    {
                        // Create a copy for the delete action
                        var captionCopy = new Caption
                        {
                            Id = captionToDelete.Id,
                            Text = captionToDelete.Text,
                            X = captionToDelete.X,
                            Y = captionToDelete.Y,
                            Color = captionToDelete.Color,
                            FillColor = captionToDelete.FillColor,
                            Size = captionToDelete.Size,
                            FontFamily = captionToDelete.FontFamily,
                            IsBold = captionToDelete.IsBold,
                            IsItalic = captionToDelete.IsItalic,
                            CreatedAt = captionToDelete.CreatedAt,
                            ModifiedAt = captionToDelete.ModifiedAt
                        };
                        
                        // Remove from saved captions
                        savedCaptions.Remove(captionToDelete);
                        
                        // Remove corresponding TextOverlay
                        var overlayToRemove = textOverlays.FirstOrDefault(o => 
                            Math.Abs(o.X - captionToDelete.X) < 0.1f && 
                            Math.Abs(o.Y - captionToDelete.Y) < 0.1f &&
                            o.Text == captionToDelete.Text);
                        if (overlayToRemove != null)
                        {
                            textOverlays.Remove(overlayToRemove);
                        }
                        
                        // Register delete action for undo/redo
                        viewModel.RegisterAction(new DeleteCaptionAction(captionCopy));
                        
                        // Clear editing state
                        currentEditingTextOverlay = null;
                        selectedTextOverlay = null;
                        editingCaptionId = null;
                        viewModel.TextVM.HasSelectedText = false;
                        
                        // Re-enable +Add button
                        canAddNewText = true;
                        UpdateAddButtonState();
                        
                        // Close text mode
                        viewModel.TextVM.IsTextMode = false;
                        viewModel.SelectedToolType = ToolType.None;
                        viewModel.CloseToolPanelCommand.Execute(null);
                        
                        CanvasView.InvalidateSurface();
                        viewModel.NotifyCanResetChanged();
                        return;
                    }
                }
                
                // If creating a new text overlay, just clear it
                currentEditingTextOverlay = null;
                selectedTextOverlay = null;
                editingCaptionId = null;
                viewModel.TextVM.HasSelectedText = false;
                
                // Re-enable +Add button
                canAddNewText = true;
                UpdateAddButtonState();
                
                CanvasView.InvalidateSurface();
            }
        }
        private void OnTextDoneTapped(object? sender, EventArgs e)
        {
            if (viewModel != null && viewModel.TextVM != null)
            {
                // Save current editing text overlay to captions array
                if (currentEditingTextOverlay != null)
                {
                    Caption caption;
                    
                    // Check if we're editing an existing caption or creating a new one
                    if (editingCaptionId.HasValue)
                    {
                        // Updating existing caption - preserve the ID
                        caption = new Caption
                        {
                            Id = editingCaptionId.Value, // Preserve original ID
                            Text = currentEditingTextOverlay.Text,
                            X = currentEditingTextOverlay.X,
                            Y = currentEditingTextOverlay.Y,
                            Color = currentEditingTextOverlay.Color,
                            FillColor = currentEditingTextOverlay.FillColor, // Save fill color
                            Size = currentEditingTextOverlay.Size,
                            IsBold = currentEditingTextOverlay.IsBold,
                            IsItalic = currentEditingTextOverlay.IsItalic,
                            FontFamily = "Arial", // Default font family
                            ModifiedAt = DateTime.Now // Update modification time
                        };
                    }
                    else
                    {
                        // Creating new caption
                        caption = new Caption
                        {
                            Text = currentEditingTextOverlay.Text,
                            X = currentEditingTextOverlay.X,
                            Y = currentEditingTextOverlay.Y,
                            Color = currentEditingTextOverlay.Color,
                            FillColor = currentEditingTextOverlay.FillColor, // Save fill color
                            Size = currentEditingTextOverlay.Size,
                            IsBold = currentEditingTextOverlay.IsBold,
                            IsItalic = currentEditingTextOverlay.IsItalic,
                            FontFamily = "Arial" // Default font family
                        };
                    }
                    
                    savedCaptions.Add(caption);
                    
                    // Register action for undo/redo (only for new captions, not edits)
                    if (!editingCaptionId.HasValue)
                    {
                        viewModel.RegisterAction(new AddCaptionAction(caption));
                    }
                    
                    // Convert to TextOverlay for rendering (non-editable)
                    var savedOverlay = new TextOverlay
                    {
                        Text = caption.Text,
                        X = caption.X,
                        Y = caption.Y,
                        Color = caption.Color,
                        FillColor = caption.FillColor, // Include fill color
                        Size = caption.Size,
                        IsSelected = false,
                        IsBold = caption.IsBold,
                        IsItalic = caption.IsItalic
                    };
                    textOverlays.Add(savedOverlay);
                    
                    // Clear current editing overlay and editing state
                    currentEditingTextOverlay = null;
                    selectedTextOverlay = null;
                    editingCaptionId = null; // Clear editing ID
                    viewModel.TextVM.HasSelectedText = false;
                    
                    // Re-enable +Add button
                    canAddNewText = true;
                    UpdateAddButtonState();
                    
                    // Notify that Reset button state may have changed (text finalized)
                    viewModel.NotifyCanResetChanged();
                    
                }
                
                // Close any open panels first
                viewModel.TextVM.ShowFontFamilyPanel = false;
                viewModel.TextVM.ShowTextColorPanel = false;
                viewModel.TextVM.ShowFillColorPanel = false;
                viewModel.TextVM.IsTextMode = false;
                viewModel.SelectedToolType = ToolType.None;
                
                // Dismiss keyboard when Done is clicked
                if (TextPanel != null)
                {
                    TextPanel.UnfocusEntry();
                }
                
                CanvasView.InvalidateSurface();
            }
        }
        
        private void UpdateAddButtonState()
        {
            // Update all +Add button labels to reflect enabled/disabled state
            // We'll use opacity to show disabled state
            var opacity = canAddNewText ? 1.0 : 0.5;
            var textColor = canAddNewText ? Colors.White : Colors.Gray;
            
            // Find all +Add labels in the visual tree and update them
            UpdateAddButtonInVisualTree(this, opacity, textColor);
        }
        
        private void UpdateAddButtonInVisualTree(VisualElement element, double opacity, Color textColor)
        {
            if (element is Label label && label.Text == "+Add")
            {
                label.Opacity = opacity;
                label.TextColor = textColor;
                // Disable gesture recognizer when disabled
                var tapGesture = label.GestureRecognizers.OfType<TapGestureRecognizer>().FirstOrDefault();
                if (tapGesture != null)
                {
                    // We can't directly disable a gesture recognizer, so we'll handle it in the handler
                }
            }
            
            if (element is Layout layout)
            {
                foreach (var child in layout.Children)
                {
                    if (child is VisualElement visualChild)
                    {
                        UpdateAddButtonInVisualTree(visualChild, opacity, textColor);
                    }
                }
            }
        }
        private void OnCropModeSelected(object? sender, TappedEventArgs e)
        {
            if (sender is Microsoft.Maui.Controls.View view && view.GestureRecognizers.OfType<TapGestureRecognizer>().FirstOrDefault() is TapGestureRecognizer tap)
            {
                var mode = tap.CommandParameter?.ToString();
                if (!string.IsNullOrEmpty(mode) && viewModel?.CropVM != null)
                {
                    viewModel.CropVM.SelectCropModeCommand.Execute(mode);
                    if (!viewModel.CropVM.IsCropMode)
                    {
                        viewModel.CropVM.StartCropMode();
                    }
                    CanvasView.InvalidateSurface();
                }
            }
        }
        private void OnCropRatioTapped(object? sender, TappedEventArgs e)
        {
            if (sender is Image img && img.GestureRecognizers.OfType<TapGestureRecognizer>().FirstOrDefault() is TapGestureRecognizer tap)
            {
                var ratio = tap.CommandParameter?.ToString();
                if (!string.IsNullOrEmpty(ratio) && viewModel?.CropVM != null)
                {
                    // Apply aspect ratio to crop
                    var aspectRatio = viewModel.CropVM.AspectRatios.FirstOrDefault(r =>
                        r.Name == ratio ||
                        (ratio == "1:1" && r.Name == "1:1") ||
                        (ratio == "3:2" && r.Ratio.HasValue && Math.Abs(r.Ratio.Value - 3.0f / 2.0f) < 0.01f) ||
                        (ratio == "4:3" && r.Ratio.HasValue && Math.Abs(r.Ratio.Value - 4.0f / 3.0f) < 0.01f) ||
                        (ratio == "16:9" && r.Ratio.HasValue && Math.Abs(r.Ratio.Value - 16.0f / 9.0f) < 0.01f) ||
                        (ratio == "Original" && r.Name == "Free") ||
                        (ratio == "Custom" && r.Name == "Free")
                    );

                    if (aspectRatio != null)
                    {
                        viewModel.CropVM.SelectedRatio = aspectRatio;
                    }

                    // Fast: Invalidate canvas immediately for preview
                    CanvasView.InvalidateSurface();
                }
            }
        }
        private void OnShapePresetTapped(object? sender, TappedEventArgs e)
        {
            if (sender is Microsoft.Maui.Controls.View view && view.GestureRecognizers.OfType<TapGestureRecognizer>().FirstOrDefault() is TapGestureRecognizer tap)
            {
                var preset = tap.CommandParameter?.ToString();
                if (!string.IsNullOrEmpty(preset) && viewModel?.ShapesVM != null)
                {
                    // Map preset names to ShapeType
                    ShapeType shapeType = ShapeType.None;
                    switch (preset)
                    {
                        case "Custom":
                        case "Rectangle":
                            shapeType = ShapeType.Rectangle;
                            break;
                        case "Circle":
                            shapeType = ShapeType.Circle;
                            break;
                        case "Ellipse":
                            shapeType = ShapeType.Circle; // Use circle for ellipse
                            break;
                        case "Square":
                            shapeType = ShapeType.Rectangle; // Use rectangle for square
                            break;
                        case "Original":
                            shapeType = ShapeType.Rectangle; // Default to rectangle
                            break;
                    }
                    
                    if (shapeType != ShapeType.None)
                    {
                        // Trigger shape creation via the event
                        OnShapeSelectedForCreation(shapeType);
                    }
                }
            }
        }
        private async void OnOpenFolderClicked(object? sender, EventArgs e)
        {
            try
            {
                var result = await MediaPicker.PickPhotoAsync(new MediaPickerOptions
                {
                    Title = "Select an image to edit"
                });
                if (result != null && viewModel != null)
                {
                    // Replace the current image with the selected one
                    viewModel.ReplaceImage(result.FullPath);
                }
            }
            catch (Exception ex)
            {
                await Application.Current!.MainPage!.DisplayAlert("Error", $"Failed to open gallery: {ex.Message}", "OK");
            }
        }
        private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
        {
            if (viewModel?.WorkingBitmap == null) return;
            var canvas = e.Surface.Canvas;
            var info = e.Info;
            var bitmap = viewModel.WorkingBitmap;
            // === SYNC FUSION 2025 CENTERED FOCUSED LOOK ===
            canvas.Clear(new SKColor(18, 18, 18)); // Exact background (#121212)
            // 1. Determine display dimensions (after rotation) for scaling calculation
            float displayW = bitmap.Width;
            float displayH = bitmap.Height;
            bool isRotated = (viewModel.RotationAngle % 180) != 0;
            if (isRotated)
            {
                displayW = bitmap.Height;
                displayH = bitmap.Width;
            }
            // 2. Add outside padding for centered focused container
            float padding = 60f; // Creates the "pro" focused look
            float availableWidth = info.Width - padding * 2f;
            float availableHeight = info.Height - padding * 2f;
            // 3. Calculate scale to fit inside padded area (letterbox)
            float scale = Math.Min(availableWidth / displayW, availableHeight / displayH);
            float scaledW = displayW * scale;
            float scaledH = displayH * scale;
            // 4. Center offsets (image will be centered with padding around it)
            float offsetX = (info.Width - scaledW) / 2f;
            float offsetY = (info.Height - scaledH) / 2f;
            canvas.Save();
            // Move to center of display area
            canvas.Translate(offsetX + scaledW / 2f, offsetY + scaledH / 2f);
            // Apply rotation
            canvas.RotateDegrees(viewModel.RotationAngle);
            // Apply flips
            if (viewModel.FlipHorizontal) canvas.Scale(-1, 1);
            if (viewModel.FlipVertical) canvas.Scale(1, -1);
            // Draw the bitmap centered at origin, using original bitmap dimensions with calculated scale
            var destRect = SKRect.Create(-bitmap.Width * scale / 2f, -bitmap.Height * scale / 2f, bitmap.Width * scale, bitmap.Height * scale);
            canvas.DrawBitmap(bitmap, destRect, new SKPaint
            {
                IsAntialias = true,
                FilterQuality = SKFilterQuality.High
            });
            canvas.Restore();
            // Draw overlays (crop, draw, text) on top
            DrawOverlaysWithTransform(canvas, info, scale, offsetX, offsetY, displayW, displayH);
        }
        private SKPoint TransformImageToScreen(SKPoint imagePoint)
        {
            var bitmap = viewModel.WorkingBitmap;
            if (bitmap == null) return imagePoint;
            float displayWidth = bitmap.Width;
            float displayHeight = bitmap.Height;
            if (viewModel.RotationAngle % 180 != 0)
            {
                displayWidth = bitmap.Height;
                displayHeight = bitmap.Width;
            }
            float padding = 60f;
            var canvasSize = CanvasView.CanvasSize;
            float availableWidth = canvasSize.Width - padding * 2f;
            float availableHeight = canvasSize.Height - padding * 2f;
            var scale = Math.Min(availableWidth / displayWidth, availableHeight / displayHeight);
            var offsetX = (canvasSize.Width - displayWidth * scale) / 2f;
            var offsetY = (canvasSize.Height - displayHeight * scale) / 2f;
            float screenCenterX = offsetX + displayWidth * scale / 2f;
            float screenCenterY = offsetY + displayHeight * scale / 2f;
            float imgX = (imagePoint.X - bitmap.Width / 2f) * scale;
            float imgY = (imagePoint.Y - bitmap.Height / 2f) * scale;
            float angleRad = viewModel.RotationAngle * (float)Math.PI / 180f;
            float cos = (float)Math.Cos(angleRad);
            float sin = (float)Math.Sin(angleRad);
            float rotatedX = imgX * cos - imgY * sin;
            float rotatedY = imgX * sin + imgY * cos;
            if (viewModel.FlipHorizontal) rotatedX = -rotatedX;
            if (viewModel.FlipVertical) rotatedY = -rotatedY;
            return new SKPoint(screenCenterX + rotatedX, screenCenterY + rotatedY);
        }
        private SKPoint TransformScreenToImage(SKPoint screenPoint)
        {
            var bitmap = viewModel.WorkingBitmap;
            if (bitmap == null) return screenPoint;
            float displayWidth = bitmap.Width;
            float displayHeight = bitmap.Height;
            if (viewModel.RotationAngle % 180 != 0)
            {
                displayWidth = bitmap.Height;
                displayHeight = bitmap.Width;
            }
            float padding = 60f;
            var canvasSize = CanvasView.CanvasSize;
            float availableWidth = canvasSize.Width - padding * 2f;
            float availableHeight = canvasSize.Height - padding * 2f;
            var scale = Math.Min(availableWidth / displayWidth, availableHeight / displayHeight);
            var offsetX = (canvasSize.Width - displayWidth * scale) / 2f;
            var offsetY = (canvasSize.Height - displayHeight * scale) / 2f;
            float screenCenterX = offsetX + displayWidth * scale / 2f;
            float screenCenterY = offsetY + displayHeight * scale / 2f;
            // Convert to centered coordinates
            float centeredX = screenPoint.X - screenCenterX;
            float centeredY = screenPoint.Y - screenCenterY;
            // Apply inverse flips
            if (viewModel.FlipHorizontal) centeredX = -centeredX;
            if (viewModel.FlipVertical) centeredY = -centeredY;
            // Apply inverse rotation
            float angleRad = -viewModel.RotationAngle * (float)Math.PI / 180f;
            float cos = (float)Math.Cos(angleRad);
            float sin = (float)Math.Sin(angleRad);
            float rotatedX = centeredX * cos - centeredY * sin;
            float rotatedY = centeredX * sin + centeredY * cos;
            // Convert back to image coordinates
            float imageX = (rotatedX / scale) + bitmap.Width / 2f;
            float imageY = (rotatedY / scale) + bitmap.Height / 2f;
            return new SKPoint(imageX, imageY);
        }
        private SKRect GetTransformedCropDisplayRect(out SKPoint[] transformedCorners)
        {
            var cropVM = viewModel.CropVM;
            var cropRect = cropVM.CropRect;
            var bitmap = viewModel.WorkingBitmap;
            float displayWidth = bitmap.Width;
            float displayHeight = bitmap.Height;
            if (viewModel.RotationAngle % 180 != 0)
            {
                displayWidth = bitmap.Height;
                displayHeight = bitmap.Width;
            }
            float padding = 60f;
            var canvasSize = CanvasView.CanvasSize;
            float availableWidth = canvasSize.Width - padding * 2f;
            float availableHeight = canvasSize.Height - padding * 2f;
            var scale = Math.Min(availableWidth / displayWidth, availableHeight / displayHeight);
            var offsetX = (canvasSize.Width - displayWidth * scale) / 2f;
            var offsetY = (canvasSize.Height - displayHeight * scale) / 2f;
            float centerX = offsetX + displayWidth * scale / 2f;
            float centerY = offsetY + displayHeight * scale / 2f;
            var corners = new SKPoint[]
            {
                new SKPoint(cropRect.Left, cropRect.Top),
                new SKPoint(cropRect.Right, cropRect.Top),
                new SKPoint(cropRect.Right, cropRect.Bottom),
                new SKPoint(cropRect.Left, cropRect.Bottom)
            };
            var transformed = new SKPoint[4];
            float angleRad = viewModel.RotationAngle * (float)Math.PI / 180f;
            float cos = (float)Math.Cos(angleRad);
            float sin = (float)Math.Sin(angleRad);
            for (int i = 0; i < 4; i++)
            {
                float x = (corners[i].X - bitmap.Width / 2f) * scale;
                float y = (corners[i].Y - bitmap.Height / 2f) * scale;
                float rx = x * cos - y * sin;
                float ry = x * sin + y * cos;
                if (viewModel.FlipHorizontal) rx = -rx;
                if (viewModel.FlipVertical) ry = -ry;
                transformed[i] = new SKPoint(centerX + rx, centerY + ry);
            }
            float minX = transformed.Min(p => p.X);
            float minY = transformed.Min(p => p.Y);
            float maxX = transformed.Max(p => p.X);
            float maxY = transformed.Max(p => p.Y);
            transformedCorners = transformed;
            return SKRect.Create(minX, minY, maxX - minX, maxY - minY);
        }
        // Track initial pan touch position for coordinate calculation
        private SKPoint? initialPanTouchPoint = null;
        private bool isWaitingForInitialPosition = false; // Track if we're waiting for first Running event
        
        // Pointer event handlers to capture actual touch position (more reliable than PanGestureRecognizer)
        // Platform-specific touch handler attachment
        private void AttachPlatformTouchHandlers()
        {
#if ANDROID
            if (TouchOverlay?.Handler?.PlatformView is Android.Views.View androidView)
            {
                androidView.Touch += OnAndroidViewTouch;
            }
            else
            {
                // Handler might not be ready yet, try again when loaded
                if (TouchOverlay != null)
                {
                    TouchOverlay.HandlerChanged += (s, e) =>
                    {
                        if (TouchOverlay?.Handler?.PlatformView is Android.Views.View view)
                        {
                            view.Touch += OnAndroidViewTouch;
                        }
                    };
                }
            }
#endif
        }
        
#if ANDROID
        private void OnAndroidViewTouch(object? sender, Android.Views.View.TouchEventArgs e)
        {
            if (CanvasContainer == null || viewModel?.WorkingBitmap == null) return;
            
            bool isDrawMode = viewModel.SelectedToolType == ToolType.Draw;
            bool isCropMode = viewModel.CropVM?.IsCropMode ?? false;
            
            if (!isDrawMode && !isCropMode) return;
            
            var motionEvent = e.Event;
            if (motionEvent == null) return;
            
            Android.Views.MotionEventActions action = (Android.Views.MotionEventActions)((int)motionEvent.Action & (int)Android.Views.MotionEventActions.Mask);
            
            // Get touch position relative to the view
            float x = motionEvent.GetX();
            float y = motionEvent.GetY();
            
            // Convert to coordinates relative to CanvasView (which is what TransformScreenToImage expects)
            // x, y are relative to the TouchOverlay view, we need to convert to CanvasView coordinates
            if (sender is Android.Views.View touchView && CanvasContainer != null && CanvasView != null)
            {
                // Get the TouchOverlay view's location on screen
                int[] touchViewLocation = new int[2];
                touchView.GetLocationOnScreen(touchViewLocation);
                
                // Get the CanvasView's location on screen
                int[] canvasViewLocation = new int[2];
                if (CanvasView.Handler?.PlatformView is Android.Views.View canvasAndroidView)
                {
                    canvasAndroidView.GetLocationOnScreen(canvasViewLocation);
                    
                    // Calculate absolute screen coordinates from touch
                    float absoluteScreenX = touchViewLocation[0] + x;
                    float absoluteScreenY = touchViewLocation[1] + y;
                    
                    // Convert to coordinates relative to CanvasView
                    var screenPoint = new SKPoint(
                        absoluteScreenX - canvasViewLocation[0],
                        absoluteScreenY - canvasViewLocation[1]
                    );
                    
                    if (action == Android.Views.MotionEventActions.Down)
                    {
                        // Store as initial pan position for subsequent pan events
                        initialPanTouchPoint = screenPoint;
                        
                        if (isDrawMode)
                        {
                            HandleTouchPressed(screenPoint);
                        }
                        else if (isCropMode)
                        {
                            HandleTouchPressed(screenPoint);
                        }
                    }
                }
            }
        }
#endif
        
        // Tap handler to capture initial touch position (fallback)
        private void OnTouchOverlayTapped(object? sender, TappedEventArgs e)
        {
            if (sender is Microsoft.Maui.Controls.View view && CanvasContainer != null && viewModel?.WorkingBitmap != null)
            {
                // Get the tap position relative to the container
                var containerBounds = CanvasContainer.Bounds;
                var tapPosition = e.GetPosition(CanvasContainer);
                
                var screenPoint = new SKPoint(
                    (float)(containerBounds.X + tapPosition.Value.X),
                    (float)(containerBounds.Y + tapPosition.Value.Y)
                );
                
                bool isCropMode = viewModel.CropVM?.IsCropMode ?? false;
                bool isDrawMode = viewModel.SelectedToolType == ToolType.Draw;
                
                if (isCropMode)
                {
                    
                    // Store as initial pan position for subsequent pan events
                    initialPanTouchPoint = screenPoint;
                    HandleTouchPressed(screenPoint);
                }
                else if (isDrawMode)
                {
                    
                    // Store as initial pan position for subsequent pan events
                    initialPanTouchPoint = screenPoint;
                    HandleTouchPressed(screenPoint);
                }
            }
        }
        
        // Pan gesture handler (works on Android when SKTouchEventArgs doesn't)
        private void OnPanGestureUpdated(object? sender, PanUpdatedEventArgs e)
        {
            if (sender is not Microsoft.Maui.Controls.View view || CanvasContainer == null || viewModel?.WorkingBitmap == null) return;
            
            bool isCropMode = viewModel.CropVM?.IsCropMode ?? false;
            bool isDrawMode = viewModel.SelectedToolType == ToolType.Draw;
            
            if (!isCropMode && !isDrawMode) return;
            
            if (isCropMode)
            {
            }
            else if (isDrawMode)
            {
            }
            
            // Get the view's bounds relative to the page
            var viewBounds = view.Bounds;
            var containerBounds = CanvasContainer.Bounds;
            
            SKPoint screenPoint;
            
            if (e.StatusType == GestureStatus.Started)
            {
                // At start, TotalX/TotalY are 0
                // If we have an initialPanTouchPoint from PointerPressed or OnTouchOverlayTapped, use it
                // Otherwise, we'll wait for the first Running event to get the actual position
                if (initialPanTouchPoint.HasValue)
                {
                    // We already have the initial position from PointerPressed, so we don't need to call HandleTouchPressed again
                    // Just log that we're using it
                    screenPoint = initialPanTouchPoint.Value;
                    if (isCropMode)
                    {
                    }
                    else if (isDrawMode)
                    {
                    }
                    // Don't call HandleTouchPressed here - it was already called in PointerPressed
                }
                else
                {
                    // For draw mode, we'll capture the initial position from the first Running event
                    // Don't call HandleTouchPressed here - wait for Running event
                    if (isDrawMode)
                    {
                        isWaitingForInitialPosition = true;
                    }
                    // For crop mode, use center as fallback
                    else if (isCropMode)
                    {
                        screenPoint = new SKPoint(
                            (float)(containerBounds.X + viewBounds.Width / 2),
                            (float)(containerBounds.Y + viewBounds.Height / 2)
                        );
                        HandleTouchPressed(screenPoint);
                    }
                }
            }
            else if (e.StatusType == GestureStatus.Running)
            {
                // TotalX/TotalY are cumulative deltas from the start position
                if (initialPanTouchPoint.HasValue)
                {
                    screenPoint = new SKPoint(
                        initialPanTouchPoint.Value.X + (float)e.TotalX,
                        initialPanTouchPoint.Value.Y + (float)e.TotalY
                    );
                }
                else
                {
                    // For draw mode: if this is the first Running event and we haven't started drawing yet,
                    // we need to estimate the initial position.
                    // 
                    // The key insight: TotalX/TotalY are deltas from the initial touch point P.
                    // So: currentPosition = P + (TotalX, TotalY)
                    // We need to estimate P.
                    //
                    // Strategy: Since we don't know P, but we know the user touched somewhere on the view,
                    // we can use the view bounds to estimate. The challenge is that TotalX/TotalY are small
                    // in the first Running event, meaning the user hasn't moved much from the initial touch.
                    //
                    // Heuristic: If the delta is small, the initial touch was likely near where the current
                    // position would be if we assume it's near the center. But we need to work backwards:
                    // - Estimate current position as somewhere on the view (we'll use center as a reference)
                    // - Initial position = estimated current - delta
                    // - But we don't know the exact current position either!
                    //
                    // Better heuristic: For the first Running event with small delta, assume the initial
                    // touch was at the center of the view, and the current position is center + delta.
                    // This is reasonable because:
                    // 1. Small deltas mean the user touched and moved slightly
                    // 2. Without knowing the exact touch position, center is a reasonable default
                    // 3. The error will be small if the user touched near center, and will be corrected
                    //    as they continue drawing
                    if (isDrawMode && !isDrawing && isWaitingForInitialPosition)
                    {
                        // Estimate initial position based on the first Running event
                        // Strategy: Since TotalX/TotalY are small deltas from the actual touch point,
                        // and we know the user touched somewhere on the view, we can estimate:
                        // - The current position (after small movement) is approximately: center + delta
                        // - The initial position (before movement) is approximately: (center + delta) - delta = center
                        // But this assumes the touch started at center, which is often wrong.
                        //
                        // Better approach: Use the view bounds to estimate. If the delta is small,
                        // the touch likely started near where the current position would be if we
                        // assume it's in a reasonable area of the view.
                        //
                        // Heuristic: Estimate initial position as center, but adjust slightly based on delta direction
                        // This accounts for the fact that small movements often indicate the touch
                        // started slightly away from center in the direction opposite to the movement.
                        float centerX = (float)(containerBounds.X + containerBounds.Width / 2);
                        float centerY = (float)(containerBounds.Y + containerBounds.Height / 2);
                        
                        // Adjust initial position estimate: if user moved right, they likely touched slightly left of center
                        // Scale factor of 0.5 means we assume the touch was halfway between center and the offset
                        float estimatedInitialX = centerX - (float)(e.TotalX * 0.5f);
                        float estimatedInitialY = centerY - (float)(e.TotalY * 0.5f);
                        
                        // Clamp to view bounds
                        estimatedInitialX = Math.Max((float)containerBounds.X, Math.Min(estimatedInitialX, (float)(containerBounds.X + containerBounds.Width)));
                        estimatedInitialY = Math.Max((float)containerBounds.Y, Math.Min(estimatedInitialY, (float)(containerBounds.Y + containerBounds.Height)));
                        
                        screenPoint = new SKPoint(estimatedInitialX, estimatedInitialY);
                        initialPanTouchPoint = screenPoint;
                        isWaitingForInitialPosition = false;
                        HandleTouchPressed(screenPoint);
                        
                        // Now calculate the current position based on the estimated initial position
                        screenPoint = new SKPoint(
                            initialPanTouchPoint.Value.X + (float)e.TotalX,
                            initialPanTouchPoint.Value.Y + (float)e.TotalY
                        );
                    }
                    else
                    {
                        // Fallback: use container center + delta
                        screenPoint = new SKPoint(
                            (float)(containerBounds.X + containerBounds.Width / 2 + e.TotalX),
                            (float)(containerBounds.Y + containerBounds.Height / 2 + e.TotalY)
                        );
                    }
                }
                
                if (isCropMode)
                {
                }
                else if (isDrawMode)
                {
                }
                
                // Only call HandleTouchMoved if we're already drawing (HandleTouchPressed was called)
                if (isDrawing || isCropMode)
                {
                    HandleTouchMoved(screenPoint);
                }
            }
            else
            {
                // Canceled or Completed
                initialPanTouchPoint = null;
                isWaitingForInitialPosition = false;
                HandleTouchReleased();
                return;
            }
        }
        
        private void OnTouch(object? sender, SKTouchEventArgs e)
        {
            if (viewModel?.WorkingBitmap == null)
            {
                e.Handled = true;
                return;
            }
            
            var screenPoint = e.Location;
            bool isCropMode = viewModel.CropVM?.IsCropMode ?? false;
            
            // Always process crop touches if in crop mode
            if (isCropMode)
            {
                switch (e.ActionType)
                {
                    case SKTouchAction.Pressed:
                        HandleTouchPressed(screenPoint);
                        break;
                    case SKTouchAction.Moved:
                        HandleTouchMoved(screenPoint);
                        break;
                    case SKTouchAction.Released:
                    case SKTouchAction.Cancelled:
                        HandleTouchReleased();
                        break;
                }
                e.Handled = true;
                return;
            }
            
            // Handle Shapes mode - MUST be before Draw mode check
            if (viewModel?.SelectedToolType == ToolType.Shapes && currentEditingShape != null)
            {
                switch (e.ActionType)
                {
                    case SKTouchAction.Pressed:
                        HandleTouchPressed(screenPoint);
                        break;
                    case SKTouchAction.Moved:
                        HandleTouchMoved(screenPoint);
                        break;
                    case SKTouchAction.Released:
                    case SKTouchAction.Cancelled:
                        HandleTouchReleased();
                        break;
                }
                e.Handled = true;
                return; // Exit early - shape handling is done
            }
            
            // Handle Draw mode - try to use SKTouchEventArgs directly as fallback
            // PanGestureRecognizer is preferred but may not always work
            if (viewModel?.SelectedToolType == ToolType.Draw)
            {
                
                // Try to handle draw mode directly with SKTouchEventArgs as fallback
                // This ensures drawing works even if PanGestureRecognizer doesn't fire
                // Use e.Location directly instead of creating a new variable
                switch (e.ActionType)
                {
                    case SKTouchAction.Pressed:
                        HandleTouchPressed(e.Location);
                        break;
                    case SKTouchAction.Moved:
                        HandleTouchMoved(e.Location);
                        break;
                    case SKTouchAction.Released:
                    case SKTouchAction.Cancelled:
                        HandleTouchReleased();
                        break;
                }
                
                e.Handled = true;
                return;
            }
            
            // Fallback for other tool types
            switch (e.ActionType)
            {
                case SKTouchAction.Pressed:
                    HandleTouchPressed(screenPoint);
                    break;
                case SKTouchAction.Moved:
                    HandleTouchMoved(screenPoint);
                    break;
                case SKTouchAction.Released:
                case SKTouchAction.Cancelled:
                    HandleTouchReleased();
                    break;
            }
            e.Handled = true;
        }
        private void HandleTouchPressed(SKPoint screenPoint)
        {
            if (viewModel == null) return;
            
            bool isCropMode = viewModel.CropVM?.IsCropMode ?? false;
            
            // Check if tapping on saved elements when NOT in any tool mode
            // This allows users to tap on saved text/shapes to edit them
            
            // Only check for tap-to-edit if we're not in any active tool mode
            if (!isCropMode && 
                viewModel.SelectedToolType != ToolType.Draw && 
                viewModel.SelectedToolType != ToolType.Text && 
                viewModel.SelectedToolType != ToolType.Shapes &&
                currentEditingTextOverlay == null &&
                currentEditingShape == null)
            {
                var imagePoint = TransformScreenToImage(screenPoint);
                
                // First check for saved text caption
                var savedCaption = FindSavedCaptionAtPoint(imagePoint);
                if (savedCaption != null)
                {
                    // Remove from saved lists
                    savedCaptions.Remove(savedCaption);
                    var overlayToRemove = textOverlays.FirstOrDefault(o => 
                        Math.Abs(o.X - savedCaption.X) < 0.1f && 
                        Math.Abs(o.Y - savedCaption.Y) < 0.1f &&
                        o.Text == savedCaption.Text);
                    if (overlayToRemove != null)
                    {
                        textOverlays.Remove(overlayToRemove);
                    }
                    
                    // Create editable overlay from saved caption
                    currentEditingTextOverlay = new TextOverlay
                    {
                        Text = savedCaption.Text,
                        X = savedCaption.X,
                        Y = savedCaption.Y,
                        Color = savedCaption.Color,
                        FillColor = savedCaption.FillColor,
                        Size = savedCaption.Size,
                        IsSelected = true,
                        IsBold = savedCaption.IsBold,
                        IsItalic = savedCaption.IsItalic
                    };
                    editingCaptionId = savedCaption.Id;
                    selectedTextOverlay = currentEditingTextOverlay;
                    
                    // Activate Text mode
                    if (viewModel.TextVM != null)
                    {
                        viewModel.SelectToolByNameCommand.Execute("Text");
                        viewModel.TextVM.StartTextMode();
                        viewModel.TextVM.HasSelectedText = true;
                        viewModel.TextVM.SelectedTextColor = ConvertSKColorToColor(savedCaption.Color);
                        viewModel.TextVM.IsBold = savedCaption.IsBold;
                        viewModel.TextVM.IsItalic = savedCaption.IsItalic;
                        viewModel.TextVM.SyncTextFromOverlay(savedCaption.Text);
                        canAddNewText = false;
                    }
                    
                    UpdateToolPanelVisibility();
                    CanvasView.InvalidateSurface();
                    return;
                }
                
                // Then check for saved shape
                var savedShape = FindSavedShapeAtPoint(imagePoint);
                if (savedShape != null)
                {
                    // Remove from saved list
                    savedShapes.Remove(savedShape);
                    
                    // Set as current editing shape
                    savedShape.IsSelected = true;
                    currentEditingShape = savedShape;
                    editingShapeId = savedShape.Id;
                    
                    // Activate Shapes mode
                    if (viewModel.ShapesVM != null)
                    {
                        viewModel.SelectToolByNameCommand.Execute("Shapes");
                        viewModel.ShapesVM.StartShapeMode();
                        viewModel.ShapesVM.SelectedShapeType = savedShape.Type;
                        viewModel.ShapesVM.StrokeWidth = savedShape.StrokeWidth;
                        viewModel.ShapesVM.SelectedStrokeColor = ConvertSKColorToColor(savedShape.StrokeColor);
                        if (savedShape.FillColor.HasValue)
                        {
                            viewModel.ShapesVM.SelectedFillColor = ConvertSKColorToColor(savedShape.FillColor.Value);
                        }
                    }
                    
                    UpdateToolPanelVisibility();
                    CanvasView.InvalidateSurface();
                    return;
                }
            }
            
            // Check if in crop mode with new system
            if (viewModel.CropVM != null && isCropMode)
            {
                var cropVM = viewModel.CropVM;
                var bitmap = viewModel.WorkingBitmap;
                var cropRect = cropVM.CropRect;
                
                
                // Safety check: if crop rect is empty or invalid, initialize it
                if (bitmap != null)
                {
                    float width = bitmap.Width;
                    float height = bitmap.Height;
                    if (viewModel.RotationAngle % 180 != 0)
                    {
                        (width, height) = (height, width);
                    }
                    
                    if (cropRect.IsEmpty || cropRect.Width < 20 || cropRect.Height < 20 || 
                        cropRect.Left < 0 || cropRect.Top < 0 || 
                        cropRect.Right > width || cropRect.Bottom > height)
                    {
                        cropVM.CropRect = new SKRect(0, 0, width, height);
                        cropRect = cropVM.CropRect;
                    }
                }
                
                // Get screen-space crop rect for handle detection
                try
                {
                    var screenCropRect = GetTransformedCropDisplayRect(out SKPoint[] screenCorners);
                    
                    // Detect handle in SCREEN space (where handles are actually drawn)
                    draggingHandle = GetHandleAtScreenPoint(screenPoint, screenCorners, screenCropRect);

                    if (draggingHandle.HasValue)
                    {
                        // Store initial state for delta calculation (in image space for calculations)
                        var imgPoint = TransformScreenToImage(screenPoint);
                        initialTouch = imgPoint;
                        // Ensure we have a valid initial crop rect
                        if (cropRect.IsEmpty || cropRect.Width < 20 || cropRect.Height < 20)
                        {
                            float width = bitmap.Width;
                            float height = bitmap.Height;
                            if (viewModel.RotationAngle % 180 != 0)
                            {
                                (width, height) = (height, width);
                            }
                            initialCropRect = new SKRect(0, 0, width, height);
                            cropVM.CropRect = initialCropRect;
                        }
                        else
                        {
                            initialCropRect = new SKRect(cropRect.Left, cropRect.Top, cropRect.Right, cropRect.Bottom);
                        }
                        CanvasView.InvalidateSurface();
                        return;
                    }
                    else
                    {
                    }
                }
                catch (Exception ex)
                {
                }
            }
            else if (viewModel.SelectedToolType == ToolType.Draw && viewModel.DrawVM != null)
            {
                // Don't start a new stroke if one is already in progress
                if (isDrawing && currentPath != null)
                {
                    return;
                }
                
                
                // Transform to image space for drawing
                var imagePoint = TransformScreenToImage(screenPoint);
                
                // Clamp point to image bounds
                var bitmap = viewModel.WorkingBitmap;
                if (bitmap != null)
                {
                    float originalX = imagePoint.X;
                    float originalY = imagePoint.Y;
                    imagePoint.X = Math.Max(0, Math.Min(imagePoint.X, bitmap.Width));
                    imagePoint.Y = Math.Max(0, Math.Min(imagePoint.Y, bitmap.Height));
                    
                    if (originalX != imagePoint.X || originalY != imagePoint.Y)
                    {
                    }
                    
                }
                
                
                // Ensure we clear any previous path
                if (currentPath != null)
                {
                    currentPath.Dispose();
                }
                currentPath = null;
                
                // Create a new path for this stroke
                currentPath = new SKPath();
                
                currentPath.MoveTo(imagePoint.X, imagePoint.Y);
                
                var pointCount = currentPath.PointCount;
                
                if (pointCount > 0)
                {
                    var firstPoint = currentPath.GetPoint(0);
                    
                    if (Math.Abs(firstPoint.X - imagePoint.X) > 0.01f || Math.Abs(firstPoint.Y - imagePoint.Y) > 0.01f)
                    {
                    }
                    else
                    {
                    }
                }
                else
                {
                }
                
                // Store the starting point
                lastPoint = imagePoint;
                previousPoint = null; // Reset previous point for new stroke
                isDrawing = true;
                
                CanvasView.InvalidateSurface();
            }
            else if (viewModel.SelectedToolType == ToolType.Crop)
            {
                // Transform to image space for legacy crop mode
                var imagePoint = TransformScreenToImage(screenPoint);
                isCropMode = true;
                cropStart = new SKPoint(imagePoint.X, imagePoint.Y);
                cropRect = new SKRect(imagePoint.X, imagePoint.Y, imagePoint.X, imagePoint.Y);
            }
            else if (viewModel.SelectedToolType == ToolType.Text && viewModel.TextVM != null)
            {
                var imagePoint = TransformScreenToImage(screenPoint);
                
                if (currentEditingTextOverlay != null && currentEditingTextOverlay.IsSelected)
                {
                    // Check if tapping on resize handle first
                    var handle = GetTextHandleAtPoint(imagePoint, currentEditingTextOverlay);
                    if (handle.HasValue)
                    {
                        draggingTextHandle = handle.Value;
                        initialTextSize = currentEditingTextOverlay.Size;
                        initialTextBounds = ComputeTextBounds(currentEditingTextOverlay, currentEditingTextOverlay.X, currentEditingTextOverlay.Y);
                        initialTextTouch = imagePoint;
                        CanvasView.InvalidateSurface();
                        return;
                    }
                }

                var tappedOverlay = FindTextOverlayAtPoint(imagePoint);
                
                if (tappedOverlay != null)
                {
                    if (tappedOverlay == currentEditingTextOverlay)
                    {
                        if (selectedTextOverlay != null && selectedTextOverlay != tappedOverlay)
                        {
                            selectedTextOverlay.IsSelected = false;
                        }
                        tappedOverlay.IsSelected = true;
                        selectedTextOverlay = tappedOverlay;
                        viewModel.TextVM.HasSelectedText = true;
                        viewModel.TextVM.SelectedTextColor = ConvertSKColorToColor(tappedOverlay.Color);
                        viewModel.TextVM.IsBold = tappedOverlay.IsBold;
                        viewModel.TextVM.IsItalic = tappedOverlay.IsItalic;

                        viewModel.TextVM.SyncTextFromOverlay(tappedOverlay.Text);

                        initialTextPosition = new SKPoint(tappedOverlay.X, tappedOverlay.Y);
                        initialTextTouch = imagePoint;
                        isDraggingText = false;
                        textDragStarted = false;

                        // Ensure TextPanel is visible
                        if (TextPanel != null && !TextPanel.IsVisible)
                        {
                            TextPanel.IsVisible = true;
                        }

                        // Force canvas redraw
                        CanvasView.InvalidateSurface();

                        // Open keyboard immediately
                        if (TextPanel != null)
                        {
                            MainThread.BeginInvokeOnMainThread(() =>
                            {
                                TextPanel.FocusEntry();
                            });
                        }
                    }
                    // If tapping on a saved caption, do nothing (they're read-only)
                }
                else
                {
                    // Only allow creating new text if +Add is enabled and no text is currently being edited
                    // Don't create new text on canvas tap - user must use +Add button
                    // This prevents accidental text creation when trying to move existing text
                    if (canAddNewText && currentEditingTextOverlay == null)
                    {
                        // User can tap to add text, but we'll use +Add button for consistency
                        // For now, do nothing on canvas tap - require +Add button
                    }
                    else if (currentEditingTextOverlay != null)
                    {
                        // If there's a current editing overlay, allow moving it even if tap is outside bounds
                        // This helps with moving text that might be partially off-screen
                        if (selectedTextOverlay == currentEditingTextOverlay)
                        {
                            // Store initial position for potential dragging
                            initialTextPosition = new SKPoint(currentEditingTextOverlay.X, currentEditingTextOverlay.Y);
                            initialTextTouch = imagePoint;
                            isDraggingText = false;
                            textDragStarted = false;
                        }
                    }
                }
            }
            else if (viewModel.SelectedToolType == ToolType.Shapes && viewModel.ShapesVM != null && currentEditingShape != null)
            {
                
                // Transform to image space
                var imagePoint = TransformScreenToImage(screenPoint);

                // Check if tapping on rotation handle first (before other handles)
                var bounds = currentEditingShape.Bounds;
                float padding = 80f; // Match the padding used in DrawShape
                var borderBounds = SKRect.Create(
                    bounds.Left - padding,
                    bounds.Top - padding,
                    bounds.Width + padding * 2,
                    bounds.Height + padding * 2
                );
                
                // Rotation handle is at top-center, above the border
                SKPoint rotationHandlePoint = new SKPoint(borderBounds.MidX, borderBounds.Top - 60f);
                float rotationHandleRadius = 140f; // MASSIVE touch target - super easy to grab!
                float distToRotationHandle = (float)Math.Sqrt(
                    Math.Pow(imagePoint.X - rotationHandlePoint.X, 2) +
                    Math.Pow(imagePoint.Y - rotationHandlePoint.Y, 2)
                );
                
                if (distToRotationHandle <= rotationHandleRadius)
                {
                    draggingShapeHandle = 5; // 5 = rotation
                    initialShapeTouch = imagePoint;
                    initialShapePosition = new SKPoint(bounds.MidX, bounds.MidY);
                    initialRotation = currentEditingShape.Rotation;
                    CanvasView.InvalidateSurface();
                    return;
                }
                
                // Check if tapping on shape handle (corner handles) or body
                var handle = GetShapeHandleAtPoint(imagePoint, currentEditingShape);
                if (handle.HasValue)
                {
                    // Store initial state for resizing
                    draggingShapeHandle = handle.Value;
                    initialShapeBounds = new SKRect(
                        currentEditingShape.Bounds.Left,
                        currentEditingShape.Bounds.Top,
                        currentEditingShape.Bounds.Right,
                        currentEditingShape.Bounds.Bottom
                    );
                    initialShapeTouch = imagePoint;
                    CanvasView.InvalidateSurface();
                    return;
                }
                
                bool isInShape = IsPointInShape(imagePoint, currentEditingShape);
                
                if (isInShape)
                {
                    // Store initial state for dragging - move mode starts immediately
                    draggingShapeHandle = 4; // 4 = move
                    initialShapePosition = new SKPoint(currentEditingShape.Bounds.Left, currentEditingShape.Bounds.Top);
                    initialShapeTouch = imagePoint;
                    isDraggingShape = true; // Start dragging immediately
                    shapeDragStarted = true;
                    // DO NOT set initialShapeBounds here for move! Only for resize handles
                    // initialShapeBounds is only set when a handle is tapped (resize mode)
                    // For move mode, we use currentEditingShape.Bounds.Width/Height in HandleTouchMoved
                    CanvasView.InvalidateSurface();
                    return;
                }
                else
                {
                }
            }
            else
            {
            }

            CanvasView.InvalidateSurface();
        }
        private void HandleTouchMoved(SKPoint screenPoint)
        {
            
            if (draggingHandle != null && viewModel?.CropVM != null && viewModel.CropVM.IsCropMode)
            {
                var cropVM = viewModel.CropVM;
                var bitmap = viewModel.WorkingBitmap;
                if (bitmap == null)
                {
                    return;
                }
                
                // Ensure initial state is valid
                if (initialCropRect.IsEmpty)
                {
                    initialCropRect = cropVM.CropRect;
                    if (initialCropRect.IsEmpty)
                    {
                        initialCropRect = new SKRect(0, 0, bitmap.Width, bitmap.Height);
                    }
                }
                
                
                // Transform screen point to image space
                var imgPoint = TransformScreenToImage(screenPoint);

                // Calculate delta from initial touch point
                float dx = imgPoint.X - initialTouch.X;
                float dy = imgPoint.Y - initialTouch.Y;
                
                
                // Determine target aspect ratio
                float? targetRatio = null;
                
                // Check if crop mode enforces aspect ratio
                switch (cropVM.SelectedCropMode)
                {
                    case CropMode.Circle:
                    case CropMode.Square:
                        targetRatio = 1.0f; // 1:1 ratio
                        break;
                    case CropMode.Ratio3_1:
                        targetRatio = 3.0f / 1.0f;
                        break;
                    case CropMode.Ratio3_2:
                        targetRatio = 3.0f / 2.0f;
                        break;
                    case CropMode.Ratio4_3:
                        targetRatio = 4.0f / 3.0f;
                        break;
                    case CropMode.Ratio5_4:
                        targetRatio = 5.0f / 4.0f;
                        break;
                    case CropMode.Ratio7_5:
                        targetRatio = 7.0f / 5.0f;
                        break;
                    case CropMode.Ratio16_9:
                        targetRatio = 16.0f / 9.0f;
                        break;
                }
                
                // If no mode-specific ratio, check if aspect ratio is selected (and not "Free")
                if (!targetRatio.HasValue && cropVM.SelectedRatio != null && cropVM.SelectedRatio.Ratio.HasValue)
                {
                    targetRatio = cropVM.SelectedRatio.Ratio.Value;
                }
                
                // Start with initial crop rect (create a copy to modify)
                var rect = new SKRect(initialCropRect.Left, initialCropRect.Top, initialCropRect.Right, initialCropRect.Bottom);
                
                // Apply delta based on which handle is being dragged
                if (targetRatio.HasValue && draggingHandle != 4)
                {
                    // Maintain aspect ratio while dragging handles
                    AdjustCropRectWithAspectRatio(ref rect, draggingHandle.Value, dx, dy, targetRatio.Value, initialCropRect, bitmap);
                }
                else
                {
                    // Free-form adjustment (no aspect ratio constraint)
                    switch (draggingHandle.Value)
                    {
                        case 0: // top-left
                            rect.Left = initialCropRect.Left + dx;
                            rect.Top = initialCropRect.Top + dy;
                            // Ensure valid rect (left < right, top < bottom)
                            if (rect.Left >= rect.Right) rect.Left = rect.Right - 20;
                            if (rect.Top >= rect.Bottom) rect.Top = rect.Bottom - 20;
                            break;
                        case 1: // top-right
                            rect.Right = initialCropRect.Right + dx;
                            rect.Top = initialCropRect.Top + dy;
                            if (rect.Left >= rect.Right) rect.Right = rect.Left + 20;
                            if (rect.Top >= rect.Bottom) rect.Top = rect.Bottom - 20;
                            break;
                        case 2: // bottom-left
                            rect.Left = initialCropRect.Left + dx;
                            rect.Bottom = initialCropRect.Bottom + dy;
                            if (rect.Left >= rect.Right) rect.Left = rect.Right - 20;
                            if (rect.Top >= rect.Bottom) rect.Bottom = rect.Top + 20;
                            break;
                        case 3: // bottom-right
                            rect.Right = initialCropRect.Right + dx;
                            rect.Bottom = initialCropRect.Bottom + dy;
                            if (rect.Left >= rect.Right) rect.Right = rect.Left + 20;
                            if (rect.Top >= rect.Bottom) rect.Bottom = rect.Top + 20;
                            break;
                        case 4: // move whole rect
                            rect.Left = initialCropRect.Left + dx;
                            rect.Top = initialCropRect.Top + dy;
                            float width = initialCropRect.Width;
                            float height = initialCropRect.Height;
                            rect.Right = rect.Left + width;
                            rect.Bottom = rect.Top + height;
                            break;
                    }
                }
                
                // Ensure minimum size and valid rect
                if (rect.Width >= 20 && rect.Height >= 20)
                {
                    // Clamp to image bounds
                    rect.Left = Math.Max(0, Math.Min(rect.Left, bitmap.Width - 20));
                    rect.Top = Math.Max(0, Math.Min(rect.Top, bitmap.Height - 20));
                    rect.Right = Math.Max(rect.Left + 20, Math.Min(rect.Right, bitmap.Width));
                    rect.Bottom = Math.Max(rect.Top + 20, Math.Min(rect.Bottom, bitmap.Height));

                    cropVM.CropRect = rect;
                    CanvasView.InvalidateSurface();
                }
                else
                {
                }
            }
            else if (draggingTextHandle.HasValue && draggingTextHandle.Value == 3 && currentEditingTextOverlay != null && viewModel?.WorkingBitmap != null)
            {
                var imagePoint = TransformScreenToImage(screenPoint);
                var bitmap = viewModel.WorkingBitmap;
                
                // For bottom-right corner, calculate distance from top-left corner for scaling
                float initialTopLeftX = initialTextBounds.Left;
                float initialTopLeftY = initialTextBounds.Top;
                float initialBottomRightX = initialTextBounds.Right;
                float initialBottomRightY = initialTextBounds.Bottom;
                
                // Calculate initial diagonal distance from top-left to bottom-right
                float initialWidth = initialBottomRightX - initialTopLeftX;
                float initialHeight = initialBottomRightY - initialTopLeftY;
                float initialDiagonal = (float)Math.Sqrt(initialWidth * initialWidth + initialHeight * initialHeight);
                
                // Calculate current diagonal distance from top-left to current touch point
                float currentWidth = imagePoint.X - initialTopLeftX;
                float currentHeight = imagePoint.Y - initialTopLeftY;
                float currentDiagonal = (float)Math.Sqrt(currentWidth * currentWidth + currentHeight * currentHeight);
                
                // Scale text size proportionally based on diagonal distance
                float scaleFactor = initialDiagonal > 0.1f ? currentDiagonal / initialDiagonal : 1f;
                
                float newSize = Math.Max(50f, Math.Min(500f, initialTextSize * scaleFactor));
                currentEditingTextOverlay.Size = newSize;
                viewModel.TextVM.TextSize = newSize;
                
                CanvasView.InvalidateSurface();
            }
            else if (selectedTextOverlay != null && viewModel?.WorkingBitmap != null &&
                     (viewModel.SelectedToolType == ToolType.Text || isDraggingText))
            {
                if (selectedTextOverlay != currentEditingTextOverlay)
                {
                    return;
                }
                
                var imagePoint = TransformScreenToImage(screenPoint);

                // Calculate delta from initial touch point
                float dx = imagePoint.X - initialTextTouch.X;
                float dy = imagePoint.Y - initialTextTouch.Y;
                float distance = (float)Math.Sqrt(dx * dx + dy * dy);

                // Start dragging only if movement exceeds threshold
                if (!textDragStarted && distance > TextDragThreshold)
                {
                    isDraggingText = true;
                    textDragStarted = true;
                }

                // If dragging has started, update text position
                if (isDraggingText && textDragStarted && currentEditingTextOverlay != null)
                {
                    // Calculate proposed new center
                    float proposedX = initialTextPosition.X + dx;
                    float proposedY = initialTextPosition.Y + dy;

                    // Get current bounds with proposed position
                    var proposedBounds = ComputeTextBounds(currentEditingTextOverlay, proposedX, proposedY);

                    // Adjust if out of bounds
                    var bitmap = viewModel.WorkingBitmap;
                    float adjustX = 0;
                    float adjustY = 0;

                    if (proposedBounds.Left < 0)
                    {
                        adjustX = -proposedBounds.Left;
                    }
                    else if (proposedBounds.Right > bitmap.Width)
                    {
                        adjustX = bitmap.Width - proposedBounds.Right;
                    }

                    if (proposedBounds.Top < 0)
                    {
                        adjustY = -proposedBounds.Top;
                    }
                    else if (proposedBounds.Bottom > bitmap.Height)
                    {
                        adjustY = bitmap.Height - proposedBounds.Bottom;
                    }

                    // Apply adjustments to center
                    float newX = proposedX + adjustX;
                    float newY = proposedY + adjustY;

                    // Update text overlay position (only current editing overlay can be moved)
                    currentEditingTextOverlay.X = newX;
                    currentEditingTextOverlay.Y = newY;
                    selectedTextOverlay.X = newX;
                    selectedTextOverlay.Y = newY;

                    CanvasView.InvalidateSurface();
                }
            }
            else if (draggingShapeHandle != null && currentEditingShape != null && viewModel?.WorkingBitmap != null &&
                     (viewModel.SelectedToolType == ToolType.Shapes || isDraggingShape))
            {
                
                var bitmap = viewModel.WorkingBitmap;
                
                // Transform screen point to image space
                var imagePoint = TransformScreenToImage(screenPoint);
                
                // Calculate delta from initial touch point
                float dx = imagePoint.X - initialShapeTouch.X;
                float dy = imagePoint.Y - initialShapeTouch.Y;
                
                if (draggingShapeHandle == 4) // Moving shape
                {
                    
                    // Move the entire shape immediately (no threshold)
                    float newX = initialShapePosition.X + dx;
                    float newY = initialShapePosition.Y + dy;
                    
                    // FIX: Use CURRENT shape size, not initialShapeBounds (which is only for resize!)
                    float width = currentEditingShape.Bounds.Width;
                    float height = currentEditingShape.Bounds.Height;
                    
                    // Fallback safety in case bounds are invalid
                    if (width <= 0) width = 400f;
                    if (height <= 0) height = 400f;
                    
                    
                    // Clamp to image bounds - ensure shape stays completely inside image
                    float clampedX = Math.Max(0, Math.Min(newX, bitmap.Width - width));
                    float clampedY = Math.Max(0, Math.Min(newY, bitmap.Height - height));
                    
                    currentEditingShape.Bounds = new SKRect(
                        clampedX,
                        clampedY,
                        clampedX + width,
                        clampedY + height
                    );
                    
                    CanvasView.InvalidateSurface();
                }
                else if (draggingShapeHandle == 5 && currentEditingShape != null) // Rotation
                {
                    
                    var center = new SKPoint(currentEditingShape.Bounds.MidX, currentEditingShape.Bounds.MidY);
                    float rotDx = imagePoint.X - center.X;
                    float rotDy = imagePoint.Y - center.Y;
                    
                    // Calculate angle from center to touch point
                    // -90 to make top = 0Â° (standard rotation)
                    float angle = (float)(Math.Atan2(rotDy, rotDx) * 180.0 / Math.PI) - 90f;
                    
                    // Normalize angle to 0-360 range
                    while (angle < 0) angle += 360f;
                    while (angle >= 360) angle -= 360f;
                    
                    currentEditingShape.Rotation = angle;
                    
                    CanvasView.InvalidateSurface();
                }
                else // Resizing shape (dragging handle)
                {
                    
                    // Start with initial bounds
                    var rect = new SKRect(
                        initialShapeBounds.Left,
                        initialShapeBounds.Top,
                        initialShapeBounds.Right,
                        initialShapeBounds.Bottom
                    );
                    
                    // Apply delta based on which handle is being dragged (opposite corner stays fixed)
                    // Minimum size: 50x50
                    const float minSize = 50f;
                    
                    switch (draggingShapeHandle.Value)
                    {
                        case 0: // top-left - fix bottom-right (opposite corner)
                            rect.Left = initialShapeBounds.Left + dx;
                            rect.Top = initialShapeBounds.Top + dy;
                            // Ensure minimum size
                            if (rect.Width < minSize)
                            {
                                rect.Left = rect.Right - minSize;
                            }
                            if (rect.Height < minSize)
                            {
                                rect.Top = rect.Bottom - minSize;
                            }
                            break;
                        case 1: // top-right - fix bottom-left (opposite corner)
                            rect.Right = initialShapeBounds.Right + dx;
                            rect.Top = initialShapeBounds.Top + dy;
                            // Ensure minimum size
                            if (rect.Width < minSize)
                            {
                                rect.Right = rect.Left + minSize;
                            }
                            if (rect.Height < minSize)
                            {
                                rect.Top = rect.Bottom - minSize;
                            }
                            break;
                        case 2: // bottom-left - fix top-right (opposite corner)
                            rect.Left = initialShapeBounds.Left + dx;
                            rect.Bottom = initialShapeBounds.Bottom + dy;
                            // Ensure minimum size
                            if (rect.Width < minSize)
                            {
                                rect.Left = rect.Right - minSize;
                            }
                            if (rect.Height < minSize)
                            {
                                rect.Bottom = rect.Top + minSize;
                            }
                            break;
                        case 3: // bottom-right - fix top-left (opposite corner)
                            rect.Right = initialShapeBounds.Right + dx;
                            rect.Bottom = initialShapeBounds.Bottom + dy;
                            // Ensure minimum size
                            if (rect.Width < minSize)
                            {
                                rect.Right = rect.Left + minSize;
                            }
                            if (rect.Height < minSize)
                            {
                                rect.Bottom = rect.Top + minSize;
                            }
                            break;
                    }
                    
                    
                    // Ensure minimum size and valid rect
                    if (rect.Width >= minSize && rect.Height >= minSize)
                    {
                        // Clamp to image bounds - ensure shape stays completely inside image
                        // Keep opposite corner fixed while clamping
                        switch (draggingShapeHandle.Value)
                        {
                            case 0: // top-left - fix bottom-right (opposite corner)
                                // Keep bottom-right fixed at initial position
                                rect.Right = initialShapeBounds.Right;
                                rect.Bottom = initialShapeBounds.Bottom;
                                // Clamp left and top, ensuring minimum size
                                rect.Left = Math.Max(0, Math.Min(rect.Left, rect.Right - minSize));
                                rect.Top = Math.Max(0, Math.Min(rect.Top, rect.Bottom - minSize));
                                break;
                            case 1: // top-right - fix bottom-left (opposite corner)
                                // Keep bottom-left fixed at initial position
                                rect.Left = initialShapeBounds.Left;
                                rect.Bottom = initialShapeBounds.Bottom;
                                // Clamp right and top, ensuring minimum size
                                rect.Right = Math.Min(bitmap.Width, Math.Max(rect.Right, rect.Left + minSize));
                                rect.Top = Math.Max(0, Math.Min(rect.Top, rect.Bottom - minSize));
                                break;
                            case 2: // bottom-left - fix top-right (opposite corner)
                                // Keep top-right fixed at initial position
                                rect.Right = initialShapeBounds.Right;
                                rect.Top = initialShapeBounds.Top;
                                // Clamp left and bottom, ensuring minimum size
                                rect.Left = Math.Max(0, Math.Min(rect.Left, rect.Right - minSize));
                                rect.Bottom = Math.Min(bitmap.Height, Math.Max(rect.Bottom, rect.Top + minSize));
                                break;
                            case 3: // bottom-right - fix top-left (opposite corner)
                                // Keep top-left fixed at initial position
                                rect.Left = initialShapeBounds.Left;
                                rect.Top = initialShapeBounds.Top;
                                // Clamp right and bottom, ensuring minimum size
                                rect.Right = Math.Min(bitmap.Width, Math.Max(rect.Right, rect.Left + minSize));
                                rect.Bottom = Math.Min(bitmap.Height, Math.Max(rect.Bottom, rect.Top + minSize));
                                break;
                        }
                        
                        
                        currentEditingShape.Bounds = rect;
                        CanvasView.InvalidateSurface();
                    }
                    else
                    {
                    }
                }
            }
            else if (isDrawing && viewModel?.SelectedToolType == ToolType.Draw && currentPath != null && viewModel.WorkingBitmap != null)
            {
                
                // Transform to image space for drawing
                var imagePoint = TransformScreenToImage(screenPoint);
                
                // Clamp point to image bounds
                var bitmap = viewModel.WorkingBitmap;
                float originalX = imagePoint.X;
                float originalY = imagePoint.Y;
                imagePoint.X = Math.Max(0, Math.Min(imagePoint.X, bitmap.Width));
                imagePoint.Y = Math.Max(0, Math.Min(imagePoint.Y, bitmap.Height));
                
                if (originalX != imagePoint.X || originalY != imagePoint.Y)
                {
                }
                
                var pointCountBefore = currentPath.PointCount;
                
                // Ensure path has a starting point (should have been set in ACTION_DOWN)
                if (pointCountBefore == 0)
                {
                    currentPath.MoveTo(imagePoint.X, imagePoint.Y);
                    lastPoint = imagePoint;
                    previousPoint = null;
                }
                else
                {
                    // Use cubic Bezier curves for very smooth drawing
                    // Calculate control points based on previous points for smooth, continuous curves
                    float controlX1, controlY1, controlX2, controlY2;
                    
                    if (previousPoint.HasValue)
                    {
                        // We have a previous point, so we can create smooth cubic curves
                        // First control point: extends from lastPoint in the direction of movement
                        float dx1 = lastPoint.X - previousPoint.Value.X;
                        float dy1 = lastPoint.Y - previousPoint.Value.Y;
                        controlX1 = lastPoint.X + dx1 * 0.5f;
                        controlY1 = lastPoint.Y + dy1 * 0.5f;
                        
                        // Second control point: extends from lastPoint toward current point
                        float dx2 = imagePoint.X - lastPoint.X;
                        float dy2 = imagePoint.Y - lastPoint.Y;
                        controlX2 = lastPoint.X + dx2 * 0.5f;
                        controlY2 = lastPoint.Y + dy2 * 0.5f;
                    }
                    else
                    {
                        // First move after down - use simpler control points
                        // Control points are positioned to create a smooth curve from lastPoint to imagePoint
                        float dx = imagePoint.X - lastPoint.X;
                        float dy = imagePoint.Y - lastPoint.Y;
                        controlX1 = lastPoint.X + dx * 0.33f;
                        controlY1 = lastPoint.Y + dy * 0.33f;
                        controlX2 = lastPoint.X + dx * 0.67f;
                        controlY2 = lastPoint.Y + dy * 0.67f;
                    }
                    
                    // Use CubicTo for smooth cubic Bezier curves
                    currentPath.CubicTo(controlX1, controlY1, controlX2, controlY2, imagePoint.X, imagePoint.Y);
                }
                
                var pointCountAfter = currentPath.PointCount;
                
                // Get first point for verification
                if (pointCountAfter > 0)
                {
                    var firstPoint = currentPath.GetPoint(0);
                }
                
                // Update point tracking: previousPoint becomes lastPoint, lastPoint becomes current
                previousPoint = lastPoint;
                lastPoint = imagePoint;
                if (previousPoint.HasValue)
                {
                }
                else
                {
                }
                
                CanvasView.InvalidateSurface();
            }
        }
        private void HandleTouchReleased()
        {
            if (viewModel == null) return;
            
            if (draggingHandle.HasValue)
            {
            }
            
            draggingHandle = null;
            cropStart = null;
            lastTouchPoint = null;
            // Clear initial state for next drag
            initialTouch = SKPoint.Empty;
            initialCropRect = SKRect.Empty;

            // Clear text dragging state
            isDraggingText = false;
            textDragStarted = false;
            initialTextPosition = SKPoint.Empty;
            initialTextTouch = SKPoint.Empty;
            
            // Clear text resize state
            draggingTextHandle = null;
            initialTextSize = 0f;
            initialTextBounds = SKRect.Empty;
            
            // Clear shape dragging state
            if (draggingShapeHandle != null)
            {
                if (currentEditingShape != null)
                {
                }
            }
            isDraggingShape = false;
            shapeDragStarted = false;
            draggingShapeHandle = null;
            initialShapePosition = SKPoint.Empty;
            initialShapeTouch = SKPoint.Empty;
            initialShapeBounds = SKRect.Empty;
            initialRotation = 0f;
            if (isDrawing && currentPath != null && viewModel?.DrawVM != null)
            {
                var pointCount = currentPath.PointCount;
                
                // Only create a stroke if the path has at least one point
                if (pointCount > 0)
                {
                    // Get the first point to verify it's correct
                    var firstPoint = currentPath.GetPoint(0);
                    
                    // Get the last point
                    if (pointCount > 1)
                    {
                        var lastPathPoint = currentPath.GetPoint(pointCount - 1);
                    }
                    
                    // Create a stroke with current color and thickness
                    var drawColor = viewModel.DrawVM.SelectedColor;
                    var skColor = new SKColor(
                        (byte)(drawColor.Red * 255),
                        (byte)(drawColor.Green * 255),
                        (byte)(drawColor.Blue * 255),
                        (byte)(drawColor.Alpha * 255)
                    );
                    
                    
                    // Create a copy of the path for the stroke (finalize it - do not modify afterward)
                    var strokePath = new SKPath(currentPath);
                    var strokePathPointCount = strokePath.PointCount;
                    
                    if (strokePathPointCount > 0)
                    {
                        var copiedFirstPoint = strokePath.GetPoint(0);
                    }
                    
                    var stroke = new Stroke(strokePath, skColor, viewModel.DrawVM.StrokeWidth);
                    
                    // Add to temporary strokes (will be saved to bitmap when user presses check)
                    temporaryStrokes.Add(stroke);
                    
                }
                else
                {
                }
                
                // Clear current path and reset drawing state
                if (currentPath != null)
                {
                    currentPath.Dispose();
                }
                currentPath = null;
                isDrawing = false;
                previousPoint = null; // Reset previous point for next stroke
                lastPoint = SKPoint.Empty;
                
                CanvasView.InvalidateSurface();
            }
        }
        private void DrawOverlaysWithTransform(SKCanvas canvas, SKImageInfo info, float scale, float offsetX, float offsetY, float displayW, float displayH)
        {
            if (viewModel == null) return;
            canvas.Save();
            canvas.Translate(offsetX + displayW * scale / 2f, offsetY + displayH * scale / 2f);
            canvas.RotateDegrees(viewModel.RotationAngle);
            if (viewModel.FlipHorizontal) canvas.Scale(-1, 1);
            if (viewModel.FlipVertical) canvas.Scale(1, -1);
            var bitmap = viewModel.WorkingBitmap;
            
            // Draw saved strokes first (permanently saved, can be undone/redone)
            if (savedStrokes.Count > 0)
            {
                foreach (var stroke in savedStrokes)
                {
                    if (stroke?.Path == null) continue;
                    
                    using (var paint = new SKPaint
                    {
                        Color = stroke.Color,
                        StrokeWidth = stroke.Thickness * scale,
                        Style = SKPaintStyle.Stroke,
                        StrokeCap = SKStrokeCap.Round,
                        StrokeJoin = SKStrokeJoin.Round,
                        IsAntialias = true
                    })
                    {
                        // Paths are stored in image coordinates (0 to bitmap.Width, 0 to bitmap.Height)
                        // Transform to centered coordinates: scale then translate to center
                        var p = new SKPath(stroke.Path);
                        
                        // First scale from image pixels to display pixels
                        p.Transform(SKMatrix.CreateScale(scale, scale));
                        
                        // Then translate from top-left origin to center origin
                        p.Transform(SKMatrix.CreateTranslation(-bitmap.Width * scale / 2f, -bitmap.Height * scale / 2f));
                        
                        canvas.DrawPath(p, paint);
                    }
                }
            }
            
            // Draw temporary strokes (being drawn, not yet saved)
            if (viewModel.DrawVM != null && (temporaryStrokes.Count > 0 || currentPath != null))
            {
                
                // Draw all temporary strokes
                for (int i = 0; i < temporaryStrokes.Count; i++)
                {
                    var stroke = temporaryStrokes[i];
                    
                    // Get first point before transformation
                    if (stroke.Path.PointCount > 0)
                    {
                        var originalFirstPoint = stroke.Path.GetPoint(0);
                    }
                    
                    using (var paint = new SKPaint
                    {
                        Color = stroke.Color,
                        StrokeWidth = stroke.Thickness * scale,
                        Style = SKPaintStyle.Stroke,
                        StrokeCap = SKStrokeCap.Round,
                        StrokeJoin = SKStrokeJoin.Round,
                        IsAntialias = true
                    })
                    {
                        // Paths are stored in image coordinates (0 to bitmap.Width, 0 to bitmap.Height)
                        // Transform to centered coordinates: scale then translate to center
                        var p = new SKPath(stroke.Path);
                        
                        // First scale from image pixels to display pixels
                        p.Transform(SKMatrix.CreateScale(scale, scale));
                        
                        // Then translate from top-left origin to center origin
                        p.Transform(SKMatrix.CreateTranslation(-bitmap.Width * scale / 2f, -bitmap.Height * scale / 2f));
                        
                        // Get first point after transformation
                        if (p.PointCount > 0)
                        {
                            var transformedFirstPoint = p.GetPoint(0);
                        }
                        
                        canvas.DrawPath(p, paint);
                    }
                }
                
                // Draw current path being drawn
                if (currentPath != null && viewModel.DrawVM != null)
                {
                    
                    if (currentPath.PointCount > 0)
                    {
                        var originalFirstPoint = currentPath.GetPoint(0);
                    }
                    
                    var drawColor = viewModel.DrawVM.SelectedColor;
                    var skColor = new SKColor(
                        (byte)(drawColor.Red * 255),
                        (byte)(drawColor.Green * 255),
                        (byte)(drawColor.Blue * 255),
                        (byte)(drawColor.Alpha * 255)
                    );
                    using (var paint = new SKPaint
                    {
                        Color = skColor,
                        StrokeWidth = viewModel.DrawVM.StrokeWidth * scale,
                        Style = SKPaintStyle.Stroke,
                        StrokeCap = SKStrokeCap.Round,
                        StrokeJoin = SKStrokeJoin.Round,
                        IsAntialias = true
                    })
                    {
                        // Paths are stored in image coordinates (0 to bitmap.Width, 0 to bitmap.Height)
                        // Transform to centered coordinates: scale then translate to center
                        var p = new SKPath(currentPath);
                        
                        // First scale from image pixels to display pixels
                        p.Transform(SKMatrix.CreateScale(scale, scale));
                        
                        // Then translate from top-left origin to center origin
                        p.Transform(SKMatrix.CreateTranslation(-bitmap.Width * scale / 2f, -bitmap.Height * scale / 2f));
                        
                        if (p.PointCount > 0)
                        {
                            var transformedFirstPoint = p.GetPoint(0);
                        }
                        
                        canvas.DrawPath(p, paint);
                    }
                }
            }
            // Draw text overlays - draw saved captions first, then current editing overlay
            var allTextOverlays = new List<TextOverlay>();
            
            // Add saved text overlays (finalized captions)
            if (textOverlays != null && textOverlays.Count > 0)
            {
                allTextOverlays.AddRange(textOverlays);
            }
            
            // Add current editing overlay (if exists)
            if (currentEditingTextOverlay != null)
            {
                allTextOverlays.Add(currentEditingTextOverlay);
            }
            
            if (allTextOverlays.Count > 0)
            {

                using (var paint = new SKPaint
                {
                    IsAntialias = true,
                    Style = SKPaintStyle.Fill
                })
                {
                    foreach (var textOverlay in allTextOverlays)
                    {
                        if (textOverlay == null || string.IsNullOrEmpty(textOverlay.Text))
                            continue;

                        // Determine font style based on bold and italic properties
                        SKFontStyleWeight weight = textOverlay.IsBold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal;
                        SKFontStyleWidth width = SKFontStyleWidth.Normal;
                        SKFontStyleSlant slant = textOverlay.IsItalic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright;
                        SKFontStyle fontStyle = new SKFontStyle(weight, width, slant);
                        
                        paint.Typeface = SKTypeface.FromFamilyName("Arial", fontStyle);
                        paint.Color = textOverlay.Color;
                        paint.TextSize = textOverlay.Size * scale;

                        // Transform text position to image coordinate space (center point)
                        float centerX = (textOverlay.X - bitmap.Width / 2f) * scale;
                        float centerY = (textOverlay.Y - bitmap.Height / 2f) * scale;

                        // Calculate available width within image bounds (accounting for padding)
                        float padding = 80 * scale;
                        float maxTextWidth = (bitmap.Width * scale) - (padding * 2);

                        // Wrap text into multiple lines that fit within image bounds
                        var wrappedLines = WrapText(textOverlay.Text, paint, maxTextWidth);

                        // Calculate total text height (all lines)
                        float lineHeight = paint.TextSize * 1.2f; // Line spacing
                        float totalTextHeight = wrappedLines.Count * lineHeight;
                        float firstLineBaseline = paint.TextSize; // First line baseline offset

                        // Calculate max line width first to determine border width
                        float maxLineWidth = 0;
                        foreach (var line in wrappedLines)
                        {
                            if (string.IsNullOrEmpty(line)) continue;
                            var lineBounds = new SKRect();
                            paint.MeasureText(line, ref lineBounds);
                            maxLineWidth = Math.Max(maxLineWidth, lineBounds.Width);
                        }

                        // Calculate border position first (centered)
                        float borderWidth = maxLineWidth + padding * 2;
                        float borderLeft = centerX - borderWidth / 2f;

                        // Ensure border stays within image bounds
                        float imageLeft = -bitmap.Width * scale / 2f;
                        float imageRight = bitmap.Width * scale / 2f;
                        if (borderLeft < imageLeft)
                            borderLeft = imageLeft;
                        if (borderLeft + borderWidth > imageRight)
                            borderLeft = imageRight - borderWidth;

                        // Calculate text area within border (accounting for padding)
                        float textAreaLeft = borderLeft + padding;
                        float textAreaWidth = borderWidth - padding * 2;

                        // Calculate starting Y position (centered vertically)
                        float startY = centerY - (totalTextHeight / 2f) + firstLineBaseline;

                        // Draw fill color background if specified
                        if (textOverlay.FillColor.HasValue)
                        {
                            // Calculate text bounds for background
                            float textTop = startY - firstLineBaseline;
                            float textBottom = textTop + totalTextHeight;
                            float textLeft = borderLeft + padding;
                            float textRight = borderLeft + borderWidth - padding;
                            
                            // Draw rounded rectangle background
                            using (var fillPaint = new SKPaint
                            {
                                Color = textOverlay.FillColor.Value,
                                Style = SKPaintStyle.Fill,
                                IsAntialias = true
                            })
                            {
                                var fillRect = SKRect.Create(
                                    textLeft - 10 * scale, // Add some padding
                                    textTop - 5 * scale,
                                    textRight - textLeft + 20 * scale,
                                    textBottom - textTop + 10 * scale
                                );
                                canvas.DrawRoundRect(fillRect, 8 * scale, 8 * scale, fillPaint);
                            }
                        }

                        // Draw each line of text - align within the border box
                        var firstLineBounds = new SKRect();
                        int lineIndex = 0;
                        foreach (var line in wrappedLines)
                        {
                            if (string.IsNullOrEmpty(line)) continue;

                            // Measure this line
                            var lineBounds = new SKRect();
                            paint.MeasureText(line, ref lineBounds);
                            float lineWidth = lineBounds.Width;

                            if (lineIndex == 0)
                            {
                                firstLineBounds = lineBounds;
                            }

                            // Calculate X position - center within text area (which is centered in border)
                            float lineX = textAreaLeft + (textAreaWidth - lineWidth) / 2f;

                            // Ensure text stays within text area bounds
                            if (lineX < textAreaLeft)
                                lineX = textAreaLeft;
                            if (lineX + lineWidth > textAreaLeft + textAreaWidth)
                                lineX = textAreaLeft + textAreaWidth - lineWidth;

                            // Draw the line
                            canvas.DrawText(line, lineX, startY, paint);
                            startY += lineHeight;
                            lineIndex++;
                        }

                        // Calculate drawY for border positioning
                        float textCenterOffset = (firstLineBounds.Top + firstLineBounds.Bottom) / 2f;
                        float drawY = centerY - textCenterOffset;


                        // Draw dotted border and resize handles if selected OR if this is the current editing overlay
                        // Always show handles for currentEditingTextOverlay to ensure visibility in all scenarios
                        bool shouldShowHandles = textOverlay.IsSelected || (currentEditingTextOverlay != null && textOverlay == currentEditingTextOverlay);
                        
                        
                        if (shouldShowHandles)
                        {
                            float borderTop = drawY + firstLineBounds.Top - padding;
                            float borderHeight = totalTextHeight + padding * 2;

                            float imageTop = -bitmap.Height * scale / 2f;
                            float imageBottom = bitmap.Height * scale / 2f;
                            if (borderTop < imageTop)
                                borderTop = imageTop;
                            if (borderTop + borderHeight > imageBottom)
                                borderTop = imageBottom - borderHeight;

                            var borderRect = SKRect.Create(
                                borderLeft,
                                borderTop,
                                borderWidth,
                                borderHeight
                            );
                            
                            
                            // Draw dotted border
                            using (var borderPaint = new SKPaint
                            {
                                Color = SKColors.White,
                                Style = SKPaintStyle.Stroke,
                                StrokeWidth = 16 * scale,
                                PathEffect = SKPathEffect.CreateDash(new float[] { 10 * scale, 10 * scale }, 0),
                                IsAntialias = true
                            })
                            {
                                canvas.DrawRect(borderRect, borderPaint);
                            }
                            
                            // Draw resize handle only at bottom-right corner for easy resizing
                            // Reduced to half size for better appearance
                            float minHandleSize = 20f; // Minimum handle size in screen pixels (reduced from 40f)
                            float scaledHandleSize = 40f * scale; // Scaled handle size (reduced from 80f)
                            float handleRadius = Math.Max(scaledHandleSize, minHandleSize);
                            
                            // Ensure handle is never too small - add additional safety check
                            if (handleRadius < 12f)
                            {
                                handleRadius = 12f; // Absolute minimum (reduced from 25f)
                            }
                            
                            // Calculate handle position at bottom-right corner of border
                            float handleX = borderRect.Right;
                            float handleY = borderRect.Bottom;
                            
                            
                            // Draw handle with white fill - make it solid and prominent
                            // Add a stroke outline for better visibility
                            using (var handlePaint = new SKPaint
                            {
                                Color = SKColors.White,
                                Style = SKPaintStyle.Fill,
                                IsAntialias = true
                            })
                            {
                                // Draw the bottom-right corner handle as a solid white circle
                                canvas.DrawCircle(handleX, handleY, handleRadius, handlePaint);
                            }
                            
                            // Draw a subtle outline for better visibility
                            using (var outlinePaint = new SKPaint
                            {
                                Color = new SKColor(0, 0, 0, 128), // Semi-transparent black outline
                                Style = SKPaintStyle.Stroke,
                                StrokeWidth = Math.Max(2f * scale, 1f), // Ensure minimum stroke width
                                IsAntialias = true
                            })
                            {
                                canvas.DrawCircle(handleX, handleY, handleRadius, outlinePaint);
                            }
                            
                        }
                        else
                        {
                        }
                    }
                }
            }
            else
            {
            }
            canvas.Restore();
            
            // Draw shapes - draw saved shapes first, then current editing shape
            canvas.Save();
            canvas.Translate(offsetX + displayW * scale / 2f, offsetY + displayH * scale / 2f);
            float angleRad = viewModel.RotationAngle * (float)Math.PI / 180f;
            canvas.RotateDegrees(viewModel.RotationAngle);
            if (viewModel.FlipHorizontal) canvas.Scale(-1, 1);
            if (viewModel.FlipVertical) canvas.Scale(1, -1);
            
            // Draw saved shapes (non-editable)
            foreach (var shape in savedShapes)
            {
                DrawShape(canvas, shape, scale, bitmap, false);
            }
            
            // Draw current editing shape (with selection handles)
            if (currentEditingShape != null)
            {
                DrawShape(canvas, currentEditingShape, scale, bitmap, true);
            }
            
            canvas.Restore();
            
            // Draw crop overlay on top (in screen space) - if crop mode is active and overlay is visible
            if (viewModel.CropVM?.IsCropMode == true &&
                !viewModel.CropVM.CropRect.IsEmpty &&
                viewModel.CropVM.ShowCropOverlay)
            {
                DrawCropOverlay(canvas, info, offsetX, offsetY, scale, displayW, displayH);
            }
            else if (isCropMode && cropRect.HasValue)
            {
                // Legacy crop mode
                using (var paint = new SKPaint
                {
                    Color = SKColors.White,
                    StrokeWidth = 2 * scale,
                    Style = SKPaintStyle.Stroke
                })
                {
                    var cropDisplayRect = new SKRect(
                        offsetX + cropRect.Value.Left * scale,
                        offsetY + cropRect.Value.Top * scale,
                        offsetX + cropRect.Value.Right * scale,
                        offsetY + cropRect.Value.Bottom * scale
                    );
                    canvas.DrawRect(cropDisplayRect, paint);
                }
            }
        }
        /// <summary>
        /// Normalizes stroke width by device density to ensure consistent physical size across devices.
        /// </summary>
        /// <param name="strokeWidthInImagePixels">Stroke width in image pixels</param>
        /// <param name="imageToScreenScale">Scale factor from image to screen</param>
        /// <returns>Normalized stroke width in screen pixels</returns>
        private float NormalizeStrokeWidth(float strokeWidthInImagePixels, float imageToScreenScale)
        {
            float deviceDensity = (float)DeviceDisplay.MainDisplayInfo.Density;
            const float referenceDensity = 2.0f; // Android mdpi reference (typical baseline)
            // Normalize: (strokeWidth * scale) / deviceDensity gives dp, then * referenceDensity gives consistent pixels
            return (strokeWidthInImagePixels * imageToScreenScale * referenceDensity) / deviceDensity;
        }

        private void DrawShape(SKCanvas canvas, ShapeLayer shape, float scale, SKBitmap bitmap, bool isSelected)
        {
            if (shape == null) return;
            
            // Transform shape bounds from image coordinates to centered coordinates
            var bounds = shape.Bounds;
            float centerX = (bounds.Left - bitmap.Width / 2f) * scale;
            float centerY = (bounds.Top - bitmap.Height / 2f) * scale;
            float width = bounds.Width * scale;
            float height = bounds.Height * scale;
            var displayRect = SKRect.Create(centerX, centerY, width, height);
            
            // Calculate shape center in display coordinates
            float shapeCenterX = displayRect.MidX;
            float shapeCenterY = displayRect.MidY;
            
            canvas.Save();
            
            // Apply rotation at center of shape
            canvas.Translate(shapeCenterX, shapeCenterY);
            canvas.RotateDegrees(shape.Rotation);
            canvas.Translate(-shapeCenterX, -shapeCenterY);
            
            // Draw fill if specified
            if (shape.FillColor.HasValue)
            {
                using (var fillPaint = new SKPaint
                {
                    Color = shape.FillColor.Value,
                    Style = SKPaintStyle.Fill,
                    IsAntialias = true
                })
                {
                    DrawShapePath(canvas, displayRect, shape.Type, fillPaint);
                }
            }
            
            // Draw stroke - normalize by device density for consistent physical size
            float normalizedStrokeWidth = NormalizeStrokeWidth(shape.StrokeWidth, scale);
            
            using (var strokePaint = new SKPaint
            {
                Color = shape.StrokeColor,
                StrokeWidth = normalizedStrokeWidth,
                Style = SKPaintStyle.Stroke,
                IsAntialias = true
            })
            {
                DrawShapePath(canvas, displayRect, shape.Type, strokePaint);
            }
            
            canvas.Restore(); // Restore rotation before drawing selection border
            
            // Draw dotted outline and handles if selected (outside rotation)
            if (isSelected)
            {
                // Add padding between shape and dotted border
                float padding = 40f * scale; // Padding in display pixels (reduced by half)
                var borderRect = SKRect.Create(
                    displayRect.Left - padding,
                    displayRect.Top - padding,
                    displayRect.Width + padding * 2,
                    displayRect.Height + padding * 2
                );
                
                // Draw dotted border outside the shape with padding - BIGGER GAPS (Instagram style)
                using (var borderPaint = new SKPaint
                {
                    Color = SKColors.White,
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = 16 * scale, // Thick border line
                    PathEffect = SKPathEffect.CreateDash(new float[] { 24 * scale, 24 * scale }, 0), // Bigger gaps - Instagram style
                    IsAntialias = true
                })
                {
                    // Draw border around shape bounds with padding
                    canvas.DrawRect(borderRect, borderPaint);
                }
                
                // Draw resize handles at corners of the border (outside the dotted line)
                // Slightly bigger visual size for confidence (35f instead of 30f)
                float handleRadius = 35f * scale;
                using (var handlePaint = new SKPaint
                {
                    Color = SKColors.White,
                    Style = SKPaintStyle.Fill,
                    IsAntialias = true
                })
                {
                    // Draw 4 corner handles at the border corners
                    canvas.DrawCircle(borderRect.Left, borderRect.Top, handleRadius, handlePaint);      // top-left
                    canvas.DrawCircle(borderRect.Right, borderRect.Top, handleRadius, handlePaint);     // top-right
                    canvas.DrawCircle(borderRect.Left, borderRect.Bottom, handleRadius, handlePaint);  // bottom-left
                    canvas.DrawCircle(borderRect.Right, borderRect.Bottom, handleRadius, handlePaint);   // bottom-right
                    
                    // ROTATION HANDLE - TOP CENTER (above the border)
                    float rotationHandleY = borderRect.Top - 60f * scale;
                    canvas.DrawCircle(borderRect.MidX, rotationHandleY, handleRadius, handlePaint);
                    
                    // Draw line from shape top to rotation handle
                    using (var linePaint = new SKPaint
                    {
                        Color = SKColors.White,
                        StrokeWidth = 4 * scale,
                        IsAntialias = true
                    })
                    {
                        canvas.DrawLine(borderRect.MidX, borderRect.Top, borderRect.MidX, rotationHandleY, linePaint);
                    }
                }
            }
        }
        
        /// <summary>
        /// Draws a shape directly onto a bitmap in image coordinates (for saving)
        /// </summary>
        private void DrawShapeToBitmap(SKCanvas canvas, ShapeLayer shape, SKBitmap bitmap)
        {
            if (shape == null || bitmap == null) return;
            
            var bounds = shape.Bounds;
            var rect = bounds; // Use bounds directly in image coordinates
            
            canvas.Save();
            
            // Apply rotation at center of shape
            canvas.Translate(rect.MidX, rect.MidY);
            canvas.RotateDegrees(shape.Rotation);
            canvas.Translate(-rect.MidX, -rect.MidY);
            
            // Draw fill if specified
            if (shape.FillColor.HasValue)
            {
                using (var fillPaint = new SKPaint
                {
                    Color = shape.FillColor.Value,
                    Style = SKPaintStyle.Fill,
                    IsAntialias = true
                })
                {
                    DrawShapePath(canvas, rect, shape.Type, fillPaint);
                }
            }
            
            // Draw stroke
            using (var strokePaint = new SKPaint
            {
                Color = shape.StrokeColor,
                StrokeWidth = shape.StrokeWidth, // No scale needed - direct image coordinates
                Style = SKPaintStyle.Stroke,
                IsAntialias = true
            })
            {
                DrawShapePath(canvas, rect, shape.Type, strokePaint);
            }
            
            canvas.Restore();
        }
        
        /// <summary>
        /// Draws a text overlay directly onto a bitmap in image coordinates (for saving) - Caption version
        /// </summary>
        private void DrawTextOverlayToBitmap(SKCanvas canvas, Caption textOverlay, SKBitmap bitmap)
        {
            if (textOverlay == null || bitmap == null || string.IsNullOrEmpty(textOverlay.Text) || textOverlay.Text == "Enter Text") return;
            
            try
            {
                // Get text properties - Caption uses Size, not FontSize
                var text = textOverlay.Text;
                var centerX = textOverlay.X;
                var centerY = textOverlay.Y;
                var color = textOverlay.Color;
                var fillColor = textOverlay.FillColor;
                var fontSize = textOverlay.Size; // Caption uses Size property
                var isBold = textOverlay.IsBold;
                var isItalic = textOverlay.IsItalic;
                
                // Create font style
                var fontStyle = SKFontStyleWeight.Normal;
                if (isBold) fontStyle = SKFontStyleWeight.Bold;
                
                var fontSlant = SKFontStyleSlant.Upright;
                if (isItalic) fontSlant = SKFontStyleSlant.Italic;
                
                using (var typeface = SKTypeface.FromFamilyName("Arial", new SKFontStyle(fontStyle, SKFontStyleWidth.Normal, fontSlant)))
                {
                    // Draw fill color background if specified
                    if (fillColor.HasValue)
                    {
                        // First, measure text to get bounds
                        using (var measurePaint = new SKPaint
                        {
                            TextSize = fontSize,
                            Typeface = typeface
                        })
                        {
                            var maxWidth = bitmap.Width * 0.8f;
                            var wrappedLines = WrapText(text, measurePaint, maxWidth);
                            
                            if (wrappedLines.Count > 0)
                            {
                                var lineHeight = fontSize * 1.2f;
                                var totalHeight = wrappedLines.Count * lineHeight;
                                var textTop = centerY - (totalHeight / 2f);
                                
                                // Calculate text width
                                float maxLineWidth = 0;
                                foreach (var line in wrappedLines)
                                {
                                    var bounds = new SKRect();
                                    measurePaint.MeasureText(line, ref bounds);
                                    maxLineWidth = Math.Max(maxLineWidth, bounds.Width);
                                }
                                
                                // Draw rounded rectangle background
                                using (var fillPaint = new SKPaint
                                {
                                    Color = fillColor.Value,
                                    Style = SKPaintStyle.Fill,
                                    IsAntialias = true
                                })
                                {
                                    var fillRect = SKRect.Create(
                                        centerX - maxLineWidth / 2f - 10,
                                        textTop - 5,
                                        maxLineWidth + 20,
                                        totalHeight + 10
                                    );
                                    canvas.DrawRoundRect(fillRect, 8, 8, fillPaint);
                                }
                            }
                        }
                    }
                    
                    // Create paint for text
                    using (var paint = new SKPaint
                    {
                        Color = color,
                        TextSize = fontSize,
                        Typeface = typeface,
                        IsAntialias = true,
                        TextAlign = SKTextAlign.Center
                    })
                    {
                        // Wrap text if needed
                        var maxWidth = bitmap.Width * 0.8f; // 80% of image width
                        var wrappedLines = WrapText(text, paint, maxWidth);
                        
                        if (wrappedLines.Count == 0) return;
                        
                        // Calculate line height
                        var lineHeight = fontSize * 1.2f;
                        var totalHeight = wrappedLines.Count * lineHeight;
                        var startY = centerY - (totalHeight / 2f) + (lineHeight / 2f);
                        
                        // Draw each line
                        foreach (var line in wrappedLines)
                        {
                            canvas.DrawText(line, centerX, startY, paint);
                            startY += lineHeight;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
            }
        }
        
        /// <summary>
        /// Draws a text overlay directly onto a bitmap in image coordinates (for saving) - TextOverlay version
        /// </summary>
        private void DrawTextOverlayToBitmap(SKCanvas canvas, TextOverlay textOverlay, SKBitmap bitmap)
        {
            if (textOverlay == null || bitmap == null || string.IsNullOrEmpty(textOverlay.Text) || textOverlay.Text == "Enter Text") return;
            
            try
            {
                // Get text properties
                var text = textOverlay.Text;
                var centerX = textOverlay.X;
                var centerY = textOverlay.Y;
                var color = textOverlay.Color;
                var fillColor = textOverlay.FillColor;
                var fontSize = textOverlay.Size;
                var isBold = textOverlay.IsBold;
                var isItalic = textOverlay.IsItalic;
                
                // Create font style
                var fontStyle = SKFontStyleWeight.Normal;
                if (isBold) fontStyle = SKFontStyleWeight.Bold;
                
                var fontSlant = SKFontStyleSlant.Upright;
                if (isItalic) fontSlant = SKFontStyleSlant.Italic;
                
                using (var typeface = SKTypeface.FromFamilyName("Arial", new SKFontStyle(fontStyle, SKFontStyleWidth.Normal, fontSlant)))
                {
                    // Draw fill color background if specified
                    if (fillColor.HasValue)
                    {
                        // First, measure text to get bounds
                        using (var measurePaint = new SKPaint
                        {
                            TextSize = fontSize,
                            Typeface = typeface
                        })
                        {
                            var maxWidth = bitmap.Width * 0.8f;
                            var wrappedLines = WrapText(text, measurePaint, maxWidth);
                            
                            if (wrappedLines.Count > 0)
                            {
                                var lineHeight = fontSize * 1.2f;
                                var totalHeight = wrappedLines.Count * lineHeight;
                                var textTop = centerY - (totalHeight / 2f);
                                
                                // Calculate text width
                                float maxLineWidth = 0;
                                foreach (var line in wrappedLines)
                                {
                                    var bounds = new SKRect();
                                    measurePaint.MeasureText(line, ref bounds);
                                    maxLineWidth = Math.Max(maxLineWidth, bounds.Width);
                                }
                                
                                // Draw rounded rectangle background
                                using (var fillPaint = new SKPaint
                                {
                                    Color = fillColor.Value,
                                    Style = SKPaintStyle.Fill,
                                    IsAntialias = true
                                })
                                {
                                    var fillRect = SKRect.Create(
                                        centerX - maxLineWidth / 2f - 10,
                                        textTop - 5,
                                        maxLineWidth + 20,
                                        totalHeight + 10
                                    );
                                    canvas.DrawRoundRect(fillRect, 8, 8, fillPaint);
                                }
                            }
                        }
                    }
                    
                    // Create paint for text
                    using (var paint = new SKPaint
                    {
                        Color = color,
                        TextSize = fontSize,
                        Typeface = typeface,
                        IsAntialias = true,
                        TextAlign = SKTextAlign.Center
                    })
                    {
                        // Wrap text if needed
                        var maxWidth = bitmap.Width * 0.8f; // 80% of image width
                        var wrappedLines = WrapText(text, paint, maxWidth);
                        
                        if (wrappedLines.Count == 0) return;
                        
                        // Calculate line height
                        var lineHeight = fontSize * 1.2f;
                        var totalHeight = wrappedLines.Count * lineHeight;
                        var startY = centerY - (totalHeight / 2f) + (lineHeight / 2f);
                        
                        // Draw each line
                        foreach (var line in wrappedLines)
                        {
                            canvas.DrawText(line, centerX, startY, paint);
                            startY += lineHeight;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
            }
        }
        
        private void DrawShapePath(SKCanvas canvas, SKRect rect, ShapeType shapeType, SKPaint paint)
        {
            switch (shapeType)
            {
                case ShapeType.Rectangle:
                    canvas.DrawRect(rect, paint);
                    break;
                case ShapeType.Circle:
                    // Draw circle using the smaller dimension
                    float radius = Math.Min(rect.Width, rect.Height) / 2f;
                    canvas.DrawCircle(rect.MidX, rect.MidY, radius, paint);
                    break;
                case ShapeType.Line:
                    // Draw line upwards (from bottom to top)
                    canvas.DrawLine(rect.Left, rect.Bottom, rect.Right, rect.Top, paint);
                    break;
                case ShapeType.SingleArrow:
                    DrawArrow(canvas, rect, paint, false);
                    break;
                case ShapeType.DoubleArrow:
                    DrawArrow(canvas, rect, paint, true);
                    break;
                case ShapeType.DottedLine:
                    using (var dottedPaint = new SKPaint
                    {
                        Color = paint.Color,
                        StrokeWidth = paint.StrokeWidth,
                        Style = paint.Style,
                        IsAntialias = paint.IsAntialias,
                        PathEffect = SKPathEffect.CreateDash(new float[] { 10, 10 }, 0)
                    })
                    {
                        // Draw dotted line upwards (from bottom to top)
                        canvas.DrawLine(rect.Left, rect.Bottom, rect.Right, rect.Top, dottedPaint);
                    }
                    break;
            }
        }
        
        private void DrawArrow(SKCanvas canvas, SKRect rect, SKPaint paint, bool doubleArrow)
        {
            // Calculate arrow direction - draw upwards (from bottom to top)
            float dx = rect.Right - rect.Left;
            float dy = rect.Top - rect.Bottom; // Reversed for upward direction
            float length = (float)Math.Sqrt(dx * dx + dy * dy);
            
            if (length < 1) return;
            
            float unitX = dx / length;
            float unitY = dy / length;
            
            // Arrow head size
            float arrowHeadSize = Math.Min(30, length * 0.3f);
            float arrowHeadAngle = (float)(Math.PI / 6); // 30 degrees
            
            // Start and end points - reversed for upward direction
            SKPoint start = new SKPoint(rect.Left, rect.Bottom); // Start at bottom
            SKPoint end = new SKPoint(rect.Right, rect.Top); // End at top
            
            // Draw line
            canvas.DrawLine(start, end, paint);
            
            // Draw arrow head(s)
            if (doubleArrow)
            {
                // Draw arrow head at start (bottom)
                DrawArrowHead(canvas, start, new SKPoint(-unitX, -unitY), arrowHeadSize, arrowHeadAngle, paint);
            }
            // Draw arrow head at end (top)
            DrawArrowHead(canvas, end, new SKPoint(unitX, unitY), arrowHeadSize, arrowHeadAngle, paint);
        }
        
        private void DrawArrowHead(SKCanvas canvas, SKPoint point, SKPoint direction, float size, float angle, SKPaint paint)
        {
            float perpX = -direction.Y;
            float perpY = direction.X;
            
            SKPoint tip = point;
            SKPoint left = new SKPoint(
                point.X - direction.X * size + perpX * size * (float)Math.Tan(angle),
                point.Y - direction.Y * size + perpY * size * (float)Math.Tan(angle)
            );
            SKPoint right = new SKPoint(
                point.X - direction.X * size - perpX * size * (float)Math.Tan(angle),
                point.Y - direction.Y * size - perpY * size * (float)Math.Tan(angle)
            );
            
            using (var path = new SKPath())
            {
                path.MoveTo(tip);
                path.LineTo(left);
                path.LineTo(right);
                path.Close();
                
                // Draw filled arrow head
                using (var fillPaint = new SKPaint
                {
                    Color = paint.Color,
                    Style = SKPaintStyle.Fill,
                    IsAntialias = paint.IsAntialias
                })
                {
                    canvas.DrawPath(path, fillPaint);
                }
                
                // Draw stroke outline for better visibility
                using (var strokePaint = new SKPaint
                {
                    Color = paint.Color,
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = paint.StrokeWidth,
                    IsAntialias = paint.IsAntialias
                })
                {
                    canvas.DrawPath(path, strokePaint);
                }
            }
        }
        
        private void DrawCropOverlay(SKCanvas canvas, SKImageInfo info, float offsetX, float offsetY, float scale, float displayW, float displayH)
        {
            if (viewModel?.CropVM == null || !viewModel.CropVM.IsCropMode) return;
            if (viewModel.CropVM.CropRect.IsEmpty) return;
            var cropRect = viewModel.CropVM.CropRect;
            var mode = viewModel.CropVM.SelectedCropMode;
            var bitmap = viewModel.WorkingBitmap;
            bool shouldHideGrid = !viewModel.CropVM.ShowGrid;
            
            // Default to rectangle if mode is None
            if (mode == CropMode.None)
            {
                mode = CropMode.Custom;
            }
            // Transform crop rect corners to screen space (same logic as before)
            float screenCenterX = offsetX + displayW * scale / 2f;
            float screenCenterY = offsetY + displayH * scale / 2f;
            SKPoint[] cropCorners = {
                new SKPoint(cropRect.Left, cropRect.Top),
                new SKPoint(cropRect.Right, cropRect.Top),
                new SKPoint(cropRect.Right, cropRect.Bottom),
                new SKPoint(cropRect.Left, cropRect.Bottom)
            };
            SKPoint[] screenCorners = new SKPoint[4];
            float angleRad = viewModel.RotationAngle * (float)Math.PI / 180f;
            float cos = (float)Math.Cos(angleRad);
            float sin = (float)Math.Sin(angleRad);
            for (int i = 0; i < 4; i++)
            {
                float x = (cropCorners[i].X - bitmap.Width / 2f) * scale;
                float y = (cropCorners[i].Y - bitmap.Height / 2f) * scale;
                float rotatedX = x * cos - y * sin;
                float rotatedY = x * sin + y * cos;
                if (viewModel.FlipHorizontal) rotatedX = -rotatedX;
                if (viewModel.FlipVertical) rotatedY = -rotatedY;
                screenCorners[i] = new SKPoint(screenCenterX + rotatedX, screenCenterY + rotatedY);
            }
            float minX = screenCorners.Min(p => p.X);
            float minY = screenCorners.Min(p => p.Y);
            float maxX = screenCorners.Max(p => p.X);
            float maxY = screenCorners.Max(p => p.Y);
            var displayRect = SKRect.Create(minX, minY, maxX - minX, maxY - minY);
            // === 1. Dark overlay outside crop area (4 sides) ===
            using (var overlay = new SKPaint { Color = new SKColor(0, 0, 0, 210) })
            {
                // Top
                canvas.DrawRect(0, 0, info.Width, displayRect.Top, overlay);
                // Bottom
                canvas.DrawRect(0, displayRect.Bottom, info.Width, info.Height - displayRect.Bottom, overlay);
                // Left (excluding top/bottom already covered)
                canvas.DrawRect(0, displayRect.Top, displayRect.Left, displayRect.Height, overlay);
                // Right
                canvas.DrawRect(displayRect.Right, displayRect.Top, info.Width - displayRect.Right, displayRect.Height, overlay);
            }
            // === 2. Draw border (circle, ellipse, or rectangle) ===
            if (mode == CropMode.Circle)
            {
                float radius = Math.Min(displayRect.Width, displayRect.Height) / 2f;
                float innerRadius = radius - 1.5f;
                // Subtle inner square hint (for visual reference)
                float squareSize = Math.Min(displayRect.Width, displayRect.Height);
                float squareLeft = displayRect.MidX - squareSize / 2f;
                float squareTop = displayRect.MidY - squareSize / 2f;
                var squareRect = SKRect.Create(squareLeft, squareTop, squareSize, squareSize);
                using var sqPaint = new SKPaint { Color = SKColors.White.WithAlpha(80), StrokeWidth = 1.5f, Style = SKPaintStyle.Stroke, IsAntialias = true };
                canvas.DrawRect(squareRect, sqPaint);
                // Main circle outline
                using var circlePaint = new SKPaint { Color = SKColors.White, StrokeWidth = 3f, Style = SKPaintStyle.Stroke, IsAntialias = true };
                canvas.DrawCircle(displayRect.MidX, displayRect.MidY, innerRadius, circlePaint);
            }
            else if (mode == CropMode.Eclipse)
            {
                // Draw ellipse border
                using var ellipsePaint = new SKPaint { Color = SKColors.White, StrokeWidth = 3f, Style = SKPaintStyle.Stroke, IsAntialias = true };
                float rx = displayRect.Width / 2f;
                float ry = displayRect.Height / 2f;
                using var ellipsePath = new SKPath();
                ellipsePath.AddOval(new SKRect(displayRect.Left, displayRect.Top, displayRect.Right, displayRect.Bottom));
                canvas.DrawPath(ellipsePath, ellipsePaint);
            }
            else
            {
                using var borderPaint = new SKPaint { Color = SKColors.White, StrokeWidth = 3f, Style = SKPaintStyle.Stroke, IsAntialias = true };
                canvas.DrawRect(displayRect, borderPaint);
            }
            // === 3. Draw Rule-of-Thirds Grid ===
            if (!shouldHideGrid && mode != CropMode.None)
            {
                canvas.Save();
                // Clip to shape for circle or ellipse mode
                if (mode == CropMode.Circle)
                {
                    using var clipPath = new SKPath();
                    clipPath.AddCircle(displayRect.MidX, displayRect.MidY, Math.Min(displayRect.Width, displayRect.Height) / 2f);
                    canvas.ClipPath(clipPath);
                }
                else if (mode == CropMode.Eclipse)
                {
                    using var clipPath = new SKPath();
                    clipPath.AddOval(new SKRect(displayRect.Left, displayRect.Top, displayRect.Right, displayRect.Bottom));
                    canvas.ClipPath(clipPath);
                }
                using var gridPaint = new SKPaint
                {
                    Color = SKColors.White.WithAlpha(170),
                    StrokeWidth = 1.5f,
                    Style = SKPaintStyle.Stroke,
                    IsAntialias = true
                };
                // Grid lines touch the edges
                float thirdW = displayRect.Width / 3f;
                float thirdH = displayRect.Height / 3f;
                // Two vertical lines - from top to bottom edge
                canvas.DrawLine(displayRect.Left + thirdW, displayRect.Top, displayRect.Left + thirdW, displayRect.Bottom, gridPaint);
                canvas.DrawLine(displayRect.Left + 2 * thirdW, displayRect.Top, displayRect.Left + 2 * thirdW, displayRect.Bottom, gridPaint);
                // Two horizontal lines - from left to right edge
                canvas.DrawLine(displayRect.Left, displayRect.Top + thirdH, displayRect.Right, displayRect.Top + thirdH, gridPaint);
                canvas.DrawLine(displayRect.Left, displayRect.Top + 2 * thirdH, displayRect.Right, displayRect.Top + 2 * thirdH, gridPaint);
                canvas.Restore();
            }
            // === 4. Corner / Circle Handles (LARGE like before) ===
            float handleOuter = 34f; // Outer circle size (matches your original)
            float handleInner = 28f; // Inner circle size (matches your original)
            using var outerHandle = new SKPaint { Color = SKColors.Black.WithAlpha(220), IsAntialias = true };
            using var innerHandle = new SKPaint { Color = SKColors.White, IsAntialias = true };
            SKPoint[] handlePositions;
            if (mode == CropMode.Circle)
            {
                float r = Math.Min(displayRect.Width, displayRect.Height) / 2f;
                handlePositions = new[]
                {
                    new SKPoint(displayRect.MidX, displayRect.Top), // Top
                    new SKPoint(displayRect.Right, displayRect.MidY), // Right
                    new SKPoint(displayRect.MidX, displayRect.Bottom), // Bottom
                    new SKPoint(displayRect.Left, displayRect.MidY) // Left
                };
            }
            else if (mode == CropMode.Eclipse)
            {
                // Eclipse uses corner handles like rectangle
                handlePositions = screenCorners;
            }
            else
            {
                // Handles at exact corners (using screenCorners array)
                handlePositions = screenCorners;
            }
            foreach (var pos in handlePositions)
            {
                canvas.DrawCircle(pos, handleOuter / 2f, outerHandle);
                canvas.DrawCircle(pos, handleInner / 2f, innerHandle);
            }
        }
        private void AdjustCropRectWithAspectRatio(ref SKRect rect, int handle, float dx, float dy, float aspectRatio, SKRect initialRect, SKBitmap bitmap)
        {
            // Anchor the opposite corner and maintain aspect ratio
            // Determine which dimension change is larger to use as primary
            float newWidth, newHeight;
            float anchorX, anchorY; // The corner that stays fixed (opposite to dragged handle)
            
            switch (handle)
            {
                case 0: // top-left - anchor bottom-right
                    anchorX = initialRect.Right;
                    anchorY = initialRect.Bottom;
                    // Use larger delta to determine size
                    if (Math.Abs(dx) > Math.Abs(dy))
                    {
                        newWidth = anchorX - (initialRect.Left + dx);
                        newHeight = newWidth / aspectRatio;
                    }
                    else
                    {
                        newHeight = anchorY - (initialRect.Top + dy);
                        newWidth = newHeight * aspectRatio;
                    }
                    rect.Left = anchorX - newWidth;
                    rect.Top = anchorY - newHeight;
                    rect.Right = anchorX;
                    rect.Bottom = anchorY;
                    break;
                    
                case 1: // top-right - anchor bottom-left
                    anchorX = initialRect.Left;
                    anchorY = initialRect.Bottom;
                    if (Math.Abs(dx) > Math.Abs(dy))
                    {
                        newWidth = (initialRect.Right + dx) - anchorX;
                        newHeight = newWidth / aspectRatio;
                    }
                    else
                    {
                        newHeight = anchorY - (initialRect.Top + dy);
                        newWidth = newHeight * aspectRatio;
                    }
                    rect.Left = anchorX;
                    rect.Top = anchorY - newHeight;
                    rect.Right = anchorX + newWidth;
                    rect.Bottom = anchorY;
                    break;
                    
                case 2: // bottom-left - anchor top-right
                    anchorX = initialRect.Right;
                    anchorY = initialRect.Top;
                    if (Math.Abs(dx) > Math.Abs(dy))
                    {
                        newWidth = anchorX - (initialRect.Left + dx);
                        newHeight = newWidth / aspectRatio;
                    }
                    else
                    {
                        newHeight = (initialRect.Bottom + dy) - anchorY;
                        newWidth = newHeight * aspectRatio;
                    }
                    rect.Left = anchorX - newWidth;
                    rect.Top = anchorY;
                    rect.Right = anchorX;
                    rect.Bottom = anchorY + newHeight;
                    break;
                    
                case 3: // bottom-right - anchor top-left
                    anchorX = initialRect.Left;
                    anchorY = initialRect.Top;
                    if (Math.Abs(dx) > Math.Abs(dy))
                    {
                        newWidth = (initialRect.Right + dx) - anchorX;
                        newHeight = newWidth / aspectRatio;
                    }
                    else
                    {
                        newHeight = (initialRect.Bottom + dy) - anchorY;
                        newWidth = newHeight * aspectRatio;
                    }
                    rect.Left = anchorX;
                    rect.Top = anchorY;
                    rect.Right = anchorX + newWidth;
                    rect.Bottom = anchorY + newHeight;
                    break;
                    
                default:
                    return;
            }
            
            // Ensure minimum size
            if (newWidth < 20 || newHeight < 20)
            {
                if (newWidth < 20)
                {
                    newWidth = 20;
                    newHeight = newWidth / aspectRatio;
                }
                else
                {
                    newHeight = 20;
                    newWidth = newHeight * aspectRatio;
                }
                // Re-center around initial center
                float centerX = (initialRect.Left + initialRect.Right) / 2f;
                float centerY = (initialRect.Top + initialRect.Bottom) / 2f;
                rect.Left = centerX - newWidth / 2f;
                rect.Top = centerY - newHeight / 2f;
                rect.Right = centerX + newWidth / 2f;
                rect.Bottom = centerY + newHeight / 2f;
            }
            
            // Clamp to image bounds while maintaining aspect ratio
            // First, check if we need to shrink to fit
            if (rect.Right > bitmap.Width || rect.Bottom > bitmap.Height || rect.Left < 0 || rect.Top < 0)
            {
                // Calculate maximum size that fits with aspect ratio
                float maxWidth = bitmap.Width;
                float maxHeight = bitmap.Height;
                
                // Calculate what size would fit
                float fitWidth = maxWidth;
                float fitHeight = fitWidth / aspectRatio;
                if (fitHeight > maxHeight)
                {
                    fitHeight = maxHeight;
                    fitWidth = fitHeight * aspectRatio;
                }
                
                // Use the smaller of current size or fit size
                if (newWidth > fitWidth || newHeight > fitHeight)
                {
                    newWidth = fitWidth;
                    newHeight = fitHeight;
                }
                
                // Re-center within bounds
                float centerX = Math.Max(newWidth / 2f, Math.Min(bitmap.Width - newWidth / 2f, rect.MidX));
                float centerY = Math.Max(newHeight / 2f, Math.Min(bitmap.Height - newHeight / 2f, rect.MidY));
                
                rect.Left = centerX - newWidth / 2f;
                rect.Top = centerY - newHeight / 2f;
                rect.Right = centerX + newWidth / 2f;
                rect.Bottom = centerY + newHeight / 2f;
            }
        }
        
        // Detect handles in SCREEN space (where they're actually drawn)
        // Corner order in screenCorners: [0]=top-left, [1]=top-right, [2]=bottom-right, [3]=bottom-left
        // Handle indices: 0=top-left, 1=top-right, 2=bottom-left, 3=bottom-right
        private int? GetHandleAtScreenPoint(SKPoint screenPoint, SKPoint[] screenCorners, SKRect screenRect)
        {
            if (screenCorners == null || screenCorners.Length < 4)
            {
                return null;
            }
            if (screenRect.IsEmpty)
            {
                return null;
            }
            
            // Handle size in screen space - matches the drawn handle size (34px outer circle)
            // Use larger touch target for easier interaction (like the reference code with 30px frames)
            // Increased to 80px for much easier touch interaction
            float handleTouchSize = 80f; // Touch target size in screen pixels (radius)
            
            
            // Check each corner handle (in screen space) - prioritize closest handle
            float minDistance = float.MaxValue;
            int? closestHandle = null;
            
            // Map screenCorners array indices to handle indices:
            // screenCorners[0] = top-left -> handle 0
            // screenCorners[1] = top-right -> handle 1
            // screenCorners[2] = bottom-right -> handle 3
            // screenCorners[3] = bottom-left -> handle 2
            int[] handleMap = { 0, 1, 3, 2 };
            
            for (int i = 0; i < 4; i++)
            {
                float dx = screenPoint.X - screenCorners[i].X;
                float dy = screenPoint.Y - screenCorners[i].Y;
                float distance = (float)Math.Sqrt(dx * dx + dy * dy);
                
                
                if (distance < handleTouchSize / 2f && distance < minDistance)
                {
                    minDistance = distance;
                    closestHandle = handleMap[i];
                }
            }
            
            if (closestHandle.HasValue)
            {
                return closestHandle.Value;
            }
            
            // Check if point is inside rect (for moving the whole crop frame)
            // But exclude a border around the edges to avoid conflicts with handles
            float borderMargin = handleTouchSize / 2f;
            SKRect innerRect = SKRect.Create(
                screenRect.Left + borderMargin,
                screenRect.Top + borderMargin,
                screenRect.Width - borderMargin * 2f,
                screenRect.Height - borderMargin * 2f
            );
            
            bool insideRect = innerRect.Contains(screenPoint);
            if (insideRect) return 4;
            
            return null;
        }
        
        private bool HitScreen(float x, float y, SKPoint p, float size)
        {
            // Check if point is within size/2 distance from (x, y) in screen space
            float dx = Math.Abs(p.X - x);
            float dy = Math.Abs(p.Y - y);
            float distance = (float)Math.Sqrt(dx * dx + dy * dy);
            return distance < size / 2f;
        }
        
        // Legacy method for image space (kept for compatibility)
        private int? GetHandleAtPoint(SKPoint p, SKRect r)
        {
            if (r.IsEmpty) return null;
            
            // Handle size in image space - make it larger for easier touch interaction
            float hs = 50; // handle size (increased from 40 for better touch targets)
            
            // Check corners first (handles)
            if (Hit(r.Left, r.Top, p, hs)) return 0;      // top-left
            if (Hit(r.Right, r.Top, p, hs)) return 1;     // top-right
            if (Hit(r.Left, r.Bottom, p, hs)) return 2;    // bottom-left
            if (Hit(r.Right, r.Bottom, p, hs)) return 3;  // bottom-right
            
            // Check if point is inside rect (for moving)
            if (r.Contains(p)) return 4;
            
            return null;
        }
        private bool Hit(float x, float y, SKPoint p, float size)
        {
            // Check if point is within size distance from (x, y)
            float dx = Math.Abs(p.X - x);
            float dy = Math.Abs(p.Y - y);
            return dx < size / 2f && dy < size / 2f;
        }
        private List<string> WrapText(string text, SKPaint paint, float maxWidth)
        {
            var lines = new List<string>();
            if (string.IsNullOrEmpty(text)) return lines;
            // Split by words first
            var words = text.Split(' ');
            var currentLine = new System.Text.StringBuilder();
            foreach (var word in words)
            {
                var testLine = currentLine.Length > 0
                    ? currentLine.ToString() + " " + word
                    : word;
                var bounds = new SKRect();
                paint.MeasureText(testLine, ref bounds);
                if (bounds.Width <= maxWidth)
                {
                    // Word fits on current line
                    if (currentLine.Length > 0)
                        currentLine.Append(" ");
                    currentLine.Append(word);
                }
                else
                {
                    // Word doesn't fit, start new line
                    if (currentLine.Length > 0)
                    {
                        lines.Add(currentLine.ToString());
                        currentLine.Clear();
                    }

                    // Check if word itself is too long, break it
                    if (paint.MeasureText(word, ref bounds) > maxWidth)
                    {
                        // Break long word character by character
                        foreach (var ch in word)
                        {
                            var testChar = currentLine.ToString() + ch;
                            paint.MeasureText(testChar, ref bounds);
                            if (bounds.Width <= maxWidth)
                            {
                                currentLine.Append(ch);
                            }
                            else
                            {
                                if (currentLine.Length > 0)
                                {
                                    lines.Add(currentLine.ToString());
                                    currentLine.Clear();
                                }
                                currentLine.Append(ch);
                            }
                        }
                    }
                    else
                    {
                        currentLine.Append(word);
                    }
                }
            }
            if (currentLine.Length > 0)
            {
                lines.Add(currentLine.ToString());
            }
            return lines;
        }
        private SKColor ConvertColor(Color color)
        {
            return new SKColor(
                (byte)(color.Red * 255),
                (byte)(color.Green * 255),
                (byte)(color.Blue * 255),
                (byte)(color.Alpha * 255)
            );
        }
        private Color ConvertSKColorToColor(SKColor skColor)
        {
            return Color.FromRgba(skColor.Red, skColor.Green, skColor.Blue, skColor.Alpha);
        }
        private TextOverlay? FindTextOverlayAtPoint(SKPoint imagePoint)
        {
            if (viewModel?.WorkingBitmap == null) return null;
            var bitmap = viewModel.WorkingBitmap;
            float scale = 1f; // In image space, scale is 1

            // First check current editing overlay (has priority)
            if (currentEditingTextOverlay != null)
            {
                var bounds = ComputeTextBounds(currentEditingTextOverlay, currentEditingTextOverlay.X, currentEditingTextOverlay.Y);
                if (bounds.Contains(imagePoint))
                {
                    return currentEditingTextOverlay;
                }
            }

            // Then check saved text overlays (read-only, but can be detected for visual feedback)
            foreach (var overlay in textOverlays)
            {
                var bounds = ComputeTextBounds(overlay, overlay.X, overlay.Y);
                if (bounds.Contains(imagePoint))
                {
                    return overlay; // Return it but it won't be editable
                }
            }

            return null;
        }

        /// <summary>
        /// Finds a saved caption at the given point. Returns the Caption object if found.
        /// </summary>
        private Caption? FindSavedCaptionAtPoint(SKPoint imagePoint)
        {
            if (viewModel?.WorkingBitmap == null)
            {
                return null;
            }


            // Check saved captions (reverse order to get topmost first)
            for (int i = savedCaptions.Count - 1; i >= 0; i--)
            {
                var caption = savedCaptions[i];
                // Create a temporary TextOverlay to compute bounds
                var tempOverlay = new TextOverlay
                {
                    Text = caption.Text,
                    X = caption.X,
                    Y = caption.Y,
                    Size = caption.Size,
                    IsBold = caption.IsBold,
                    IsItalic = caption.IsItalic
                };
                var bounds = ComputeTextBounds(tempOverlay, caption.X, caption.Y);
                if (bounds.Contains(imagePoint))
                {
                    return caption;
                }
            }

            return null;
        }

        /// <summary>
        /// Finds a saved shape at the given point. Returns the ShapeLayer object if found.
        /// </summary>
        private ShapeLayer? FindSavedShapeAtPoint(SKPoint imagePoint)
        {
            if (viewModel?.WorkingBitmap == null)
            {
                return null;
            }


            // Check saved shapes (reverse order to get topmost first)
            for (int i = savedShapes.Count - 1; i >= 0; i--)
            {
                var shape = savedShapes[i];
                if (IsPointInShape(imagePoint, shape))
                {
                    return shape;
                }
            }

            return null;
        }
        // Shape helper functions
        private bool IsPointInShape(SKPoint imagePoint, ShapeLayer shape)
        {
            
            // First check if point is near a corner handle - if so, don't consider it "inside shape" for move mode
            var handle = GetShapeHandleAtPoint(imagePoint, shape);
            if (handle.HasValue)
            {
                return false; // Point is on a handle, not inside shape body
            }
            
            // Expanded selection area - easier to select shapes (tap anywhere near it!)
            var bounds = shape.Bounds;
            float padding = 100f; // Extra selection padding around shape
            
            var expanded = SKRect.Inflate(bounds, padding, padding);
            bool hit = expanded.Contains(imagePoint.X, imagePoint.Y);
            
            return hit;
        }

        private int? GetShapeHandleAtPoint(SKPoint imagePoint, ShapeLayer shape)
        {
            if (shape == null) return null;

            var bounds = shape.Bounds;
            float padding = 40f; // Reduced by half
            var borderBounds = SKRect.Create(
                bounds.Left - padding,
                bounds.Top - padding,
                bounds.Width + padding * 2,
                bounds.Height + padding * 2
            );

            // MASSIVE TOUCH TARGETS â€” THIS IS THE KEY!
            float handleTouchRadius = 120f;        // Was 40f â†’ now 3x bigger!
            float rotationHandleTouchRadius = 140f; // Even bigger for rotation

            // Corner handles (4 corners of the border)
            SKPoint[] corners = {
                new SKPoint(borderBounds.Left,  borderBounds.Top),    // 0 top-left
                new SKPoint(borderBounds.Right, borderBounds.Top),    // 1 top-right
                new SKPoint(borderBounds.Left,  borderBounds.Bottom), // 2 bottom-left
                new SKPoint(borderBounds.Right, borderBounds.Bottom)  // 3 bottom-right
            };

            for (int i = 0; i < 4; i++)
            {
                float dx = imagePoint.X - corners[i].X;
                float dy = imagePoint.Y - corners[i].Y;
                if (dx * dx + dy * dy <= handleTouchRadius * handleTouchRadius)
                {
                    return i;
                }
            }

            // Rotation handle â€” top center, above the border
            SKPoint rotHandle = new SKPoint(borderBounds.MidX, borderBounds.Top - 60f);
            float dxRot = imagePoint.X - rotHandle.X;
            float dyRot = imagePoint.Y - rotHandle.Y;

            if (dxRot * dxRot + dyRot * dyRot <= rotationHandleTouchRadius * rotationHandleTouchRadius)
            {
                return 5;
            }

            return null;
        }

        private int? GetTextHandleAtPoint(SKPoint imagePoint, TextOverlay overlay)
        {
            if (overlay == null) return null;
            
            var bounds = ComputeTextBounds(overlay, overlay.X, overlay.Y);
            if (bounds.IsEmpty) return null;
            
            float padding = 80f;
            var borderBounds = SKRect.Create(
                bounds.Left - padding,
                bounds.Top - padding,
                bounds.Width + padding * 2,
                bounds.Height + padding * 2
            );
            
            float handleTouchRadius = 120f;
            
            // Only check bottom-right corner (index 3)
            SKPoint bottomRightCorner = new SKPoint(borderBounds.Right, borderBounds.Bottom);
            float dx = imagePoint.X - bottomRightCorner.X;
            float dy = imagePoint.Y - bottomRightCorner.Y;
            if (dx * dx + dy * dy <= handleTouchRadius * handleTouchRadius)
            {
                return 3; // Return 3 for bottom-right corner
            }
            
            return null;
        }
        
        private SKRect ComputeTextBounds(TextOverlay overlay, float centerX, float centerY)
        {
            if (viewModel?.WorkingBitmap == null) return SKRect.Empty;
            var bitmap = viewModel.WorkingBitmap;
            float scale = 1f; // Image space
            using (var paint = new SKPaint
            {
                IsAntialias = true,
                Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Normal),
                Style = SKPaintStyle.Fill,
                TextSize = overlay.Size * scale
            })
            {
                float padding = 80f;
                float maxTextWidth = bitmap.Width - (padding * 2);
                var wrappedLines = WrapText(overlay.Text, paint, maxTextWidth);
                if (wrappedLines.Count == 0) return SKRect.Empty;
                float lineHeight = paint.TextSize * 1.2f;
                float totalTextHeight = wrappedLines.Count * lineHeight;
                float maxLineWidth = 0;
                var firstLineBounds = new SKRect();
                int lineIndex = 0;
                foreach (var line in wrappedLines)
                {
                    if (string.IsNullOrEmpty(line)) continue;
                    var lineBounds = new SKRect();
                    paint.MeasureText(line, ref lineBounds);
                    maxLineWidth = Math.Max(maxLineWidth, lineBounds.Width);
                    if (lineIndex == 0)
                    {
                        firstLineBounds = lineBounds;
                    }
                    lineIndex++;
                }
                float textCenterOffset = (firstLineBounds.Top + firstLineBounds.Bottom) / 2f;
                float drawY = centerY - textCenterOffset;
                float borderTop = drawY + firstLineBounds.Top - padding;
                float borderLeft = centerX - maxLineWidth / 2f - padding;
                if (borderLeft < padding)
                    borderLeft = padding;
                if (borderLeft + maxLineWidth + padding * 2 > bitmap.Width - padding)
                    borderLeft = bitmap.Width - maxLineWidth - padding * 2;
                float borderHeight = totalTextHeight + padding * 2;
                float borderWidth = maxLineWidth + padding * 2;
                return new SKRect(
                    borderLeft,
                    borderTop,
                    borderLeft + borderWidth,
                    borderTop + borderHeight
                );
            }
        }
        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            viewModel?.ResetImageCommand.Execute(null);
        }
        // Helper classes
        class TextOverlay
        {
            public string Text { get; set; } = string.Empty;
            public float X { get; set; }
            public float Y { get; set; }
            public SKColor Color { get; set; }
            public SKColor? FillColor { get; set; } = null; // Background fill color for text
            public float Size { get; set; }
            public bool IsSelected { get; set; }
            public bool IsBold { get; set; }
            public bool IsItalic { get; set; }
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
    }
}

