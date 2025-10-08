namespace MauiApp.Core.Models;

public class GeneratedReport
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; } = DateTime.Now;
    public int ImageCount { get; set; }
    public int ImagesPerPage { get; set; }
    public long FileSizeBytes { get; set; }
}
