using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace FWO.Ui.Services
{
    /// <summary>
    /// Wrapper for ProtectedSessionStorage to implement ISessionStorage.
    /// </summary>
    public class SessionStorageWrapper : ISessionStorage
    {
        private readonly ProtectedSessionStorage protectedSessionStorage;

        public SessionStorageWrapper(ProtectedSessionStorage protectedSessionStorage)
        {
            this.protectedSessionStorage = protectedSessionStorage;
        }

        public ValueTask<ProtectedBrowserStorageResult<TValue>> GetAsync<TValue>(string key)
        {
            return protectedSessionStorage.GetAsync<TValue>(key);
        }

        public ValueTask SetAsync(string key, object value)
        {
            return protectedSessionStorage.SetAsync(key, value);
        }

        public ValueTask DeleteAsync(string key)
        {
            return protectedSessionStorage.DeleteAsync(key);
        }
    }
}
