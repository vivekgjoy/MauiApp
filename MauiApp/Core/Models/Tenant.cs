namespace MauiApp.Core.Models
{
    /// <summary>
    /// Represents a tenant in the multi-tenant system
    /// </summary>
    public class Tenant
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string LogoUrl { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
    }
}
