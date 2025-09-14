using MauiApp.Core.Interfaces;
using MauiApp.Core.Models;
using System.Text.Json;

namespace MauiApp.Core.Services
{
    /// <summary>
    /// Service for handling authentication operations
    /// </summary>
    public class AuthenticationService : IAuthenticationService
    {
        private readonly INavigationService _navigationService;
        private readonly IBiometricService _biometricService;
        private readonly ICredentialStorageService _credentialStorage;
        private readonly IAppStateService _appStateService;
        private User? _currentUser;
        private const string UserKey = "CurrentUser";
        private const string TokenKey = "AuthToken";

        public AuthenticationService(INavigationService navigationService, IBiometricService biometricService, ICredentialStorageService credentialStorage, IAppStateService appStateService)
        {
            _navigationService = navigationService;
            _biometricService = biometricService;
            _credentialStorage = credentialStorage;
            _appStateService = appStateService;
        }

        public async Task<LoginResponse> LoginAsync(LoginRequest request)
        {
            try
            {
                // Simulate API call - replace with actual API implementation
                await Task.Delay(1000);

                // Mock authentication logic
                if (string.IsNullOrEmpty(request.UsernameOrEmail) || string.IsNullOrEmpty(request.Password))
                {
                    return new LoginResponse
                    {
                        IsSuccess = false,
                        Message = "Username/Email and Password are required"
                    };
                }

                // Mock successful login
                var user = new User
                {
                    Id = 1,
                    Email = request.UsernameOrEmail.Contains("@") ? request.UsernameOrEmail : $"{request.UsernameOrEmail}@company.com",
                    Username = request.UsernameOrEmail,
                    FirstName = "Udhayakumar",
                    LastName = "S",
                    TenantId = request.TenantId,
                    TenantName = "SmartERP",
                    IsBiometricEnabled = true,
                    LastLoginDate = DateTime.UtcNow,
                    CreatedDate = DateTime.UtcNow.AddYears(-1)
                };

                var token = $"mock_token_{Guid.NewGuid()}";

                // Store user data
                await SecureStorage.SetAsync(UserKey, JsonSerializer.Serialize(user));
                await SecureStorage.SetAsync(TokenKey, token);

                // Save credentials for biometric login
                await _credentialStorage.SaveCredentialsAsync(request.UsernameOrEmail, request.Password, token);

                // Mark app as running
                await _appStateService.SetAppRunningAsync();

                _currentUser = user;

                return new LoginResponse
                {
                    IsSuccess = true,
                    Message = "Login successful",
                    User = user,
                    Token = token,
                    ExpiresAt = DateTime.UtcNow.AddHours(8)
                };
            }
            catch (Exception ex)
            {
                return new LoginResponse
                {
                    IsSuccess = false,
                    Message = $"Login failed: {ex.Message}"
                };
            }
        }

        public async Task<LoginResponse> LoginWithBiometricAsync()
        {
            try
            {
                // Check if biometric is available
                if (!await _biometricService.IsBiometricAvailableAsync())
                {
                    return new LoginResponse
                    {
                        IsSuccess = false,
                        Message = "Biometric authentication is not available on this device"
                    };
                }

                // Check if valid credentials exist
                if (!await _credentialStorage.HasValidCredentialsAsync())
                {
                    return new LoginResponse
                    {
                        IsSuccess = false,
                        Message = "No saved credentials found. Please login with username and password first."
                    };
                }

                // Authenticate using biometric
                var biometricSuccess = await _biometricService.AuthenticateAsync("Authenticate to access your account");
                if (!biometricSuccess)
                {
                    return new LoginResponse
                    {
                        IsSuccess = false,
                        Message = "Biometric authentication failed"
                    };
                }

                // Get saved credentials
                var credentials = await _credentialStorage.GetCredentialsAsync();
                if (credentials == null)
                {
                    return new LoginResponse
                    {
                        IsSuccess = false,
                        Message = "No valid credentials found. Please login with username and password."
                    };
                }

                // Use saved credentials to authenticate without re-saving
                var user = new User
                {
                    Id = 1,
                    Email = credentials.Value.Username.Contains("@") ? credentials.Value.Username : $"{credentials.Value.Username}@company.com",
                    Username = credentials.Value.Username,
                    FirstName = "Udhayakumar",
                    LastName = "S",
                    TenantId = "1",
                    TenantName = "SmartERP",
                    IsBiometricEnabled = true,
                    LastLoginDate = DateTime.UtcNow,
                    CreatedDate = DateTime.UtcNow.AddYears(-1)
                };

                // Store user data and token
                await SecureStorage.SetAsync(UserKey, JsonSerializer.Serialize(user));
                await SecureStorage.SetAsync(TokenKey, credentials.Value.Token);

                // Mark app as running
                await _appStateService.SetAppRunningAsync();

                _currentUser = user;

                return new LoginResponse
                {
                    IsSuccess = true,
                    Message = "Biometric login successful",
                    User = user,
                    Token = credentials.Value.Token,
                    ExpiresAt = DateTime.UtcNow.AddHours(8)
                };
            }
            catch (Exception ex)
            {
                return new LoginResponse
                {
                    IsSuccess = false,
                    Message = $"Biometric login failed: {ex.Message}"
                };
            }
        }

        public async Task LogoutAsync()
        {
            try
            {
                // Clear all stored data
                SecureStorage.Remove(UserKey);
                SecureStorage.Remove(TokenKey);
                await _credentialStorage.ClearCredentialsAsync();
                await _appStateService.ClearAppStateAsync();
                _currentUser = null;
                
                await _navigationService.NavigateToRootAsync("//LoginPage");
            }
            catch (Exception ex)
            {
                // Log error
                System.Diagnostics.Debug.WriteLine($"Logout error: {ex.Message}");
            }
        }

        public async Task ClearSessionAsync()
        {
            try
            {
                // Clear session data but keep credentials for biometric login
                SecureStorage.Remove(UserKey);
                SecureStorage.Remove(TokenKey);
                await _appStateService.ClearAppStateAsync();
                _currentUser = null;
                
                // Don't clear credentials - keep them for biometric login
                System.Diagnostics.Debug.WriteLine("Session cleared but credentials kept for biometric login");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Clear session error: {ex.Message}");
            }
        }

        public async Task<bool> IsBiometricAvailableAsync()
        {
            try
            {
                return await _biometricService.IsBiometricAvailableAsync();
            }
            catch
            {
                return false;
            }
        }

        public async Task<User?> GetCurrentUserAsync()
        {
            try
            {
                if (_currentUser != null)
                    return _currentUser;

                var userJson = await SecureStorage.GetAsync(UserKey);
                if (!string.IsNullOrEmpty(userJson))
                {
                    _currentUser = JsonSerializer.Deserialize<User>(userJson);
                }

                return _currentUser;
            }
            catch
            {
                return null;
            }
        }

        public async Task<bool> IsAuthenticatedAsync()
        {
            try
            {
                var user = await GetCurrentUserAsync();
                var token = await SecureStorage.GetAsync(TokenKey);
                var wasProperlyClosed = await _appStateService.WasAppProperlyClosedAsync();
                
                // User is authenticated only if:
                // 1. User data exists
                // 2. Token exists
                // 3. App was properly closed (not killed)
                return user != null && !string.IsNullOrEmpty(token) && wasProperlyClosed;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> HasValidCredentialsAsync()
        {
            try
            {
                // Check if user has valid saved credentials (for biometric login)
                // This should return true even after app kill, as long as credentials exist
                return await _credentialStorage.HasValidCredentialsAsync();
            }
            catch
            {
                return false;
            }
        }
    }
}
