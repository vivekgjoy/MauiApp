using MauiApp.Core.Interfaces;
using MauiApp.Core.Models;

namespace MauiApp.Core.Services;

public class ReportImageService : IReportImageService
{
    private readonly List<ReportImage> _reportImages = new();

    public List<ReportImage> ReportImages => _reportImages;

    public void AddImage(ReportImage reportImage)
    {
        if (reportImage != null && !string.IsNullOrEmpty(reportImage.ImagePath))
        {
            _reportImages.Add(reportImage);
        }
    }

    public void RemoveImage(string imageId)
    {
        var image = _reportImages.FirstOrDefault(x => x.Id == imageId);
        if (image != null)
        {
            _reportImages.Remove(image);
        }
    }

    public void UpdateImageComment(string imageId, string comment)
    {
        var image = _reportImages.FirstOrDefault(x => x.Id == imageId);
        if (image != null)
        {
            image.Comment = comment;
        }
    }

    public void UpdateImagePath(string imageId, string newImagePath)
    {
        var image = _reportImages.FirstOrDefault(x => x.Id == imageId);
        if (image != null)
        {
            image.ImagePath = newImagePath;
        }
    }

    public void ClearAllImages()
    {
        _reportImages.Clear();
    }

    public ReportImage? GetImage(string imageId)
    {
        return _reportImages.FirstOrDefault(x => x.Id == imageId);
    }
}
