using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace FWO.Ui.Services
{
    /// <summary>
    /// Interface for session storage operations to enable testing.
    /// </summary>
    public interface ISessionStorage
    {
        /// <summary>
        /// Gets a value from session storage.
        /// </summary>
        ValueTask<ProtectedBrowserStorageResult<TValue>> GetAsync<TValue>(string key);

        /// <summary>
        /// Sets a value in session storage.
        /// </summary>
        ValueTask SetAsync(string key, object value);

        /// <summary>
        /// Deletes a value from session storage.
        /// </summary>
        ValueTask DeleteAsync(string key);
    }
}
