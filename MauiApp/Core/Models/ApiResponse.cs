using System.Text.Json.Serialization;

namespace MauiApp.Core.Models
{
    /// <summary>
    /// Generic API response wrapper
    /// </summary>
    /// <typeparam name="T">Type of data in the response</typeparam>
    public class ApiResponse<T>
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("data")]
        public T? Data { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }

        [JsonPropertyName("errorCode")]
        public string? ErrorCode { get; set; }

        [JsonPropertyName("timestamp")]
        public DateTime? Timestamp { get; set; }
    }

    /// <summary>
    /// API response for authentication endpoint
    /// </summary>
    public class ApiAuthenticationResponse
    {
        [JsonPropertyName("accessToken")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonPropertyName("refreshToken")]
        public string? RefreshToken { get; set; }

        [JsonPropertyName("expiresIn")]
        public int ExpiresIn { get; set; }

        [JsonPropertyName("tokenType")]
        public string TokenType { get; set; } = "Bearer";

        [JsonPropertyName("user")]
        public ApiUser? User { get; set; }

        [JsonPropertyName("tenant")]
        public ApiTenant? Tenant { get; set; }
    }

    /// <summary>
    /// API user model
    /// </summary>
    public class ApiUser
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("username")]
        public string Username { get; set; } = string.Empty;

        [JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;

        [JsonPropertyName("firstName")]
        public string? FirstName { get; set; }

        [JsonPropertyName("lastName")]
        public string? LastName { get; set; }

        [JsonPropertyName("isActive")]
        public bool IsActive { get; set; }

        [JsonPropertyName("lastLoginDate")]
        public DateTime? LastLoginDate { get; set; }

        [JsonPropertyName("createdDate")]
        public DateTime? CreatedDate { get; set; }
    }

    /// <summary>
    /// API tenant model
    /// </summary>
    public class ApiTenant
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("displayName")]
        public string? DisplayName { get; set; }

        [JsonPropertyName("isActive")]
        public bool IsActive { get; set; }
    }
}
