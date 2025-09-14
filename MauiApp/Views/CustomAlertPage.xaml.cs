using System.Windows.Input;

namespace MauiApp.Views
{
    /// <summary>
    /// Custom alert page with rounded corners and proper button
    /// </summary>
    public partial class CustomAlertPage : ContentPage
    {
        public CustomAlertPage(string title, string message, string buttonText = "OK")
        {
            InitializeComponent();
            
            TitleLabel.Text = title;
            MessageLabel.Text = message;
            ActionButton.Text = buttonText;
            ActionButton.Command = new Command(async () => await CloseAsync());
        }

        public static async Task ShowAsync(string title, string message, string buttonText = "OK")
        {
            var dialog = new CustomAlertPage(title, message, buttonText);
            await Application.Current.MainPage.Navigation.PushModalAsync(dialog, false);
        }

        private async Task CloseAsync()
        {
            await Navigation.PopModalAsync(false);
        }
    }
}
