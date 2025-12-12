namespace MauiApp.ImageEditor.Models;

public class ToolItem
{
    public string Name { get; set; }
    public string Icon { get; set; }
    public ToolType Type { get; set; }

    public ToolItem(string name, string icon, ToolType type)
    {
        Name = name;
        Icon = icon;
        Type = type;
    }
}

public enum ToolType
{
    None,
    Crop,
    Draw,
    Text,
    Sticker,
    Adjust,
    Shapes
}

public enum SubToolType
{
    None,
    CropRatios,
    ShapesPresets
}

public enum CropMode
{
    None,
    Custom,
    Original,
    Circle,
    Square,
    Ratio3_1,
    Ratio3_2,
    Ratio4_3,
    Ratio5_4,
    Ratio7_5,
    Ratio16_9,
    Eclipse
}

public enum ShapeType
{
    None,
    Rectangle,
    Circle,
    Line,
    SingleArrow,
    DoubleArrow,
    DottedLine
}


