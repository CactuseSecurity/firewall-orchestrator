using FWO.Basics;
using FWO.Services.Triviality;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class RuleTrivialityEvaluatorTest
    {
        private readonly RuleTrivialityEvaluator _evaluator = new();

        [Test]
        public void EvaluateBroadNetworkObjectCriterion_ShouldReturnTrivialWhenAllObjectsMeetThreshold()
        {
            var rule = TrivialityTestHelper.CreateRule(
                [TrivialityTestHelper.CreateNetworkLocation(TrivialityTestHelper.CreateNetworkObject("Source", "10.1.2.0/24", "10.1.2.255/24"))],
                [TrivialityTestHelper.CreateNetworkLocation(TrivialityTestHelper.CreateNetworkObject("Destination", "10.9.8.7/32", "10.9.8.7/32"))]);

            TrivialityCheckResult result = _evaluator.EvaluateBroadNetworkObjectCriterion(rule, 24);

            ClassicAssert.IsTrue(result.IsTrivial);
            ClassicAssert.AreEqual(string.Empty, result.Reason);
        }

        [Test]
        public void EvaluateBroadNetworkObjectCriterion_ShouldReturnNonTrivialWhenSourceViolatesThreshold()
        {
            var rule = TrivialityTestHelper.CreateRule(
                [TrivialityTestHelper.CreateNetworkLocation(TrivialityTestHelper.CreateNetworkObject("Source", "10.0.0.0/8", "10.255.255.255/8"))],
                [TrivialityTestHelper.CreateNetworkLocation(TrivialityTestHelper.CreateNetworkObject("Destination", "10.9.8.7/32", "10.9.8.7/32"))]);

            TrivialityCheckResult result = _evaluator.EvaluateBroadNetworkObjectCriterion(rule, 24);

            ClassicAssert.IsFalse(result.IsTrivial);
            ClassicAssert.AreEqual(RuleTrivialityEvaluator.BroadNetworkObjectReason, result.Reason);
        }

        [Test]
        public void EvaluateBroadNetworkObjectCriterion_ShouldReturnNonTrivialWhenDestinationViolatesThreshold()
        {
            var rule = TrivialityTestHelper.CreateRule(
                [TrivialityTestHelper.CreateNetworkLocation(TrivialityTestHelper.CreateNetworkObject("Source", "10.9.8.7/32", "10.9.8.7/32"))],
                [TrivialityTestHelper.CreateNetworkLocation(TrivialityTestHelper.CreateNetworkObject("Destination", "172.16.0.0/12", "172.31.255.255/12"))]);

            TrivialityCheckResult result = _evaluator.EvaluateBroadNetworkObjectCriterion(rule, 24);

            ClassicAssert.IsFalse(result.IsTrivial);
            ClassicAssert.AreEqual(RuleTrivialityEvaluator.BroadNetworkObjectReason, result.Reason);
        }

        [Test]
        public void EvaluateBroadNetworkObjectCriterion_ShouldResolveFlatGroupMembers()
        {
            var broadMember = TrivialityTestHelper.CreateNetworkObject("BroadMember", "192.168.0.0/16", "192.168.255.255/16");
            var rule = TrivialityTestHelper.CreateRule(
                [TrivialityTestHelper.CreateNetworkLocation(TrivialityTestHelper.CreateGroup("Group", broadMember))],
                [TrivialityTestHelper.CreateNetworkLocation(TrivialityTestHelper.CreateNetworkObject("Destination", "10.9.8.7/32", "10.9.8.7/32"))]);

            TrivialityCheckResult result = _evaluator.EvaluateBroadNetworkObjectCriterion(rule, 24);

            ClassicAssert.IsFalse(result.IsTrivial);
            ClassicAssert.AreEqual(RuleTrivialityEvaluator.BroadNetworkObjectReason, result.Reason);
        }

        [Test]
        public void EvaluateBroadNetworkObjectCriterion_ShouldReturnNonTrivialForIpv6Objects()
        {
            var rule = TrivialityTestHelper.CreateRule(
                [TrivialityTestHelper.CreateNetworkLocation(TrivialityTestHelper.CreateNetworkObject("Ipv6", "2001:db8::/64", "2001:db8::ffff/64"))],
                [TrivialityTestHelper.CreateNetworkLocation(TrivialityTestHelper.CreateNetworkObject("Destination", "10.9.8.7/32", "10.9.8.7/32"))]);

            TrivialityCheckResult result = _evaluator.EvaluateBroadNetworkObjectCriterion(rule, 24);

            ClassicAssert.IsFalse(result.IsTrivial);
            ClassicAssert.AreEqual(RuleTrivialityEvaluator.Ipv6NotSupportedReason, result.Reason);
        }

        [Test]
        public void EvaluateBidirectionalDuplicateCriterion_ShouldReturnNonTrivialWhenIndexContainsReverseDuplicate()
        {
            var rule = TrivialityTestHelper.CreateRule(
                [TrivialityTestHelper.CreateNetworkLocation(TrivialityTestHelper.CreateNetworkObject("Source", "10.1.2.3/32", "10.1.2.3/32"))],
                [TrivialityTestHelper.CreateNetworkLocation(TrivialityTestHelper.CreateNetworkObject("Destination", "10.9.8.7/32", "10.9.8.7/32"))],
                [TrivialityTestHelper.CreateService("Https", 6, 443, 443)],
                id: 1001);

            var reverseRule = TrivialityTestHelper.CreateRule(
                [TrivialityTestHelper.CreateNetworkLocation(TrivialityTestHelper.CreateNetworkObject("Destination", "10.9.8.7/32", "10.9.8.7/32"))],
                [TrivialityTestHelper.CreateNetworkLocation(TrivialityTestHelper.CreateNetworkObject("Source", "10.1.2.3/32", "10.1.2.3/32"))],
                [TrivialityTestHelper.CreateService("Https", 6, 443, 443)],
                id: 1002);

            RuleBidirectionalDuplicateIndex duplicateIndex = new([rule, reverseRule]);
            TrivialityCheckResult result = _evaluator.EvaluateBidirectionalDuplicateCriterion(rule, duplicateIndex);

            ClassicAssert.IsFalse(result.IsTrivial);
            ClassicAssert.AreEqual(RuleTrivialityEvaluator.BidirectionalDuplicateReason, result.Reason);
        }

        [Test]
        public void EvaluateBidirectionalDuplicateCriterion_ShouldReturnTrivialForDisabledRule()
        {
            var rule = TrivialityTestHelper.CreateRule(
                [TrivialityTestHelper.CreateNetworkLocation(TrivialityTestHelper.CreateNetworkObject("Source", "10.1.2.3/32", "10.1.2.3/32"))],
                [TrivialityTestHelper.CreateNetworkLocation(TrivialityTestHelper.CreateNetworkObject("Destination", "10.9.8.7/32", "10.9.8.7/32"))],
                [TrivialityTestHelper.CreateService("Https", 6, 443, 443)],
                id: 1001,
                disabled: true);

            var reverseRule = TrivialityTestHelper.CreateRule(
                [TrivialityTestHelper.CreateNetworkLocation(TrivialityTestHelper.CreateNetworkObject("Destination", "10.9.8.7/32", "10.9.8.7/32"))],
                [TrivialityTestHelper.CreateNetworkLocation(TrivialityTestHelper.CreateNetworkObject("Source", "10.1.2.3/32", "10.1.2.3/32"))],
                [TrivialityTestHelper.CreateService("Https", 6, 443, 443)],
                id: 1002);

            RuleBidirectionalDuplicateIndex duplicateIndex = new([rule, reverseRule]);
            TrivialityCheckResult result = _evaluator.EvaluateBidirectionalDuplicateCriterion(rule, duplicateIndex);

            ClassicAssert.IsTrue(result.IsTrivial);
            ClassicAssert.AreEqual(string.Empty, result.Reason);
        }

        [Test]
        public void EvaluateZoneObjectAsSourceCriterion_ShouldReturnNonTrivialForSourceZoneObject()
        {
            var rule = TrivialityTestHelper.CreateRule(
                [TrivialityTestHelper.CreateNetworkLocation(TrivialityTestHelper.CreateNetworkObject("DMZ_ZONE", "10.1.2.3/32", "10.1.2.3/32"))],
                [TrivialityTestHelper.CreateNetworkLocation(TrivialityTestHelper.CreateNetworkObject("Destination", "10.9.8.7/32", "10.9.8.7/32"))]);

            TrivialityCheckResult result = _evaluator.EvaluateZoneObjectAsSourceCriterion(rule);

            ClassicAssert.IsFalse(result.IsTrivial);
            ClassicAssert.AreEqual(RuleTrivialityEvaluator.ForbidZonesAsSourceReason, result.Reason);
        }

        [Test]
        public void EvaluateZoneObjectAsDestinationCriterion_ShouldReturnNonTrivialForDestinationZoneObject()
        {
            var rule = TrivialityTestHelper.CreateRule(
                [TrivialityTestHelper.CreateNetworkLocation(TrivialityTestHelper.CreateNetworkObject("Source", "10.1.2.3/32", "10.1.2.3/32"))],
                [TrivialityTestHelper.CreateNetworkLocation(TrivialityTestHelper.CreateNetworkObject("INTERN_ZONE", "10.9.8.7/32", "10.9.8.7/32"))]);

            TrivialityCheckResult result = _evaluator.EvaluateZoneObjectAsDestinationCriterion(rule);

            ClassicAssert.IsFalse(result.IsTrivial);
            ClassicAssert.AreEqual(RuleTrivialityEvaluator.ForbidZonesAsDestinationReason, result.Reason);
        }

        [Test]
        public void EvaluateZoneObjectAsSourceCriterion_ShouldResolveGroupMembers()
        {
            var zoneMember = TrivialityTestHelper.CreateNetworkObject("PARTNER_ZONE", "10.9.8.7/32", "10.9.8.7/32");
            var rule = TrivialityTestHelper.CreateRule(
                [TrivialityTestHelper.CreateNetworkLocation(TrivialityTestHelper.CreateGroup("Group", zoneMember))],
                [TrivialityTestHelper.CreateNetworkLocation(TrivialityTestHelper.CreateNetworkObject("Destination", "10.1.2.3/32", "10.1.2.3/32"))]);

            TrivialityCheckResult result = _evaluator.EvaluateZoneObjectAsSourceCriterion(rule);

            ClassicAssert.IsFalse(result.IsTrivial);
            ClassicAssert.AreEqual(RuleTrivialityEvaluator.ForbidZonesAsSourceReason, result.Reason);
        }

        [Test]
        public void EvaluateZoneObjectAsDestinationCriterion_ShouldResolveGroupMembers()
        {
            var zoneMember = TrivialityTestHelper.CreateNetworkObject("PARTNER_ZONE", "10.9.8.7/32", "10.9.8.7/32");
            var rule = TrivialityTestHelper.CreateRule(
                [TrivialityTestHelper.CreateNetworkLocation(TrivialityTestHelper.CreateNetworkObject("Source", "10.1.2.3/32", "10.1.2.3/32"))],
                [TrivialityTestHelper.CreateNetworkLocation(TrivialityTestHelper.CreateGroup("Group", zoneMember))]);

            TrivialityCheckResult result = _evaluator.EvaluateZoneObjectAsDestinationCriterion(rule);

            ClassicAssert.IsFalse(result.IsTrivial);
            ClassicAssert.AreEqual(RuleTrivialityEvaluator.ForbidZonesAsDestinationReason, result.Reason);
        }

        [Test]
        public void EvaluateZoneObjectAsSourceCriterion_ShouldReturnTrivialWhenNoSourceZoneObjectIsUsed()
        {
            var rule = TrivialityTestHelper.CreateRule(
                [TrivialityTestHelper.CreateNetworkLocation(TrivialityTestHelper.CreateNetworkObject("Source", "10.1.2.3/32", "10.1.2.3/32"))],
                [TrivialityTestHelper.CreateNetworkLocation(TrivialityTestHelper.CreateNetworkObject("Destination", "10.9.8.7/32", "10.9.8.7/32"))]);

            TrivialityCheckResult result = _evaluator.EvaluateZoneObjectAsSourceCriterion(rule);

            ClassicAssert.IsTrue(result.IsTrivial);
            ClassicAssert.AreEqual(string.Empty, result.Reason);
        }

        [Test]
        public void EvaluateZoneObjectAsDestinationCriterion_ShouldReturnTrivialWhenNoDestinationZoneObjectIsUsed()
        {
            var rule = TrivialityTestHelper.CreateRule(
                [TrivialityTestHelper.CreateNetworkLocation(TrivialityTestHelper.CreateNetworkObject("Source", "10.1.2.3/32", "10.1.2.3/32"))],
                [TrivialityTestHelper.CreateNetworkLocation(TrivialityTestHelper.CreateNetworkObject("Destination", "10.9.8.7/32", "10.9.8.7/32"))]);

            TrivialityCheckResult result = _evaluator.EvaluateZoneObjectAsDestinationCriterion(rule);

            ClassicAssert.IsTrue(result.IsTrivial);
            ClassicAssert.AreEqual(string.Empty, result.Reason);
        }
    }
}
