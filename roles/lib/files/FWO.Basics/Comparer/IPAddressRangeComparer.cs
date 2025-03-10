using System.Numerics;
using NetTools;

namespace FWO.Basics.Comparer
{
    public class IPAddressRangeComparer : IComparer<IPAddressRange>
    {
        public int Compare(IPAddressRange? x, IPAddressRange? y)
        {
            if(x is null || y is null)
            {
                return 0;
            }

            IPAdressComparer iPAdressComparer = new ();
            int compareIPAddressResult = iPAdressComparer.Compare(x.Begin, y.Begin);

            if (compareIPAddressResult != 0)
            {
                return compareIPAddressResult;
            }

            BigInteger xRangeSize = GetIPRangeSize(x);
            BigInteger yRangeSize = GetIPRangeSize(y);

            if(xRangeSize < yRangeSize)
            {
                return -1;
            }

            if(xRangeSize > yRangeSize)
            {
                return 1;
            }

            return 0;
        }

        public BigInteger GetIPRangeSize(IPAddressRange range)
        {
            byte[] startBytes = range.Begin.GetAddressBytes();
            byte[] endBytes = range.End.GetAddressBytes();

            // prevents overflow problem
            byte[] startBytesPadded = startBytes.Reverse().Concat(new byte[] { 0 }).ToArray();
            byte[] endBytesPadded = endBytes.Reverse().Concat(new byte[] { 0 }).ToArray();

            BigInteger startValue = new BigInteger(startBytesPadded);
            BigInteger endValue = new BigInteger(endBytesPadded);

            return endValue - startValue;
        }
    }
}