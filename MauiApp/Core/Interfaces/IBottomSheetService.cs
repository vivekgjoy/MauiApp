using MauiApp.Core.Models;

namespace MauiApp.Core.Interfaces
{
    public interface IBottomSheetService
    {
        Task<T?> ShowSelectionAsync<T>(IEnumerable<T> items, Func<T, string> displaySelector, string title = "");
        Task<Tenant?> ShowTenantSelectionAsync(IEnumerable<Tenant> tenants, Tenant? currentSelectedTenant = null);
    }
}


