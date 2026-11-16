using System.Text.Json;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data;
using FWO.Middleware.Server;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class RuleHitImportRequesterTest
    {
        private static readonly List<Management> kManagements = [new Management { Id = 3 }, new Management { Id = 7 }];
        private static readonly List<Management> kSingleManagement = [new Management { Id = 3 }];
        private static readonly List<Management> kNoManagements = [];

        [Test]
        public async Task RequestsARuleHitImportForEveryManagement()
        {
            RuleHitRequestTestApiConn apiConnection = new(kManagements);

            int written = await new RuleHitImportRequester(apiConnection).RequestRuleHitImports();

            Assert.Multiple(() =>
            {
                Assert.That(written, Is.EqualTo(kManagements.Count));
                Assert.That(apiConnection.InsertCalls, Is.EqualTo(1));
                Assert.That(apiConnection.InsertedVariables, Does.Contain("\"mgm_id\":3"));
                Assert.That(apiConnection.InsertedVariables, Does.Contain("\"mgm_id\":7"));
            });
        }

        [Test]
        public async Task WritesNothingWithoutManagements()
        {
            RuleHitRequestTestApiConn apiConnection = new(kNoManagements);

            int written = await new RuleHitImportRequester(apiConnection).RequestRuleHitImports();

            Assert.Multiple(() =>
            {
                Assert.That(written, Is.Zero);
                Assert.That(apiConnection.InsertCalls, Is.Zero);
            });
        }

        [Test]
        public void StopsBeforeWritingWhenCanceled()
        {
            RuleHitRequestTestApiConn apiConnection = new(kSingleManagement);
            using CancellationTokenSource cancellationTokenSource = new();
            cancellationTokenSource.Cancel();

            Assert.Multiple(() =>
            {
                Assert.That(() => new RuleHitImportRequester(apiConnection).RequestRuleHitImports(cancellationTokenSource.Token),
                    Throws.InstanceOf<OperationCanceledException>());
                Assert.That(apiConnection.InsertCalls, Is.Zero);
            });
        }

        internal sealed class RuleHitRequestTestApiConn(List<Management> managements) : SimulatedApiConnection
        {
            public int InsertCalls { get; private set; }
            public string InsertedVariables { get; private set; } = "";

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null,
                string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                if (query == ImportQueries.getManagementsForRuleHitImport)
                {
                    return Task.FromResult((QueryResponseType)(object)managements);
                }
                if (query == ImportQueries.addRuleHitImportRequests)
                {
                    InsertCalls++;
                    InsertedVariables = JsonSerializer.Serialize(variables);
                    return Task.FromResult((QueryResponseType)(object)new ReturnId { AffectedRows = managements.Count });
                }
                throw new NotImplementedException($"unexpected query: {query}");
            }
        }
    }
}
