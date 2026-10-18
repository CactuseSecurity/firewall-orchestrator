using System.Net;
using FWO.Basics;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Data.Report;
using FWO.Report;
using FWO.Services.Modelling;
using FWO.Ui.Display;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Every display helper that builds html from stored firewall or modelling data must encode that data
    /// (GHSA-mm29-m6vq-gwgg). The payload carries an element with an event handler, quotes and an entity.
    /// </summary>
    [TestFixture]
    [Parallelizable]
    internal class HtmlDisplayEncodingTest
    {
        private const string kPayload = "<img src=x onerror=console.log(1)>\"'&amp;";
        private const string kRawElement = "<img";
        private static readonly string EncodedPayload = WebUtility.HtmlEncode(kPayload);

        private static readonly ZoneWrapper[] PayloadZones = [new() { Content = new NetworkZone { Name = kPayload } }];
        private static readonly DeviceWrapper[] PayloadGateways = [new() { Content = new Device { Id = 1, Name = kPayload } }];
        private static readonly ComplianceViolation[] PayloadViolations = [new() { Details = kPayload }];
        private static readonly List<ModellingAppServer> NoSurplusAppServers = [];

        private readonly SimulatedUserConfig userConfig = new();

        private static void AssertEncoded(string html)
        {
            Assert.Multiple(() =>
            {
                Assert.That(html, Does.Not.Contain(kRawElement));
                Assert.That(html, Does.Contain(EncodedPayload));
            });
        }

        private static NetworkObject PayloadObject()
        {
            return new NetworkObject { Id = 1, Name = kPayload, IP = "10.0.0.1/32", IpEnd = "10.0.0.1/32", Type = new NetworkObjectType { Name = ObjectType.Host } };
        }

        private static NetworkService PayloadService()
        {
            return new NetworkService { Id = 1, Name = kPayload, Uid = kPayload, Comment = kPayload, Type = new NetworkServiceType { Name = "simple" } };
        }

        private static Rule PayloadRule()
        {
            return new Rule
            {
                Id = 1,
                MgmtId = 1,
                Name = kPayload,
                Uid = kPayload,
                Comment = kPayload,
                Action = kPayload,
                Track = kPayload,
                RuleFromZones = PayloadZones,
                RuleToZones = PayloadZones,
                Froms = [new NetworkLocation(new NetworkUser(), PayloadObject())],
                Tos = [new NetworkLocation(new NetworkUser { Id = 1, Name = kPayload }, PayloadObject())],
                Services = [new ServiceWrapper { Content = PayloadService() }],
                EnforcingGateways = PayloadGateways,
                Violations = [.. PayloadViolations]
            };
        }

        [Test]
        public void RuleDisplayHtml_EncodesPlainRuleFields()
        {
            Rule rule = PayloadRule();
            AssertEncoded(RuleDisplayHtml.DisplayName(rule));
            AssertEncoded(RuleDisplayHtml.DisplayUid(rule));
            AssertEncoded(RuleDisplayHtml.DisplayComment(rule));
            AssertEncoded(RuleDisplayHtml.DisplayAction(rule));
            AssertEncoded(RuleDisplayHtml.DisplayTrack(rule));
            AssertEncoded(RuleDisplayHtml.DisplayName(PayloadObject()));
            AssertEncoded(RuleDisplayHtml.DisplayUid(PayloadService()));
            AssertEncoded(RuleDisplayHtml.DisplayName(new NetworkUser { Name = kPayload }));
        }

        [Test]
        public void RuleDisplayBase_PlainGettersStayUnencodedForCsvAndJson()
        {
            Assert.That(RuleDisplayBase.DisplayComment(PayloadRule()), Is.EqualTo(kPayload));
        }

        [Test]
        public void RuleDisplayBase_EncodesZonesAndViolations()
        {
            Rule rule = PayloadRule();
            AssertEncoded(RuleDisplayBase.DisplaySourceZones(rule));
            AssertEncoded(RuleDisplayBase.DisplayDestinationZones(rule));
            AssertEncoded(RuleDisplayBase.DisplayViolationDetails(rule));
        }

        [TestCase(ReportType.ResolvedRules)]
        [TestCase(ReportType.ResolvedRulesTech)]
        [TestCase(ReportType.Rules)]
        [TestCase(ReportType.VarianceAnalysis)]
        public void RuleDisplayHtml_EncodesNetworkLocationsAndServices(ReportType reportType)
        {
            RuleDisplayHtml display = new(userConfig);
            Rule rule = PayloadRule();
            string destination = display.DisplayDestination(rule, OutputLocation.report, reportType);
            string services = display.DisplayServices(rule, OutputLocation.report, reportType);
            Assert.Multiple(() =>
            {
                Assert.That(destination, Does.Not.Contain(kRawElement));
                Assert.That(services, Does.Not.Contain(kRawElement));
            });
            if (!reportType.IsTechReport())
            {
                AssertEncoded(destination);
                AssertEncoded(services);
            }
        }

        [Test]
        public void RuleDisplayHtml_EncodesGatewaysAndRecertOwner()
        {
            Rule rule = PayloadRule();
            RuleMetadata metadata = new() { RuleRecertification = [new Recertification { FwoOwner = new FwoOwner { Name = kPayload }, IpMatch = kPayload }] };
            AssertEncoded(RuleDisplayHtml.DisplayEnforcingGateways(rule, OutputLocation.report, ReportType.Rules));
            AssertEncoded(RuleDisplayHtml.DisplayEnforcingGateways(rule, OutputLocation.report, ReportType.ResolvedRules));
            AssertEncoded(RuleDisplayHtml.DisplayOwner(metadata));
            AssertEncoded(RuleDisplayHtml.DisplayRecertIpMatches(metadata));
        }

        [TestCase('I')]
        [TestCase('D')]
        [TestCase('C')]
        public void RuleChangeDisplayHtml_EncodesChangedValues(char changeAction)
        {
            RuleChangeDisplayHtml display = new(userConfig);
            Rule changedRule = PayloadRule();
            changedRule.Comment = kPayload + "changed";
            RuleChange ruleChange = new() { ChangeAction = changeAction, OldRule = PayloadRule(), NewRule = changedRule };
            NetworkObject changedObject = PayloadObject();
            changedObject.Uid = kPayload;
            changedObject.Comment = kPayload;
            ObjectChange objectChange = new() { ChangeAction = changeAction, OldObject = new NetworkObject(), NewObject = changedObject };
            ServiceChange serviceChange = new() { ChangeAction = changeAction, OldService = new NetworkService(), NewService = PayloadService() };
            if (changeAction == 'D')
            {
                objectChange.OldObject = changedObject;
                serviceChange.OldService = PayloadService();
            }

            AssertEncoded(display.DisplayName(ruleChange));
            AssertEncoded(display.DisplayComment(ruleChange));
            AssertEncoded(display.DisplayUid(objectChange));
            AssertEncoded(display.DisplayComment(objectChange));
            AssertEncoded(display.DisplayComment(serviceChange));
            AssertEncoded(display.DisplayName(serviceChange));
        }

        [Test]
        public void RuleDifferenceDisplayHtml_EncodesNames()
        {
            RuleDifferenceDisplayHtml display = new(userConfig);
            Rule rule = PayloadRule();
            AssertEncoded(display.DisplaySourceDiff(rule, OutputLocation.report, ReportType.VarianceAnalysis));
            AssertEncoded(display.DisplayServiceDiff(rule, OutputLocation.report, ReportType.VarianceAnalysis));
        }

        [Test]
        public void ModellingObjects_EncodeNamesAndTooltips()
        {
            ModellingAppServer appServer = new() { Name = kPayload, Ip = "10.0.0.1/32", IpEnd = "10.0.0.1/32", InUse = false, TooltipText = kPayload };
            ModellingAppRole appRole = new() { Name = kPayload, IdString = "AR1" };
            ModellingService service = new() { Name = kPayload };
            ModellingServiceGroup serviceGroup = new() { Name = kPayload };
            AssertEncoded(appServer.DisplayWithIcon());
            AssertEncoded(appRole.DisplayWithIcon(true, false));
            AssertEncoded(appRole.DisplayProblematicWithIcon());
            AssertEncoded(service.DisplayWithIcon());
            AssertEncoded(serviceGroup.DisplayHtml());
            Assert.That(appServer.Display(), Does.Contain(kPayload), "plain text display stays unencoded");
        }

        [Test]
        public void ConnectionReport_ListAppServers_EncodesNames()
        {
            List<ModellingAppServer> appServers = [new ModellingAppServer { Name = kPayload, Ip = "10.0.0.1/32", IpEnd = "10.0.0.1/32" }];
            AssertEncoded(ConnectionReport.ListAppServers(appServers, NoSurplusAppServers));
            AssertEncoded(ConnectionReport.ListAppServers(appServers, appServers, true));
        }

        [Test]
        public void ModellingHandlerBase_DisplayApp_EncodesOwner()
        {
            FwoOwner owner = new() { Name = kPayload, ExtAppId = kPayload, Active = true, ConnectionCount = new AggregateCount { Aggregate = new Aggregate { Count = 1 } } };
            AssertEncoded(ModellingHandlerBase.DisplayApp(userConfig, owner));
        }

        [Test]
        public void DisplayBase_DisplayServiceDetails_MatchesDisplayService()
        {
            NetworkService service = new() { Name = "https", DestinationPort = 443, Protocol = new NetworkProtocol { Id = 6, Name = "tcp" } };
            Assert.Multiple(() =>
            {
                Assert.That(DisplayBase.DisplayServiceDetails(service), Is.EqualTo(" (443/tcp)"));
                Assert.That(DisplayBase.DisplayService(service, false).ToString(), Is.EqualTo("https (443/tcp)"));
            });
        }
    }
}
