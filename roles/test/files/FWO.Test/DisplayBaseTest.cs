using NUnit.Framework;
using FWO.GlobalConstants;
using FWO.Api.Data;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class DisplayBaseTest
    {

        static readonly string ip1 = "1.0.0.0";
        static readonly string ip2 = "1.0.0.0/32";
        static readonly string ip3 = "1.0.0.3/32";
        static readonly string ip4 = "1.0.1.3/32";
        static readonly string ip5 = "1.0.0.0/24";
        static readonly string ip6 = "1.0.0.0/31";
        static readonly string ip7 = "1.0.0.2/31";

        static readonly string ip11 = ":a:";
        static readonly string ip12 = ":a:/128";
        static readonly string ip13 = ":a:/111";

        static readonly NetworkService serv1 = new(){ Name = "Serv1", DestinationPort = 1000, Protocol = new(){ Name="TCP" }};
        static readonly NetworkService serv2 = new(){ Name = "Serv2", DestinationPort = 1000, DestinationPortEnd = 2000, Protocol = new(){ Name="UDP" }};
        static readonly NetworkService serv3 = new(){ Name = "Serv3", Protocol = new(){ Name="ESP" }};

        [SetUp]
        public void Initialize()
        {}

        [Test]
        public void TestGetNetmask()
        {
            Assert.AreEqual("", DisplayBase.GetNetmask(ip1));
            Assert.AreEqual("32", DisplayBase.GetNetmask(ip2));
            Assert.AreEqual("24", DisplayBase.GetNetmask(ip5));
            Assert.AreEqual("", DisplayBase.GetNetmask(ip11));
            Assert.AreEqual("111", DisplayBase.GetNetmask(ip13));
        }

        [Test]
        public void TestAutoDetectType()
        {
            Assert.AreEqual(ObjectType.Host, DisplayBase.AutoDetectType(ip1, ip1));
            Assert.AreEqual(ObjectType.Host, DisplayBase.AutoDetectType(ip1, ip2));
            Assert.AreEqual(ObjectType.Network, DisplayBase.AutoDetectType(ip2, ip3));
            Assert.AreEqual(ObjectType.IPRange, DisplayBase.AutoDetectType(ip2, ip4));
            Assert.AreEqual(ObjectType.Network, DisplayBase.AutoDetectType(ip5, ip5));
            // Assert.AreEqual(ObjectType.Network, DisplayBase.AutoDetectType(ip6, ip7)); // should detect this?
            Assert.AreEqual(ObjectType.IPRange, DisplayBase.AutoDetectType(ip6, ip7));

            Assert.AreEqual(ObjectType.Host, DisplayBase.AutoDetectType(ip11, ip11));
            Assert.AreEqual(ObjectType.Host, DisplayBase.AutoDetectType(ip11, ip12));
            Assert.AreEqual(ObjectType.Network, DisplayBase.AutoDetectType(ip13, ip13));
        }

        [Test]
        public void TestDisplayService()
        {
            Assert.AreEqual("Serv1 (1000/TCP)", DisplayBase.DisplayService(serv1, false).ToString());
            Assert.AreEqual("Serv2 (1000-2000/UDP)", DisplayBase.DisplayService(serv2, false).ToString());
            Assert.AreEqual("Serv3 (ESP)", DisplayBase.DisplayService(serv3, false).ToString());
            Assert.AreEqual("NewName (1000/TCP)", DisplayBase.DisplayService(serv1, false, "NewName").ToString());
            Assert.AreEqual("1000-2000/UDP", DisplayBase.DisplayService(serv2, true).ToString());
            Assert.AreEqual("ESP", DisplayBase.DisplayService(serv3, true).ToString());
        }
    }
}
