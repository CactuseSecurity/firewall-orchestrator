using NetTools;
using System.Net;
using System.Text;
using FWO.Logging;

namespace FWO.Ui.Display
{
    public static class NwObjDisplay
    {

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
