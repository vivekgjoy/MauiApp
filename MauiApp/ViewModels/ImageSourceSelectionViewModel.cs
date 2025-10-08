using System.Windows.Input;

namespace MauiApp.ViewModels;

public class ImageSourceSelectionViewModel : BaseViewModel
{
    private string? _selectedSource;

    public string? SelectedSource
    {
        get => _selectedSource;
        set => SetProperty(ref _selectedSource, value);
    }

    public ICommand SelectGalleryCommand { get; }
    public ICommand SelectCameraCommand { get; }
    public ICommand DismissCommand { get; }

    public ImageSourceSelectionViewModel()
    {
        System.Diagnostics.Debug.WriteLine("ImageSourceSelectionViewModel constructor called");
        
        SelectGalleryCommand = new Command(async () => await SelectGallery());
        SelectCameraCommand = new Command(async () => await SelectCamera());
        DismissCommand = new Command(async () => await Dismiss());
    }

    private async Task SelectGallery()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("Gallery selected");
            SelectedSource = "Gallery";
            await Dismiss();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error selecting gallery: {ex.Message}");
        }
    }

    private async Task SelectCamera()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("Camera selected");
            SelectedSource = "Camera";
            await Dismiss();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error selecting camera: {ex.Message}");
        }
    }

    private async Task Dismiss()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("Dismissing ImageSourceSelectionPage");
            await Application.Current.MainPage.Navigation.PopModalAsync(true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error dismissing page: {ex.Message}");
        }
    }
}
