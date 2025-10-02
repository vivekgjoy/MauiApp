using SkiaSharp;

namespace MauiApp.Views;

public partial class ImageEditPage : ContentPage
{
    private string? _imagePath;
    private SKColor _currentColor = SKColors.Red;
    private float _brushSize = 5f;
    private string _currentTool = "Brush";
    private string _currentSymbol = "→";

    public string? ImagePath 
    { 
        get => _imagePath; 
        set 
        { 
            _imagePath = value;
            if (!string.IsNullOrEmpty(value))
            {
                EditImage.Source = ImageSource.FromFile(value);
            }
        } 
    }
    
    public string? EditedImagePath { get; private set; }

    public ImageEditPage()
    {
        InitializeComponent();
        UpdateToolSelection("Brush");
    }

    private void OnColorButtonClicked(object sender, EventArgs e)
    {
        if (sender == RedColorButton)
            OnColorSelected(SKColors.Red);
        else if (sender == BlueColorButton)
            OnColorSelected(SKColors.Blue);
        else if (sender == GreenColorButton)
            OnColorSelected(SKColors.Green);
        else if (sender == BlackColorButton)
            OnColorSelected(SKColors.Black);
    }

    private void OnColorSelected(SKColor color)
    {
        _currentColor = color;
        UpdateColorSelection(color);
    }

    private void UpdateColorSelection(SKColor selectedColor)
    {
        // Reset all color buttons to normal state
        RedColorButton.Text = "";
        BlueColorButton.Text = "";
        GreenColorButton.Text = "";
        BlackColorButton.Text = "";

        // Highlight selected color with a checkmark
        if (selectedColor == SKColors.Red)
        {
            RedColorButton.Text = "✓";
            RedColorButton.TextColor = Colors.White;
        }
        else if (selectedColor == SKColors.Blue)
        {
            BlueColorButton.Text = "✓";
            BlueColorButton.TextColor = Colors.White;
        }
        else if (selectedColor == SKColors.Green)
        {
            GreenColorButton.Text = "✓";
            GreenColorButton.TextColor = Colors.White;
        }
        else if (selectedColor == SKColors.Black)
        {
            BlackColorButton.Text = "✓";
            BlackColorButton.TextColor = Colors.White;
        }
    }

    private void OnBrushSizeChanged(object sender, ValueChangedEventArgs e)
    {
        _brushSize = (float)e.NewValue;
        BrushSizeLabel.Text = $"{_brushSize:F0}px";
    }

    private void OnToolSelected(object sender, EventArgs e)
    {
        if (sender == BrushToolButton)
        {
            _currentTool = "Brush";
            UpdateToolSelection("Brush");
        }
        else if (sender == EraserToolButton)
        {
            _currentTool = "Eraser";
            UpdateToolSelection("Eraser");
        }
    }

    private void UpdateToolSelection(string selectedTool)
    {
        // Reset all tool buttons
        BrushToolButton.BackgroundColor = Colors.Transparent;
        BrushToolButton.TextColor = Color.FromArgb("#ED1C24");

        EraserToolButton.BackgroundColor = Colors.Transparent;
        EraserToolButton.TextColor = Color.FromArgb("#ED1C24");

        UndoButton.BackgroundColor = Colors.Transparent;
        UndoButton.TextColor = Color.FromArgb("#ED1C24");

        RedoButton.BackgroundColor = Colors.Transparent;
        RedoButton.TextColor = Color.FromArgb("#ED1C24");

        // Highlight selected tool
        if (selectedTool == "Brush")
        {
            BrushToolButton.BackgroundColor = Color.FromArgb("#ED1C24");
            BrushToolButton.TextColor = Colors.White;
        }
        else if (selectedTool == "Eraser")
        {
            EraserToolButton.BackgroundColor = Color.FromArgb("#ED1C24");
            EraserToolButton.TextColor = Colors.White;
        }
    }

    private void OnSymbolSelected(object sender, EventArgs e)
    {
        _currentTool = "Symbol";
        
        if (sender == ArrowSymbolButton)
            _currentSymbol = "→";
        else if (sender == CheckSymbolButton)
            _currentSymbol = "✓";
        else if (sender == CrossSymbolButton)
            _currentSymbol = "✗";
        else if (sender == CircleSymbolButton)
            _currentSymbol = "○";
        else if (sender == SquareSymbolButton)
            _currentSymbol = "□";
    }

    private void OnUndoClicked(object sender, EventArgs e)
    {
        // Simplified undo - just show a message for now
        DisplayAlert("Info", "Undo functionality would be implemented here", "OK");
    }

    private void OnRedoClicked(object sender, EventArgs e)
    {
        // Simplified redo - just show a message for now
        DisplayAlert("Info", "Redo functionality would be implemented here", "OK");
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        try
        {
            if (string.IsNullOrEmpty(_imagePath))
            {
                await DisplayAlert("Error", "No image to save", "OK");
                return;
            }

            // For now, just copy the original image
            // In a real implementation, you would apply the edits here
            EditedImagePath = _imagePath;

            // Navigate back to AddReportPage with edited image
            await Navigation.PopAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to save image: {ex.Message}", "OK");
        }
    }
}