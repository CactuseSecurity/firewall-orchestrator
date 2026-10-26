using FWO.Data;
using FWO.Data.Workflow;
using NetTools;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Tests which fields of a request element are converted into its address range, used by the path analysis and the
    /// task bundling. The parsing itself is tested with <c>IpOperations.TryParseIPRange</c>.
    /// </summary>
    [TestFixture]
    [Parallelizable]
    internal class WfReqElementIpRangeTest
    {
        private static readonly List<WfReqElement> kMixedFieldElements =
        [
            new WfReqElement { Field = ElemFieldType.source.ToString(), IpString = "10.0.0.1" },
            new WfReqElement { Field = ElemFieldType.source.ToString(), IpString = "10.0.1.1", IpEnd = "10.0.1.9" },
            new WfReqElement { Field = ElemFieldType.source.ToString(), NetworkId = 4 },
            new WfReqElement { Field = ElemFieldType.source.ToString(), IpString = "10.0.0.9", IpEnd = "10.0.0.1" },
            new WfReqElement { Field = ElemFieldType.destination.ToString(), IpString = "2001:db8::1" }
        ];

        [Test]
        public void GetIpRanges_ConvertsTheUsableElementsOfTheFieldOnly()
        {
            List<IPAddressRange> sources = WfReqElement.GetIpRanges(kMixedFieldElements, ElemFieldType.source);
            List<IPAddressRange> destinations = WfReqElement.GetIpRanges(kMixedFieldElements, ElemFieldType.destination);

            Assert.Multiple(() =>
            {
                Assert.That(sources, Has.Count.EqualTo(2), "element without address and reversed range are skipped");
                Assert.That(sources[1].End.ToString(), Is.EqualTo("10.0.1.9"));
                Assert.That(destinations.Single().ToString(), Is.EqualTo("2001:db8::1"));
            });
        }

        [TestCase("10.0.3.0/24", "10.0.3.0", "10.0.3.255")]
        [TestCase("2001:db8:1::/48", "2001:db8:1::", "2001:db8:1:ffff:ffff:ffff:ffff:ffff")]
        public void GetIpRange_FallsBackToCidr(string cidr, string expectedBegin, string expectedEnd)
        {
            WfReqElement element = new() { Cidr = new Cidr(cidr) };

            IPAddressRange? range = element.GetIpRange();

            Assert.Multiple(() =>
            {
                Assert.That(range?.Begin.ToString(), Is.EqualTo(expectedBegin));
                Assert.That(range?.End.ToString(), Is.EqualTo(expectedEnd));
            });
        }

        [Test]
        public void GetIpRange_PrefersIpStringOverCidr()
        {
            WfReqElement element = new() { IpString = "10.0.0.1", Cidr = new Cidr("10.9.9.0/24") };

            Assert.That(element.GetIpRange()?.ToString(), Is.EqualTo("10.0.0.1"));
        }

        [Test]
        public void GetIpRange_WithoutAnyAddress_ReturnsNull()
        {
            Assert.That(new WfReqElement { NetworkId = 4 }.GetIpRange(), Is.Null);
        }
    }
}
