using MauiApp.ViewModels;

namespace MauiApp.Views
{
    /// <summary>
    /// Splash screen page with custom logo and gradient background
    /// </summary>
    public partial class SplashPage : ContentPage
    {
        private readonly SplashViewModel _viewModel;

        public SplashPage()
        {
            InitializeComponent();
            _viewModel = ServiceHelper.GetService<SplashViewModel>();
            BindingContext = _viewModel;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await _viewModel.InitializeAsync();
        }
    }
}
