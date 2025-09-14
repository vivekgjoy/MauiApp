using Foundation;
using MauiAppType = Microsoft.Maui.Hosting.MauiApp;

namespace MauiApp
{
    [Register("AppDelegate")]
    public class AppDelegate : MauiUIApplicationDelegate
    {
        protected override MauiAppType CreateMauiApp() => MauiProgram.CreateMauiApp();
    }
}
