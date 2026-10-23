using FWO.Basics;
using FWO.Data.Workflow;
using NetTools;

namespace FWO.Services.PathAnalysis
{
    /// <summary>
    /// Converts the source and destination elements of a request task into the address ranges a path analysis needs.
    /// </summary>
    public static class ReqElementIpRanges
    {
        /// <summary>
        /// Returns the address ranges of the elements of one field (source or destination). Elements without an
        /// address, e.g. referencing a network object only, or with an unparsable address are skipped.
        /// </summary>
        /// <param name="elements">Elements of the request task.</param>
        /// <param name="field">Field whose elements are converted.</param>
        public static List<IPAddressRange> ToRanges(IEnumerable<WfReqElement> elements, ElemFieldType field)
        {
            List<IPAddressRange> ranges = [];
            foreach (WfReqElement element in elements.Where(element => element.Field == field.ToString()))
            {
                IPAddressRange? range = TryGetRange(element);
                if (range != null)
                {
                    ranges.Add(range);
                }
            }
            return ranges;
        }

        private static IPAddressRange? TryGetRange(WfReqElement element)
        {
            if (string.IsNullOrWhiteSpace(element.IpString))
            {
                return null;
            }
            string ipText = string.IsNullOrWhiteSpace(element.IpEnd)
                ? element.IpString
                : $"{element.IpString.StripOffNetmask()}-{element.IpEnd.StripOffNetmask()}";
            try
            {
                return ipText.TryParseIPString(out IPAddressRange? range) ? range : null;
            }
            catch (ArgumentException)
            {
                // a reversed range or a range mixing IPv4 and IPv6
                return null;
            }
        }
    }
}
