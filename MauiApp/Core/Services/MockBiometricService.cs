using MauiApp.Core.Interfaces;
using System.Threading.Tasks;

namespace MauiApp.Core.Services
{
    public class MockBiometricService : IBiometricService
    {
        public async Task<bool> IsBiometricAvailableAsync()
        {
            // Mock implementation - always returns false for non-Android platforms
            return await Task.FromResult(false);
        }

        public async Task<bool> AuthenticateAsync(string reason = "Authenticate to access your account")
        {
            // Mock implementation - always returns false for non-Android platforms
            return await Task.FromResult(false);
        }

        public async Task<string> GetBiometricTypeAsync()
        {
            // Mock implementation - returns "Not Available" for non-Android platforms
            return await Task.FromResult("Not Available");
        }
    }
}
