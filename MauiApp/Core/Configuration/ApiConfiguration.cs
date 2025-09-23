namespace MauiApp.Core.Configuration
{
    /// <summary>
    /// API configuration constants and settings
    /// </summary>
    public static class ApiConfiguration
    {
        /// <summary>
        /// Base URL for the API
        /// </summary>
        public const string BaseUrl = "https://sixstardemo.dyndns.org";

        /// <summary>
        /// API endpoints
        /// </summary>
        public static class Endpoints
        {
            public const string Authenticate = "/api/Account/Authenticate";
            public const string RefreshToken = "/api/Account/RefreshToken";
            public const string Logout = "/api/Account/Logout";
        }

        /// <summary>
        /// HTTP client configuration
        /// </summary>
        public static class HttpClient
        {
            public const int TimeoutSeconds = 30;
            public const int RetryCount = 3;
            public const int RetryDelayMs = 1000;
        }

        /// <summary>
        /// Content types
        /// </summary>
        public static class ContentTypes
        {
            public const string Json = "application/json";
            public const string FormUrlEncoded = "application/x-www-form-urlencoded";
        }

        /// <summary>
        /// HTTP headers
        /// </summary>
        public static class Headers
        {
            public const string Authorization = "Authorization";
            public const string ContentType = "Content-Type";
            public const string Accept = "Accept";
            public const string UserAgent = "User-Agent";
        }

        /// <summary>
        /// Error codes
        /// </summary>
        public static class ErrorCodes
        {
            public const string NetworkError = "NETWORK_ERROR";
            public const string TimeoutError = "TIMEOUT_ERROR";
            public const string Unauthorized = "UNAUTHORIZED";
            public const string Forbidden = "FORBIDDEN";
            public const string NotFound = "NOT_FOUND";
            public const string ServerError = "SERVER_ERROR";
            public const string ValidationError = "VALIDATION_ERROR";
            public const string UnknownError = "UNKNOWN_ERROR";
        }
    }
}
