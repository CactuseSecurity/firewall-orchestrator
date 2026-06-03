using FWO.Api.Client;
using FWO.Config.Api.Data;
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

            Assert.That(provider.GetInternalServiceTokenLifetime(), Is.EqualTo(TimeSpan.FromMinutes(60)));
        }

        [Test]
        public async Task GetUserAccessTokenLifetimeAsync_WhenDatabaseReturnsValue_ConvertsHoursToTimeSpan()
        {
            ApiConnection apiConnection = Substitute.For<ApiConnection>();
            apiConnection.SendQueryAsync<List<ConfExpirationTime>>(Arg.Any<string>(), Arg.Any<object?>(), Arg.Any<string?>())
                .Returns(new List<ConfExpirationTime> { new() { ExpirationValue = 4 } });
            apiConnection.SendQueryAsync<List<ConfigItem>>(Arg.Any<string>(), Arg.Any<object?>(), Arg.Any<string?>())
                .Returns(new List<ConfigItem> { new() { Value = "Hours" } });
            TokenLifetimeProvider provider = new();

            TimeSpan accessLifetime = await provider.GetUserAccessTokenLifetimeAsync(apiConnection);

            Assert.That(accessLifetime, Is.EqualTo(TimeSpan.FromHours(4)));
        }

        [Test]
        public async Task GetUserAccessTokenLifetimeAsync_WhenConfiguredInMinutes_ConvertsMinutesToTimeSpan()
        {
            ApiConnection apiConnection = Substitute.For<ApiConnection>();
            apiConnection.SendQueryAsync<List<ConfExpirationTime>>(Arg.Any<string>(), Arg.Any<object?>(), Arg.Any<string?>())
                .Returns(new List<ConfExpirationTime> { new() { ExpirationValue = 30 } });
            apiConnection.SendQueryAsync<List<ConfigItem>>(Arg.Any<string>(), Arg.Any<object?>(), Arg.Any<string?>())
                .Returns(new List<ConfigItem> { new() { Value = "Minutes" } });
            TokenLifetimeProvider provider = new();

            TimeSpan accessLifetime = await provider.GetUserAccessTokenLifetimeAsync(apiConnection);

            Assert.That(accessLifetime, Is.EqualTo(TimeSpan.FromMinutes(30)));
        }
    }
}
