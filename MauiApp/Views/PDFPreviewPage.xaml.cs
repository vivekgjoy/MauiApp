using MauiApp.Core.Interfaces;
using MauiApp.Core.Services;
using MauiApp.Core.Models;
using Microsoft.Maui.Platform;
#if ANDROID
using AndroidX.AppCompat.App;
using Android.OS;
using Android.Graphics;
#endif

namespace MauiApp.Views;

public partial class PDFPreviewPage : ContentPage
{
    private readonly IReportImageService _reportImageService;
    private readonly IPDFGeneratorService _pdfGeneratorService;
    private readonly IReportStorageService _reportStorageService;
    private int _imagesPerPage = 2;
    private List<ReportImage> _images = new();

    public PDFPreviewPage()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("PDFPreviewPage constructor started");
            
            InitializeComponent();
            System.Diagnostics.Debug.WriteLine("InitializeComponent completed");
            
            // Initialize services
            _reportImageService = ServiceHelper.GetService<IReportImageService>();
            _pdfGeneratorService = ServiceHelper.GetService<IPDFGeneratorService>();
            _reportStorageService = ServiceHelper.GetService<IReportStorageService>();

            System.Diagnostics.Debug.WriteLine("All services initialized successfully");

            // Handle safe area for status bar
            this.Loaded += OnPageLoaded;

            System.Diagnostics.Debug.WriteLine("PDFPreviewPage constructor completed successfully");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Critical error in PDFPreviewPage constructor: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            
            // Show error on page
            Device.BeginInvokeOnMainThread(() =>
            {
                Content = new Label
                {
                    Text = $"Error loading page: {ex.Message}",
                    TextColor = Colors.Red,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center
                };
            });
        }
    }

    protected override async void OnAppearing()
    {
        try
        {
            base.OnAppearing();
            System.Diagnostics.Debug.WriteLine("PDFPreviewPage OnAppearing started");
            
            // Initialize the page
            ImagesPerPageLabel.Text = _imagesPerPage.ToString();
            
            // Load and display images
            LoadImages();
            UpdatePreview();
            
            System.Diagnostics.Debug.WriteLine("PDFPreviewPage OnAppearing completed successfully");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in OnAppearing: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            await DisplayAlert("Error", $"Failed to load preview: {ex.Message}", "OK");
        }
    }

#if ANDROID
    private void OnPageLoaded(object sender, EventArgs e)
    {
        if (DeviceInfo.Platform == DevicePlatform.Android)
        {
            var activity = Platform.CurrentActivity as AppCompatActivity;
            if (activity != null && Build.VERSION.SdkInt >= BuildVersionCodes.Lollipop)
            {
                activity.Window.SetStatusBarColor(Android.Graphics.Color.ParseColor("#ED1C24"));
            }
        }
    }
#else
    private void OnPageLoaded(object sender, EventArgs e) { }
