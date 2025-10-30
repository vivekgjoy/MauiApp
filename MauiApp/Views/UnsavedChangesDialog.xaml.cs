using MauiApp.ViewModels;

namespace MauiApp.Views
{
    public enum UnsavedChangesResult { Continue, StartNew, Closed }

    public partial class UnsavedChangesDialog : ContentPage
    {
        private readonly UnsavedChangesViewModel _viewModel;

        public UnsavedChangesDialog()
        {
            InitializeComponent();
            _viewModel = new UnsavedChangesViewModel();
            BindingContext = _viewModel;
        }

        public static async Task<UnsavedChangesResult> ShowAsync()
        {
            var dialog = new UnsavedChangesDialog();
            await Application.Current.MainPage.Navigation.PushModalAsync(dialog);
            return await dialog._viewModel.ResultTask;
        }

        private async void OnCloseClicked(object sender, EventArgs e)
        {
            _viewModel.SetResult(UnsavedChangesResult.Closed);
            await Navigation.PopModalAsync();
        }
    }
}


