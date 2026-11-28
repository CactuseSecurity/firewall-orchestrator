using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Data.Report;
using FWO.Report;
using FWO.Report.Filter;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    internal class ReportAppRulesTest
    {
        private const int kManagementId = 1;
        private const int kGatewayId = 11;
        private const int kRulebaseId = 21;
        private const int kOtherRulebaseId = 22;
        private const int kOwnerId = 5;

        private readonly SimulatedUserConfig userConfig = new();

        /// <summary>
        /// Answers the app server query of the owner and the object query of the right sidebar.
        /// </summary>
        private sealed class AppRulesApiConnection(List<ModellingAppServer> appServers, NetworkObject[]? reportObjects = null) : SimulatedApiConnection
        {
            public object? AppServerVariables { get; private set; }

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                if (query == ModellingQueries.getAppServersForOwner)
                {
                    AppServerVariables = variables;
                    return Task.FromResult((QueryResponseType)(object)appServers);
                }
                if (query == ObjectQueries.getReportFilteredNetworkObjectDetails)
                {
                    return Task.FromResult((QueryResponseType)(object)new List<ManagementReport> { new() { ReportObjects = reportObjects ?? [] } });
                }
                if (typeof(QueryResponseType) == typeof(List<ManagementReport>))
                {
                    return Task.FromResult((QueryResponseType)(object)new List<ManagementReport>());
                }
                throw new NotImplementedException(query);
            }
        }

        private static List<ModellingAppServer> OwnerServers()
        {
            return [new ModellingAppServer { Ip = "10.0.0.1/32", IpEnd = "" }, new ModellingAppServer { Ip = "10.0.1.0/32", IpEnd = "10.0.1.255/32" }];
        }

        private static NetworkObject Host(long id, string ip, string? ipEnd = null)
        {
            return new NetworkObject { Id = id, Name = $"object {id}", IP = ip, IpEnd = ipEnd ?? ip, Type = new NetworkObjectType { Name = ObjectType.Host } };
        }

        private static NetworkObject AnyObject(long id)
        {
            return new NetworkObject { Id = id, Name = "any", IP = "0.0.0.0/32", IpEnd = "255.255.255.255/32", Type = new NetworkObjectType { Name = ObjectType.Network } };
        }

        private static NetworkObject GroupOf(long id, params NetworkObject[] members)
        {
            return new NetworkObject
            {
                Id = id,
                Name = $"group {id}",
                Type = new NetworkObjectType { Name = ObjectType.Group },
                ObjectGroupFlats = [.. members.Select(member => new GroupFlat<NetworkObject> { Object = member })],
                ObjectGroups = [.. members.Select(member => new Group<NetworkObject> { Object = member })]
            };
        }

        private static NetworkLocation At(NetworkObject networkObject)
        {
            return new NetworkLocation(new NetworkUser(), networkObject);
        }

        private static Rule BuildRule(long id, NetworkObject[] froms, NetworkObject[] tos, string action = "accept", int rulebaseId = kRulebaseId)
        {
            return new Rule { Id = id, Name = $"rule {id}", RulebaseId = rulebaseId, Action = action, Froms = [.. froms.Select(At)], Tos = [.. tos.Select(At)] };
        }

        private static List<ManagementReport> Managements(params Rule[] rules)
        {
            DeviceReport gateway = new()
            {
                Id = kGatewayId,
                Name = "gateway",
                RulebaseLinks = [new RulebaseLink { GatewayId = kGatewayId, NextRulebaseId = kRulebaseId, IsInitial = true }]
            };
            foreach (Rule rule in rules)
            {
                gateway.AddRule(rule);
            }
            return
            [
                new ManagementReport
                {
                    Id = kManagementId,
                    Name = "management",
                    Rulebases = [new RulebaseReport { Id = kRulebaseId, Name = "rulebase", Rules = rules }],
                    Devices = [gateway]
                }
            ];
        }

        private static async Task<List<ManagementReport>> Prepare(ModellingFilter modellingFilter, params Rule[] rules)
        {
            return await ReportAppRules.PrepareAppRulesReport(Managements(rules), modellingFilter, new AppRulesApiConnection(OwnerServers()), kOwnerId);
        }

        private ReportAppRules CreateReport(ModellingFilter modellingFilter)
        {
            modellingFilter.SelectedOwner = new FwoOwner { Id = kOwnerId, Name = "owner" };
            ReportTemplate template = new("", new ReportParams((int)ReportType.AppRules, new DeviceFilter()) { ModellingFilter = modellingFilter });
            return (ReportAppRules)ReportBase.ConstructReport(template, userConfig);
        }

        private static Dictionary<string, object> ObjectQueryVariables()
        {
            return new() { [QueryVar.MgmIds] = kManagementId, [QueryVar.Limit] = 100, [QueryVar.Offset] = 0 };
        }

        [Test]
        public async Task OnlyRulesTouchingTheOwnersServersAreReported()
        {
            Rule matching = BuildRule(1, [Host(101, "10.0.0.1/32"), Host(102, "192.168.0.1/32")], [Host(103, "172.16.0.1/32")]);
            Rule foreign = BuildRule(2, [Host(104, "192.168.0.2/32")], [Host(105, "172.16.0.2/32")]);

            List<ManagementReport> managements = await Prepare(new ModellingFilter(), matching, foreign);

            Rule reported = managements.Single().Rulebases.Single().Rules.Single();
            Assert.That(reported.Id, Is.EqualTo(1));
            Assert.That(reported.Froms.Select(from => from.Object.Id), Is.EqualTo((List<long>)[101]));
            Assert.That(reported.DisregardedFroms.Select(from => from.Object.Id), Is.EqualTo((List<long>)[102]));
            Assert.That(reported.Tos, Is.Empty);
            Assert.That(reported.DisregardedTos.Select(to => to.Object.Id), Is.EqualTo((List<long>)[103]));
            Assert.That(managements[0].ReportedRuleIds, Is.EqualTo((List<long>)[1]));
        }

        [Test]
        public async Task AnOwnerServerRangeMatchesOverlappingObjects()
        {
            Rule rule = BuildRule(1, [Host(101, "10.0.1.128/32")], [Host(102, "10.0.1.200/32", "10.0.2.10/32")]);

            List<ManagementReport> managements = await Prepare(new ModellingFilter(), rule);

            Rule reported = managements.Single().Rulebases.Single().Rules.Single();
            Assert.That(reported.Froms, Has.Length.EqualTo(1));
            Assert.That(reported.Tos, Has.Length.EqualTo(1));
        }

        [Test]
        public async Task ManagementsWithoutMatchingRulesAreLeftOut()
        {
            List<ManagementReport> managements = await Prepare(new ModellingFilter(), BuildRule(1, [Host(101, "192.168.0.1/32")], [Host(102, "192.168.0.2/32")]));

            Assert.That(managements, Is.Empty);
        }

        [TestCase(false, 0)]
        [TestCase(true, 1)]
        public async Task DropRulesAreOnlyReportedWhenWanted(bool showDropRules, int expectedRules)
        {
            Rule dropRule = BuildRule(1, [Host(101, "10.0.0.1/32")], [], action: "drop");

            List<ManagementReport> managements = await Prepare(new ModellingFilter { ShowDropRules = showDropRules }, dropRule);

            Assert.That(managements.SelectMany(management => management.Rulebases).SelectMany(rulebase => rulebase.Rules).Count(), Is.EqualTo(expectedRules));
        }

        [TestCase(false, 0)]
        [TestCase(true, 1)]
        public async Task AnyObjectsOnlyMatchWhenWanted(bool showAnyMatch, int expectedRules)
        {
            Rule rule = BuildRule(1, [AnyObject(101)], [Host(102, "192.168.0.1/32")]);

            List<ManagementReport> managements = await Prepare(new ModellingFilter { ShowAnyMatch = showAnyMatch }, rule);

            Assert.That(managements.SelectMany(management => management.Rulebases).SelectMany(rulebase => rulebase.Rules).Count(), Is.EqualTo(expectedRules));
        }

        [Test]
        public async Task AGroupMatchesWhenOneOfItsMembersIsAnOwnerServer()
        {
            NetworkObject group = GroupOf(110, new NetworkObject { Id = 111, Name = "without ip", IP = null, Type = new NetworkObjectType { Name = ObjectType.Host } },
                Host(112, "192.168.0.1/32"), Host(113, "10.0.0.1/32"));
            Rule rule = BuildRule(1, [group], [GroupOf(120, Host(121, "192.168.0.2/32"))]);

            List<ManagementReport> managements = await Prepare(new ModellingFilter(), rule);

            Rule reported = managements.Single().Rulebases.Single().Rules.Single();
            Assert.That(reported.Froms.Select(from => from.Object.Id), Is.EqualTo((List<long>)[110]));
            Assert.That(reported.DisregardedTos.Select(to => to.Object.Id), Is.EqualTo((List<long>)[120]));
        }

        [Test]
        public async Task ANegatedSourceMatchesWhenTheOwnerServersAreNotAllExcluded()
        {
            Rule notCovering = BuildRule(1, [Host(101, "10.0.0.1/32")], []);
            notCovering.SourceNegated = true;
            Rule covering = BuildRule(2, [Host(102, "10.0.0.0/32", "10.0.255.255/32")], []);
            covering.SourceNegated = true;

            List<ManagementReport> managements = await Prepare(new ModellingFilter(), notCovering, covering);

            // "not 10.0.0.1" still reaches the server range 10.0.1.0-255, "not 10.0.0.0-10.0.255.255" reaches none
            Assert.That(managements.Single().Rulebases.Single().Rules.Select(rule => rule.Id), Is.EqualTo((List<long>)[1]));
        }

        [Test]
        public async Task SourcesAreIgnoredWhenOnlyTheDestinationShallMatch()
        {
            Rule rule = BuildRule(1, [Host(101, "10.0.0.1/32")], [Host(102, "10.0.0.1/32")]);

            List<ManagementReport> managements = await Prepare(new ModellingFilter { ShowSourceMatch = false }, rule);

            Rule reported = managements.Single().Rulebases.Single().Rules.Single();
            Assert.That(reported.Froms, Is.Empty);
            Assert.That(reported.DisregardedFroms.Select(from => from.Object.Id), Is.EqualTo((List<long>)[101]));
            Assert.That(reported.Tos.Select(to => to.Object.Id), Is.EqualTo((List<long>)[102]));
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task TheDisregardedObjectsAreShownForFullRulesOnly(bool showFullRules)
        {
            Rule rule = BuildRule(1, [Host(101, "10.0.0.1/32")], []);

            List<ManagementReport> managements = await Prepare(new ModellingFilter { ShowFullRules = showFullRules }, rule);

            Assert.That(managements.Single().Rulebases.Single().Rules.Single().ShowDisregarded, Is.EqualTo(showFullRules));
        }

        [Test]
        public async Task TheGatewaysKeepOnlyTheirLinkedRules()
        {
            Rule linked = BuildRule(1, [Host(101, "10.0.0.1/32")], []);
            Rule unlinked = BuildRule(2, [Host(102, "10.0.0.1/32")], [], rulebaseId: kOtherRulebaseId);

            List<ManagementReport> managements = await Prepare(new ModellingFilter(), linked, unlinked);

            DeviceReport gateway = managements.Single().Devices.Single();
            Assert.That(gateway.GetRuleList().Select(rule => rule.Id), Is.EqualTo((List<long>)[1]));
        }

        [Test]
        public async Task TheOwnersServersAreQueriedForTheGivenOwner()
        {
            AppRulesApiConnection apiConnection = new([]);

            await ReportAppRules.PrepareAppRulesReport([], new ModellingFilter(), apiConnection, kOwnerId);

            Assert.That(apiConnection.AppServerVariables!.GetType().GetProperty("appId")!.GetValue(apiConnection.AppServerVariables), Is.EqualTo(kOwnerId));
        }

        [Test]
        public async Task GeneratingWithoutManagementsReportsNothing()
        {
            ReportAppRules report = CreateReport(new ModellingFilter());

            await report.Generate(10, new AppRulesApiConnection(OwnerServers()), _ => Task.CompletedTask, CancellationToken.None);

            Assert.That(report.ReportData.ManagementData, Is.Empty);
        }

        [Test]
        public async Task AReportBuiltFromARulesReportKeepsItsQuery()
        {
            ReportTemplate template = new("", new ReportParams((int)ReportType.Rules, new DeviceFilter()));
            ReportRules rulesReport = (ReportRules)ReportBase.ConstructReport(template, userConfig);

            ReportAppRules report = new(rulesReport, new ModellingFilter());
            await report.Generate(10, new AppRulesApiConnection([]), _ => Task.CompletedTask, CancellationToken.None);

            Assert.That(report.Query, Is.SameAs(rulesReport.Query));
            Assert.That(report.ReportType, Is.EqualTo(ReportType.Rules));
        }

        [Test]
        public async Task TheSidebarShowsOnlyTheRelevantObjectsAndHighlightsTheMatchingOnes()
        {
            NetworkObject ownerHost = Host(101, "10.0.0.1/32");
            NetworkObject foreignHost = Host(102, "192.168.0.1/32");
            NetworkObject unrelated = Host(103, "172.16.0.1/32");
            NetworkObject group = GroupOf(110, Host(111, "10.0.0.1/32"), Host(112, "192.168.0.2/32"));
            ReportAppRules report = CreateReport(new ModellingFilter());
            report.ReportData.ManagementData = await Prepare(new ModellingFilter(), BuildRule(1, [ownerHost, foreignHost], [group]));
            AppRulesApiConnection apiConnection = new(OwnerServers(), [ownerHost, foreignHost, unrelated, group]);

            bool gotAllObjects = await report.GetObjectsForManagementInReport(ObjectQueryVariables(), ObjCategory.nobj, 2, apiConnection, _ => Task.CompletedTask);

            ManagementReport management = report.ReportData.ManagementData.Single();
            Assert.That(gotAllObjects, Is.True);
            Assert.That(management.ReportObjects.Select(obj => obj.Id), Is.EqualTo((List<long>)[101, 110]));
            Assert.That(ownerHost.Highlighted, Is.True);
            Assert.That(group.Highlighted, Is.True);
            Assert.That(group.ObjectGroupFlats.Select(member => member.Object!.Highlighted), Is.EqualTo((List<bool>)[true, false]));
        }

        [Test]
        public async Task FullRulesShowAllFetchedObjects()
        {
            NetworkObject ownerHost = Host(101, "10.0.0.1/32");
            NetworkObject anyObject = AnyObject(104);
            ReportAppRules report = CreateReport(new ModellingFilter { ShowFullRules = true });
            report.ReportData.ManagementData = await Prepare(new ModellingFilter { ShowFullRules = true }, BuildRule(1, [ownerHost], []));
            AppRulesApiConnection apiConnection = new(OwnerServers(), [ownerHost, anyObject, Host(103, "172.16.0.1/32")]);

            await report.GetObjectsForManagementInReport(ObjectQueryVariables(), ObjCategory.nobj, 2, apiConnection, _ => Task.CompletedTask);

            ManagementReport management = report.ReportData.ManagementData.Single();
            Assert.That(management.ReportObjects, Has.Length.EqualTo(3));
            Assert.That(anyObject.Highlighted, Is.True);
        }

        [Test]
        public async Task ARuleMatchingOnlyByDestinationKeepsItsDisregardedSourcesRelevant()
        {
            NetworkObject disregardedSource = Host(102, "192.168.0.1/32");
            ReportAppRules report = CreateReport(new ModellingFilter());
            report.ReportData.ManagementData = await Prepare(new ModellingFilter(), BuildRule(1, [disregardedSource], [Host(101, "10.0.0.1/32")]));

            await report.GetObjectsForManagementInReport(ObjectQueryVariables(), ObjCategory.nobj, 2, new AppRulesApiConnection(OwnerServers(), [disregardedSource]), _ => Task.CompletedTask);

            ManagementReport management = report.ReportData.ManagementData.Single();
            Assert.That(management.RelevantObjectIds, Is.EquivalentTo((List<long>)[101, 102]));
            Assert.That(management.HighlightedObjectIds, Is.EqualTo((List<long>)[101]));
            Assert.That(disregardedSource.Highlighted, Is.False);
        }

        [Test]
        public void AskingForObjectsOfAnUnknownManagementIsRejected()
        {
            ReportAppRules report = CreateReport(new ModellingFilter());

            Assert.ThrowsAsync<ArgumentException>(() => report.GetObjectsForManagementInReport(ObjectQueryVariables(), ObjCategory.nobj, 2,
                new AppRulesApiConnection(OwnerServers()), _ => Task.CompletedTask));
        }
    }
}
