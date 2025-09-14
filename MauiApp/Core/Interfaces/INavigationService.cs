namespace MauiApp.Core.Interfaces
{
    /// <summary>
    /// Interface for navigation operations
    /// </summary>
    public interface INavigationService
    {
        /// <summary>
        /// Navigates to a specific page
        /// </summary>
        Task NavigateToAsync(string route);
        
        /// <summary>
        /// Navigates back to the previous page
        /// </summary>
        Task GoBackAsync();
        
        /// <summary>
        /// Navigates to a page and clears the navigation stack
        /// </summary>
        Task NavigateToRootAsync(string route);
    }
}
