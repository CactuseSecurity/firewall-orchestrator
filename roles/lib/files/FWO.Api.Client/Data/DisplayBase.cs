using System.Text;
using NetTools;
using FWO.Logging;
using System.Net;
using FWO.GlobalConstants;

namespace FWO.Api.Data
{
    public static class DisplayBase
    {
        public static StringBuilder DisplayService(NetworkService service, bool isTechReport, string? serviceName = null)
        {
            StringBuilder result = new ();
            string ports = service.DestinationPortEnd == null || service.DestinationPortEnd == 0 || service.DestinationPort == service.DestinationPortEnd ?
                $"{service.DestinationPort}" : $"{service.DestinationPort}-{service.DestinationPortEnd}";
            if (isTechReport)
            {
                if (service.DestinationPort == null)
                {
                    if (service.Protocol?.Name != null)
                    {
                        result.Append($"{service.Protocol?.Name}");
                    }
                    else
                    {
                        result.Append($"{service.Name}");
                    }
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
            if(elem.Name != null && elem.Name != "")
            {
                return elem.Name + DisplayIp(elem.IP, elem.IpEnd, true);
            }
            return DisplayIp(elem.IP, elem.IpEnd);
        }

        public static string DisplayIp(string ip1, string ip2, bool inBrackets = false)
        {
            try
            {
                if (ip2 == "")
                {
                    ip2 = ip1;
                }
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
            if (nwObjType != ObjectType.Group)
            {
                if (!IsV4Address(ip1) && !IsV6Address(ip1))
                {
                    Log.WriteError("Ip displaying", $"Found undefined IP family: {ip1} - {ip2}");
                }
                else if (IsV4Address(ip1) == IsV6Address(ip2))
                {
                    Log.WriteError("Ip displaying", $"Found mixed IP family: {ip1} - {ip2}");
                }
                else
                {
                    if (ip2 == "")
                    {
                        ip2 = ip1;
                    }
                    string IpStart = StripOffUnnecessaryNetmask(ip1);
                    string IpEnd = StripOffUnnecessaryNetmask(ip2);

                    try
                    {
                        result = inBrackets ? " (" : "";
                        if (nwObjType == ObjectType.Network)
                        {
                            if(GetNetmask(IpStart) == "")
                            {
                                IPAddressRange ipRange = new (IPAddress.Parse(IpStart), IPAddress.Parse(IpEnd));
                                if (ipRange != null)
                                {
                                    result += ipRange.ToCidrString();
                                }
                            }
                            else
                            {
                                result += IpStart;
                            }
                        }
                        else
                        {
                            result += IpStart;
                            if (nwObjType == ObjectType.IPRange)
                            {
                                result += $"-{IpEnd}";
                            }
                        }
                        result += inBrackets ? ")" : "";
                    }
                    catch (Exception exc)
                    {
                        Log.WriteError("Ip displaying", $"Wrong ip format {IpStart} - {IpEnd}\nMessage: {exc.Message}");
                    }
                }
            }
            return result;
        }

        public static string DisplayIpRange(string ip1, string ip2, bool inBrackets = false)
        {
            string result = "";
            string nwObjType = AutoDetectType(ip1, ip2);
            if (nwObjType != ObjectType.Group)
            {
                if (!IsV4Address(ip1) && !IsV6Address(ip1))
                {
                    Log.WriteError("Ip displaying", $"Found undefined IP family: {ip1} - {ip2}");
                }
                else if (IsV4Address(ip1) == IsV6Address(ip2))
                {
                    Log.WriteError("Ip displaying", $"Found mixed IP family: {ip1} - {ip2}");
                }
                else
                {
                    if (ip2 == "")
                    {
                        ip2 = ip1;
                    }
                    string IpStart = StripOffUnnecessaryNetmask(ip1);
                    string IpEnd = StripOffUnnecessaryNetmask(ip2);

                    try
                    {
                        result = inBrackets ? " (" : "";
                        if (nwObjType == ObjectType.Network)
                        {
                            if (GetNetmask(IpStart) == "")
                            {
                                IPAddressRange ipRange = new(IPAddress.Parse(IpStart), IPAddress.Parse(IpEnd));
                                if (ipRange != null)
                                {
                                    result += $"{ipRange.Begin}-{ipRange.End}";
                                }
                            }
                            else
                            {
                                result += IpStart;
                            }
                        }
                        else
                        {
                            result += IpStart;
                            if (nwObjType == ObjectType.IPRange)
                            {
                                result += $"-{IpEnd}";
                            }
                        }
                        result += inBrackets ? ")" : "";
                    }
                    catch (Exception exc)
                    {
                        Log.WriteError("Ip displaying", $"Wrong ip format {IpStart} - {IpEnd}\nMessage: {exc.Message}");
                    }
                }
            }
            return result;
        }

        public static string GetNetmask(string ip)
        {
            int pos = ip.LastIndexOf('/');
            if (pos > -1 && ip.Length > pos + 1)
            {
                return ip[(pos + 1)..];
            }
            return "";
        }

        public static string StripOffNetmask(string ip)
        {
            int pos = ip.LastIndexOf('/');
            if (pos > -1 && ip.Length > pos + 1)
            {
                return ip[..pos];
            }
            return ip;
        }

        private static string StripOffUnnecessaryNetmask(string ip)
        {
            string netmask = GetNetmask(ip);
            if (IsV4Address(ip) && netmask == "32" || IsV6Address(ip) && netmask == "128")
            {
                return StripOffNetmask(ip);
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

        public static string AutoDetectType(string ip1, string ip2)
        {
            ip1 = StripOffUnnecessaryNetmask(ip1);
            ip2 = StripOffUnnecessaryNetmask(ip2);
            if (ip1 == ip2)
            {
                string netmask = GetNetmask(ip1);
                if(netmask != "")
                {
                    return ObjectType.Network;
                }
                return ObjectType.Host;
            }
            if (SpanSingleNetwork(ip1, ip2))
            {
                return ObjectType.Network;
            }
            return ObjectType.IPRange;
        }

        private static bool IsV6Address(string ip)
        {
            return ip.Contains(':');
        }

        private static bool IsV4Address(string ip)
        {
            return ip.Contains('.');
        }
    }
}
