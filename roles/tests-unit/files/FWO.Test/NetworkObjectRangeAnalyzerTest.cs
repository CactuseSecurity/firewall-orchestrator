using System.Net;
using FWO.Basics;
using FWO.Data;
using FWO.Data.Networking;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class NetworkObjectRangeAnalyzerTest
    {
        private readonly NetworkObjectRangeAnalyzer _analyzer = new();

        [Test]
        public void Analyze_ShouldReturnHostPrefixForSingleIpv4Object()
        {
            NetworkObject networkObject = CreateNetworkObject("Host", "10.1.2.3/32", "10.1.2.3/32");

            NetworkObjectRangeAnalysis analysis = _analyzer.Analyze(networkObject);

            ClassicAssert.IsTrue(analysis.IsSupported);
            ClassicAssert.IsTrue(analysis.IsIpv4);
            ClassicAssert.AreEqual(32, analysis.PrefixLength);
        }

        [Test]
        public void Analyze_ShouldReturnSubnetPrefixForIpv4Range()
        {
            NetworkObject networkObject = CreateNetworkObject("Subnet", "10.1.0.0/32", "10.1.255.255/32");

            NetworkObjectRangeAnalysis analysis = _analyzer.Analyze(networkObject);

            ClassicAssert.IsTrue(analysis.IsSupported);
            ClassicAssert.AreEqual(16, analysis.PrefixLength);
        }

        [Test]
        public void Analyze_ShouldMarkIpv6AsUnsupported()
        {
            NetworkObject networkObject = CreateNetworkObject("Ipv6", "2001:db8::/64", "2001:db8::ffff/64");

            NetworkObjectRangeAnalysis analysis = _analyzer.Analyze(networkObject);

            ClassicAssert.IsFalse(analysis.IsSupported);
            ClassicAssert.IsFalse(analysis.IsIpv4);
            ClassicAssert.AreEqual(-1, analysis.PrefixLength);
        }

        [Test]
        public void MatchesIpFilter_ShouldAcceptWhenAnyObjectMeetsThresholdAndContainsIp()
        {
            List<NetworkObject> objects =
            [
                CreateNetworkObject("Broad", "10.0.0.0/32", "10.255.255.255/32"),
                CreateNetworkObject("Host", "10.1.2.3/32", "10.1.2.3/32")
            ];

            bool matches = _analyzer.MatchesIpFilter(IPAddress.Parse("10.1.2.3"), 24, objects);

            ClassicAssert.IsTrue(matches);
        }

        [Test]
        public void MatchesIpFilter_ShouldAcceptWhenEveryObjectMeetsThresholdAndContainsIp()
        {
            List<NetworkObject> objects =
            [
                CreateNetworkObject("Subnet", "10.1.2.0/32", "10.1.2.255/32"),
                CreateNetworkObject("Host", "10.1.2.3/32", "10.1.2.3/32")
            ];

            bool matches = _analyzer.MatchesIpFilter(IPAddress.Parse("10.1.2.3"), 24, objects);

            ClassicAssert.IsTrue(matches);
        }

        [Test]
        public void MatchesIpFilter_ShouldIgnoreUnsupportedObjectsWhenAnyIpv4ObjectMatches()
        {
            List<NetworkObject> objects =
            [
                CreateNetworkObject("Ipv6", "2001:db8::/64", "2001:db8::ffff/64"),
                CreateNetworkObject("Host", "10.1.2.3/32", "10.1.2.3/32")
            ];

            bool matches = _analyzer.MatchesIpFilter(IPAddress.Parse("10.1.2.3"), 24, objects);

            ClassicAssert.IsTrue(matches);
        }

        [Test]
        public void MatchesIpFilter_ShouldRejectWhenNoObjectMatches()
        {
            List<NetworkObject> objects =
            [
                CreateNetworkObject("Broad", "10.0.0.0/32", "10.255.255.255/32"),
                CreateNetworkObject("OtherHost", "192.168.1.1/32", "192.168.1.1/32")
            ];

            bool matches = _analyzer.MatchesIpFilter(IPAddress.Parse("10.1.2.3"), 24, objects);

            ClassicAssert.IsFalse(matches);
        }

        [Test]
        public void MatchesIpFilter_ShouldRejectWhenNoResolvableObjectsArePresent()
        {
            List<NetworkObject> objects =
            [
                new()
                {
                    Name = "EmptyGroup",
                    Type = new NetworkObjectType { Name = ObjectType.Group }
                }
            ];

            bool matches = _analyzer.MatchesIpFilter(IPAddress.Parse("10.1.2.3"), 24, objects);

            ClassicAssert.IsFalse(matches);
        }

        [Test]
        public void ExceedsPrefixThreshold_ShouldFlagWhenAnyObjectIsBroaderThanThreshold()
        {
            List<NetworkObject> objects =
            [
                CreateNetworkObject("Host", "10.1.2.3/32", "10.1.2.3/32"),
                CreateNetworkObject("Broad", "10.0.0.0/32", "10.255.255.255/32")
            ];

            bool exceedsThreshold = _analyzer.ExceedsPrefixThreshold(24, objects);

            ClassicAssert.IsTrue(exceedsThreshold);
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
    }
}
