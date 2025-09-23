using MauiApp.Core.Interfaces;
using MauiApp.Core.Models;
using MauiApp.Core.Services;
using MauiApp.Views;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace MauiApp.ViewModels
{
    /// <summary>
    /// ViewModel for the Login Screen
    /// </summary>
    public class LoginViewModel : BaseViewModel
    {
        private readonly IAuthenticationService _authenticationService;
        private readonly INavigationService _navigationService;
        private readonly ITenantService _tenantService;
        private readonly IBottomSheetService _bottomSheetService;
        private readonly ICredentialStorageService _credentialStorage;

        private string _usernameOrEmail = string.Empty;
        private string _password = string.Empty;
        private Tenant? _selectedTenant;
        private bool _isBiometricAvailable;
        private bool _showBiometricSection;
        private bool _isPasswordVisible;
        private bool _isTenantSelectorOpen;
        private string _greetingMessage = "Hi";
        private string _biometricButtonText = "Use Biometric Login";

        public LoginViewModel(
            IAuthenticationService authenticationService,
            INavigationService navigationService,
            ITenantService tenantService,
            IBottomSheetService bottomSheetService,
            ICredentialStorageService credentialStorage)
        {
            _authenticationService = authenticationService;
            _navigationService = navigationService;
            _tenantService = tenantService;
            _bottomSheetService = bottomSheetService;
            _credentialStorage = credentialStorage;

            // Initialize commands
            LoginCommand = new Command(async () => await LoginAsync());
            BiometricLoginCommand = new Command(async () => await BiometricLoginAsync());
            ForgotPasswordCommand = new Command(async () => await ForgotPasswordAsync());
            LoadTenantsCommand = new Command(async () => await LoadTenantsAsync());
            TogglePasswordVisibilityCommand = new Command(() => IsPasswordVisible = !IsPasswordVisible);
            OpenTenantSelectorCommand = new Command(async () => await OpenTenantSelectorAsync());

            // Initialize tenants collection
            Tenants = new ObservableCollection<Tenant>();
        }

        #region Properties

        public string UsernameOrEmail
        {
            get => _usernameOrEmail;
            set => SetProperty(ref _usernameOrEmail, value);
        }

        public string Password
        {
            get => _password;
            set => SetProperty(ref _password, value);
        }

        public Tenant? SelectedTenant
        {
            get => _selectedTenant;
            set => SetProperty(ref _selectedTenant, value);
        }

        public bool IsBiometricAvailable
        {
            get => _isBiometricAvailable;
            set => SetProperty(ref _isBiometricAvailable, value);
        }

        public bool ShowBiometricSection
        {
            get => _showBiometricSection;
            set => SetProperty(ref _showBiometricSection, value);
        }

        public string GreetingMessage
        {
            get => _greetingMessage;
            set => SetProperty(ref _greetingMessage, value);
        }

        public bool IsPasswordVisible
        {
            get => _isPasswordVisible;
            set => SetProperty(ref _isPasswordVisible, value);
        }

        public bool IsTenantSelectorOpen
        {
            get => _isTenantSelectorOpen;
            set => SetProperty(ref _isTenantSelectorOpen, value);
        }

        public ObservableCollection<Tenant> Tenants { get; }

        public string BiometricButtonText
        {
            get => _biometricButtonText;
            set => SetProperty(ref _biometricButtonText, value);
        }

        #endregion

        #region Commands

        public ICommand LoginCommand { get; }
        public ICommand BiometricLoginCommand { get; }
        public ICommand ForgotPasswordCommand { get; }
        public ICommand LoadTenantsCommand { get; }
        public ICommand TogglePasswordVisibilityCommand { get; }
        public ICommand OpenTenantSelectorCommand { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Initializes the login screen
        /// </summary>
        public async Task InitializeAsync()
        {
            try
            {
                // Load tenants
                await LoadTenantsAsync();

                // Check if biometric is available on device
                var biometricAvailable = await _authenticationService.IsBiometricAvailableAsync();
                System.Diagnostics.Debug.WriteLine($"Biometric available: {biometricAvailable}");

                // Check if user has previously logged in and has saved credentials
                var hasValidCredentials = await _authenticationService.HasValidCredentialsAsync();
                System.Diagnostics.Debug.WriteLine($"Has valid credentials: {hasValidCredentials}");
                
                // Only show biometric section if:
                // 1. Biometric is available on device
                // 2. User has previously logged in and has saved credentials
                IsBiometricAvailable = biometricAvailable && hasValidCredentials;
                ShowBiometricSection = IsBiometricAvailable;

                System.Diagnostics.Debug.WriteLine($"ShowBiometricSection: {ShowBiometricSection}");

                // Set biometric button text based on availability
                if (IsBiometricAvailable)
                {
                    BiometricButtonText = "🔐 Use Biometric Login";
                }

                // Get saved username from credentials for greeting message
                if (hasValidCredentials)
                {
                    var credentials = await _credentialStorage.GetCredentialsAsync();
                    if (credentials.HasValue)
                    {
                        GreetingMessage = $"Hi {credentials.Value.Username}!";
                        UsernameOrEmail = credentials.Value.Username;
                    }
                    else
                    {
                        GreetingMessage = "Welcome!";
                    }
                }
                else
                {
                    // Set default greeting for fresh installs
                    GreetingMessage = "Welcome!";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Login initialization error: {ex.Message}");
            }
        }

        private async Task LoadTenantsAsync()
        {
            try
            {
                var tenants = await _tenantService.GetTenantsAsync();
                
                Tenants.Clear();
                foreach (var tenant in tenants)
                {
                    Tenants.Add(tenant);
                }

                // Don't auto-select tenant - let user choose
                // SelectedTenant will remain null until user selects one
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Load tenants error: {ex.Message}");
            }
        }

        private async Task OpenTenantSelectorAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("OpenTenantSelectorAsync called");
                IsTenantSelectorOpen = true;
                
                if (!Tenants.Any())
                {
                    System.Diagnostics.Debug.WriteLine("Loading tenants...");
                    await LoadTenantsAsync();
                }

                System.Diagnostics.Debug.WriteLine($"Showing bottom sheet with {Tenants.Count} tenants");
                var picked = await _bottomSheetService.ShowTenantSelectionAsync(Tenants, SelectedTenant);
                System.Diagnostics.Debug.WriteLine($"Bottom sheet returned: {picked?.Name ?? "null"}");
                if (picked != null)
                    SelectedTenant = picked;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Tenant selector error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                await Application.Current.MainPage.DisplayAlert("Error", ex.Message, "OK");
            }
            finally
            {
                IsTenantSelectorOpen = false;
            }
        }

        private async Task LoginAsync()
        {
            try
            {
                if (IsBusy) return;

                IsBusy = true;

                // Validate input
                if (string.IsNullOrWhiteSpace(UsernameOrEmail))
                {
                    await CustomAlertPage.ShowAsync("Error", "Please enter your username or email", "OK");
                    return;
                }

                if (string.IsNullOrWhiteSpace(Password))
                {
                    await CustomAlertPage.ShowAsync("Error", "Please enter your password", "OK");
                    return;
                }

                if (SelectedTenant == null)
                {
                    await CustomAlertPage.ShowAsync("Error", "Please select a tenant", "OK");
                    return;
                }

                // Create login request
                var request = new LoginRequest
                {
                    UsernameOrEmail = UsernameOrEmail.Trim(),
                    Password = Password,
                    TenancyName = SelectedTenant.Name,
                    TenantId = SelectedTenant.Id,
                    UseBiometric = false
                };

                // Debug: Log the request details
                System.Diagnostics.Debug.WriteLine($"=== LOGIN REQUEST ===");
                System.Diagnostics.Debug.WriteLine($"UsernameOrEmail: '{request.UsernameOrEmail}'");
                System.Diagnostics.Debug.WriteLine($"Password: '{new string('*', request.Password.Length)}' (Length: {request.Password.Length})");
                System.Diagnostics.Debug.WriteLine($"TenancyName: '{request.TenancyName}'");
                System.Diagnostics.Debug.WriteLine($"TenantId: '{request.TenantId}'");
                System.Diagnostics.Debug.WriteLine($"SelectedTenant: {SelectedTenant?.Name ?? "NULL"}");
                System.Diagnostics.Debug.WriteLine($"=====================");

                // Attempt login
                var response = await _authenticationService.LoginAsync(request);

                // Debug: Log the response details
                System.Diagnostics.Debug.WriteLine($"=== LOGIN RESPONSE ===");
                System.Diagnostics.Debug.WriteLine($"Success: {response.IsSuccess}");
                System.Diagnostics.Debug.WriteLine($"Message: '{response.Message}'");
                System.Diagnostics.Debug.WriteLine($"User: {response.User?.Username ?? "NULL"}");
                System.Diagnostics.Debug.WriteLine($"Token: {response.Token?.Substring(0, Math.Min(20, response.Token?.Length ?? 0))}...");
                System.Diagnostics.Debug.WriteLine($"=====================");

                if (response.IsSuccess)
                {
                    // Navigate to main app
                    await _navigationService.NavigateToRootAsync("MainPage");
                }
                else
                {
                    await CustomAlertPage.ShowAsync("Login Failed", response.Message, "OK");
                }
            }
            catch (Exception ex)
            {
                await CustomAlertPage.ShowAsync("Error", $"An error occurred: {ex.Message}", "OK");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task BiometricLoginAsync()
        {
            try
            {
                if (IsBusy) return;

                IsBusy = true;

                var response = await _authenticationService.LoginWithBiometricAsync();

                if (response.IsSuccess)
                {
                    // Navigate to main app
                    await _navigationService.NavigateToRootAsync("MainPage");
                }
                else
                {
                    await CustomAlertPage.ShowAsync("Biometric Login Failed", response.Message, "OK");
                }
            }
            catch (Exception ex)
            {
                await CustomAlertPage.ShowAsync("Error", $"An error occurred: {ex.Message}", "OK");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task ForgotPasswordAsync()
        {
            try
            {
                await CustomAlertPage.ShowAsync("Forgot Password", "Password reset functionality will be implemented here.", "OK");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Forgot password error: {ex.Message}");
            }
        }


        #endregion
    }
}
