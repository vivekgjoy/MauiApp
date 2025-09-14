using MauiApp.Core.Interfaces;
using System;

namespace MauiApp.Core.Services
{
    public class AppLifecycleService : IAppLifecycleService
    {
        public event EventHandler AppTerminating;
        public event EventHandler AppPausing;
        public event EventHandler AppResuming;

        public void Initialize()
        {
            // Subscribe to app lifecycle events
            Microsoft.Maui.Controls.Application.Current.PageAppearing += OnPageAppearing;
            Microsoft.Maui.Controls.Application.Current.PageDisappearing += OnPageDisappearing;
        }

        private void OnPageAppearing(object sender, Microsoft.Maui.Controls.Page page)
        {
            AppResuming?.Invoke(this, EventArgs.Empty);
        }

        private void OnPageDisappearing(object sender, Microsoft.Maui.Controls.Page page)
        {
            AppPausing?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Triggers the app terminating event
        /// </summary>
        public void OnAppTerminating()
        {
            AppTerminating?.Invoke(this, EventArgs.Empty);
        }
    }
}
