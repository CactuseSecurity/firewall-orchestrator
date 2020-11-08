using FWO.Ui.Filter.Ast;
using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Dynamic;
using Newtonsoft.Json.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using FWO.Logging;
using GraphQL;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.SystemTextJson;
using GraphQL.Client.Abstractions;
using System.Linq;
//using GraphQL.SystemTextJson;

namespace FWO.Ui.Filter
{
    public class DynGraphqlQuery
    {
        public string queryDeviceHeader { get; }
        public int parameterCounter;

        public Dictionary<string, object> queryVariableDict { get; set; }
        public JObject queryVariables { get; set; }

        public string fullQuery { get; set; }
        public string whereQueryPart { get; set; }
        public List<string> queryParameters { get; set; }
        public string timeFilter { get; set; }

        public DynGraphqlQuery()
        {
            whereQueryPart = "";
            timeFilter = "";
            parameterCounter = 0;
            queryDeviceHeader = @"                    
                management(
                    where: { mgm_id: { _in: $managementId } }
                    order_by: { mgm_name: asc }
                ) 
                {
                    mgm_id
                    mgm_name
                    devices(
                        where: { dev_id: { _in: $deviceId } }
                        order_by: { dev_name: asc }
                    ) {
                        dev_id
                        dev_name
                    }
                ";

            queryParameters = new List<string>();
            queryParameters.Add(" $managementId: [Int!] ");
            queryParameters.Add(" $deviceId: [Int!] ");
            queryParameters.Add(" $limit: Int ");
            queryParameters.Add(" $offset: Int ");
            queryVariableDict = new Dictionary<string, object>();
            queryVariables = new JObject();

        }
        public object getVariableValue(string key)
        {
            return queryVariables.GetValue(key);
            // return queryVariableDict[key];
        }
        public void setVariable(string key, object value)
        {
            // queryVariables.Add(new JProperty(key, new JValue(value)));
            queryVariables.Add(new JProperty(key, value));
            //queryVariables.Add(key, value);
            //queryVariableDict[key] = value;
        }
        public object getVariables()
        {
            // return DictionaryToObject(queryVariableDict);
            return queryVariables;
        }

        // public string deserializeVariables()
        // {
        //     return JsonSerializer.Deserialize<string,object>(queryVariables);
        // }
        // private static dynamic DictionaryToObject(IDictionary<string, Object> dictionary)
        // {
        //     var expandoObj = new ExpandoObject();
        //     var expandoObjCollection = (ICollection<KeyValuePair<string, Object>>)expandoObj;

        //     foreach (var keyValuePair in dictionary)
        //     {
        //         expandoObjCollection.Add(keyValuePair);
        //     }
        //     dynamic eoDynamic = expandoObj;
        //     return eoDynamic;
        // }

// JObject o = JObject.Parse(json);
        // private object getVariablesAsJson()
        // {
        //     var jsSerializer = new JavaScriptSerializer();
        //     var serialized = jsSerializer.Serialize(queryVariableDict);
        //     var deserializedResult = jsSerializer.Deserialize<List<Person>>(serialized);
        // }
    }
}
