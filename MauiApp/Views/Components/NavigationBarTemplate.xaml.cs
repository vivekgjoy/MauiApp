using System.Windows.Input;

namespace MauiApp.Views;

/*
    TEMPLATE FOR USING GENERIC NAVIGATION BAR IN ANY PAGE
    
    Copy this template and replace the content as needed:
    
    1. Replace "YourPageName" with your actual page class name
    2. Replace the namespace if needed
    3. Add your page logic here
    4. Set up the NavigationBar.BackCommand in the constructor
*/

public partial class YourPageName : ContentPage
{
    public YourPageName()
    {
        InitializeComponent();
        
        // Set up navigation bar back command
        NavigationBar.BackCommand = new Command(async () => await OnBackClicked());
        
        // Optional: Add right action button
        // NavigationBar.ShowRightAction = true;
        // NavigationBar.RightActionText = "Save";
        // NavigationBar.RightActionCommand = new Command(async () => await OnSaveClicked());
    }

    private async Task OnBackClicked()
    {
        await Navigation.PopAsync();
    }

    // Optional: Add your save method
    // private async Task OnSaveClicked()
    // {
    //     // Your save logic here
    // }
}

