using SkiaSharp;

namespace MauiApp.ImageEditor.Models;

/// <summary>
/// Base interface for all editor actions that can be undone/redone.
/// </summary>
public interface IEditorAction
{
    /// <summary>
    /// The type of action (for identification)
    /// </summary>
    EditorActionType ActionType { get; }
}

/// <summary>
/// Types of editor actions
/// </summary>
public enum EditorActionType
{
    AddStroke,
    AddShape,
    AddCaption,
    Crop,
    Transform, // Rotation, flip, etc.
    DeleteStroke,
    DeleteShape,
    DeleteCaption
}

/// <summary>
/// Action for adding a stroke
/// </summary>
public class AddStrokeAction : IEditorAction
{
    public EditorActionType ActionType => EditorActionType.AddStroke;
    public Stroke Stroke { get; set; }
    
    public AddStrokeAction(Stroke stroke)
    {
        Stroke = stroke;
    }
}

/// <summary>
/// Action for adding a shape
/// </summary>
public class AddShapeAction : IEditorAction
{
    public EditorActionType ActionType => EditorActionType.AddShape;
    public ShapeLayer Shape { get; set; }
    
    public AddShapeAction(ShapeLayer shape)
    {
        Shape = shape;
    }
}

/// <summary>
/// Action for adding a caption
/// </summary>
public class AddCaptionAction : IEditorAction
{
    public EditorActionType ActionType => EditorActionType.AddCaption;
    public Caption Caption { get; set; }
    
    public AddCaptionAction(Caption caption)
    {
        Caption = caption;
    }
}

/// <summary>
/// Action for cropping
/// </summary>
public class CropAction : IEditorAction
{
    public EditorActionType ActionType => EditorActionType.Crop;
    public SKBitmap? BeforeBitmap { get; set; }
    public SKBitmap? AfterBitmap { get; set; }
    
    public CropAction(SKBitmap? before, SKBitmap? after)
    {
        BeforeBitmap = before;
        AfterBitmap = after;
    }
}

/// <summary>
/// Action for transformations (rotation, flip)
/// </summary>
public class TransformAction : IEditorAction
{
    public EditorActionType ActionType => EditorActionType.Transform;
    public float OldRotation { get; set; }
    public bool OldFlipHorizontal { get; set; }
    public bool OldFlipVertical { get; set; }
    public float NewRotation { get; set; }
    public bool NewFlipHorizontal { get; set; }
    public bool NewFlipVertical { get; set; }
    
    public TransformAction(float oldRotation, bool oldFlipH, bool oldFlipV,
                          float newRotation, bool newFlipH, bool newFlipV)
    {
        OldRotation = oldRotation;
        OldFlipHorizontal = oldFlipH;
        OldFlipVertical = oldFlipV;
        NewRotation = newRotation;
        NewFlipHorizontal = newFlipH;
        NewFlipVertical = newFlipV;
    }
}

/// <summary>
/// Action for deleting a stroke
/// </summary>
public class DeleteStrokeAction : IEditorAction
{
    public EditorActionType ActionType => EditorActionType.DeleteStroke;
    public Stroke Stroke { get; set; }
    
    public DeleteStrokeAction(Stroke stroke)
    {
        Stroke = stroke;
    }
}

/// <summary>
/// Action for deleting a shape
/// </summary>
public class DeleteShapeAction : IEditorAction
{
    public EditorActionType ActionType => EditorActionType.DeleteShape;
    public ShapeLayer Shape { get; set; }
    
    public DeleteShapeAction(ShapeLayer shape)
    {
        Shape = shape;
    }
}

/// <summary>
/// Action for deleting a caption
/// </summary>
public class DeleteCaptionAction : IEditorAction
{
    public EditorActionType ActionType => EditorActionType.DeleteCaption;
    public Caption Caption { get; set; }
    
    public DeleteCaptionAction(Caption caption)
    {
        Caption = caption;
    }
}




