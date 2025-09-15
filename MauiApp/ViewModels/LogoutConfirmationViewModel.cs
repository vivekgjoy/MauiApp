using System.Windows.Input;

namespace MauiApp.ViewModels
{
    /// <summary>
    /// ViewModel for logout confirmation dialog
    /// </summary>
    public class LogoutConfirmationViewModel : BaseViewModel
    {
        private TaskCompletionSource<bool> _resultCompletionSource;

        public LogoutConfirmationViewModel()
        {
            _resultCompletionSource = new TaskCompletionSource<bool>();
            YesCommand = new Command(OnYesClicked);
            NoCommand = new Command(OnNoClicked);
        }

        public ICommand YesCommand { get; }
        public ICommand NoCommand { get; }

        public Task<bool> ResultTask => _resultCompletionSource.Task;

        private async void OnYesClicked()
        {
            _resultCompletionSource.SetResult(true);
            await Application.Current.MainPage.Navigation.PopModalAsync();
        }

        private async void OnNoClicked()
        {
            _resultCompletionSource.SetResult(false);
            await Application.Current.MainPage.Navigation.PopModalAsync();
        }
    }
}
