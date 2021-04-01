using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using FWO.Logging;

namespace FWO.ApiClient.Queries
{
    public class ObjectQueries : Queries
    {
        public static readonly string getNetworkObjectDetails;
        public static readonly string getNetworkServiceObjectDetails;
        public static readonly string getUserDetails;
        public static readonly string getAllObjectDetails;
        public static readonly string getRuleDetails;

        static ObjectQueries() 
        {
            try
            {
                getNetworkObjectDetails =
                    File.ReadAllText(QueryPath + "networkObject/fragments/networkObjectDetails.graphql") +
                    File.ReadAllText(QueryPath + "networkObject/getNetworkObjectDetails.graphql");
                getNetworkServiceObjectDetails =
                    File.ReadAllText(QueryPath + "networkService/fragments/networkServiceDetails.graphql") +
                    File.ReadAllText(QueryPath + "networkService/getNetworkServiceDetails.graphql");
                getUserDetails =
                    File.ReadAllText(QueryPath + "user/fragments/userDetails.graphql") +
                    File.ReadAllText(QueryPath + "user/getUserDetails.graphql");
                getAllObjectDetails =
                    File.ReadAllText(QueryPath + "user/fragments/userDetails.graphql") +
                    File.ReadAllText(QueryPath + "networkService/fragments/networkServiceDetails.graphql") +
                    File.ReadAllText(QueryPath + "networkObject/fragments/networkObjectDetails.graphql") +
                    File.ReadAllText(QueryPath + "allObjects/getAllObjectDetails.graphql");
                getRuleDetails =
                    File.ReadAllText(QueryPath + "rule/fragments/ruleDetails.graphql") +
                    File.ReadAllText(QueryPath + "user/fragments/userDetails.graphql") +
                    File.ReadAllText(QueryPath + "networkService/fragments/networkServiceDetails.graphql") +
                    File.ReadAllText(QueryPath + "networkObject/fragments/networkObjectDetails.graphql") +
                    File.ReadAllText(QueryPath + "rule/getRuleDetails.graphql");
            }
            catch (Exception exception)
            {
                Log.WriteError("Initialize Api Queries", "Api Object Queries could not be loaded." , exception);
                Environment.Exit(-1);
            }
        }
    }
}
