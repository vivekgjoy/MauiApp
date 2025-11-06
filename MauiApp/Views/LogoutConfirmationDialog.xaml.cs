using MauiApp.ViewModels;
#if ANDROID
using AndroidX.AppCompat.App;
using Android.OS;
using Android.Views;
using AColor = Android.Graphics.Color;
using Microsoft.Maui.Platform;
#endif

namespace MauiApp.Views
{
    /// <summary>
    /// Logout confirmation dialog with Yes/No options
    /// </summary>
    public partial class LogoutConfirmationDialog : ContentPage
    {
        private readonly LogoutConfirmationViewModel _viewModel;

#if ANDROID
        private AColor? _prevStatusBarColor;
        private bool _prevLightStatusBar;
#endif

        public LogoutConfirmationDialog()
        {
            InitializeComponent();
            _viewModel = new LogoutConfirmationViewModel();
            BindingContext = _viewModel;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();

#if ANDROID
            try
            {
                var activity = Platform.CurrentActivity as AppCompatActivity;
                if (activity != null && Build.VERSION.SdkInt >= BuildVersionCodes.Lollipop)
                {
                    // Save previous status bar appearance
                    if (activity.Window != null)
                    {
                        _prevStatusBarColor = new AColor(activity.Window.StatusBarColor);
                    }

                    var decor = activity.Window?.DecorView;
                    if (decor != null)
                    {
                        var flags = (int)decor.SystemUiFlags;
                        _prevLightStatusBar = (flags & (int)SystemUiFlags.LightStatusBar) != 0;
                        // Ensure light content (white icons) on coral background
                        decor.SystemUiFlags = (SystemUiFlags)(flags & ~(int)SystemUiFlags.LightStatusBar);
                    }

                    // Maintain coral status bar color when dialog appears
                    activity.Window?.SetStatusBarColor(AColor.ParseColor("#FF6B5A"));
                }
            }
            catch { }
#endif
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();

#if ANDROID
            try
            {
                var activity = Platform.CurrentActivity as AppCompatActivity;
                if (activity != null && Build.VERSION.SdkInt >= BuildVersionCodes.Lollipop)
                {
                    // Restore previous status bar color
                    if (_prevStatusBarColor.HasValue)
                    {
                        activity.Window?.SetStatusBarColor(_prevStatusBarColor.Value);
                    }

                    // Restore previous icon/light flag
                    var decor = activity.Window?.DecorView;
                    if (decor != null)
                    {
                        var flags = (int)decor.SystemUiFlags;
                        if (_prevLightStatusBar)
                        {
                            decor.SystemUiFlags = (SystemUiFlags)(flags | (int)SystemUiFlags.LightStatusBar);
                        }
                        else
                        {
                            decor.SystemUiFlags = (SystemUiFlags)(flags & ~(int)SystemUiFlags.LightStatusBar);
                        }
                    }
                }
            }
            catch { }
#endif
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
