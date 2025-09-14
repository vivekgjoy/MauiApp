using MauiApp.ViewModels;

namespace MauiApp.Views;

public partial class BottomSheetSelectionPage : ContentPage
{
    private double _initialY;
    private double _currentY;
    private bool _isAnimating = false;

    public BottomSheetSelectionPage()
    {
        System.Diagnostics.Debug.WriteLine("BottomSheetSelectionPage constructor called");
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        System.Diagnostics.Debug.WriteLine("BottomSheetSelectionPage OnAppearing called");
        
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
        if (_isAnimating) return;

        switch (e.StatusType)
        {
            case GestureStatus.Started:
                _initialY = Sheet.TranslationY;
                break;

            case GestureStatus.Running:
                _currentY = _initialY + e.TotalY;
                if (_currentY >= 0) // Only allow downward swipes
                {
                    Sheet.TranslationY = _currentY;
                }
                break;

            case GestureStatus.Completed:
                var threshold = Sheet.Height * 0.3; // 30% of sheet height
                
                if (_currentY > threshold) // If swiped down enough, close
                {
                    await CloseBottomSheet();
                }
                else // Otherwise, snap back
                {
                    await Sheet.TranslateTo(0, 0, 250, Easing.CubicOut);
                }
                break;
        }
    }

    private async Task CloseBottomSheet()
    {
        await AnimateOut();
        await Navigation.PopModalAsync();
    }

    private void OnSheetTapped(object sender, EventArgs e)
    {
        // Prevent tap from propagating to background
        // This ensures tapping on the sheet doesn't close it
    }
}