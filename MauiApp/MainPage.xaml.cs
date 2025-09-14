using MauiApp.Core.Interfaces;

namespace MauiApp;

public partial class MainPage : ContentPage
{
    private readonly IAuthenticationService _authenticationService;

    public MainPage()
    {
        InitializeComponent();
        _authenticationService = ServiceHelper.GetService<IAuthenticationService>();
    }

    private async void OnLogoutClicked(object sender, EventArgs e)
    {
        try
        {
            var result = await DisplayAlert("Logout", "Are you sure you want to logout?", "Yes", "No");
            if (result)
            {
                await _authenticationService.LogoutAsync();
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Logout failed: {ex.Message}", "OK");
        }
    }
}