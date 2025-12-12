using MauiApp.Core.Interfaces;
using MauiApp.Core.Models;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Controls;
#if ANDROID
using AndroidX.AppCompat.App;
using Android.OS;
using Android.Graphics;
using AndroidX.Core.View;
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
        //NavigationBar.BackCommand = new Command(async () => await OnBackClicked());

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
            
            // Handle safe area insets for bottom navigation bar
            ApplySafeAreaInsets();
        }
#else
        // No status bar color setting needed for other platforms
#endif
    }

#if ANDROID
    private void ApplySafeAreaInsets()
    {
        try
        {
            var activity = Platform.CurrentActivity as AppCompatActivity;
            if (activity == null) return;

            // Find the bottom buttons Grid by name
            var bottomGrid = this.FindByName<Grid>("BottomButtonsGrid");
            if (bottomGrid == null)
            {
                // Fallback: Try to find it by traversing the visual tree
                // Wait a bit for the visual tree to be ready
                Device.BeginInvokeOnMainThread(async () =>
                {
                    await Task.Delay(100);
                    ApplySafeAreaInsets();
                });
                return;
            }

            // Use a more reliable method to get window insets
            int bottomInset = 0;

            // Method 1: Use ViewCompat (most reliable for Android 12+)
            var rootView = activity.Window?.DecorView?.RootView;
            if (rootView != null)
            {
                var insets = AndroidX.Core.View.ViewCompat.GetRootWindowInsets(rootView);
                if (insets != null)
                {
                    var navigationBarInsets = insets.GetInsets(AndroidX.Core.View.WindowInsetsCompat.Type.NavigationBars());
                    bottomInset = navigationBarInsets.Bottom;
                }
            }

            // Method 2: Fallback - calculate from window insets directly
            if (bottomInset == 0 && activity.Window != null)
            {
                var decorView = activity.Window.DecorView;
                if (decorView != null && Build.VERSION.SdkInt >= BuildVersionCodes.R)
                {
                    var windowInsets = decorView.RootWindowInsets;
                    if (windowInsets != null)
                    {
                        var insets = windowInsets.GetInsets(Android.Views.WindowInsets.Type.NavigationBars());
                        bottomInset = insets.Bottom;
                    }
                }
            }

            // Convert pixels to device-independent units
            var density = activity.Resources?.DisplayMetrics?.Density ?? 1f;
            var bottomPaddingDp = bottomInset / density;

            // Apply padding to bottom buttons to prevent overlap
            MainThread.BeginInvokeOnMainThread(() =>
            {
                var currentPadding = bottomGrid.Padding;
                var extraSpacing = 25.0;
                var minimumPadding = 30.0;
                var newBottomPadding = bottomPaddingDp > 0 
                    ? Math.Max(minimumPadding, bottomPaddingDp + extraSpacing)
                    : minimumPadding;
                
                bottomGrid.Padding = new Thickness(
                    currentPadding.Left,
                    currentPadding.Top,
                    currentPadding.Right,
                    newBottomPadding
                );
            });
        }
        catch (Exception ex)
        {
            // Silently fail - safe area handling is best effort
            System.Diagnostics.Debug.WriteLine($"Failed to apply safe area insets: {ex.Message}");
        }
    }
#endif

    private async Task OnBackClicked()
    {
        await HandleBackNavigation();
    }

    private async void OnBackButtonClicked(object sender, EventArgs e)
    {
        await HandleBackNavigation();
    }

    protected override bool OnBackButtonPressed()
    {
        // Handle hardware back button
        Device.BeginInvokeOnMainThread(async () =>
        {
            await HandleBackNavigation();
        });
        return true; // Prevent default back behavior
    }

    private async Task HandleBackNavigation()
    {
        // Check if there are any unsaved changes (comment text)
        if (!string.IsNullOrEmpty(CommentEditor.Text?.Trim()))
        {
            var result = await DisplayAlert(
                "Unsaved Changes", 
                "You have unsaved changes. Are you sure you want to go back? You will lose your comment.", 
                "Yes, Go Back", 
                "Cancel");
            
            if (!result)
            {
                return; // User cancelled, stay on current page
            }
        }
        
        // Navigate back
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

            // 1. Save the image/comment as before
            if (_isEditingExisting && !string.IsNullOrEmpty(_imageId))
            {
                _reportImageService.UpdateImageComment(_imageId, CommentEditor.Text?.Trim() ?? string.Empty);
                _reportImageService.UpdateImagePath(_imageId, _imagePath);
            }
            else
            {
                var reportImage = new ReportImage
                {
                    ImagePath = _imagePath,
                    Comment = CommentEditor.Text?.Trim() ?? string.Empty,
                    CreatedAt = DateTime.Now
                };
                _reportImageService.AddImage(reportImage);
            }

            // 2. Prune ALL intermediate pages above AddReportPage
            while (Navigation.NavigationStack.Count > 1 &&
                Navigation.NavigationStack[Navigation.NavigationStack.Count - 2] is not AddReportPage)
            {
                var pageToRemove = Navigation.NavigationStack[Navigation.NavigationStack.Count - 2];
                Navigation.RemovePage(pageToRemove);
            }

            // 3. If AddReportPage is just below us: pop there
            if (Navigation.NavigationStack.Count > 1 &&
                Navigation.NavigationStack[Navigation.NavigationStack.Count - 2] is AddReportPage)
            {
                await Navigation.PopAsync(animated: false);
            }
            else
            {
                // Fallback: If no AddReportPage in stack, navigate absolutely
                await Shell.Current.GoToAsync("//AddReportPage");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to save image: {ex.Message}", "OK");
        }
    }
}
