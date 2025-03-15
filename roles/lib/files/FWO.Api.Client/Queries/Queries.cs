using System.Text.RegularExpressions;
using FWO.Basics;


namespace FWO.Api.Client.Queries
{
    public class Queries
    {
        // protected static readonly string QueryPath = AppDomain.CurrentDomain.BaseDirectory + "../../../../../../common/files/fwo-api-calls/";
        protected static readonly string QueryPath = GlobalConst.kFwoBaseDir + "/fwo-api-calls/";
        public static string Compact(string raw_query)
        { //             return Regex.Replace(input, @"[^\w\.\*\-\:\?@/\(\)\[\]\{\}\$\+<>#\$ ]", "").Trim();
            raw_query = Regex.Replace(raw_query, @"\\t+", @"\s").Trim();    // replace tabs with a single space
            raw_query = Regex.Replace(raw_query, @"\\s+", @"\s");    // replace multiple space chars by a single one
            raw_query = Regex.Replace(raw_query, @"[\n]", "");  // remove EOL chars
            return raw_query;
        }
    }
}
