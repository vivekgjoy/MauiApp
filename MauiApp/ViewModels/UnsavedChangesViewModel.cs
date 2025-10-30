using System.Windows.Input;
using Microsoft.Maui.Controls;
using MauiApp.Views;

namespace MauiApp.ViewModels
{
    public class UnsavedChangesViewModel
    {
        private readonly TaskCompletionSource<UnsavedChangesResult> _tcs = new();
        public Task<UnsavedChangesResult> ResultTask => _tcs.Task;

        public ICommand ContinueCommand { get; }
        public ICommand StartNewCommand { get; }

        public UnsavedChangesViewModel()
        {
            ContinueCommand = new Command(async () =>
            {
                SetResult(UnsavedChangesResult.Continue);
                await Application.Current.MainPage.Navigation.PopModalAsync();
            });

            StartNewCommand = new Command(async () =>
            {
                SetResult(UnsavedChangesResult.StartNew);
                await Application.Current.MainPage.Navigation.PopModalAsync();
            });
        }

        public void SetResult(UnsavedChangesResult result)
        {
            if (!_tcs.Task.IsCompleted)
            {
                _tcs.SetResult(result);
            }
        }
    }
}


