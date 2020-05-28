using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Xml;
using System.Xml.Linq;

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
