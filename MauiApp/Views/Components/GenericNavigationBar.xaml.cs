using System.Windows.Input;

namespace MauiApp.Views.Components;

public partial class GenericNavigationBar : ContentView
{
    public static readonly BindableProperty TitleProperty =
        BindableProperty.Create(nameof(Title), typeof(string), typeof(GenericNavigationBar), string.Empty);

    public static readonly BindableProperty ShowRightActionProperty =
        BindableProperty.Create(nameof(ShowRightAction), typeof(bool), typeof(GenericNavigationBar), false);

    public static readonly BindableProperty RightActionTextProperty =
        BindableProperty.Create(nameof(RightActionText), typeof(string), typeof(GenericNavigationBar), string.Empty);

    public static readonly BindableProperty BackCommandProperty =
        BindableProperty.Create(nameof(BackCommand), typeof(ICommand), typeof(GenericNavigationBar), null);

    public static readonly BindableProperty RightActionCommandProperty =
        BindableProperty.Create(nameof(RightActionCommand), typeof(ICommand), typeof(GenericNavigationBar), null);

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public bool ShowRightAction
    {
        get => (bool)GetValue(ShowRightActionProperty);
        set => SetValue(ShowRightActionProperty, value);
    }

    public string RightActionText
    {
        get => (string)GetValue(RightActionTextProperty);
        set => SetValue(RightActionTextProperty, value);
    }

    public ICommand BackCommand
    {
        get => (ICommand)GetValue(BackCommandProperty);
        set => SetValue(BackCommandProperty, value);
    }

    public ICommand RightActionCommand
    {
        get => (ICommand)GetValue(RightActionCommandProperty);
        set => SetValue(RightActionCommandProperty, value);
    }

    public GenericNavigationBar()
    {
        InitializeComponent();
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        if (BackCommand != null && BackCommand.CanExecute(null))
        {
            BackCommand.Execute(null);
            return;
        }

        // Fallback: Use Shell or Navigation
        try
        {
            if (Application.Current?.MainPage is Shell shell)
            {
                await shell.GoToAsync("..", true);
            }
            else if (Application.Current?.MainPage?.Navigation?.NavigationStack?.Count > 1)
            {
                await Application.Current.MainPage.Navigation.PopAsync(true);
            }
        }
        catch
        {
            // safe fallback, ignore errors
        }
    }
}
