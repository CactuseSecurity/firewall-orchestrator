using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Data;
using FWO.Data.Workflow;
using FWO.Services;
using FWO.Services.Modelling;
using FWO.Services.Workflow;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    internal class ModellingVarianceAnalysisRequestStateTest
    {
        private const int kDoneStateId = 631;
        private const int kOpenStateId = 2;
        private static readonly FwoOwner Application = new() { Id = 1, Name = "App1" };
        private readonly SimulatedUserConfig userConfig = new();
        private readonly ExtStateHandler extStateHandler = new(new ExtStateTestApiConn());

        [Test]
        public async Task GetSuccessfulRequestState_ReturnsNewestDoneTicketFromOneChunkedQuery()
        {
            RequestStateTestApiConn apiConn = new()
            {
                LatestTicketIds = [new() { Id = 30 }, new() { Id = 20 }, new() { Id = 10 }],
                Tickets =
                [
                    CreateTicket(10, kDoneStateId, "Older"),
                    CreateTicket(20, kDoneStateId, "Newer"),
                    CreateTicket(30, kOpenStateId, "Open")
                ]
            };
            ModellingVarianceAnalysis varianceAnalysis = new(apiConn, extStateHandler, userConfig, Application, DefaultInit.DoNothing);

            string result = await varianceAnalysis.GetSuccessfulRequestState();

            Assert.That(result, Is.EqualTo("Last successful: 2026-01-20 08:00:00, Implemented: 2026-02-20 08:00:00, Newer"));
            Assert.That(apiConn.Queries, Is.EqualTo(new List<string> { ExtRequestQueries.getLatestTicketIds, RequestQueries.getTicketOverviewsByIds }));
            Assert.That(apiConn.RequestedTicketIds, Is.EqualTo(new List<long> { 30, 20, 10 }));
            Assert.That(apiConn.TicketChunkingOptions, Is.Not.Null);
            Assert.That(apiConn.TicketChunkingOptions!.Enabled, Is.True);
            Assert.That(apiConn.TicketChunkingOptions.ChunkVariableName, Is.EqualTo("ids"));
            Assert.That(apiConn.TicketChunkingOptions.MergeMode, Is.EqualTo(ChunkMergeMode.TopLevelArrayConcat));
        }

        [Test]
        public async Task GetSuccessfulRequestState_RequestsDuplicateTicketIdsOnlyOnce()
        {
            RequestStateTestApiConn apiConn = new()
            {
                LatestTicketIds = [new() { Id = 20 }, new() { Id = 20 }, new() { Id = 10 }],
                Tickets = [CreateTicket(10, kDoneStateId, "Older")]
            };
            ModellingVarianceAnalysis varianceAnalysis = new(apiConn, extStateHandler, userConfig, Application, DefaultInit.DoNothing);

            string result = await varianceAnalysis.GetSuccessfulRequestState();

            Assert.That(result, Does.EndWith(", Older"));
            Assert.That(apiConn.RequestedTicketIds, Is.EqualTo(new List<long> { 20, 10 }));
        }

        [Test]
        public async Task GetSuccessfulRequestState_WithoutDoneTicket_ReturnsNeverSucceeded()
        {
            RequestStateTestApiConn apiConn = new()
            {
                LatestTicketIds = [new() { Id = 20 }, new() { Id = 10 }],
                Tickets = [CreateTicket(20, kOpenStateId, "Open")]
            };
            ModellingVarianceAnalysis varianceAnalysis = new(apiConn, extStateHandler, userConfig, Application, DefaultInit.DoNothing);

            string result = await varianceAnalysis.GetSuccessfulRequestState();

            Assert.That(result, Is.EqualTo(userConfig.GetText("never_succ_req")));
        }

        [Test]
        public async Task GetSuccessfulRequestState_WithoutTickets_DoesNotQueryTickets()
        {
            RequestStateTestApiConn apiConn = new();
            ModellingVarianceAnalysis varianceAnalysis = new(apiConn, extStateHandler, userConfig, Application, DefaultInit.DoNothing);

            string result = await varianceAnalysis.GetSuccessfulRequestState();

            Assert.That(result, Is.EqualTo(userConfig.GetText("never_requested")));
            Assert.That(apiConn.Queries, Is.EqualTo(new List<string> { ExtRequestQueries.getLatestTicketIds }));
        }

        private static WfTicket CreateTicket(long id, int stateId, string requesterName)
        {
            return new WfTicket
            {
                Id = id,
                StateId = stateId,
                CreationDate = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc).AddDays(id - 1),
                CompletionDate = new DateTime(2026, 2, 1, 8, 0, 0, DateTimeKind.Utc).AddDays(id - 1),
                Requester = new() { Name = requesterName }
            };
        }

        private sealed class RequestStateTestApiConn : SimulatedApiConnection
        {
            public List<TicketId> LatestTicketIds { get; set; } = [];
            public List<WfTicket> Tickets { get; set; } = [];
            public List<string> Queries { get; } = [];
            public List<long> RequestedTicketIds { get; private set; } = [];
            public QueryChunkingOptions? TicketChunkingOptions { get; private set; }

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                Queries.Add(query);
                if (typeof(QueryResponseType) == typeof(List<TicketId>) && query == ExtRequestQueries.getLatestTicketIds)
                {
                    return Task.FromResult((QueryResponseType)(object)LatestTicketIds);
                }
                if (typeof(QueryResponseType) == typeof(List<WfTicket>) && query == RequestQueries.getTicketOverviewsByIds)
                {
                    RequestedTicketIds = [.. (IEnumerable<long>)(variables?.GetType().GetProperty("ids")?.GetValue(variables) ?? new List<long>())];
                    TicketChunkingOptions = chunkingOptions;
                    List<WfTicket> requestedTickets = [.. Tickets.Where(ticket => RequestedTicketIds.Contains(ticket.Id))];
                    return Task.FromResult((QueryResponseType)(object)requestedTickets);
                }
                throw new NotImplementedException();
            }
        }
    }
}
