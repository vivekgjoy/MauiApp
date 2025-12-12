using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Graphics;
using MauiApp.ImageEditor.Models;

namespace MauiApp.ImageEditor.ViewModels;

public partial class DrawToolViewModel : ObservableObject
{
    private readonly ImageEditorViewModel parent;

    [ObservableProperty]
    private float strokeWidth = 10f;

    [ObservableProperty]
    private ObservableCollection<Color> brushColors = new();

    [ObservableProperty]
    private Color selectedColor = Colors.Red;

    [ObservableProperty]
    private bool isDrawMode = false;

    partial void OnIsDrawModeChanged(bool value)
    {
        OnPropertyChanged(nameof(IsMainToolbarVisible));
        if (!value)
        {
            ShowFillColorPanel = false;
            ShowStrokePanel = false;
        }
    }

    [ObservableProperty]
    private bool showFillColorPanel = false;

    partial void OnShowFillColorPanelChanged(bool value)
    {
        OnPropertyChanged(nameof(IsMainToolbarVisible));
    }

    [ObservableProperty]
    private bool showStrokePanel = false;

    partial void OnShowStrokePanelChanged(bool value)
    {
        OnPropertyChanged(nameof(IsMainToolbarVisible));
    }

    [ObservableProperty]
    private ObservableCollection<Color> fillColors = new();

    [ObservableProperty]
    private Color selectedFillColor = Colors.Red;

    private Color baseSelectedFillColor = Colors.Red; // Base color without opacity

    [ObservableProperty]
    private ObservableCollection<Color> strokeColors = new();

    [ObservableProperty]
    private Color selectedStrokeColor = Colors.Red;

    [ObservableProperty]
    private float fillColorOpacity = 1.0f;

    [ObservableProperty]
    private bool showFillColorOpacityPanel = false;

    public bool IsMainToolbarVisible => IsDrawMode && !ShowFillColorPanel && !ShowStrokePanel;

    public ToolType SelectedToolType => parent.SelectedToolType;

    public DrawToolViewModel(ImageEditorViewModel parent)
    {
        this.parent = parent;
        SetupColors();
        // Initialize base colors
        baseSelectedFillColor = SelectedFillColor;
    }

    private void SetupColors()
    {
        BrushColors.Add(Colors.Red);
        BrushColors.Add(Colors.Blue);
        BrushColors.Add(Colors.Green);
        BrushColors.Add(Colors.Yellow);
        BrushColors.Add(Colors.White);
        BrushColors.Add(Colors.Black);
        BrushColors.Add(Colors.Purple);
        BrushColors.Add(Colors.Orange);

        // Fill colors
        FillColors.Add(Colors.Red);
        FillColors.Add(Colors.Black);
        FillColors.Add(Colors.White);
        FillColors.Add(Colors.Yellow);
        FillColors.Add(Colors.Green);
        FillColors.Add(Colors.Blue);
        FillColors.Add(Colors.Purple);
        FillColors.Add(Colors.Orange);

        // Stroke colors (same palette)
        StrokeColors.Add(Colors.Red);
        StrokeColors.Add(Colors.Black);
        StrokeColors.Add(Colors.White);
        StrokeColors.Add(Colors.Yellow);
        StrokeColors.Add(Colors.Green);
        StrokeColors.Add(Colors.Blue);
        StrokeColors.Add(Colors.Purple);
        StrokeColors.Add(Colors.Orange);
    }

    public void StartDrawMode()
    {
        IsDrawMode = true;
        ShowFillColorPanel = false;
        ShowStrokePanel = false;
    }

    [RelayCommand]
    public void SelectColor(Color color)
    {
        SelectedColor = color;
    }

    [RelayCommand]
    public void ClosePanel()
    {
        parent.CloseToolPanelCommand.Execute(null);
    }

    [RelayCommand]
    public void ShowFillColorOptions()
    {
        ShowFillColorPanel = true;
        ShowStrokePanel = false;
    }

    [RelayCommand]
    public void ShowStrokeOptions()
    {
        ShowStrokePanel = true;
        ShowFillColorPanel = false;
    }

    [RelayCommand]
    public void CloseFillColorPanel()
    {
        ShowFillColorPanel = false;
    }

    [RelayCommand]
    public void CloseStrokePanel()
    {
        ShowStrokePanel = false;
    }

    [RelayCommand]
    public void SelectFillColor(Color color)
    {
        // Reset opacity to 1.0 when selecting a new color
        FillColorOpacity = 1.0f;
        // Store base color (normalize to full opacity for comparison)
        baseSelectedFillColor = new Color(color.Red, color.Green, color.Blue, 1.0f);
        // Set the color with full opacity
        SelectedFillColor = baseSelectedFillColor;
        SelectedColor = baseSelectedFillColor; // Also update the main selected color
    }

    public Color BaseSelectedFillColor => baseSelectedFillColor;

    [RelayCommand]
    public void SelectStrokeColor(Color color)
    {
        SelectedStrokeColor = color;
        SelectedColor = color; // Also update the main selected color
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
}


