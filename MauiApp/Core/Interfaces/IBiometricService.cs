using System.Threading.Tasks;

namespace MauiApp.Core.Interfaces
{
    public interface IBiometricService
    {
        /// <summary>
        /// Checks if biometric authentication is available on the device
        /// </summary>
        Task<bool> IsBiometricAvailableAsync();

        /// <summary>
        /// Authenticates user using biometric (fingerprint/face ID)
        /// </summary>
        Task<bool> AuthenticateAsync(string reason = "Authenticate to access your account");

        /// <summary>
        /// Gets the biometric type available on the device
        /// </summary>
        Task<string> GetBiometricTypeAsync();
    }
}
