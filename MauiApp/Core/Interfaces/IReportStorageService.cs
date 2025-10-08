using MauiApp.Core.Models;

namespace MauiApp.Core.Interfaces;

public interface IReportStorageService
{
    Task<string> SaveReportAsync(string pdfPath, int imageCount, int imagesPerPage);
    Task<List<GeneratedReport>> GetAllReportsAsync();
    Task<GeneratedReport?> GetReportAsync(string id);
    Task<bool> DeleteReportAsync(string id);
    Task<bool> DeleteAllReportsAsync();
    Task<long> GetTotalStorageSizeAsync();
}
