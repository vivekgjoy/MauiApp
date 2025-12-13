using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Maui.Controls;

namespace MauiApp.ImageEditor.Models;

public class FontStyleItem : INotifyPropertyChanged
{
    private bool _isSelected = false;
    
    public string FontFamily { get; set; } = string.Empty;
    public FontAttributes Style { get; set; } = FontAttributes.None;
    
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }
    }
    
    public FontStyleItem(string fontFamily, FontAttributes style = FontAttributes.None)
    {
        FontFamily = fontFamily;
        Style = style;
    }
    
    public event PropertyChangedEventHandler? PropertyChanged;
    
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}







