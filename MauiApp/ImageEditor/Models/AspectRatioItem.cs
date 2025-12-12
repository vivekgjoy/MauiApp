using CommunityToolkit.Mvvm.ComponentModel;

namespace MauiApp.ImageEditor.Models;

public class AspectRatioItem : ObservableObject
{
    private bool isSelected;
    
    public string Name { get; set; } = string.Empty;
    public float? Ratio { get; set; }
    
    public bool IsSelected
    {
        get => isSelected;
        set => SetProperty(ref isSelected, value);
    }
}






