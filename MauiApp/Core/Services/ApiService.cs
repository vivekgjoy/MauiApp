using MauiApp.Core.Configuration;
using MauiApp.Core.Interfaces;
using MauiApp.Core.Models;
using System.Net;
using System.Text;
using System.Text.Json;

namespace MauiApp.Core.Services
{
    /// <summary>
    /// Service for handling API calls
    /// </summary>
    public class ApiService : IApiService, IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _jsonOptions;

        public ApiService()
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(ApiConfiguration.BaseUrl),
                Timeout = TimeSpan.FromSeconds(ApiConfiguration.HttpClient.TimeoutSeconds)
            };

            // Configure default headers
            _httpClient.DefaultRequestHeaders.Add(ApiConfiguration.Headers.Accept, ApiConfiguration.ContentTypes.Json);
            _httpClient.DefaultRequestHeaders.Add(ApiConfiguration.Headers.UserAgent, "SmartERP-Mobile/1.0");

            // Configure JSON serialization options
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true,
                WriteIndented = false
            };
        }

        /// <summary>
        /// Authenticates user with the API
        /// </summary>
        public async Task<ApiResponse<ApiAuthenticationResponse>> AuthenticateAsync(LoginRequest request)
        {
            try
            {
                // Create API request model
                var apiRequest = new
                {
                    TenancyName = request.TenancyName,
                    UsernameOrEmailAddress = request.UsernameOrEmail,
                    Password = request.Password
                };

                var json = JsonSerializer.Serialize(apiRequest, _jsonOptions);
                var content = new StringContent(json, Encoding.UTF8, ApiConfiguration.ContentTypes.Json);

                System.Diagnostics.Debug.WriteLine($"=== API REQUEST ===");
                System.Diagnostics.Debug.WriteLine($"URL: {ApiConfiguration.BaseUrl}{ApiConfiguration.Endpoints.Authenticate}");
                System.Diagnostics.Debug.WriteLine($"Request Body: {json}");
                System.Diagnostics.Debug.WriteLine($"==================");

                var response = await _httpClient.PostAsync(ApiConfiguration.Endpoints.Authenticate, content);
                var responseContent = await response.Content.ReadAsStringAsync();

                System.Diagnostics.Debug.WriteLine($"=== API RESPONSE ===");
                System.Diagnostics.Debug.WriteLine($"Status Code: {response.StatusCode}");
                System.Diagnostics.Debug.WriteLine($"Is Success: {response.IsSuccessStatusCode}");
                System.Diagnostics.Debug.WriteLine($"Response Headers: {string.Join(", ", response.Headers.Select(h => $"{h.Key}: {string.Join(", ", h.Value)}"))}");
                System.Diagnostics.Debug.WriteLine($"Content Type: {response.Content.Headers.ContentType}");
                System.Diagnostics.Debug.WriteLine($"Response Content Length: {responseContent.Length}");
                System.Diagnostics.Debug.WriteLine($"Response Content: {responseContent}");
                System.Diagnostics.Debug.WriteLine($"===================");

                if (response.IsSuccessStatusCode)
                {
                    // Try to deserialize the actual API response format first
                    try
                    {
                        System.Diagnostics.Debug.WriteLine($"=== ATTEMPTING JSON DESERIALIZATION ===");
                        System.Diagnostics.Debug.WriteLine($"Raw response content: '{responseContent}'");
                        System.Diagnostics.Debug.WriteLine($"Response content length: {responseContent?.Length ?? 0}");
                        System.Diagnostics.Debug.WriteLine($"Trying to deserialize as ApiActualResponse");
                        var actualResponse = JsonSerializer.Deserialize<ApiActualResponse>(responseContent, _jsonOptions);
                        System.Diagnostics.Debug.WriteLine($"Actual response deserialization result: {actualResponse != null}");
                        
                        if (actualResponse != null && actualResponse.Success)
                        {
                            System.Diagnostics.Debug.WriteLine($"=== PARSED ACTUAL API RESPONSE ===");
                            System.Diagnostics.Debug.WriteLine($"Success: {actualResponse.Success}");
                            System.Diagnostics.Debug.WriteLine($"Result (Token): {actualResponse.Result?.Substring(0, Math.Min(20, actualResponse.Result.Length))}...");
                            System.Diagnostics.Debug.WriteLine($"Error: {actualResponse.Error}");
                            System.Diagnostics.Debug.WriteLine($"UnAuthorizedRequest: {actualResponse.UnAuthorizedRequest}");
                            System.Diagnostics.Debug.WriteLine($"================================");
                            
                            // Convert to our expected format
                            var authData = new ApiAuthenticationResponse
                            {
                                AccessToken = actualResponse.Result,
                                TokenType = "Bearer",
                                ExpiresIn = 3600, // Default to 1 hour
                                User = new ApiUser
                                {
                                    Id = 1, // Default user ID
                                    Username = request.UsernameOrEmail,
                                    Email = request.UsernameOrEmail.Contains("@") ? request.UsernameOrEmail : $"{request.UsernameOrEmail}@company.com",
                                    FirstName = "",
                                    LastName = "",
                                    IsActive = true,
                                    LastLoginDate = DateTime.UtcNow,
                                    CreatedDate = DateTime.UtcNow.AddYears(-1)
                                },
                                Tenant = new ApiTenant
                                {
                                    Id = request.TenantId,
                                    Name = request.TenancyName,
                                    DisplayName = request.TenancyName,
                                    IsActive = true
                                }
                            };
                            
                            return new ApiResponse<ApiAuthenticationResponse>
                            {
                                Success = true,
                                Message = "Authentication successful",
                                Data = authData,
                                Timestamp = DateTime.UtcNow
                            };
                        }
                        else if (actualResponse != null && !actualResponse.Success)
                        {
                            System.Diagnostics.Debug.WriteLine($"API returned success=false");
                            return new ApiResponse<ApiAuthenticationResponse>
                            {
                                Success = false,
                                Message = actualResponse.Error ?? "Authentication failed",
                                Error = actualResponse.Error,
                                ErrorCode = actualResponse.UnAuthorizedRequest ? "UNAUTHORIZED" : "AUTH_FAILED",
                                Timestamp = DateTime.UtcNow
                            };
                        }
                    }
                    catch (JsonException jsonEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"Actual response JSON deserialization error: {jsonEx.Message}");
                    }
                    
                    // Fallback: Try to deserialize as our expected format
                    try
                    {
                        System.Diagnostics.Debug.WriteLine($"Trying fallback deserialization as ApiResponse<ApiAuthenticationResponse>");
                        var apiResponse = JsonSerializer.Deserialize<ApiResponse<ApiAuthenticationResponse>>(responseContent, _jsonOptions);
                        System.Diagnostics.Debug.WriteLine($"Fallback deserialization result: {apiResponse != null}");
                        if (apiResponse != null)
                        {
                            System.Diagnostics.Debug.WriteLine($"=== PARSED FALLBACK API RESPONSE ===");
                            System.Diagnostics.Debug.WriteLine($"Success: {apiResponse.Success}");
                            System.Diagnostics.Debug.WriteLine($"Message: {apiResponse.Message}");
                            System.Diagnostics.Debug.WriteLine($"Error: {apiResponse.Error}");
                            System.Diagnostics.Debug.WriteLine($"Error Code: {apiResponse.ErrorCode}");
                            System.Diagnostics.Debug.WriteLine($"Data is null: {apiResponse.Data == null}");
                            if (apiResponse.Data != null)
                            {
                                System.Diagnostics.Debug.WriteLine($"Access Token: {apiResponse.Data.AccessToken?.Substring(0, Math.Min(20, apiResponse.Data.AccessToken.Length))}...");
                                System.Diagnostics.Debug.WriteLine($"Token Type: {apiResponse.Data.TokenType}");
                                System.Diagnostics.Debug.WriteLine($"Expires In: {apiResponse.Data.ExpiresIn} seconds");
                                System.Diagnostics.Debug.WriteLine($"User ID: {apiResponse.Data.User?.Id}");
                                System.Diagnostics.Debug.WriteLine($"Username: {apiResponse.Data.User?.Username}");
                                System.Diagnostics.Debug.WriteLine($"Email: {apiResponse.Data.User?.Email}");
                                System.Diagnostics.Debug.WriteLine($"Tenant ID: {apiResponse.Data.Tenant?.Id}");
                                System.Diagnostics.Debug.WriteLine($"Tenant Name: {apiResponse.Data.Tenant?.Name}");
                            }
                            System.Diagnostics.Debug.WriteLine($"================================");
                            return apiResponse;
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"Fallback deserialization returned null");
                        }
                    }
                    catch (JsonException jsonEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"Fallback JSON deserialization error: {jsonEx.Message}");
                    }

                    return new ApiResponse<ApiAuthenticationResponse>
                    {
                        Success = false,
                        Message = "Invalid response format from server",
                        Error = "INVALID_RESPONSE_FORMAT",
                        ErrorCode = ApiConfiguration.ErrorCodes.ServerError,
                        Timestamp = DateTime.UtcNow
                    };
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"=== API ERROR RESPONSE ===");
                    System.Diagnostics.Debug.WriteLine($"Status Code: {response.StatusCode}");
                    System.Diagnostics.Debug.WriteLine($"Error Content: {responseContent}");
                    System.Diagnostics.Debug.WriteLine($"=========================");
                    return await HandleErrorResponse<ApiAuthenticationResponse>(response, responseContent);
                }
            }
            catch (HttpRequestException httpEx)
            {
                System.Diagnostics.Debug.WriteLine($"=== HTTP REQUEST ERROR ===");
                System.Diagnostics.Debug.WriteLine($"Error: {httpEx.Message}");
                System.Diagnostics.Debug.WriteLine($"Inner Exception: {httpEx.InnerException?.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack Trace: {httpEx.StackTrace}");
                System.Diagnostics.Debug.WriteLine($"=========================");
                return new ApiResponse<ApiAuthenticationResponse>
                {
                    Success = false,
                    Message = "Network error occurred. Please check your internet connection.",
                    Error = httpEx.Message,
                    ErrorCode = ApiConfiguration.ErrorCodes.NetworkError,
                    Timestamp = DateTime.UtcNow
                };
            }
            catch (TaskCanceledException tcEx) when (tcEx.InnerException is TimeoutException)
            {
                System.Diagnostics.Debug.WriteLine($"Timeout Error: {tcEx.Message}");
                return new ApiResponse<ApiAuthenticationResponse>
                {
                    Success = false,
                    Message = "Request timed out. Please try again.",
                    Error = tcEx.Message,
                    ErrorCode = ApiConfiguration.ErrorCodes.TimeoutError,
                    Timestamp = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"=== UNEXPECTED ERROR ===");
                System.Diagnostics.Debug.WriteLine($"Error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Inner Exception: {ex.InnerException?.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack Trace: {ex.StackTrace}");
                System.Diagnostics.Debug.WriteLine($"=======================");
                return new ApiResponse<ApiAuthenticationResponse>
                {
                    Success = false,
                    Message = "An unexpected error occurred. Please try again.",
                    Error = ex.Message,
                    ErrorCode = ApiConfiguration.ErrorCodes.UnknownError,
                    Timestamp = DateTime.UtcNow
                };
            }
        }

        /// <summary>
        /// Refreshes authentication token
        /// </summary>
        public async Task<ApiResponse<ApiAuthenticationResponse>> RefreshTokenAsync(string refreshToken)
        {
            try
            {
                var request = new { RefreshToken = refreshToken };
                var json = JsonSerializer.Serialize(request, _jsonOptions);
                var content = new StringContent(json, Encoding.UTF8, ApiConfiguration.ContentTypes.Json);

                var response = await _httpClient.PostAsync(ApiConfiguration.Endpoints.RefreshToken, content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var apiResponse = JsonSerializer.Deserialize<ApiResponse<ApiAuthenticationResponse>>(responseContent, _jsonOptions);
                    return apiResponse ?? new ApiResponse<ApiAuthenticationResponse>
                    {
                        Success = false,
                        Message = "Invalid response format",
                        ErrorCode = ApiConfiguration.ErrorCodes.ServerError
                    };
                }
                else
                {
                    return await HandleErrorResponse<ApiAuthenticationResponse>(response, responseContent);
                }
            }
            catch (Exception ex)
            {
                return new ApiResponse<ApiAuthenticationResponse>
                {
                    Success = false,
                    Message = "Token refresh failed",
                    Error = ex.Message,
                    ErrorCode = ApiConfiguration.ErrorCodes.UnknownError
                };
            }
        }

        /// <summary>
        /// Logs out user from the API
        /// </summary>
        public async Task<ApiResponse<object>> LogoutAsync(string token)
        {
            try
            {
                var request = new { Token = token };
                var json = JsonSerializer.Serialize(request, _jsonOptions);
                var content = new StringContent(json, Encoding.UTF8, ApiConfiguration.ContentTypes.Json);

                var response = await _httpClient.PostAsync(ApiConfiguration.Endpoints.Logout, content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    return new ApiResponse<object>
                    {
                        Success = true,
                        Message = "Logout successful"
                    };
                }
                else
                {
                    return await HandleErrorResponse<object>(response, responseContent);
                }
            }
            catch (Exception ex)
            {
                return new ApiResponse<object>
                {
                    Success = false,
                    Message = "Logout failed",
                    Error = ex.Message,
                    ErrorCode = ApiConfiguration.ErrorCodes.UnknownError
                };
            }
        }

        /// <summary>
        /// Sets the authorization header for subsequent requests
        /// </summary>
        public void SetAuthorizationHeader(string token)
        {
            _httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }

        /// <summary>
        /// Clears the authorization header
        /// </summary>
        public void ClearAuthorizationHeader()
        {
            _httpClient.DefaultRequestHeaders.Authorization = null;
        }

        /// <summary>
        /// Handles error responses from the API
        /// </summary>
        private async Task<ApiResponse<T>> HandleErrorResponse<T>(HttpResponseMessage response, string responseContent)
        {
            var errorCode = response.StatusCode switch
            {
                HttpStatusCode.Unauthorized => ApiConfiguration.ErrorCodes.Unauthorized,
                HttpStatusCode.Forbidden => ApiConfiguration.ErrorCodes.Forbidden,
                HttpStatusCode.NotFound => ApiConfiguration.ErrorCodes.NotFound,
                HttpStatusCode.BadRequest => ApiConfiguration.ErrorCodes.ValidationError,
                HttpStatusCode.InternalServerError => ApiConfiguration.ErrorCodes.ServerError,
                _ => ApiConfiguration.ErrorCodes.UnknownError
            };

            var message = response.StatusCode switch
            {
                HttpStatusCode.Unauthorized => "Invalid credentials. Please check your username and password.",
                HttpStatusCode.Forbidden => "Access denied. You don't have permission to access this resource.",
                HttpStatusCode.NotFound => "The requested resource was not found.",
                HttpStatusCode.BadRequest => "Invalid request. Please check your input.",
                HttpStatusCode.InternalServerError => "Server error occurred. Please try again later.",
                _ => "An error occurred while processing your request."
            };

            // Try to parse error details from response
            try
            {
                var errorResponse = JsonSerializer.Deserialize<ApiResponse<object>>(responseContent, _jsonOptions);
                if (errorResponse != null && !string.IsNullOrEmpty(errorResponse.Message))
                {
                    message = errorResponse.Message;
                }
            }
            catch
            {
                // Use default message if parsing fails
            }

            return new ApiResponse<T>
            {
                Success = false,
                Message = message,
                Error = responseContent,
                ErrorCode = errorCode,
                Timestamp = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Disposes the HTTP client
        /// </summary>
        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}
