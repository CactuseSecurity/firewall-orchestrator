using FWO.Ui.Filter.Ast;
using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace FWO.Ui.Filter
{
    public class DynGraphqlQuery
    {
        public string queryDeviceHeader { get; }

        public MemoryStream queryVariables { get; set; }

        //= new MemoryStream();
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
            queryVariables = new MemoryStream();
            queryParameters.Add(" $managementId: [Int!] ");
            queryParameters.Add(" $deviceId: [Int!] ");
            queryParameters.Add(" $limit: Int ");
            queryParameters.Add(" $offset: Int ");
        }

    }
}