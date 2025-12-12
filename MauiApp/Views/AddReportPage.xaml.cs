using MauiApp.Core.Interfaces;
using MauiApp.Core.Services;
using MauiApp.ViewModels;
using MauiApp.Core.Models;
using System.Linq;

namespace MauiApp.Views;

public partial class AddReportPage : ContentPage
{
    private readonly IBottomSheetService _bottomSheetService;
    private readonly IReportImageService _reportImageService;
    private const int MaxImages = 10;
	private bool _isFirstLoad = true;
    private bool _isEditingImage = false; // Flag to prevent multiple edit clicks
    private bool _isNavigatingAway = false; // Flag to skip UpdateImagesCollection when navigating
    private bool _isShowingImageSource = false; // Flag to prevent rapid multiple image source selections
    
    // State preservation properties
    private string _savedTitle = string.Empty;
    private string _savedDescription = string.Empty;

    public AddReportPage()
    {
        InitializeComponent();
        _bottomSheetService = ServiceHelper.GetService<IBottomSheetService>();
        _reportImageService = ServiceHelper.GetService<IReportImageService>();
        
        // Subscribe to text change events to save state
        TitleEntry.TextChanged += OnTitleTextChanged;
        DescriptionEditor.TextChanged += OnDescriptionTextChanged;
    }

	protected override async void OnAppearing()
	{
		base.OnAppearing();

		// Set navigation bar styling
		SetNavigationBarStyling();

		// Only clear on first load
		if (_isFirstLoad)
		{
			_isFirstLoad = false;
			_savedTitle = string.Empty;
			_savedDescription = string.Empty;
			TitleEntry.Text = string.Empty;
			DescriptionEditor.Text = string.Empty;
		}
		else
		{
			// Restore saved form data when coming back (e.g., after picking image)
			if (!string.IsNullOrEmpty(_savedTitle))
				TitleEntry.Text = _savedTitle;
			if (!string.IsNullOrEmpty(_savedDescription))
				DescriptionEditor.Text = _savedDescription;
		}

		// Skip UpdateImagesCollection if we're navigating away immediately
		// This prevents the delay when selecting an image
		if (!_isNavigatingAway)
		{
			// Initialize images collection
			await UpdateImagesCollection();
		}
		else
		{
			// Reset flag after skipping
			_isNavigatingAway = false;
		}
	}

    private void OnTitleTextChanged(object sender, TextChangedEventArgs e)
    {
        _savedTitle = e.NewTextValue ?? string.Empty;
    }

