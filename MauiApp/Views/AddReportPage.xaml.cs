using MauiApp.Core.Interfaces;
using MauiApp.Core.Services;
using MauiApp.ViewModels;
using MauiApp.Core.Models;

namespace MauiApp.Views;

public partial class AddReportPage : ContentPage
{
    private readonly IBottomSheetService _bottomSheetService;
    private readonly IReportImageService _reportImageService;
    private const int MaxImages = 10;

    public AddReportPage()
    {
        InitializeComponent();
        _bottomSheetService = ServiceHelper.GetService<IBottomSheetService>();
        _reportImageService = ServiceHelper.GetService<IReportImageService>();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Set navigation bar styling
        SetNavigationBarStyling();

        // Initialize images collection
        await UpdateImagesCollection();
    }

    private void SetNavigationBarStyling()
    {
#if ANDROID
        if (Shell.Current?.CurrentPage == this)
        {
            var activity = Platform.CurrentActivity;
            if (activity != null)
            {
                // Status bar color is now set globally, just ensure light content (white text/icons)
                if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.M)
                {
                    var decorView = activity.Window?.DecorView;
                    if (decorView != null)
                    {
                        var flags = (int)decorView.SystemUiFlags;
                        decorView.SystemUiFlags = (Android.Views.SystemUiFlags)(flags & ~(int)Android.Views.SystemUiFlags.LightStatusBar);
                    }
                }
                
                // Ensure proper navigation bar styling
                if (activity is AndroidX.AppCompat.App.AppCompatActivity appCompatActivity)
                {
                    var actionBar = appCompatActivity.SupportActionBar;
                    if (actionBar != null)
                    {
                        actionBar.SetDisplayHomeAsUpEnabled(true);
                        actionBar.SetHomeButtonEnabled(true);
                        // Action bar color is now handled globally
                    }
                }
            }
        }
#endif
    }

    private async void OnAddImageClicked(object sender, EventArgs e)
    {
        try
        {
            if (_reportImageService.ReportImages.Count >= MaxImages)
            {
                await DisplayAlert("Limit Reached", $"You can only add up to {MaxImages} images.", "OK");
                return;
            }

            // Show dedicated image source selection bottom sheet
            var imageSourcePage = new ImageSourceSelectionPage();
            var viewModel = new ImageSourceSelectionViewModel();
            imageSourcePage.BindingContext = viewModel;
            
            // Subscribe to the source selected event
            viewModel.SourceSelected += async (sender, selectedSource) =>
            {
                await HandleImageSelection(selectedSource);
            };
            
            await Navigation.PushModalAsync(imageSourcePage);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to show image selection options: {ex.Message}", "OK");
        }
    }

    private async Task HandleImageSelection(string option)
    {
        try
        {
            FileResult photo = null;

            if (option == "Gallery")
            {
                photo = await MediaPicker.Default.PickPhotoAsync();
            }
            else if (option == "Camera")
            {
                // Request camera permission before capturing
                await RequestCameraPermission();
                photo = await MediaPicker.Default.CapturePhotoAsync();
            }

            if (photo != null)
            {
                // Automatically navigate to cropping page instead of adding directly
                await NavigateToImageEditing(photo.FullPath);
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to select image: {ex.Message}", "OK");
        }
    }

    private async Task RequestCameraPermission()
    {
        try
        {
            var status = await Permissions.CheckStatusAsync<Permissions.Camera>();
            if (status != PermissionStatus.Granted)
            {
                status = await Permissions.RequestAsync<Permissions.Camera>();
            }

            if (status != PermissionStatus.Granted)
            {
                await DisplayAlert("Permission Required", "Camera permission is required to take photos.", "OK");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Permission request error: {ex.Message}");
        }
    }


    private async Task UpdateImagesCollection()
    {
        // Clear existing children
        ImagesGrid.Children.Clear();

        // Add images to grid (2 per row)
        for (int i = 0; i < _reportImageService.ReportImages.Count; i++)
        {
            var reportImage = _reportImageService.ReportImages[i];
            var row = i / 2;
            var column = i % 2;

            // Create the image frame
            var imageFrame = CreateImageFrame(reportImage, i);

            // Add to grid
            Grid.SetRow(imageFrame, row);
            Grid.SetColumn(imageFrame, column);
            ImagesGrid.Children.Add(imageFrame);
        }

        // Update button visibility
        AddImageButton.IsVisible = _reportImageService.ReportImages.Count < MaxImages;
    }

    private Frame CreateImageFrame(ReportImage reportImage, int index)
    {
        var frame = new Frame
        {
            BackgroundColor = Color.FromArgb("#2D2D2D"), // Dark gray background
            CornerRadius = 8,
            Padding = new Thickness(8),
            Margin = new Thickness(4),
            HasShadow = false
        };

        var grid = new Grid
        {
            HeightRequest = 120,
            WidthRequest = 150
        };

        // Image
        var image = new Image
        {
            Source = ImageSource.FromFile(reportImage.ImagePath),
            Aspect = Aspect.AspectFill,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill
        };

        // Delete button
        var deleteButton = new Button
        {
            Text = "×",
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White,
            BackgroundColor = Color.FromArgb("#E50000"), // Red background
            CornerRadius = 12,
            WidthRequest = 24,
            HeightRequest = 24,
            HorizontalOptions = LayoutOptions.End,
            VerticalOptions = LayoutOptions.Start,
            Margin = new Thickness(0, 4, 4, 0)
        };

        // Add click handler for delete
        deleteButton.Clicked += (s, e) => OnRemoveImageClicked(s, e, reportImage.Id);

        grid.Children.Add(image);
        grid.Children.Add(deleteButton);
        frame.Content = grid;

        return frame;
    }

    private async void OnRemoveImageClicked(object sender, EventArgs e, string imageId)
    {
        _reportImageService.RemoveImage(imageId);
        await UpdateImagesCollection();
    }

    private async Task NavigateToImageEditing(string imagePath)
    {
        try
        {
            var imageCropPage = new ImageCropPage
            {
                ImagePath = imagePath
            };

            await Navigation.PushAsync(imageCropPage);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to open image editor: {ex.Message}", "OK");
        }
    }


    private async void OnBackClicked(object sender, EventArgs e)
    {
        try
        {
            // Use Shell navigation since all pages are registered as Shell routes
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            // Fallback to main page if navigation fails
            try
            {
                await Shell.Current.GoToAsync("//MainPage");
            }
            catch
            {
                // Last resort - just go back to main page
                await Shell.Current.GoToAsync("//MainPage");
            }
        }
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        try
        {
            // Use Shell navigation since all pages are registered as Shell routes
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            // Fallback to main page if navigation fails
            try
            {
                await Shell.Current.GoToAsync("//MainPage");
            }
            catch
            {
                // Last resort - just go back to main page
                await Shell.Current.GoToAsync("//MainPage");
            }
        }
    }

    private async void OnGenerateReportClicked(object sender, EventArgs e)
    {
        try
        {
            var title = TitleEntry.Text?.Trim();
            var description = DescriptionEditor.Text?.Trim();

            if (string.IsNullOrEmpty(title))
            {
                await DisplayAlert("Validation Error", "Please enter a report title.", "OK");
                return;
            }

            if (string.IsNullOrEmpty(description))
            {
                await DisplayAlert("Validation Error", "Please enter a report description.", "OK");
                return;
            }

            if (_reportImageService.ReportImages.Count == 0)
            {
                await DisplayAlert("Validation Error", "Please select at least one image for the report.", "OK");
                return;
            }

            // Show progress loader
            await ShowProgressLoader("Preparing report preview...");

            // Navigate to PDF preview page
            await Shell.Current.GoToAsync(nameof(PDFPreviewPage));
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to prepare report: {ex.Message}", "OK");
        }
    }

    private async Task ShowProgressLoader(string message)
    {
        // Create a progress overlay
        var progressOverlay = new Grid
        {
            BackgroundColor = Color.FromArgb("#80000000"), // Semi-transparent black
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill
        };

        var progressFrame = new Frame
        {
            BackgroundColor = Color.FromArgb("#2D2D2D"),
            CornerRadius = 12,
            Padding = new Thickness(30),
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            HasShadow = true
        };

        var progressStack = new StackLayout
        {
            Spacing = 20,
            HorizontalOptions = LayoutOptions.Center
        };

        var activityIndicator = new ActivityIndicator
        {
            IsRunning = true,
            Color = Color.FromArgb("#E50000"),
            WidthRequest = 40,
            HeightRequest = 40
        };

        var progressLabel = new Label
        {
            Text = message,
            FontSize = 16,
            TextColor = Colors.White,
            HorizontalOptions = LayoutOptions.Center
        };

        progressStack.Children.Add(activityIndicator);
        progressStack.Children.Add(progressLabel);
        progressFrame.Content = progressStack;
        progressOverlay.Children.Add(progressFrame);

        // Add overlay to the page
        if (Content is Grid mainGrid)
        {
            mainGrid.Children.Add(progressOverlay);
        }
        else
        {
            // If content is not a Grid, wrap it
            var wrapperGrid = new Grid();
            wrapperGrid.Children.Add(Content);
            wrapperGrid.Children.Add(progressOverlay);
            Content = wrapperGrid;
        }

        // Show progress for a short time
        await Task.Delay(1500);

        // Remove overlay
        if (Content is Grid grid && grid.Children.Contains(progressOverlay))
        {
            grid.Children.Remove(progressOverlay);
        }
    }
}
