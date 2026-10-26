using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api.Data;
using FWO.Middleware.Server.Services;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class RestApiConfigServiceTest
    {
        private static readonly ConfigItem[] kChangedLimit = [new ConfigItem { Key = "maxRestResponseObjectLimit", Value = "250", User = 0 }];

        [Test]
        public void ChangedLimitIsAppliedToTheSharedConfigAtOnce()
        {
            SubscribingApiConnection apiConnection = new();
            SimulatedGlobalConfig globalConfig = new();
            using RestApiConfigService service = new(apiConnection, globalConfig);

            apiConnection.ConfigUpdateHandler!(kChangedLimit);

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.Subscription, Is.EqualTo(ConfigQueries.subscribeRestApiConfigChanges));
                Assert.That(globalConfig.MaxRestResponseObjectLimit, Is.EqualTo(250));
                Assert.That(ListPaging.GetMaxObjectLimit(globalConfig), Is.EqualTo(250));
            });
        }

        [Test]
        public void AFailingSubscriptionKeepsTheConfiguredLimit()
        {
            SimulatedGlobalConfig globalConfig = new();

            Assert.DoesNotThrow(() =>
            {
                using RestApiConfigService service = new(new SimulatedApiConnection(), globalConfig);
            });
            Assert.That(ListPaging.GetMaxObjectLimit(globalConfig), Is.EqualTo(GlobalConst.kDefaultMaxRestResponseObjectLimit));
        }

        [Test]
        public void SubscriptionSelectsOnlyTheGlobalRestApiSettings()
        {
            Assert.Multiple(() =>
            {
                Assert.That(ConfigQueries.subscribeRestApiConfigChanges, Does.Contain("maxRestResponseObjectLimit"));
                Assert.That(ConfigQueries.subscribeRestApiConfigChanges, Does.Contain("config_user: {_eq: 0}"));
            });
        }

        [Test]
        public void ConfigDataDefaultsTheLimitToTenThousand()
        {
            Assert.That(new ConfigData().MaxRestResponseObjectLimit, Is.EqualTo(10000));
        }

        [TestCase(1, GlobalConst.kMinRestResponseObjectLimit)]
        [TestCase(GlobalConst.kMinRestResponseObjectLimit, GlobalConst.kMinRestResponseObjectLimit)]
        [TestCase(0, GlobalConst.kDefaultMaxRestResponseObjectLimit)]
        [TestCase(GlobalConst.kMaxRestResponseObjectLimitCeiling + 1, GlobalConst.kMaxRestResponseObjectLimitCeiling)]
        public void UsableLimitStaysWithinItsBounds(int configuredLimit, int expectedLimit)
        {
            Assert.That(ConfigData.GetUsableMaxRestResponseObjectLimit(configuredLimit), Is.EqualTo(expectedLimit));
        }

        private sealed class SubscribingApiConnection : SimulatedApiConnection
        {
            public GraphQlApiSubscription<ConfigItem[]>.SubscriptionUpdate? ConfigUpdateHandler { get; private set; }
            public string? Subscription { get; private set; }

            public override GraphQlApiSubscription<SubscriptionResponseType> GetSubscription<SubscriptionResponseType>(Action<Exception> exceptionHandler,
                GraphQlApiSubscription<SubscriptionResponseType>.SubscriptionUpdate subscriptionUpdateHandler, string subscription,
                object? variables = null, string? operationName = null)
            {
                ConfigUpdateHandler = (GraphQlApiSubscription<ConfigItem[]>.SubscriptionUpdate)(object)subscriptionUpdateHandler;
                Subscription = subscription;
                return null!;
            }
        }
    }
}
