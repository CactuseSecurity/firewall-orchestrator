using System.Net;
using NetTools;
using NUnit.Framework;
using FWO.Basics;
using NUnit.Framework.Legacy;

namespace FWO.Test
{
    [TestFixture]
    public class IpOperationsTests
    {
        [Test]
        public void SplitIpToRange_Cidr_ReturnsStartEnd()
        {
            // Arrange
            string input = "192.168.1.0/24";

            // Act
            (string start, string end) = IpOperations.SplitIpToRange(input);

            // Assert
            ClassicAssert.AreEqual("192.168.1.0", start);
            ClassicAssert.AreEqual("192.168.1.255", end);
        }

        [Test]
        public void SplitIpToRange_Range_ReturnsStartEnd()
        {
            // Arrange
            string input = "10.0.0.5-10.0.0.9";

            // Act
            (string start, string end) = IpOperations.SplitIpToRange(input);

            // Assert
            ClassicAssert.AreEqual("10.0.0.5", start);
            ClassicAssert.AreEqual("10.0.0.9", end);
        }

        [Test]
        public void SplitIpToRange_Single_ReturnsSame()
        {
            // Arrange
            string input = "8.8.8.8";

            // Act
            (string start, string end) = IpOperations.SplitIpToRange(input);

            // Assert
            ClassicAssert.AreEqual("8.8.8.8", start);
            ClassicAssert.AreEqual("8.8.8.8", end);
        }

        [Test]
        public void TryParseIPStringToRange_ValidIPv4_StrictTrue_Succeeds()
        {
            // Arrange
            string input = "192.168.0.1";

            // Act
            bool ok = IpOperations.TryParseIPStringToRange(input, out (string start, string end) range, strictv4Parse: true);

            // Assert
            Assert.That(ok);
            ClassicAssert.AreEqual("192.168.0.1", range.start);
            ClassicAssert.AreEqual("192.168.0.1", range.end);
        }

        [Test]
        public void TryParseIPStringToRange_InvalidIPv4_StrictTrue_Fails()
        {
            // Arrange
            string input = "999.168.0.1";

            // Act
            bool ok = IpOperations.TryParseIPStringToRange(input, out (string start, string end) _, strictv4Parse: true);

            // Assert
            Assert.That(!ok);
        }

        [Test]
        public void TryParseIPString_StringTuple_Succeeds()
        {
            // Arrange
            string input = "10.1.2.3-10.1.2.10";

            // Act
            bool ok = IpOperations.TryParseIPString(input, out (string, string) tuple);

            // Assert
            Assert.That(ok);
            ClassicAssert.AreEqual("10.1.2.3", tuple.Item1);
            ClassicAssert.AreEqual("10.1.2.10", tuple.Item2);
        }

        [Test]
        public void TryParseIPString_FullRangeIPv4_Succeeds()
        {
            // Arrange
            string input = "0.0.0.0/0";

            // Act
            bool ok = IpOperations.TryParseIPString(input, out IPAddressRange? range);

            // Assert
            Assert.That(ok);
            ClassicAssert.AreEqual("0.0.0.0", range!.Begin.ToString());
            ClassicAssert.AreEqual("255.255.255.255", range.End.ToString());
        }

        [Test]
        public void TryParseIPString_IPAddressRange_Succeeds()
        {
            // Arrange
            string input = "172.16.0.0/30";

            // Act
            bool ok = IpOperations.TryParseIPString(input, out IPAddressRange? range);

            // Assert
            Assert.That(ok);
            ClassicAssert.AreEqual("172.16.0.0", range!.Begin.ToString());
            ClassicAssert.AreEqual("172.16.0.3", range.End.ToString());
        }

        [Test]
        public void TryParseIPString_IPAddressTuple_Succeeds()
        {
            // Arrange
            string input = "192.0.2.1-192.0.2.5";

            // Act
            bool ok = IpOperations.TryParseIPString(input, out (IPAddress, IPAddress) tup);

            // Assert
            Assert.That(ok);
            ClassicAssert.AreEqual(IPAddress.Parse("192.0.2.1"), tup!.Item1);
            ClassicAssert.AreEqual(IPAddress.Parse("192.0.2.5"), tup.Item2);
        }

        [Test]
        public void TryParseIPString_UnsupportedType_Fails()
        {
            // Arrange
            string input = "192.168.0.1";

            // Act
            bool ok = IpOperations.TryParseIPString(input, out int _);

            // Assert
            Assert.That(!ok);
        }

