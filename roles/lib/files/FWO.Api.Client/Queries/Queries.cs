using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FWO.Config;
using System.Text.RegularExpressions;


namespace FWO.ApiClient.Queries
{
    public class Queries
    {
        protected static readonly string QueryPath = AppDomain.CurrentDomain.BaseDirectory + "../../../../../../lib/files/FWO.Api.Client/APIcalls/";
        public static string compact(string raw_query)
        { //             return Regex.Replace(input, @"[^\w\.\*\-\:\?@/\(\)\[\]\{\}\$\+<>#\$ ]", "").Trim();
            raw_query = Regex.Replace(raw_query, @"\\t+", @"\s").Trim();    // replace tabs with a single space
            raw_query = Regex.Replace(raw_query, @"\\s+", @"\s");    // replace multiple space chars by a single one
            raw_query = Regex.Replace(raw_query, @"[\n]", "");  // remove EOL chars
            return raw_query;
        }
    }
}
