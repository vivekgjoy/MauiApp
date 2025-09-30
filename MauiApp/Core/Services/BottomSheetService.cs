using MauiApp.Views;
using MauiApp.ViewModels;
using MauiApp.Core.Interfaces;
using MauiApp.Core.Models;

namespace MauiApp.Core.Services;

public class BottomSheetService : IBottomSheetService
{
    public async Task<T?> ShowSelectionAsync<T>(IEnumerable<T> items, Func<T, string> displaySelector, string title = "")
    {
        var page = new BottomSheetSelectionPage();
        var vm = new BottomSheetSelectionViewModel<T>(items, displaySelector, title);
        page.BindingContext = vm;

        await Application.Current.MainPage.Navigation.PushModalAsync(page, true);
        
        return vm.SelectedItem;
    }

    public async Task<Tenant?> ShowTenantSelectionAsync(IEnumerable<Tenant> tenants, Tenant? currentSelectedTenant = null)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("BottomSheetService.ShowTenantSelectionAsync called");
            var page = new BottomSheetSelectionPage();
            var vm = new TenantSelectionViewModel(tenants, currentSelectedTenant);
            page.BindingContext = vm;

            System.Diagnostics.Debug.WriteLine("Pushing modal page...");
            
            // Try different navigation approaches
            if (Application.Current?.MainPage is Shell shell)
            {
                await shell.Navigation.PushModalAsync(page, true);
            }
            else if (Application.Current?.MainPage is NavigationPage navPage)
            {
                await navPage.Navigation.PushModalAsync(page, true);
            }
            else
            {
                await Application.Current.MainPage.Navigation.PushModalAsync(page, true);
            }
            
            System.Diagnostics.Debug.WriteLine("Modal page pushed successfully");
            
            // Wait for the page to be dismissed and return the selected item
            var tcs = new TaskCompletionSource<Tenant?>();
            
            // Subscribe to the page's Disappearing event
            page.Disappearing += (sender, e) =>
            {
                tcs.SetResult(vm.SelectedItem);
            };
            
            return await tcs.Task;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"BottomSheetService error: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            throw;
        }
    }

    public async Task<string?> ShowSelectionAsync(string title, IEnumerable<string> options)
    {
        try
        {
            var page = new BottomSheetSelectionPage();
            var vm = new BottomSheetSelectionViewModel<string>(options, x => x, title);
            page.BindingContext = vm;

            await Application.Current.MainPage.Navigation.PushModalAsync(page, true);
            
            // Wait for the page to be dismissed and return the selected item
            var tcs = new TaskCompletionSource<string?>();
            
            // Subscribe to the page's Disappearing event
            page.Disappearing += (sender, e) =>
            {
                tcs.SetResult(vm.SelectedItem);
            };
            
            return await tcs.Task;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"BottomSheetService error: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            throw;
        }
    }
}