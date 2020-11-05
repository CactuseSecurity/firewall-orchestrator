using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using FWO.Logging;

namespace FWO.ApiClient.Queries
{
    public class RuleQueries : Queries
    {
        public static readonly string ruleOverviewFragments;
        public static readonly string getRuleOverview;
        public static readonly string getRuleDetails;

        static RuleQueries()
        {
            try
            {
                ruleOverviewFragments =
                    File.ReadAllText(QueryPath + "networkObject/fragments/networkObjectOverview.graphql") +
                    File.ReadAllText(QueryPath + "networkService/fragments/networkServiceOverview.graphql") +
                    File.ReadAllText(QueryPath + "user/fragments/userOverview.graphql") +
                    File.ReadAllText(QueryPath + "rule/fragments/ruleOverview.graphql");

                getRuleOverview = ruleOverviewFragments + File.ReadAllText(QueryPath + "rule/getRuleOverview.graphql");

                getRuleDetails =
                    File.ReadAllText(QueryPath + "networkObject/fragments/networkObjectDetails.graphql") +
                    File.ReadAllText(QueryPath + "networkService/fragments/networkServiceDetails.graphql") +
                    File.ReadAllText(QueryPath + "user/fragments/userDetails.graphql") +
                    File.ReadAllText(QueryPath + "rule/fragments/ruleDetails.graphql") +
                    File.ReadAllText(QueryPath + "rule/getRuleDetails.graphql");
            }
            catch (Exception exception)
            {
                Log.WriteError("Initialize Api Queries", "Api Rule Queries could not be loaded.", exception);
                Environment.Exit(-1);
            }
        }
    }
}
