using FWO.Api.Client;
using FWO.Api.Client.Queries;
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
        private const int kTicketBatchSize = 500;
        private const int kHistoryTicketCount = 3 * kTicketBatchSize + 1;
        private static readonly FwoOwner Application = new() { Id = 1, Name = "App1" };
        private readonly SimulatedUserConfig userConfig = new();
        private readonly ExtStateHandler extStateHandler = new(new ExtStateTestApiConn());

        [Test]
        public async Task GetSuccessfulRequestState_ReturnsNewestDoneTicketFromOneBatch()
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
            Assert.That(apiConn.Queries, Is.EqualTo(new List<string> { ExtRequestQueries.getLatestTicketIds, RequestQueries.getTicketStatesByIds }));
            Assert.That(apiConn.RequestedTicketIds, Is.EqualTo(new List<long> { 30, 20, 10 }));
            Assert.That(apiConn.RequestedTicketBatches, Has.Count.EqualTo(1));
            Assert.That(RequestQueries.getTicketStatesByIds, Does.Not.Contain("reqtasks"));
            Assert.That(RequestQueries.getTicketStatesByIds, Does.Not.Contain("additional_info"));
        }

        /// <summary>
        /// Completed tickets can still be described when the completion date or requester is unavailable.
        /// </summary>
        [Test]
        public async Task GetSuccessfulRequestState_WithMissingCompletionDetails_ReturnsAvailableFields()
        {
            WfTicket ticket = CreateTicket(20, kDoneStateId, "Requester");
            ticket.CompletionDate = null;
            ticket.Requester = null;
            RequestStateTestApiConn apiConn = new()
            {
                LatestTicketIds = [new() { Id = ticket.Id }],
                Tickets = [ticket]
            };
            ModellingVarianceAnalysis varianceAnalysis = new(apiConn, extStateHandler, userConfig, Application, DefaultInit.DoNothing);

            string result = await varianceAnalysis.GetSuccessfulRequestState();

            Assert.That(result, Is.EqualTo("Last successful: 2026-01-20 08:00:00, Implemented: , "));
        }

        /// <summary>
        /// A successful newest ticket must prevent requests for older batches, even with a long history.
        /// </summary>
        [Test]
        public async Task GetSuccessfulRequestState_WithNewestDoneTicket_DoesNotReadOlderBatches()
        {
            RequestStateTestApiConn apiConn = new()
            {
                LatestTicketIds = CreateTicketHistory(),
                Tickets = [CreateTicket(kHistoryTicketCount, kDoneStateId, "Newest")]
            };
            ModellingVarianceAnalysis varianceAnalysis = new(apiConn, extStateHandler, userConfig, Application, DefaultInit.DoNothing);

            string result = await varianceAnalysis.GetSuccessfulRequestState();

            List<long> expectedIds = [.. apiConn.LatestTicketIds.Take(kTicketBatchSize).Select(ticket => ticket.Id)];
            Assert.That(result, Does.EndWith(", Newest"));
            Assert.That(apiConn.RequestedTicketBatches, Has.Count.EqualTo(1));
            Assert.That(apiConn.RequestedTicketIds, Is.EqualTo(expectedIds));
        }

        /// <summary>
        /// Open or missing tickets in an earlier batch must not hide a completed ticket in the next batch.
        /// </summary>
        [Test]
        public async Task GetSuccessfulRequestState_WithDoneTicketInSecondBatch_StopsAfterThatBatch()
        {
            const int kNewestDoneTicketId = kHistoryTicketCount - kTicketBatchSize;
            RequestStateTestApiConn apiConn = new()
            {
                LatestTicketIds = CreateTicketHistory(),
                Tickets =
                [
                    CreateTicket(kNewestDoneTicketId - 1, kDoneStateId, "Older"),
                    CreateTicket(kHistoryTicketCount, kOpenStateId, "Open"),
                    CreateTicket(kNewestDoneTicketId, kDoneStateId, "Newest done")
                ]
            };
            ModellingVarianceAnalysis varianceAnalysis = new(apiConn, extStateHandler, userConfig, Application, DefaultInit.DoNothing);

            string result = await varianceAnalysis.GetSuccessfulRequestState();

            List<long> expectedIds = [.. apiConn.LatestTicketIds.Take(2 * kTicketBatchSize).Select(ticket => ticket.Id)];
            Assert.That(result, Does.EndWith(", Newest done"));
            Assert.That(apiConn.RequestedTicketBatches, Has.Count.EqualTo(2));
            Assert.That(apiConn.RequestedTicketBatches.All(batch => batch.Count == kTicketBatchSize), Is.True);
            Assert.That(apiConn.RequestedTicketIds, Is.EqualTo(expectedIds));
        }

        /// <summary>
        /// Exhausting a history must visit every batch, including a final partial batch.
        /// </summary>
        [Test]
        public async Task GetSuccessfulRequestState_WithNoDoneTickets_ReadsFinalPartialBatch()
        {
            RequestStateTestApiConn apiConn = new() { LatestTicketIds = CreateTicketHistory() };
            ModellingVarianceAnalysis varianceAnalysis = new(apiConn, extStateHandler, userConfig, Application, DefaultInit.DoNothing);

            string result = await varianceAnalysis.GetSuccessfulRequestState();

            List<long> expectedIds = [.. apiConn.LatestTicketIds.Select(ticket => ticket.Id)];
            Assert.That(result, Is.EqualTo(userConfig.GetText("never_succ_req")));
            Assert.That(apiConn.RequestedTicketBatches, Has.Count.EqualTo(4));
            Assert.That(apiConn.RequestedTicketBatches.All(batch => batch.Count <= kTicketBatchSize), Is.True);
            Assert.That(apiConn.RequestedTicketBatches.Last(), Has.Count.EqualTo(1));
            Assert.That(apiConn.RequestedTicketIds, Is.EqualTo(expectedIds));
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

        /// <summary>
        /// Query failures must retain the existing empty-state fallback.
        /// </summary>
        [Test]
        public async Task GetSuccessfulRequestState_WithQueryFailure_ReturnsEmptyState()
        {
            RequestStateTestApiConn apiConn = new()
            {
                LatestTicketIds = [new() { Id = 1 }],
                FailTicketQueries = true
            };
            ModellingVarianceAnalysis varianceAnalysis = new(apiConn, extStateHandler, userConfig, Application, DefaultInit.DoNothing);

            string result = await varianceAnalysis.GetSuccessfulRequestState();

            Assert.That(result, Is.Empty);
        }

        /// <summary>
        /// Builds a descending ticket ID history spanning several batches.
        /// </summary>
        private static List<TicketId> CreateTicketHistory()
        {
            return [.. Enumerable.Range(1, kHistoryTicketCount).Reverse().Select(id => new TicketId { Id = id })];
        }

        /// <summary>
        /// Creates the ticket fields used to describe a completed request.
        /// </summary>
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
            public List<long> RequestedTicketIds { get; } = [];
            public List<List<long>> RequestedTicketBatches { get; } = [];
            public bool FailTicketQueries { get; set; }

            /// <summary>
            /// Returns ticket IDs or the ticket states from the requested batch while recording query inputs.
            /// </summary>
            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                Queries.Add(query);
                if (typeof(QueryResponseType) == typeof(List<TicketId>) && query == ExtRequestQueries.getLatestTicketIds)
                {
                    return Task.FromResult((QueryResponseType)(object)LatestTicketIds);
                }
                if (typeof(QueryResponseType) == typeof(List<WfTicket>) && query == RequestQueries.getTicketStatesByIds)
                {
                    if (FailTicketQueries)
                    {
                        throw new InvalidOperationException("Ticket query failed.");
                    }
                    List<long> batchIds = [.. (IEnumerable<long>)(variables?.GetType().GetProperty("ids")?.GetValue(variables) ?? new List<long>())];
                    RequestedTicketBatches.Add(batchIds);
                    RequestedTicketIds.AddRange(batchIds);
                    List<WfTicket> requestedTickets = [.. Tickets.Where(ticket => batchIds.Contains(ticket.Id))];
                    return Task.FromResult((QueryResponseType)(object)requestedTickets);
                }
                throw new NotImplementedException();
            }
        }
    }
}
