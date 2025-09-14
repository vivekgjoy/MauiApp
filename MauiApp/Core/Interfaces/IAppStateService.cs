using System.Threading.Tasks;

namespace MauiApp.Core.Interfaces
{
    public interface IAppStateService
    {
        /// <summary>
        /// Marks the app as running (called when user logs in)
        /// </summary>
        Task SetAppRunningAsync();

        /// <summary>
        /// Marks the app as stopped (called when user logs out or app is killed)
        /// </summary>
        Task SetAppStoppedAsync();

        /// <summary>
        /// Checks if the app was properly closed (not killed)
        /// </summary>
        Task<bool> WasAppProperlyClosedAsync();

        /// <summary>
        /// Clears the app state
        /// </summary>
        Task ClearAppStateAsync();
    }
}
