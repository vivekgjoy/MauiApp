using System.ComponentModel.DataAnnotations;

namespace MauiApp.Core.Models
{
    /// <summary>
    /// Represents a user in the system
    /// </summary>
    public class User
    {
        public int Id { get; set; }
        
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
        
        [Required]
        public string Username { get; set; } = string.Empty;
        
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        
        public string FullName => $"{FirstName} {LastName}".Trim();
        
        public string TenantId { get; set; } = string.Empty;
        public string TenantName { get; set; } = string.Empty;
        
        public bool IsBiometricEnabled { get; set; }
        public DateTime LastLoginDate { get; set; }
        public DateTime CreatedDate { get; set; }
    }
}
