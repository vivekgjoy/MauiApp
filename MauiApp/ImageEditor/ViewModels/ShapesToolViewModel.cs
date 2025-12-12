using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MauiApp.ImageEditor.Models;
using Microsoft.Maui.Graphics;

namespace MauiApp.ImageEditor.ViewModels;

public partial class ShapesToolViewModel : ObservableObject
{
    private readonly ImageEditorViewModel parentViewModel;

    [ObservableProperty]
    private ShapeType selectedShapeType = ShapeType.None;

    [ObservableProperty]
    private bool isShapeMode = false;

    partial void OnIsShapeModeChanged(bool value)
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

    [ObservableProperty]
    private float strokeWidth = 10f;

    partial void OnStrokeWidthChanged(float value)
    {
        // Notify that stroke width changed - page will update current shape
        OnShapePropertyChanged?.Invoke();
    }

    [ObservableProperty]
    private ObservableCollection<Color> strokeColors = new();

    [ObservableProperty]
    private Color selectedStrokeColor = Colors.Red; // Default red stroke color

    partial void OnSelectedStrokeColorChanged(Color value)
    {
        // Notify that stroke color changed - page will update current shape
        OnShapePropertyChanged?.Invoke();
    }

    partial void OnSelectedFillColorChanged(Color value)
    {
        // Notify that fill color changed - page will update current shape
        OnShapePropertyChanged?.Invoke();
    }

    public bool IsMainToolbarVisible => IsShapeMode && !ShowFillColorPanel && !ShowStrokePanel;

    public ToolType SelectedToolType => parentViewModel.SelectedToolType;

    public ShapesToolViewModel(ImageEditorViewModel parent)
    {
        parentViewModel = parent;
        SetupColors();
    }

    private void SetupColors()
    {
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

    public void StartShapeMode()
    {
        IsShapeMode = true;
        SelectedShapeType = ShapeType.None;
        ShowFillColorPanel = false;
        ShowStrokePanel = false;
    }

    [RelayCommand]
    public void SelectShapeType(string shapeType)
    {
        if (Enum.TryParse<ShapeType>(shapeType, out var shape))
        {
            SelectedShapeType = shape;
            if (!IsShapeMode)
            {
                IsShapeMode = true;
            }
            ShowFillColorPanel = false;
            ShowStrokePanel = false;
            
            // Notify that a shape type was selected - page will create default shape
            OnShapeSelectedForCreation?.Invoke(shape);
        }
    }

    // Event to notify page when a shape type is selected for immediate creation
    public event Action<ShapeType>? OnShapeSelectedForCreation;
    
    // Event to notify page when shape properties (color, stroke) change
    public event Action? OnShapePropertyChanged;

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
        SelectedFillColor = color;
        // Property changed event will fire automatically
    }

    [RelayCommand]
    public void SelectStrokeColor(Color color)
    {
        SelectedStrokeColor = color;
        // Property changed event will fire automatically
    }
}





