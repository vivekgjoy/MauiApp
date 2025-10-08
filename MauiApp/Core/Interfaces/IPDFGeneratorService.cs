using MauiApp.Core.Models;

namespace MauiApp.Core.Interfaces;

public interface IPDFGeneratorService
{
    Task<string?> GeneratePDFAsync(List<ReportImage> images, int imagesPerPage);
    Task<string?> GeneratePDFAsync(List<ReportImage> images, int imagesPerPage, string outputPath);
}
