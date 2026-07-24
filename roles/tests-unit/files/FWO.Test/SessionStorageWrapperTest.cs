using Bunit;
using FWO.Ui.Services;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.AspNetCore.DataProtection;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    public class SessionStorageWrapperTest
    {
        [Test]
        public async Task WrapperDelegatesToUnderlyingProtectedSessionStorage()
        {
            using BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            ProtectedSessionStorage protectedSessionStorage = new(context.JSInterop.JSRuntime, new EphemeralDataProtectionProvider());
            SessionStorageWrapper wrapper = new(protectedSessionStorage);

            await wrapper.SetAsync("key", "value");
            ProtectedBrowserStorageResult<string> result = await wrapper.GetAsync<string>("key");
            await wrapper.DeleteAsync("key");

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.Not.Null);
                Assert.That(result.Success, Is.False);
            });
        }
    }
}
