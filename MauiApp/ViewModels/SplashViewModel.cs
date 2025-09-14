using MauiApp.Core.Interfaces;
using MauiApp.Core.Models;

namespace MauiApp.ViewModels
{
    /// <summary>
    /// ViewModel for the Splash Screen
    /// </summary>
    public class SplashViewModel : BaseViewModel
    {
        private readonly IAuthenticationService _authenticationService;
        private readonly INavigationService _navigationService;
        private readonly ITenantService _tenantService;

        public SplashViewModel(
            IAuthenticationService authenticationService,
            INavigationService navigationService,
            ITenantService tenantService)
        {
            _authenticationService = authenticationService;
            _navigationService = navigationService;
            _tenantService = tenantService;
        }

        /// <summary>
        /// Initializes the splash screen and determines navigation
        /// </summary>
        public async Task InitializeAsync()
        {
            try
            {
                IsBusy = true;
                Title = "SmartERP";

                // Simulate splash screen delay
                await Task.Delay(3000);

                // Check if user is already authenticated
                var isAuthenticated = await _authenticationService.IsAuthenticatedAsync();
                
                if (isAuthenticated)
                {
                    // Navigate to main app
                    await _navigationService.NavigateToRootAsync("MainPage");
                }
                else
                {
                    // Navigate to login
                    await _navigationService.NavigateToRootAsync("LoginPage");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Splash initialization error: {ex.Message}");
                // Navigate to login on error
                await _navigationService.NavigateToRootAsync("LoginPage");
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
