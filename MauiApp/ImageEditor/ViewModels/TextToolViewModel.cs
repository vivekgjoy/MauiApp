using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Controls;
using MauiApp.ImageEditor.Models;

namespace MauiApp.ImageEditor.ViewModels;

public partial class TextToolViewModel : ObservableObject
{
    private readonly ImageEditorViewModel parent;

    public ToolType SelectedToolType => parent.SelectedToolType;

    [ObservableProperty]
    private string inputText = "Enter Text";

    [ObservableProperty]
    private float textSize = 150f;

    [ObservableProperty]
    private ObservableCollection<Color> textColors = new();

    [ObservableProperty]
    private Color selectedTextColor = Colors.Red;

    private Color baseSelectedTextColor = Colors.Red; // Base color without opacity

    private bool hasSelectedText = false;

    public bool HasSelectedText
    {
        get => hasSelectedText;
        set
        {
            if (SetProperty(ref hasSelectedText, value))
            {
                OnPropertyChanged(nameof(TextColorUnderline));
            }
        }
    }

    [ObservableProperty]
    private bool isTextMode = false;

    partial void OnSelectedTextColorChanged(Color value)
    {
        OnPropertyChanged(nameof(TextColorUnderline));
    }

    public Color TextColorUnderline => HasSelectedText ? SelectedTextColor : Colors.Gray;

    partial void OnIsTextModeChanged(bool value)
    {
        OnPropertyChanged(nameof(IsMainToolbarVisible));
        if (!value)
        {
            ShowFontFamilyPanel = false;
            ShowTextColorPanel = false;
            ShowFillColorPanel = false;
            ShowFontAttributesPanel = false;
        }
    }

    [ObservableProperty]
    private bool showFontFamilyPanel = false;

    partial void OnShowFontFamilyPanelChanged(bool value)
    {
        OnPropertyChanged(nameof(IsMainToolbarVisible));
    }

    [ObservableProperty]
    private bool showTextColorPanel = false;

    partial void OnShowTextColorPanelChanged(bool value)
    {
        OnPropertyChanged(nameof(IsMainToolbarVisible));
    }

    [ObservableProperty]
    private bool showFillColorPanel = false;

    partial void OnShowFillColorPanelChanged(bool value)
    {
        OnPropertyChanged(nameof(IsMainToolbarVisible));
    }

    [ObservableProperty]
    private bool showFontAttributesPanel = false;

    partial void OnShowFontAttributesPanelChanged(bool value)
    {
        OnPropertyChanged(nameof(IsMainToolbarVisible));
    }

    [ObservableProperty]
    private ObservableCollection<FontStyleItem> fontStyles = new();

    [ObservableProperty]
    private string selectedFontFamily = "Arial";

    [ObservableProperty]
    private ObservableCollection<Color> fillColors = new();

    [ObservableProperty]
    private Color? selectedFillColor = null;

    private Color? baseSelectedFillColor = null; // Base color without opacity

    [ObservableProperty]
    private float textColorOpacity = 1.0f;

    [ObservableProperty]
    private float fillColorOpacity = 1.0f;

    [ObservableProperty]
    private bool showTextColorOpacityPanel = false;

    [ObservableProperty]
    private bool showFillColorOpacityPanel = false;

    [ObservableProperty]
    private bool isBold = false;

    [ObservableProperty]
    private bool isItalic = false;

    [ObservableProperty]
    private bool isUnderline = false;

    public bool IsMainToolbarVisible => IsTextMode && !ShowFontFamilyPanel && !ShowTextColorPanel && !ShowFillColorPanel && !ShowFontAttributesPanel;

    public TextToolViewModel(ImageEditorViewModel parent)
    {
        this.parent = parent;
        SetupColors();
        SetupFonts();
        // Initialize base colors
        baseSelectedTextColor = SelectedTextColor;
    }

    private void SetupColors()
    {
        TextColors.Add(Colors.Black);
        TextColors.Add(Colors.White);
        TextColors.Add(Colors.Yellow);
        TextColors.Add(Colors.Red);
        TextColors.Add(Colors.Green);
        TextColors.Add(Colors.Blue);
        TextColors.Add(Colors.Orange);
        TextColors.Add(Colors.Cyan);

        FillColors.Add(Colors.Black);
        FillColors.Add(Colors.White);
        FillColors.Add(Colors.Yellow);
        FillColors.Add(Colors.Red);
        FillColors.Add(Colors.Green);
        FillColors.Add(Colors.Blue);
        FillColors.Add(Colors.Orange);
        FillColors.Add(Colors.Cyan);
    }

    private void SetupFonts()
    {
        // Add fonts with different visual styles to match screenshot
        FontStyles.Add(new FontStyleItem("Arial", FontAttributes.None)); // Standard sans-serif
        FontStyles.Add(new FontStyleItem("Helvetica", FontAttributes.None)); // Standard sans-serif
        FontStyles.Add(new FontStyleItem("Arial", FontAttributes.Italic)); // Italic style
        FontStyles.Add(new FontStyleItem("Arial", FontAttributes.Bold | FontAttributes.Italic)); // Bold Italic
        FontStyles.Add(new FontStyleItem("Arial", FontAttributes.Bold)); // Bold sans-serif
    }

