using MauiApp.ViewModels;
using Microsoft.Maui.Platform;
#if ANDROID
using AndroidX.AppCompat.App;
using Android.OS;
using Android.Views;
using AColor = Android.Graphics.Color;
#endif

namespace MauiApp.Views;

public partial class ImageSourceSelectionPage : ContentPage
{
    private double _initialY;
    private double _currentY;
    private bool _isAnimating = false;
    
#if ANDROID
    private AColor? _prevStatusBarColor;
    private bool _prevLightStatusBar;
#endif

    public ImageSourceSelectionPage()
    {
        System.Diagnostics.Debug.WriteLine("ImageSourceSelectionPage constructor called");
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        System.Diagnostics.Debug.WriteLine("ImageSourceSelectionPage OnAppearing called");

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
                    // Force dark status bar background with light icons by clearing LightStatusBar flag
                    decor.SystemUiFlags = (SystemUiFlags)(flags & ~(int)SystemUiFlags.LightStatusBar);
                }

                // Make status bar transparent so parent page gradient/color shows through
                activity.Window?.SetStatusBarColor(AColor.Transparent);
            }
        }
        catch { }
#endif

        // Wait a bit for the page to be fully rendered
        await Task.Delay(50);

        // Slide up animation
        await AnimateIn();
    }

    protected override async void OnDisappearing()
    {
        base.OnDisappearing();

        // Slide down animation when closing
        await AnimateOut();

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

    private async Task AnimateIn()
    {
        if (_isAnimating) return;
        _isAnimating = true;

        System.Diagnostics.Debug.WriteLine("AnimateIn called");

        // Start from below screen
        Sheet.TranslationY = Sheet.Height;
        Sheet.Opacity = 1;
        this.Opacity = 0;

        System.Diagnostics.Debug.WriteLine($"Sheet height: {Sheet.Height}, TranslationY set to: {Sheet.Height}");

        // Animate up
        await Task.WhenAll(
            Sheet.TranslateTo(0, 0, 300, Easing.CubicOut),
            this.FadeTo(1, 200, Easing.CubicOut)
        );

        System.Diagnostics.Debug.WriteLine("Animation completed");
        _isAnimating = false;
    }

    private async Task AnimateOut()
    {
        if (_isAnimating) return;
        _isAnimating = true;

        // Animate down and fade
        await Task.WhenAll(
            Sheet.TranslateTo(0, Sheet.Height, 250, Easing.CubicIn),
            this.FadeTo(0, 200, Easing.CubicIn)
        );

        _isAnimating = false;
    }

    private async void OnPanUpdated(object sender, PanUpdatedEventArgs e)
    {
        switch (e.StatusType)
        {
            case GestureStatus.Running:
                if (_initialY == 0)
                {
                    _initialY = Sheet.TranslationY;
                }
                _currentY = _initialY + e.TotalY;
                Sheet.TranslationY = Math.Max(0, _currentY);
                break;

            case GestureStatus.Completed:
                _initialY = 0;
                if (_currentY > Sheet.Height * 0.3)
                {
                    // Dismiss if dragged down more than 30% of height
                    await Dismiss();
                }
                else
                {
                    // Snap back to original position
                    await Sheet.TranslateTo(0, 0, 200, Easing.CubicOut);
                }
                break;
        }
    }

    private async void OnSheetTapped(object sender, EventArgs e)
    {
        // Prevent dismissing when tapping on the sheet itself
        // Only dismiss when tapping outside
    }

    private async Task Dismiss()
    {
        if (BindingContext is ImageSourceSelectionViewModel vm)
        {
            vm.DismissCommand.Execute(null);
        }
    }
}
