using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using System.Diagnostics.CodeAnalysis;

namespace FWO.Ui.Services
{
    /// <summary>
    /// Wrapper for ProtectedSessionStorage to implement ISessionStorage.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class SessionStorage(ProtectedSessionStorage protectedSessionStorage) : ISessionStorage
    {
        public async Task<ProtectedBrowserStorageResult<TValue>> GetAsync<TValue>(string key)
        {
            return await protectedSessionStorage.GetAsync<TValue>(key);
        }

        public async Task SetAsync(string key, object value)
        {
            await protectedSessionStorage.SetAsync(key, value);
        }

        public async Task DeleteAsync(string key)
        {
            await protectedSessionStorage.DeleteAsync(key);
        }
    }
}
