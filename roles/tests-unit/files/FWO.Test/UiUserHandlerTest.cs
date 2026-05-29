using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.Api.Data;
using FWO.Data.Enums;
using FWO.Middleware.Server;
using NSubstitute;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    public class UiUserHandlerTest
    {
        [Test]
        public async Task GetExpirationTime_WhenConfigExistsInDatabase_ReturnsDatabaseValue()
        {
            ApiConnection apiConnection = Substitute.For<ApiConnection>();
            apiConnection.SendQueryAsync<List<ConfExpirationTime>>(ConfigQueries.getConfigItemByKey, Arg.Any<object?>(), Arg.Any<string?>())
                .Returns(new List<ConfExpirationTime> { new() { ExpirationValue = 11 } });

            int expirationTime = await UiUserHandler.GetExpirationTime(apiConnection, nameof(ConfigData.AccessTokenLifetime));

            Assert.That(expirationTime, Is.EqualTo(11));
        }

        [Test]
        public async Task GetExpirationTime_WhenConfigMissingInDatabase_ReturnsConfiguredDefault()
        {
            ApiConnection apiConnection = Substitute.For<ApiConnection>();
            apiConnection.SendQueryAsync<List<ConfExpirationTime>>(ConfigQueries.getConfigItemByKey, Arg.Any<object?>(), Arg.Any<string?>())
                .Returns(new List<ConfExpirationTime>());

            int expirationTime = await UiUserHandler.GetExpirationTime(apiConnection, nameof(ConfigData.RefreshTokenLifetime));

            Assert.That(expirationTime, Is.EqualTo(1));
        }

        [Test]
        public async Task GetExpirationTime_WhenLifetimeKeyIsUnknown_ReturnsHardcodedDefault()
        {
            ApiConnection apiConnection = Substitute.For<ApiConnection>();

            int expirationTime = await UiUserHandler.GetExpirationTime(apiConnection, "UnknownLifetimeKey");

            Assert.That(expirationTime, Is.EqualTo(720));
        }

        [Test]
        public async Task GetExpirationUnit_WhenConfigExistsInDatabase_ReturnsParsedUnit()
        {
            ApiConnection apiConnection = Substitute.For<ApiConnection>();
            apiConnection.SendQueryAsync<List<ConfigItem>>(ConfigQueries.getConfigItemByKey, Arg.Any<object?>(), Arg.Any<string?>())
                .Returns(new List<ConfigItem> { new() { Value = "Minutes" } });

            TokenLifetimeUnit unit = await UiUserHandler.GetExpirationUnit(apiConnection, nameof(ConfigData.AccessTokenLifetimeUnit));

            Assert.That(unit, Is.EqualTo(TokenLifetimeUnit.Minutes));
        }

        [Test]
        public async Task GetExpirationUnit_WhenConfigMissingInDatabase_ReturnsConfiguredDefault()
        {
            ApiConnection apiConnection = Substitute.For<ApiConnection>();
            apiConnection.SendQueryAsync<List<ConfigItem>>(ConfigQueries.getConfigItemByKey, Arg.Any<object?>(), Arg.Any<string?>())
                .Returns(new List<ConfigItem>());

            TokenLifetimeUnit unit = await UiUserHandler.GetExpirationUnit(apiConnection, nameof(ConfigData.RefreshTokenLifetimeUnit));

            Assert.That(unit, Is.EqualTo(TokenLifetimeUnit.Days));
        }
    }
}
