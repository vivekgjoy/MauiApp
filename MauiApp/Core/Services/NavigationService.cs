using MauiApp.Core.Interfaces;

namespace MauiApp.Core.Services
{
    /// <summary>
    /// Service for handling navigation operations
    /// </summary>
    public class NavigationService : INavigationService
    {
        public async Task NavigateToAsync(string route)
        {
            try
            {
                await Shell.Current.GoToAsync(route);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Navigation error: {ex.Message}");
            }
        }

        public async Task GoBackAsync()
        {
            try
            {
                await Shell.Current.GoToAsync("..");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Go back error: {ex.Message}");
            }
        }

        public async Task NavigateToRootAsync(string route)
        {
            try
            {
                await Shell.Current.GoToAsync($"//{route}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Navigate to root error: {ex.Message}");
            }
        }
    }
}
