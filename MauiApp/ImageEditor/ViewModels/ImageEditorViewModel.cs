using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MauiApp.ImageEditor.Models;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using Microsoft.Maui.Storage;

namespace MauiApp.ImageEditor.ViewModels;

public partial class ImageEditorViewModel : ObservableObject
{
    [ObservableProperty]
    private SKBitmap? originalBitmap;

    [ObservableProperty]
    private SKBitmap? workingBitmap;

    [ObservableProperty]
    private ToolType selectedToolType = ToolType.None;

    [ObservableProperty]
    private bool isToolPanelVisible = false;

    [ObservableProperty]
    private ToolItem? selectedTool;

    [ObservableProperty]
    private double panelTranslationY = 420;

    [ObservableProperty]
    private SubToolType selectedSubToolType = SubToolType.None;

    [ObservableProperty]
    private string panelTitle = "Tool Options";

    // Transformation state for instant preview
    [ObservableProperty]
    private float rotationAngle = 0f;

    [ObservableProperty]
    private bool flipHorizontal = false;

    [ObservableProperty]
    private bool flipVertical = false;

    public bool IsSubPanelVisible => SelectedSubToolType != SubToolType.None;

    public bool CanUndo => actionUndoStack.Count > 0 || undoStack.Count > 1;

    public bool CanRedo => actionRedoStack.Count > 0 || redoStack.Count > 0;

    public bool CanReset => undoStack.Count > 1 || actionUndoStack.Count > 0 || RotationAngle != 0 || FlipHorizontal || FlipVertical || (HasOverlaysCallback?.Invoke() ?? false);

    public bool IsMainToolbarVisible => (CropVM == null || !CropVM.IsCropMode) && (DrawVM == null || !DrawVM.IsDrawMode) && (ShapesVM == null || !ShapesVM.IsShapeMode) && (TextVM == null || !TextVM.IsTextMode);

    private readonly SKCanvasView? canvasView;
    private readonly Stack<SKBitmap> undoStack = new(30);
    private readonly Stack<SKBitmap> redoStack = new(30);
    
    // Action-based undo/redo stacks (for strokes, shapes, text)
    private readonly Stack<IEditorAction> actionUndoStack = new(30);
    private readonly Stack<IEditorAction> actionRedoStack = new(30);
    
    // Callback to apply overlays (shapes, text) to bitmap before saving
    public Func<SKBitmap, SKBitmap?>? ApplyOverlaysCallback { get; set; }
    
    // Callback to check if there are any overlays (shapes, text) that would require reset
    public Func<bool>? HasOverlaysCallback { get; set; }
    
    // Callback to clear all overlays when reset is called
    public Action? ClearOverlaysCallback { get; set; }
    
    // Callback to save captions metadata
    public Func<string, Task>? SaveCaptionsCallback { get; set; }
    
    // Callbacks for applying/removing actions (set by the page)
    public Action<Stroke>? AddStrokeCallback { get; set; }
    public Action<Stroke>? RemoveStrokeCallback { get; set; }
    public Action<ShapeLayer>? AddShapeCallback { get; set; }
    public Action<ShapeLayer>? RemoveShapeCallback { get; set; }
    public Action<Caption>? AddCaptionCallback { get; set; }
    public Action<Caption>? RemoveCaptionCallback { get; set; }
    
    /// <summary>
    /// Notifies that CanReset property may have changed. Call this when overlays are added/removed.
    /// </summary>
    public void NotifyCanResetChanged()
    {
        OnPropertyChanged(nameof(CanReset));
    }

    // Tool Panels Data
    public ObservableCollection<ToolItem> Tools { get; } = new();

    public CropToolViewModel? CropVM { get; private set; }
    public DrawToolViewModel? DrawVM { get; private set; }
    public TextToolViewModel? TextVM { get; private set; }
    public ShapesToolViewModel? ShapesVM { get; private set; }

