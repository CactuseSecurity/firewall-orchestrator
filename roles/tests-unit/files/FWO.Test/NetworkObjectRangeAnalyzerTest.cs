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
            NetworkObject networkObject = CreateNetworkObject("Subnet", "10.1.0.0/24", "10.1.255.255/24");

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
        public void MatchesIpFilter_ShouldIgnoreObjectsBelowPrefixThresholdAndKeepSearching()
        {
            List<NetworkObject> objects =
            [
                CreateNetworkObject("Broad", "10.0.0.0/8", "10.255.255.255/8"),
                CreateNetworkObject("Host", "10.1.2.3/32", "10.1.2.3/32")
            ];

            bool matches = _analyzer.MatchesIpFilter(IPAddress.Parse("10.1.2.3"), 24, objects);

            ClassicAssert.IsTrue(matches);
        }

        [Test]
        public void ExceedsPrefixThreshold_ShouldFlagWhenAnyObjectIsBroaderThanThreshold()
        {
            List<NetworkObject> objects =
            [
                CreateNetworkObject("Host", "10.1.2.3/32", "10.1.2.3/32"),
                CreateNetworkObject("Broad", "10.0.0.0/8", "10.255.255.255/8")
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
