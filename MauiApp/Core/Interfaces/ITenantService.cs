using MauiApp.Core.Models;

namespace MauiApp.Core.Interfaces
{
    /// <summary>
    /// Interface for tenant operations
    /// </summary>
    public interface ITenantService
    {
        /// <summary>
        /// Gets all available tenants
        /// </summary>
        Task<IEnumerable<Tenant>> GetTenantsAsync();
        
        /// <summary>
        /// Gets a specific tenant by ID
        /// </summary>
        Task<Tenant?> GetTenantAsync(string tenantId);
        
        /// <summary>
        /// Sets the current tenant
        /// </summary>
        Task SetCurrentTenantAsync(string tenantId);
        
        /// <summary>
        /// Gets the current tenant
        /// </summary>
        Task<Tenant?> GetCurrentTenantAsync();
    }
}
