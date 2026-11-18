using FWO.Basics;
using FWO.ExternalSystems.FirewallWrite;
using NUnit.Framework;
using System.Text.Json.Nodes;

namespace FWO.Test
{
    /// <summary>
    /// IP criteria of Guardicore labels: one criterion per member for a new label, and the smallest change of the IP
    /// criteria of an existing label, leaving read-only and other criteria alone.
    /// </summary>
    [TestFixture]
    internal class GuardicoreLabelCriteriaTest
    {
        private const string kIssueExampleCriteria =
            """[{"field":"numeric_ip_addresses","op":"SUBNET","argument":"1.2.3.4/32"},""" +
            """{"field":"numeric_ip_addresses","op":"SUBNET","argument":"2.3.4.0/24"},""" +
            """{"field":"numeric_ip_addresses","op":"RANGE","argument":"1.1.1.0-1.1.1.17"}]""";

        private static readonly List<string> kIssueExampleMembers = ["1.2.3.4", "2.3.4.0/24", "1.1.1.0-1.1.1.17"];
        private static readonly List<string> kCoveredMember = ["10.0.0.0/24", "10.0.0.5"];
        private static readonly List<IpInterval> kNone = [];

        private static readonly GuardicoreCriterion Host1 = new(GuardicoreLabelCriteria.kIpField, GuardicoreLabelCriteria.kSubnetOp, "10.0.0.1/32");
        private static readonly GuardicoreCriterion Range1 = new(GuardicoreLabelCriteria.kIpField, GuardicoreLabelCriteria.kRangeOp, "10.0.1.0-10.0.1.17");
        private static readonly GuardicoreCriterion Equals1 = new(GuardicoreLabelCriteria.kIpField, "EQUALS", "10.0.2.1");
        private static readonly List<GuardicoreCriterion> kCurrent =
        [
            Host1, Range1, Equals1,
            new("os_info", "EQUALS", "Linux"),
            new(GuardicoreLabelCriteria.kIpField, GuardicoreLabelCriteria.kSubnetOp, "172.16.0.0/16", ReadOnly: true)
        ];
        private static readonly List<GuardicoreCriterion> kWithAddedNetwork =
        [
            Host1, Range1, Equals1, new(GuardicoreLabelCriteria.kIpField, GuardicoreLabelCriteria.kSubnetOp, "10.0.3.0/24")
        ];
        private static readonly List<GuardicoreCriterion> kHostAndRestOfRange =
        [
            Host1, new(GuardicoreLabelCriteria.kIpField, GuardicoreLabelCriteria.kSubnetOp, "10.0.1.0/28")
        ];
        private static readonly List<GuardicoreCriterion> kHostAndThreeNines =
        [
            Host1, new(GuardicoreLabelCriteria.kIpField, GuardicoreLabelCriteria.kSubnetOp, "10.9.9.9/32")
        ];
        private const string kLabelJson = """
            {"id":"lab-1","dynamic_criteria":[
              {"id":"c1","field":"numeric_ip_addresses","op":"SUBNET","argument":"10.0.0.1/32","source":"User"},
              {"id":"c2","field":"os_info","op":"EQUALS","argument":"Linux"},
              {"id":"c3","compound_criteria":[{"field":"name","op":"STARTSWITH","argument":"web"}]},
              {"id":"c4","field":"numeric_ip_addresses","op":"SUBNET","argument":"172.16.0.0/16","read_only":true,"source":"Worksite"}]}
            """;
        private const string kUpdateJson =
            """[{"key":"AppRole","value":"AR1","criteria":[{"field":"os_info","op":"EQUALS","argument":"Linux"},""" +
            """{"compound_criteria":[{"field":"name","op":"STARTSWITH","argument":"web"}]},""" +
            """{"field":"numeric_ip_addresses","op":"SUBNET","argument":"172.16.0.0/16"},""" +
            """{"field":"numeric_ip_addresses","op":"SUBNET","argument":"10.9.9.9/32"}]}]""";

        private static List<IpInterval> Entries(params string[] entries) => GuardicoreLabelCriteria.ParseEntries(entries);

