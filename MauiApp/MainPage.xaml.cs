using MauiApp.Core.Interfaces;
using MauiApp.Views;

namespace MauiApp;

public partial class MainPage : ContentPage
{
    private readonly IAuthenticationService _authenticationService;
    private readonly IApiService _apiService;
    private readonly IReportImageService _reportImageService;

    public MainPage()
    {
        InitializeComponent();
        _authenticationService = ServiceHelper.GetService<IAuthenticationService>();
        _apiService = ServiceHelper.GetService<IApiService>();
        _reportImageService = ServiceHelper.GetService<IReportImageService>();
    }

    private async void OnAddReportsClicked(object sender, EventArgs e)
    {
        try
        {
            // Check if there are any unsaved changes in the report
            if (_reportImageService.ReportImages.Count > 0)
            {
                var result = await DisplayAlert(
                    "Unsaved Changes", 
                    "You have unsaved changes in your current report. Starting a new report will clear all your work. Are you sure you want to continue?", 
                    "Yes, Start New Report", 
                    "Cancel");
                
                if (!result)
                {
                    return; // User cancelled, stay on current page
                }
                
                // Clear all images for fresh start
                _reportImageService.ClearAllImages();
            }
            
            await Shell.Current.GoToAsync(nameof(AddReportPage));
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to navigate to Add Reports: {ex.Message}", "OK");
        }
    }

    private async void OnReportsClicked(object sender, EventArgs e)
    {
        try
        {
            await Shell.Current.GoToAsync(nameof(ReportsHistoryPage));
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to navigate to Reports: {ex.Message}", "OK");
        }
    }

    private async void OnLogoutClicked(object sender, EventArgs e)
    {
        try
        {
            var result = await LogoutConfirmationDialog.ShowAsync();
            if (result)
            {
                // Small delay to ensure dialog is fully closed
                await Task.Delay(100);
                await _authenticationService.LogoutAsync();
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Logout failed: {ex.Message}", "OK");
        }
    }

    /// <summary>
    /// Test method to call User Information API
    /// </summary>
    private async void OnTestUserInfoClicked(object sender, EventArgs e)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("=== TESTING USER INFORMATION API ===");
            
            // Create user information request
            var userInfoRequest = new Core.Models.UserInformationRequest
            {
                UserName = "allstartic"
            };

            // Call the API
            var response = await _apiService.GetUserInformationAsync(userInfoRequest);

            if (response.Success && response.Data != null)
            {
                System.Diagnostics.Debug.WriteLine("=== USER INFORMATION API SUCCESS ===");
                System.Diagnostics.Debug.WriteLine($"User ID: {response.Data.Id}");
                System.Diagnostics.Debug.WriteLine($"Username: {response.Data.UserName}");
                System.Diagnostics.Debug.WriteLine($"Full Name: {response.Data.FullName}");
                System.Diagnostics.Debug.WriteLine($"Email: {response.Data.EmailAddress}");
                System.Diagnostics.Debug.WriteLine($"Phone: {response.Data.PhoneNumber}");
                System.Diagnostics.Debug.WriteLine($"Department: {response.Data.Department}");
                System.Diagnostics.Debug.WriteLine($"Job Title: {response.Data.JobTitle}");
                System.Diagnostics.Debug.WriteLine($"Company: {response.Data.Company}");
                System.Diagnostics.Debug.WriteLine($"Is Active: {response.Data.IsActive}");
                System.Diagnostics.Debug.WriteLine($"Last Login: {response.Data.LastLoginTime}");
                System.Diagnostics.Debug.WriteLine($"Creation Time: {response.Data.CreationTime}");
                System.Diagnostics.Debug.WriteLine("=====================================");

                // Show success message
                await DisplayAlert("Success", 
                    $"User Information Retrieved Successfully!\n\n" +
                    $"User: {response.Data.UserName}\n" +
                    $"Name: {response.Data.FullName}\n" +
                    $"Email: {response.Data.EmailAddress}\n" +
                    $"Department: {response.Data.Department}\n" +
                    $"Job Title: {response.Data.JobTitle}", 
                    "OK");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("=== USER INFORMATION API FAILED ===");
                System.Diagnostics.Debug.WriteLine($"Error: {response.Message}");
                System.Diagnostics.Debug.WriteLine($"Error Code: {response.ErrorCode}");
                System.Diagnostics.Debug.WriteLine("===================================");

                await DisplayAlert("Error", $"Failed to retrieve user information: {response.Message}", "OK");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"=== USER INFO TEST ERROR ===");
            System.Diagnostics.Debug.WriteLine($"Error: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack Trace: {ex.StackTrace}");
            System.Diagnostics.Debug.WriteLine("=============================");

            await DisplayAlert("Error", $"An error occurred: {ex.Message}", "OK");
        }
    }
}