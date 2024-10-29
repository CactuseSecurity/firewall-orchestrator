using NUnit.Framework;
using NetTools;
using System.Net;
using FWO.FWO.Basics;

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
                    if (CheckOverlap(subnet, ipOverlap))
                    {
                        ipsThatOverlapped++;
                    }
                }

                foreach (var ipNotOverlap in ipsShouldNotOverlap)
                {
                    if (CheckOverlap(subnet, ipNotOverlap))
                    {
                        ipsThatOverlapped++;
                    }
                }
            }

            Assert.That(Is.Equals(ipsThatOverlapped, ipsShouldOverlap.Count));
        }

        private bool OverlapExists(IPAddressRange a, IPAddressRange b)
        {
            return IpToUint(a.Begin) <= IpToUint(b.End) && IpToUint(b.Begin) <= IpToUint(a.End);
        }
        private uint IpToUint(IPAddress ipAddress)
        {
            byte[] bytes = ipAddress.GetAddressBytes();

            // flip big-endian(network order) to little-endian
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }

            return BitConverter.ToUInt32(bytes, 0);
        }
        private bool CheckOverlap(string ip1, string ip2)
        {
            IPAddressRange range1 = GetIPAdressRange(ip1);
            IPAddressRange range2 = GetIPAdressRange(ip2);

            if (range1.Begin.AddressFamily != range2.Begin.AddressFamily)
                return false;


            return OverlapExists(range1, range2);
        }
        private IPAddressRange GetIPAdressRange(string ip)
        {
            IPAddressRange ipAddressRange;

            if (ip.TryGetNetmask(out _))
            {
                (string Start, string End) = ip.CidrToRangeString();
                ipAddressRange = new(IPAddress.Parse(Start), IPAddress.Parse(End));
            }
            else if (ip.TrySplit('-', 1, out _) && IPAddressRange.TryParse(ip, out IPAddressRange ipRange))
            {
                ipAddressRange = ipRange;
            }
            else
            {
                ipAddressRange = new IPAddressRange(IPAddress.Parse(ip), IPAddress.Parse(ip));
            }

            return ipAddressRange;
        }
    }
}