        [Test]
        public void GetObjectType_HostAndNetworkAndRange_Succeeds()
        {
            // Arrange
            string host = "192.168.0.10";
            string network = "192.168.0.0/24";
            string rStart = "10.0.0.1";
            string rEnd = "10.0.0.5";

            // Act
            string hostType = IpOperations.GetObjectType(host, "");
            string networkType = IpOperations.GetObjectType(network, "");
            string rangeType = IpOperations.GetObjectType(rStart, rEnd);

            // Assert
            ClassicAssert.AreEqual(ObjectType.Host, hostType);
            ClassicAssert.AreEqual(ObjectType.Network, networkType);
            ClassicAssert.AreEqual(ObjectType.IPRange, rangeType);
        }

        [Test]
        public void RangeOverlapExists_Overlapping_ReturnsTrue()
        {
            // Arrange
            IPAddressRange a = IPAddressRange.Parse("10.0.0.1-10.0.0.10");
            IPAddressRange b = IPAddressRange.Parse("10.0.0.5-10.0.0.20");

            // Act
            bool result = IpOperations.RangeOverlapExists(a, b);

            // Assert
            Assert.That(result);
        }

        [Test]
        public void RangeOverlapExists_NonOverlapping_ReturnsFalse()
        {
            // Arrange
            IPAddressRange a = IPAddressRange.Parse("10.0.0.1-10.0.0.10");
            IPAddressRange b = IPAddressRange.Parse("10.0.0.20-10.0.0.30");

            // Act
            bool result = IpOperations.RangeOverlapExists(a, b);

            // Assert
            Assert.That(!result);
        }

        [Test]
        public void IpToUint_And_Back_Roundtrip()
        {
            // Arrange
            IPAddress ip = IPAddress.Parse("1.2.3.4");

            // Act
            uint u = IpOperations.IpToUint(ip);
            IPAddress back = IpOperations.UintToIp(u);

            // Assert
            ClassicAssert.AreEqual(ip, back);
        }

        [Test]
        public void CheckOverlap_MixedFamilies_ReturnsFalse()
        {
            // Arrange
            string left = "192.168.0.0/24";
            string right = "2001:db8::/32";

            // Act
            bool result = IpOperations.CheckOverlap(left, right);

            // Assert
            Assert.That(!result);
        }

        [Test]
        public void CheckOverlap_OverlappingStrings_ReturnsTrue()
        {
            // Arrange
            string left = "10.0.0.0/25";
            string right = "10.0.0.64-10.0.0.200";

            // Act
            bool result = IpOperations.CheckOverlap(left, right);

            // Assert
            Assert.That(result);
        }

        [Test]
        public void CheckOverlap_NonOverlappingStrings_ReturnsFalse()
        {
            // Arrange
            string left = "10.0.1.0/24";
            string right = "10.0.2.0/24";

            // Act
            bool result = IpOperations.CheckOverlap(left, right);

            // Assert
            Assert.That(!result);
        }

                [Test]
        public void GetIPAdressRange_FullRangeIPv4_Succeeds()
        {
            // Arrange
            string input = "0.0.0.0/0";

            // Act
            IPAddressRange r = IpOperations.GetIPAdressRange(input);

            // Assert
            ClassicAssert.AreEqual("0.0.0.0", r.Begin.ToString());
            ClassicAssert.AreEqual("255.255.255.255", r.End.ToString());
        }

        [Test]
        public void GetIPAdressRange_Cidr_Succeeds()
        {
            // Arrange
            string input = "192.168.2.0/30";

            // Act
            IPAddressRange r = IpOperations.GetIPAdressRange(input);

            // Assert
            ClassicAssert.AreEqual("192.168.2.0", r.Begin.ToString());
            ClassicAssert.AreEqual("192.168.2.3", r.End.ToString());
        }

        [Test]
        public void GetIPAdressRange_Range_Succeeds()
        {
            // Arrange
            string input = "10.10.10.1-10.10.10.9";

            // Act
            IPAddressRange r = IpOperations.GetIPAdressRange(input);

            // Assert
            ClassicAssert.AreEqual("10.10.10.1", r.Begin.ToString());
            ClassicAssert.AreEqual("10.10.10.9", r.End.ToString());
        }

