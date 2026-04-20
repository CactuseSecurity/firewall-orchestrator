using FWO.Basics;
using FWO.Services.Triviality;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class RuleBidirectionalDuplicateIndexTest
    {
        [Test]
        public void HasReverseDuplicate_ShouldReturnTrueForReverseRule()
        {
            var rule = TrivialityTestHelper.CreateRule(
                [TrivialityTestHelper.CreateNetworkLocation(TrivialityTestHelper.CreateNetworkObject("Source", "10.1.2.3/32", "10.1.2.3/32"))],
                [TrivialityTestHelper.CreateNetworkLocation(TrivialityTestHelper.CreateNetworkObject("Destination", "10.9.8.7/32", "10.9.8.7/32"))],
                [TrivialityTestHelper.CreateProtocolService("Https", 6, 0, 0, 443, 443)],
                id: 1001);

            var reverseRule = TrivialityTestHelper.CreateRule(
                [TrivialityTestHelper.CreateNetworkLocation(TrivialityTestHelper.CreateNetworkObject("DestinationAlias", "10.9.8.7/32", "10.9.8.7/32"))],
                [TrivialityTestHelper.CreateNetworkLocation(TrivialityTestHelper.CreateNetworkObject("SourceAlias", "10.1.2.3/32", "10.1.2.3/32"))],
                [TrivialityTestHelper.CreateProtocolService("HttpsAlias", 6, 443, 443, 0, 0)],
                id: 1002);

            RuleBidirectionalDuplicateIndex duplicateIndex = new([rule, reverseRule]);

            ClassicAssert.IsTrue(duplicateIndex.HasReverseDuplicate(rule));
            ClassicAssert.IsTrue(duplicateIndex.HasReverseDuplicate(reverseRule));
        }

        [Test]
        public void HasReverseDuplicate_ShouldIgnoreManagementDifferences()
        {
            var rule = TrivialityTestHelper.CreateRule(
                [TrivialityTestHelper.CreateNetworkLocation(TrivialityTestHelper.CreateNetworkObject("Source", "10.1.2.3/32", "10.1.2.3/32"))],
                [TrivialityTestHelper.CreateNetworkLocation(TrivialityTestHelper.CreateNetworkObject("Destination", "10.9.8.7/32", "10.9.8.7/32"))],
                [TrivialityTestHelper.CreateProtocolService("Https", 6, 0, 0, 443, 443)],
                mgmtId: 7,
                id: 1001);

            var reverseRule = TrivialityTestHelper.CreateRule(
                [TrivialityTestHelper.CreateNetworkLocation(TrivialityTestHelper.CreateNetworkObject("Destination", "10.9.8.7/32", "10.9.8.7/32"))],
                [TrivialityTestHelper.CreateNetworkLocation(TrivialityTestHelper.CreateNetworkObject("Source", "10.1.2.3/32", "10.1.2.3/32"))],
                [TrivialityTestHelper.CreateProtocolService("Https", 6, 443, 443, 0, 0)],
                mgmtId: 9,
                id: 1002);

            RuleBidirectionalDuplicateIndex duplicateIndex = new([rule, reverseRule]);

            ClassicAssert.IsTrue(duplicateIndex.HasReverseDuplicate(rule));
        }

        [Test]
        public void HasReverseDuplicate_ShouldReturnFalseForDifferentProtocol()
        {
            var rule = TrivialityTestHelper.CreateRule(
                [TrivialityTestHelper.CreateNetworkLocation(TrivialityTestHelper.CreateNetworkObject("Source", "10.1.2.3/32", "10.1.2.3/32"))],
                [TrivialityTestHelper.CreateNetworkLocation(TrivialityTestHelper.CreateNetworkObject("Destination", "10.9.8.7/32", "10.9.8.7/32"))],
                [TrivialityTestHelper.CreateProtocolService("Https", 6, 0, 0, 443, 443)],
                id: 1001);

            var reverseRule = TrivialityTestHelper.CreateRule(
                [TrivialityTestHelper.CreateNetworkLocation(TrivialityTestHelper.CreateNetworkObject("Destination", "10.9.8.7/32", "10.9.8.7/32"))],
                [TrivialityTestHelper.CreateNetworkLocation(TrivialityTestHelper.CreateNetworkObject("Source", "10.1.2.3/32", "10.1.2.3/32"))],
                [TrivialityTestHelper.CreateProtocolService("Dns", 17, 53, 53, 0, 0)],
                id: 1002);

            RuleBidirectionalDuplicateIndex duplicateIndex = new([rule, reverseRule]);

            ClassicAssert.IsFalse(duplicateIndex.HasReverseDuplicate(rule));
        }

        [Test]
        public void HasReverseDuplicate_ShouldReturnFalseForDifferentTcpPorts()
        {
            var rule = TrivialityTestHelper.CreateRule(
                [TrivialityTestHelper.CreateNetworkLocation(TrivialityTestHelper.CreateNetworkObject("Source", "10.1.2.3/32", "10.1.2.3/32"))],
                [TrivialityTestHelper.CreateNetworkLocation(TrivialityTestHelper.CreateNetworkObject("Destination", "10.9.8.7/32", "10.9.8.7/32"))],
                [TrivialityTestHelper.CreateService("Https", 6, 443, 443)],
                id: 1001);

            var reverseRule = TrivialityTestHelper.CreateRule(
                [TrivialityTestHelper.CreateNetworkLocation(TrivialityTestHelper.CreateNetworkObject("Destination", "10.9.8.7/32", "10.9.8.7/32"))],
                [TrivialityTestHelper.CreateNetworkLocation(TrivialityTestHelper.CreateNetworkObject("Source", "10.1.2.3/32", "10.1.2.3/32"))],
                [TrivialityTestHelper.CreateService("AltHttps", 6, 8443, 8443)],
                id: 1002);

            RuleBidirectionalDuplicateIndex duplicateIndex = new([rule, reverseRule]);

            ClassicAssert.IsFalse(duplicateIndex.HasReverseDuplicate(rule));
        }

        [Test]
        public void HasReverseDuplicate_ShouldResolveServiceGroups()
        {
            var https = TrivialityTestHelper.CreateService("Https", 6, 443, 443);
            var rule = TrivialityTestHelper.CreateRule(
                [TrivialityTestHelper.CreateNetworkLocation(TrivialityTestHelper.CreateNetworkObject("Source", "10.1.2.3/32", "10.1.2.3/32"))],
                [TrivialityTestHelper.CreateNetworkLocation(TrivialityTestHelper.CreateNetworkObject("Destination", "10.9.8.7/32", "10.9.8.7/32"))],
                [TrivialityTestHelper.CreateServiceGroup("WebGroup", https)],
                id: 1001);

            var reverseRule = TrivialityTestHelper.CreateRule(
                [TrivialityTestHelper.CreateNetworkLocation(TrivialityTestHelper.CreateNetworkObject("Destination", "10.9.8.7/32", "10.9.8.7/32"))],
                [TrivialityTestHelper.CreateNetworkLocation(TrivialityTestHelper.CreateNetworkObject("Source", "10.1.2.3/32", "10.1.2.3/32"))],
                [TrivialityTestHelper.CreateProtocolService("Https", 6, 443, 443, 0, 0)],
                id: 1002);

            RuleBidirectionalDuplicateIndex duplicateIndex = new([rule, reverseRule]);

            ClassicAssert.IsTrue(duplicateIndex.HasReverseDuplicate(rule));
        }

        [Test]
        public void HasReverseDuplicate_ShouldReturnFalseWhenOnlySameRuleExistsInIndex()
        {
            var rule = TrivialityTestHelper.CreateRule(
                [TrivialityTestHelper.CreateNetworkLocation(TrivialityTestHelper.CreateNetworkObject("Source", "10.1.2.3/32", "10.1.2.3/32"))],
                [TrivialityTestHelper.CreateNetworkLocation(TrivialityTestHelper.CreateNetworkObject("Destination", "10.9.8.7/32", "10.9.8.7/32"))],
                [TrivialityTestHelper.CreateService("Https", 6, 443, 443)],
                id: 1001);

            RuleBidirectionalDuplicateIndex duplicateIndex = new([rule]);

            ClassicAssert.IsFalse(duplicateIndex.HasReverseDuplicate(rule));
        }

        [Test]
        public void HasReverseDuplicate_ShouldIgnoreDuplicateSourceMembersInSignature()
        {
            var sourceA = TrivialityTestHelper.CreateNetworkObject("SourceA", "10.1.2.3/32", "10.1.2.3/32");
            var sourceB = TrivialityTestHelper.CreateNetworkObject("SourceB", "10.1.2.4/32", "10.1.2.4/32");
            var destination = TrivialityTestHelper.CreateNetworkObject("Destination", "10.9.8.7/32", "10.9.8.7/32");

            var rule = TrivialityTestHelper.CreateRule(
                [TrivialityTestHelper.CreateNetworkLocation(sourceA), TrivialityTestHelper.CreateNetworkLocation(sourceB)],
                [TrivialityTestHelper.CreateNetworkLocation(destination)],
                [TrivialityTestHelper.CreateProtocolService("Https", 6, 0, 0, 443, 443)],
                id: 1001);

            var reverseRule = TrivialityTestHelper.CreateRule(
                [TrivialityTestHelper.CreateNetworkLocation(destination)],
                [TrivialityTestHelper.CreateNetworkLocation(sourceA), TrivialityTestHelper.CreateNetworkLocation(sourceA), TrivialityTestHelper.CreateNetworkLocation(sourceB)],
                [TrivialityTestHelper.CreateProtocolService("Https", 6, 443, 443, 0, 0)],
                id: 1002);

            RuleBidirectionalDuplicateIndex duplicateIndex = new([rule, reverseRule]);

            ClassicAssert.IsTrue(duplicateIndex.HasReverseDuplicate(rule));
        }

        [Test]
        public void HasReverseDuplicate_ShouldIgnoreMemberOrderingInSignature()
        {
            var sourceA = TrivialityTestHelper.CreateNetworkObject("SourceA", "10.1.2.3/32", "10.1.2.3/32");
            var sourceB = TrivialityTestHelper.CreateNetworkObject("SourceB", "10.1.2.4/32", "10.1.2.4/32");
            var destination = TrivialityTestHelper.CreateNetworkObject("Destination", "10.9.8.7/32", "10.9.8.7/32");

            var rule = TrivialityTestHelper.CreateRule(
                [TrivialityTestHelper.CreateNetworkLocation(sourceA), TrivialityTestHelper.CreateNetworkLocation(sourceB)],
                [TrivialityTestHelper.CreateNetworkLocation(destination)],
                [TrivialityTestHelper.CreateProtocolService("Https", 6, 0, 0, 443, 443)],
                id: 1001);

            var reverseRule = TrivialityTestHelper.CreateRule(
                [TrivialityTestHelper.CreateNetworkLocation(destination)],
                [TrivialityTestHelper.CreateNetworkLocation(sourceB), TrivialityTestHelper.CreateNetworkLocation(sourceA)],
                [TrivialityTestHelper.CreateProtocolService("Https", 6, 443, 443, 0, 0)],
                id: 1002);

            RuleBidirectionalDuplicateIndex duplicateIndex = new([rule, reverseRule]);

            ClassicAssert.IsTrue(duplicateIndex.HasReverseDuplicate(rule));
        }

        [Test]
        public void HasReverseDuplicate_ShouldSwapPortsForReverseMatchWhenProtocolIsMissing()
        {
            var rule = TrivialityTestHelper.CreateRule(
                [TrivialityTestHelper.CreateNetworkLocation(TrivialityTestHelper.CreateNetworkObject("Source", "10.1.2.3/32", "10.1.2.3/32"))],
                [TrivialityTestHelper.CreateNetworkLocation(TrivialityTestHelper.CreateNetworkObject("Destination", "10.9.8.7/32", "10.9.8.7/32"))],
                [TrivialityTestHelper.CreatePortOnlyService("PortOnlyA", 1024, 1024, 443, 443)],
                id: 1001);

            var reverseRule = TrivialityTestHelper.CreateRule(
                [TrivialityTestHelper.CreateNetworkLocation(TrivialityTestHelper.CreateNetworkObject("Destination", "10.9.8.7/32", "10.9.8.7/32"))],
                [TrivialityTestHelper.CreateNetworkLocation(TrivialityTestHelper.CreateNetworkObject("Source", "10.1.2.3/32", "10.1.2.3/32"))],
                [TrivialityTestHelper.CreatePortOnlyService("PortOnlyB", 443, 443, 1024, 1024)],
                id: 1002);

            RuleBidirectionalDuplicateIndex duplicateIndex = new([rule, reverseRule]);

            ClassicAssert.IsTrue(duplicateIndex.HasReverseDuplicate(rule));
        }

        [Test]
        public void HasReverseDuplicate_ShouldStillMatchSameProtocolWhenPortsAreEmpty()
        {
            var rule = TrivialityTestHelper.CreateRule(
                [TrivialityTestHelper.CreateNetworkLocation(TrivialityTestHelper.CreateNetworkObject("Source", "10.1.2.3/32", "10.1.2.3/32"))],
                [TrivialityTestHelper.CreateNetworkLocation(TrivialityTestHelper.CreateNetworkObject("Destination", "10.9.8.7/32", "10.9.8.7/32"))],
                [TrivialityTestHelper.CreateProtocolService("IcmpA", 1, 0, 0, 0, 0)],
                id: 1001);

            var reverseRule = TrivialityTestHelper.CreateRule(
                [TrivialityTestHelper.CreateNetworkLocation(TrivialityTestHelper.CreateNetworkObject("Destination", "10.9.8.7/32", "10.9.8.7/32"))],
                [TrivialityTestHelper.CreateNetworkLocation(TrivialityTestHelper.CreateNetworkObject("Source", "10.1.2.3/32", "10.1.2.3/32"))],
                [TrivialityTestHelper.CreateProtocolService("IcmpB", 1, 0, 0, 0, 0)],
                id: 1002);

            RuleBidirectionalDuplicateIndex duplicateIndex = new([rule, reverseRule]);

            ClassicAssert.IsTrue(duplicateIndex.HasReverseDuplicate(rule));
        }

        [Test]
        public void HasReverseDuplicate_ShouldIgnoreDisabledAndNonAcceptRulesWhenBuildingIndex()
        {
            var rule = TrivialityTestHelper.CreateRule(
                [TrivialityTestHelper.CreateNetworkLocation(TrivialityTestHelper.CreateNetworkObject("Source", "10.1.2.3/32", "10.1.2.3/32"))],
                [TrivialityTestHelper.CreateNetworkLocation(TrivialityTestHelper.CreateNetworkObject("Destination", "10.9.8.7/32", "10.9.8.7/32"))],
                [TrivialityTestHelper.CreateProtocolService("Https", 6, 0, 0, 443, 443)],
                id: 1001);

            var disabledReverseRule = TrivialityTestHelper.CreateRule(
                [TrivialityTestHelper.CreateNetworkLocation(TrivialityTestHelper.CreateNetworkObject("Destination", "10.9.8.7/32", "10.9.8.7/32"))],
                [TrivialityTestHelper.CreateNetworkLocation(TrivialityTestHelper.CreateNetworkObject("Source", "10.1.2.3/32", "10.1.2.3/32"))],
                [TrivialityTestHelper.CreateProtocolService("Https", 6, 443, 443, 0, 0)],
                id: 1002,
                disabled: true);

            var deniedReverseRule = TrivialityTestHelper.CreateRule(
                [TrivialityTestHelper.CreateNetworkLocation(TrivialityTestHelper.CreateNetworkObject("Destination", "10.9.8.7/32", "10.9.8.7/32"))],
                [TrivialityTestHelper.CreateNetworkLocation(TrivialityTestHelper.CreateNetworkObject("Source", "10.1.2.3/32", "10.1.2.3/32"))],
                [TrivialityTestHelper.CreateProtocolService("Https", 6, 443, 443, 0, 0)],
                id: 1003,
                action: RuleActions.Deny);

            RuleBidirectionalDuplicateIndex duplicateIndex = new([rule, disabledReverseRule, deniedReverseRule]);

            ClassicAssert.IsFalse(duplicateIndex.HasReverseDuplicate(rule));
        }

        [Test]
        public void HasReverseDuplicate_ShouldUseUidForSelfIdentificationWhenIdIsMissing()
        {
            var rule = TrivialityTestHelper.CreateRule(
                [TrivialityTestHelper.CreateNetworkLocation(TrivialityTestHelper.CreateNetworkObject("Source", "10.1.2.3/32", "10.1.2.3/32"))],
                [TrivialityTestHelper.CreateNetworkLocation(TrivialityTestHelper.CreateNetworkObject("Destination", "10.9.8.7/32", "10.9.8.7/32"))],
                [TrivialityTestHelper.CreateProtocolService("Https", 6, 0, 0, 443, 443)],
                uid: "rule-a");

            var reverseRule = TrivialityTestHelper.CreateRule(
                [TrivialityTestHelper.CreateNetworkLocation(TrivialityTestHelper.CreateNetworkObject("Destination", "10.9.8.7/32", "10.9.8.7/32"))],
                [TrivialityTestHelper.CreateNetworkLocation(TrivialityTestHelper.CreateNetworkObject("Source", "10.1.2.3/32", "10.1.2.3/32"))],
                [TrivialityTestHelper.CreateProtocolService("Https", 6, 443, 443, 0, 0)],
                uid: "rule-b");

            RuleBidirectionalDuplicateIndex duplicateIndex = new([rule, reverseRule]);

            ClassicAssert.IsTrue(duplicateIndex.HasReverseDuplicate(rule));
        }

        [Test]
        public void HasReverseDuplicate_ShouldNotTreatTransientRuleAsDuplicateOfItself()
        {
            var rule = TrivialityTestHelper.CreateRule(
                [TrivialityTestHelper.CreateNetworkLocation(TrivialityTestHelper.CreateNetworkObject("Source", "10.1.2.3/32", "10.1.2.3/32"))],
                [TrivialityTestHelper.CreateNetworkLocation(TrivialityTestHelper.CreateNetworkObject("Destination", "10.9.8.7/32", "10.9.8.7/32"))],
                [TrivialityTestHelper.CreateService("Https", 6, 443, 443)]);

            RuleBidirectionalDuplicateIndex duplicateIndex = new([rule]);

            ClassicAssert.IsFalse(duplicateIndex.HasReverseDuplicate(rule));
        }
    }
}
