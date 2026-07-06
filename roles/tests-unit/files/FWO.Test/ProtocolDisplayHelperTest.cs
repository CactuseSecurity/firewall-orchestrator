using FWO.Data;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class ProtocolDisplayHelperTest
    {
        [Test]
        public void CustomSortProtocols_ReducedSetUsesDefaultProtocolOrder()
        {
            List<IpProtocol> protocols = BuildProtocols();

            List<IpProtocol> sortedProtocols = ProtocolDisplayHelper.CustomSortProtocols(protocols, null, true);

            Assert.That(sortedProtocols.Select(protocol => protocol.Name), Is.EqualTo(ProtocolNames.DefaultReducedProtocolNames));
        }

        [Test]
        public void CustomSortProtocols_ReducedSetUsesConfiguredProtocolOrder()
        {
            List<IpProtocol> protocols = BuildProtocols();

            List<IpProtocol> sortedProtocols = ProtocolDisplayHelper.CustomSortProtocols(protocols, ["icmp", "tcp"], true);

            Assert.That(sortedProtocols.Select(protocol => protocol.Name), Is.EqualTo(["icmp", "tcp"]));
        }

        [Test]
        public void CustomSortProtocols_FullSetKeepsPreferredProtocolsFirst()
        {
            List<IpProtocol> protocols = BuildProtocols();

            List<IpProtocol> sortedProtocols = ProtocolDisplayHelper.CustomSortProtocols(protocols, ["icmp"], false);

            Assert.That(sortedProtocols.Select(protocol => protocol.Name), Is.EqualTo(["tcp", "udp", "icmp", "esp", "gre"]));
        }

        [Test]
        public void CustomSortProtocols_ReducedSetFallsBackToDefaultWhenConfigIsEmpty()
        {
            List<IpProtocol> protocols = BuildProtocols();

            List<IpProtocol> sortedProtocols = ProtocolDisplayHelper.CustomSortProtocols(protocols, [], true);

            Assert.That(sortedProtocols.Select(protocol => protocol.Name), Is.EqualTo(ProtocolNames.DefaultReducedProtocolNames));
        }

        [Test]
        public void CustomSortProtocols_ReducedSetIgnoresUnknownProtocolNames()
        {
            List<IpProtocol> protocols = BuildProtocols();

            List<IpProtocol> sortedProtocols = ProtocolDisplayHelper.CustomSortProtocols(protocols, ["bogus", "icmp", "tcp"], true);

            Assert.That(sortedProtocols.Select(protocol => protocol.Name), Is.EqualTo(["icmp", "tcp"]));
        }

        [Test]
        public void CustomSortProtocols_IgnoresCaseAndDuplicateNamesInConfiguration()
        {
            List<IpProtocol> protocols = BuildProtocols();

            List<IpProtocol> sortedProtocols = ProtocolDisplayHelper.CustomSortProtocols(protocols, ["TCP", "icmp", "tcp", "esp"], true);

            Assert.That(sortedProtocols.Select(protocol => protocol.Name), Is.EqualTo(["tcp", "icmp", "esp"]));
        }

        [Test]
        public void CustomSortProtocols_FullSetIgnoresUnassignedProtocol()
        {
            List<IpProtocol> protocols = BuildProtocols();

            List<IpProtocol> sortedProtocols = ProtocolDisplayHelper.CustomSortProtocols(protocols, ["icmp"], false);

            Assert.That(sortedProtocols.Select(protocol => protocol.Name), Does.Not.Contain("unassigned"));
        }

        [Test]
        public void ParseProtocolNames_InvalidJsonFallsBackToDefaultReducedSet()
        {
            List<string> parsedNames = ProtocolDisplayHelper.ParseProtocolNames("not-json");

            Assert.That(parsedNames, Is.EqualTo(ProtocolNames.DefaultReducedProtocolNames));
        }

        [Test]
        public void ParseProtocolNames_EmptyStringFallsBackToDefaultReducedSet()
        {
            List<string> parsedNames = ProtocolDisplayHelper.ParseProtocolNames("");

            Assert.That(parsedNames, Is.EqualTo(ProtocolNames.DefaultReducedProtocolNames));
        }

        [Test]
        public void ParseProtocolNames_TrimsAndDeduplicatesConfiguredValues()
        {
            List<string> parsedNames = ProtocolDisplayHelper.ParseProtocolNames("""[" tcp ","UDP","udp","","icmp"]""");

            Assert.That(parsedNames, Is.EqualTo(["tcp", "UDP", "icmp"]));
        }

        [Test]
        public void SerializeProtocolNames_RemovesWhitespaceOnlyEntriesAndKeepsOrder()
        {
            string serialized = ProtocolDisplayHelper.SerializeProtocolNames([" ", "icmp", " tcp ", "esp"]);

            Assert.That(serialized, Is.EqualTo("""["icmp","tcp","esp"]"""));
        }

        [Test]
        public void SerializeProtocolNames_TrimsAndDeduplicatesConfiguredValues()
        {
            string serialized = ProtocolDisplayHelper.SerializeProtocolNames([" tcp ", "UDP", "udp", "", "icmp"]);

            Assert.That(serialized, Is.EqualTo("""["tcp","UDP","icmp"]"""));
        }

        private static List<IpProtocol> BuildProtocols()
        {
            return
            [
                new() { Id = 1, Name = "udp" },
                new() { Id = 2, Name = "gre" },
                new() { Id = 3, Name = "icmp" },
                new() { Id = 4, Name = "unassigned" },
                new() { Id = 5, Name = "tcp" },
                new() { Id = 6, Name = "esp" }
            ];
        }
    }
}
