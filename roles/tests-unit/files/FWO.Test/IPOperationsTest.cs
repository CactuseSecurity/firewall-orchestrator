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

        [TestCase("192.0.2.10")]
        [TestCase("2001:db8::10")]
        [TestCase("::ffff:192.0.2.128")]
        public void TryParseIpAddressAndPrefix_BareAddress_ReturnsAddressWithoutPrefix(string input)
        {
            bool result = input.TryParseIpAddressAndPrefix(
                out IPAddress? address,
                out int? prefixLength);

            Assert.That(result, Is.True);
            Assert.That(address, Is.EqualTo(IPAddress.Parse(input)));
            Assert.That(prefixLength, Is.Null);
        }

        [TestCase("192.000.002.000/24", "192.0.2.0", 24)]
        [TestCase("2001:db8::192.000.002.000/120", "2001:db8::c000:200", 120)]
        [TestCase("192.0.2.0/24", "192.0.2.0", 24)]
        [TestCase("2001:db8::/32", "2001:db8::", 32)]
        [TestCase("0.0.0.0/0", "0.0.0.0", 0)]
        [TestCase("::/0", "::", 0)]
        [TestCase("192.0.2.10/32", "192.0.2.10", 32)]
        [TestCase("2001:db8::10/128", "2001:db8::10", 128)]
        [TestCase("::ffff:192.0.2.0/120", "::ffff:192.0.2.0", 120)]
        public void TryParseIpAddressAndPrefix_CanonicalNetwork_ReturnsAddressAndPrefix(
            string input,
            string expectedAddress,
            int expectedPrefixLength)
        {
            bool result = input.TryParseIpAddressAndPrefix(
                out IPAddress? address,
                out int? prefixLength);

            Assert.That(result, Is.True);
            Assert.That(address, Is.EqualTo(IPAddress.Parse(expectedAddress)));
            Assert.That(prefixLength, Is.EqualTo(expectedPrefixLength));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("not-an-ip")]
        [TestCase("192.0.2.1-192.0.2.10")]
        [TestCase("192.0.2.0/24/1")]
        [TestCase("/24")]
        [TestCase("192.0.2.0/")]
        [TestCase("192.0.2.0/not-a-prefix")]
        [TestCase("192.0.2.0/+24")]
        [TestCase("192.0.2.0/-1")]
        [TestCase("192.0.2.0/ 24")]
        [TestCase("192.0.2.0/24 ")]
        [TestCase("192.0.2.0/33")]
        [TestCase("2001:db8::/129")]
        [TestCase("192.168.1.42/24")]
        [TestCase("2001:db8::1/64")]
        [TestCase("192.168.1.0/255.255.255.0")]
        [TestCase("fe80::%3/64")]
        [TestCase("fe80::1%3")]
        [TestCase("127.1/32")]
        [TestCase("0x7f000001")]
        [TestCase("2130706433")]
        [TestCase("0300.0000.0002.0010")]
        public void TryParseIpAddressAndPrefix_InvalidInput_ReturnsFalseAndResetsOutputs(string? input)
        {
            IPAddress? address = IPAddress.Loopback;
            int? prefixLength = 32;

            Assert.DoesNotThrow(() =>
            {
                bool result = input.TryParseIpAddressAndPrefix(out address, out prefixLength);

                Assert.That(result, Is.False);
            });
            Assert.That(address, Is.Null);
            Assert.That(prefixLength, Is.Null);
        }

        [TestCase("192.168.1.0", 24, "192.168.1.0", "192.168.1.255")]
        [TestCase("0.0.0.0", 0, "0.0.0.0", "255.255.255.255")]
        [TestCase("192.0.2.42", 32, "192.0.2.42", "192.0.2.42")]
        [TestCase("2001:db8:1234::", 48, "2001:db8:1234::", "2001:db8:1234:ffff:ffff:ffff:ffff:ffff")]
        [TestCase("::", 0, "::", "ffff:ffff:ffff:ffff:ffff:ffff:ffff:ffff")]
        [TestCase("2001:db8::42", 128, "2001:db8::42", "2001:db8::42")]
        public void TryGetNetworkRange_CanonicalNetwork_ReturnsInclusiveRange(
            string address,
            int prefixLength,
            string expectedStart,
            string expectedEnd)
        {
            bool result = IpOperations.TryGetNetworkRange(
                IPAddress.Parse(address),
                prefixLength,
                out (IPAddress start, IPAddress end) ipRange);

            Assert.That(result, Is.True);
            Assert.That(ipRange.start, Is.EqualTo(IPAddress.Parse(expectedStart)));
            Assert.That(ipRange.end, Is.EqualTo(IPAddress.Parse(expectedEnd)));
        }

        [TestCase("192.168.1.0", -1)]
        [TestCase("192.168.1.0", 33)]
        [TestCase("2001:db8::", -1)]
        [TestCase("2001:db8::", 129)]
        [TestCase("192.168.1.42", 24)]
        [TestCase("2001:db8::1", 64)]
        [TestCase("fe80::%3", 64)]
        [TestCase("fe80::1%3", 128)]
        public void TryGetNetworkRange_InvalidNetwork_ReturnsFalseWithoutThrowing(
            string address,
            int prefixLength)
        {
            Assert.DoesNotThrow(() =>
            {
                bool result = IpOperations.TryGetNetworkRange(
                    IPAddress.Parse(address),
                    prefixLength,
                    out _);

                Assert.That(result, Is.False);
            });
        }

        [TestCase("192.000.002.010", "192.0.2.10")]
        [TestCase("010.008.009.010", "10.8.9.10")]
        [TestCase("2001:db8::192.000.002.010", "2001:db8::c000:20a")]
        [TestCase("::ffff:192.000.002.010", "::ffff:192.0.2.10")]
        [TestCase("2001:0db8:0000:0000:0000:0000:0000:0010", "2001:db8::10")]
        public void TryParseIpAddress_DecimalOctetsAndStandardIpv6_ReturnsCanonicalAddress(string input, string expected)
        {
            Assert.That(IpOperations.TryParseIpAddress(input, out IPAddress? address), Is.True);
            Assert.That(address!.ToString(), Is.EqualTo(expected));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("127.1")]
        [TestCase("2130706433")]
        [TestCase("0x7f000001")]
        [TestCase("0x7f.0.0.1")]
        [TestCase("0300.0000.0002.0010")]
        [TestCase("192.0.2.256")]
        [TestCase("192..2.10")]
        [TestCase("192.0.2.+10")]
        [TestCase("192.0.2.10 ")]
        [TestCase("[2001:db8::10]")]
        [TestCase("fe80::1%3")]
        [TestCase("fe80::1%0")]
        [TestCase("fe80::1%eth0")]
        [TestCase("2001:db8::127.1")]
        [TestCase("2001:db8::0x7f.0.0.1")]
        [TestCase("2001:db8::0300.0000.0002.0010")]
        public void TryParseIpAddress_InvalidInput_ResetsOutput(string? input)
        {
            Assert.That(IpOperations.TryParseIpAddress(input, out IPAddress? address), Is.False);
            Assert.That(address, Is.Null);
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
        public void RangeOverlapExists_OverlappingIPv6_ReturnsTrue()
        {
            IPAddressRange a = IPAddressRange.Parse("2001:db8::1-2001:db8::10");
            IPAddressRange b = IPAddressRange.Parse("2001:db8::5-2001:db8::20");

            bool result = IpOperations.RangeOverlapExists(a, b);

            Assert.That(result);
        }

        [Test]
        public void RangeOverlapExists_MixedFamilies_ReturnsFalse()
        {
            IPAddressRange ipv4Range = IPAddressRange.Parse("10.0.0.1-10.0.0.10");
            IPAddressRange ipv6Range = IPAddressRange.Parse("2001:db8::1-2001:db8::10");

            bool result = IpOperations.RangeOverlapExists(ipv4Range, ipv6Range);

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
        public void GetIPAdressRange_Range_Fails_InvalidRange()
        {
            // Arrange
            string input = "10.0.0.235-10.0.0.43";

            // Act
            TestDelegate action = () => IpOperations.GetIPAdressRange(input);

            // Assert
            Assert.Throws<FormatException>(action);
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
        public void ToDotNotation_UnalignedRange_ReturnsSmallestContainingNetwork()
        {
            string s = IpOperations.ToDotNotation("192.168.1.1", "192.168.1.9");

            ClassicAssert.AreEqual("192.168.1.0/255.255.255.240", s);
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
            if (IPNetwork2.TryParseRange("192.168.10.0-192.168.10.127", out IEnumerable<IPNetwork2>? a)) nets.AddRange(a);
            if (IPNetwork2.TryParseRange("192.168.10.128-192.168.10.255", out IEnumerable<IPNetwork2>? b)) nets.AddRange(b);

            // Act
            List<IPAddressRange> merged = IpOperations.ToMergedRanges(nets);

            // Assert
            ClassicAssert.AreEqual(1, merged.Count);
            ClassicAssert.AreEqual("192.168.10.0", merged[0].Begin.ToString());
            ClassicAssert.AreEqual("192.168.10.255", merged[0].End.ToString());
        }

        [Test]
        public void GetIntersection_OverlapExists_ReturnsIntersection()
        {
            var rangeA = new IPAddressRange(IPAddress.Parse("192.168.1.0"), IPAddress.Parse("192.168.1.255"));
            var rangeB = new IPAddressRange(IPAddress.Parse("192.168.1.128"), IPAddress.Parse("192.168.2.0"));

            var intersection = IpOperations.GetIntersection(rangeA, rangeB);

            Assert.IsNotNull(intersection);
            Assert.AreEqual("192.168.1.128", intersection!.Begin.ToString());
            Assert.AreEqual("192.168.1.255", intersection!.End.ToString());
        }

        [Test]
        public void GetIntersection_SwappedRanges_ReturnsSameIntersection()
        {
            var rangeA = new IPAddressRange(IPAddress.Parse("192.168.1.128"), IPAddress.Parse("192.168.2.0"));
            var rangeB = new IPAddressRange(IPAddress.Parse("192.168.1.0"), IPAddress.Parse("192.168.1.255"));

            var intersection = IpOperations.GetIntersection(rangeA, rangeB);

            Assert.IsNotNull(intersection);
            Assert.AreEqual("192.168.1.128", intersection!.Begin.ToString());
            Assert.AreEqual("192.168.1.255", intersection.End.ToString());
        }

        [Test]
        public void GetIntersection_TouchingRanges_ReturnsSingleIp()
        {
            var rangeA = new IPAddressRange(IPAddress.Parse("192.168.1.0"), IPAddress.Parse("192.168.1.10"));
            var rangeB = new IPAddressRange(IPAddress.Parse("192.168.1.10"), IPAddress.Parse("192.168.1.20"));

            var intersection = IpOperations.GetIntersection(rangeA, rangeB);

            Assert.IsNotNull(intersection);
            Assert.AreEqual("192.168.1.10", intersection!.Begin.ToString());
            Assert.AreEqual("192.168.1.10", intersection.End.ToString());
        }

        [Test]
        public void GetIntersection_OneInsideAnother_ReturnsInnerRange()
        {
            var rangeA = new IPAddressRange(IPAddress.Parse("192.168.1.0"), IPAddress.Parse("192.168.1.255"));
            var rangeB = new IPAddressRange(IPAddress.Parse("192.168.1.50"), IPAddress.Parse("192.168.1.100"));

            var intersection = IpOperations.GetIntersection(rangeA, rangeB);

            Assert.IsNotNull(intersection);
            Assert.AreEqual("192.168.1.50", intersection!.Begin.ToString());
            Assert.AreEqual("192.168.1.100", intersection.End.ToString());
        }

        [Test]
        public void GetIntersection_NoOverlap_ReturnsNull()
        {
            var rangeA = new IPAddressRange(IPAddress.Parse("10.0.0.0"), IPAddress.Parse("10.0.0.255"));
            var rangeB = new IPAddressRange(IPAddress.Parse("192.168.1.0"), IPAddress.Parse("192.168.1.255"));

            var intersection = IpOperations.GetIntersection(rangeA, rangeB);

            Assert.IsNull(intersection);
        }

        [Test]
        public void GetIntersection_IPv6Overlap_ReturnsIntersection()
        {
            var rangeA = new IPAddressRange(IPAddress.Parse("2001:db8::1"), IPAddress.Parse("2001:db8::ff"));
            var rangeB = new IPAddressRange(IPAddress.Parse("2001:db8::80"), IPAddress.Parse("2001:db8::1ff"));

            var intersection = IpOperations.GetIntersection(rangeA, rangeB);

            Assert.IsNotNull(intersection);
            Assert.AreEqual("2001:db8::80", intersection!.Begin.ToString());
            Assert.AreEqual("2001:db8::ff", intersection!.End.ToString());
        }

        [Test]
        public void GetIntersection_MixedIpVersions_ReturnsNull()
        {
            var rangeA = new IPAddressRange(IPAddress.Parse("192.168.1.0"), IPAddress.Parse("192.168.1.255"));
            var rangeB = new IPAddressRange(IPAddress.Parse("2001:db8::1"), IPAddress.Parse("2001:db8::ff"));

            var intersection = IpOperations.GetIntersection(rangeA, rangeB);

            Assert.IsNull(intersection);
        }

        [Test]
        public void GetIntersection_SmallIPv6Range_WorksCorrectly()
        {
            var rangeA = new IPAddressRange(IPAddress.Parse("2001:db8::1"), IPAddress.Parse("2001:db8::2"));
            var rangeB = new IPAddressRange(IPAddress.Parse("2001:db8::2"), IPAddress.Parse("2001:db8::3"));

            var intersection = IpOperations.GetIntersection(rangeA, rangeB);

            Assert.IsNotNull(intersection);
            Assert.AreEqual("2001:db8::2", intersection!.Begin.ToString());
            Assert.AreEqual("2001:db8::2", intersection.End.ToString());
        }
        [Test]
        public void ToCompactNotation_SingleAddress_ReturnsIpWithoutMask()
        {
            Assert.AreEqual("10.1.0.1", IpOperations.ToCompactNotation("10.1.0.1/32", "10.1.0.1/32"));
            Assert.AreEqual("10.1.0.1", IpOperations.ToCompactNotation("10.1.0.1", "10.1.0.1"));
            Assert.AreEqual("10.1.0.1", IpOperations.ToCompactNotation("10.1.0.1/32", ""));
        }

        [Test]
        public void ToCompactNotation_Subnet_ReturnsCidr()
        {
            Assert.AreEqual("10.2.0.0/24", IpOperations.ToCompactNotation("10.2.0.0/24", "10.2.0.255/24"));
            Assert.AreEqual("10.2.0.0/24", IpOperations.ToCompactNotation("10.2.0.0/24", ""));
            Assert.AreEqual("10.2.0.0/31", IpOperations.ToCompactNotation("10.2.0.0", "10.2.0.1"));
            Assert.AreEqual("0.0.0.0/0", IpOperations.ToCompactNotation("0.0.0.0", "255.255.255.255"));
        }

        [Test]
        public void ToCompactNotation_Range_ReturnsStartAndEndWithoutMask()
        {
            Assert.AreEqual("10.3.0.1-10.3.0.9", IpOperations.ToCompactNotation("10.3.0.1/32", "10.3.0.9/32"));
            Assert.AreEqual("10.3.0.1-10.3.1.0", IpOperations.ToCompactNotation("10.3.0.1", "10.3.1.0"));
        }

        [Test]
        public void ToCompactNotation_IPv6_ReturnsCompactNotation()
        {
            Assert.AreEqual("2001:db8::1", IpOperations.ToCompactNotation("2001:db8::1/128", "2001:db8::1/128"));
            Assert.AreEqual("2001:db8::/120", IpOperations.ToCompactNotation("2001:db8::", "2001:db8::ff"));
            Assert.AreEqual("2001:db8::1-2001:db8::9", IpOperations.ToCompactNotation("2001:db8::1", "2001:db8::9"));
        }

        [Test]
        public void ToCompactNotation_InvalidOrMixedInput_ReturnsStartUnchanged()
        {
            Assert.AreEqual("not-an-ip", IpOperations.ToCompactNotation("not-an-ip", "10.0.0.1"));
            Assert.AreEqual("10.0.0.1", IpOperations.ToCompactNotation("10.0.0.1", "2001:db8::1"));
        }
    }
}
