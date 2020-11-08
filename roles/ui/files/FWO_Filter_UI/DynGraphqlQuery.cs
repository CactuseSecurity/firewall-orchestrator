using FWO.Ui.Filter.Ast;
using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Dynamic;
using System.Net.Http;
using System.Threading.Tasks;
using FWO.Logging;
using GraphQL;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.SystemTextJson;
using GraphQL.Client.Abstractions;
using System.Linq;

namespace FWO.Ui.Filter
{
    public class DynGraphqlQuery
    {
        public string queryDeviceHeader { get; }
        public int parameterCounter;

        public Dictionary<string, object> QueryVariables { get; set; }

        public string FullQuery { get; set; }
        public string WhereQueryPart { get; set; }
        public List<string> QueryParameters { get; set; }
        public string TimeFilter { get; set; }

        public DynGraphqlQuery()
        {
            WhereQueryPart = "";
            TimeFilter = "";
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

            QueryParameters = new List<string>();
            QueryParameters.Add(" $managementId: [Int!] ");
            QueryParameters.Add(" $deviceId: [Int!] ");
            QueryParameters.Add(" $limit: Int ");
            QueryParameters.Add(" $offset: Int ");
            QueryVariables = new Dictionary<string, object>();
        }
    }
}
