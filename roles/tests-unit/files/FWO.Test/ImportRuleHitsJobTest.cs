using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data;
using FWO.Middleware.Server.Jobs;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class ImportRuleHitsJobTest
    {
        private static readonly List<Management> kManagements = [new Management { Id = 1 }];

        [Test]
        public async Task Execute_RequestsRuleHitImportsWithoutAlert()
        {
            RuleHitJobTestApiConn apiConnection = new(failRequests: false);
            ImportRuleHitsJob job = new(apiConnection, new SimulatedGlobalConfig());

            await job.Execute(null!);

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.RequestCalls, Is.EqualTo(1));
                Assert.That(apiConnection.AlertCalls, Is.Zero);
            });
        }

        [Test]
        public async Task Execute_AlertsWhenRequestingFails()
        {
            RuleHitJobTestApiConn apiConnection = new(failRequests: true);
            ImportRuleHitsJob job = new(apiConnection, new SimulatedGlobalConfig());

            await job.Execute(null!);

            Assert.That(apiConnection.AlertCalls, Is.GreaterThan(0), "a failing request is alerted");
        }

        [Test]
        public async Task Execute_WithCanceledTokenStopsWithoutRequesting()
        {
            RuleHitJobTestApiConn apiConnection = new(failRequests: false);
            ImportRuleHitsJob job = new(apiConnection, new SimulatedGlobalConfig());
            using CancellationTokenSource cancellationTokenSource = new();
            await cancellationTokenSource.CancelAsync();

            await job.Execute(null!, cancellationTokenSource.Token);

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.RequestCalls, Is.Zero);
                Assert.That(apiConnection.AlertCalls, Is.Zero);
            });
        }

        private sealed class RuleHitJobTestApiConn(bool failRequests) : SimulatedApiConnection
        {
            public int RequestCalls { get; private set; }
            public int AlertCalls { get; private set; }

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null,
                string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                if (query == ImportQueries.getManagementsForRuleHitImport)
                {
                    return Task.FromResult((QueryResponseType)(object)kManagements);
                }
                if (query == ImportQueries.addRuleHitImportRequests)
                {
                    RequestCalls++;
                    if (failRequests)
                    {
                        throw new InvalidOperationException("request insert failed");
                    }
                    return Task.FromResult((QueryResponseType)(object)new ReturnId { AffectedRows = 1 });
                }
                if (query == MonitorQueries.addAlert)
                {
                    AlertCalls++;
                    return Task.FromResult((QueryResponseType)(object)new ReturnIdWrapper());
                }
                if (query == MonitorQueries.addDataImportLogEntry || query == MonitorQueries.addLogEntry)
                {
                    return Task.FromResult((QueryResponseType)(object)new ReturnIdWrapper());
                }
                return Task.FromResult(default(QueryResponseType)!);
            }
        }
    }
}
