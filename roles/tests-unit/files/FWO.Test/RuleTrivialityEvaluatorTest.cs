using FWO.Basics;
using FWO.Data;
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
            Rule rule = CreateRule(
                [CreateNetworkLocation(CreateNetworkObject("Source", "10.1.2.0/24", "10.1.2.255/24"))],
                [CreateNetworkLocation(CreateNetworkObject("Destination", "10.9.8.7/32", "10.9.8.7/32"))]);

            TrivialityCheckResult result = _evaluator.EvaluateBroadNetworkObjectCriterion(rule, 24);

            ClassicAssert.IsTrue(result.IsTrivial);
            ClassicAssert.AreEqual(string.Empty, result.Reason);
        }

        [Test]
        public void EvaluateBroadNetworkObjectCriterion_ShouldReturnNonTrivialWhenSourceViolatesThreshold()
        {
            Rule rule = CreateRule(
                [CreateNetworkLocation(CreateNetworkObject("Source", "10.0.0.0/8", "10.255.255.255/8"))],
                [CreateNetworkLocation(CreateNetworkObject("Destination", "10.9.8.7/32", "10.9.8.7/32"))]);

            TrivialityCheckResult result = _evaluator.EvaluateBroadNetworkObjectCriterion(rule, 24);

            ClassicAssert.IsFalse(result.IsTrivial);
            ClassicAssert.AreEqual(RuleTrivialityEvaluator.BroadNetworkObjectReason, result.Reason);
        }

        [Test]
        public void EvaluateBroadNetworkObjectCriterion_ShouldReturnNonTrivialWhenDestinationViolatesThreshold()
        {
            Rule rule = CreateRule(
                [CreateNetworkLocation(CreateNetworkObject("Source", "10.9.8.7/32", "10.9.8.7/32"))],
                [CreateNetworkLocation(CreateNetworkObject("Destination", "172.16.0.0/12", "172.31.255.255/12"))]);

            TrivialityCheckResult result = _evaluator.EvaluateBroadNetworkObjectCriterion(rule, 24);

            ClassicAssert.IsFalse(result.IsTrivial);
            ClassicAssert.AreEqual(RuleTrivialityEvaluator.BroadNetworkObjectReason, result.Reason);
        }

        [Test]
        public void EvaluateBroadNetworkObjectCriterion_ShouldResolveFlatGroupMembers()
        {
            NetworkObject broadMember = CreateNetworkObject("BroadMember", "192.168.0.0/16", "192.168.255.255/16");
            NetworkObject group = CreateGroup("Group", broadMember);
            Rule rule = CreateRule(
                [CreateNetworkLocation(group)],
                [CreateNetworkLocation(CreateNetworkObject("Destination", "10.9.8.7/32", "10.9.8.7/32"))]);

            TrivialityCheckResult result = _evaluator.EvaluateBroadNetworkObjectCriterion(rule, 24);

            ClassicAssert.IsFalse(result.IsTrivial);
            ClassicAssert.AreEqual(RuleTrivialityEvaluator.BroadNetworkObjectReason, result.Reason);
        }

        [Test]
        public void EvaluateBroadNetworkObjectCriterion_ShouldReturnNonTrivialForIpv6Objects()
        {
            Rule rule = CreateRule(
                [CreateNetworkLocation(CreateNetworkObject("Ipv6", "2001:db8::/64", "2001:db8::ffff/64"))],
                [CreateNetworkLocation(CreateNetworkObject("Destination", "10.9.8.7/32", "10.9.8.7/32"))]);

            TrivialityCheckResult result = _evaluator.EvaluateBroadNetworkObjectCriterion(rule, 24);

            ClassicAssert.IsFalse(result.IsTrivial);
            ClassicAssert.AreEqual(RuleTrivialityEvaluator.Ipv6NotSupportedReason, result.Reason);
        }

        [Test]
        public void EvaluateBidirectionalDuplicateCriterion_ShouldReturnNonTrivialForReverseRule()
        {
            Rule rule = CreateRule(
                [CreateNetworkLocation(CreateNetworkObject("Source", "10.1.2.3/32", "10.1.2.3/32"))],
                [CreateNetworkLocation(CreateNetworkObject("Destination", "10.9.8.7/32", "10.9.8.7/32"))],
                [CreateService("Https", 6, 443, 443)],
                7,
                1001);

            Rule reverseRule = CreateRule(
                [CreateNetworkLocation(CreateNetworkObject("Destination", "10.9.8.7/32", "10.9.8.7/32"))],
                [CreateNetworkLocation(CreateNetworkObject("Source", "10.1.2.3/32", "10.1.2.3/32"))],
                [CreateService("Https", 6, 443, 443)],
                7,
                1002);

            TrivialityCheckResult result = _evaluator.EvaluateBidirectionalDuplicateCriterion(rule, [reverseRule]);

            ClassicAssert.IsFalse(result.IsTrivial);
            ClassicAssert.AreEqual(RuleTrivialityEvaluator.BidirectionalDuplicateReason, result.Reason);
        }

        [Test]
        public void EvaluateBidirectionalDuplicateCriterion_ShouldReturnTrivialForDifferentManagement()
        {
            Rule rule = CreateRule(
                [CreateNetworkLocation(CreateNetworkObject("Source", "10.1.2.3/32", "10.1.2.3/32"))],
                [CreateNetworkLocation(CreateNetworkObject("Destination", "10.9.8.7/32", "10.9.8.7/32"))],
                [CreateService("Https", 6, 443, 443)],
                7,
                1001);

            Rule reverseRule = CreateRule(
                [CreateNetworkLocation(CreateNetworkObject("Destination", "10.9.8.7/32", "10.9.8.7/32"))],
                [CreateNetworkLocation(CreateNetworkObject("Source", "10.1.2.3/32", "10.1.2.3/32"))],
                [CreateService("Https", 6, 443, 443)],
                9,
                1002);

            TrivialityCheckResult result = _evaluator.EvaluateBidirectionalDuplicateCriterion(rule, [reverseRule]);

            ClassicAssert.IsTrue(result.IsTrivial);
        }

        [Test]
        public void EvaluateBidirectionalDuplicateCriterion_ShouldReturnTrivialForDifferentService()
        {
            Rule rule = CreateRule(
                [CreateNetworkLocation(CreateNetworkObject("Source", "10.1.2.3/32", "10.1.2.3/32"))],
                [CreateNetworkLocation(CreateNetworkObject("Destination", "10.9.8.7/32", "10.9.8.7/32"))],
                [CreateService("Https", 6, 443, 443)],
                7,
                1001);

            Rule reverseRule = CreateRule(
                [CreateNetworkLocation(CreateNetworkObject("Destination", "10.9.8.7/32", "10.9.8.7/32"))],
                [CreateNetworkLocation(CreateNetworkObject("Source", "10.1.2.3/32", "10.1.2.3/32"))],
                [CreateService("Http", 6, 80, 80)],
                7,
                1002);

            TrivialityCheckResult result = _evaluator.EvaluateBidirectionalDuplicateCriterion(rule, [reverseRule]);

            ClassicAssert.IsTrue(result.IsTrivial);
        }

        [Test]
        public void EvaluateBidirectionalDuplicateCriterion_ShouldResolveServiceGroups()
        {
            NetworkService https = CreateService("Https", 6, 443, 443);
            Rule rule = CreateRule(
                [CreateNetworkLocation(CreateNetworkObject("Source", "10.1.2.3/32", "10.1.2.3/32"))],
                [CreateNetworkLocation(CreateNetworkObject("Destination", "10.9.8.7/32", "10.9.8.7/32"))],
                [CreateServiceGroup("WebGroup", https)],
                7,
                1001);

            Rule reverseRule = CreateRule(
                [CreateNetworkLocation(CreateNetworkObject("Destination", "10.9.8.7/32", "10.9.8.7/32"))],
                [CreateNetworkLocation(CreateNetworkObject("Source", "10.1.2.3/32", "10.1.2.3/32"))],
                [CreateService("Https", 6, 443, 443)],
                7,
                1002);

            TrivialityCheckResult result = _evaluator.EvaluateBidirectionalDuplicateCriterion(rule, [reverseRule]);

            ClassicAssert.IsFalse(result.IsTrivial);
            ClassicAssert.AreEqual(RuleTrivialityEvaluator.BidirectionalDuplicateReason, result.Reason);
        }

        [Test]
        public void EvaluateZoneObjectCriterion_ShouldReturnNonTrivialForSourceZoneObject()
        {
            Rule rule = CreateRule(
                [CreateNetworkLocation(CreateNetworkObject("DMZ_ZONE", "10.1.2.3/32", "10.1.2.3/32"))],
                [CreateNetworkLocation(CreateNetworkObject("Destination", "10.9.8.7/32", "10.9.8.7/32"))]);

            TrivialityCheckResult result = _evaluator.EvaluateZoneObjectCriterion(rule);

            ClassicAssert.IsFalse(result.IsTrivial);
            ClassicAssert.AreEqual(RuleTrivialityEvaluator.ZoneObjectUsageReason, result.Reason);
        }

        [Test]
        public void EvaluateZoneObjectCriterion_ShouldReturnNonTrivialForDestinationZoneObject()
        {
            Rule rule = CreateRule(
                [CreateNetworkLocation(CreateNetworkObject("Source", "10.1.2.3/32", "10.1.2.3/32"))],
                [CreateNetworkLocation(CreateNetworkObject("INTERN_ZONE", "10.9.8.7/32", "10.9.8.7/32"))]);

            TrivialityCheckResult result = _evaluator.EvaluateZoneObjectCriterion(rule);

            ClassicAssert.IsFalse(result.IsTrivial);
            ClassicAssert.AreEqual(RuleTrivialityEvaluator.ZoneObjectUsageReason, result.Reason);
        }

        [Test]
        public void EvaluateZoneObjectCriterion_ShouldResolveGroupMembers()
        {
            NetworkObject zoneMember = CreateNetworkObject("PARTNER_ZONE", "10.9.8.7/32", "10.9.8.7/32");
            Rule rule = CreateRule(
                [CreateNetworkLocation(CreateGroup("Group", zoneMember))],
                [CreateNetworkLocation(CreateNetworkObject("Destination", "10.1.2.3/32", "10.1.2.3/32"))]);

            TrivialityCheckResult result = _evaluator.EvaluateZoneObjectCriterion(rule);

            ClassicAssert.IsFalse(result.IsTrivial);
            ClassicAssert.AreEqual(RuleTrivialityEvaluator.ZoneObjectUsageReason, result.Reason);
        }

        [Test]
        public void EvaluateZoneObjectCriterion_ShouldReturnTrivialWhenNoZoneObjectIsUsed()
        {
            Rule rule = CreateRule(
                [CreateNetworkLocation(CreateNetworkObject("Source", "10.1.2.3/32", "10.1.2.3/32"))],
                [CreateNetworkLocation(CreateNetworkObject("Destination", "10.9.8.7/32", "10.9.8.7/32"))]);

            TrivialityCheckResult result = _evaluator.EvaluateZoneObjectCriterion(rule);

            ClassicAssert.IsTrue(result.IsTrivial);
            ClassicAssert.AreEqual(string.Empty, result.Reason);
        }

        private static Rule CreateRule(List<NetworkLocation> froms, List<NetworkLocation> tos, List<NetworkService>? services = null, int mgmtId = 0, long id = 0)
        {
            return new()
            {
                Action = RuleActions.Accept,
                Id = id,
                MgmtId = mgmtId,
                Froms = [.. froms],
                Tos = [.. tos],
                Services = [.. (services ?? []).Select(service => new ServiceWrapper { Content = service })]
            };
        }

        private static NetworkLocation CreateNetworkLocation(NetworkObject networkObject)
        {
            return new(new NetworkUser(), networkObject);
        }

        private static NetworkObject CreateNetworkObject(string name, string ip, string ipEnd)
        {
            return new()
            {
                Name = name,
                IP = ip,
                IpEnd = ipEnd,
                Type = new NetworkObjectType
                {
                    Name = ObjectType.Network
                }
            };
        }

        private static NetworkObject CreateGroup(string name, NetworkObject member)
        {
            return new()
            {
                Name = name,
                Type = new NetworkObjectType
                {
                    Name = ObjectType.Group
                },
                ObjectGroupFlats =
                [
                    new GroupFlat<NetworkObject>
                    {
                        Object = member
                    }
                ]
            };
        }

        private static NetworkService CreateService(string name, int protoId, int portStart, int portEnd)
        {
            return new()
            {
                Name = name,
                ProtoId = protoId,
                DestinationPort = portStart,
                DestinationPortEnd = portEnd,
                Type = new NetworkServiceType
                {
                    Name = ServiceType.SimpleService
                }
            };
        }

        private static NetworkService CreateServiceGroup(string name, NetworkService member)
        {
            return new()
            {
                Name = name,
                Type = new NetworkServiceType
                {
                    Name = ServiceType.Group
                },
                ServiceGroupFlats =
                [
                    new GroupFlat<NetworkService>
                    {
                        Object = member
                    }
                ]
            };
        }
    }
}
