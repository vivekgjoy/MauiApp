using System.Threading.Tasks;
using Microsoft.Maui.Controls;
#if ANDROID
using Android.Views.InputMethods;
using Android.Content;
#endif

namespace MauiApp.ImageEditor.Views.ToolPanels;

public partial class TextToolPanel : ContentView
{
    public TextToolPanel()
    {
        InitializeComponent();
        
        // Add tap gesture to ensure keyboard opens when tapping the entry
        if (TextEntry != null)
        {
            var tapGesture = new TapGestureRecognizer();
            tapGesture.Tapped += async (s, e) =>
            {
                await Task.Delay(100);
                FocusEntry();
            };
            TextEntry.GestureRecognizers.Add(tapGesture);
        }
    }

    private void OnTextEntryFocused(object? sender, FocusEventArgs e)
    {
        
        // If text is "Enter Text", select all so typing replaces it
        if (TextEntry != null && TextEntry.Text == "Enter Text")
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await Task.Delay(100);
                TextEntry.CursorPosition = 0;
                TextEntry.SelectionLength = TextEntry.Text?.Length ?? 0;
            });
        }
        
#if ANDROID
        // Ensure keyboard opens when entry is focused
        if (TextEntry?.Handler?.PlatformView is Android.Widget.EditText editText)
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await Task.Delay(200);
                var imm = (InputMethodManager)Android.App.Application.Context
                    .GetSystemService(Android.Content.Context.InputMethodService)!;
                imm.ShowSoftInput(editText, 0);
            });
        }
#endif
    }

    private void OnTextEntryTextChanged(object? sender, TextChangedEventArgs e)
    {
        // Handle "Enter Text" placeholder clearing when user starts typing
        if (TextEntry != null && BindingContext is ViewModels.TextToolViewModel viewModel)
        {
            string currentText = TextEntry.Text ?? string.Empty;
            string oldText = e.OldTextValue ?? string.Empty;
            
            // If the old text was "Enter Text" and user typed something different, 
            // the Entry should have already replaced it (if selected), but ensure viewmodel is synced
            if (oldText == "Enter Text" && currentText != "Enter Text")
            {
                // User started typing - text is already updated in Entry, just sync viewmodel
                // The binding should handle this, but we ensure it's correct
                if (viewModel.InputText == "Enter Text" && currentText.Length > 0)
                {
                    // Temporarily remove handler to avoid recursion
                    TextEntry.TextChanged -= OnTextEntryTextChanged;
                    viewModel.InputText = currentText;
                    TextEntry.TextChanged += OnTextEntryTextChanged;
                }
            }
        }
    }

    public void FocusEntry()
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                if (TextEntry == null)
                {
                    return;
                }

                // Ensure Entry is visible and enabled
                TextEntry.IsVisible = true;
                TextEntry.IsEnabled = true;
                
                // Always unfocus first to reset state
                TextEntry.Unfocus();
                await Task.Delay(200);

                // Wait for handler to be ready
                int retries = 0;
                while (TextEntry.Handler == null && retries < 10)
                {
                    await Task.Delay(100);
                    retries++;
                }

                // Focus the entry
                bool focused = TextEntry.Focus();
                await Task.Delay(200);

#if ANDROID
                // On Android, show keyboard
                // This ensures keyboard opens every time, not just the first time
                await Task.Delay(300);

                if (TextEntry.Handler?.PlatformView is Android.Widget.EditText editText)
                {
                    var imm = (InputMethodManager)Android.App.Application.Context
                        .GetSystemService(Context.InputMethodService)!;

                    // Force show keyboard - always call ShowSoftInput to ensure it opens
                    // Use 0 for .NET 9 compatibility (equivalent to ShowFlags.None)
                    imm.ShowSoftInput(editText, 0);
                    
                    // Also try focusing again to ensure it's active
                    if (!TextEntry.IsFocused)
                    {
                        await Task.Delay(200);
                        TextEntry.Focus();
                        // Try showing keyboard again
                        imm.ShowSoftInput(editText, 0);
                    }
                }
                else
                {
                    // Fallback: try focusing again
                    await Task.Delay(200);
                    TextEntry.Focus();
                }
#endif

#if IOS || MACCATALYST
                await Task.Delay(300);
                TextEntry.Focus();
#endif

#if WINDOWS
                // On Windows, ensure focus
                if (!TextEntry.IsFocused)
                {
                    await Task.Delay(200);
                    TextEntry.Focus();
                }
#endif
            }
            catch (Exception ex)
            {
            }
        });
    }

    public void UnfocusEntry()
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                if (TextEntry == null)
                {
                    return;
                }

                // Unfocus the entry
                TextEntry.Unfocus();
                await Task.Delay(100);

#if ANDROID
                // On Android, hide keyboard
                if (TextEntry.Handler?.PlatformView is Android.Widget.EditText editText)
                {
                    var imm = (InputMethodManager)Android.App.Application.Context
                        .GetSystemService(Context.InputMethodService)!;
                    
                    // Hide keyboard
                    imm.HideSoftInputFromWindow(editText.WindowToken, 0);
                }
#endif
            }
            catch (Exception ex)
            {
            }
        });
    }
}
