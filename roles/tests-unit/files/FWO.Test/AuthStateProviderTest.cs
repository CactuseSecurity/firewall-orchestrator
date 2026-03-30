using FWO.Config.Api;
using FWO.Services.EventMediator;
using FWO.Services.EventMediator.Events;
using FWO.Test.Mocks;
using FWO.Ui.Auth;
using FWO.Ui.Services;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    public class AuthStateProviderTest
    {
        [Test]
        public async Task RefreshAuthenticationState_WhenRefreshFails_ShouldPublishJwtExpiredEvent()
        {
            MockMiddlewareClient mockMiddlewareClient = new();
            MockProtectedSessionStorage mockSessionStorage = new();
            EventMediator eventMediator = new();
            TokenService tokenService = new(mockMiddlewareClient, mockSessionStorage);
            AuthStateProvider authStateProvider = new(tokenService, eventMediator);
            UserConfig userConfig = new();
            userConfig.User.Dn = "cn=test-user,dc=example,dc=com";

            string? publishedUserDn = null;
            int publishCount = 0;

            eventMediator.Subscribe<JwtExpiredEvent>(nameof(JwtExpiredEvent), _ =>
            {
                publishCount++;
                publishedUserDn = _.EventArgs?.UserDn;
            });

            bool refreshed = await authStateProvider.RefreshAuthenticationState(new MockApiConnection(), mockMiddlewareClient, userConfig, new CircuitHandlerService(eventMediator));

            Assert.That(refreshed, Is.False);
            Assert.That(publishCount, Is.EqualTo(1));
            Assert.That(publishedUserDn, Is.EqualTo(userConfig.User.Dn));
        }
    }
}
