using FWO.Config.File;

namespace FWO.Ui.Services
{
    /// <summary>
    /// Reads the public certificate of FWO's internal certificate authority.
    /// </summary>
    public interface IInternalCaCertificateProvider
    {
        /// <summary>
        /// Reads the internal CA certificate in PEM format.
        /// </summary>
        /// <returns>The PEM encoded public CA certificate.</returns>
        Task<string> GetAsync();
    }

    /// <summary>
    /// Reads the internal CA certificate from the path supplied by the installer.
    /// </summary>
    public class InternalCaCertificateProvider : IInternalCaCertificateProvider
    {
        /// <inheritdoc />
        public async Task<string> GetAsync()
        {
            return await File.ReadAllTextAsync(ConfigFile.InternalCaCertificate);
        }
    }
}
