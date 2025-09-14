using MauiApp.Core.Models;

namespace MauiApp.Core.Interfaces
{
    /// <summary>
    /// Interface for authentication operations
    /// </summary>
    public interface IAuthenticationService
    {
        /// <summary>
        /// Authenticates user with username/email and password
        /// </summary>
        Task<LoginResponse> LoginAsync(LoginRequest request);
        
        /// <summary>
        /// Authenticates user using biometric authentication
        /// </summary>
        Task<LoginResponse> LoginWithBiometricAsync();
        
        /// <summary>
        /// Logs out the current user
        /// </summary>
        Task LogoutAsync();
        
        /// <summary>
        /// Clears session data but keeps credentials for biometric login
        /// </summary>
        Task ClearSessionAsync();
        
        /// <summary>
        /// Checks if biometric authentication is available
        /// </summary>
        Task<bool> IsBiometricAvailableAsync();
        
        /// <summary>
        /// Gets the current authenticated user
        /// </summary>
        Task<User?> GetCurrentUserAsync();
        
        /// <summary>
        /// Checks if user is currently authenticated
        /// </summary>
        Task<bool> IsAuthenticatedAsync();
        
        /// <summary>
        /// Checks if user has valid saved credentials (for biometric login)
        /// </summary>
        Task<bool> HasValidCredentialsAsync();
    }
}
