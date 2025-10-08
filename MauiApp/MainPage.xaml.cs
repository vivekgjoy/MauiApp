using MauiApp.Core.Interfaces;
using MauiApp.Views;

namespace MauiApp;

public partial class MainPage : ContentPage
{
    private readonly IAuthenticationService _authenticationService;

    public MainPage()
    {
        InitializeComponent();
        _authenticationService = ServiceHelper.GetService<IAuthenticationService>();
    }

    private async void OnAddReportsClicked(object sender, EventArgs e)
    {
        try
        {
            await Shell.Current.GoToAsync("//AddReportPage");
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
            await Shell.Current.GoToAsync("//ReportsHistoryPage");
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
}