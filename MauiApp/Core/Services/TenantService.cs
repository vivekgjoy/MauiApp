using MauiApp.Core.Interfaces;
using MauiApp.Core.Models;

namespace MauiApp.Core.Services
{
    /// <summary>
    /// Service for handling tenant operations
    /// </summary>
    public class TenantService : ITenantService
    {
        private readonly List<Tenant> _tenants;
        private Tenant? _currentTenant;
        private const string CurrentTenantKey = "CurrentTenant";

        public TenantService()
        {
            // Mock tenant data - replace with actual API calls
            _tenants = new List<Tenant>
            {
                new Tenant
                {
                    Id = "1",
                    Name = "SmartERP",
                    Description = "SmartERP Enterprise Solutions",
                    LogoUrl = "smart_erp_logo.png",
                    IsActive = true
                },
                new Tenant
                {
                    Id = "2",
                    Name = "TechCorp",
                    Description = "Technology Corporation",
                    LogoUrl = "tech_corp_logo.png",
                    IsActive = true
                },
                new Tenant
                {
                    Id = "3",
                    Name = "GlobalBiz",
                    Description = "Global Business Solutions",
                    LogoUrl = "global_biz_logo.png",
                    IsActive = true
                }
            };
        }

        public async Task<IEnumerable<Tenant>> GetTenantsAsync()
        {
            // Simulate API call
            await Task.Delay(500);
            return _tenants.Where(t => t.IsActive);
        }

        public async Task<Tenant?> GetTenantAsync(string tenantId)
        {
            await Task.Delay(200);
            return _tenants.FirstOrDefault(t => t.Id == tenantId && t.IsActive);
        }

        public async Task SetCurrentTenantAsync(string tenantId)
        {
            var tenant = await GetTenantAsync(tenantId);
            if (tenant != null)
            {
                _currentTenant = tenant;
                await SecureStorage.SetAsync(CurrentTenantKey, tenantId);
            }
        }

        public async Task<Tenant?> GetCurrentTenantAsync()
        {
            if (_currentTenant != null)
                return _currentTenant;

            try
            {
                var tenantId = await SecureStorage.GetAsync(CurrentTenantKey);
                if (!string.IsNullOrEmpty(tenantId))
                {
                    _currentTenant = await GetTenantAsync(tenantId);
                }
            }
            catch
            {
                // Handle error silently
            }

            return _currentTenant;
        }
    }
}
