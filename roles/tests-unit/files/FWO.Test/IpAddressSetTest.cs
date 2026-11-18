using FWO.Basics;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Address sets compare and combine addresses independent of their notation: single addresses, CIDR blocks and ranges.
    /// </summary>
    [TestFixture]
    internal class IpAddressSetTest
    {
        private static readonly List<string> kRangeAsCidrBlocks = ["10.0.0.0/28", "10.0.0.16/31"];

        private static IpInterval Interval(string notation, string? end = null)
        {
            Assert.That(IpAddressSet.TryParseInterval(notation, end, out IpInterval interval), Is.True, notation);
            return interval;
        }

        private static IpAddressSet Set(params string[] notations) => IpAddressSet.Of(notations.Select(notation => Interval(notation)));

        [TestCase("1.2.3.4", "1.2.3.4/32")]
        [TestCase("1.2.3.4/32", "1.2.3.4/32")]
        [TestCase("2.3.4.0/24", "2.3.4.0/24")]
        [TestCase("1.1.1.0-1.1.1.17", "1.1.1.0-1.1.1.17")]
        [TestCase("10.0.0.0-10.0.0.255", "10.0.0.0/24")]
        [TestCase("2001:db8::1", "2001:db8::1/128")]
        [TestCase("2001:db8::/64", "2001:db8::/64")]
        public void Format_WritesCidrBlocksOrRanges(string notation, string expected)
        {
            Assert.That(IpAddressSet.Format(Interval(notation)), Is.EqualTo(expected));
        }

        [Test]
        public void TryParseInterval_TakesTheEndFromASeparateAddress()
        {
            Assert.That(IpAddressSet.Format(Interval("10.0.0.0/32", "10.0.0.255/32")), Is.EqualTo("10.0.0.0/24"));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("no address")]
        [TestCase("10.0.0.9-10.0.0.1")]
        public void TryParseInterval_RefusesInvalidAddresses(string? notation)
        {
            Assert.That(IpAddressSet.TryParseInterval(notation, out _), Is.False);
        }

        [Test]
        public void TryParseInterval_RefusesMixedFamilies()
        {
            Assert.That(IpAddressSet.TryParseInterval("10.0.0.1", "2001:db8::1", out _), Is.False);
        }

        [Test]
        public void Add_MergesAdjacentAndOverlappingIntervals()
        {
            IpAddressSet set = Set("10.0.0.0/28", "10.0.0.16/31", "10.0.0.5", "192.168.0.1");
            Assert.Multiple(() =>
            {
                Assert.That(set.Intervals, Has.Count.EqualTo(2));
                Assert.That(IpAddressSet.Format(set.Intervals[0]), Is.EqualTo("10.0.0.0-10.0.0.17"));
                Assert.That(set.SetEquals(Set("10.0.0.0-10.0.0.17", "192.168.0.1")), Is.True);
            });
        }

        [Test]
        public void Remove_SplitsIntervals()
        {
            IpAddressSet set = Set("10.0.0.0/24");
            set.Remove(Interval("10.0.0.10-10.0.0.19"));
            Assert.Multiple(() =>
            {
                Assert.That(set.Intervals, Has.Count.EqualTo(2));
                Assert.That(set.Contains(Interval("10.0.0.0-10.0.0.9")), Is.True);
                Assert.That(set.Contains(Interval("10.0.0.20-10.0.0.255")), Is.True);
                Assert.That(set.Overlaps(Interval("10.0.0.15")), Is.False);
            });
        }

        [Test]
        public void Remove_OfAnotherSetLeavesOtherFamiliesAlone()
        {
            IpAddressSet set = Set("10.0.0.0/30", "2001:db8::1");
            set.Remove(Set("10.0.0.0/30", "2001:db8::2"));
            Assert.That(set.SetEquals(Set("2001:db8::1")), Is.True);
        }

        [Test]
        public void SameAddresses_InDifferentNotationsAreEqual()
        {
            IpAddressSet range = Set("10.0.0.0-10.0.0.17");
            IpAddressSet blocks = IpAddressSet.Of(kRangeAsCidrBlocks.Select(block => Interval(block)));
            Assert.Multiple(() =>
            {
                Assert.That(range.SetEquals(blocks), Is.True);
                Assert.That(range.Contains(Interval("10.0.0.16/31")), Is.True);
                Assert.That(range.Contains(Interval("10.0.0.0/27")), Is.False);
                Assert.That(range.Overlaps(Interval("10.0.0.0/27")), Is.True);
            });
        }

        [Test]
        public void Copy_IsIndependent()
        {
            IpAddressSet set = Set("10.0.0.1");
            IpAddressSet copy = set.Copy();
            copy.Add(Interval("10.0.0.2"));
            Assert.Multiple(() =>
            {
                Assert.That(set.Intervals, Has.Count.EqualTo(1));
                Assert.That(copy.Contains(Interval("10.0.0.1-10.0.0.2")), Is.True);
                Assert.That(new IpAddressSet().IsEmpty, Is.True);
            });
        }
    }
}
