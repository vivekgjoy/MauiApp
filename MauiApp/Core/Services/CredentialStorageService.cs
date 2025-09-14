using MauiApp.Core.Interfaces;
using System;
using System.Threading.Tasks;

namespace MauiApp.Core.Services
{
    public class CredentialStorageService : ICredentialStorageService
    {
        private const string USERNAME_KEY = "saved_username";
        private const string PASSWORD_KEY = "saved_password";
        private const string TOKEN_KEY = "auth_token";
        private const string CREDENTIALS_VALID_KEY = "credentials_valid";

        public async Task SaveCredentialsAsync(string username, string password, string token)
        {
            try
            {
                await SecureStorage.SetAsync(USERNAME_KEY, username);
                await SecureStorage.SetAsync(PASSWORD_KEY, password);
                await SecureStorage.SetAsync(TOKEN_KEY, token);
                await SecureStorage.SetAsync(CREDENTIALS_VALID_KEY, "true");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving credentials: {ex.Message}");
            }
        }

        public async Task<(string Username, string Password, string Token)?> GetCredentialsAsync()
        {
            try
            {
                var username = await SecureStorage.GetAsync(USERNAME_KEY);
                var password = await SecureStorage.GetAsync(PASSWORD_KEY);
                var token = await SecureStorage.GetAsync(TOKEN_KEY);

                if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password) || string.IsNullOrEmpty(token))
                {
                    return null;
                }

                return (username, password, token);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting credentials: {ex.Message}");
                return null;
            }
        }

        public async Task ClearCredentialsAsync()
        {
            try
            {
                SecureStorage.Remove(USERNAME_KEY);
                SecureStorage.Remove(PASSWORD_KEY);
                SecureStorage.Remove(TOKEN_KEY);
                SecureStorage.Remove(CREDENTIALS_VALID_KEY);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error clearing credentials: {ex.Message}");
            }
        }

        public async Task<bool> HasValidCredentialsAsync()
        {
            try
            {
                var credentialsValid = await SecureStorage.GetAsync(CREDENTIALS_VALID_KEY);
                return credentialsValid == "true";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error checking credentials validity: {ex.Message}");
                return false;
            }
        }

        public async Task SaveTokenAsync(string token)
        {
            try
            {
                await SecureStorage.SetAsync(TOKEN_KEY, token);
                await SecureStorage.SetAsync(CREDENTIALS_VALID_KEY, "true");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving token: {ex.Message}");
            }
        }

        public async Task<string> GetTokenAsync()
        {
            try
            {
                return await SecureStorage.GetAsync(TOKEN_KEY);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting token: {ex.Message}");
                return null;
            }
        }

        public async Task ClearTokenAsync()
        {
            try
            {
                SecureStorage.Remove(TOKEN_KEY);
                SecureStorage.Remove(CREDENTIALS_VALID_KEY);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error clearing token: {ex.Message}");
            }
        }
    }
}
