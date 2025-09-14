using System;

namespace MauiApp.Core.Interfaces
{
    public interface IAppLifecycleService
    {
        /// <summary>
        /// Event fired when the app is being terminated
        /// </summary>
        event EventHandler AppTerminating;

        /// <summary>
        /// Event fired when the app is being paused
        /// </summary>
        event EventHandler AppPausing;

        /// <summary>
        /// Event fired when the app is being resumed
        /// </summary>
        event EventHandler AppResuming;

        /// <summary>
        /// Initializes the app lifecycle monitoring
        /// </summary>
        void Initialize();

        /// <summary>
        /// Triggers the app terminating event
        /// </summary>
        void OnAppTerminating();
    }
}
