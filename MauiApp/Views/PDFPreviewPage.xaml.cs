using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MauiApp.Core.Models;
using MauiApp.Core.Interfaces;
using MauiApp.Core.Services;
using Microsoft.Maui.ApplicationModel.DataTransfer;

using MauiColor = Microsoft.Maui.Graphics.Color; // ? Fix for Color ambiguity

namespace MauiApp.Views
{
    public partial class PDFPreviewPage : ContentPage
    {
        private readonly List<ReportImage> _reportImages;
        private int _imagesPerPage = 6; // Default 2x3 grid
        private readonly IReportImageService _reportImageService;
        private readonly IPDFGeneratorService _pdfGeneratorService;
        private readonly IReportStorageService _reportStorageService;

        public PDFPreviewPage()
        {
            InitializeComponent();
            _reportImageService = ServiceHelper.GetService<IReportImageService>();
            _pdfGeneratorService = ServiceHelper.GetService<IPDFGeneratorService>();
            _reportStorageService = ServiceHelper.GetService<IReportStorageService>();
            _reportImages = _reportImageService.ReportImages.ToList();
            BuildPreviewPages();
            UpdateUI();
        }

        private void BuildPreviewPages()
        {
            if (PreviewContainer == null) return;

            // Clear existing pages
            PreviewContainer.Children.Clear();

            int totalPages = (int)Math.Ceiling((double)_reportImages.Count / _imagesPerPage);
            for (int i = 0; i < totalPages; i++)
            {
                var imagesForPage = _reportImages
                    .Skip(i * _imagesPerPage)
                    .Take(_imagesPerPage)
                    .ToList();

                var pageView = CreatePagePreview(i + 1, imagesForPage);
                PreviewContainer.Children.Add(pageView);
            }
        }

        // ?? PAGE PREVIEW LAYOUT (Header + 2x3 Grid + Footer)
        private Frame CreatePagePreview(int pageNumber, List<ReportImage> images)
        {
            var pageFrame = new Frame
            {
                BackgroundColor = Colors.White,
                BorderColor = Colors.LightGray,
                CornerRadius = 8,
                Padding = 0,
                HasShadow = true,
                Margin = new Thickness(0, 0, 0, 20)
            };

            var pageContainer = new Grid
            {
                RowDefinitions =
                {
                    new RowDefinition { Height = GridLength.Auto }, // Header
                    new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }, // Image grid
                    new RowDefinition { Height = GridLength.Auto }  // Footer
                },
                Padding = new Thickness(20)
            };

            // ?? HEADER SECTION
            var headerLayout = new VerticalStackLayout
            {
                Spacing = 2,
                HorizontalOptions = LayoutOptions.Fill
            };

            headerLayout.Children.Add(new Label
            {
                Text = "EXTERNAL VISUAL INSPECTION PHOTOS",
                FontSize = 16,
                FontAttributes = FontAttributes.Bold,
                TextColor = MauiColor.FromArgb("#ED1C24"),
                HorizontalOptions = LayoutOptions.Center
            });

            headerLayout.Children.Add(new Label
            {
                Text = "FOTOS DE INSPECCI�N VISUAL EXTERNA",
                FontSize = 12,
                FontAttributes = FontAttributes.Bold,
                TextColor = Colors.Black,
                HorizontalOptions = LayoutOptions.Center
            });

            headerLayout.Children.Add(new Label
            {
                Text = $"Report Number � AT/IVI/VM34/002/{pageNumber:D2}",
                FontSize = 11,
                TextColor = Colors.Black,
                HorizontalOptions = LayoutOptions.Center
            });

            headerLayout.Children.Add(new Label
            {
                Text = "INSPECTION PERIOD: 22/3/2021 � 16/4/2021",
                FontSize = 11,
                TextColor = MauiColor.FromArgb("#ED1C24"),
                HorizontalOptions = LayoutOptions.Center
            });

            pageContainer.Add(headerLayout, 0, 0);

            // ?? IMAGES GRID (2x3)
            var imageGrid = new Grid
            {
                RowSpacing = 10,
                ColumnSpacing = 10
            };

            // 3 rows � 2 columns
            // Calculate grid layout based on number of images
            int imageCount = images.Count;
            
            if (imageCount == 1)
            {
                // Single image - center it
                imageGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                imageGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                imageGrid.HorizontalOptions = LayoutOptions.Center;
                imageGrid.VerticalOptions = LayoutOptions.Center;
                
                // Add image block directly - it will be centered in the grid
                imageGrid.Add(CreateImageBlock(images[0], 1), 0, 0);
            }
            else
            {
                // Multiple images - create dynamic grid
                // Always use 2 columns, calculate rows needed
                int columns = 2;
                int rows = (int)Math.Ceiling((double)imageCount / columns);
                
                for (int i = 0; i < rows; i++)
                    imageGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                for (int j = 0; j < columns; j++)
                    imageGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                for (int i = 0; i < imageCount; i++)
                {
                    var row = i / columns;
                    var col = i % columns;
                    imageGrid.Add(CreateImageBlock(images[i], i + 1), col, row);
                }
            }

            pageContainer.Add(imageGrid, 0, 1);

            // ?? FOOTER SECTION
            var footerLayout = new VerticalStackLayout
            {
                Spacing = 2,
                HorizontalOptions = LayoutOptions.Center
            };

