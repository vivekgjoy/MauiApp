namespace MauiApp.Core.Models
{
    /// <summary>
    /// Represents the response from a login attempt
    /// </summary>
    public class LoginResponse
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; } = string.Empty;
        public User? User { get; set; }
        public string? Token { get; set; }
        public DateTime? ExpiresAt { get; set; }
    }
}
