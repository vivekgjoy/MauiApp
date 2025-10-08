using MauiApp.Core.Interfaces;
using MauiApp.Core.Services;
using MauiApp.Core.Models;
using Microsoft.Maui.Platform;
using System.Collections.ObjectModel;
using System.Windows.Input;
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
    private int _imagesPerPage = 4;
    private List<ReportImage> _images = new();

    public PDFPreviewPage()
    {
        try
        {
            InitializeComponent();
            
            // Simple test first
            System.Diagnostics.Debug.WriteLine("PDFPreviewPage constructor started");
            
            _reportImageService = ServiceHelper.GetService<IReportImageService>();
            _pdfGeneratorService = ServiceHelper.GetService<IPDFGeneratorService>();
            _reportStorageService = ServiceHelper.GetService<IReportStorageService>();

            System.Diagnostics.Debug.WriteLine("Services initialized");

            // Set up back navigation
            // NavigationBar.BackCommand = new Command(async () => await OnBackClicked());

            // Handle safe area for status bar
            this.Loaded += OnPageLoaded;

            System.Diagnostics.Debug.WriteLine("PDFPreviewPage constructor completed");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in PDFPreviewPage constructor: {ex.Message}");
        }
    }

    protected override async void OnAppearing()
    {
        try
        {
            base.OnAppearing();
            System.Diagnostics.Debug.WriteLine("PDFPreviewPage OnAppearing started");
            
            // Update test label
            TestLabel.Text = "Page loaded successfully!";
            TestLabel.TextColor = Colors.Green;
            
            // Add a small delay to ensure the service is ready
            await Task.Delay(500);
            
            LoadImages();
            UpdatePreview();
            
            System.Diagnostics.Debug.WriteLine("PDFPreviewPage OnAppearing completed");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in OnAppearing: {ex.Message}");
            TestLabel.Text = $"Error: {ex.Message}";
            TestLabel.TextColor = Colors.Red;
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
            if (_reportImageService == null)
            {
                System.Diagnostics.Debug.WriteLine("ReportImageService is null!");
                _images = new List<ReportImage>();
                TotalImagesLabel.Text = "Service Error";
                return;
            }
            
            _images = _reportImageService.ReportImages.ToList();
            TotalImagesLabel.Text = _images.Count.ToString();
            
            // Debug logging
            System.Diagnostics.Debug.WriteLine($"PDFPreviewPage: Loaded {_images.Count} images from service");
            System.Diagnostics.Debug.WriteLine($"Service instance hash: {_reportImageService.GetHashCode()}");
            foreach (var img in _images)
            {
                System.Diagnostics.Debug.WriteLine($"Image: {img.ImagePath}, Comment: {img.Comment}, ID: {img.Id}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading images: {ex.Message}");
            _images = new List<ReportImage>();
            TotalImagesLabel.Text = "Error: " + ex.Message;
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
                var noImagesLabel = new Label
                {
                    Text = "No images found. Please add images in the previous page.",
                    FontSize = 16,
                    TextColor = Colors.Red,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center
                };
                PreviewContainer.Children.Add(noImagesLabel);
                
                // Add debug info
                var debugLabel = new Label
                {
                    Text = $"Debug: Service null? {_reportImageService == null}, Images in service: {_reportImageService?.ReportImages?.Count ?? -1}",
                    FontSize = 12,
                    TextColor = Colors.Red,
                    HorizontalOptions = LayoutOptions.Center
                };
                PreviewContainer.Children.Add(debugLabel);
                
                System.Diagnostics.Debug.WriteLine("No images found - showing empty state");
                return;
            }

            var totalPages = (int)Math.Ceiling((double)_images.Count / _imagesPerPage);

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
            var errorLabel = new Label
            {
                Text = $"Error: {ex.Message}",
                FontSize = 14,
                TextColor = Colors.Red,
                HorizontalOptions = LayoutOptions.Center
            };
            PreviewContainer.Children.Add(errorLabel);
        }
    }

    private Frame CreatePagePreview(int pageNumber, List<ReportImage> images)
    {
        var pageFrame = new Frame
        {
            BackgroundColor = Colors.White,
            BorderColor = Colors.LightGray,
            CornerRadius = 8,
            Padding = 20,
            HasShadow = true,
            Margin = new Thickness(0, 0, 0, 20)
        };

        var pageContainer = new Grid
        {
            RowDefinitions = new RowDefinitionCollection
            {
                new RowDefinition { Height = GridLength.Auto }, // Page header
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) } // Images grid
            }
        };

        // Page header
        var pageHeader = new Label
        {
            Text = $"Page {pageNumber}",
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.Gray,
            HorizontalOptions = LayoutOptions.Center,
            Margin = new Thickness(0, 0, 0, 10)
        };
        pageContainer.Add(pageHeader, 0, 0);

        // Images grid
        var imagesGrid = CreateImagesGrid(images);
        pageContainer.Add(imagesGrid, 0, 1);

        pageFrame.Content = pageContainer;
        return pageFrame;
    }

    private Grid CreateImagesGrid(List<ReportImage> images)
    {
        var (cols, rows) = CalculateGridDimensions(images.Count);
        
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection(),
            RowDefinitions = new RowDefinitionCollection()
        };

        // Create columns
        for (int i = 0; i < cols; i++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        // Create rows
        for (int i = 0; i < rows; i++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        }

        // Add images to grid
        for (int i = 0; i < images.Count; i++)
        {
            var image = images[i];
            var imageFrame = CreateImageFrame(image);
            
            var row = i / cols;
            var col = i % cols;
            
            grid.Add(imageFrame, col, row);
        }

        return grid;
    }

    private Frame CreateImageFrame(ReportImage reportImage)
    {
        var imageFrame = new Frame
        {
            BackgroundColor = Colors.LightGray,
            CornerRadius = 4,
            Padding = 5,
            HasShadow = false,
            Margin = new Thickness(2)
        };

        var imageContainer = new StackLayout
        {
            Spacing = 5
        };

        var image = new Image
        {
            Source = ImageSource.FromFile(reportImage.ImagePath),
            Aspect = Aspect.AspectFit,
            BackgroundColor = Colors.White
        };

        imageContainer.Children.Add(image);

        // Add comment if available
        if (!string.IsNullOrEmpty(reportImage.Comment))
        {
            var commentLabel = new Label
            {
                Text = reportImage.Comment,
                FontSize = 10,
                TextColor = Colors.Gray,
                MaxLines = 2,
                LineBreakMode = LineBreakMode.TailTruncation,
                HorizontalOptions = LayoutOptions.Center
            };
            imageContainer.Children.Add(commentLabel);
        }

        imageFrame.Content = imageContainer;
        return imageFrame;
    }

    private (int cols, int rows) CalculateGridDimensions(int imageCount)
    {
        var cols = (int)Math.Ceiling(Math.Sqrt(imageCount));
        var rows = (int)Math.Ceiling((double)imageCount / cols);
        return (cols, rows);
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
            ShowProgress(true, "Generating PDF...");
            
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
                await DisplayAlert("Success", $"PDF generated successfully!\nSaved to Reports History", "OK");
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
