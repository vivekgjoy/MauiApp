using System.Windows.Input;

namespace MauiApp.Views
{
    /// <summary>
    /// Custom popup overlay with smooth animations
    /// </summary>
    public partial class PopupOverlay : ContentView
    {
        public PopupOverlay(string title, string message, string buttonText = "OK")
        {
            InitializeComponent();
            
            TitleLabel.Text = title;
            MessageLabel.Text = message;
            ActionButton.Text = buttonText;
            ActionButton.Command = new Command(async () => await CloseAsync());
        }

        /// <summary>
        /// Animates the popup in
        /// </summary>
        public async Task AnimateInAsync()
        {
            // Start with hidden state
            OverlayBackground.Opacity = 0;
            AlertFrame.Scale = 0.8;
            AlertFrame.Opacity = 0;

            // Animate overlay fade in
            await OverlayBackground.FadeTo(1, 200, Easing.CubicOut);
            
            // Animate dialog scale and fade in
            await Task.WhenAll(
                AlertFrame.ScaleTo(1, 300, Easing.CubicOut),
                AlertFrame.FadeTo(1, 300, Easing.CubicOut)
            );
        }

        /// <summary>
        /// Animates the popup out
        /// </summary>
        public async Task AnimateOutAsync()
        {
            // Animate dialog scale and fade out
            await Task.WhenAll(
                AlertFrame.ScaleTo(0.8, 200, Easing.CubicIn),
                AlertFrame.FadeTo(0, 200, Easing.CubicIn)
            );
            
            // Animate overlay fade out
            await OverlayBackground.FadeTo(0, 200, Easing.CubicIn);
        }

        /// <summary>
        /// Closes the popup
        /// </summary>
        private async Task CloseAsync()
        {
            await AnimateOutAsync();
            await Core.Services.PopupService.ClosePopupAsync();
        }
    }
}