    public ImageEditorViewModel(string imagePath, SKCanvasView canvasView)
    {
        this.canvasView = canvasView;
        // Don't load image in constructor - load it asynchronously
        SetupTools();
        
        CropVM = new CropToolViewModel(this);
        DrawVM = new DrawToolViewModel(this);
        TextVM = new TextToolViewModel(this);
        ShapesVM = new ShapesToolViewModel(this);
        
        // Load image asynchronously
        _ = LoadImageAsync(imagePath);
        
        // Subscribe to property changes for toolbar visibility
        if (CropVM != null)
        {
            CropVM.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(CropToolViewModel.IsCropMode))
                {
                    OnPropertyChanged(nameof(IsMainToolbarVisible));
                }
            };
        }
        
        if (DrawVM != null)
        {
            DrawVM.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(DrawToolViewModel.IsDrawMode))
                {
                    OnPropertyChanged(nameof(IsMainToolbarVisible));
                }
            };
        }
        
        if (ShapesVM != null)
        {
            ShapesVM.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(ShapesToolViewModel.IsShapeMode))
                {
                    OnPropertyChanged(nameof(IsMainToolbarVisible));
                }
            };
        }
        
        if (TextVM != null)
        {
            TextVM.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(TextToolViewModel.IsTextMode))
                {
                    OnPropertyChanged(nameof(IsMainToolbarVisible));
                }
            };
        }
    }

    private async Task LoadImageAsync(string path)
    {
        try
        {
            // Load image on background thread to avoid blocking UI
            SKBitmap correctedBitmap = await Task.Run(() =>
            {
                // Step 1: Decode bitmap (raw pixels, no auto-rotation)
                using var sourceBitmap = SKBitmap.Decode(path);
                if (sourceBitmap == null)
                {
                    return null;
                }

                // Step 2: Read EXIF orientation from the file
                int orientation = GetExifOrientation(path);

                // Step 3: Apply correct rotation/flip based on EXIF
                return ApplyExifOrientation(sourceBitmap, orientation);
            });

            if (correctedBitmap == null)
            {
                return;
            }

            // Step 4: Assign to Original & Working on UI thread
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                OriginalBitmap = correctedBitmap;
                WorkingBitmap = new SKBitmap(correctedBitmap.Info);
                correctedBitmap.CopyTo(WorkingBitmap);

                SaveToUndo();
                
                // Ensure CanReset is notified after initial load
                OnPropertyChanged(nameof(CanReset));

                canvasView?.InvalidateSurface();
            });
        }
        catch (Exception ex)
        {
        }
    }

    private void LoadImage(string path)
    {
        // Synchronous version for backward compatibility
        _ = LoadImageAsync(path);
    }

    /// <summary>
    /// Replaces the current image with a new one from the specified path.
    /// Resets all transformations and clears undo/redo stacks.
    /// </summary>
    public void ReplaceImage(string path)
    {
        // Dispose old bitmaps
        OriginalBitmap?.Dispose();
        WorkingBitmap?.Dispose();
        
        // Clear undo/redo stacks
        while (undoStack.Count > 0)
        {
            undoStack.Pop()?.Dispose();
        }
        while (redoStack.Count > 0)
        {
            redoStack.Pop()?.Dispose();
        }
        
        // Reset transformations
        RotationAngle = 0f;
        FlipHorizontal = false;
        FlipVertical = false;
        
        // Close any active tool modes
        SelectedToolType = ToolType.None;
        SelectedSubToolType = SubToolType.None;
        IsToolPanelVisible = false;
        PanelTranslationY = 420;
        
        // Close tool-specific modes by setting their IsMode properties to false
        if (CropVM != null)
        {
            CropVM.IsCropMode = false;
        }
        if (DrawVM != null)
        {
            DrawVM.IsDrawMode = false;
        }
        if (ShapesVM != null)
        {
            ShapesVM.IsShapeMode = false;
        }
        if (TextVM != null)
        {
            TextVM.IsTextMode = false;
        }
        
        // Load new image
        LoadImage(path);
        
        // Notify property changes
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        OnPropertyChanged(nameof(CanReset));
        OnPropertyChanged(nameof(IsMainToolbarVisible));
    }

    // ──────────────────────────────────────────────────
    // 2. Apply EXIF rotation correctly
    // ──────────────────────────────────────────────────
    private SKBitmap ApplyExifOrientation(SKBitmap bitmap, int orientation)
    {
        SKBitmap rotatedBitmap;
        switch (orientation)
        {
            case 2: // Flip horizontal
                rotatedBitmap = new SKBitmap(bitmap.Width, bitmap.Height);
                using (var canvas = new SKCanvas(rotatedBitmap))
                {
                    canvas.Scale(-1, 1);
                    canvas.Translate(-bitmap.Width, 0);
                    canvas.DrawBitmap(bitmap, 0, 0);
                }
                return rotatedBitmap;

            case 3: // Rotate 180
                rotatedBitmap = new SKBitmap(bitmap.Width, bitmap.Height);
                using (var canvas = new SKCanvas(rotatedBitmap))
                {
                    canvas.RotateDegrees(180);
                    canvas.Translate(-bitmap.Width, -bitmap.Height);
                    canvas.DrawBitmap(bitmap, 0, 0);
                }
                return rotatedBitmap;

            case 4: // Flip vertical
                rotatedBitmap = new SKBitmap(bitmap.Width, bitmap.Height);
                using (var canvas = new SKCanvas(rotatedBitmap))
                {
                    canvas.Scale(1, -1);
                    canvas.Translate(0, -bitmap.Height);
                    canvas.DrawBitmap(bitmap, 0, 0);
                }
                return rotatedBitmap;

            case 5: // Flip horizontal + rotate 90 CW → transpose
            case 6: // Rotate 90 CW ← MOST COMMON FOR PORTRAIT CAMERA PHOTOS
                rotatedBitmap = new SKBitmap(bitmap.Height, bitmap.Width); // Swap dimensions!
                using (var canvas = new SKCanvas(rotatedBitmap))
                {
                    canvas.Translate(bitmap.Height, 0);
                    canvas.RotateDegrees(90);
                    if (orientation == 5) canvas.Scale(-1, 1); // Extra flip for case 5
                    canvas.DrawBitmap(bitmap, 0, 0);
                }
                return rotatedBitmap;

            case 7: // Flip vertical + rotate 90 CW
                rotatedBitmap = new SKBitmap(bitmap.Height, bitmap.Width);
                using (var canvas = new SKCanvas(rotatedBitmap))
                {
                    canvas.Translate(bitmap.Height, 0);
                    canvas.RotateDegrees(90);
                    canvas.Scale(1, -1);
                    canvas.DrawBitmap(bitmap, 0, 0);
                }
                return rotatedBitmap;

            case 8: // Rotate 270 CW (or -90)
                rotatedBitmap = new SKBitmap(bitmap.Height, bitmap.Width);
                using (var canvas = new SKCanvas(rotatedBitmap))
                {
                    canvas.Translate(0, bitmap.Width);
                    canvas.RotateDegrees(270);
                    canvas.DrawBitmap(bitmap, 0, 0);
                }
                return rotatedBitmap;

            default: // 1 or unknown
                var copy = new SKBitmap(bitmap.Info);
                bitmap.CopyTo(copy);
                return copy;
        }
    }

    // ──────────────────────────────────────────────────
    // 1. Get EXIF Orientation Tag
    // ──────────────────────────────────────────────────
    private int GetExifOrientation(string path)
    {
        try
        {
            var directories = ImageMetadataReader.ReadMetadata(path);
            var exif = directories.OfType<ExifIfd0Directory>().FirstOrDefault();
            if (exif != null && exif.TryGetInt32(ExifIfd0Directory.TagOrientation, out int orientation))
            {
                return orientation; // Returns standard 1–8 values
            }
        }
        catch { /* ignore */ }

        return 1; // Default = no rotation
    }


    private void SetupTools()
    {
        Tools.Add(new ToolItem("Crop", "ic_crop_edit.svg", ToolType.Crop));
        Tools.Add(new ToolItem("Shapes", "ic_shapes_edit.svg", ToolType.Shapes));
        Tools.Add(new ToolItem("Draw", "ic_brush_edit.svg", ToolType.Draw));
        Tools.Add(new ToolItem("Text", "ic_text_fields_edit.svg", ToolType.Text));
    }

    public void SaveToUndo()
    {
        if (WorkingBitmap == null) return;

        if (undoStack.Count >= 30)
        {
            undoStack.Pop()?.Dispose();
        }

        var copy = new SKBitmap(WorkingBitmap.Info);
        WorkingBitmap.CopyTo(copy);
        undoStack.Push(copy);

        redoStack.Clear();
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        OnPropertyChanged(nameof(CanReset));
    }

    /// <summary>
    /// Registers an action for undo/redo tracking
    /// </summary>
    public void RegisterAction(IEditorAction action)
    {
        if (action == null) return;
        
        // Dispose paths in redo stack when a new action is registered (redo is no longer possible)
        while (actionRedoStack.Count > 0)
        {
            var redoAction = actionRedoStack.Pop();
            if (redoAction is AddStrokeAction addStrokeAction && addStrokeAction.Stroke?.Path != null)
            {
                addStrokeAction.Stroke.Path.Dispose();
            }
            else if (redoAction is DeleteStrokeAction deleteStrokeAction && deleteStrokeAction.Stroke?.Path != null)
            {
                deleteStrokeAction.Stroke.Path.Dispose();
            }
        }
        
        if (actionUndoStack.Count >= 30)
        {
            var oldAction = actionUndoStack.Pop();
            // Dispose old action's resources if it's a stroke action
            if (oldAction is AddStrokeAction oldAddStrokeAction && oldAddStrokeAction.Stroke?.Path != null)
            {
                oldAddStrokeAction.Stroke.Path.Dispose();
            }
            else if (oldAction is DeleteStrokeAction oldDeleteStrokeAction && oldDeleteStrokeAction.Stroke?.Path != null)
            {
                oldDeleteStrokeAction.Stroke.Path.Dispose();
            }
        }
        
        actionUndoStack.Push(action);
        
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        OnPropertyChanged(nameof(CanReset));
        
        // Notify commands that CanExecute may have changed
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanUndo))]
    public void Undo()
    {
        try
        {
            // First try action-based undo (for strokes, shapes, text)
            if (actionUndoStack.Count > 0)
            {
                var action = actionUndoStack.Pop();
                actionRedoStack.Push(action);
                
                ApplyUndoAction(action);
                
                OnPropertyChanged(nameof(CanUndo));
                OnPropertyChanged(nameof(CanRedo));
                OnPropertyChanged(nameof(CanReset));
                
                // Notify commands that CanExecute may have changed
                UndoCommand.NotifyCanExecuteChanged();
                RedoCommand.NotifyCanExecuteChanged();
                
                canvasView?.InvalidateSurface();
                return;
            }
            
            // Fall back to bitmap-based undo (for crop, etc.)
            if (undoStack.Count > 1 && WorkingBitmap != null)
            {
                redoStack.Push(WorkingBitmap);
                undoStack.Pop()?.Dispose();
                
                var previous = undoStack.Peek();
                WorkingBitmap?.Dispose();
                WorkingBitmap = new SKBitmap(previous.Info);
                previous.CopyTo(WorkingBitmap);
                
                OnPropertyChanged(nameof(CanUndo));
                OnPropertyChanged(nameof(CanRedo));
                OnPropertyChanged(nameof(CanReset));
                
                // Notify commands that CanExecute may have changed
                UndoCommand.NotifyCanExecuteChanged();
                RedoCommand.NotifyCanExecuteChanged();
                
                canvasView?.InvalidateSurface();
            }
        }
        catch (Exception ex)
        {
            // Log error but don't crash
            System.Diagnostics.Debug.WriteLine($"Undo error: {ex.Message}");
        }
    }

    [RelayCommand(CanExecute = nameof(CanRedo))]
    public void Redo()
    {
        try
        {
            // First try action-based redo (for strokes, shapes, text)
            if (actionRedoStack.Count > 0)
            {
                var action = actionRedoStack.Pop();
                actionUndoStack.Push(action);
                
                ApplyRedoAction(action);
                
                OnPropertyChanged(nameof(CanUndo));
                OnPropertyChanged(nameof(CanRedo));
                OnPropertyChanged(nameof(CanReset));
                
                // Notify commands that CanExecute may have changed
                UndoCommand.NotifyCanExecuteChanged();
                RedoCommand.NotifyCanExecuteChanged();
                
                canvasView?.InvalidateSurface();
                return;
            }
            
            // Fall back to bitmap-based redo (for crop, etc.)
            if (redoStack.TryPop(out var bmp) && WorkingBitmap != null)
            {
                undoStack.Push(WorkingBitmap);
                
                WorkingBitmap?.Dispose();
                WorkingBitmap = new SKBitmap(bmp.Info);
                bmp.CopyTo(WorkingBitmap);
                
                OnPropertyChanged(nameof(CanUndo));
                OnPropertyChanged(nameof(CanRedo));
                OnPropertyChanged(nameof(CanReset));
                
                // Notify commands that CanExecute may have changed
                UndoCommand.NotifyCanExecuteChanged();
                RedoCommand.NotifyCanExecuteChanged();
                
                canvasView?.InvalidateSurface();
            }
        }
        catch (Exception ex)
        {
            // Log error but don't crash
            System.Diagnostics.Debug.WriteLine($"Redo error: {ex.Message}");
        }
    }
    
    private void ApplyUndoAction(IEditorAction action)
    {
        switch (action.ActionType)
        {
            case EditorActionType.AddStroke:
                if (action is AddStrokeAction addStrokeAction)
                {
                    RemoveStrokeCallback?.Invoke(addStrokeAction.Stroke);
                }
                break;
                
            case EditorActionType.AddShape:
                if (action is AddShapeAction addShapeAction)
                {
                    RemoveShapeCallback?.Invoke(addShapeAction.Shape);
                }
                break;
                
            case EditorActionType.AddCaption:
                if (action is AddCaptionAction addCaptionAction)
                {
                    RemoveCaptionCallback?.Invoke(addCaptionAction.Caption);
                }
                break;
                
            case EditorActionType.Crop:
                if (action is CropAction cropAction && cropAction.BeforeBitmap != null && WorkingBitmap != null)
                {
                    WorkingBitmap?.Dispose();
                    WorkingBitmap = new SKBitmap(cropAction.BeforeBitmap.Info);
                    cropAction.BeforeBitmap.CopyTo(WorkingBitmap);
                }
                break;
                
            case EditorActionType.Transform:
                if (action is TransformAction transformAction)
                {
                    RotationAngle = transformAction.OldRotation;
                    FlipHorizontal = transformAction.OldFlipHorizontal;
                    FlipVertical = transformAction.OldFlipVertical;
                }
                break;
                
            case EditorActionType.DeleteStroke:
                // Undo delete = add back
                if (action is DeleteStrokeAction deleteStrokeAction)
                {
                    AddStrokeCallback?.Invoke(deleteStrokeAction.Stroke);
                }
                break;
                
            case EditorActionType.DeleteShape:
                // Undo delete = add back
                if (action is DeleteShapeAction deleteShapeAction)
                {
                    AddShapeCallback?.Invoke(deleteShapeAction.Shape);
                }
                break;
                
            case EditorActionType.DeleteCaption:
                // Undo delete = add back
                if (action is DeleteCaptionAction deleteCaptionAction)
                {
                    AddCaptionCallback?.Invoke(deleteCaptionAction.Caption);
                }
                break;
        }
    }
    
    private void ApplyRedoAction(IEditorAction action)
    {
        switch (action.ActionType)
        {
            case EditorActionType.AddStroke:
                if (action is AddStrokeAction addStrokeAction)
                {
                    AddStrokeCallback?.Invoke(addStrokeAction.Stroke);
                }
                break;
                
            case EditorActionType.AddShape:
                if (action is AddShapeAction addShapeAction)
                {
                    AddShapeCallback?.Invoke(addShapeAction.Shape);
                }
                break;
                
            case EditorActionType.AddCaption:
                if (action is AddCaptionAction addCaptionAction)
                {
                    AddCaptionCallback?.Invoke(addCaptionAction.Caption);
                }
                break;
                
            case EditorActionType.Crop:
                if (action is CropAction cropAction && cropAction.AfterBitmap != null && WorkingBitmap != null)
                {
                    WorkingBitmap?.Dispose();
                    WorkingBitmap = new SKBitmap(cropAction.AfterBitmap.Info);
                    cropAction.AfterBitmap.CopyTo(WorkingBitmap);
                }
                break;
                
            case EditorActionType.Transform:
                if (action is TransformAction transformAction)
                {
                    RotationAngle = transformAction.NewRotation;
                    FlipHorizontal = transformAction.NewFlipHorizontal;
                    FlipVertical = transformAction.NewFlipVertical;
                }
                break;
                
            case EditorActionType.DeleteStroke:
                // Redo delete = remove again
                if (action is DeleteStrokeAction deleteStrokeAction)
                {
                    RemoveStrokeCallback?.Invoke(deleteStrokeAction.Stroke);
                }
                break;
                
            case EditorActionType.DeleteShape:
                // Redo delete = remove again
                if (action is DeleteShapeAction deleteShapeAction)
                {
                    RemoveShapeCallback?.Invoke(deleteShapeAction.Shape);
                }
                break;
                
            case EditorActionType.DeleteCaption:
                // Redo delete = remove again
                if (action is DeleteCaptionAction deleteCaptionAction)
                {
                    RemoveCaptionCallback?.Invoke(deleteCaptionAction.Caption);
                }
                break;
        }
    }

    [RelayCommand]
    public void SelectTool(ToolItem? tool)
    {
        if (tool == null) return;

        SelectedTool = tool;
        SelectedToolType = tool.Type;
        
        // Crop tool doesn't use sliding panel - it replaces bottom toolbar
        if (tool.Type == ToolType.Crop)
        {
            IsToolPanelVisible = false;
            PanelTranslationY = 420;
            SelectedSubToolType = SubToolType.None;
            if (CropVM != null)
            {
                CropVM.StartCropMode();
            }
            return;
        }
        
        // Shapes tool doesn't use sliding panel - it replaces bottom toolbar
        if (tool.Type == ToolType.Shapes)
        {
            IsToolPanelVisible = false;
            PanelTranslationY = 420;
            SelectedSubToolType = SubToolType.None;
            if (ShapesVM != null)
            {
                ShapesVM.StartShapeMode();
            }
            return;
        }
        
        // Text tool doesn't use sliding panel - it replaces bottom toolbar
        if (tool.Type == ToolType.Text)
        {
            IsToolPanelVisible = false;
            PanelTranslationY = 420;
            SelectedSubToolType = SubToolType.None;
            if (TextVM != null)
            {
                TextVM.StartTextMode();
            }
            return;
        }
        
        // Other tools use sliding panel
        IsToolPanelVisible = true;
        PanelTranslationY = 0; // Slide panel up
        
        // Set sub-panel based on tool type
        SelectedSubToolType = SubToolType.None;
        
        PanelTitle = SelectedSubToolType != SubToolType.None 
            ? $"{tool.Name} Options" 
            : "Tool Options";
    }

    [RelayCommand]
    public void SelectToolByName(string? toolName)
    {
        if (string.IsNullOrEmpty(toolName)) return;
        
        // Handle Crop tool specially - it replaces bottom toolbar
        if (toolName == "Crop")
        {
            SelectedToolType = ToolType.Crop;
            IsToolPanelVisible = false;
            PanelTranslationY = 420;
            SelectedSubToolType = SubToolType.None;
            if (CropVM != null)
            {
                CropVM.StartCropMode();
            }
            return;
        }
        
        // Handle Shapes tool specially - it replaces bottom toolbar
        if (toolName == "Shapes")
        {
            SelectedToolType = ToolType.Shapes;
            IsToolPanelVisible = false;
            PanelTranslationY = 420;
            SelectedSubToolType = SubToolType.None;
            if (ShapesVM != null)
            {
                ShapesVM.StartShapeMode();
            }
            return;
        }
        
        // Handle Text tool specially - it replaces bottom toolbar
        if (toolName == "Text")
        {
            SelectedToolType = ToolType.Text;
            IsToolPanelVisible = false;
            PanelTranslationY = 420;
            SelectedSubToolType = SubToolType.None;
            if (TextVM != null)
            {
                TextVM.StartTextMode();
            }
            return;
        }
        
        var tool = Tools.FirstOrDefault(t => t.Type.ToString() == toolName);
        if (tool != null)
        {
            SelectTool(tool);
        }
    }

    [RelayCommand]
    public void CloseToolPanel()
    {
        IsToolPanelVisible = false;
        SelectedToolType = ToolType.None;
        SelectedSubToolType = SubToolType.None;
        PanelTitle = "Tool Options";
        PanelTranslationY = 420; // Slide panel down
    }
    
    public void CloseSubPanel()
    {
        SelectedSubToolType = SubToolType.None;
        PanelTitle = "Tool Options";
    }

    public void ApplyBitmapChanges(SKBitmap newBitmap)
    {
        if (newBitmap == null) return;

        WorkingBitmap?.Dispose();
        WorkingBitmap = newBitmap;
        SaveToUndo();
        canvasView?.InvalidateSurface();
    }

    public event EventHandler<string>? ImageSaved;

    [RelayCommand]
    public async Task SaveImage()
    {
        if (WorkingBitmap == null) return;

        try
        {
            // Apply any pending transforms before saving
            SKBitmap bitmapToSave = WorkingBitmap;
            bool needsDispose = false;
            
            if (RotationAngle != 0 || FlipHorizontal || FlipVertical)
            {
                var transformed = ApplyCurrentTransformToBitmap();
                if (transformed != null)
                {
                    bitmapToSave = transformed;
                    needsDispose = true;
                }
            }

            // Apply overlays (shapes, text) to the bitmap
            if (ApplyOverlaysCallback != null)
            {
                var withOverlays = ApplyOverlaysCallback(bitmapToSave);
                if (withOverlays != null)
                {
                    if (needsDispose)
                    {
                        bitmapToSave.Dispose();
                    }
                    bitmapToSave = withOverlays;
                    needsDispose = true;
                }
            }

            using var image = SKImage.FromBitmap(bitmapToSave);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            
            if (needsDispose)
            {
                bitmapToSave.Dispose();
            }
            
            var fileName = $"skia_edited_{DateTime.Now:yyyyMMdd_HHmmss}.png";
            
            // Save to cache directory for now (will be passed to comment page)
            var cachePath = FileSystem.CacheDirectory;
            var filePath = Path.Combine(cachePath, fileName);
            using (var stream = File.OpenWrite(filePath))
            {
                data.SaveTo(stream);
            }

            // Save caption metadata if callback is set
            if (SaveCaptionsCallback != null)
            {
                await SaveCaptionsCallback(filePath);
            }

            // Fire event for external handling - this will be handled by the page to navigate to comment page
            ImageSaved?.Invoke(this, filePath);
        }
        catch (Exception ex)
        {
            await Application.Current!.MainPage!.DisplayAlert("Error", $"Failed to save: {ex.Message}", "OK");
        }
    }

    /// <summary>
    /// Gets the edited image as a byte array
    /// </summary>
    public byte[]? GetEditedImageAsBytes()
    {
        if (WorkingBitmap == null) return null;

        try
        {
            SKBitmap bitmapToSave = WorkingBitmap;
            bool needsDispose = false;
            
            if (RotationAngle != 0 || FlipHorizontal || FlipVertical)
            {
                var transformed = ApplyCurrentTransformToBitmap();
                if (transformed != null)
                {
                    bitmapToSave = transformed;
                    needsDispose = true;
                }
            }

            if (ApplyOverlaysCallback != null)
            {
                var withOverlays = ApplyOverlaysCallback(bitmapToSave);
                if (withOverlays != null)
                {
                    if (needsDispose) bitmapToSave.Dispose();
                    bitmapToSave = withOverlays;
                    needsDispose = true;
                }
            }

            using var image = SKImage.FromBitmap(bitmapToSave);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            
            if (needsDispose) bitmapToSave.Dispose();
            
            return data.ToArray();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Gets the edited image as a stream
    /// </summary>
    public Stream? GetEditedImageAsStream()
    {
        var bytes = GetEditedImageAsBytes();
        return bytes != null ? new MemoryStream(bytes) : null;
    }

    [RelayCommand]
    public void ResetImage()
    {
        if (OriginalBitmap == null) return;

        WorkingBitmap?.Dispose();
        WorkingBitmap = new SKBitmap(OriginalBitmap.Info);
        OriginalBitmap.CopyTo(WorkingBitmap);
        
        // Reset transformations
        RotationAngle = 0f;
        FlipHorizontal = false;
        FlipVertical = false;
        
        undoStack.Clear();
        redoStack.Clear();
        actionUndoStack.Clear();
        actionRedoStack.Clear();
        SaveToUndo();
        
        // Clear overlays (shapes, text, drawings) via callback
        ClearOverlaysCallback?.Invoke();
        
        // Notify CanReset changed (will be false after reset)
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        OnPropertyChanged(nameof(CanReset));
        
        // Notify commands that CanExecute may have changed
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
        
        canvasView?.InvalidateSurface();
    }

    [RelayCommand]
    public void ApplyCrop(SKRect cropRect)
    {
        if (WorkingBitmap == null) return;

        try
        {
            // First, apply any pending transforms to the bitmap before cropping
            // This ensures the crop is applied to the transformed image
            if (RotationAngle != 0 || FlipHorizontal || FlipVertical)
            {
                var transformed = ApplyCurrentTransformToBitmap();
                if (transformed != null)
                {
                    WorkingBitmap?.Dispose();
                    WorkingBitmap = transformed;
                    // Reset transforms after applying
                    RotationAngle = 0;
                    FlipHorizontal = false;
                    FlipVertical = false;
                }
            }

            var rect = SKRectI.Round(cropRect);
            rect.Left = Math.Max(0, Math.Min(rect.Left, WorkingBitmap.Width));
            rect.Top = Math.Max(0, Math.Min(rect.Top, WorkingBitmap.Height));
            rect.Right = Math.Max(rect.Left, Math.Min(rect.Right, WorkingBitmap.Width));
            rect.Bottom = Math.Max(rect.Top, Math.Min(rect.Bottom, WorkingBitmap.Height));

            if (rect.Width > 0 && rect.Height > 0)
            {
                var before = new SKBitmap(WorkingBitmap.Info);
                WorkingBitmap.CopyTo(before);
                SaveToUndo();

                var cropped = new SKBitmap(rect.Width, rect.Height);
                if (WorkingBitmap.ExtractSubset(cropped, rect))
                {
                    WorkingBitmap?.Dispose();
                    WorkingBitmap = cropped;
                    canvasView?.InvalidateSurface();
                }
                else
                {
                    cropped.Dispose();
                }
            }
        }
        catch (Exception ex)
        {
        }
    }

    /// <summary>
    /// Applies current transformations (rotation, flips) to the bitmap and resets transform state.
    /// Call this when user wants to "bake" the transforms into the image (e.g., before export).
    /// </summary>
    [RelayCommand]
    public void ApplyTransform()
    {
        if (WorkingBitmap == null || (RotationAngle == 0 && !FlipHorizontal && !FlipVertical))
            return; // No transforms to apply

        var transformed = ApplyCurrentTransformToBitmap();
        if (transformed != null)
        {
            // Reset transforms
            RotationAngle = 0;
            FlipHorizontal = false;
            FlipVertical = false;

            ApplyBitmapChanges(transformed);
        }
    }

    private SKBitmap? ApplyCurrentTransformToBitmap()
    {
        if (WorkingBitmap == null) return null;

        try
        {
            var bitmap = WorkingBitmap;
            SKBitmap newBitmap;
            
            // For 90/270 degree rotations, swap width/height
            if (RotationAngle % 180 != 0)
            {
                newBitmap = new SKBitmap(bitmap.Height, bitmap.Width, bitmap.ColorType, bitmap.AlphaType);
            }
            else
            {
                newBitmap = new SKBitmap(bitmap.Info);
            }

            using (var canvas = new SKCanvas(newBitmap))
            {
                canvas.Clear();
                
                var centerX = newBitmap.Width / 2f;
                var centerY = newBitmap.Height / 2f;
                
                // Apply transformations
                canvas.Translate(centerX, centerY);
                canvas.RotateDegrees(RotationAngle);
                
                if (FlipHorizontal) canvas.Scale(-1, 1);
                if (FlipVertical) canvas.Scale(1, -1);
                
                canvas.Translate(-bitmap.Width / 2f, -bitmap.Height / 2f);
                canvas.DrawBitmap(bitmap, 0, 0);
            }

            return newBitmap;
        }
        catch (Exception ex)
        {
            return null;
        }
    }

    partial void OnRotationAngleChanged(float value)
    {
        // Invalidate canvas for instant preview
        canvasView?.InvalidateSurface();
        // Notify CanReset changed
        OnPropertyChanged(nameof(CanReset));
    }

    partial void OnFlipHorizontalChanged(bool value)
    {
        // Invalidate canvas for instant preview
        canvasView?.InvalidateSurface();
        // Notify CanReset changed
        OnPropertyChanged(nameof(CanReset));
    }

    partial void OnFlipVerticalChanged(bool value)
    {
        // Invalidate canvas for instant preview
        canvasView?.InvalidateSurface();
        // Notify CanReset changed
        OnPropertyChanged(nameof(CanReset));
    }

}




