using FWO.Basics;
using FWO.Test.Mocks;
using FWO.Ui.Services;
using Microsoft.AspNetCore.Session;
using NUnit.Framework;
using System.Reflection;

namespace FWO.Test
{
    [TestFixture]
    internal class ExecutionModeStorageTest
    {
        [Test]
        public async Task GetExecutionModeReturnsStoredValue()
        {
            MockProtectedSessionStorage sessionStorage = new();
            ExecutionModeStorage storage = new(sessionStorage);

            await storage.SetExecutionMode(Roles.Admin);

            string? result = await storage.GetExecutionMode();

            Assert.That(result, Is.EqualTo(Roles.Admin));
        }

        [Test]
        public async Task GetExecutionModeReturnsNullForWhitespaceAndMissingEntries()
        {
            MockProtectedSessionStorage sessionStorage = new();
            ExecutionModeStorage storage = new(sessionStorage);

            await sessionStorage.SetAsync("execution_mode", "   ");
            string? whitespaceResult = await storage.GetExecutionMode();
            await sessionStorage.DeleteAsync("execution_mode");
            string? missingResult = await storage.GetExecutionMode();

            Assert.Multiple(() =>
            {
                Assert.That(whitespaceResult, Is.Null);
                Assert.That(missingResult, Is.Null);
            });
        }

        [Test]
        public async Task SetExecutionModeStoresUserRoleSelectionWhenInputIsEmpty()
        {
            MockProtectedSessionStorage sessionStorage = new();
            ExecutionModeStorage storage = new(sessionStorage);

            await storage.SetExecutionMode("");

            string? result = await storage.GetExecutionMode();

            Assert.That(result, Is.EqualTo(GlobalConst.kUserRolesSelection));
        }

        [Test]
        public async Task GetExecutionModeClearsStorageAndReturnsNullWhenSessionStorageThrows()
        {
            ThrowingSessionStorage sessionStorage = new(getException: new InvalidOperationException("get failed"));
            ExecutionModeStorage storage = new(sessionStorage);

            string? result = await storage.GetExecutionMode();

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.Null);
                Assert.That(sessionStorage.DeleteCallCount, Is.EqualTo(1));
            });
        }

        [Test]
        public async Task ClearExecutionModeSwallowsDeleteFailures()
        {
            ThrowingSessionStorage sessionStorage = new(deleteException: new InvalidOperationException("delete failed"));
            ExecutionModeStorage storage = new(sessionStorage);

            Assert.DoesNotThrowAsync(async () => await storage.ClearExecutionMode());
            Assert.That(sessionStorage.DeleteCallCount, Is.EqualTo(1));
        }

        private sealed class ThrowingSessionStorage : ISessionStorage
        {
            private readonly Exception? getException;
            private readonly Exception? deleteException;

            public int DeleteCallCount { get; private set; }

            public ThrowingSessionStorage(Exception? getException = null, Exception? deleteException = null)
            {
                this.getException = getException;
                this.deleteException = deleteException;
            }

            public ValueTask<Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage.ProtectedBrowserStorageResult<TValue>> GetAsync<TValue>(string key)
            {
                if (getException != null)
                {
                    throw getException;
                }

                return ValueTask.FromResult(CreateFailureResult<TValue>());
            }

            public ValueTask SetAsync(string key, object value)
            {
                return ValueTask.CompletedTask;
            }

            public ValueTask DeleteAsync(string key)
            {
                DeleteCallCount++;
                if (deleteException != null)
                {
                    throw deleteException;
                }

                return ValueTask.CompletedTask;
            }

            private static Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage.ProtectedBrowserStorageResult<TValue> CreateFailureResult<TValue>()
            {
                var constructor = typeof(Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage.ProtectedBrowserStorageResult<TValue>).GetConstructors(
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)[0];

                return (Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage.ProtectedBrowserStorageResult<TValue>)constructor.Invoke([false, default(TValue)]);
            }
        }
    }
}
