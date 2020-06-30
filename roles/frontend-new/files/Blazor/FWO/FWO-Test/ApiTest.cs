using FWO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading.Tasks;

namespace FWO_Test
{
    [TestClass]
    public class ApiTest
    {
        APIConnection Connection;

        [TestInitialize]
        public void EtablishConnectionToServer()
        {
            Connection = new APIConnection();
        }

        [TestMethod]
        public async Task QueryTestRules()
        {
            // Query aufgebaut wie folgt { "query" : " 'query' ", "variables" : { 'variables' } } mit 'query' für die zu versendende Query und 'variables' für die dazugehörigen Variablen
            string Query = @"{ ""query"": "" 
                query listRules
                {
                    rule(where: { active: { _eq: true}, rule_disabled: { _eq: false} }, order_by: { rule_num: asc}) {
                    rule_num
                    rule_src
                    rule_dst
                    rule_svc
                    rule_action
                    rule_track
                }
            } "", "" variables "" : {} }";

            await Connection.SendQuery(Query);

            throw new NotImplementedException();

            //TODO: Compare with correct DataSet
        }
    }
}