    private void OnDescriptionTextChanged(object sender, TextChangedEventArgs e)
    {
        _savedDescription = e.NewTextValue ?? string.Empty;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        
        // Save current form state when navigating away
        _savedTitle = TitleEntry.Text ?? string.Empty;
        _savedDescription = DescriptionEditor.Text ?? string.Empty;
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
        // Prevent rapid multiple taps
        if (_isShowingImageSource) return;
        _isShowingImageSource = true;

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
			
			// Save state before opening image source selection
			_savedTitle = TitleEntry.Text ?? string.Empty;
			_savedDescription = DescriptionEditor.Text ?? string.Empty;

			await Navigation.PushModalAsync(imageSourcePage);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to show image selection options: {ex.Message}", "OK");
        }
        finally
        {
            _isShowingImageSource = false;
        }
    }

    private async Task HandleImageSelection(string option)
    {
        FileResult photo = null;

        try
        {
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
                // 1. SHOW FULLSCREEN OVERLAY IMMEDIATELY
                MainThread.BeginInvokeOnMainThread(() => 
                {
                    ProcessingOverlay.IsVisible = true;
                });

                // Set flag to skip UpdateImagesCollection in OnAppearing
                // This prevents the delay when OnAppearing is called after modal pop
                _isNavigatingAway = true;
                
                try
                {
                    // 2. Pop modal (no animation)
                    // This will trigger OnAppearing, but it will skip UpdateImagesCollection due to the flag
                    while (Navigation.ModalStack.Count > 0)
                    {
                        await Navigation.PopModalAsync(false); // false = no animation
                    }
                    
                    // 3. Navigate to editor
                    await NavigateToImageEditing(photo.FullPath);
                    
                    // 4. HIDE OVERLAY ONCE EDITOR IS LOADED
                    // This is done inside NavigateToImageEditing, after SetImageSourceAsync completes
                }
                finally
                {
                    // Critical: always reset flag to prevent permanent breakage
                    // If this isn't reset, UpdateImagesCollection will be permanently skipped
                    _isNavigatingAway = false;
                }
            }
            else
            {
                // If no photo selected, dismiss modal normally
                while (Navigation.ModalStack.Count > 0)
                {
                    await Navigation.PopModalAsync();
                }
            }
        }
        catch (Exception ex)
        {
            // Ensure modal is dismissed even on error
            while (Navigation.ModalStack.Count > 0)
            {
                try { await Navigation.PopModalAsync(); } catch { break; }
            }
            
            // Ensure flag is reset on error
            _isNavigatingAway = false;
            
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

        // Delete button - increased size for better visibility
        var deleteButton = new Button
        {
            Text = "×",
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White,
            BackgroundColor = Color.FromArgb("#ED1C24"), // Red background
            CornerRadius = 10,
            WidthRequest = 20,
            HeightRequest = 20,
            HorizontalOptions = LayoutOptions.End,
            VerticalOptions = LayoutOptions.Start,
            Margin = new Thickness(0, 4, 4, 0), // Top: 4, Right: 4 (matching - both same value)
            Padding = new Thickness(0),
            MinimumWidthRequest = 20,
            MinimumHeightRequest = 20
        };

        // Edit button - increased size with progress indicator container
        // Positioned to the left of delete button with proper spacing
        var editButtonContainer = new Grid
        {
            WidthRequest = 20,
            HeightRequest = 20,
            HorizontalOptions = LayoutOptions.End,
            VerticalOptions = LayoutOptions.Start,
            Margin = new Thickness(0, 4, 28, 0) // 28px from right (20px button + 4px gap + 4px delete margin)
        };

        var editButton = new Button
        {
            Text = "✏️",
            FontSize = 12,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White,
            BackgroundColor = Color.FromArgb("#4CAF50"), // Green background for edit
            CornerRadius = 10,
            WidthRequest = 20,
            HeightRequest = 20,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Padding = new Thickness(0),
            MinimumWidthRequest = 20,
            MinimumHeightRequest = 20
        };

        // Progress indicator overlay (initially hidden)
        var progressIndicator = new ActivityIndicator
        {
            IsRunning = false,
            IsVisible = false,
            Color = Colors.White,
            WidthRequest = 12,
            HeightRequest = 12,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };

        editButtonContainer.Children.Add(editButton);
        editButtonContainer.Children.Add(progressIndicator);

        // Add click handlers
        deleteButton.Clicked += (s, e) => OnRemoveImageClicked(s, e, reportImage.Id);
        editButton.Clicked += (s, e) => OnEditImageClicked(s, e, reportImage, editButtonContainer);

        // Add to grid - edit button first (behind), then delete button (on top)
        // This ensures proper z-order with delete button visible on top
        grid.Children.Add(image);
        grid.Children.Add(editButtonContainer);
        grid.Children.Add(deleteButton);
        frame.Content = grid;

        return frame;
    }

    private async void OnRemoveImageClicked(object sender, EventArgs e, string imageId)
    {
        _reportImageService.RemoveImage(imageId);
        await UpdateImagesCollection();
    }

    private async void OnEditImageClicked(object sender, EventArgs e, ReportImage reportImage, Grid? buttonContainer = null)
    {
        // Prevent multiple simultaneous clicks
        if (_isEditingImage)
        {
            return;
        }

        Button? editButton = sender as Button;
        ActivityIndicator? progressIndicator = null;

        try
        {
            _isEditingImage = true;
            
            // Disable the button and show progress indicator immediately
            if (editButton != null)
            {
                editButton.IsEnabled = false;
                editButton.Opacity = 0.6; // Dim the button to show it's disabled
                
                // Show progress indicator if available
                if (buttonContainer != null)
                {
                    progressIndicator = buttonContainer.Children.OfType<ActivityIndicator>().FirstOrDefault();
                    if (progressIndicator != null)
                    {
                        progressIndicator.IsRunning = true;
                        progressIndicator.IsVisible = true;
                    }
                }
            }

            // Store image path and ID for callback
            string imagePath = reportImage.ImagePath;
            string imageId = reportImage.Id;

            // Create page instance first so we can reference it in the callback
            MauiApp.ImageEditor.SkiaSharpImageEditorPage? imageEditorPage = null;
            
            // Create page callback (lightweight operation)
            Action<string> onImageSaved = async (savedImagePath) =>
            {
                // Get the current navigation and image editor page
                var navigation = imageEditorPage?.Navigation ?? Navigation;
                var currentEditorPage = imageEditorPage;
                
                // Show progress overlay on image editor page BEFORE navigation
                if (currentEditorPage != null)
                {
                    await MainThread.InvokeOnMainThreadAsync(async () =>
                    {
                        await ShowProgressOnImageEditorPage(currentEditorPage);
                    });
                    
                    // Small delay to ensure progress overlay is visible
                    await Task.Delay(50);
                }
                
                // Set flag BEFORE popping to prevent OnAppearing from updating images collection
                _isNavigatingAway = true;
                
                try
                {
                    // Update the image path in the service first
                    _reportImageService.UpdateImagePath(imageId, savedImagePath);
                    
                    // Create comment page
                    var commentPage = new ImageCommentPage
                    {
                        ImagePath = savedImagePath,
                        ImageId = imageId,
                        IsEditingExisting = true
                    };
                    
                    // Navigate directly: Insert comment page before AddReportPage, then pop twice
                    // This way AddReportPage never appears
                    if (navigation.NavigationStack.Count >= 2)
                    {
                        // Insert comment page before AddReportPage (which is second to last)
                        var addReportPageIndex = navigation.NavigationStack.Count - 2;
                        navigation.InsertPageBefore(commentPage, navigation.NavigationStack[addReportPageIndex]);
                        
                        // Pop image editor (no animation)
                        await navigation.PopAsync(false).ConfigureAwait(false);
                        
                        // Pop AddReportPage (no animation) - now comment page is on top
                        await navigation.PopAsync(false).ConfigureAwait(false);
                    }
                    else
                    {
                        // Fallback: pop and push if navigation stack is unexpected
                        await navigation.PopAsync(false).ConfigureAwait(false);
                        await navigation.PushAsync(commentPage, false).ConfigureAwait(false);
                    }
                }
                finally
                {
                    // Reset flag after navigation is complete
                    _isNavigatingAway = false;
                }
            };

            // Create page instance (should be fast - just UI structure)
            imageEditorPage = new MauiApp.ImageEditor.SkiaSharpImageEditorPage(onImageSaved);
            
            // Navigate IMMEDIATELY - start navigation without blocking
            var navigationTask = Navigation.PushAsync(imageEditorPage);
            
            // Yield control to UI thread immediately so UI can update
            await Task.Yield();
            
            // Continue image loading setup in background
            _ = Task.Run(async () =>
            {
                try
                {
                    // Wait for navigation to complete first (but don't block UI thread)
                    await navigationTask.ConfigureAwait(false);
                    
                    // Small delay to ensure page is fully rendered
                    await Task.Delay(50).ConfigureAwait(false);
                    
                    // Now load image asynchronously after navigation is complete
                    await MainThread.InvokeOnMainThreadAsync(async () =>
                    {
                        await imageEditorPage.SetImageSourceAsync(imagePath).ConfigureAwait(false);
                    }).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    await MainThread.InvokeOnMainThreadAsync(async () =>
                    {
                        try
                        {
                            await imageEditorPage.DisplayAlert("Error", $"Failed to load image: {ex.Message}", "OK").ConfigureAwait(false);
                        }
                        catch
                        {
                            // Page might not be ready yet, ignore
                        }
                    }).ConfigureAwait(false);
                }
                finally
                {
                    // Hide progress indicator and re-enable button on UI thread
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        if (progressIndicator != null)
                        {
                            progressIndicator.IsRunning = false;
                            progressIndicator.IsVisible = false;
                        }
                        if (editButton != null)
                        {
                            editButton.IsEnabled = true;
                            editButton.Opacity = 1.0;
                        }
                        _isEditingImage = false;
                    }).ConfigureAwait(false);
                }
            });
        }
        catch (Exception ex)
        {
            _isEditingImage = false;
            
            // Hide progress indicator and re-enable button
            if (progressIndicator != null)
            {
                progressIndicator.IsRunning = false;
                progressIndicator.IsVisible = false;
            }
            if (editButton != null)
            {
                editButton.IsEnabled = true;
                editButton.Opacity = 1.0;
            }
            
            await DisplayAlert("Error", $"Failed to open image editor: {ex.Message}", "OK").ConfigureAwait(false);
        }
    }

    private async Task NavigateToImageEditing(string imagePath)
    {
        try
        {
            var imageEditorPage = new MauiApp.ImageEditor.SkiaSharpImageEditorPage(async (savedImagePath) =>
            {
                await Navigation.PopAsync();
                var commentPage = new ImageCommentPage { ImagePath = savedImagePath };
                await Navigation.PushAsync(commentPage);
            });

            // Push editor page
            await Navigation.PushAsync(imageEditorPage);

            // Now load image in background
            _ = Task.Run(async () =>
            {
                try
                {
                    await MainThread.InvokeOnMainThreadAsync(async () =>
                    {
                        await imageEditorPage.SetImageSourceAsync(imagePath);
                    });

                    // SUCCESS: Hide overlay only when image is loaded and visible
                    await Task.Delay(300); // Small delay for smoothness
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        ProcessingOverlay.IsVisible = false;
                    });
                }
                catch (Exception ex)
                {
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        ProcessingOverlay.IsVisible = false;
                        await imageEditorPage.DisplayAlert("Error", $"Failed to load image: {ex.Message}", "OK");
                    });
                }
            });
        }
        catch (Exception ex)
        {
            ProcessingOverlay.IsVisible = false;
            await DisplayAlert("Error", $"Failed to open editor: {ex.Message}", "OK");
        }
    }

    private async Task<string> NormalizeImageOrientation(string imagePath)
    {
        try
        {
            using var stream = File.OpenRead(imagePath);
            using var codec = SkiaSharp.SKCodec.Create(stream);
            
            if (codec == null)
                return imagePath;
            
            var orientation = codec.EncodedOrigin;
            var info = codec.Info;
            
            if (orientation == SkiaSharp.SKEncodedOrigin.TopLeft)
                return imagePath;
            
            stream.Position = 0;
            using var originalBitmap = SkiaSharp.SKBitmap.Decode(stream);
            
            if (originalBitmap == null)
                return imagePath;
            
            bool isRotated = orientation == SkiaSharp.SKEncodedOrigin.LeftTop ||
                             orientation == SkiaSharp.SKEncodedOrigin.RightTop ||
                             orientation == SkiaSharp.SKEncodedOrigin.LeftBottom ||
                             orientation == SkiaSharp.SKEncodedOrigin.RightBottom;
            
            int outputWidth = isRotated ? info.Height : info.Width;
            int outputHeight = isRotated ? info.Width : info.Height;
            
            using var normalizedBitmap = new SkiaSharp.SKBitmap(outputWidth, outputHeight);
            using var canvas = new SkiaSharp.SKCanvas(normalizedBitmap);
            canvas.Clear(SkiaSharp.SKColors.White);
            
            canvas.Save();
            float centerX = outputWidth / 2f;
            float centerY = outputHeight / 2f;
            canvas.Translate(centerX, centerY);
            
            switch (orientation)
            {
                case SkiaSharp.SKEncodedOrigin.TopRight:
                    canvas.Scale(-1, 1);
                    break;
                case SkiaSharp.SKEncodedOrigin.BottomRight:
                    canvas.RotateDegrees(180);
                    break;
                case SkiaSharp.SKEncodedOrigin.BottomLeft:
                    canvas.RotateDegrees(180);
                    canvas.Scale(-1, 1);
                    break;
                case SkiaSharp.SKEncodedOrigin.LeftTop:
                    canvas.RotateDegrees(-90);
                    canvas.Scale(-1, 1);
                    break;
                case SkiaSharp.SKEncodedOrigin.RightTop:
                    canvas.RotateDegrees(90);
                    break;
                case SkiaSharp.SKEncodedOrigin.RightBottom:
                    canvas.RotateDegrees(90);
                    canvas.Scale(-1, 1);
                    break;
                case SkiaSharp.SKEncodedOrigin.LeftBottom:
                    canvas.RotateDegrees(-90);
                    break;
            }
            
            canvas.Translate(-centerX, -centerY);
            
            var destRect = new SkiaSharp.SKRect(0, 0, outputWidth, outputHeight);
            canvas.DrawBitmap(originalBitmap, destRect);
            canvas.Restore();
            
            var cachePath = FileSystem.CacheDirectory;
            var normalizedFileName = $"normalized_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";
            var normalizedImagePath = Path.Combine(cachePath, normalizedFileName);
            
            using var image = SkiaSharp.SKImage.FromBitmap(normalizedBitmap);
            using var data = image.Encode(SkiaSharp.SKEncodedImageFormat.Jpeg, 95);
            using var fileStream = File.Create(normalizedImagePath);
            data.SaveTo(fileStream);
            
            return normalizedImagePath;
        }
        catch
        {
            return imagePath;
        }
    }


    private async void OnBackClicked(object sender, EventArgs e)
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
        // Check if there are any unsaved changes
        if (!string.IsNullOrEmpty(TitleEntry.Text?.Trim()) || 
            !string.IsNullOrEmpty(DescriptionEditor.Text?.Trim()) || 
            _reportImageService.ReportImages.Count > 0)
        {
            var result = await DisplayAlert(
                "Unsaved Changes", 
                "You have unsaved changes. Are you sure you want to go back? You will lose your work.", 
                "Yes, Go Back", 
                "Cancel");
            
            if (!result)
            {
                return; // User cancelled, stay on current page
            }
        }
        
        // Navigate back to main page
        try
        {
            await Shell.Current.GoToAsync("//MainPage");
        }
        catch (Exception ex)
        {
            // Fallback navigation
            await Shell.Current.GoToAsync("//MainPage");
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

    private async Task ShowProgressOnImageEditorPage(ContentPage imageEditorPage)
    {
        // Create a progress overlay
        var progressOverlay = new Grid
        {
            BackgroundColor = Color.FromArgb("#80000000"), // Semi-transparent black
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            ZIndex = 9999 // Ensure it's on top
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
            Text = "Saving image...",
            FontSize = 16,
            TextColor = Colors.White,
            HorizontalOptions = LayoutOptions.Center
        };

        progressStack.Children.Add(activityIndicator);
        progressStack.Children.Add(progressLabel);
        progressFrame.Content = progressStack;
        progressOverlay.Children.Add(progressFrame);

        // Add overlay to the image editor page
        if (imageEditorPage.Content is Grid mainGrid)
        {
            mainGrid.Children.Add(progressOverlay);
        }
        else if (imageEditorPage.Content is Microsoft.Maui.Controls.View content)
        {
            // If content is not a Grid, wrap it
            var wrapperGrid = new Grid();
            wrapperGrid.Children.Add(content);
            wrapperGrid.Children.Add(progressOverlay);
            imageEditorPage.Content = wrapperGrid;
        }
    }
}
