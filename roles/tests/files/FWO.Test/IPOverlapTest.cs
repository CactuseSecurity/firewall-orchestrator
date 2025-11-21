using NUnit.Framework;
using FWO.Basics;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class IPOverlapTest
    {
        [Test]
        [Parallelizable]
        public void TestIPOverlaps()
        {
            List<string> areaIPs = [
                "2001:db8::1-2001:db8::4",
                "1.2.3.0/24",
                "2.2.2.2",
                "3.3.3.3-3.3.3.4",
                "10.121.254.128/27",
                "172.0.0.0/7",
                ];

            List<string> ipsShouldOverlap = [
                "2001:db8::3",
                "1.2.3.0/24",
                "2.2.2.2",
                "3.3.3.3-3.3.3.4",
                "10.121.254.128/27",
                "172.0.0.0/7",
                ];

            List<string> ipsShouldNotOverlap = [
                "2001:db8::",
                "1.2.2.0/24",
                "1.1.1.1",
                "2.1.1.1-2.1.1.4",
                "9.0.254.128/27",
                "171.0.0.0/7",
             ];

            int ipsThatOverlapped = 0;

            foreach (var subnet in areaIPs)
            {
                foreach (var ipOverlap in ipsShouldOverlap)
                {
                    if (subnet.CheckOverlap(ipOverlap))
                    {
                        ipsThatOverlapped++;
                    }
                }

                foreach (var ipNotOverlap in ipsShouldNotOverlap)
                {
                    if (subnet.CheckOverlap(ipNotOverlap))
                    {
                        ipsThatOverlapped++;
                    }
                }
            }

            Assert.That(Is.Equals(ipsThatOverlapped, ipsShouldOverlap.Count));
        }
        
    }
}
