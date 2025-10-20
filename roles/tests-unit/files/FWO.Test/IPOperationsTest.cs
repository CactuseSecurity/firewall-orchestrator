using System;
using System.Collections.Generic;
using System.Linq;
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
        // --- SplitIpToRange ---
        [Test]
        public void SplitIpToRange_Cidr_ReturnsStartEnd()
        {
            var (start, end) = IpOperations.SplitIpToRange("192.168.1.0/24");
            ClassicAssert.AreEqual("192.168.1.0", start);
            ClassicAssert.AreEqual("192.168.1.255", end);
        }

        [Test]
        public void SplitIpToRange_Range_ReturnsStartEnd()
        {
            var (start, end) = IpOperations.SplitIpToRange("10.0.0.5-10.0.0.9");
            ClassicAssert.AreEqual("10.0.0.5", start);
            ClassicAssert.AreEqual("10.0.0.9", end);
        }

        [Test]
        public void SplitIpToRange_Single_ReturnsSame()
        {
            var (start, end) = IpOperations.SplitIpToRange("8.8.8.8");
            ClassicAssert.AreEqual("8.8.8.8", start);
            ClassicAssert.AreEqual("8.8.8.8", end);
        }

        // --- TryParseIPStringToRange ---
        [Test]
        public void TryParseIPStringToRange_ValidIPv4_StrictTrue_Succeeds()
        {
            var ok = IpOperations.TryParseIPStringToRange("192.168.0.1", out var range, strictv4Parse: true);
            Assert.That(ok);
            ClassicAssert.AreEqual("192.168.0.1", range.Start);
            ClassicAssert.AreEqual("192.168.0.1", range.End);
        }

        [Test]
        public void TryParseIPStringToRange_InvalidIPv4_StrictTrue_Fails()
        {
            var ok = IpOperations.TryParseIPStringToRange("999.168.0.1", out var _ , strictv4Parse: true);
            Assert.That(!ok);
        }

        // --- TryParseIPString<T> ---
        [Test]
        public void TryParseIPString_StringTuple_Succeeds()
        {
            var ok = IpOperations.TryParseIPString<(string, string)>("10.1.2.3-10.1.2.10", out var tuple);
            Assert.That(ok);
            ClassicAssert.AreEqual("10.1.2.3", tuple.Item1);
            ClassicAssert.AreEqual("10.1.2.10", tuple.Item2);
        }

        [Test]
        public void TryParseIPString_IPAddressRange_Succeeds()
        {
            var ok = IpOperations.TryParseIPString<IPAddressRange>("172.16.0.0/30", out var range);
            Assert.That(ok);
            ClassicAssert.AreEqual("172.16.0.0", range!.Begin.ToString());
            ClassicAssert.AreEqual("172.16.0.3", range.End.ToString());
        }

        [Test]
        public void TryParseIPString_IPAddressTuple_Succeeds()
        {
            var ok = IpOperations.TryParseIPString<(IPAddress, IPAddress)>("192.0.2.1-192.0.2.5", out var tup);
            Assert.That(ok);
            ClassicAssert.AreEqual(IPAddress.Parse("192.0.2.1"), tup!.Item1);
            ClassicAssert.AreEqual(IPAddress.Parse("192.0.2.5"), tup.Item2);
        }

        [Test]
        public void TryParseIPString_UnsupportedType_Fails()
        {
            var ok = IpOperations.TryParseIPString<int>("192.168.0.1", out var _);
            Assert.That(!ok);
        }

        // --- GetObjectType ---
        [Test]
        public void GetObjectType_HostAndNetworkAndRange()
        {
            // We assert against ObjectType constants if available in the project
            ClassicAssert.AreEqual(ObjectType.Host, IpOperations.GetObjectType("192.168.0.10", ""));
            ClassicAssert.AreEqual(ObjectType.Network, IpOperations.GetObjectType("192.168.0.0/24", ""));
            ClassicAssert.AreEqual(ObjectType.IPRange, IpOperations.GetObjectType("10.0.0.1", "10.0.0.5"));
        }

        // --- RangeOverlapExists ---
        [Test]
        public void RangeOverlapExists_Overlapping_ReturnsTrue()
        {
            var a = IPAddressRange.Parse("10.0.0.1-10.0.0.10");
            var b = IPAddressRange.Parse("10.0.0.5-10.0.0.20");
            Assert.That(IpOperations.RangeOverlapExists(a, b));
        }

        [Test]
        public void RangeOverlapExists_NonOverlapping_ReturnsFalse()
        {
            var a = IPAddressRange.Parse("10.0.0.1-10.0.0.10");
            var b = IPAddressRange.Parse("10.0.0.20-10.0.0.30");
            Assert.That(!IpOperations.RangeOverlapExists(a, b));
        }

        // --- IpToUint / UintToIp ---
        [Test]
        public void IpToUint_And_Back_Roundtrip()
        {
            var ip = IPAddress.Parse("1.2.3.4");
            uint u = IpOperations.IpToUint(ip);
            var back = IpOperations.UintToIp(u);
            ClassicAssert.AreEqual(ip, back);
        }

        // --- CheckOverlap ---
        [Test]
        public void CheckOverlap_MixedFamilies_ReturnsFalse()
        {
            Assert.That(!IpOperations.CheckOverlap("192.168.0.0/24", "2001:db8::/32"));
        }

        [Test]
        public void CheckOverlap_OverlappingStrings_ReturnsTrue()
        {
            Assert.That(IpOperations.CheckOverlap("10.0.0.0/25", "10.0.0.64-10.0.0.200"));
        }

        [Test]
        public void CheckOverlap_NonOverlappingStrings_ReturnsFalse()
        {
            Assert.That(!IpOperations.CheckOverlap("10.0.1.0/24", "10.0.2.0/24"));
        }

        // --- GetIPAdressRange ---
        [Test]
        public void GetIPAdressRange_Cidr()
        {
            var r = IpOperations.GetIPAdressRange("192.168.2.0/30");
            ClassicAssert.AreEqual("192.168.2.0", r.Begin.ToString());
            ClassicAssert.AreEqual("192.168.2.3", r.End.ToString());
        }

        [Test]
        public void GetIPAdressRange_Range()
        {
            var r = IpOperations.GetIPAdressRange("10.10.10.1-10.10.10.9");
            ClassicAssert.AreEqual("10.10.10.1", r.Begin.ToString());
            ClassicAssert.AreEqual("10.10.10.9", r.End.ToString());
        }

        [Test]
        public void GetIPAdressRange_Single()
        {
            var r = IpOperations.GetIPAdressRange("8.8.4.4");
            ClassicAssert.AreEqual("8.8.4.4", r.Begin.ToString());
            ClassicAssert.AreEqual("8.8.4.4", r.End.ToString());
        }

        // --- ToDotNotation ---
        [Test]
        public void ToDotNotation_ExactNetworkIPv4()
        {
            var s = IpOperations.ToDotNotation("192.168.1.0", "192.168.1.255");
            ClassicAssert.AreEqual("192.168.1.0/255.255.255.0", s);
        }

        [Test]
        public void ToDotNotation_SingleIP_Returns_32Mask()
        {
            var s = IpOperations.ToDotNotation("10.0.0.5", "10.0.0.5");
            ClassicAssert.AreEqual("10.0.0.5/255.255.255.255", s);
        }

        [Test]
        public void ToDotNotation_MismatchedFamilies_Throws()
        {
            Assert.Throws<ArgumentException>(() => IpOperations.ToDotNotation("10.0.0.1", "2001:db8::1"));
        }

        // --- CompareIpValues ---
        [Test]
        public void CompareIpValues_Works()
        {
            var a = IPAddress.Parse("10.0.0.1");
            var b = IPAddress.Parse("10.0.0.2");
            Assert.That(IpOperations.CompareIpValues(a, b) <  0);
            Assert.That(IpOperations.CompareIpValues(b, a) > 0);
            ClassicAssert.AreEqual(0, IpOperations.CompareIpValues(a, IPAddress.Parse("10.0.0.1")));
        }

        // --- CompareIpFamilies ---
        [Test]
        public void CompareIpFamilies_V4BeforeV6()
        {
            var v4 = IPAddress.Parse("192.0.2.1");
            var v6 = IPAddress.Parse("2001:db8::1");
            Assert.That(IpOperations.CompareIpFamilies(v4, v6) < 0);
            Assert.That(IpOperations.CompareIpFamilies(v6, v4) > 0);
            ClassicAssert.AreEqual(0, IpOperations.CompareIpFamilies(v4, IPAddress.Parse("198.51.100.2")));
        }

        // --- Subtract (IPAddressRange vs List<IPAddressRange>) ---
        [Test]
        public void Subtract_IPAddressRange_List_RemovesHoleAndMerges()
        {
            var source = new IPAddressRange(IPAddress.Parse("10.0.0.0"), IPAddress.Parse("10.0.0.255")); // /24
            var subtract = new List<IPAddressRange>
            {
                new IPAddressRange(IPAddress.Parse("10.0.0.10"), IPAddress.Parse("10.0.0.20"))
            };

            var result = IpOperations.Subtract(source, subtract);

            // expect two ranges: 10.0.0.0-10.0.0.9 and 10.0.0.21-10.0.0.255
            ClassicAssert.AreEqual(2, result.Count);
            ClassicAssert.AreEqual("10.0.0.0", result[0].Begin.ToString());
            ClassicAssert.AreEqual("10.0.0.9", result[0].End.ToString());
            ClassicAssert.AreEqual("10.0.0.21", result[1].Begin.ToString());
            ClassicAssert.AreEqual("10.0.0.255", result[1].End.ToString());
        }

        // --- ToMergedRanges (via IPNetwork2) ---
        [Test]
        public void ToMergedRanges_MergesAdjacentNetworks()
        {
            // two adjacent /25 inside 192.168.10.0/24
            var nets = new List<IPNetwork2>();

            if (IPNetwork2.TryParseRange("192.168.10.0-192.168.10.127", out IEnumerable<IPNetwork2> a)) nets.AddRange(a);
            if (IPNetwork2.TryParseRange("192.168.10.128-192.168.10.255", out IEnumerable<IPNetwork2> b)) nets.AddRange(b);

            var merged = IpOperations.ToMergedRanges(nets);
            
            ClassicAssert.AreEqual(1, merged.Count);
            ClassicAssert.AreEqual("192.168.10.0", merged[0].Begin.ToString());
            ClassicAssert.AreEqual("192.168.10.255", merged[0].End.ToString());
        }
    }
}
