using System.Text.Json.Nodes;
using FWO.Basics;
using FWO.Data.DeviceSpecificSettings;
using FWO.Data.Workflow;
using FWO.ExternalSystems.FirewallWrite;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Guards the planning of Guardicore rules: one rule per protocol of a connection with only the ports of that protocol,
    /// sides of labels and CIDR subnets, ruleset and comment from the modelled connection, and the step that deletes the
    /// other rules of the connection.
    /// </summary>
    [TestFixture]
    [Parallelizable]
    internal class LabelRuleStepPlannerTest
    {
        private const string kMarker = "FWOC12";
        private const string kAppId = "APP-1234";
        private const int kTcp = 6;
        private const int kUdp = 17;
        private const int kIcmp = 1;
        private const int kEsp = 50;

        private static readonly List<string> kProtocolOrder = ["ICMP", "TCP", "UDP"];
        private static readonly List<string> kRangeBlocks = ["10.0.0.0/28", "10.0.0.16/31"];
        private static readonly List<string> kHostBlock = ["10.0.0.5/32"];
        private static readonly List<string> kIpv6Blocks = ["2001:db8::/127", "2001:db8::2/128"];

        private static FirewallWriteContext Context(string? marker = kMarker, string? appId = kAppId) =>
            FirewallWriteTestSupport.Context(FirewallVendors.kGuardicoreManagement) with { ConnectionMarker = marker, ExtAppId = appId };

        private static WfImplElement Label(string name, ElemFieldType field) => new() { GroupName = name, Field = field.ToString(), ImplAction = nameof(RequestAction.create) };

        private static WfImplElement Service(int protoId, int? port, int? portEnd = null) => FirewallWriteTestSupport.Service($"svc{protoId}-{port}", protoId, port ?? 0, portEnd);

        private static WfImplTask AccessTask(params WfImplElement[] services)
        {
            List<WfImplElement> elements =
            [
                Label("AppRole: AR504711-001", ElemFieldType.source),
                FirewallWriteTestSupport.Network("range", "10.0.0.0", "10.0.0.17", ElemFieldType.destination),
                .. services
            ];
            return FirewallWriteTestSupport.Task(WfTaskType.access, [.. elements]);
        }

        private static List<FirewallWriteStep> Plan(WfImplTask task, FirewallWriteContext? context = null) =>
            LabelRuleStepPlanner.Plan(task, context ?? Context(), FirewallVendorProfile.For(FirewallVendor.Guardicore));

        private static JsonNode Json(FirewallWriteStep step, string placeholder) => JsonNode.Parse(step.Values[placeholder].Value)!;

        private static string Text(FirewallWriteStep step, string placeholder) => step.Values[placeholder].Value;

        [Test]
        public void Access_OneRulePerProtocolWithItsPortsOnly()
        {
            List<FirewallWriteStep> steps = Plan(AccessTask(Service(kTcp, 443), Service(kTcp, 8000, 8080), Service(kUdp, 53), Service(kIcmp, null)));

            List<FirewallWriteStep> rules = [.. steps.Where(step => step.Operation == FirewallWriteOperation.RuleCreate)];
            FirewallWriteStep tcp = rules.Single(step => Text(step, FirewallWritePlaceholders.RuleProtocol) == "TCP");
            FirewallWriteStep udp = rules.Single(step => Text(step, FirewallWritePlaceholders.RuleProtocol) == "UDP");
            FirewallWriteStep icmp = rules.Single(step => Text(step, FirewallWritePlaceholders.RuleProtocol) == "ICMP");
            Assert.Multiple(() =>
            {
                Assert.That(rules.Select(step => Text(step, FirewallWritePlaceholders.RuleProtocol)), Is.EqualTo(kProtocolOrder));
                Assert.That(Json(tcp, FirewallWritePlaceholders.RulePorts).ToJsonString(), Is.EqualTo("[443]"));
                Assert.That(Json(tcp, FirewallWritePlaceholders.RulePortRanges).ToJsonString(), Is.EqualTo("""[{"start":8000,"end":8080}]"""));
                Assert.That(Json(udp, FirewallWritePlaceholders.RulePorts).ToJsonString(), Is.EqualTo("[53]"));
                Assert.That(Json(udp, FirewallWritePlaceholders.RulePortRanges).ToJsonString(), Is.EqualTo("[]"));
                Assert.That(Json(icmp, FirewallWritePlaceholders.RulePorts).ToJsonString(), Is.EqualTo("[]"));
                Assert.That(rules.TrueForAll(step => Text(step, FirewallWritePlaceholders.RuleName) == kAppId), Is.True, "the ruleset is the app");
                Assert.That(rules.TrueForAll(step => Text(step, FirewallWritePlaceholders.RuleComment) == kMarker), Is.True);
                Assert.That(rules.TrueForAll(step => Text(step, FirewallWritePlaceholders.RuleAction) == "ALLOW"), Is.True);
                Assert.That(steps[^1].Operation, Is.EqualTo(FirewallWriteOperation.RuleDelete));
                Assert.That(Text(steps[^1], FirewallWritePlaceholders.RuleProtocol), Is.EqualTo("ICMP,TCP,UDP"));
                Assert.That(Text(steps[^1], FirewallWritePlaceholders.RuleComment), Is.EqualTo(kMarker));
            });
        }

        [Test]
        public void Access_SidesHoldLabelsAndCidrSubnets()
        {
            FirewallWriteStep rule = Plan(AccessTask(Service(kTcp, 443)))[0];

            JsonNode sources = Json(rule, FirewallWritePlaceholders.RuleSources);
            JsonNode destinations = Json(rule, FirewallWritePlaceholders.RuleDestinations);
            Assert.Multiple(() =>
            {
                Assert.That(sources.ToJsonString(), Is.EqualTo("""{"labels":{"or_labels":[{"and_labels":["AppRole: AR504711-001"]}]}}"""));
                Assert.That(destinations["subnets"]!.AsArray().Select(subnet => subnet!.GetValue<string>()), Is.EqualTo(kRangeBlocks));
                Assert.That(destinations["labels"], Is.Null);
            });
        }

        [Test]
        public void ServiceWithoutPort_AllowsAllPortsOfItsProtocol()
        {
            FirewallWriteStep rule = Plan(AccessTask(Service(kTcp, 443), Service(kTcp, null)))[0];

            Assert.That(Json(rule, FirewallWritePlaceholders.RulePorts).ToJsonString(), Is.EqualTo("[]"));
        }

        [Test]
        public void DeletedElements_AreNotWritten()
        {
            WfImplElement removed = Service(kUdp, 53);
            removed.ImplAction = nameof(RequestAction.delete);

            List<FirewallWriteStep> steps = Plan(AccessTask(Service(kTcp, 443), removed));

            Assert.That(steps.Count(step => step.Operation == FirewallWriteOperation.RuleCreate), Is.EqualTo(1));
        }

        [Test]
        public void RuleDelete_OnlyDeletesAllRulesOfTheConnection()
        {
            List<FirewallWriteStep> steps = Plan(FirewallWriteTestSupport.Task(WfTaskType.rule_delete, FirewallWriteTestSupport.Rule("rule-1")));

            Assert.Multiple(() =>
            {
                Assert.That(steps.Single().Operation, Is.EqualTo(FirewallWriteOperation.RuleDelete));
                Assert.That(Text(steps.Single(), FirewallWritePlaceholders.RuleProtocol), Is.Empty);
                Assert.That(steps.Single().TargetName, Does.Contain("all rules"));
            });
        }

        [Test]
        public void InvalidTasks_AreRefused()
        {
            WfImplTask noSource = FirewallWriteTestSupport.Task(WfTaskType.access, Label("AppRole: AR1", ElemFieldType.destination), Service(kTcp, 443));
            WfImplTask groupWithoutLabel = AccessTask(Service(kTcp, 443));
            groupWithoutLabel.ImplElements[0].GroupName = "AR504711-001";
            Assert.Multiple(() =>
            {
                Assert.That(Kind(() => Plan(AccessTask(Service(kTcp, 443)), Context(marker: null))), Is.EqualTo(FirewallWriteErrorKind.InvalidTaskData));
                Assert.That(Kind(() => Plan(AccessTask(Service(kTcp, 443)), Context(appId: null))), Is.EqualTo(FirewallWriteErrorKind.InvalidTaskData));
                Assert.That(Kind(() => Plan(AccessTask(Service(kEsp, null)))), Is.EqualTo(FirewallWriteErrorKind.Unsupported));
                Assert.That(Kind(() => Plan(AccessTask())), Is.EqualTo(FirewallWriteErrorKind.InvalidTaskData), "no service");
                Assert.That(Kind(() => Plan(noSource)), Is.EqualTo(FirewallWriteErrorKind.InvalidTaskData), "a side is not widened to any");
                Assert.That(Kind(() => Plan(groupWithoutLabel)), Is.EqualTo(FirewallWriteErrorKind.InvalidTaskData));
            });
        }

        [Test]
        public void CidrBlocks_CoverIntervalsExactly()
        {
            Assert.Multiple(() =>
            {
                Assert.That(LabelRuleStepPlanner.CidrBlocks(Interval("10.0.0.0", "10.0.0.17")), Is.EqualTo(kRangeBlocks));
                Assert.That(LabelRuleStepPlanner.CidrBlocks(Interval("10.0.0.5", "10.0.0.5")), Is.EqualTo(kHostBlock));
                Assert.That(LabelRuleStepPlanner.CidrBlocks(Interval("2001:db8::", "2001:db8::2")), Is.EqualTo(kIpv6Blocks));
                Assert.That(LabelRuleStepPlanner.CidrBlocks(Interval("0.0.0.0", "255.255.255.255")).Single(), Is.EqualTo("0.0.0.0/0"));
            });
        }

        private static IpInterval Interval(string start, string end)
        {
            Assert.That(IpAddressSet.TryParseInterval(start, end, out IpInterval interval), Is.True);
            return interval;
        }

        private static FirewallWriteErrorKind? Kind(Action plan)
        {
            try
            {
                plan();
                return null;
            }
            catch (FirewallWriteException exception)
            {
                return exception.Error.Kind;
            }
        }
    }
}
