using System.Text;
using NetTools;
using FWO.Logging;
using System.Net;
using System.Text.RegularExpressions;

namespace FWO.Api.Data
{
    public static class DisplayBase
    {
        public static StringBuilder DisplayService(NetworkService service, bool isTechReport, string? serviceName = null)
        {
            StringBuilder result = new StringBuilder();
            string ports = service.DestinationPortEnd == null || service.DestinationPortEnd == 0 || service.DestinationPort == service.DestinationPortEnd ?
                $"{service.DestinationPort}" : $"{service.DestinationPort}-{service.DestinationPortEnd}";
            if (isTechReport)
            {
                if (service.DestinationPort == null)
                {
                    result.Append($"{service.Name}");
                }
                else
                {
                    result.Append($"{ports}/{service.Protocol?.Name}");
                }
            }
            else
            {
                result.Append($"{serviceName ?? service.Name}");
                if (service.DestinationPort != null)
                {
                    result.Append($" ({ports}/{service.Protocol?.Name})");
                }
                else if (service.Protocol?.Name != null)
                {
                    result.Append($" ({service.Protocol?.Name})");
                }
            }
            return result;
        }

        public static string DisplayIpWithName(NetworkObject elem)
        {
            string ip = DisplayIp(elem.IP, elem.IpEnd);
            if(elem.Name != null && elem.Name != "")
            {
                return elem.Name + " (" + ip + ")";
            }
            return ip;
        }

        public static string DisplayIp(string ip1, string ip2, bool inBrackets = false)
        {
            try
            {
                string nwObjType = AutoDetectType(ip1, ip2);
                return DisplayIp(ip1, ip2, nwObjType, inBrackets);
            }
            catch(Exception exc)
            {
                Log.WriteError("Ip displaying", $"Exception thrown: {exc.Message}");
                return "";
            }
        }

        public static string DisplayIp(string ip1, string ip2, string nwObjType, bool inBrackets = false)
        {
            string result = "";
            IPAddressRange IpRange;
            string IpStart;
            string IpEnd;
            if (nwObjType != ObjectType.Group)
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
                                result = inBrackets ? " (" : "";
                                if (nwObjType == ObjectType.Network)
                                {
                                    result += IpRange.ToCidrString();
                                }
                                else
                                {
                                    result += IpStart;
                                    if (nwObjType.Contains(ObjectType.IPRange))
                                    {
                                        result += $"-{IpEnd}";
                                    }
                                }
                                result += inBrackets ? ")" : "";
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

        private static string StripOffNetmask(string ip)
        {
            Match match = Regex.Match(ip, @"^([\d\.\:]+)\/");
            if (match.Success)
            {
                string matchedString = match.Value;
                return matchedString.Remove( matchedString.Length - 1 );
            }
            return ip;
        }

        private static bool SpanSingleNetwork(string ipInStart, string ipInEnd)
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

        private static string AutoDetectType(string ip1, string ip2)
        {
            if (ip1 == ip2)
            {
                return ObjectType.Host;
            }
            if (SpanSingleNetwork(ip1, ip2))
            {
                return ObjectType.Network;
            }
            return ObjectType.IPRange;
        }

        private static bool isV6Address(string ip)
        {
            return ip.Contains(":");
        }

        private static bool isV4Address(string ip)
        {
            return ip.Contains(".");
        }
    }
}