    public void StartTextMode()
    {
        IsTextMode = true;
        ShowFontFamilyPanel = false;
        ShowTextColorPanel = false;
        ShowFillColorPanel = false;
        ShowFontAttributesPanel = false;
    }

    [RelayCommand]
    public void SelectTextColor(Color color)
    {
        // Reset opacity to 1.0 when selecting a new color
        TextColorOpacity = 1.0f;
        // Store base color (normalize to full opacity for comparison)
        baseSelectedTextColor = new Color(color.Red, color.Green, color.Blue, 1.0f);
        // Set the color with full opacity
        SelectedTextColor = baseSelectedTextColor;
    }

    public Color BaseSelectedTextColor => baseSelectedTextColor;

    [RelayCommand]
    public void ShowFontFamilyOptions()
    {
        ShowFontFamilyPanel = true;
        ShowTextColorPanel = false;
        ShowFillColorPanel = false;
        ShowFontAttributesPanel = false;
        IsBold = false;
        IsItalic = false;
    }

    [RelayCommand]
    public void ShowTextColorOptions()
    {
        ShowTextColorPanel = true;
        ShowFontFamilyPanel = false;
        ShowFillColorPanel = false;
        ShowFontAttributesPanel = false;
        IsBold = false;
        IsItalic = false;
    }

    [RelayCommand]
    public void ShowFillColorOptions()
    {
        ShowFillColorPanel = true;
        ShowFontFamilyPanel = false;
        ShowTextColorPanel = false;
        ShowFontAttributesPanel = false;
        IsBold = false;
        IsItalic = false;
    }

    [RelayCommand]
    public void ShowFontAttributesOptions()
    {
        ShowFontAttributesPanel = true;
        ShowFontFamilyPanel = false;
        ShowTextColorPanel = false;
        ShowFillColorPanel = false;
    }

    [RelayCommand]
    public void CloseFontFamilyPanel()
    {
        ShowFontFamilyPanel = false;
    }

    [RelayCommand]
    public void CloseTextColorPanel()
    {
        ShowTextColorPanel = false;
    }

    [RelayCommand]
    public void CloseFillColorPanel()
    {
        ShowFillColorPanel = false;
    }

    [RelayCommand]
    public void ShowTextColorOpacityOptions()
    {
        ShowTextColorOpacityPanel = true;
    }

    [RelayCommand]
    public void CloseTextColorOpacityPanel()
    {
        ShowTextColorOpacityPanel = false;
    }

    [RelayCommand]
    public void ShowFillColorOpacityOptions()
    {
        ShowFillColorOpacityPanel = true;
    }

    [RelayCommand]
    public void CloseFillColorOpacityPanel()
    {
        ShowFillColorOpacityPanel = false;
    }

    [RelayCommand]
    public void CloseFontAttributesPanel()
    {
        ShowFontAttributesPanel = false;
    }

    [RelayCommand]
    public void SelectFontFamily(FontStyleItem? fontStyleItem)
    {
        if (fontStyleItem != null)
        {
            // Clear previous selection
            foreach (var item in FontStyles)
            {
                item.IsSelected = false;
            }
            
            // Set new selection
            fontStyleItem.IsSelected = true;
            SelectedFontFamily = fontStyleItem.FontFamily;
            
            // Notify property changes
            OnPropertyChanged(nameof(SelectedFontFamily));
            OnPropertyChanged(nameof(FontStyles));
            
            // Also update the font style if needed
            if (fontStyleItem.Style.HasFlag(FontAttributes.Bold))
            {
                IsBold = true;
            }
            if (fontStyleItem.Style.HasFlag(FontAttributes.Italic))
            {
                IsItalic = true;
            }
        }
    }
    
    [RelayCommand]
    public void SelectFontFamilyByName(string fontFamily)
    {
        SelectedFontFamily = fontFamily;
        OnPropertyChanged(nameof(SelectedFontFamily));
    }

    [RelayCommand]
    public void SelectFillColor(Color? color)
    {
        if (color != null)
        {
            // Reset opacity to 1.0 when selecting a new color
            FillColorOpacity = 1.0f;
            // Store base color (normalize to full opacity for comparison)
            baseSelectedFillColor = new Color(color.Red, color.Green, color.Blue, color.Alpha);
            // Set the color with full opacity
            SelectedFillColor = baseSelectedFillColor;
        }
        else
        {
            baseSelectedFillColor = null;
            SelectedFillColor = null;
        }
    }

    public Color? BaseSelectedFillColor => baseSelectedFillColor;

    [RelayCommand]
    public void ToggleBold()
    {
        IsBold = !IsBold;
    }

    [RelayCommand]
    public void ToggleItalic()
    {
        IsItalic = !IsItalic;
    }

    [RelayCommand]
    public void ToggleUnderline()
    {
        IsUnderline = !IsUnderline;
    }

    [RelayCommand]
    public void AddText()
    {
        if (!string.IsNullOrEmpty(InputText) && InputText != "Enter Text")
        {
            // Text addition logic will be handled in the main view model
            InputText = "Enter Text"; // Reset to placeholder for next text
            parent.CloseToolPanelCommand.Execute(null);
        }
    }

    // Method to sync InputText with selected overlay text
    public void SyncTextFromOverlay(string? text)
    {
        if (text != null && !string.IsNullOrEmpty(text))
        {
            InputText = text;
        }
        else
        {
            InputText = "Enter Text"; // Set placeholder for new text
        }
    }
}







