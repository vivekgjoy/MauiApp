using MauiApp.Core.Interfaces;
using MauiApp.Core.Services;

namespace MauiApp.Views;

public partial class AddReportPage : ContentPage
{
    private readonly IBottomSheetService _bottomSheetService;
    private List<string> _selectedImagePaths = new List<string>();
    private const int MaxImages = 10;

    public AddReportPage()
    {
        InitializeComponent();
        _bottomSheetService = ServiceHelper.GetService<IBottomSheetService>();
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
                // Set status bar to red and make it light content (white text/icons)
                activity.Window?.SetStatusBarColor(Android.Graphics.Color.ParseColor("#E50000")); // PrimaryRed
                activity.Window?.SetNavigationBarColor(Android.Graphics.Color.ParseColor("#E50000")); // PrimaryRed
                
                // Make status bar content light (white text/icons) - using compatible API
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
                        actionBar.SetBackgroundDrawable(new Android.Graphics.Drawables.ColorDrawable(Android.Graphics.Color.ParseColor("#E50000")));
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
            if (_selectedImagePaths.Count >= MaxImages)
            {
                await DisplayAlert("Limit Reached", $"You can only add up to {MaxImages} images.", "OK");
                return;
            }

            // Create bottom sheet options
            var options = new List<string> { "Gallery", "Camera" };
            
            var selectedOption = await _bottomSheetService.ShowSelectionAsync("Select Image Source", options);
            
            if (selectedOption != null)
            {
                await HandleImageSelection(selectedOption);
            }
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
                _selectedImagePaths.Add(photo.FullPath);
                await UpdateImagesCollection();
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
        for (int i = 0; i < _selectedImagePaths.Count; i++)
        {
            var imagePath = _selectedImagePaths[i];
            var row = i / 2;
            var column = i % 2;
            
            // Create the image frame
            var imageFrame = CreateImageFrame(imagePath, i);
            
            // Add to grid
            Grid.SetRow(imageFrame, row);
            Grid.SetColumn(imageFrame, column);
            ImagesGrid.Children.Add(imageFrame);
        }
        
        // Update button visibility
        AddImageButton.IsVisible = _selectedImagePaths.Count < MaxImages;
    }

    private Frame CreateImageFrame(string imagePath, int index)
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
            Source = ImageSource.FromFile(imagePath),
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
        deleteButton.Clicked += (s, e) => OnRemoveImageClicked(s, e, imagePath);

        grid.Children.Add(image);
        grid.Children.Add(deleteButton);
        frame.Content = grid;

        return frame;
    }

    private async void OnRemoveImageClicked(object sender, EventArgs e, string imagePath)
    {
        _selectedImagePaths.Remove(imagePath);
        await UpdateImagesCollection();
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
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

            if (_selectedImagePaths.Count == 0)
            {
                await DisplayAlert("Validation Error", "Please select at least one image for the report.", "OK");
                return;
            }

            // TODO: Implement actual report generation logic here
            await DisplayAlert("Success", $"Report generated successfully with {_selectedImagePaths.Count} image(s)!", "OK");
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to generate report: {ex.Message}", "OK");
        }
    }
}
