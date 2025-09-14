using MauiApp.Core.Interfaces;
using System.Threading.Tasks;

namespace MauiApp.Core.Services
{
    public class AppStateService : IAppStateService
    {
        private const string APP_RUNNING_KEY = "app_running";
        private const string APP_PROPERLY_CLOSED_KEY = "app_properly_closed";

        public async Task SetAppRunningAsync()
        {
            try
            {
                await SecureStorage.SetAsync(APP_RUNNING_KEY, "true");
                await SecureStorage.SetAsync(APP_PROPERLY_CLOSED_KEY, "false");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error setting app running state: {ex.Message}");
            }
        }

        public async Task SetAppStoppedAsync()
        {
            try
            {
                await SecureStorage.SetAsync(APP_RUNNING_KEY, "false");
                await SecureStorage.SetAsync(APP_PROPERLY_CLOSED_KEY, "true");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error setting app stopped state: {ex.Message}");
            }
        }

        public async Task<bool> WasAppProperlyClosedAsync()
        {
            try
            {
                var properlyClosed = await SecureStorage.GetAsync(APP_PROPERLY_CLOSED_KEY);
                return properlyClosed == "true";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error checking app closure state: {ex.Message}");
                return false;
            }
        }

        public async Task ClearAppStateAsync()
        {
            try
            {
                SecureStorage.Remove(APP_RUNNING_KEY);
                SecureStorage.Remove(APP_PROPERLY_CLOSED_KEY);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error clearing app state: {ex.Message}");
            }
        }
    }
}
