using FWO.Basics;
using FWO.Data;
using FWO.Data.DeviceSpecificSettings;
using FWO.Data.Labelling;
using FWO.Data.Modelling;
using FWO.Data.Workflow;
using FWO.ExternalSystems.FirewallWrite;
using FWO.Services;
using FWO.Services.Modelling;
using FWO.Services.Workflow;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Guards the comparison of rule sides of managements that write labels (Guardicore): app roles, app zones, network
    /// areas implemented as labels and other groups are compared by their label name, app servers and areas without label
    /// by their addresses as a set.
    /// </summary>
    [TestFixture]
    [Parallelizable]
    internal class ModellingVarianceAnalysisLabelRulesTest
    {
        private const string kRecognitionOption =
            "{\"nwRegardIp\":true,\"nwRegardName\":false,\"nwRegardGroupName\":false,\"nwResolveGroup\":false,\"nwSeparateGroupAnalysis\":true," +
            "\"svcRegardPortAndProt\":true,\"svcRegardName\":false,\"svcRegardGroupName\":false,\"svcResolveGroup\":true,\"svcSplitPortRanges\":false}";

        private const string kNameAndId = "{name} ({id})";
        private static readonly List<string> kAppRoleLabel = ["AppRole: AR504711-001"];
        private static readonly List<string> kFormattedAppRoleLabel = ["AppRole: AppRole1 (AR504711-001)"];
        private static readonly List<string> kNetworkAreaLabel = ["NetworkArea: NA50"];
        private static readonly List<string> kStageLabel = ["Stage: Prod"];

        private static readonly FwoOwner Application = new() { Id = 1, Name = "App1" };
        private static readonly ExtStateTestApiConn ExtStateApiConnection = new();
        private static readonly ModellingService Https = new() { Id = 1, Name = "Https", Port = 443, PortEnd = 443, ProtoId = 6 };
        private static readonly NetworkService ProdHttps = new() { Id = 1, Name = "Https", DestinationPort = 443, DestinationPortEnd = 443, ProtoId = 6 };
        private static readonly ModellingAppServer RangeServer = new() { Id = 2, Name = "Range", Ip = "10.0.0.0", IpEnd = "10.0.0.17" };
        private static readonly ModellingAppServer Host = new() { Id = 1, Name = "Host", Ip = "1.2.3.4", IpEnd = "1.2.3.4" };
        private static readonly ModellingAppRole AppRole = new()
        {
            Id = 1,
            Name = "AppRole1",
            IdString = "AR504711-001",
            AppServers = [new() { Content = Host }]
        };
        private static readonly ModellingNetworkArea Area = new()
        {
            Id = 50,
            Name = "Area50",
            IdString = "NA50",
            IpData = [new() { Content = new() { Id = 1, Name = "Net50", Ip = "10.50.0.0/16", IpEnd = "10.50.255.255/32" } }]
        };

        private static NetworkObject Host32(long id, string ip) => new() { Id = id, Name = ip, IP = ip, IpEnd = ip, Type = new() { Name = ObjectType.Host } };
        private static NetworkObject Block(long id, string cidr, string end) => new() { Id = id, Name = cidr, IP = cidr, IpEnd = end, Type = new() { Name = ObjectType.Network } };
        private static NetworkObject Label(long id, string name, params NetworkObject[] members) => new()
        {
            Id = id,
            Name = name,
            Type = new() { Name = ObjectType.Group },
            ObjectGroupFlats = [.. members.Select(member => new GroupFlat<NetworkObject> { Object = member })]
        };

        private static ModellingConnection Connection(Action<ModellingConnection> source)
        {
            ModellingConnection connection = new()
            {
                Id = 1,
                Name = "Conn1",
                DestinationAppServers = [new() { Content = Host }],
                Services = [new() { Content = Https }]
            };
            source(connection);
            return connection;
        }

        private static Rule ProdRule(params NetworkObject[] sources) => new()
        {
            Id = 1,
            Name = "FWOC1",
            Comment = "FWOC1",
            MgmtId = 1,
            Froms = [.. sources.Select(source => new NetworkLocation(new(), source))],
            Tos = [new(new(), Host32(100, "1.2.3.4/32"))],
            Services = [new() { Content = ProdHttps }]
        };

        private static ConnectionLabel ConnLabel(int andGroup, string key, string value) =>
            new() { AndGroup = andGroup, Label = new() { Value = value, Key = new() { Name = key } } };

        /// <summary>The source (Stage: Prod AND OS: Linux) OR (Stage: Test).</summary>
        private static readonly List<ConnectionLabel> kSourceLabels = [ConnLabel(0, "Stage", "Prod"), ConnLabel(0, "OS", "Linux"), ConnLabel(1, "Stage", "Test")];

        private static async Task<ModellingVarianceResult> Analyse(ModellingConnection connection, Rule rule, bool areasAsLabels = false,
            int deviceTypeId = FirewallVendors.kGuardicoreManagement, bool labelBasedModelling = true, Action<SimulatedUserConfig>? configure = null,
            ProvisioningPositioningAlgorithm positioningAlgorithm = ProvisioningPositioningAlgorithm.DefaultEndOfRulebase)
        {
            ModellingVarianceAnalysisTestApiConn api = new() { ManagementDeviceTypeId = deviceTypeId, Rules = [rule] };
            api.Settings.SetValue(1, DeviceSpecificSettingKeys.ModellingImplementNetworkAreaObjects, areasAsLabels);
            api.Settings.SetValue(1, DeviceSpecificSettingKeys.PositioningAlgorithm, positioningAlgorithm);
            SimulatedUserConfig userConfig = new()
            {
                RuleRecognitionOption = kRecognitionOption,
                ModRolloutNatHeuristic = false,
                LabelBasedModelling = labelBasedModelling
            };
            configure?.Invoke(userConfig);
            ModellingVarianceAnalysis analysis = new(api, new ExtStateHandler(ExtStateApiConnection), userConfig, Application, DefaultInit.DoNothing);
            return await analysis.AnalyseRulesVsModelledConnections([connection], new());
        }

        private static Rule DifferingRule(ModellingVarianceResult result)
        {
            Assert.That(result.OkRules, Is.Empty);
            return result.RuleDifferences.Single().ImplementedRules.Single();
        }

        [Test]
        public async Task AppRole_IsFoundByItsLabelName()
        {
            ModellingVarianceResult result = await Analyse(Connection(c => c.SourceAppRoles = [new() { Content = AppRole }]),
                ProdRule(Label(10, "AppRole: AR504711-001", Host32(11, "1.2.3.4/32"))));

            Assert.That(result.RuleDifferences, Is.Empty);
            Assert.That(result.OkRules, Has.Count.EqualTo(1));
        }

        /// <summary>
        /// A rule references a label by its id, which a rename keeps: a label of a former name or format stands for the app
        /// role and network area whose id its value names.
        /// </summary>
        [Test]
        public async Task Labels_OfAnotherNameOrFormat_StandForTheGroupsOfTheirId()
        {
            ModellingVarianceResult result = await Analyse(Connection(c =>
                {
                    c.SourceAppRoles = [new() { Content = AppRole }];
                    c.SourceAreas = [new() { Content = Area }];
                }),
                ProdRule(Label(10, "AppRole: AR504711-001", Host32(11, "1.2.3.4/32")), Label(12, "NetworkArea: Former (NA50)", Host32(13, "10.50.0.0/16"))),
                areasAsLabels: true, configure: config => config.GroupLabelValueFormat = kNameAndId);

            Assert.That(result.RuleDifferences, Is.Empty);
        }

        private static ModellingConnection ExchangeConnection(bool isCommonService) => Connection(c =>
        {
            c.SourceAppRoles = [new() { Content = AppRole }];
            c.App = new FwoOwner { Id = 1, Name = "Exchange", ExtAppId = "COM-1611" };
            c.IsCommonService = isCommonService;
        });

        private static Rule RuleInRuleset(string rulesetName)
        {
            Rule rule = ProdRule(Label(10, "AppRole: AR504711-001", Host32(11, "1.2.3.4/32")));
            rule.Rulebase = new() { Name = $"04 Allow {rulesetName}".TrimEnd() };
            return rule;
        }

        /// <summary>
        /// With the app ruleset algorithm a rule has to lie in the ruleset of its app, named by the App label value format
        /// (common services in the one with the prefix); a rule in another ruleset differs, so that the connection is
        /// requested again and its rules are moved.
        /// </summary>
        [TestCase(false, "Exchange (COM-1611)", true)]
        [TestCase(true, "ComSvc Exchange (COM-1611)", true)]
        [TestCase(false, "COM-1611", false)]
        [TestCase(true, "Exchange (COM-1611)", false)]
        [TestCase(false, "", false)]
        [TestCase(false, "EXCHANGE (com-1611)", true)]
        public async Task Rules_HaveToLieInTheRulesetOfTheirApp(bool isCommonService, string rulesetName, bool implemented)
        {
            ModellingVarianceResult result = await Analyse(ExchangeConnection(isCommonService), RuleInRuleset(rulesetName),
                configure: config => config.AppLabelValueFormat = kNameAndId,
                positioningAlgorithm: ProvisioningPositioningAlgorithm.GuardicoreEndOfAppRulesetDistinguishCommonServices);

            Assert.That(result.RuleDifferences, implemented ? Is.Empty : Is.Not.Empty);
        }

        /// <summary>
        /// The app ruleset follows the App label value format: with the default format "{id}" it is the bare app id, so a
        /// rule in the ruleset "app name (app id)" written with another format is requested again.
        /// </summary>
        [TestCase(false, "COM-1611", true)]
        [TestCase(true, "ComSvc COM-1611", true)]
        [TestCase(false, "Exchange (COM-1611)", false)]
        [TestCase(true, "CS COM-1611", false)]
        public async Task Rules_FollowTheAppLabelValueFormat(bool isCommonService, string rulesetName, bool implemented)
        {
            ModellingVarianceResult result = await Analyse(ExchangeConnection(isCommonService), RuleInRuleset(rulesetName),
                positioningAlgorithm: ProvisioningPositioningAlgorithm.GuardicoreEndOfAppRulesetDistinguishCommonServices);

            Assert.That(result.RuleDifferences, implemented ? Is.Empty : Is.Not.Empty);
        }

        [Test]
        public async Task Rules_InTheRulesetOfTheAppId_AreInPlaceByDefault()
        {
            ModellingVarianceResult result = await Analyse(ExchangeConnection(false), RuleInRuleset("COM-1611"));

            Assert.That(result.RuleDifferences, Is.Empty);
        }

        [Test]
        public async Task Labels_OfAnotherId_AreMissingAndSurplus()
        {
            ModellingVarianceResult result = await Analyse(Connection(c => c.SourceAppRoles = [new() { Content = AppRole }]),
                ProdRule(Label(10, "AppRole: AppRole1 (AR504711-002)", Host32(11, "1.2.3.4/32"))), configure: config => config.GroupLabelValueFormat = kNameAndId);

            Rule rule = DifferingRule(result);
            Assert.Multiple(() =>
            {
                Assert.That(rule.DisregardedFroms.Select(location => location.Object.Name), Is.EqualTo(kFormattedAppRoleLabel));
                Assert.That(rule.Froms.Single().Object.IsSurplus, Is.True);
            });
        }

        [Test]
        public async Task AppRole_GroupWithoutLabelName_IsMissingAndSurplus()
        {
            ModellingVarianceResult result = await Analyse(Connection(c => c.SourceAppRoles = [new() { Content = AppRole }]),
                ProdRule(Label(10, "AR504711-001", Host32(11, "1.2.3.4/32"))));

            Rule rule = DifferingRule(result);
            Assert.Multiple(() =>
            {
                Assert.That(rule.DisregardedFroms.Select(location => location.Object.Name), Is.EqualTo(kAppRoleLabel));
                Assert.That(rule.Froms.Single().Object.IsSurplus, Is.True, "a group of addresses that are not modelled");
            });
        }

        [Test]
        public async Task AppServerRange_MatchesItsCidrBlocks()
        {
            ModellingVarianceResult result = await Analyse(Connection(c => c.SourceAppServers = [new() { Content = RangeServer }]),
                ProdRule(Block(20, "10.0.0.0/28", "10.0.0.15/32"), Block(21, "10.0.0.16/31", "10.0.0.17/32")));

            Assert.That(result.RuleDifferences, Is.Empty);
        }

        [Test]
        public async Task AppServerRange_PartlyCovered_IsMissing()
        {
            ModellingVarianceResult result = await Analyse(Connection(c => c.SourceAppServers = [new() { Content = RangeServer }]),
                ProdRule(Block(20, "10.0.0.0/28", "10.0.0.15/32")));

            Assert.That(DifferingRule(result).DisregardedFroms.Single().Object.Name, Is.EqualTo("Range"));
        }

        [Test]
        public async Task SurplusLabelAndAddress_AreMarked()
        {
            ModellingVarianceResult result = await Analyse(Connection(c => c.SourceAppServers = [new() { Content = Host }]),
                ProdRule(Host32(11, "1.2.3.4/32"), Label(12, "Stage: Prod", Host32(13, "1.2.3.4/32")), Host32(14, "9.9.9.9/32")));

            Rule rule = DifferingRule(result);
            Assert.Multiple(() =>
            {
                Assert.That(rule.DisregardedFroms, Is.Empty);
                Assert.That(rule.Froms.Where(location => location.Object.IsSurplus).Select(location => location.Object.Id), Is.EquivalentTo(new List<long> { 12, 14 }));
                Assert.That(rule.Froms.Single(location => location.Object.Id == 12).Object.Name, Is.EqualTo(kStageLabel.Single()));
            });
        }

        [Test]
        public async Task NetworkArea_ImplementedAsLabel_IsFoundByItsLabelName()
        {
            ModellingVarianceResult asLabel = await Analyse(Connection(c => c.SourceAreas = [new() { Content = Area }]),
                ProdRule(Label(30, "NetworkArea: NA50", Block(31, "10.50.0.0/16", "10.50.255.255/32"))), areasAsLabels: true);
            ModellingVarianceResult asAddresses = await Analyse(Connection(c => c.SourceAreas = [new() { Content = Area }]),
                ProdRule(Block(31, "10.50.0.0/16", "10.50.255.255/32")), areasAsLabels: true);

            Assert.Multiple(() =>
            {
                Assert.That(asLabel.RuleDifferences, Is.Empty);
                Assert.That(DifferingRule(asAddresses).DisregardedFroms.Select(location => location.Object.Name), Is.EqualTo(kNetworkAreaLabel));
            });
        }

        [Test]
        public async Task NetworkArea_NotImplementedAsLabel_IsComparedByItsAddresses()
        {
            ModellingVarianceResult result = await Analyse(Connection(c => c.SourceAreas = [new() { Content = Area }]),
                ProdRule(Block(31, "10.50.0.0/17", "10.50.127.255/32"), Block(32, "10.50.128.0/17", "10.50.255.255/32")));

            Assert.That(result.RuleDifferences, Is.Empty);
        }

        /// <summary>
        /// A host based firewall needs every connection: a connection implemented on a gateway is still requested for a
        /// management that writes labels, but not for another gateway that lacks it.
        /// </summary>
        [Test]
        public async Task Request_MissingRuleOnLabelWriter_IsRequestedThoughAnotherManagementHasIt()
        {
            const string kExtMgtData = "{\"id\":\"1\",\"name\":\"Ext\"}";
            Rule checkPointRule = ProdRule(Host32(11, "1.2.3.4/32"));
            ModellingVarianceAnalysisTestApiConn api = new()
            {
                Managements =
                [
                    new() { Id = 1, Name = "CheckPoint", ExtMgtData = kExtMgtData, DeviceType = new() { Id = 9 }, Devices = [new() { Id = 1 }] },
                    new() { Id = 2, Name = "Guardicore", DeviceType = new() { Id = FirewallVendors.kGuardicoreManagement }, Devices = [new() { Id = 2 }] },
                    new() { Id = 3, Name = "OtherCheckPoint", ExtMgtData = kExtMgtData, DeviceType = new() { Id = 9 }, Devices = [new() { Id = 3 }] }
                ],
                Rules = [checkPointRule]
            };
            SimulatedUserConfig userConfig = new() { RuleRecognitionOption = kRecognitionOption, ModRolloutNatHeuristic = false };
            ModellingVarianceAnalysis analysis = new(api, new ExtStateHandler(ExtStateApiConnection), userConfig, Application, DefaultInit.DoNothing);

            List<WfReqTask> tasks = await analysis.AnalyseModelledConnectionsForRequest([Connection(c => c.SourceAppServers = [new() { Content = Host }])]);

            Assert.That(tasks.Where(task => task.TaskType == nameof(WfTaskType.access)).Select(task => task.ManagementId), Is.EqualTo(new List<int?> { 2 }));
        }

        /// <summary>The AND-groups of the labels of a connection side are compared with the label groups of the rule, in any order.</summary>
        [Test]
        public async Task LabelGroups_AreComparedAsSetsOfAndGroups()
        {
            ModellingVarianceResult same = await Analyse(Connection(c => c.SourceLabels = kSourceLabels),
                ProdRule(Label(40, "Stage: Test"), Label(41, "OS: Linux & Stage: Prod")));
            ModellingVarianceResult otherGroup = await Analyse(Connection(c => c.SourceLabels = kSourceLabels),
                ProdRule(Label(40, "Stage: Test"), Label(42, "Stage: Prod")));

            Rule differing = DifferingRule(otherGroup);
            Assert.Multiple(() =>
            {
                Assert.That(same.RuleDifferences, Is.Empty);
                Assert.That(same.OkRules, Has.Count.EqualTo(1));
                Assert.That(differing.DisregardedFroms.Single().Object.Name, Is.EqualTo("OS: Linux & Stage: Prod"));
                Assert.That(differing.Froms.Single(location => location.Object.Id == 42).Object.IsSurplus, Is.True);
            });
        }

        [Test]
        public async Task LabelGroups_AreIgnoredWhileLabelBasedModellingIsOff()
        {
            ModellingVarianceResult result = await Analyse(Connection(c => { c.SourceLabels = kSourceLabels; c.SourceAppServers = [new() { Content = Host }]; }),
                ProdRule(Host32(11, "1.2.3.4/32")), labelBasedModelling: false);

            Assert.That(result.RuleDifferences, Is.Empty);
        }

        /// <summary>
        /// FWO writes Guardicore rules to the Allow section only: a rule of another section that carries the marker (e.g. a
        /// copy in Override Allow) is no implementation of the connection, so the writer is not asked again and again to
        /// change a rule it never touches.
        /// </summary>
        [Test]
        public async Task MarkerRuleOutsideTheAllowSection_IsNotModelled()
        {
            Rule overrideRule = ProdRule(Host32(11, "1.2.3.4/32"));
            overrideRule.Rulebase = new() { Name = "01 Override Allow APP-1" };
            Rule allowRule = ProdRule(Host32(11, "1.2.3.4/32"));
            allowRule.Rulebase = new() { Name = "04 Allow APP-1" };

            ModellingVarianceResult outside = await Analyse(Connection(c => c.SourceAppServers = [new() { Content = Host }]), overrideRule);
            ModellingVarianceResult inside = await Analyse(Connection(c => c.SourceAppServers = [new() { Content = Host }]), allowRule);

            Assert.Multiple(() =>
            {
                Assert.That(outside.ConnsNotImplemented.Select(conn => conn.Id), Is.EqualTo(new List<int> { 1 }));
                Assert.That(outside.UnModelledRules.Values.Single(), Has.Count.EqualTo(1));
                Assert.That(inside.OkRules, Has.Count.EqualTo(1));
            });
        }

        /// <summary>
        /// On a management that writes labels the marker is read as the writer finds it, as a whole word: a rule whose comment
        /// carries it only inside another word (e.g. "XFWOC1") is no rule of the connection, which the writer would never
        /// replace, so that it is not requested again and again; a marker after other text is found.
        /// </summary>
        [Test]
        public async Task MarkerInsideAnotherWord_IsNoMarkerOnLabelWriters()
        {
            Rule embedded = ProdRule(Host32(11, "1.2.3.4/32"));
            embedded.Comment = "XFWOC1";
            Rule afterText = ProdRule(Host32(11, "1.2.3.4/32"));
            afterText.Comment = "see FWOC1, ComSvc";

            ModellingVarianceResult embeddedResult = await Analyse(Connection(c => c.SourceAppServers = [new() { Content = Host }]), embedded);
            ModellingVarianceResult afterTextResult = await Analyse(Connection(c => c.SourceAppServers = [new() { Content = Host }]), afterText);

            Assert.Multiple(() =>
            {
                Assert.That(embeddedResult.ConnsNotImplemented.Select(conn => conn.Id), Is.EqualTo(new List<int> { 1 }));
                Assert.That(embeddedResult.UnModelledRules.Values.Single(), Has.Count.EqualTo(1));
                Assert.That(afterTextResult.OkRules, Has.Count.EqualTo(1));
            });
        }

        [Test]
        public async Task OtherVendor_KeepsTheGroupComparison()
        {
            ModellingVarianceResult result = await Analyse(Connection(c => c.SourceAppRoles = [new() { Content = AppRole }]),
                ProdRule(Label(10, "AppRole: AR504711-001", Host32(11, "1.2.3.4/32"))), deviceTypeId: 9);

            Assert.That(DifferingRule(result).DisregardedFroms, Is.Not.Empty, "label names mean nothing to other vendors");
        }
    }
}
