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
        private readonly IApiService _apiService;
        private User? _currentUser;
        private const string UserKey = "CurrentUser";
        private const string TokenKey = "AuthToken";

        public AuthenticationService(INavigationService navigationService, IBiometricService biometricService, ICredentialStorageService credentialStorage, IAppStateService appStateService, IApiService apiService)
        {
            _navigationService = navigationService;
            _biometricService = biometricService;
            _credentialStorage = credentialStorage;
            _appStateService = appStateService;
            _apiService = apiService;
        }

        public async Task<LoginResponse> LoginAsync(LoginRequest request)
        {
            try
            {
                // Validate input
                if (string.IsNullOrEmpty(request.UsernameOrEmail) || string.IsNullOrEmpty(request.Password))
                {
                    return new LoginResponse
                    {
                        IsSuccess = false,
                        Message = "Username/Email and Password are required"
                    };
                }

                if (string.IsNullOrEmpty(request.TenancyName))
                {
                    return new LoginResponse
                    {
                        IsSuccess = false,
                        Message = "Tenancy name is required"
                    };
                }

                // Call the API service
                var apiResponse = await _apiService.AuthenticateAsync(request);

                // Debug: Log the API response
                System.Diagnostics.Debug.WriteLine($"=== AUTHENTICATION SERVICE RESPONSE ===");
                System.Diagnostics.Debug.WriteLine($"API Success: {apiResponse.Success}");
                System.Diagnostics.Debug.WriteLine($"API Message: {apiResponse.Message}");
                System.Diagnostics.Debug.WriteLine($"API Error: {apiResponse.Error}");
                System.Diagnostics.Debug.WriteLine($"API Error Code: {apiResponse.ErrorCode}");
                System.Diagnostics.Debug.WriteLine($"API Data is null: {apiResponse.Data == null}");
                if (apiResponse.Data != null)
                {
                    System.Diagnostics.Debug.WriteLine($"API Data AccessToken: {apiResponse.Data.AccessToken?.Substring(0, Math.Min(20, apiResponse.Data.AccessToken.Length))}...");
                    System.Diagnostics.Debug.WriteLine($"API Data User: {apiResponse.Data.User?.Username ?? "NULL"}");
                }
                System.Diagnostics.Debug.WriteLine($"=====================================");

                if (apiResponse.Success && apiResponse.Data != null)
                {
                    var authData = apiResponse.Data;
                    
                    // Debug: Log the parsed authentication data
                    System.Diagnostics.Debug.WriteLine($"=== PARSED AUTHENTICATION DATA ===");
                    System.Diagnostics.Debug.WriteLine($"Access Token: {authData.AccessToken?.Substring(0, Math.Min(20, authData.AccessToken.Length))}...");
                    System.Diagnostics.Debug.WriteLine($"Token Type: {authData.TokenType}");
                    System.Diagnostics.Debug.WriteLine($"Expires In: {authData.ExpiresIn} seconds");
                    System.Diagnostics.Debug.WriteLine($"User ID: {authData.User?.Id}");
                    System.Diagnostics.Debug.WriteLine($"Username: {authData.User?.Username}");
                    System.Diagnostics.Debug.WriteLine($"Email: {authData.User?.Email}");
                    System.Diagnostics.Debug.WriteLine($"First Name: {authData.User?.FirstName}");
                    System.Diagnostics.Debug.WriteLine($"Last Name: {authData.User?.LastName}");
                    System.Diagnostics.Debug.WriteLine($"Is Active: {authData.User?.IsActive}");
                    System.Diagnostics.Debug.WriteLine($"Last Login: {authData.User?.LastLoginDate}");
                    System.Diagnostics.Debug.WriteLine($"Created Date: {authData.User?.CreatedDate}");
                    System.Diagnostics.Debug.WriteLine($"Tenant ID: {authData.Tenant?.Id}");
                    System.Diagnostics.Debug.WriteLine($"Tenant Name: {authData.Tenant?.Name}");
                    System.Diagnostics.Debug.WriteLine($"Tenant Display Name: {authData.Tenant?.DisplayName}");
                    System.Diagnostics.Debug.WriteLine($"Tenant Is Active: {authData.Tenant?.IsActive}");
                    System.Diagnostics.Debug.WriteLine($"=================================");
                    
                    // Convert API user to our User model
                    var user = new User
                    {
                        Id = authData.User?.Id ?? 0,
                        Email = authData.User?.Email ?? request.UsernameOrEmail,
                        Username = authData.User?.Username ?? request.UsernameOrEmail,
                        FirstName = authData.User?.FirstName ?? "",
                        LastName = authData.User?.LastName ?? "",
                        TenantId = authData.Tenant?.Id ?? request.TenantId,
                        TenantName = authData.Tenant?.DisplayName ?? authData.Tenant?.Name ?? request.TenancyName,
                        IsBiometricEnabled = true,
                        LastLoginDate = authData.User?.LastLoginDate ?? DateTime.UtcNow,
                        CreatedDate = authData.User?.CreatedDate ?? DateTime.UtcNow.AddYears(-1)
                    };

                    var token = authData.AccessToken;
                    var expiresAt = DateTime.UtcNow.AddSeconds(authData.ExpiresIn);

                    // Store user data
                    await SecureStorage.SetAsync(UserKey, JsonSerializer.Serialize(user));
                    await SecureStorage.SetAsync(TokenKey, token);

                    // Save credentials for biometric login
                    await _credentialStorage.SaveCredentialsAsync(request.UsernameOrEmail, request.Password, token);

                    // Set authorization header for future API calls
                    _apiService.SetAuthorizationHeader(token);

                    // Mark app as running
                    await _appStateService.SetAppRunningAsync();

                    _currentUser = user;

                    var loginResponse = new LoginResponse
                    {
                        IsSuccess = true,
                        Message = apiResponse.Message,
                        User = user,
                        Token = token,
                        ExpiresAt = expiresAt
                    };

                    // Debug: Log the final login response
                    System.Diagnostics.Debug.WriteLine($"=== FINAL LOGIN RESPONSE ===");
                    System.Diagnostics.Debug.WriteLine($"Success: {loginResponse.IsSuccess}");
                    System.Diagnostics.Debug.WriteLine($"Message: {loginResponse.Message}");
                    System.Diagnostics.Debug.WriteLine($"User ID: {loginResponse.User?.Id}");
                    System.Diagnostics.Debug.WriteLine($"Username: {loginResponse.User?.Username}");
                    System.Diagnostics.Debug.WriteLine($"Email: {loginResponse.User?.Email}");
                    System.Diagnostics.Debug.WriteLine($"Tenant: {loginResponse.User?.TenantName}");
                    System.Diagnostics.Debug.WriteLine($"Token: {loginResponse.Token?.Substring(0, Math.Min(20, loginResponse.Token.Length))}...");
                    System.Diagnostics.Debug.WriteLine($"Expires At: {loginResponse.ExpiresAt}");
                    System.Diagnostics.Debug.WriteLine($"=============================");

                    return loginResponse;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"=== LOGIN FAILURE REASON ===");
                    System.Diagnostics.Debug.WriteLine($"API Success: {apiResponse.Success}");
                    System.Diagnostics.Debug.WriteLine($"API Data is null: {apiResponse.Data == null}");
                    System.Diagnostics.Debug.WriteLine($"API Message: '{apiResponse.Message}'");
                    System.Diagnostics.Debug.WriteLine($"API Error: '{apiResponse.Error}'");
                    System.Diagnostics.Debug.WriteLine($"=============================");

                    var errorResponse = new LoginResponse
                    {
                        IsSuccess = false,
                        Message = string.IsNullOrEmpty(apiResponse.Message) ? "Login failed: No data received from server" : apiResponse.Message,
                        Token = null,
                        ExpiresAt = null
                    };

                    // Debug: Log the error response
                    System.Diagnostics.Debug.WriteLine($"=== LOGIN ERROR RESPONSE ===");
                    System.Diagnostics.Debug.WriteLine($"Success: {errorResponse.IsSuccess}");
                    System.Diagnostics.Debug.WriteLine($"Message: {errorResponse.Message}");
                    System.Diagnostics.Debug.WriteLine($"=============================");

                    return errorResponse;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Login error: {ex.Message}");
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
                    FirstName = "",
                    LastName = "",
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
                // Get current token before clearing data
                var token = await SecureStorage.GetAsync(TokenKey);
                
                // Call API logout if token exists
                if (!string.IsNullOrEmpty(token))
                {
                    try
                    {
                        var apiResponse = await _apiService.LogoutAsync(token);
                        if (!apiResponse.Success)
                        {
                            System.Diagnostics.Debug.WriteLine($"API logout failed: {apiResponse.Message}");
                        }
                    }
                    catch (Exception apiEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"API logout error: {apiEx.Message}");
                        // Continue with local logout even if API call fails
                    }
                }

                // Clear authorization header
                _apiService.ClearAuthorizationHeader();

                // Clear all stored data
                SecureStorage.Remove(UserKey);
                SecureStorage.Remove(TokenKey);
                await _credentialStorage.ClearCredentialsAsync();
                await _appStateService.ClearAppStateAsync();
                _currentUser = null;
                
                // Navigate to login page on the main thread
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    try
                    {
                        await Shell.Current.GoToAsync("//LoginPage");
                    }
                    catch (Exception navEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"Navigation error during logout: {navEx.Message}");
                    }
                });
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
