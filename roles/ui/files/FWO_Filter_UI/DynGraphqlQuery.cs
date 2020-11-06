using FWO.Ui.Filter.Ast;
using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Dynamic;
//using GraphQL.SystemTextJson;
//using Newtonsoft.Json.Linq;

namespace FWO.Ui.Filter
{
    public class DynGraphqlQuery
    {
        public string queryDeviceHeader { get; }

        public Dictionary<string, object> queryVariableDict { get; set; }

        public string fullQuery { get; set; }
        public string whereQueryPart { get; set; }
        public List<string> queryParameters { get; set; }
        public string timeFilter { get; set; }

        public DynGraphqlQuery()
        {
            whereQueryPart = "";
            timeFilter = "";
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
        }
        public object getVariableValue(string key)
        {
            return queryVariableDict[key];
        }
        public void addVariable(string key, object value)
        {
            queryVariableDict[key] = value;
        }
        public void setVariableObject(string key, object value)
        {
            queryVariableDict[key] = value;
        }

        public object getVariableObject()
        {
            return DictionaryToObject(queryVariableDict);
        }
        private static dynamic DictionaryToObject(IDictionary<string, Object> dictionary)
        {
            var expandoObj = new ExpandoObject();
            var expandoObjCollection = (ICollection<KeyValuePair<string, Object>>)expandoObj;

            foreach (var keyValuePair in dictionary)
            {
                expandoObjCollection.Add(keyValuePair);
            }
            dynamic eoDynamic = expandoObj;
            return eoDynamic;
        }
    
    }
}
