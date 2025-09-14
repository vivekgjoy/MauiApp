using System.Windows.Input;

namespace MauiApp.ViewModels
{
    /// <summary>
    /// ViewModel for custom alert dialog
    /// </summary>
    public class CustomAlertViewModel : BaseViewModel
    {
        private string _title = string.Empty;
        private string _message = string.Empty;
        private string _buttonText = "OK";

        public CustomAlertViewModel(string title, string message, string buttonText = "OK")
        {
            Title = title;
            Message = message;
            ButtonText = buttonText;
            ButtonCommand = new Command(async () => await CloseDialogAsync());
        }

        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        public string Message
        {
            get => _message;
            set => SetProperty(ref _message, value);
        }

        public string ButtonText
        {
            get => _buttonText;
            set => SetProperty(ref _buttonText, value);
        }

        public ICommand ButtonCommand { get; }

        private async Task CloseDialogAsync()
        {
            await Application.Current.MainPage.Navigation.PopModalAsync();
        }
    }
}
