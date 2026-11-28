using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Data;
using FWO.Data.Report;
using FWO.Report;
using FWO.Report.Filter;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Covers how a changes report pages through the changes between every pair of consecutive imports of the period.
    /// </summary>
    [TestFixture]
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    internal class ReportChangesPagingTest
    {
        private const string kChangesQuery = "changes-query";
        private const int kManagementId = 1;
        private const int kElementsPerFetch = 2;
        private const long kImportBeforePeriod = 5;

        /// <summary>
        /// Delivers one import before the period and the given imports within it, and answers the changes query
        /// for each import pair and page with the configured number of rule changes.
        /// </summary>
        private sealed class ChangesPagingApiConnection(long[] importsInPeriod, Dictionary<(long Old, long New, int Offset), int> changesPerPage) : SimulatedApiConnection
        {
            public List<(long Old, long New, int Offset)> FetchedPages { get; } = [];

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                if (query == ReportQueries.getRelevantImportIdsAtTime)
                {
                    ManagementReport management = new() { Id = kManagementId, Name = "management" };
                    management.Import.ImportAggregate.ImportAggregateMax.RelevantImportId = kImportBeforePeriod;
                    return Task.FromResult((QueryResponseType)(object)new List<ManagementReport> { management });
                }
                if (query == ReportQueries.getRelevantImportIdsInTimeRange)
                {
                    ManagementReport management = new()
                    {
                        Id = kManagementId,
                        ImportControls = [.. importsInPeriod.Select(importId => new ImportControl { ControlId = importId })]
                    };
                    return Task.FromResult((QueryResponseType)(object)new List<ManagementReport> { management });
                }
                if (query == kChangesQuery && variables is Dictionary<string, object> queryVariables)
                {
                    (long Old, long New, int Offset) page = ((long)queryVariables[QueryVar.ImportIdOld], (long)queryVariables[QueryVar.ImportIdNew], (int)queryVariables[QueryVar.Offset]);
                    FetchedPages.Add(page);
                    int changeCount = changesPerPage.GetValueOrDefault(page);
                    ManagementReport result = new()
                    {
                        Id = kManagementId,
                        Name = "management",
                        RuleChanges = [.. Enumerable.Range(0, changeCount).Select(index => new RuleChange { ChangeAction = 'I', NewRule = new Rule { Id = page.New * 100 + page.Offset + index } })]
                    };
                    return Task.FromResult((QueryResponseType)(object)new List<ManagementReport> { result });
                }
                throw new NotImplementedException(query);
            }
        }

        private static ReportChanges CreateReport()
        {
            DynGraphqlQuery query = new("") { FullQuery = kChangesQuery, RelevantManagementIds = [kManagementId] };
            TimeFilter timeFilter = new() { TimeRangeType = TimeRangeType.Fixeddates, StartTime = new DateTime(2026, 1, 1), EndTime = new DateTime(2026, 2, 1) };
            return new ReportChanges(query, new SimulatedUserConfig(), ReportType.Changes, timeFilter, includeObjects: false);
        }

        [Test]
        public async Task WithOneImportInThePeriodOnlyTheFirstPageIsFetched()
        {
            ChangesPagingApiConnection apiConnection = new([6], new() { [(kImportBeforePeriod, 6, 0)] = 1 });
            ReportChanges report = CreateReport();

            await report.Generate(kElementsPerFetch, apiConnection, _ => Task.CompletedTask, CancellationToken.None);

            Assert.That(apiConnection.FetchedPages, Is.EqualTo(new List<(long, long, int)> { (kImportBeforePeriod, 6, 0) }));
            Assert.That(report.ReportData.ManagementData.Single().RuleChanges, Has.Length.EqualTo(1));
        }

        [Test]
        public async Task EveryPairOfConsecutiveImportsIsFetchedAndFullPagesArePagedOn()
        {
            // the first pair fills its first page, so its second page is fetched as well before the next pair follows
            ChangesPagingApiConnection apiConnection = new([6, 7, 8], new()
            {
                [(kImportBeforePeriod, 6, 0)] = kElementsPerFetch,
                [(kImportBeforePeriod, 6, kElementsPerFetch)] = kElementsPerFetch,
                [(kImportBeforePeriod, 6, 2 * kElementsPerFetch)] = 1,
                [(6, 7, 0)] = 1,
                [(7, 8, 0)] = 0
            });
            ReportChanges report = CreateReport();
            int callbacks = 0;

            await report.Generate(kElementsPerFetch, apiConnection, _ => { callbacks++; return Task.CompletedTask; }, CancellationToken.None);

            Assert.That(apiConnection.FetchedPages, Is.EqualTo(new List<(long, long, int)>
            {
                (kImportBeforePeriod, 6, 0),
                (kImportBeforePeriod, 6, kElementsPerFetch),
                (kImportBeforePeriod, 6, 2 * kElementsPerFetch),
                (6, 7, 0),
                (7, 8, 0)
            }));
            Assert.That(report.ReportData.ManagementData.Single().RuleChanges, Has.Length.EqualTo(2 * kElementsPerFetch + 1 + 1));
            Assert.That(callbacks, Is.EqualTo(4));
        }

        [Test]
        public async Task ACancelledGenerationStopsBeforeTheNextPage()
        {
            ChangesPagingApiConnection apiConnection = new([6, 7, 8], new() { [(kImportBeforePeriod, 6, 0)] = 1 });
            ReportChanges report = CreateReport();
            using CancellationTokenSource tokenSource = new();
            tokenSource.Cancel();

            Assert.ThrowsAsync<OperationCanceledException>(() => report.Generate(kElementsPerFetch, apiConnection, _ => Task.CompletedTask, tokenSource.Token));
            Assert.That(apiConnection.FetchedPages, Is.EqualTo(new List<(long, long, int)> { (kImportBeforePeriod, 6, 0) }));
        }
    }
}
