using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using FWO.ApiClient;

namespace FWO.Test.Api
{
    [TestClass]
    public class ApiTest
    {
        APIConnection Connection;

        [TestInitialize]
        public void EtablishConnectionToServer()
        {
            //Connection = new APIConnection();
        }

        [TestMethod]
        public async Task QueryTestRules()
        {
            // Query syntax:
            // { "query" : " 'query' ", "variables" : { 'variables' } } with 'query' containing the query and 'variables' the corresponding variables used in the query
            string Query = @"{ ""query"": "" 
query listRules($management_id: [Int!], $device_id: [Int!], $rule_src_name: [String!], $rule_src_ip: [cidr!]) {
  management(where: {mgm_id: {_in: $management_id}}) {
    mgm_id
    mgm_name
    devices(where: {dev_id: {_in: $device_id}}) {
      dev_id
      dev_name
      rules(where: {active: {_eq: true}, rule_src: {_in: $rule_src_name}, rule_disabled: {_eq: false}, rule_froms: {object: {obj_ip: {_in: $rule_src_ip}}}}, order_by: {rule_num: asc}) {
        rule_num
        rule_src
        rule_froms {
          object {
            obj_ip
          }
        }
        rule_dst
        rule_svc
        rule_action
        rule_track
      }
    }
  }
}
            } "", "" variables "" : {} }";

            //await Connection.SendQuery(Query);

            throw new NotImplementedException();

            //TODO: Compare with correct DataSet
        }
    }
}
