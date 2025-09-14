using MauiApp.Views;

namespace MauiApp.Core.Services
{
    /// <summary>
    /// Service for showing custom popups
    /// </summary>
    public class PopupService
    {
        private static PopupOverlay? _currentPopup;

        /// <summary>
        /// Shows a custom popup with title, message, and button text
        /// </summary>
        public static async Task ShowPopupAsync(string title, string message, string buttonText = "OK")
        {
            try
            {
                // Close any existing popup
                if (_currentPopup != null)
                {
                    await ClosePopupAsync();
                }

                // Create new popup
                _currentPopup = new PopupOverlay(title, message, buttonText);
                
                // Add to the current page
                var currentPage = Application.Current?.MainPage;
                if (currentPage is ContentPage contentPage)
                {
                    // Try to find root grid first
                    var rootGrid = contentPage.FindByName<Grid>("RootGrid");
                    if (rootGrid != null)
                    {
                        rootGrid.Children.Add(_currentPopup);
                    }
                    else if (contentPage.Content is Grid grid)
                    {
                        grid.Children.Add(_currentPopup);
                    }
                    else
                    {
                        // Wrap content in grid
                        var wrapperGrid = new Grid();
                        wrapperGrid.Children.Add(contentPage.Content);
                        wrapperGrid.Children.Add(_currentPopup);
                        contentPage.Content = wrapperGrid;
                    }
                }

                // Animate in
                await _currentPopup.AnimateInAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Show popup error: {ex.Message}");
            }
        }

        /// <summary>
        /// Closes the current popup
        /// </summary>
        public static async Task ClosePopupAsync()
        {
            try
            {
                if (_currentPopup != null)
                {
                    await _currentPopup.AnimateOutAsync();
                    
                    var currentPage = Application.Current?.MainPage;
                    if (currentPage is ContentPage contentPage)
                    {
                        if (contentPage.Content is Grid grid)
                        {
                            grid.Children.Remove(_currentPopup);
                        }
                    }
                    
                    _currentPopup = null;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Close popup error: {ex.Message}");
            }
        }
    }
}
