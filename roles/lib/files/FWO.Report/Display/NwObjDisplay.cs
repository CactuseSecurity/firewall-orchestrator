using NetTools;
using System.Net;
using System.Numerics;
using System.Text;
using FWO.Api.Data;
using FWO.Logging;

namespace FWO.Ui.Display
{
    public class NwObjDisplay
    {
        protected string Name;
        protected string IpStart = "";
        protected string IpEnd = "";
        protected string NwObjType;
        protected IPAddressRange? IpRange;
        protected string NwObjUid;
        protected string NwObjComment;

        public NwObjDisplay(string name, string ip1, string ip2, string typeIn, string uid, string comment)
        {
            NwObjType = typeIn;
            Name = name;
            NwObjUid = uid;
            NwObjComment = comment;

            if (NwObjType != "group")
            {
                if (isV4Address(ip1))
                {
                    IpStart = ip1.Replace("/32", "");
                    if (ip2!=null)
                    {
                        IpEnd = ip2.Replace("/32", "");
                    }
                    else
                    {
                        Log.WriteError("Ip displaying", $"Found undefined IpEnd {IpStart} - {IpEnd}");
                    }
                }
                else if (isV6Address(ip1))
                {
                    IpStart = ip1.Replace("/128", "");
                    IpEnd = ip2.Replace("/128", "");
                }

                try
                {
                    IpRange = new IPAddressRange(IPAddress.Parse(IpStart), IPAddress.Parse(IpEnd));
                }
                catch (Exception exc)
                {
                    Log.WriteError("Ip displaying", $"Wrong ip format {IpStart} - {IpEnd}\nMessage: {exc.Message}");
                }
            }
        }

        public string GetNwObjType()
        {
            return NwObjType;
        }
        public string DisplayName()
        {
            return Name;
        }
        public string DisplayIp(bool inBrackets = false)
        {
            string result = "";
            if (IpRange!=null)
            {
                result = ((inBrackets) ? " (" : "");
                if (NwObjType == "network")
                {
                    result += IpRange.ToCidrString();
                }
                else
                {
                    result += IpStart;
                    if (NwObjType.Contains("range"))
                    {
                        result += $"-{IpEnd}";
                    }
                }
                result += ((inBrackets) ? ")" : "");
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

        public string DisplayUid()
        {
            return (NwObjUid != null ? NwObjUid : "");
        }

        public string DisplayComment()
        {
            return (NwObjComment != null ? NwObjComment : "");
        }

    }
}
