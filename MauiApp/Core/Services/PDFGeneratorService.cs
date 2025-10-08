using MauiApp.Core.Interfaces;
using MauiApp.Core.Models;
using SkiaSharp;
using System.IO;

namespace MauiApp.Core.Services;

public class PDFGeneratorService : IPDFGeneratorService
{
    private const float A4_WIDTH_MM = 210f;
    private const float A4_HEIGHT_MM = 297f;
    private const float MM_TO_POINTS = 2.834645669f; // 1 mm = 2.834645669 points
    private const float A4_WIDTH_POINTS = A4_WIDTH_MM * MM_TO_POINTS;
    private const float A4_HEIGHT_POINTS = A4_HEIGHT_MM * MM_TO_POINTS;
    private const float PAGE_MARGIN_POINTS = 20f;
    private const int DPI = 300;

    public async Task<string?> GeneratePDFAsync(List<ReportImage> images, int imagesPerPage)
    {
        var fileName = $"Report_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
        var outputPath = Path.Combine(FileSystem.CacheDirectory, fileName);
        return await GeneratePDFAsync(images, imagesPerPage, outputPath);
    }

    public async Task<string?> GeneratePDFAsync(List<ReportImage> images, int imagesPerPage, string outputPath)
    {
        try
        {
            if (images == null || images.Count == 0)
                return null;

            var totalPages = (int)Math.Ceiling((double)images.Count / imagesPerPage);
            
            using var stream = new FileStream(outputPath, FileMode.Create);
            using var document = SKDocument.CreatePdf(stream, new SKDocumentPdfMetadata
            {
                Title = "Image Report",
                Author = "MauiApp",
                Subject = "Generated Report",
                Creator = "MauiApp PDF Generator"
            });

            for (int pageIndex = 0; pageIndex < totalPages; pageIndex++)
            {
                var pageImages = images.Skip(pageIndex * imagesPerPage).Take(imagesPerPage).ToList();
                await GeneratePageAsync(document, pageImages, pageIndex + 1);
            }

            document.Close();
            return outputPath;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"PDF Generation Error: {ex.Message}");
            return null;
        }
    }

    private async Task GeneratePageAsync(SKDocument document, List<ReportImage> pageImages, int pageNumber)
    {
        using var canvas = document.BeginPage(A4_WIDTH_POINTS, A4_HEIGHT_POINTS);
        
        // Clear page with white background
        canvas.Clear(SKColors.White);

        // Calculate grid dimensions
        var (cols, rows) = CalculateGridDimensions(pageImages.Count);
        
        // Calculate available space for images
        var availableWidth = A4_WIDTH_POINTS - (2 * PAGE_MARGIN_POINTS);
        var availableHeight = A4_HEIGHT_POINTS - (2 * PAGE_MARGIN_POINTS) - 40; // Leave space for page number
        
        var cellWidth = availableWidth / cols;
        var cellHeight = availableHeight / rows;
        var cellSize = Math.Min(cellWidth, cellHeight);
        
        // Center the grid
        var startX = PAGE_MARGIN_POINTS + (availableWidth - (cols * cellSize)) / 2;
        var startY = PAGE_MARGIN_POINTS + (availableHeight - (rows * cellSize)) / 2;

        // Draw images
        for (int i = 0; i < pageImages.Count; i++)
        {
            var image = pageImages[i];
            var row = i / cols;
            var col = i % cols;
            
            var x = startX + (col * cellSize);
            var y = startY + (row * cellSize);
            
            await DrawImageInCell(canvas, image, x, y, cellSize);
        }

        // Draw page number
        DrawPageNumber(canvas, pageNumber);
        
        document.EndPage();
    }

    private async Task DrawImageInCell(SKCanvas canvas, ReportImage reportImage, float x, float y, float cellSize)
    {
        try
        {
            using var imageStream = File.OpenRead(reportImage.ImagePath);
            using var imageData = SKData.Create(imageStream);
            using var image = SKImage.FromEncodedData(imageData);
            
            if (image == null) return;

            // Calculate scaling to fit within cell while maintaining aspect ratio
            var scaleX = cellSize / image.Width;
            var scaleY = cellSize / image.Height;
            var scale = Math.Min(scaleX, scaleY);
            
            var scaledWidth = image.Width * scale;
            var scaledHeight = image.Height * scale;
            
            // Center the image within the cell
            var imageX = x + (cellSize - scaledWidth) / 2;
            var imageY = y + (cellSize - scaledHeight) / 2;
            
            var destRect = new SKRect(imageX, imageY, imageX + scaledWidth, imageY + scaledHeight);
            
            // Draw image
            canvas.DrawImage(image, destRect);
            
            // Draw border around cell
            using var borderPaint = new SKPaint
            {
                Color = SKColors.LightGray,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1
            };
            canvas.DrawRect(new SKRect(x, y, x + cellSize, y + cellSize), borderPaint);
            
            // Draw comment if available
            if (!string.IsNullOrEmpty(reportImage.Comment))
            {
                DrawComment(canvas, reportImage.Comment, x, y + cellSize - 20, cellSize);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error drawing image {reportImage.ImagePath}: {ex.Message}");
            
            // Draw placeholder for failed image
            using var placeholderPaint = new SKPaint
            {
                Color = SKColors.LightGray,
                Style = SKPaintStyle.Fill
            };
            canvas.DrawRect(new SKRect(x, y, x + cellSize, y + cellSize), placeholderPaint);
            
            using var textPaint = new SKPaint
            {
                Color = SKColors.Gray,
                TextSize = 12,
                IsAntialias = true
            };
            canvas.DrawText("Image Error", x + 5, y + cellSize / 2, textPaint);
        }
    }

    private void DrawComment(SKCanvas canvas, string comment, float x, float y, float cellWidth)
    {
        using var textPaint = new SKPaint
        {
            Color = SKColors.Gray,
            TextSize = 8,
            IsAntialias = true
        };
        
        // Truncate comment if too long
        var maxChars = (int)(cellWidth / 4); // Approximate character width
        var displayComment = comment.Length > maxChars ? comment.Substring(0, maxChars) + "..." : comment;
        
        canvas.DrawText(displayComment, x + 2, y, textPaint);
    }

    private void DrawPageNumber(SKCanvas canvas, int pageNumber)
    {
        using var textPaint = new SKPaint
        {
            Color = SKColors.Gray,
            TextSize = 12,
            IsAntialias = true,
            TextAlign = SKTextAlign.Center
        };
        
        var pageText = $"Page {pageNumber}";
        canvas.DrawText(pageText, A4_WIDTH_POINTS / 2, A4_HEIGHT_POINTS - 10, textPaint);
    }

    private (int cols, int rows) CalculateGridDimensions(int imageCount)
    {
        var cols = (int)Math.Ceiling(Math.Sqrt(imageCount));
        var rows = (int)Math.Ceiling((double)imageCount / cols);
        return (cols, rows);
    }
}
