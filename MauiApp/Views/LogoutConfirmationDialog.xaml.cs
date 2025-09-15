using MauiApp.ViewModels;

namespace MauiApp.Views
{
    /// <summary>
    /// Logout confirmation dialog with Yes/No options
    /// </summary>
    public partial class LogoutConfirmationDialog : ContentPage
    {
        private readonly LogoutConfirmationViewModel _viewModel;

        public LogoutConfirmationDialog()
        {
            InitializeComponent();
            _viewModel = new LogoutConfirmationViewModel();
            BindingContext = _viewModel;
        }

        public static async Task<bool> ShowAsync()
        {
            var dialog = new LogoutConfirmationDialog();
            await Application.Current.MainPage.Navigation.PushModalAsync(dialog);
            
            // Wait for the dialog to complete and return the result
            return await dialog._viewModel.ResultTask;
        }
    }
}
