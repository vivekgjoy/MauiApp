using System.ComponentModel.DataAnnotations;

namespace MauiApp.Core.Models
{
    /// <summary>
    /// Represents a login request
    /// </summary>
    public class LoginRequest
    {
        [Required]
        public string UsernameOrEmail { get; set; } = string.Empty;
        
        [Required]
        public string Password { get; set; } = string.Empty;
        
        public string TenantId { get; set; } = string.Empty;
        public bool UseBiometric { get; set; }
    }
}