        [Test]
        public void GetIPAdressRange_Single_Succeeds()
        {
            // Arrange
            string input = "8.8.4.4";

            // Act
            IPAddressRange r = IpOperations.GetIPAdressRange(input);

            // Assert
            ClassicAssert.AreEqual("8.8.4.4", r.Begin.ToString());
            ClassicAssert.AreEqual("8.8.4.4", r.End.ToString());
        }

        [Test]
        public void ToDotNotation_ExactNetworkIPv4_Succeeds()
        {
            // Arrange
            string start = "192.168.1.0";
            string end = "192.168.1.255";

            // Act
            string s = IpOperations.ToDotNotation(start, end);

            // Assert
            ClassicAssert.AreEqual("192.168.1.0/255.255.255.0", s);
        }

        [Test]
        public void ToDotNotation_SingleIP__Succeeds()
        {
            // Arrange
            string start = "10.0.0.5";
            string end = "10.0.0.5";

            // Act
            string s = IpOperations.ToDotNotation(start, end);

            // Assert
            ClassicAssert.AreEqual("10.0.0.5/255.255.255.255", s);
        }

        [Test]
        public void ToDotNotation_MismatchedFamilies_Throws()
        {
            // Arrange
            string start = "10.0.0.1";
            string end = "2001:db8::1";

            // Act & Assert
            Assert.Throws<ArgumentException>(() => IpOperations.ToDotNotation(start, end));
        }

        [Test]
        public void CompareIpValues_Basic_Works()
        {
            // Arrange
            IPAddress a = IPAddress.Parse("10.0.0.1");
            IPAddress b = IPAddress.Parse("10.0.0.2");

            // Act
            int ab = IpOperations.CompareIpValues(a, b);
            int ba = IpOperations.CompareIpValues(b, a);
            int aa = IpOperations.CompareIpValues(a, IPAddress.Parse("10.0.0.1"));

            // Assert
            Assert.That(ab < 0);
            Assert.That(ba > 0);
            ClassicAssert.AreEqual(0, aa);
        }

        [Test]
        public void CompareIpFamilies_V4AndV6_V4BeforeV6()
        {
            // Arrange
            IPAddress v4 = IPAddress.Parse("192.0.2.1");
            IPAddress v6 = IPAddress.Parse("2001:db8::1");

            // Act
            int v4v6 = IpOperations.CompareIpFamilies(v4, v6);
            int v6v4 = IpOperations.CompareIpFamilies(v6, v4);
            int v4v4 = IpOperations.CompareIpFamilies(v4, IPAddress.Parse("198.51.100.2"));

            // Assert
            Assert.That(v4v6 < 0);
            Assert.That(v6v4 > 0);
            ClassicAssert.AreEqual(0, v4v4);
        }

        [Test]
        public void Subtract_IPAddressRangeList_Succeeds()
        {
            // Arrange
            IPAddressRange source = new IPAddressRange(IPAddress.Parse("10.0.0.0"), IPAddress.Parse("10.0.0.255"));
            List<IPAddressRange> subtract = new List<IPAddressRange>
            {
                new IPAddressRange(IPAddress.Parse("10.0.0.10"), IPAddress.Parse("10.0.0.20"))
            };

            // Act
            List<IPAddressRange> result = IpOperations.Subtract(source, subtract);

            // Assert
            ClassicAssert.AreEqual(2, result.Count);
            ClassicAssert.AreEqual("10.0.0.0", result[0].Begin.ToString());
            ClassicAssert.AreEqual("10.0.0.9", result[0].End.ToString());
            ClassicAssert.AreEqual("10.0.0.21", result[1].Begin.ToString());
            ClassicAssert.AreEqual("10.0.0.255", result[1].End.ToString());
        }

        [Test]
        public void ToMergedRanges_AdjacentNetworks_Succeeds()
        {
            // Arrange
            List<IPNetwork2> nets = new List<IPNetwork2>();
            if (IPNetwork2.TryParseRange("192.168.10.0-192.168.10.127", out IEnumerable<IPNetwork2> a)) nets.AddRange(a);
            if (IPNetwork2.TryParseRange("192.168.10.128-192.168.10.255", out IEnumerable<IPNetwork2> b)) nets.AddRange(b);

            // Act
            List<IPAddressRange> merged = IpOperations.ToMergedRanges(nets);

            // Assert
            ClassicAssert.AreEqual(1, merged.Count);
            ClassicAssert.AreEqual("192.168.10.0", merged[0].Begin.ToString());
            ClassicAssert.AreEqual("192.168.10.255", merged[0].End.ToString());
        }
    }
}
