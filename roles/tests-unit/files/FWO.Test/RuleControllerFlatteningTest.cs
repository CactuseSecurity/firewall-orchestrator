using System.Reflection;
using FWO.Basics;
using FWO.Data;
using FWO.Middleware.Server.Controllers;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class RuleControllerFlatteningTest
    {
        [Test]
        public void FlattenRuleNetworkObjects_ShouldDropTypelessPlaceholder()
        {
            var placeholder = new NetworkObject
            {
                Id = 1,
                Name = "Group A",
                Type = new NetworkObjectType()
            };

            var leaf = new NetworkObject
            {
                Id = 2,
                Name = "Leaf A",
                Type = new NetworkObjectType { Name = ObjectType.Network },
                IP = "10.0.0.1",
                IpEnd = "10.0.0.1"
            };

            var group = new NetworkObject
            {
                Id = 3,
                Name = "Group A",
                Type = new NetworkObjectType { Name = ObjectType.Group },
                ObjectGroupFlats = [new GroupFlat<NetworkObject> { Id = 4, Object = leaf }]
            };

            List<NetworkObject> result = InvokePrivateFlatten<NetworkObject>("FlattenRuleNetworkObjects",
                [placeholder, group]);

            ClassicAssert.AreEqual(2, result.Count);
            ClassicAssert.IsFalse(result.Any(item => item.Id == placeholder.Id));
            ClassicAssert.IsTrue(result.Any(item => item.Id == group.Id));
            ClassicAssert.IsTrue(result.Any(item => item.Id == leaf.Id));
        }

        [Test]
        public void FlattenRuleServices_ShouldDropTypelessPlaceholder()
        {
            var placeholder = new NetworkService
            {
                Id = 10,
                Name = "Service A",
                Type = new NetworkServiceType()
            };

            var leaf = new NetworkService
            {
                Id = 11,
                Name = "Leaf Service",
                Type = new NetworkServiceType { Name = "service" },
                DestinationPort = 443,
                Protocol = new NetworkProtocol { Name = "tcp" }
            };

            var nestedGroup = new NetworkService
            {
                Id = 12,
                Name = "Group A",
                Type = new NetworkServiceType { Name = ServiceType.Group },
                ServiceGroupFlats =
                [
                    new GroupFlat<NetworkService> { Id = 13, Object = leaf }
                ]
            };

            var group = new NetworkService
            {
                Id = 14,
                Name = "Service A",
                Type = new NetworkServiceType { Name = ServiceType.Group },
                ServiceGroupFlats =
                [
                    new GroupFlat<NetworkService> { Id = 15, Object = nestedGroup },
                    new GroupFlat<NetworkService> { Id = 16, Object = leaf }
                ]
            };

            List<NetworkService> result = InvokePrivateFlatten<NetworkService>("FlattenRuleServices",
                [placeholder, group]);

            ClassicAssert.AreEqual(1, result.Count);
            ClassicAssert.IsFalse(result.Any(item => item.Id == placeholder.Id));
            ClassicAssert.IsFalse(result.Any(item => item.Id == group.Id));
            ClassicAssert.IsTrue(result.Any(item => item.Id == leaf.Id));
        }

        private static List<T> InvokePrivateFlatten<T>(string methodName, List<T> input)
        {
            MethodInfo method = typeof(RuleController)
                .GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new InvalidOperationException($"Could not find method {methodName}.");

            object? result = method.Invoke(null, [input]);
            return result as List<T> ?? throw new InvalidOperationException($"Unexpected result from {methodName}.");
        }
    }
}
