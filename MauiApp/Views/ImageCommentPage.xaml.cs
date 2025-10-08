using MauiApp.Core.Interfaces;
using MauiApp.Core.Models;
using Microsoft.Maui.Platform;
#if ANDROID
using AndroidX.AppCompat.App;
using Android.OS;
using Android.Graphics;
#endif

namespace MauiApp.Views;

public partial class ImageCommentPage : ContentPage
{
    private readonly IReportImageService _reportImageService;
    private string? _imagePath;
    private string? _imageId;
    private bool _isEditingExisting = false;

    public string? ImagePath
    {
        get => _imagePath;
        set
        {
            _imagePath = value;
            if (!string.IsNullOrEmpty(value))
            {
                ImagePreview.Source = ImageSource.FromFile(value);
            }
        }
    }

    public string? ImageId
    {
        get => _imageId;
        set => _imageId = value;
    }

    public bool IsEditingExisting
    {
        get => _isEditingExisting;
        set => _isEditingExisting = value;
    }

    public ImageCommentPage()
    {
        InitializeComponent();
        _reportImageService = ServiceHelper.GetService<IReportImageService>();
        
        // Set up navigation bar back command
        NavigationBar.BackCommand = new Command(async () => await OnBackClicked());
        
        // Handle safe area for status bar
        this.Loaded += OnPageLoaded;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (!string.IsNullOrEmpty(ImagePath))
        {
            ImagePreview.Source = ImageSource.FromFile(ImagePath);
        }

        // Load existing comment if editing
        if (_isEditingExisting && !string.IsNullOrEmpty(_imageId))
        {
            var existingImage = _reportImageService.GetImage(_imageId);
            if (existingImage != null)
            {
                CommentEditor.Text = existingImage.Comment ?? string.Empty;
            }
        }
    }

    private void OnPageLoaded(object sender, EventArgs e)
    {
        // Set status bar color to red to match the header
#if ANDROID
        if (DeviceInfo.Platform == DevicePlatform.Android)
        {
            var activity = Platform.CurrentActivity as AppCompatActivity;
            if (activity != null && Build.VERSION.SdkInt >= BuildVersionCodes.Lollipop)
            {
                activity.Window.SetStatusBarColor(Android.Graphics.Color.ParseColor("#ED1C24"));
            }
        }
#else
        // No status bar color setting needed for other platforms
#endif
    }

    private async Task OnBackClicked()
    {
        await Navigation.PopAsync();
    }

    private async void OnSkipClicked(object sender, EventArgs e)
    {
        await SaveImageAndNavigate();
    }

    private async void OnSaveAndContinueClicked(object sender, EventArgs e)
    {
        await SaveImageAndNavigate();
    }

    private async Task SaveImageAndNavigate()
    {
        try
        {
            if (string.IsNullOrEmpty(_imagePath))
            {
                await DisplayAlert("Error", "No image to save", "OK");
                return;
            }

            if (_isEditingExisting && !string.IsNullOrEmpty(_imageId))
            {
                // Update existing image
                System.Diagnostics.Debug.WriteLine($"Updating existing image {_imageId} with path: {_imagePath}");
                _reportImageService.UpdateImageComment(_imageId, CommentEditor.Text?.Trim() ?? string.Empty);
                _reportImageService.UpdateImagePath(_imageId, _imagePath); // Update image path in case it changed
            }
            else
            {
                // Create new ReportImage object
                var reportImage = new ReportImage
                {
                    ImagePath = _imagePath,
                    Comment = CommentEditor.Text?.Trim() ?? string.Empty,
                    CreatedAt = DateTime.Now
                };

                System.Diagnostics.Debug.WriteLine($"Adding new image to service: {_imagePath}, Comment: {reportImage.Comment}");
                
                // Add to the shared collection
                _reportImageService.AddImage(reportImage);
                
                System.Diagnostics.Debug.WriteLine($"Image added. Total images in service: {_reportImageService.ReportImages.Count}");
            }

            // Navigate back to AddReportPage
            await Shell.Current.GoToAsync("///AddReportPage");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to save image: {ex.Message}", "OK");
        }
    }
}
