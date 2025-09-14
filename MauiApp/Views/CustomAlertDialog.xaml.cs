using MauiApp.ViewModels;

namespace MauiApp.Views
{
    /// <summary>
    /// Custom modern alert dialog
    /// </summary>
    public partial class CustomAlertDialog : ContentPage
    {
        private readonly CustomAlertViewModel _viewModel;

        public CustomAlertDialog(string title, string message, string buttonText = "OK")
        {
            InitializeComponent();
            _viewModel = new CustomAlertViewModel(title, message, buttonText);
            BindingContext = _viewModel;
        }

        public static async Task ShowAsync(string title, string message, string buttonText = "OK")
        {
            var dialog = new CustomAlertDialog(title, message, buttonText);
            await Application.Current.MainPage.Navigation.PushModalAsync(dialog);
        }
    }
}
