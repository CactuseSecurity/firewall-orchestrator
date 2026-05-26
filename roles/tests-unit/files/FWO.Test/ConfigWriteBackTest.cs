using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.Api;
using FWO.Config.Api.Data;
using NUnit.Framework;
using System.Reflection;

namespace FWO.Test
{
    [TestFixture]
    internal class ConfigWriteBackTest
    {
        [Test]
        public async Task WriteToDatabase_UpdatesLiveConfigAfterSuccessfulSave()
        {
            SimulatedGlobalConfig globalConfig = new()
            {
                FlowNamingSourceManagementId = null
            };
            ConfigData editableConfig = await globalConfig.GetEditableConfig();
            editableConfig.FlowNamingSourceManagementId = 42;
            TrackingApiConnection apiConnection = new();

            await globalConfig.WriteToDatabase(editableConfig, apiConnection);

            Assert.That(apiConnection.UpsertConfigCallCount, Is.EqualTo(1));
            Assert.That(apiConnection.LastConfigItems, Has.Count.EqualTo(1));
            Assert.That(apiConnection.LastConfigItems[0].Key, Is.EqualTo("flowNamingSourceManagementId"));
            Assert.That(apiConnection.LastConfigItems[0].Value, Is.EqualTo("42"));
            Assert.That(globalConfig.FlowNamingSourceManagementId, Is.EqualTo(42));
        }

        private sealed class TrackingApiConnection : SimulatedApiConnection
        {
            public int UpsertConfigCallCount { get; private set; }
            public List<ConfigItem> LastConfigItems { get; private set; } = [];

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null)
            {
                if (query == ConfigQueries.upsertConfigItems)
                {
                    UpsertConfigCallCount++;
                    PropertyInfo configItemsProperty = variables?.GetType().GetProperty("config_items")
                        ?? throw new MissingFieldException("config_items");
                    LastConfigItems = ((IEnumerable<ConfigItem>)configItemsProperty.GetValue(variables)!).ToList();
                    return Task.FromResult((QueryResponseType)(object)new object());
                }

                throw new NotImplementedException();
            }
        }
    }
}
