namespace MauiApp.Core.Models;

public class ReportImage
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ImagePath { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public string ThumbnailPath { get; set; } = string.Empty;
}
