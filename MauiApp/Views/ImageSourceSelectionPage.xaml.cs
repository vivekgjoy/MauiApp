using MauiApp.ViewModels;

namespace MauiApp.Views;

public partial class ImageSourceSelectionPage : ContentPage
{
    private double _initialY;
    private double _currentY;
    private bool _isAnimating = false;

    public ImageSourceSelectionPage()
    {
        System.Diagnostics.Debug.WriteLine("ImageSourceSelectionPage constructor called");
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        System.Diagnostics.Debug.WriteLine("ImageSourceSelectionPage OnAppearing called");

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
