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
        private static readonly long[] kChangedObjectIds = [101, 102, 103];
        private static readonly long[] kChangedServiceIds = [201];
        private static readonly int[] kTwoPageOffsets = [0, 2];
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
            RecordingApiConnection api = new(kManagementId, pageSize: 10);
            int callbacks = 0;

            bool complete = await report.GetObjectsForManagementInReport(BuildQueryVariables(limit: 10), ObjCategory.all, 5, api,
                _ => { callbacks++; return Task.CompletedTask; });

            ManagementReport management = report.ReportData.ManagementData[0];
            Assert.That(complete, Is.True);
            Assert.That(api.Queries, Has.All.EqualTo(ObjectQueries.getObjectDetailsByIds));
            Assert.That(api.Variables[0][QueryVar.NwObjIds], Is.EqualTo(kChangedObjectIds));
            Assert.That(api.Variables[0][QueryVar.SvcIds], Is.EqualTo(kChangedServiceIds));
            Assert.That((List<long>)api.Variables[0][QueryVar.UserIds], Is.Empty);
            Assert.That(management.ReportObjects.Select(o => o.Id), Is.EqualTo(kChangedObjectIds));
            Assert.That(management.ReportServices.Select(s => s.Id), Is.EqualTo(kChangedServiceIds));
            Assert.That(callbacks, Is.EqualTo(1));
        }

        [Test]
        public async Task FetchesFurtherPagesWhileAPageIsFull()
        {
            ReportChanges report = BuildReport();
            RecordingApiConnection api = new(kManagementId, pageSize: 2);

            bool complete = await report.GetObjectsForManagementInReport(BuildQueryVariables(limit: 2), ObjCategory.nobj, 5, api, _ => Task.CompletedTask);

            Assert.That(complete, Is.True);
            Assert.That(api.Variables.Select(v => (int)v[QueryVar.Offset]), Is.EqualTo(kTwoPageOffsets));
            Assert.That(report.ReportData.ManagementData[0].ReportObjects.Select(o => o.Id), Is.EqualTo(kChangedObjectIds));
            Assert.That((List<long>)api.Variables[0][QueryVar.SvcIds], Is.Empty, "only network objects were requested");
        }

        [Test]
        public async Task ReportsIncompleteFetchWhenMaxFetchCyclesAreExceeded()
        {
            ReportChanges report = BuildReport();
            RecordingApiConnection api = new(kManagementId, pageSize: 1);

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
            RecordingApiConnection api = new(kManagementId, pageSize: 10);

            bool complete = await report.GetObjectsForManagementInReport(BuildQueryVariables(limit: 10), ObjCategory.all, 5, api, _ => Task.CompletedTask);

            Assert.That(complete, Is.True);
            Assert.That(api.Queries, Is.Empty);
        }

        [Test]
        public void RejectsMissingQueryVariablesAndUnknownManagement()
        {
            ReportChanges report = BuildReport();
            RecordingApiConnection api = new(kManagementId, pageSize: 10);
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
            RecordingApiConnection api = new(kManagementId, pageSize: 10);
            // 103 is a group: direct members 102 (also referenced by the rule) and 104 (nested group with members 105, 106)
            api.ObjectGroupMembers[103] = (kGroupDirectMembers, kGroupFlatMembers);
            api.ServiceGroupMembers[201] = kServiceGroupMembers;
            int callbacks = 0;

            bool complete = await report.GetObjectsForManagementInReport(BuildQueryVariables(limit: 10), ObjCategory.all, 5, api,
                _ => { callbacks++; return Task.CompletedTask; });

            ManagementReport management = report.ReportData.ManagementData[0];
            Assert.That(complete, Is.True);
            Assert.That(api.Queries, Has.Count.EqualTo(2), "referenced objects first, then the missing group members");
            Assert.That(api.Variables[1][QueryVar.NwObjIds], Is.EqualTo(kMissingObjectMembers));
            Assert.That(api.Variables[1][QueryVar.SvcIds], Is.EqualTo(kServiceGroupMembers));
            Assert.That((int)api.Variables[1][QueryVar.Offset], Is.Zero, "the member fetch starts with the first page");
            Assert.That(management.ReportObjects.Select(o => o.Id), Is.EqualTo(kAllFetchedObjectIds), "ordered by name");
            Assert.That(management.ReportServices.Select(s => s.Id), Is.EqualTo(kAllFetchedServiceIds));
            Assert.That(callbacks, Is.EqualTo(2));
        }

        [Test]
        public async Task MemberFetchCountsAgainstTheSameFetchLimit()
        {
            ReportChanges report = BuildReport();
            RecordingApiConnection api = new(kManagementId, pageSize: 10);
            api.ObjectGroupMembers[103] = (kGroupDirectMembers, kGroupFlatMembers);

            bool complete = await report.GetObjectsForManagementInReport(BuildQueryVariables(limit: 10), ObjCategory.all, 1, api, _ => Task.CompletedTask);

            Assert.That(complete, Is.False, "the members could not be fetched anymore");
            Assert.That(api.Queries, Has.Count.EqualTo(1));
            Assert.That(report.ReportData.ManagementData[0].ReportObjects.Select(o => o.Id), Is.EqualTo(kChangedObjectIds));
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
        /// Answers the object query with the requested objects, paged by limit and offset like the api does.
        /// </summary>
        private sealed class RecordingApiConnection(int managementId, int pageSize) : SimulatedApiConnection
        {
            public List<string> Queries { get; } = [];
            public List<Dictionary<string, object>> Variables { get; } = [];

            /// <summary>Network object group id -> (direct member ids, flattened member ids).</summary>
            public Dictionary<long, (long[] Direct, long[] Flat)> ObjectGroupMembers { get; } = [];

            /// <summary>Service group id -> direct member ids.</summary>
            public Dictionary<long, long[]> ServiceGroupMembers { get; } = [];

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                Dictionary<string, object> vars = new((Dictionary<string, object>)variables!);
                Queries.Add(query);
                Variables.Add(vars);
                int offset = (int)vars[QueryVar.Offset];
                ManagementReport page = new()
                {
                    Id = managementId,
                    ReportObjects = [.. Page((List<long>)vars[QueryVar.NwObjIds], offset).Select(BuildObject)],
                    ReportServices = [.. Page((List<long>)vars[QueryVar.SvcIds], offset).Select(BuildService)],
                    ReportUsers = [.. Page((List<long>)vars[QueryVar.UserIds], offset).Select(id => new NetworkUser { Id = id, Name = $"usr{id}" })]
                };
                List<ManagementReport> result = [page];
                return Task.FromResult((QueryResponseType)(object)result);
            }

            private IEnumerable<long> Page(List<long> ids, int offset)
            {
                return ids.Skip(offset).Take(pageSize);
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
