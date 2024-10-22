using NUnit.Framework;
using NetTools;
using System.Net;
using FWO.GlobalConstants;

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
                "1.2.3.0/24",
                "2.2.2.2",
                "3.3.3.3-3.3.3.4",
                "10.121.254.128/27",
                "10.122.28.1/23",
                "172.0.0.0/7",
                "10.0.0.0/7"
                ];

            List<string> ipsShouldOverlap = [
                "1.2.3.0/24",
                "2.2.2.2",
                "3.3.3.3-3.3.3.4",
                "10.121.254.128/27",
                "10.122.28.1/23",
                "172.0.0.0/7",
                "10.0.0.0/7"
                ];

            List<string> ipsShouldNotOverlap = [
                "1.2.2.0/24",
                "2.2.2.0",
                "3.3.2.3-3.3.2.4",
                "10.121.253.128/27",
                "10.122.25.1/23",
                "169.0.0.0/7",
                "9.0.0.0/7"
             ];

            int ipsThatOverlapped = 0;
            int ipsThatNotOverlapped = 0;

            foreach (var subnet in areaIPs)
            {
                foreach (var ipOverlap in ipsShouldOverlap)
                {
                    if (CheckOverlap(subnet, ipOverlap))
                    {
                        ipsThatOverlapped++;
                    }
                    else
                    {
                        ipsThatNotOverlapped++;
                    }
                }

                foreach (var ipNotOverlap in ipsShouldNotOverlap)
                {
                    if (CheckOverlap(subnet, ipNotOverlap))
                    {
                        ipsThatOverlapped++;
                    }
                    else
                    {
                        ipsThatNotOverlapped++;
                    }
                }
            }

            Assert.That(Is.Equals(ipsThatOverlapped, ipsShouldOverlap.Count));
            Assert.That(Is.Equals(ipsThatNotOverlapped, ipsShouldNotOverlap.Count));
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
                (string Start, string End) = GlobalFunc.IpOperations.CidrToRangeString(ip);
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
