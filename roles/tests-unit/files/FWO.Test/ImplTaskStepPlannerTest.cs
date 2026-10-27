using FWO.Basics.Exceptions;
using FWO.Data.DeviceSpecificSettings;
using FWO.Data.Workflow;
using FWO.ExternalSystems.FirewallWrite;
using NUnit.Framework;
using static FWO.Test.FirewallWriteTestSupport;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class ImplTaskStepPlannerTest
    {
        private const int kTcp = 6;
        private const int kUdp = 17;
        private static readonly List<FirewallWriteOperation> kObjectOperations =
        [
            FirewallWriteOperation.HostCreate, FirewallWriteOperation.NetworkCreate, FirewallWriteOperation.AddressRangeCreate,
            FirewallWriteOperation.ServiceCreate
        ];
        private static readonly List<FirewallWriteOperation> kGroupCreateOperations = [FirewallWriteOperation.HostCreate, FirewallWriteOperation.GroupCreate];
        private static readonly List<FirewallWriteOperation> kGroupModifyOperations =
            [FirewallWriteOperation.HostCreate, FirewallWriteOperation.GroupAddMembers, FirewallWriteOperation.GroupRemoveMembers];
        private static readonly List<FirewallWriteOperation> kRuleDeleteOperations = [FirewallWriteOperation.RuleDelete, FirewallWriteOperation.RuleDelete];
        private static readonly List<string> kNewAndExisting = ["new_host", "existing"];
        private static readonly List<string> kNewHost = ["new_host"];
        private static readonly List<string> kOldHost = ["old"];
        private static readonly List<string> kMergedMembers = ["b", "c"];

        private static string Value(FirewallWriteStep step, string placeholder) => step.Values[placeholder].Value;

        private static List<FirewallWriteStep> Plan(WfImplTask task, int deviceTypeId = FirewallVendors.kCheckPointR8x, FirewallRulePlacement? placement = null) =>
            ImplTaskStepPlanner.Plan(task, Context(deviceTypeId), placement);

        [Test]
        public void ObjectCreate_PlansOneStepPerElementByObjectType()
        {
            WfImplTask task = Task(WfTaskType.object_create,
                Network("h1", "10.0.0.1/32"),
                Network("n1", "10.1.0.0/24"),
                Network("r1", "10.2.0.1/32", "10.2.0.10/32"),
                Service("svc", kTcp, 80, 90));

            List<FirewallWriteStep> steps = Plan(task);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(steps.Select(step => step.Operation), Is.EqualTo(kObjectOperations));
                Assert.That(Value(steps[0], FirewallWritePlaceholders.Ip), Is.EqualTo("10.0.0.1"));
                Assert.That(Value(steps[0], FirewallWritePlaceholders.Netmask), Is.EqualTo("255.255.255.255"));
                Assert.That(Value(steps[1], FirewallWritePlaceholders.Subnet), Is.EqualTo("10.1.0.0"));
                Assert.That(Value(steps[1], FirewallWritePlaceholders.MaskLength), Is.EqualTo("24"));
                Assert.That(Value(steps[1], FirewallWritePlaceholders.Netmask), Is.EqualTo("255.255.255.0"));
                Assert.That(Value(steps[1], FirewallWritePlaceholders.IpCidr), Is.EqualTo("10.1.0.0/24"));
                Assert.That(Value(steps[2], FirewallWritePlaceholders.IpEnd), Is.EqualTo("10.2.0.10"));
                Assert.That(Value(steps[2], FirewallWritePlaceholders.ObjectAddresses), Is.EqualTo("[\"10.2.0.1-10.2.0.10\"]"));
                Assert.That(Value(steps[3], FirewallWritePlaceholders.Protocol), Is.EqualTo("tcp"));
                Assert.That(Value(steps[3], FirewallWritePlaceholders.PortRange), Is.EqualTo("80-90"));
                Assert.That(Value(steps[3], FirewallWritePlaceholders.Comment), Is.EqualTo("comment"));
                Assert.That(Value(steps[0], FirewallWritePlaceholders.ManagementName), Is.EqualTo("mgmt"));
                Assert.That(Value(steps[0], FirewallWritePlaceholders.TaskId), Is.EqualTo("7"));
            }
        }

        [Test]
        public void ObjectModify_UsesElementActionsAndSkipsUnchanged()
        {
            WfImplTask task = Task(WfTaskType.object_modify,
                Network("h1", "10.0.0.1", action: RequestAction.modify),
                Network("gone", null, action: RequestAction.delete),
                Network("same", "10.0.0.3", action: RequestAction.unchanged),
                Service(null, kUdp, 53, action: RequestAction.delete));

            List<FirewallWriteStep> steps = Plan(task);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(steps, Has.Count.EqualTo(3));
                Assert.That(steps[0].Operation, Is.EqualTo(FirewallWriteOperation.HostModify));
                Assert.That(steps[1].Operation, Is.EqualTo(FirewallWriteOperation.HostDelete));
                Assert.That(steps[2].Operation, Is.EqualTo(FirewallWriteOperation.ServiceDelete));
                Assert.That(Value(steps[2], FirewallWritePlaceholders.ServiceName), Is.EqualTo("udp_53"));
            }
        }

        [Test]
        public void ObjectCreate_FailsForObjectWithoutAddress()
        {
            Assert.Throws<ConfigException>(() => Plan(Task(WfTaskType.object_create, Network("h1", null))));
        }

        [Test]
        public void CheckPointNames_ReplaceSlash()
        {
            List<FirewallWriteStep> steps = Plan(Task(WfTaskType.object_create, Network("net/24", "10.1.0.0/24")));
            Assert.That(Value(steps[0], FirewallWritePlaceholders.ObjectName), Is.EqualTo("net_24"));
        }

        [Test]
        public void GroupCreate_CreatesNewMembersFirst()
        {
            WfImplElement newHost = Network("new_host", "10.0.0.1");
            newHost.GroupName = "grp";
            WfImplElement existing = Network("existing", null, action: RequestAction.unchanged);
            existing.NetworkId = 5;

            List<FirewallWriteStep> steps = Plan(Task(WfTaskType.group_create, newHost, existing));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(steps.Select(step => step.Operation), Is.EqualTo(kGroupCreateOperations));
                Assert.That(Value(steps[1], FirewallWritePlaceholders.GroupName), Is.EqualTo("grp"));
                Assert.That(Value(steps[1], FirewallWritePlaceholders.Members), Is.EqualTo("[\"new_host\",\"existing\"]"));
                Assert.That(Value(steps[1], FirewallWritePlaceholders.MembersNameObjects), Is.EqualTo("[{\"name\":\"new_host\"},{\"name\":\"existing\"}]"));
                Assert.That(Value(steps[1], FirewallWritePlaceholders.MembersLines), Is.EqualTo("new_host\nexisting"));
                Assert.That(steps[1].MembersToAdd, Is.EqualTo(kNewAndExisting));
            }
        }

        [Test]
        public void GroupModify_AddsAndRemovesMembers()
        {
            WfImplElement add = Network("new_host", "10.0.0.1");
            WfImplElement remove = Network("old", null, action: RequestAction.delete);
            WfImplElement keep = Network("kept", null, action: RequestAction.unchanged);
            WfImplTask task = Task(WfTaskType.group_modify, add, remove, keep);
            task.Title = "group-title";

            List<FirewallWriteStep> steps = Plan(task);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(steps.Select(step => step.Operation), Is.EqualTo(kGroupModifyOperations));
                Assert.That(Value(steps[1], FirewallWritePlaceholders.GroupName), Is.EqualTo("group-title"));
                Assert.That(Value(steps[1], FirewallWritePlaceholders.MembersToAdd), Is.EqualTo("[\"new_host\"]"));
                Assert.That(Value(steps[2], FirewallWritePlaceholders.MembersToRemove), Is.EqualTo("[\"old\"]"));
                Assert.That(steps[1].MembersToAdd, Is.EqualTo(kNewHost));
                Assert.That(steps[2].MembersToRemove, Is.EqualTo(kOldHost));
            }
        }

        [Test]
        public void GroupDelete_PlansOneStep()
        {
            WfImplTask task = Task(WfTaskType.group_delete);
            List<FirewallWriteStep> steps = Plan(task);
            Assert.That(steps.Single().Operation, Is.EqualTo(FirewallWriteOperation.GroupDelete));
        }

        [Test]
        public void Groups_RejectServiceMembersAndMissingNames()
        {
            WfImplTask withService = Task(WfTaskType.group_create, Service("svc", kTcp, 80));
            WfImplTask withoutName = Task(WfTaskType.group_create, Network(null, "10.0.0.1"));
            withoutName.Title = "";
            WfImplTask unnamedMember = Task(WfTaskType.group_create, Network(null, null, action: RequestAction.unchanged));
            using (Assert.EnterMultipleScope())
            {
                Assert.Throws<ConfigException>(() => Plan(withService));
                Assert.Throws<ConfigException>(() => Plan(withoutName));
                Assert.Throws<ConfigException>(() => Plan(unnamedMember));
            }
        }

        [Test]
        public void Access_RendersRuleValuesAndPosition()
        {
            WfImplElement source = Network("src", null, action: RequestAction.unchanged);
            source.NetworkId = 1;
            WfImplTask task = Task(WfTaskType.access, source, Network("dst", "10.9.9.9", field: ElemFieldType.destination), Service("https", kTcp, 443));
            task.RuleAction = 2;
            task.Tracking = 2;

            List<FirewallWriteStep> steps = Plan(task, placement: new FirewallRulePlacement(FirewallRuleAnchor.Above, "uid-9", "layer-uid"));
            FirewallWriteStep rule = steps[^1];

            using (Assert.EnterMultipleScope())
            {
                Assert.That(steps[0].Operation, Is.EqualTo(FirewallWriteOperation.HostCreate));
                Assert.That(rule.Operation, Is.EqualTo(FirewallWriteOperation.RuleCreate));
                Assert.That(Value(rule, FirewallWritePlaceholders.RuleSources), Is.EqualTo("[\"src\"]"));
                Assert.That(Value(rule, FirewallWritePlaceholders.RuleDestinations), Is.EqualTo("[\"dst\"]"));
                Assert.That(Value(rule, FirewallWritePlaceholders.RuleServices), Is.EqualTo("[\"https\"]"));
                Assert.That(Value(rule, FirewallWritePlaceholders.RuleAction), Is.EqualTo("Drop"));
                Assert.That(Value(rule, FirewallWritePlaceholders.RuleTracking), Is.EqualTo("None"));
                Assert.That(Value(rule, FirewallWritePlaceholders.RulePosition), Is.EqualTo("{\"above\":\"uid-9\"}"));
                Assert.That(Value(rule, FirewallWritePlaceholders.LayerName), Is.EqualTo("layer-uid"));
                Assert.That(Value(rule, FirewallWritePlaceholders.RuleProtocol), Is.EqualTo("TCP"));
            }
        }

        [Test]
        public void Access_WithoutElementsUsesVendorAnyAndEndOfRulebase()
        {
            FirewallWriteStep rule = Plan(Task(WfTaskType.access), FirewallVendors.kFortiOsGateway).Single();
            using (Assert.EnterMultipleScope())
            {
                Assert.That(Value(rule, FirewallWritePlaceholders.RuleSourcesNameObjects), Is.EqualTo("[{\"name\":\"all\"}]"));
                Assert.That(Value(rule, FirewallWritePlaceholders.RuleServices), Is.EqualTo("[\"ALL\"]"));
                Assert.That(Value(rule, FirewallWritePlaceholders.RuleAction), Is.EqualTo("accept"));
                Assert.That(Value(rule, FirewallWritePlaceholders.RuleTracking), Is.EqualTo("all"));
                Assert.That(Value(rule, FirewallWritePlaceholders.RulePosition), Is.EqualTo("\"bottom\""));
                Assert.That(Value(rule, FirewallWritePlaceholders.LayerName), Is.EqualTo("Network"));
                Assert.That(Value(rule, FirewallWritePlaceholders.RuleServicesCsv), Is.Empty);
            }
        }

        [Test]
        public void Access_FailsForRuleElementWithoutName()
        {
            Assert.Throws<ConfigException>(() => Plan(Task(WfTaskType.access, Network(null, "10.0.0.1", action: RequestAction.unchanged))));
        }

        [Test]
        public void Azure_UsesAddressesAndCreatesNoObjects()
        {
            WfImplTask task = Task(WfTaskType.access, Network(null, "10.0.0.0/24"), Network("d", "10.0.1.1", field: ElemFieldType.destination),
                Service(null, kTcp, 443));

            List<FirewallWriteStep> steps = Plan(task, FirewallVendors.kAzureFirewall);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(steps.Single().Operation, Is.EqualTo(FirewallWriteOperation.RuleCreate));
                Assert.That(Value(steps[0], FirewallWritePlaceholders.RuleSourceAddresses), Is.EqualTo("[\"10.0.0.0/24\"]"));
                Assert.That(Value(steps[0], FirewallWritePlaceholders.RuleDestinationAddresses), Is.EqualTo("[\"10.0.1.1/32\"]"));
                Assert.That(Value(steps[0], FirewallWritePlaceholders.RuleServicePorts), Is.EqualTo("[\"443\"]"));
                Assert.That(Value(steps[0], FirewallWritePlaceholders.RuleAction), Is.EqualTo("Allow"));
            }
        }

        [Test]
        public void AzureGroup_UsesMemberAddresses()
        {
            WfImplElement member = Network(null, "10.0.0.5");
            member.GroupName = "ipg";
            FirewallWriteStep group = Plan(Task(WfTaskType.group_create, member), FirewallVendors.kAzure).Single();
            using (Assert.EnterMultipleScope())
            {
                Assert.That(Value(group, FirewallWritePlaceholders.MemberAddresses), Is.EqualTo("[\"10.0.0.5/32\"]"));
                Assert.That(group.MembersToAdd.Single(), Is.EqualTo("10.0.0.5/32"));
            }
        }

        [Test]
        public void RuleModify_SplitsAddedAndRemovedElements()
        {
            WfImplTask task = Task(WfTaskType.rule_modify, Rule("uid-1"), Network("add", null), Network("drop", null, action: RequestAction.delete),
                Network("keep", null, field: ElemFieldType.destination, action: RequestAction.unchanged));

            FirewallWriteStep rule = Plan(task).Single();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(rule.Operation, Is.EqualTo(FirewallWriteOperation.RuleModify));
                Assert.That(Value(rule, FirewallWritePlaceholders.RuleUid), Is.EqualTo("uid-1"));
                Assert.That(Value(rule, FirewallWritePlaceholders.RuleSources), Is.EqualTo("[\"add\"]"));
                Assert.That(Value(rule, FirewallWritePlaceholders.RuleSourcesToAdd), Is.EqualTo("[\"add\"]"));
                Assert.That(Value(rule, FirewallWritePlaceholders.RuleSourcesToRemove), Is.EqualTo("[\"drop\"]"));
                Assert.That(Value(rule, FirewallWritePlaceholders.RuleDestinationsToAdd), Is.EqualTo("[]"));
                Assert.That(Value(rule, FirewallWritePlaceholders.RuleSourcesCsv), Is.EqualTo("add"));
            }
        }

        [Test]
        public void RuleTasks_NeedRuleUids()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.Throws<ConfigException>(() => Plan(Task(WfTaskType.rule_modify)));
                Assert.Throws<ConfigException>(() => Plan(Task(WfTaskType.rule_delete)));
            }
        }

        [Test]
        public void RuleDelete_PlansOneStepPerRule()
        {
            List<FirewallWriteStep> steps = Plan(Task(WfTaskType.rule_delete, Rule("a"), Rule("b"), Rule("a")));
            using (Assert.EnterMultipleScope())
            {
                Assert.That(steps.Select(step => step.Operation), Is.EqualTo(kRuleDeleteOperations));
                Assert.That(Value(steps[1], FirewallWritePlaceholders.RuleUid), Is.EqualTo("b"));
            }
        }

        [Test]
        public void UnsupportedTaskType_Fails()
        {
            Assert.Throws<ConfigException>(() => Plan(Task(WfTaskType.generic)));
        }

        [Test]
        public void FortiManagerContext_SplitsDeviceAndVdom()
        {
            FirewallWriteStep step = Plan(Task(WfTaskType.group_delete), FirewallVendors.kFortiAdom5ff).Single();
            using (Assert.EnterMultipleScope())
            {
                Assert.That(Value(step, FirewallWritePlaceholders.DeviceName), Is.EqualTo("gw1"));
                Assert.That(Value(step, FirewallWritePlaceholders.VdomName), Is.EqualTo("vd1"));
                Assert.That(Value(step, FirewallWritePlaceholders.PolicyName), Is.EqualTo("Standard"));
            }
        }

        [TestCase("gw", "gw", "root")]
        [TestCase("gw_", "gw_", "root")]
        [TestCase("a_b_c", "a_b", "c")]
        public void SplitDeviceAndVdom(string gatewayName, string device, string vdom)
        {
            Assert.That(FirewallVendorProfile.SplitDeviceAndVdom(gatewayName), Is.EqualTo((device, vdom)));
        }

        [Test]
        public void Placement_RendersAllAnchors()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(new FirewallRulePlacement(FirewallRuleAnchor.Top).ToJson(), Is.EqualTo("\"top\""));
                Assert.That(new FirewallRulePlacement(FirewallRuleAnchor.Below, "u").ToJson(), Is.EqualTo("{\"below\":\"u\"}"));
                Assert.Throws<InvalidOperationException>(() => new FirewallRulePlacement(FirewallRuleAnchor.Above).ToJson());
                Assert.That(new EndOfRulebasePlacementProvider().GetPlacement(new WfImplTask()), Is.Null);
            }
        }

        [Test]
        public void MemberMerge_KeepsOrderAndIgnoresCase()
        {
            FirewallWriteStep step = new(FirewallWriteOperation.GroupAddMembers);
            step.MembersToAdd.Add("B");
            step.MembersToAdd.Add("c");
            step.MembersToRemove.Add("A");
            List<string> current = ["a", "b"];
            Assert.That(FirewallMemberMerge.Merge(current, step), Is.EqualTo(kMergedMembers));
        }
    }
}
