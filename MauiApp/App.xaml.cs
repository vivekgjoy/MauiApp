using MauiApp.Core.Interfaces;

namespace MauiApp
{
    public partial class App : Application
    {
        private IAppLifecycleService? _appLifecycleService;
        private IAuthenticationService? _authenticationService;

        public App()
        {
            InitializeComponent();

            // Global exception handlers to capture startup crashes
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                try
                {
                    var ex = e.ExceptionObject as Exception;
                    System.Diagnostics.Debug.WriteLine($"[UnhandledException] {ex?.GetType().FullName}: {ex?.Message}\n{ex?.StackTrace}");
                }
                catch { }
            };

            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"[UnobservedTaskException] {e.Exception.GetType().FullName}: {e.Exception.Message}\n{e.Exception.StackTrace}");
                }
                catch { }
            };
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            var window = new Window(new AppShell());
            
            // Set initial route to splash screen
            window.Created += async (s, e) =>
            {
                try
                {
                    // Initialize services
                    _appLifecycleService = ServiceHelper.GetService<IAppLifecycleService>();
                    _authenticationService = ServiceHelper.GetService<IAuthenticationService>();
                    
                    // Initialize app lifecycle monitoring
                    _appLifecycleService?.Initialize();
                    
                    // Subscribe to app terminating event for automatic logout
                    if (_appLifecycleService != null && _authenticationService != null)
                    {
                        _appLifecycleService.AppTerminating += async (sender, args) =>
                        {
                            try
                            {
                                // Clear credentials when app is terminated
                                await _authenticationService.LogoutAsync();
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Error during app termination logout: {ex.Message}");
                            }
                        };
                    }
                    
                    // Check if user should be logged out (app was killed)
                    await CheckAndHandleAppRestart();
                    
                    await Shell.Current.GoToAsync("//SplashPage");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Navigation at startup failed] {ex.Message}");
                }
            };
            
            return window;
        }

        private async Task CheckAndHandleAppRestart()
        {
            try
            {
                if (_authenticationService != null)
                {
                    // Check if app was killed (not properly closed)
                    var wasProperlyClosed = await ServiceHelper.GetService<IAppStateService>().WasAppProperlyClosedAsync();
                    if (!wasProperlyClosed)
                    {
                        // App was killed - clear session but keep credentials for biometric login
                        await _authenticationService.ClearSessionAsync();
                        System.Diagnostics.Debug.WriteLine("App was killed - cleared session but kept credentials for biometric");
                    }
                    else
                    {
                        // App was properly closed - mark as stopped for next time
                        await ServiceHelper.GetService<IAppStateService>().SetAppStoppedAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error checking app restart: {ex.Message}");
            }
        }
    }
}