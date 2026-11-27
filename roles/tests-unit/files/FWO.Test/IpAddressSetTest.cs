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

        /// <summary>
        /// Random adds and removes of small IPv4 and IPv6 intervals give the same addresses as a plain set of numbers,
        /// and the intervals stay sorted, disjoint and not adjoining.
        /// </summary>
        [Test]
        public void AddAndRemove_MatchAPlainSetOfAddresses()
        {
            const int kOperations = 2000;
            const int kAddressSpace = 300;
            const int kMaxLength = 8;
            const int kSeed = 4711;
            Random random = new(kSeed);
            IpAddressSet set = new();
            HashSet<(System.Net.Sockets.AddressFamily, int)> expected = [];
            for (int operation = 0; operation < kOperations; operation++)
            {
                System.Net.Sockets.AddressFamily family = random.Next(2) == 0
                    ? System.Net.Sockets.AddressFamily.InterNetwork
                    : System.Net.Sockets.AddressFamily.InterNetworkV6;
                int start = random.Next(kAddressSpace);
                int end = start + random.Next(kMaxLength);
                IpInterval interval = new(family, start, end);
                bool add = random.Next(3) > 0;
                if (add)
                {
                    set.Add(interval);
                }
                else
                {
                    set.Remove(interval);
                }
                for (int address = start; address <= end; address++)
                {
                    if (add)
                    {
                        expected.Add((family, address));
                    }
                    else
                    {
                        expected.Remove((family, address));
                    }
                }
            }
            HashSet<(System.Net.Sockets.AddressFamily, int)> actual = [];
            foreach (IpInterval interval in set.Intervals)
            {
                for (int address = (int)interval.Start; address <= (int)interval.End; address++)
                {
                    actual.Add((interval.Family, address));
                }
            }
            Assert.Multiple(() =>
            {
                Assert.That(actual.SetEquals(expected), Is.True);
                Assert.That(IsSortedAndDisjoint(set.Intervals), Is.True);
                Assert.That(set.Contains(new IpInterval(System.Net.Sockets.AddressFamily.InterNetwork, 0, kAddressSpace + kMaxLength)),
                    Is.EqualTo(Enumerable.Range(0, kAddressSpace + kMaxLength + 1).All(address => expected.Contains((System.Net.Sockets.AddressFamily.InterNetwork, address)))));
            });
        }

        /// <summary>
        /// Adding many disjoint hosts stays fast: the intervals are found by binary search instead of a scan and a sort per add.
        /// </summary>
        [Test]
        public void Add_ManyDisjointHostsStaysFast()
        {
            const int kHosts = 20000;
            const int kMaxMilliseconds = 2000;
            IpAddressSet set = new();
            System.Diagnostics.Stopwatch watch = System.Diagnostics.Stopwatch.StartNew();
            for (int host = kHosts - 1; host >= 0; host--)
            {
                // every second address, so that no two hosts merge
                set.Add(new IpInterval(System.Net.Sockets.AddressFamily.InterNetwork, host * 2, host * 2));
            }
            watch.Stop();
            Assert.Multiple(() =>
            {
                Assert.That(set.Intervals, Has.Count.EqualTo(kHosts));
                Assert.That(IsSortedAndDisjoint(set.Intervals), Is.True);
                Assert.That(set.Contains(new IpInterval(System.Net.Sockets.AddressFamily.InterNetwork, 2, 2)), Is.True);
                Assert.That(set.Contains(new IpInterval(System.Net.Sockets.AddressFamily.InterNetwork, 3, 3)), Is.False);
                Assert.That(watch.ElapsedMilliseconds, Is.LessThan(kMaxMilliseconds));
            });
        }

        private static bool IsSortedAndDisjoint(IReadOnlyList<IpInterval> intervals)
        {
            for (int index = 1; index < intervals.Count; index++)
            {
                IpInterval previous = intervals[index - 1];
                IpInterval current = intervals[index];
                bool ordered = previous.Family < current.Family || (previous.Family == current.Family && previous.End + 1 < current.Start);
                if (!ordered)
                {
                    return false;
                }
            }
            return true;
        }
    }
}
