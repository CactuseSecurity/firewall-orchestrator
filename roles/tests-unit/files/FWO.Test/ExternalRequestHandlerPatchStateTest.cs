using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data;
using FWO.Middleware.Server;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class ExternalRequestHandlerPatchStateTest
    {
        [TestCase(ExtStates.ExtReqAckRejected)]
        [TestCase(ExtStates.ExtReqAcknowledged)]
        [TestCase(ExtStates.ExtReqDiscarded)]
        public async Task PatchState_FinalStateSetsFinishDate(ExtStates finalState)
        {
            PatchStateApiConn apiConnection = new();
            using ExternalRequestHandler handler = new(new SimulatedUserConfig(), apiConnection, null);

            bool result = await handler.PatchState(new ExternalRequest { Id = 7, TicketId = 20, ExtRequestState = finalState.ToString() });

            Assert.That(result, Is.True);
            Assert.That(apiConnection.FinalUpdates, Has.Count.EqualTo(1));
            Assert.That(apiConnection.FinalUpdates[0], Does.Contain("finishDate"));
            Assert.That(apiConnection.FinalUpdates[0], Does.Contain(finalState.ToString()));
            Assert.That(apiConnection.ProcessUpdates, Is.Empty);
        }

        [Test]
        public async Task PatchState_NonFinalStateKeepsFinishDateEmpty()
        {
            PatchStateApiConn apiConnection = new();
            using ExternalRequestHandler handler = new(new SimulatedUserConfig(), apiConnection, null);

            bool result = await handler.PatchState(new ExternalRequest { Id = 7, TicketId = 20, ExtRequestState = ExtStates.ExtReqInProgress.ToString() });

            Assert.That(result, Is.True);
            Assert.That(apiConnection.ProcessUpdates, Has.Count.EqualTo(1));
            Assert.That(apiConnection.FinalUpdates, Is.Empty);
        }

        private sealed class PatchStateApiConn : ExtTicketHandlerTestApiConn
        {
            public List<string> FinalUpdates { get; } = [];
            public List<string> ProcessUpdates { get; } = [];

            public override async Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                if (query == ExtRequestQueries.updateExtRequestFinal)
                {
                    FinalUpdates.Add(variables?.ToString() ?? "");
                    return (QueryResponseType)(object)new ReturnId { UpdatedIdLong = 7 };
                }
                if (query == ExtRequestQueries.updateExtRequestProcess)
                {
                    ProcessUpdates.Add(variables?.ToString() ?? "");
                    return (QueryResponseType)(object)new ReturnId { UpdatedIdLong = 7 };
                }
                return await base.SendQueryAsync<QueryResponseType>(query, variables, operationName, chunkingOptions);
            }
        }
    }
}
