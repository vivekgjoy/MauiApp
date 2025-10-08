using MauiApp.Core.Interfaces;
using MauiApp.Core.Models;
using System.Text.Json;

namespace MauiApp.Core.Services;

public class ReportStorageService : IReportStorageService
{
    private readonly string _reportsDirectory;
    private readonly string _metadataFile;
    private List<GeneratedReport> _reports;

    public ReportStorageService()
    {
        _reportsDirectory = Path.Combine(FileSystem.AppDataDirectory, "GeneratedReports");
        _metadataFile = Path.Combine(_reportsDirectory, "reports_metadata.json");
        _reports = new List<GeneratedReport>();
        
        // Ensure directory exists
        Directory.CreateDirectory(_reportsDirectory);
        
        // Load existing reports
        LoadReports();
    }

    public async Task<string> SaveReportAsync(string pdfPath, int imageCount, int imagesPerPage)
    {
        try
        {
            var fileName = $"Report_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
            var destinationPath = Path.Combine(_reportsDirectory, fileName);
            
            // Copy PDF to reports directory
            File.Copy(pdfPath, destinationPath, true);
            
            // Get file size
            var fileInfo = new FileInfo(destinationPath);
            
            // Create report metadata
            var report = new GeneratedReport
            {
                FileName = fileName,
                FilePath = destinationPath,
                CreatedDate = DateTime.Now,
                ImageCount = imageCount,
                ImagesPerPage = imagesPerPage,
                FileSizeBytes = fileInfo.Length
            };
            
            // Add to collection
            _reports.Add(report);
            
            // Save metadata
            await SaveReports();
            
            System.Diagnostics.Debug.WriteLine($"Report saved: {fileName}, Size: {fileInfo.Length} bytes");
            return destinationPath;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving report: {ex.Message}");
            throw;
        }
    }

    public async Task<List<GeneratedReport>> GetAllReportsAsync()
    {
        LoadReports();
        return _reports.OrderByDescending(r => r.CreatedDate).ToList();
    }

    public async Task<GeneratedReport?> GetReportAsync(string id)
    {
        LoadReports();
        return _reports.FirstOrDefault(r => r.Id == id);
    }

    public async Task<bool> DeleteReportAsync(string id)
    {
        try
        {
            var report = _reports.FirstOrDefault(r => r.Id == id);
            if (report == null) return false;
            
            // Delete file if exists
            if (File.Exists(report.FilePath))
            {
                File.Delete(report.FilePath);
            }
            
            // Remove from collection
            _reports.Remove(report);
            
            // Save metadata
            await SaveReports();
            
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error deleting report: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> DeleteAllReportsAsync()
    {
        try
        {
            // Delete all files
            foreach (var report in _reports)
            {
                if (File.Exists(report.FilePath))
                {
                    File.Delete(report.FilePath);
                }
            }
            
            // Clear collection
            _reports.Clear();
            
            // Save metadata
            await SaveReports();
            
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error deleting all reports: {ex.Message}");
            return false;
        }
    }

    public async Task<long> GetTotalStorageSizeAsync()
    {
        LoadReports();
        return _reports.Sum(r => r.FileSizeBytes);
    }

    private void LoadReports()
    {
        try
        {
            if (File.Exists(_metadataFile))
            {
                var json = File.ReadAllText(_metadataFile);
                var reports = JsonSerializer.Deserialize<List<GeneratedReport>>(json);
                if (reports != null)
                {
                    _reports = reports;
                    
                    // Clean up reports with missing files
                    var validReports = _reports.Where(r => File.Exists(r.FilePath)).ToList();
                    if (validReports.Count != _reports.Count)
                    {
                        _reports = validReports;
                        SaveReports();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading reports: {ex.Message}");
            _reports = new List<GeneratedReport>();
        }
    }

    private async Task SaveReports()
    {
        try
        {
            var json = JsonSerializer.Serialize(_reports, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            await File.WriteAllTextAsync(_metadataFile, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving reports: {ex.Message}");
        }
    }
}
