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

public partial class ReportsHistoryPage : ContentPage
{
    private readonly IReportStorageService _reportStorageService;
    private ObservableCollection<GeneratedReport> _reports;

    public ObservableCollection<GeneratedReport> Reports => _reports;

    public ReportsHistoryPage()
    {
        InitializeComponent();
        _reportStorageService = ServiceHelper.GetService<IReportStorageService>();
        _reports = new ObservableCollection<GeneratedReport>();

        // Set up navigation bar back command
        //NavigationBar.BackCommand = new Command(async () => await OnBackClicked());

        // Set binding context
        BindingContext = this;

        // Handle safe area for status bar
        this.Loaded += OnPageLoaded;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadReports();
    }

    protected override bool OnBackButtonPressed()
    {
        // Handle back button press - navigate back instead of going to background
        Device.BeginInvokeOnMainThread(async () =>
        {
            await OnBackClicked();
        });
        return true; // Prevent default back behavior (going to background)
    }

#if ANDROID
    private void OnPageLoaded(object sender, EventArgs e)
    {
        if (DeviceInfo.Platform == DevicePlatform.Android)
        {
            var activity = Platform.CurrentActivity as AppCompatActivity;
            if (activity != null && Build.VERSION.SdkInt >= BuildVersionCodes.Lollipop)
            {
                activity.Window.SetStatusBarColor(Android.Graphics.Color.ParseColor("#FF6B5A"));
            }
        }
    }
#else
    private void OnPageLoaded(object sender, EventArgs e) { }
#endif

    private async Task OnBackClicked()
    {
        try
        {
            // Use Shell navigation since all pages are registered as Shell routes
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            // Fallback to main page if navigation fails
            try
            {
                await Shell.Current.GoToAsync("//MainPage");
            }
            catch
            {
                // Last resort - just go back to main page
                await Shell.Current.GoToAsync("//MainPage");
            }
        }
    }

    private async Task LoadReports()
    {
        try
        {
            var reports = await _reportStorageService.GetAllReportsAsync();
            
            _reports.Clear();
            foreach (var report in reports)
            {
                _reports.Add(report);
            }

            // Show/hide empty state
            EmptyStateFrame.IsVisible = _reports.Count == 0;
            ReportsCollectionView.IsVisible = _reports.Count > 0;

            System.Diagnostics.Debug.WriteLine($"Loaded {_reports.Count} reports from storage");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading reports: {ex.Message}");
            await DisplayAlert("Error", "Failed to load reports history", "OK");
        }
    }

    private async void OnViewReportClicked(object sender, EventArgs e)
    {
        try
        {
            if (sender is Button button && button.CommandParameter is string filePath)
            {
                if (File.Exists(filePath))
                {
                    // Open PDF with default app
                    await Launcher.OpenAsync(new OpenFileRequest
                    {
                        File = new ReadOnlyFile(filePath)
                    });
                }
                else
                {
                    await DisplayAlert("File Not Found", "The PDF file could not be found.", "OK");
                }
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to open PDF: {ex.Message}", "OK");
        }
    }

    private async void OnShareReportClicked(object sender, EventArgs e)
    {
        try
        {
            if (sender is Button button && button.CommandParameter is string filePath)
            {
                if (File.Exists(filePath))
                {
                    await Share.Default.RequestAsync(new ShareFileRequest
                    {
                        File = new ShareFile(filePath),
                        Title = "Share PDF Report"
                    });
                }
                else
                {
                    await DisplayAlert("File Not Found", "The PDF file could not be found.", "OK");
                }
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to share PDF: {ex.Message}", "OK");
        }
    }

    private async void OnBackButtonClicked(object sender, EventArgs e)
    {
        try
        {
            // Use Shell navigation since all pages are registered as Shell routes
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            // Fallback to main page if navigation fails
            try
            {
                await Shell.Current.GoToAsync("//MainPage");
            }
            catch
            {
                // Last resort - just go back to main page
                await Shell.Current.GoToAsync("//MainPage");
            }
        }
    }

    private async void OnGenerateNewReportClicked(object sender, EventArgs e)
    {
        try
        {
            await Shell.Current.GoToAsync(nameof(AddReportPage));
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to navigate to Add Report page: {ex.Message}", "OK");
        }
    }
}
