using Android.App;
using Android.Runtime;
using MauiAppType = Microsoft.Maui.Hosting.MauiApp;

namespace MauiApp
{
    [Application]
    public class MainApplication : MauiApplication
    {
        public MainApplication(IntPtr handle, JniHandleOwnership ownership)
            : base(handle, ownership)
        {
        }

        protected override MauiAppType CreateMauiApp() => MauiProgram.CreateMauiApp();
    }
}