#endif

    private async Task OnBackClicked()
    {
        await Navigation.PopAsync();
    }

    private void LoadImages()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("LoadImages method started");
            
            _images = _reportImageService.ReportImages.ToList();
            TotalImagesLabel.Text = $"Total Images: {_images.Count}";
            
            System.Diagnostics.Debug.WriteLine($"Loaded {_images.Count} images from service");
            foreach (var img in _images)
            {
                System.Diagnostics.Debug.WriteLine($"Image: {img.ImagePath}, Comment: {img.Comment}, ID: {img.Id}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading images: {ex.Message}");
            _images = new List<ReportImage>();
            TotalImagesLabel.Text = "Error loading images";
        }
    }

    private void OnDecreaseImagesPerPage(object sender, EventArgs e)
    {
        if (_imagesPerPage > 1)
        {
            _imagesPerPage--;
            ImagesPerPageLabel.Text = _imagesPerPage.ToString();
            UpdatePreview();
        }
    }

    private void OnIncreaseImagesPerPage(object sender, EventArgs e)
    {
        if (_imagesPerPage < 10)
        {
            _imagesPerPage++;
            ImagesPerPageLabel.Text = _imagesPerPage.ToString();
            UpdatePreview();
        }
    }

    private void UpdatePreview()
    {
        try
        {
            PreviewContainer.Children.Clear();
            System.Diagnostics.Debug.WriteLine($"UpdatePreview called - Images count: {_images.Count}");

            if (_images.Count == 0)
            {
                var noImagesFrame = new Frame
                {
                    BackgroundColor = Colors.White,
                    CornerRadius = 8,
                    Padding = 20,
                    HasShadow = true
                };

                var noImagesLabel = new Label
                {
                    Text = "No images found.\nPlease add images in the previous page.",
                    FontSize = 16,
                    TextColor = Colors.Gray,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center,
                    HorizontalTextAlignment = TextAlignment.Center
                };

                noImagesFrame.Content = noImagesLabel;
                PreviewContainer.Children.Add(noImagesFrame);
                return;
            }

            var totalPages = (int)Math.Ceiling((double)_images.Count / _imagesPerPage);
            System.Diagnostics.Debug.WriteLine($"Creating {totalPages} pages with {_imagesPerPage} images per page");

            for (int pageIndex = 0; pageIndex < totalPages; pageIndex++)
            {
                var pageImages = _images.Skip(pageIndex * _imagesPerPage).Take(_imagesPerPage).ToList();
                var pagePreview = CreatePagePreview(pageIndex + 1, pageImages);
                PreviewContainer.Children.Add(pagePreview);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in UpdatePreview: {ex.Message}");
            var errorFrame = new Frame
            {
                BackgroundColor = Colors.White,
                CornerRadius = 8,
                Padding = 20,
                HasShadow = true
            };

            var errorLabel = new Label
            {
                Text = $"Error: {ex.Message}",
                FontSize = 14,
                TextColor = Colors.Red,
                HorizontalOptions = LayoutOptions.Center
            };

            errorFrame.Content = errorLabel;
            PreviewContainer.Children.Add(errorFrame);
        }
    }

    private Frame CreatePagePreview(int pageNumber, List<ReportImage> images)
    {
        // Main page frame with A4-like proportions
        var pageFrame = new Frame
        {
            BackgroundColor = Colors.White,
            BorderColor = Colors.LightGray,
            CornerRadius = 8,
            Padding = 0,
            HasShadow = true,
            Margin = new Thickness(0, 0, 0, 20),
            HeightRequest = 400 // Fixed height for consistent preview
        };

        // Page container with margins (simulating printable area)
        var pageContainer = new Grid
        {
            Padding = new Thickness(20, 30, 20, 30), // Top, bottom, left, right margins
            RowDefinitions = new RowDefinitionCollection
            {
                new RowDefinition { Height = GridLength.Auto }, // Page header
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }, // Images area
                new RowDefinition { Height = GridLength.Auto } // Page footer
            }
        };

        // Page header with margin guidelines
        var headerFrame = new Frame
        {
            BackgroundColor = Colors.LightGray,
            CornerRadius = 4,
            Padding = new Thickness(10, 5),
            HasShadow = false
        };

        var pageHeader = new Label
        {
            Text = $"Page {pageNumber}",
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.DarkGray,
            HorizontalOptions = LayoutOptions.Center
        };

        headerFrame.Content = pageHeader;
        pageContainer.Add(headerFrame, 0, 0);

        // Images area with margin guidelines
        var imagesArea = CreateImagesArea(images);
        pageContainer.Add(imagesArea, 0, 1);

        // Page footer with margin guidelines
        var footerFrame = new Frame
        {
            BackgroundColor = Colors.LightGray,
            CornerRadius = 4,
            Padding = new Thickness(10, 5),
            HasShadow = false
        };

        var pageFooter = new Label
        {
            Text = $"Images: {images.Count}",
            FontSize = 12,
            TextColor = Colors.DarkGray,
            HorizontalOptions = LayoutOptions.Center
        };

        footerFrame.Content = pageFooter;
        pageContainer.Add(footerFrame, 0, 2);

        pageFrame.Content = pageContainer;
        return pageFrame;
    }

    private Grid CreateImagesArea(List<ReportImage> images)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection
            {
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = new GridLength(10) }, // Spacing between images
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
            },
            RowDefinitions = new RowDefinitionCollection
            {
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }
            }
        };

        // Add images to grid (2 images per page)
        for (int i = 0; i < images.Count && i < 2; i++)
        {
            var image = images[i];
            var imageFrame = CreateImageFrame(image);
            
            var col = i * 2; // 0 or 2 (skipping the middle spacing column)
            grid.Add(imageFrame, col, 0);
        }

        return grid;
    }

    private Frame CreateImageFrame(ReportImage reportImage)
    {
        var imageFrame = new Frame
        {
            BackgroundColor = Colors.White,
            BorderColor = Colors.LightGray,
            CornerRadius = 4,
            Padding = 8,
            HasShadow = true,
            Margin = new Thickness(2)
        };

        var imageContainer = new StackLayout
        {
            Spacing = 8
        };

        // Image with proper aspect ratio
        var image = new Image
        {
            Source = ImageSource.FromFile(reportImage.ImagePath),
            Aspect = Aspect.AspectFit,
            BackgroundColor = Colors.LightGray,
            HeightRequest = 150 // Fixed height for consistent preview
        };

        imageContainer.Children.Add(image);

        // Add comment if available
        if (!string.IsNullOrEmpty(reportImage.Comment))
        {
            var commentLabel = new Label
            {
                Text = reportImage.Comment,
                FontSize = 12,
                TextColor = Colors.DarkGray,
                MaxLines = 2,
                LineBreakMode = LineBreakMode.TailTruncation,
                HorizontalOptions = LayoutOptions.Center,
                HorizontalTextAlignment = TextAlignment.Center
            };
            imageContainer.Children.Add(commentLabel);
        }

        imageFrame.Content = imageContainer;
        return imageFrame;
    }


    private async void OnGeneratePDFClicked(object sender, EventArgs e)
    {
        if (_images.Count == 0)
        {
            await DisplayAlert("No Images", "Please add some images before generating PDF.", "OK");
            return;
        }

        try
        {
            ShowProgress(true, "Publishing PDF...");
            
            // Log PDF generation start
            System.Diagnostics.Debug.WriteLine($"PDF Generation Started - Images: {_images.Count}, ImagesPerPage: {_imagesPerPage}");
            
            var pdfPath = await _pdfGeneratorService.GeneratePDFAsync(_images, _imagesPerPage);
            
            ShowProgress(false);
            
            if (!string.IsNullOrEmpty(pdfPath))
            {
                // Save to reports storage
                var savedPath = await _reportStorageService.SaveReportAsync(pdfPath, _images.Count, _imagesPerPage);
                
                // Log successful PDF generation
                System.Diagnostics.Debug.WriteLine($"PDF Generation Success - Path: {savedPath}, Images: {_images.Count}, ImagesPerPage: {_imagesPerPage}");
                await DisplayAlert("Success", $"PDF published successfully!\nYour report has been saved and is available in Reports History.", "OK");
                
                // Navigate back to main page
                await Shell.Current.GoToAsync("//MainPage");
            }
            else
            {
                // Log PDF generation failure
                System.Diagnostics.Debug.WriteLine($"PDF Generation Failed - Images: {_images.Count}, ImagesPerPage: {_imagesPerPage}");
                await DisplayAlert("Error", "Failed to generate PDF. Please try again.", "OK");
            }
        }
        catch (Exception ex)
        {
            ShowProgress(false);
            // Log PDF generation error
            System.Diagnostics.Debug.WriteLine($"PDF Generation Error - Images: {_images.Count}, ImagesPerPage: {_imagesPerPage}, Error: {ex.Message}");
            await DisplayAlert("Error", $"Failed to generate PDF: {ex.Message}", "OK");
        }
    }

    private void ShowProgress(bool show, string message = "Processing...")
    {
        ProgressOverlay.IsVisible = show;
        ProgressIndicator.IsRunning = show;
        ProgressLabel.Text = message;
        GeneratePDFButton.IsEnabled = !show;
    }
}
