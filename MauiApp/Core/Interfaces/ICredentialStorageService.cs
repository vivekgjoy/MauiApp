using System.Threading.Tasks;

namespace MauiApp.Core.Interfaces
{
    public interface ICredentialStorageService
    {
        /// <summary>
        /// Saves user credentials securely
        /// </summary>
        Task SaveCredentialsAsync(string username, string password, string token);

        /// <summary>
        /// Gets saved credentials
        /// </summary>
        Task<(string Username, string Password, string Token)?> GetCredentialsAsync();

        /// <summary>
        /// Clears all saved credentials
        /// </summary>
        Task ClearCredentialsAsync();

        /// <summary>
        /// Checks if valid credentials exist
        /// </summary>
        Task<bool> HasValidCredentialsAsync();

        /// <summary>
        /// Saves authentication token
        /// </summary>
        Task SaveTokenAsync(string token);

        /// <summary>
        /// Gets authentication token
        /// </summary>
        Task<string> GetTokenAsync();

        /// <summary>
        /// Clears authentication token
        /// </summary>
        Task ClearTokenAsync();
    }
}
