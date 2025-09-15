using MauiApp.ViewModels;
#if ANDROID
using Android.Views;
#endif

namespace MauiApp.Views
{
    /// <summary>
    /// Login page with biometric and traditional login options
    /// </summary>
    public partial class LoginPage : ContentPage
    {
        private readonly LoginViewModel _viewModel;

        public LoginPage()
        {
            InitializeComponent();
            _viewModel = ServiceHelper.GetService<LoginViewModel>();
            BindingContext = _viewModel;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await _viewModel.InitializeAsync();
        }

        protected override void OnHandlerChanged()
        {
            base.OnHandlerChanged();
            
            // Handle keyboard events for all input fields
            if (PasswordEntry != null)
            {
                PasswordEntry.Focused += OnInputFocused;
                PasswordEntry.Unfocused += OnInputUnfocused;
            }
            
            // Also handle username field if it exists
            if (UsernameEntry != null)
            {
                UsernameEntry.Focused += OnInputFocused;
                UsernameEntry.Unfocused += OnInputUnfocused;
            }
            
            // Handle tenant field focus
            if (TenantEntry != null)
            {
                TenantEntry.Focused += OnTenantEntryFocused;
            }
        }

        private async void OnInputFocused(object sender, FocusEventArgs e)
        {
            // Scroll to show the input field when it gets focus
            await Task.Delay(100); // Small delay to ensure keyboard is visible
            await MainScrollView.ScrollToAsync(0, MainScrollView.ContentSize.Height, true);
        }

        private async void OnInputUnfocused(object sender, FocusEventArgs e)
        {
            // Reset scroll position when input loses focus
            await Task.Delay(100); // Small delay to ensure keyboard is dismissed
            await MainScrollView.ScrollToAsync(0, 0, true);
        }

        private void OnTenantEntryFocused(object sender, FocusEventArgs e)
        {
            // When tenant field gets focus, open the tenant selector
            if (_viewModel.OpenTenantSelectorCommand.CanExecute(null))
            {
                _viewModel.OpenTenantSelectorCommand.Execute(null);
            }
        }
    }
}