            footerLayout.Children.Add(new Label
            {
                Text = "A-STAR TESTING & INSPECTION (S) PTE LTD",
                FontSize = 11,
                FontAttributes = FontAttributes.Bold,
                TextColor = MauiColor.FromArgb("#ED1C24"),
                HorizontalOptions = LayoutOptions.Center
            });

            footerLayout.Children.Add(new Label
            {
                Text = "No.05, Soon Lee Street | Pioneer Point #03-35/37 | Singapore 627607",
                FontSize = 10,
                TextColor = Colors.Black,
                HorizontalOptions = LayoutOptions.Center
            });

            footerLayout.Children.Add(new Label
            {
                Text = "Tel: (65)62611662 / 91835902 | Fax: (65)62611663 | Web: www.astartesting.com.sg",
                FontSize = 10,
                TextColor = Colors.Gray,
                HorizontalOptions = LayoutOptions.Center
            });

            pageContainer.Add(footerLayout, 0, 2);
            pageFrame.Content = pageContainer;
            return pageFrame;
        }

        // ?? INDIVIDUAL IMAGE BLOCK (Label + Image + Caption)
        private Frame CreateImageBlock(ReportImage reportImage, int index)
        {
            var block = new Frame
            {
                BorderColor = Colors.LightGray,
                CornerRadius = 4,
                Padding = 6,
                HasShadow = true
            };

            var stack = new VerticalStackLayout
            {
                Spacing = 4
            };

            // Label above image (like �001.A�)
            stack.Children.Add(new Label
            {
                Text = $"001.{(char)('A' + index - 1)}",
                FontSize = 11,
                FontAttributes = FontAttributes.Bold,
                TextColor = Colors.Black,
                HorizontalOptions = LayoutOptions.Center
            });

            // Image itself
            stack.Children.Add(new Image
            {
                Source = ImageSource.FromFile(reportImage.ImagePath),
                Aspect = Aspect.AspectFill,
                HeightRequest = 120,
                WidthRequest = 160,
                BackgroundColor = Colors.LightGray
            });

            // Optional comment/caption
            if (!string.IsNullOrEmpty(reportImage.Comment))
            {
                stack.Children.Add(new Label
                {
                    Text = reportImage.Comment,
                    FontSize = 10,
                    TextColor = Colors.Gray,
                    HorizontalOptions = LayoutOptions.Center,
                    LineBreakMode = LineBreakMode.TailTruncation,
                    MaxLines = 1
                });
            }

            block.Content = stack;
            return block;
        }

        private void UpdateUI()
        {
            if (TotalImagesLabel != null)
                TotalImagesLabel.Text = $"Total Images: {_reportImages.Count}";
            
            if (ImagesPerPageLabel != null)
                ImagesPerPageLabel.Text = _imagesPerPage.ToString();
        }

        private async void OnBackButtonClicked(object sender, EventArgs e)
        {
            await Navigation.PopAsync();
        }

        protected override bool OnBackButtonPressed()
        {
            Device.BeginInvokeOnMainThread(async () =>
            {
                await Navigation.PopAsync();
            });
            return true;
        }

        private async void OnIncreaseImagesPerPage(object sender, EventArgs e)
        {
            if (_imagesPerPage < 12)
            {
                _imagesPerPage += 1;
                UpdateUI();
                BuildPreviewPages();
            }
        }

        private async void OnDecreaseImagesPerPage(object sender, EventArgs e)
        {
            if (_imagesPerPage > 1)
            {
                _imagesPerPage -= 1;
                UpdateUI();
                BuildPreviewPages();
            }
        }

        private async void OnGeneratePDFClicked(object sender, EventArgs e)
        {
            try
            {
                // Show progress overlay
                if (ProgressOverlay != null)
                {
                    ProgressOverlay.IsVisible = true;
                    ProgressIndicator.IsRunning = true;
                    ProgressLabel.Text = "Generating PDF...";
                }

                // Generate PDF
                var pdfPath = await _pdfGeneratorService.GeneratePDFAsync(_reportImages, _imagesPerPage);
                
                if (!string.IsNullOrEmpty(pdfPath) && File.Exists(pdfPath))
                {
                    // Update progress message
                    if (ProgressOverlay != null)
                    {
                        ProgressLabel.Text = "Saving report...";
                    }

                    // Save report locally for persistence
                    await _reportStorageService.SaveReportAsync(pdfPath, _reportImages.Count, _imagesPerPage);

                    // Clear report images after successful save
                    _reportImageService.ClearAllImages();

                    // Hide progress overlay
                    if (ProgressOverlay != null)
                    {
                        ProgressOverlay.IsVisible = false;
                        ProgressIndicator.IsRunning = false;
                    }

                    // Show success message
                    await DisplayAlert("Success", "Report published successfully!", "OK");

                    // Navigate to landing page (MainPage)
                    await Shell.Current.GoToAsync("//MainPage");
                }
                else
                {
                    if (ProgressOverlay != null)
                    {
                        ProgressOverlay.IsVisible = false;
                        ProgressIndicator.IsRunning = false;
                    }
                    await DisplayAlert("Error", "Failed to generate PDF", "OK");
                }
            }
            catch (Exception ex)
            {
                if (ProgressOverlay != null)
                {
                    ProgressOverlay.IsVisible = false;
                    ProgressIndicator.IsRunning = false;
                }
                await DisplayAlert("Error", $"Failed to generate PDF: {ex.Message}", "OK");
            }
        }
    }
}
