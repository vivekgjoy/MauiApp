namespace MauiApp
{
    public static class ServiceHelper
    {
        private static IServiceProvider? _serviceProvider;

        public static void Initialize(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public static T GetService<T>() where T : notnull
        {
            if (_serviceProvider is null)
            {
                throw new InvalidOperationException("ServiceHelper is not initialized. Call ServiceHelper.Initialize in CreateMauiApp().");
            }

            var service = _serviceProvider.GetService(typeof(T));
            if (service is T typed)
            {
                return typed;
            }

            throw new InvalidOperationException($"Service of type {typeof(T).FullName} is not registered.");
        }
    }
}


