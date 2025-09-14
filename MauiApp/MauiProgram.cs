using Microsoft.Extensions.Logging;
using MauiAppType = Microsoft.Maui.Hosting.MauiApp;
using MauiApp.Core.Interfaces;
using MauiApp.Core.Services;
using MauiApp.ViewModels;
using MauiApp.Views;
using Microsoft.Maui.LifecycleEvents;
#if ANDROID
using Android.OS;
using Android.Views;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Handlers;
#endif

namespace MauiApp
{
    public static class MauiProgram
    {
        public static MauiAppType CreateMauiApp()
        {
            var builder = MauiAppType.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureMauiHandlers(handlers =>
                {
#if ANDROID
                    // Remove Android underline/background for Entry and Picker
                    EntryHandler.Mapper.AppendToMapping("NoUnderline", (handler, view) =>
                    {
                        handler.PlatformView.Background = null;
                    });
                    PickerHandler.Mapper.AppendToMapping("NoUnderline", (handler, view) =>
                    {
                        handler.PlatformView.Background = null;
                    });
#endif
                })
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

            // Register Services
            builder.Services.AddSingleton<INavigationService, NavigationService>();
            builder.Services.AddSingleton<IAuthenticationService, AuthenticationService>();
            builder.Services.AddSingleton<ITenantService, TenantService>();
            builder.Services.AddSingleton<IBottomSheetService, BottomSheetService>();
            builder.Services.AddSingleton<ICredentialStorageService, CredentialStorageService>();
            builder.Services.AddSingleton<IAppLifecycleService, AppLifecycleService>();
            builder.Services.AddSingleton<IAppStateService, AppStateService>();

#if ANDROID
            builder.Services.AddSingleton<IBiometricService, MauiApp.Platforms.Android.Services.BiometricService>();
#else
            // Add mock biometric service for other platforms
            builder.Services.AddSingleton<IBiometricService, MockBiometricService>();
#endif

            // Register ViewModels
            builder.Services.AddTransient<SplashViewModel>();
            builder.Services.AddTransient<LoginViewModel>();

            // Register Views
            builder.Services.AddTransient<SplashPage>();
            builder.Services.AddTransient<LoginPage>();
            builder.Services.AddTransient<MainPage>();
            builder.Services.AddTransient<BottomSheetSelectionPage>();

#if DEBUG
    		builder.Logging.AddDebug();
#endif

            // Android status bar color to match app background globally
            builder.ConfigureLifecycleEvents(events =>
            {
#if ANDROID
                events.AddAndroid(android =>
                {
                    android.OnCreate((activity, bundle) =>
                    {
                        var color = Colors.Red; // fallback
                        try
                        {
                            // Use the brand PrimaryRed if available
                            var hex = "#ED1C24"; // PrimaryRed
                            color = Color.FromArgb(hex);
                        }
                        catch { }

                        var window = activity.Window;
                        if (window is not null)
                        {
                            window.SetStatusBarColor(color.ToPlatform());
                            if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
                            {
                                var decor = window.DecorView;
                                if (decor is not null)
                                {
                                    var flags = (StatusBarVisibility)decor.SystemUiVisibility;
                                    // Ensure light content (white icons) on the red background
                                    flags &= ~(StatusBarVisibility)SystemUiFlags.LightStatusBar;
                                    decor.SystemUiVisibility = (StatusBarVisibility)flags;
                                }
                            }
                        }
                    });
                });
#endif
            });

            var app = builder.Build();

            // Expose ServiceProvider globally for simple service resolution in XAML-created pages
            ServiceHelper.Initialize(app.Services);

            return app;
        }
    }
}
