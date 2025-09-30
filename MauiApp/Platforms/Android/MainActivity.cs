using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using AndroidX.Core.View;
using AndroidX.Core.Content;
using MauiApp.Core.Interfaces;
using AndroidX.Core.App;

namespace MauiApp
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        private const int CameraPermissionRequestCode = 1001;
        private const int StoragePermissionRequestCode = 1002;
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            
            // Set status bar and navigation bar colors
            if (Build.VERSION.SdkInt >= BuildVersionCodes.Lollipop)
            {
                // Set status bar to red color
                Window.SetStatusBarColor(Android.Graphics.Color.ParseColor("#ED1C24"));
                
                // Set navigation bar to transparent
                Window.SetNavigationBarColor(Android.Graphics.Color.Transparent);
                Window.SetFlags(WindowManagerFlags.LayoutNoLimits, WindowManagerFlags.LayoutNoLimits);
            }
            
            // Set additional window flags for better status bar control
            if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
            {
                // Use the new SystemUiFlags API for better control
                var flags = SystemUiFlags.LayoutStable | SystemUiFlags.LayoutFullscreen;
                Window.DecorView.SystemUiFlags = flags;
            }
        }

        protected override void OnResume()
        {
            base.OnResume();
            
            // Switch to main theme after splash
            SetTheme(Microsoft.Maui.Controls.Resource.Style.MainTheme);
            
            // Ensure status bar color is set after theme change
            SetStatusBarColor();
        }

        protected override void OnStart()
        {
            base.OnStart();
            SetStatusBarColor();
        }

        protected override void OnDestroy()
        {
            try
            {
                // Trigger app termination event for automatic logout
                try
                {
                    var appLifecycleService = ServiceHelper.GetService<IAppLifecycleService>();
                    appLifecycleService?.OnAppTerminating();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error getting app lifecycle service: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in OnDestroy: {ex.Message}");
            }
            finally
            {
                base.OnDestroy();
            }
        }

        private void SetStatusBarColor()
        {
            if (Build.VERSION.SdkInt >= BuildVersionCodes.Lollipop)
            {
                Window.SetStatusBarColor(Android.Graphics.Color.ParseColor("#ED1C24"));
                
                // Set it multiple times with delays to ensure it sticks
                new Handler(Looper.MainLooper).PostDelayed(() =>
                {
                    Window.SetStatusBarColor(Android.Graphics.Color.ParseColor("#ED1C24"));
                }, 50);
                
                new Handler(Looper.MainLooper).PostDelayed(() =>
                {
                    Window.SetStatusBarColor(Android.Graphics.Color.ParseColor("#ED1C24"));
                }, 200);
            }
        }

        public bool CheckCameraPermission()
        {
            if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
            {
                return ContextCompat.CheckSelfPermission(this, Android.Manifest.Permission.Camera) == Permission.Granted;
            }
            return true;
        }

        public bool CheckStoragePermission()
        {
            if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
            {
                return ContextCompat.CheckSelfPermission(this, Android.Manifest.Permission.ReadExternalStorage) == Permission.Granted;
            }
            return true;
        }

        public void RequestCameraPermission()
        {
            if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
            {
                if (!CheckCameraPermission())
                {
                    ActivityCompat.RequestPermissions(this, new string[] { Android.Manifest.Permission.Camera }, CameraPermissionRequestCode);
                }
            }
        }

        public void RequestStoragePermission()
        {
            if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
            {
                if (!CheckStoragePermission())
                {
                    ActivityCompat.RequestPermissions(this, new string[] { Android.Manifest.Permission.ReadExternalStorage }, StoragePermissionRequestCode);
                }
            }
        }

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
        {
            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
            
            if (requestCode == CameraPermissionRequestCode)
            {
                if (grantResults.Length > 0 && grantResults[0] == Permission.Granted)
                {
                    System.Diagnostics.Debug.WriteLine("Camera permission granted");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Camera permission denied");
                }
            }
            else if (requestCode == StoragePermissionRequestCode)
            {
                if (grantResults.Length > 0 && grantResults[0] == Permission.Granted)
                {
                    System.Diagnostics.Debug.WriteLine("Storage permission granted");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Storage permission denied");
                }
            }
        }
    }
}
