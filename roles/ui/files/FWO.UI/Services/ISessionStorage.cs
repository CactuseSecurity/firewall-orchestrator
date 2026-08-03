using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace FWO.Ui.Services
{
    /// <summary>
    /// Interface for session storage operations.
    /// </summary>
    public interface ISessionStorage
    {
        /// <summary>
        /// Gets a value from session storage.
        /// </summary>
        Task<ProtectedBrowserStorageResult<TValue>> GetAsync<TValue>(string key);

        /// <summary>
        /// Sets a value in session storage.
        /// </summary>
        Task SetAsync(string key, object value);

        /// <summary>
        /// Deletes a value from session storage.
        /// </summary>
        Task DeleteAsync(string key);
    }
}
