using FWO.Api.Client;
using FWO.Middleware.Server;
using FWO.Middleware.Server.Services;
using NSubstitute;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    public class TokenLifetimeProviderTest
    {
        [Test]
        public void GetAnonymousTokenLifetime_ReturnsShortBootstrapLifetime()
        {
            TokenLifetimeProvider provider = new();

            Assert.That(provider.GetAnonymousTokenLifetime(), Is.EqualTo(TimeSpan.FromMinutes(15)));
        }

        [Test]
        public void GetInternalServiceTokenLifetime_ReturnsShortServiceLifetime()
        {
            TokenLifetimeProvider provider = new();

            Assert.That(provider.GetInternalServiceTokenLifetime(), Is.EqualTo(TimeSpan.FromMinutes(15)));
        }

        [Test]
        public void CapDelegatedUserTokenLifetime_WhenLifetimeExceedsMaximum_ReturnsMaximum()
        {
            TokenLifetimeProvider provider = new();

            TimeSpan cappedLifetime = provider.CapDelegatedUserTokenLifetime(TimeSpan.FromHours(6));

            Assert.That(cappedLifetime, Is.EqualTo(TimeSpan.FromHours(1)));
        }

        [Test]
        public async Task GetUserAccessTokenLifetimeAsync_WhenDatabaseReturnsValue_ConvertsHoursToTimeSpan()
        {
            ApiConnection apiConnection = Substitute.For<ApiConnection>();
            apiConnection.SendQueryAsync<List<ConfExpirationTime>>(Arg.Any<string>(), Arg.Any<object?>(), Arg.Any<string?>())
                .Returns(new List<ConfExpirationTime> { new() { ExpirationValue = 4 } });
            TokenLifetimeProvider provider = new();

            TimeSpan accessLifetime = await provider.GetUserAccessTokenLifetimeAsync(apiConnection);

            Assert.That(accessLifetime, Is.EqualTo(TimeSpan.FromHours(4)));
        }
    }
}
