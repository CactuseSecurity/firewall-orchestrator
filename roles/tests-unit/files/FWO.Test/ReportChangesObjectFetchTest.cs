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
    [TestFixture]
    public class ReportChangesObjectFetchTest
    {
        private const int kManagementId = 7;
        private const long kGlobalObjectId = 150;
        private const long kLastImportBeforeReport = 20;
        private const long kFirstImportInReport = 22;
        private const long kLastImportInReport = 25;
        private static readonly long[] kChangedObjectIds = [101, 102, 103];
        private static readonly long[] kChangedServiceIds = [201];
        private static readonly long[][] kTwoIdChunks = [[101, 102], [103]];
        private static readonly string[] kExpectedQueriesWithoutUsers = [ObjectQueries.getNetworkObjectDetailsByIds, ObjectQueries.getNetworkServiceDetailsByIds];
        private static readonly string[] kDeclaredObjectVariables = [QueryVar.NwObjIds, QueryVar.ImportIdStart, QueryVar.ImportIdEnd];
        private static readonly long[] kGroupDirectMembers = [102, 104];
        private static readonly long[] kGroupFlatMembers = [102, 104, 105, 106];
        private static readonly long[] kServiceGroupMembers = [202];
        private static readonly long[] kMissingObjectMembers = [104, 105, 106];
        private static readonly long[] kAllFetchedObjectIds = [101, 102, 103, 104, 105, 106];
        private static readonly long[] kAllFetchedServiceIds = [201, 202];

        [Test]
        public async Task FetchesReportedObjectVersionsByIdAndFillsReportTab()
        {
            ReportChanges report = BuildReport();
            RecordingApiConnection api = new();
            int callbacks = 0;

            bool complete = await report.GetObjectsForManagementInReport(BuildQueryVariables(limit: 10), ObjCategory.all, 5, api,
                _ => { callbacks++; return Task.CompletedTask; });

            ManagementReport management = report.ReportData.ManagementData[0];
            Assert.That(complete, Is.True);
            Assert.That(api.Queries, Is.EqualTo(kExpectedQueriesWithoutUsers), "no query for the users, as the report shows none");
            Assert.That(api.Variables[0][QueryVar.NwObjIds], Is.EqualTo(kChangedObjectIds));
            Assert.That(api.Variables[1][QueryVar.SvcIds], Is.EqualTo(kChangedServiceIds));
            Assert.That(api.Variables[0].Keys, Is.EquivalentTo(kDeclaredObjectVariables), "the api rejects variables the query does not declare");
            Assert.That(management.ReportObjects.Select(o => o.Id), Is.EqualTo(kChangedObjectIds));
            Assert.That(management.ReportServices.Select(s => s.Id), Is.EqualTo(kChangedServiceIds));
            Assert.That(callbacks, Is.EqualTo(1));
        }

        [Test]
        public async Task FetchesTheIdsInChunksOfTheLimit()
        {
            ReportChanges report = BuildReport();
            RecordingApiConnection api = new();

            bool complete = await report.GetObjectsForManagementInReport(BuildQueryVariables(limit: 2), ObjCategory.nobj, 5, api, _ => Task.CompletedTask);

            Assert.That(complete, Is.True);
            Assert.That(api.Variables.Select(v => v[QueryVar.NwObjIds]), Is.EqualTo(kTwoIdChunks));
            Assert.That(report.ReportData.ManagementData[0].ReportObjects.Select(o => o.Id), Is.EqualTo(kChangedObjectIds));
            Assert.That(api.Queries, Has.All.EqualTo(ObjectQueries.getNetworkObjectDetailsByIds), "only network objects were requested");
        }

        [Test]
        public async Task ReportsIncompleteFetchWhenMaxFetchCyclesAreExceeded()
        {
            ReportChanges report = BuildReport();
            RecordingApiConnection api = new();

            bool complete = await report.GetObjectsForManagementInReport(BuildQueryVariables(limit: 1), ObjCategory.nobj, 2, api, _ => Task.CompletedTask);

            Assert.That(complete, Is.False);
            Assert.That(api.Queries, Has.Count.EqualTo(2));
        }

        [Test]
        public async Task DoesNotQueryWhenReportShowsNoObjects()
        {
            ReportChanges report = BuildReport();
            report.ReportData.ManagementData[0].RuleChanges = [];
            report.ReportData.ManagementData[0].ServiceChanges = [];
            RecordingApiConnection api = new();

            bool complete = await report.GetObjectsForManagementInReport(BuildQueryVariables(limit: 10), ObjCategory.all, 5, api, _ => Task.CompletedTask);

            Assert.That(complete, Is.True);
            Assert.That(api.Queries, Is.Empty);
        }

        [Test]
        public void RejectsMissingQueryVariablesAndUnknownManagement()
        {
            ReportChanges report = BuildReport();
            RecordingApiConnection api = new();
            Dictionary<string, object> withoutLimit = new() { [QueryVar.MgmIds] = kManagementId, [QueryVar.Offset] = 0 };
            Dictionary<string, object> unknownManagement = BuildQueryVariables(limit: 10);
            unknownManagement[QueryVar.MgmIds] = kManagementId + 1;

            Assert.ThrowsAsync<ArgumentException>(() => report.GetObjectsForManagementInReport(withoutLimit, ObjCategory.all, 5, api, _ => Task.CompletedTask));
            Assert.ThrowsAsync<ArgumentException>(() => report.GetObjectsForManagementInReport(unknownManagement, ObjCategory.all, 5, api, _ => Task.CompletedTask));
        }

        [Test]
        public async Task FetchesMissingGroupMembersAfterTheReferencedObjects()
        {
            ReportChanges report = BuildReport();
            RecordingApiConnection api = new();
            // 103 is a group: direct members 102 (also referenced by the rule) and 104 (nested group with members 105, 106)
            api.ObjectGroupMembers[103] = (kGroupDirectMembers, kGroupFlatMembers);
            api.ServiceGroupMembers[201] = kServiceGroupMembers;
            int callbacks = 0;

            bool complete = await report.GetObjectsForManagementInReport(BuildQueryVariables(limit: 10), ObjCategory.all, 5, api,
                _ => { callbacks++; return Task.CompletedTask; });

            ManagementReport management = report.ReportData.ManagementData[0];
            Assert.That(complete, Is.True);
            Assert.That(api.Queries, Has.Count.EqualTo(4), "referenced objects and services first, then their missing group members");
            Assert.That(api.Variables[2][QueryVar.NwObjIds], Is.EqualTo(kMissingObjectMembers));
            Assert.That(api.Variables[3][QueryVar.SvcIds], Is.EqualTo(kServiceGroupMembers));
            Assert.That(management.ReportObjects.Select(o => o.Id), Is.EqualTo(kAllFetchedObjectIds), "ordered by name");
            Assert.That(management.ReportServices.Select(s => s.Id), Is.EqualTo(kAllFetchedServiceIds));
            Assert.That(callbacks, Is.EqualTo(2));
        }

        [Test]
        public async Task MemberFetchCountsAgainstTheSameFetchLimit()
        {
            ReportChanges report = BuildReport();
            RecordingApiConnection api = new();
            api.ObjectGroupMembers[103] = (kGroupDirectMembers, kGroupFlatMembers);

            bool complete = await report.GetObjectsForManagementInReport(BuildQueryVariables(limit: 10), ObjCategory.all, 1, api, _ => Task.CompletedTask);

            Assert.That(complete, Is.False, "the members could not be fetched anymore");
            Assert.That(api.Queries, Is.EqualTo(kExpectedQueriesWithoutUsers));
            Assert.That(report.ReportData.ManagementData[0].ReportObjects.Select(o => o.Id), Is.EqualTo(kChangedObjectIds));
        }

        [Test]
        public async Task FetchesObjectsOwnedByAnotherManagementAndUsers()
        {
            const long kUserId = 301;
            ReportChanges report = BuildReport();
            NetworkLocation globalSource = new(new NetworkUser { Id = kUserId }, new NetworkObject { Id = kGlobalObjectId });
            report.ReportData.ManagementData[0].RuleChanges![0].NewRule.Froms = [globalSource];
            RecordingApiConnection api = new();

            bool complete = await report.GetObjectsForManagementInReport(BuildQueryVariables(limit: 10), ObjCategory.all, 5, api, _ => Task.CompletedTask);

            ManagementReport management = report.ReportData.ManagementData[0];
            Assert.That(complete, Is.True);
            Assert.That(management.ReportObjects.Select(o => o.Id), Does.Contain(kGlobalObjectId));
            Assert.That(management.ReportUsers.Select(u => u.Id), Is.EqualTo(new[] { kUserId }));
            Assert.That(api.Variables, Has.None.ContainKey(QueryVar.MgmIds), "the owner of an object may be the super manager of the reported management");
        }

        [Test]
        public async Task GivenImportIdsAreUsedIfTheReportWasNotGenerated()
        {
            ReportChanges report = BuildReport();
            RecordingApiConnection api = new();

            await report.GetObjectsForManagementInReport(BuildQueryVariables(limit: 10), ObjCategory.nobj, 5, api, _ => Task.CompletedTask);

            Assert.That(api.Variables[0][QueryVar.ImportIdStart], Is.EqualTo(1L));
            Assert.That(api.Variables[0][QueryVar.ImportIdEnd], Is.EqualTo(9L));
        }

        [TestCase(true, TestName = "GroupMembersAreLimitedToTheReportPeriodIfOtherImportIdsAreGiven")]
        [TestCase(false, TestName = "GroupMembersAreLimitedToTheReportPeriodIfNoImportIdsAreGiven")]
        public async Task GroupMembersAreLimitedToTheReportPeriod(bool importIdsGiven)
        {
            ReportChanges report = new(new DynGraphqlQuery(""), new SimulatedUserConfig(), ReportType.Changes, BuildFixedTimeFilter(), false);
            RecordingApiConnection api = new() { GeneratedManagement = BuildReport().ReportData.ManagementData[0] };
            await report.Generate(10, api, _ => Task.CompletedTask, CancellationToken.None);
            api.Variables.Clear();
            Dictionary<string, object> queryVariables = BuildQueryVariables(limit: 10);
            if (!importIdsGiven)
            {
                queryVariables.Remove(QueryVar.ImportIdStart);
                queryVariables.Remove(QueryVar.ImportIdEnd);
            }

            await report.GetObjectsForManagementInReport(queryVariables, ObjCategory.nobj, 5, api, _ => Task.CompletedTask);

            Assert.That(api.Variables[0][QueryVar.ImportIdStart], Is.EqualTo(kLastImportBeforeReport));
            Assert.That(api.Variables[0][QueryVar.ImportIdEnd], Is.EqualTo(kLastImportInReport));
        }

        private static TimeFilter BuildFixedTimeFilter()
        {
            return new()
            {
                TimeRangeType = TimeRangeType.Fixeddates,
                StartTime = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Local),
                EndTime = new DateTime(2026, 11, 2, 0, 0, 0, DateTimeKind.Local)
            };
        }

        private static Dictionary<string, object> BuildQueryVariables(int limit)
        {
            return new()
            {
                [QueryVar.MgmIds] = kManagementId,
                [QueryVar.Limit] = limit,
                [QueryVar.Offset] = 0,
                [QueryVar.ImportIdStart] = 1L,
                [QueryVar.ImportIdEnd] = 9L
            };
        }

        private static ReportChanges BuildReport()
        {
            Rule oldRule = new()
            {
                MgmtId = kManagementId,
                Froms = [new NetworkLocation(new NetworkUser(), new NetworkObject { Id = 101 })],
                Tos = [new NetworkLocation(new NetworkUser(), new NetworkObject { Id = 102 })],
                Services = [new ServiceWrapper { Content = new NetworkService { Id = 201 } }]
            };
            Rule newRule = new()
            {
                MgmtId = kManagementId,
                Froms = [new NetworkLocation(new NetworkUser(), new NetworkObject { Id = 101 })],
                Tos = [new NetworkLocation(new NetworkUser(), new NetworkObject { Id = 103 })],
                Services = [new ServiceWrapper { Content = new NetworkService { Id = 201 } }]
            };
            return new ReportChanges(new DynGraphqlQuery(""), new SimulatedUserConfig(), ReportType.Changes, new TimeFilter(), false)
            {
                ReportData = new ReportData
                {
                    ManagementData =
                    [
                        new ManagementReport
                        {
                            Id = kManagementId,
                            Name = "Branch",
                            RuleChanges = [new RuleChange { ChangeAction = 'C', OldRule = oldRule, NewRule = newRule }],
                            ServiceChanges = []
                        }
                    ]
                }
            };
        }

        /// <summary>
        /// Answers the object queries with the requested objects and the queries of the report generation.
        /// </summary>
        private sealed class RecordingApiConnection : SimulatedApiConnection
        {
            public List<string> Queries { get; } = [];
            public List<Dictionary<string, object>> Variables { get; } = [];

            /// <summary>Network object group id -> (direct member ids, flattened member ids).</summary>
            public Dictionary<long, (long[] Direct, long[] Flat)> ObjectGroupMembers { get; } = [];

            /// <summary>Service group id -> direct member ids.</summary>
            public Dictionary<long, long[]> ServiceGroupMembers { get; } = [];

            /// <summary>Management returned by the report query.</summary>
            public ManagementReport GeneratedManagement { get; init; } = new();

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                if (typeof(QueryResponseType) == typeof(List<ManagementReport>))
                {
                    return Task.FromResult((QueryResponseType)(object)new List<ManagementReport> { BuildManagement(query) });
                }
                Dictionary<string, object> vars = new((Dictionary<string, object>)variables!);
                Queries.Add(query);
                Variables.Add(vars);
                object result = query switch
                {
                    _ when query == ObjectQueries.getNetworkObjectDetailsByIds => ((long[])vars[QueryVar.NwObjIds]).Select(BuildObject).ToList(),
                    _ when query == ObjectQueries.getNetworkServiceDetailsByIds => ((long[])vars[QueryVar.SvcIds]).Select(BuildService).ToList(),
                    _ when query == ObjectQueries.getUserDetailsByIds => ((long[])vars[QueryVar.UserIds]).Select(id => new NetworkUser { Id = id, Name = $"usr{id}" }).ToList(),
                    _ => throw new NotSupportedException(query)
                };
                return Task.FromResult((QueryResponseType)result);
            }

            private ManagementReport BuildManagement(string query)
            {
                if (query == ReportQueries.getRelevantImportIdsAtTime)
                {
                    ManagementReport management = new() { Id = kManagementId };
                    management.Import.ImportAggregate.ImportAggregateMax.RelevantImportId = kLastImportBeforeReport;
                    return management;
                }
                if (query == ReportQueries.getRelevantImportIdsInTimeRange)
                {
                    return new()
                    {
                        Id = kManagementId,
                        ImportControls = [new ImportControl { ControlId = kFirstImportInReport }, new ImportControl { ControlId = kLastImportInReport }]
                    };
                }
                return GeneratedManagement;
            }

            private NetworkObject BuildObject(long id)
            {
                NetworkObject networkObject = new() { Id = id, Name = $"obj{id}" };
                if (ObjectGroupMembers.TryGetValue(id, out (long[] Direct, long[] Flat) members))
                {
                    networkObject.ObjectGroups = [.. members.Direct.Select(m => new Group<NetworkObject> { Id = m, Object = new NetworkObject { Id = m } })];
                    networkObject.ObjectGroupFlats = [.. members.Flat.Select(m => new GroupFlat<NetworkObject> { Id = m, Object = new NetworkObject { Id = m } })];
                }
                return networkObject;
            }

            private NetworkService BuildService(long id)
            {
                NetworkService service = new() { Id = id, Name = $"svc{id}" };
                if (ServiceGroupMembers.TryGetValue(id, out long[]? members))
                {
                    service.ServiceGroups = [.. members.Select(m => new Group<NetworkService> { Id = m, Object = new NetworkService { Id = m } })];
                }
                return service;
            }
        }
    }
}
