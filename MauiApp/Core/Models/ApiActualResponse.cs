using System.Text.Json.Serialization;

namespace MauiApp.Core.Models
{
    /// <summary>
    /// Actual API response format from the server
    /// </summary>
    public class ApiActualResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("result")]
        public string Result { get; set; } = string.Empty;

        [JsonPropertyName("error")]
        public string? Error { get; set; }

        [JsonPropertyName("unAuthorizedRequest")]
        public bool UnAuthorizedRequest { get; set; }
    }
}
