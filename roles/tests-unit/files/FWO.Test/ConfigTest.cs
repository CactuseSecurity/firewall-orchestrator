using System.Reflection;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.Api;
using FWO.Config.Api.Data;
using FWO.Data;
using FWO.Data.Workflow;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class ConfigTest
    {
        private sealed class UserConfigApiConnection(ConfigItem[] configItems) : ApiConnection
        {
            public override void SetAuthHeader(string jwt) { }
            public override void SetRole(string role) { }
            public override void SetBestRole(System.Security.Claims.ClaimsPrincipal user, List<string> targetRoleList) { }
            public override void SetProperRole(System.Security.Claims.ClaimsPrincipal user, List<string> targetRoleList) { }
            public override void SwitchBack() { }

            public override Task<ApiResponse<QueryResponseType>> SendQuerySafeAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null)
            {
                throw new NotImplementedException();
            }

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null)
            {
                if (typeof(QueryResponseType) == typeof(ConfigItem[]) && query == ConfigQueries.getConfigItemsByUser)
                {
                    return Task.FromResult((QueryResponseType)(object)configItems);
                }
                if (typeof(QueryResponseType) == typeof(List<UiText>) && query == ConfigQueries.getCustomTextsPerLanguage)
                {
                    return Task.FromResult((QueryResponseType)(object)new List<UiText>());
                }
                throw new NotImplementedException();
            }

            public override GraphQlApiSubscription<SubscriptionResponseType> GetSubscription<SubscriptionResponseType>(Action<Exception> exceptionHandler, GraphQlApiSubscription<SubscriptionResponseType>.SubscriptionUpdate subscriptionUpdateHandler, string subscription, object? variables = null, string? operationName = null)
            {
                throw new NotImplementedException();
            }

            public override void DisposeSubscriptions<T>() { }
            protected override void Dispose(bool disposing) { }
        }

        [Test]
        public void Update_KeepsDefaultEnum_WhenConfigContainsUnknownAutoCreateImplTaskOption()
        {
            SimulatedUserConfig userConfig = new();
            userConfig.ReqAutoCreateImplTasks = AutoCreateImplTaskOptions.never;

            InvokeUpdate(userConfig,
            [
                new() { Key = "reqAutoCreateImplTasks", Value = "999", User = 0 }
            ]);

            Assert.That(userConfig.ReqAutoCreateImplTasks, Is.EqualTo(AutoCreateImplTaskOptions.never));
        }

        [Test]
        public void Constructor_AppliesGlobalConfigToScheduledUserConfig()
        {
            SimulatedGlobalConfig globalConfig = new();
            globalConfig.RawConfigItems =
            [
                new() { Key = "reqOwnerBased", Value = "true", User = 0 }
            ];

            using UserConfigApiConnection apiConnection = new([]);
            UserConfig userConfig = new(globalConfig, apiConnection, new UiUser { DbId = 50, Language = "English" });

            Assert.That(userConfig.ReqOwnerBased, Is.True);
        }

        [Test]
        public void Constructor_DoesNotOverwriteUserSpecificConfigWithGlobalValues()
        {
            SimulatedGlobalConfig globalConfig = new();
            globalConfig.RawConfigItems =
            [
                new() { Key = "reqOwnerBased", Value = "true", User = 0 },
                new() { Key = "elementsPerFetch", Value = "777", User = 0 }
            ];

            using UserConfigApiConnection apiConnection =
                new([new() { Key = "elementsPerFetch", Value = "55", User = 50 }]);
            UserConfig userConfig = new(globalConfig, apiConnection, new UiUser { DbId = 50, Language = "English" });

            Assert.That(userConfig.ReqOwnerBased, Is.True);
            Assert.That(userConfig.ElementsPerFetch, Is.EqualTo(55));
        }

        [Test]
        public void DailyCheckSubscription_IncludesNotificationSettingsUsedByRunningJobs()
        {
            Assert.That(ConfigQueries.subscribeDailyCheckConfigChanges, Does.Contain("notificationLanguage"));
            Assert.That(ConfigQueries.subscribeDailyCheckConfigChanges, Does.Contain("ownerActiveRuleEmailBody"));
            Assert.That(ConfigQueries.subscribeDailyCheckConfigChanges, Does.Contain("ruleExpiryEmailBody"));
        }

        private static void InvokeUpdate(FWO.Config.Api.Config config, ConfigItem[] configItems)
        {
            MethodInfo updateMethod = typeof(FWO.Config.Api.Config).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new MissingMethodException(typeof(FWO.Config.Api.Config).FullName, "Update");

            updateMethod.Invoke(config, [configItems]);
        }
    }
}
