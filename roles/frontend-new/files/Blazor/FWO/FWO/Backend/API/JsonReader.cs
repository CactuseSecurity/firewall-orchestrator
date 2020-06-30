//using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;

namespace FWO
{
    public class JsonReader
    {
        public static DataSet ReadString(string JsonString)
        {
            //JsonString = "Müll";

            JsonDocument JsonQuery = JsonDocument.Parse(JsonString);        

            DataSet data = JsonSerializer.Deserialize<DataSet>(JsonString); 

            //DataSet data = JObject.Parse(JsonString)["data"].ToObject<DataSet>();

            return data;
        }
    }
}
