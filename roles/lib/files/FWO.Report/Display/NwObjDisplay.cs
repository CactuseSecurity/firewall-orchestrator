using FWO.Api.Data;

namespace FWO.Ui.Display
{
    public static class NwObjDisplay
    {
        public static string DisplayIp(string ip1, string ip2, bool inBrackets = false)
        {
            return DisplayBase.DisplayIp(ip1, ip2, inBrackets);
        }

        public static string DisplayIp(string ip1, string ip2, string nwObjType, bool inBrackets = false)
        {
            return DisplayBase.DisplayIp(ip1, ip2, nwObjType, inBrackets);
        }
    }
}
