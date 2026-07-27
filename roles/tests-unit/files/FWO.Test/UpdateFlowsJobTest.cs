using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Middleware.Server.Jobs;
using FWO.Services;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class UpdateFlowsJobTest
    {
        [Test]
        public async Task Execute_ReturnsWhenNoPendingImports()
        {
            RecordingApiConnection apiConnection = new();
            GlobalConfig globalConfig = new SimulatedGlobalConfig();
            UpdateFlowsJob job = new(apiConnection, globalConfig, new FlowSync(apiConnection, globalConfig));

            await job.Execute(null!);

            Assert.That(apiConnection.QueryCount, Is.EqualTo(1));
            Assert.That(apiConnection.LastQuery, Is.EqualTo(FlowQueries.getPendingFlowSyncImports));
        }

        [Test]
        public async Task Execute_LogsAlertWhenFlowSyncThrows()
        {
            ThrowingApiConnection apiConnection = new();
            GlobalConfig globalConfig = new SimulatedGlobalConfig();
            UpdateFlowsJob job = new(apiConnection, globalConfig, new FlowSync(apiConnection, globalConfig));

            await job.Execute(null!);

            Assert.That(apiConnection.LogCount, Is.EqualTo(1));
            Assert.That(apiConnection.AlertCount, Is.EqualTo(1));
        }

        private sealed class RecordingApiConnection : SimulatedApiConnection
        {
            public int QueryCount { get; private set; }
            public string? LastQuery { get; private set; }

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                QueryCount++;
                LastQuery = query;

                if (query == FlowQueries.getPendingFlowSyncImports && typeof(QueryResponseType) == typeof(List<ImportControl>))
                {
                    return Task.FromResult((QueryResponseType)(object)new List<ImportControl>());
                }

                throw new InvalidOperationException($"Unexpected query: {query}");
            }
        }

        private sealed class ThrowingApiConnection : SimulatedApiConnection
        {
            public int QueryCount { get; private set; }
            public int LogCount { get; private set; }
            public int AlertCount { get; private set; }

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                QueryCount++;

                if (query == FlowQueries.getPendingFlowSyncImports && typeof(QueryResponseType) == typeof(List<ImportControl>))
                {
                    throw new InvalidOperationException("boom");
                }

                if (query == MonitorQueries.getOpenAlerts && typeof(QueryResponseType) == typeof(List<Alert>))
                {
                    return Task.FromResult((QueryResponseType)(object)new List<Alert>());
                }

                if (query == MonitorQueries.addLogEntry && typeof(QueryResponseType) == typeof(ReturnIdWrapper))
                {
                    LogCount++;
                    return Task.FromResult((QueryResponseType)(object)new ReturnIdWrapper
                    {
                        ReturnIds = [new ReturnId { NewId = 1 }]
                    });
                }

                if (query == MonitorQueries.addAlert && typeof(QueryResponseType) == typeof(ReturnIdWrapper))
                {
                    AlertCount++;
                    return Task.FromResult((QueryResponseType)(object)new ReturnIdWrapper
                    {
                        ReturnIds = [new ReturnId { NewIdLong = 1 }]
                    });
                }

                throw new InvalidOperationException($"Unexpected query: {query}");
            }
        }
    }
}
