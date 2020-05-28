using Newtonsoft.Json.Linq;
using System.Data;

namespace Firewall_Orchestrator
{
    static class JsonReader
    {
        public static DataSet ReadString(string JsonString)
        {
            DataSet data = JObject.Parse(JsonString)["data"].ToObject<DataSet>();

            return data;
        }
    }
}
