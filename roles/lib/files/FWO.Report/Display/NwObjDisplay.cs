using NetTools;
using System.Text;
using FWO.Logging;
using System.Net;
using System.Text.RegularExpressions;

namespace FWO.Ui.Display
{
    public static class NwObjDisplay
    {
        public static string StripOffNetmask(string ip)
        {
            Match match = Regex.Match(ip, @"^([\d\.\:]+)\/");
            if (match.Success)
            {
                string matchedString = match.Value;
                return matchedString.Remove( matchedString.Length - 1 );
            }
            return ip;
        }

        public static bool SpanSingleNetwork(string ipInStart, string ipInEnd)
        {
            // IPAddressRange range = IPAddressRange.Parse(IPAddress.Parse(ipInStart), IPAddress.Parse(ipInEnd));

            IPAddressRange range = IPAddressRange.Parse(StripOffNetmask(ipInStart) + "-" + StripOffNetmask(ipInEnd));
            try
            {
                range.ToCidrString();
            }
            catch (Exception)
            {
                return false;
            }
            return true;
        }

        public static string AutoDetectType(string ip1, string ip2)
        {
            if (ip1 == ip2)
            {
                return "host";
            }
            if (SpanSingleNetwork(ip1, ip2))
            {
                return "network";
            }
            return "iprange";
        }
        public static string DisplayIp(string ip1, string ip2, bool inBrackets = false)
        {
            string nwObjType = AutoDetectType(ip1, ip2);
            return DisplayIp(ip1, ip2, nwObjType, inBrackets);
        }

        public static string DisplayIp(string ip1, string ip2, string nwObjType, bool inBrackets = false)
        {
            string result = "";
            IPAddressRange IpRange;
            string IpStart;
            string IpEnd;
            if (nwObjType != "group")
            {
                if (ip2 == null)
                {
                    Log.WriteError("Ip displaying", $"Found undefined IpEnd {ip2}");
                }
                else
                {
                    if (!isV4Address(ip1) && !isV6Address(ip1))
                    {
                        Log.WriteError("Ip displaying", $"Found undefined IP family: {ip1} - {ip2}");
                    }
                    else
                    {
                        if (isV4Address(ip1))
                        {
                            IpStart = ip1.Replace("/32", "");
                            IpEnd = ip2.Replace("/32", "");
                        }
                        else
                        {
                            IpStart = ip1.Replace("/128", "");
                            IpEnd = ip2.Replace("/128", "");
                        }

                        try
                        {
                            IpRange = new IPAddressRange(IPAddress.Parse(IpStart), IPAddress.Parse(IpEnd));
                            if (IpRange != null)
                            {
                                result = ((inBrackets) ? " (" : "");
                                if (nwObjType == "network")
                                {
                                    result += IpRange.ToCidrString();
                                }
                                else
                                {
                                    result += IpStart;
                                    if (nwObjType.Contains("range"))
                                    {
                                        result += $"-{IpEnd}";
                                    }
                                }
                                result += ((inBrackets) ? ")" : "");
                            }
                        }
                        catch (Exception exc)
                        {
                            Log.WriteError("Ip displaying", $"Wrong ip format {IpStart} - {IpEnd}\nMessage: {exc.Message}");
                        }
                    }
                }
            }
            return result;
        }

        public static bool isV6Address(string ip)
        {
            return ip.Contains(":");
        }
        static bool isV4Address(string ip)
        {
            return ip.Contains(".");
        }
    }
}
