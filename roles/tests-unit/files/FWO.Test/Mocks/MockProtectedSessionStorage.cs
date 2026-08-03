using FWO.Ui.Services;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using System.Reflection;

namespace FWO.Test.Mocks
{
    /// <summary>
    /// Mock implementation of ISessionStorage for testing purposes
    /// </summary>
    public class MockProtectedSessionStorage : ISessionStorage
    {
        private readonly Dictionary<string, object?> storage = new();

        public MockProtectedSessionStorage()
        {
        }

        public Task<ProtectedBrowserStorageResult<TValue>> GetAsync<TValue>(string key)
        {
            if (storage.TryGetValue(key, out var value) && value is TValue typedValue)
            {
                var result = CreateSuccessResult(typedValue);
                return Task.FromResult(result);
            }

            var emptyResult = CreateFailureResult<TValue>();
            return Task.FromResult(emptyResult);
        }

        public Task SetAsync(string key, object value)
        {
            storage[key] = value;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string key)
        {
            storage.Remove(key);
            return Task.CompletedTask;
        }

        public void Clear()
        {
            storage.Clear();
        }

        public bool ContainsKey(string key)
        {
            return storage.ContainsKey(key);
        }

        public int Count => storage.Count;

        private static ProtectedBrowserStorageResult<TValue> CreateSuccessResult<TValue>(TValue value)
        {
            // Use the internal constructor via reflection
            var constructor = typeof(ProtectedBrowserStorageResult<TValue>).GetConstructors(
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)[0];

            return (ProtectedBrowserStorageResult<TValue>)constructor.Invoke([true, value]);
        }

        private static ProtectedBrowserStorageResult<TValue> CreateFailureResult<TValue>()
        {
            var constructor = typeof(ProtectedBrowserStorageResult<TValue>).GetConstructors(
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)[0];

            return (ProtectedBrowserStorageResult<TValue>)constructor.Invoke([false, default(TValue)]);
        }
    }
}
