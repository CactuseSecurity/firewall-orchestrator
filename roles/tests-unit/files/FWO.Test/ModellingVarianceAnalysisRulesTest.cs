using FWO.Basics;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.ExternalSystems.FirewallWrite;
using FWO.Services;
using FWO.Services.Modelling;
using FWO.Services.Workflow;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Guards the comparison of modelled connections with their production rules. A management that writes labels
    /// (Guardicore) splits a connection with services of different protocols into one rule per protocol, so all its rules
    /// carrying the connection ID are compared together with the connection, and service groups are compared resolved.
    /// </summary>
    [TestFixture]
    [Parallelizable]
    internal class ModellingVarianceAnalysisRulesTest
    {
        private const int kOtherDeviceTypeId = 9;
        private const string kRecognitionOption =
            "{\"nwRegardIp\":true,\"nwRegardName\":false,\"nwRegardGroupName\":false,\"nwResolveGroup\":false,\"nwSeparateGroupAnalysis\":true," +
            "\"svcRegardPortAndProt\":true,\"svcRegardName\":false,\"svcRegardGroupName\":false,\"svcResolveGroup\":true,\"svcSplitPortRanges\":false}";
        private const string kRecognitionOptionWithServiceGroups =
            "{\"nwRegardIp\":true,\"nwRegardName\":false,\"nwRegardGroupName\":false,\"nwResolveGroup\":false,\"nwSeparateGroupAnalysis\":true," +
            "\"svcRegardPortAndProt\":true,\"svcRegardName\":false,\"svcRegardGroupName\":false,\"svcResolveGroup\":false,\"svcSplitPortRanges\":false}";

        private static readonly List<long> kBothRuleIds = [1, 2];
        private static readonly List<string> kDns = ["Dns"];
        private static readonly List<string> kHttps = ["Https"];
        private static readonly List<string> kSurplusCombinations = ["DnsTcp", "HttpsUdp"];

        private static readonly FwoOwner Application = new() { Id = 1, Name = "App1" };
        private static readonly ExtStateTestApiConn ExtStateApiConnection = new();
        private static readonly ModellingAppServer AppServer = new() { Id = 1, Name = "AppServerUnchanged", Ip = "1.2.3.4" };
        private static readonly NetworkObject ProdAppServer = new() { Id = 10, Name = "AppServerUnchanged", IP = "1.2.3.4", Type = new() { Name = ObjectType.Host } };

        private static readonly ModellingService Https = new() { Id = 1, Name = "Https", Port = 443, PortEnd = 443, ProtoId = 6 };
        private static readonly ModellingService Dns = new() { Id = 2, Name = "Dns", Port = 53, PortEnd = 53, ProtoId = 17 };
        private static readonly NetworkService ProdHttps = new() { Id = 1, Name = "Https", DestinationPort = 443, DestinationPortEnd = 443, ProtoId = 6 };
        private static readonly NetworkService ProdDns = new() { Id = 2, Name = "Dns", DestinationPort = 53, DestinationPortEnd = 53, ProtoId = 17 };
        private static readonly NetworkService ProdDnsTcp = new() { Id = 3, Name = "DnsTcp", DestinationPort = 53, DestinationPortEnd = 53, ProtoId = 6 };
        private static readonly NetworkService ProdHttpsUdp = new() { Id = 4, Name = "HttpsUdp", DestinationPort = 443, DestinationPortEnd = 443, ProtoId = 17 };

        private static ModellingConnection Connection(bool asServiceGroup = false) => new()
        {
            Id = 1,
            Name = "Conn1",
            SourceAppServers = [new() { Content = AppServer }],
            DestinationAppServers = [new() { Content = AppServer }],
            Services = asServiceGroup ? [] : [new() { Content = Https }, new() { Content = Dns }],
            ServiceGroups = asServiceGroup
                ? [new() { Content = new ModellingServiceGroup { Id = 1, Name = "WebAndDns", Services = [new() { Content = Https }, new() { Content = Dns }] } }]
                : []
        };

        private static Rule ProdRule(long id, params NetworkService[] services) => new()
        {
            Id = id,
            Name = $"FWOC1 part {id}",
            Comment = "FWOC1",
            MgmtId = 1,
            Froms = [new(new(), ProdAppServer)],
            Tos = [new(new(), ProdAppServer)],
            Services = [.. services.Select(service => new ServiceWrapper { Content = service })]
        };

        private static async Task<ModellingVarianceResult> Analyse(int deviceTypeId, List<Rule> rules, ModellingConnection connection,
            string recognitionOption = kRecognitionOption)
        {
            ModellingVarianceAnalysisTestApiConn api = new() { ManagementDeviceTypeId = deviceTypeId, Rules = rules };
            SimulatedUserConfig userConfig = new() { RuleRecognitionOption = recognitionOption, ModRolloutNatHeuristic = false };
            ModellingVarianceAnalysis analysis = new(api, new ExtStateHandler(ExtStateApiConnection), userConfig, Application, DefaultInit.DoNothing);
            return await analysis.AnalyseRulesVsModelledConnections([connection], new());
        }

        [Test]
        public async Task LabelWriter_OneRulePerProtocol_ImplementsTheConnectionTogether()
        {
            ModellingVarianceResult result = await Analyse(FirewallVendors.kGuardicoreManagement, [ProdRule(1, ProdHttps), ProdRule(2, ProdDns)], Connection());

            Assert.That(result.RuleDifferences, Is.Empty);
            Assert.That(result.ConnsNotImplemented, Is.Empty);
            Assert.That(result.OkRules, Has.Count.EqualTo(1));
            Assert.That(result.OkRules[0].ImplementedRules.Select(rule => rule.Id), Is.EquivalentTo(kBothRuleIds));
        }

        [Test]
        public async Task LabelWriter_RuleOfOneProtocolMissing_AllRulesDifferAndTheMissingServiceIsNamedOnce()
        {
            ModellingVarianceResult result = await Analyse(FirewallVendors.kGuardicoreManagement, [ProdRule(1, ProdHttps)], Connection());

            Assert.That(result.OkRules, Is.Empty);
            Assert.That(result.RuleDifferences, Has.Count.EqualTo(1));
            Rule rule = result.RuleDifferences[0].ImplementedRules.Single();
            Assert.That(rule.DisregardedServices.Select(service => service.Name), Is.EqualTo(kDns));
        }

        [Test]
        public async Task LabelWriter_RuleWithPortsOfBothProtocols_HasSurplusServices()
        {
            // one rule with TCP and UDP allows 443 and 53 for both protocols: the import shows all four combinations
            ModellingVarianceResult result = await Analyse(FirewallVendors.kGuardicoreManagement,
                [ProdRule(1, ProdHttps, ProdDns, ProdDnsTcp, ProdHttpsUdp)], Connection());

            Assert.That(result.OkRules, Is.Empty);
            Rule rule = result.RuleDifferences.Single().ImplementedRules.Single();
            Assert.That(rule.Services.Where(service => service.Content.IsSurplus).Select(service => service.Content.Name),
                Is.EquivalentTo(kSurplusCombinations));
        }

        [Test]
        public async Task LabelWriter_DifferentSourceInOneRule_AllRulesDiffer()
        {
            Rule otherSource = ProdRule(2, ProdDns);
            otherSource.Froms = [new(new(), new NetworkObject { Id = 11, Name = "Other", IP = "9.9.9.9", Type = new() { Name = ObjectType.Host } })];

            ModellingVarianceResult result = await Analyse(FirewallVendors.kGuardicoreManagement, [ProdRule(1, ProdHttps), otherSource], Connection());

            Assert.That(result.OkRules, Is.Empty);
            Assert.That(result.RuleDifferences.Single().ImplementedRules, Has.Count.EqualTo(2));
        }

        [Test]
        public async Task LabelWriter_ComparesServiceGroupsResolvedWhateverTheRecognitionOption()
        {
            ModellingVarianceResult result = await Analyse(FirewallVendors.kGuardicoreManagement, [ProdRule(1, ProdHttps), ProdRule(2, ProdDns)],
                Connection(asServiceGroup: true), kRecognitionOptionWithServiceGroups);

            Assert.That(result.RuleDifferences, Is.Empty);
            Assert.That(result.OkRules.Single().ImplementedRules, Has.Count.EqualTo(2));
        }

        [Test]
        public async Task OtherVendor_RulesAreComparedOneByOne()
        {
            ModellingVarianceResult result = await Analyse(kOtherDeviceTypeId, [ProdRule(1, ProdHttps), ProdRule(2, ProdDns)], Connection());

            Assert.That(result.OkRules, Is.Empty);
            List<Rule> rules = result.RuleDifferences.Single().ImplementedRules;
            Assert.That(rules, Has.Count.EqualTo(2));
            Assert.That(rules.Single(rule => rule.Id == 1).DisregardedServices.Select(service => service.Name), Is.EqualTo(kDns));
            Assert.That(rules.Single(rule => rule.Id == 2).DisregardedServices.Select(service => service.Name), Is.EqualTo(kHttps));
        }

        [Test]
        public async Task OtherVendor_OneRuleWithAllServices_ImplementsTheConnection()
        {
            ModellingVarianceResult result = await Analyse(kOtherDeviceTypeId, [ProdRule(1, ProdHttps, ProdDns)], Connection());

            Assert.That(result.RuleDifferences, Is.Empty);
            Assert.That(result.OkRules.Single().ImplementedRules.Single().Id, Is.EqualTo(1));
        }
    }
}
