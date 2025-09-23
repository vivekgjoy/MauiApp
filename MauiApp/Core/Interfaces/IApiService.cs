using MauiApp.Core.Models;

namespace MauiApp.Core.Interfaces
{
    /// <summary>
    /// Interface for API service operations
    /// </summary>
    public interface IApiService
    {
        /// <summary>
        /// Authenticates user with the API
        /// </summary>
        /// <param name="request">Login request</param>
        /// <returns>API response with authentication data</returns>
        Task<ApiResponse<ApiAuthenticationResponse>> AuthenticateAsync(LoginRequest request);

        /// <summary>
        /// Refreshes authentication token
        /// </summary>
        /// <param name="refreshToken">Refresh token</param>
        /// <returns>API response with new authentication data</returns>
        Task<ApiResponse<ApiAuthenticationResponse>> RefreshTokenAsync(string refreshToken);

        /// <summary>
        /// Logs out user from the API
        /// </summary>
        /// <param name="token">Access token</param>
        /// <returns>API response</returns>
        Task<ApiResponse<object>> LogoutAsync(string token);

        /// <summary>
        /// Sets the authorization header for subsequent requests
        /// </summary>
        /// <param name="token">Access token</param>
        void SetAuthorizationHeader(string token);

        /// <summary>
        /// Clears the authorization header
        /// </summary>
        void ClearAuthorizationHeader();
    }
}