        [Test]
        public void CreationJson_WritesOneCriterionPerMemberAsInTheIssue()
        {
            Assert.That(GuardicoreLabelCriteria.CreationJson(GuardicoreLabelCriteria.ParseEntries(kIssueExampleMembers)), Is.EqualTo(kIssueExampleCriteria));
        }

        [Test]
        public void CreationEntries_LeaveOutMembersCoveredByEarlierOnes()
        {
            Assert.That(GuardicoreLabelCriteria.CreationEntries(GuardicoreLabelCriteria.ParseEntries(kCoveredMember)), Has.Count.EqualTo(1));
        }

        [Test]
        public void ParseEntries_RefusesNoAddresses()
        {
            FirewallWriteException? exception = Assert.Throws<FirewallWriteException>(() => Entries("not an address"));
            Assert.That(exception!.Error.Kind, Is.EqualTo(FirewallWriteErrorKind.InvalidTaskData));
        }

        [Test]
        public void TargetIpCriteria_AreNullIfTheLabelHasTheTargetAlready()
        {
            List<IpInterval> sameAddressesOtherNotation = Entries("10.0.0.1", "10.0.1.0/28", "10.0.1.16/31", "10.0.2.1");
            Assert.Multiple(() =>
            {
                Assert.That(GuardicoreLabelCriteria.TargetIpCriteria(kCurrent, sameAddressesOtherNotation, kNone, true), Is.Null);
                Assert.That(GuardicoreLabelCriteria.TargetIpCriteria(kCurrent, Entries("10.0.0.1"), kNone, false), Is.Null);
            });
        }

        [Test]
        public void TargetIpCriteria_AddMissingMembersToTheCurrentCriteria()
        {
            List<GuardicoreCriterion>? target = GuardicoreLabelCriteria.TargetIpCriteria(kCurrent, Entries("10.0.0.1", "10.0.3.0/24"), kNone, false);
            Assert.That(target, Is.EqualTo(kWithAddedNetwork));
        }

        [Test]
        public void TargetIpCriteria_RemoveMembersAndKeepTheRestOfAPartlyRemovedCriterion()
        {
            List<GuardicoreCriterion>? target = GuardicoreLabelCriteria.TargetIpCriteria(kCurrent, kNone, Entries("10.0.1.16/31", "10.0.2.1"), false);
            Assert.That(target, Is.EqualTo(kHostAndRestOfRange));
        }

        [Test]
        public void TargetIpCriteria_ReplaceGivesExactlyTheMembers()
        {
            List<GuardicoreCriterion>? target = GuardicoreLabelCriteria.TargetIpCriteria(kCurrent, Entries("10.0.0.1", "10.9.9.9"), kNone, true);
            Assert.That(target, Is.EqualTo(kHostAndThreeNines));
        }

        [Test]
        public void UpdateJson_WritesBackTheCriteriaFwoDoesNotManage()
        {
            string update = GuardicoreLabelCriteria.UpdateJson("AppRole", "AR1", JsonNode.Parse(kLabelJson)!, kHostAndThreeNines.Skip(1));
            Assert.That(JsonNode.DeepEquals(JsonNode.Parse(update), JsonNode.Parse(kUpdateJson)), Is.True, update);
        }

        [Test]
        public void ReadCriteria_ReadsReadOnlyFlags()
        {
            JsonNode label = JsonNode.Parse("""{"dynamic_criteria":[{"id":12,"field":"numeric_ip_addresses","op":"SUBNET","argument":"10.0.0.0/8","read_only":true}]}""")!;
            List<GuardicoreCriterion> criteria = GuardicoreLabelCriteria.ReadCriteria(label);
            Assert.Multiple(() =>
            {
                Assert.That(criteria, Has.Count.EqualTo(1));
                Assert.That(criteria[0], Is.EqualTo(new GuardicoreCriterion(GuardicoreLabelCriteria.kIpField, GuardicoreLabelCriteria.kSubnetOp, "10.0.0.0/8", true)));
                Assert.That(GuardicoreLabelCriteria.ReadCriteria(JsonNode.Parse("{}")), Is.Empty);
            });
        }
    }
}
