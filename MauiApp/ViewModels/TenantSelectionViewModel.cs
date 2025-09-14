using System.Windows.Input;
using System.Linq;
using System.Collections.Generic;
using MauiApp.Core.Models;

namespace MauiApp.ViewModels
{
    public class TenantSelectionViewModel : BottomSheetSelectionViewModel<Tenant>
    {
        private readonly Tenant? _currentSelectedTenant;

        public TenantSelectionViewModel(IEnumerable<Tenant> tenants, Tenant? currentSelectedTenant) 
            : base(tenants, tenant => tenant.Name, "Select Tenant")
        {
            _currentSelectedTenant = currentSelectedTenant;
        }

        protected override string? GetDescription(Tenant tenant)
        {
            return null;
        }

        protected override bool IsSelected(Tenant tenant)
        {
            return _currentSelectedTenant?.Id == tenant.Id;
        }
    }
}
