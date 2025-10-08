using MauiApp.Core.Models;

namespace MauiApp.Core.Interfaces;

public interface IReportImageService
{
    List<ReportImage> ReportImages { get; }
    void AddImage(ReportImage reportImage);
    void RemoveImage(string imageId);
    void UpdateImageComment(string imageId, string comment);
    void UpdateImagePath(string imageId, string newImagePath);
    void ClearAllImages();
    ReportImage? GetImage(string imageId);
}
