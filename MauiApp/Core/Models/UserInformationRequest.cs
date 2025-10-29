using System.ComponentModel.DataAnnotations;

namespace MauiApp.Core.Models
{
    /// <summary>
    /// Represents a user information request
    /// </summary>
    public class UserInformationRequest
    {
        [Required]
        public string UserName { get; set; } = string.Empty;
    }
}









