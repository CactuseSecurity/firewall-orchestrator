using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GraphQL;
using GraphQL.Types;
using System.IO;
using FWO.Logging;

namespace FWO.ApiClient.Queries
{
    public class BasicQueries : Queries
    {
        public static readonly string getTenantId;
        public static readonly string getVisibleDeviceIdsPerTenant;
        public static readonly string getVisibleManagementIdsPerTenant;
        public static readonly string getLdapConnections;
        public static readonly string getManagementsDetails;
        public static readonly string getLanguages;
        public static readonly string getAllTexts;
        public static readonly string getTextsPerLanguage;

        static BasicQueries()
        {
            try
            {
                getTenantId = File.ReadAllText(QueryPath + "auth/getTenantId.graphql");

                getVisibleDeviceIdsPerTenant = File.ReadAllText(QueryPath + "auth/getVisibleDeviceIdsPerTenant.graphql");

                getVisibleManagementIdsPerTenant = File.ReadAllText(QueryPath + "auth/getVisibleManagementIdsPerTenant.graphql");

                getLdapConnections = File.ReadAllText(QueryPath + "auth/getLdapConnections.graphql");

                getManagementsDetails = File.ReadAllText(QueryPath + "device/getManagementsDetails.graphql");

                getLanguages = File.ReadAllText(QueryPath + "config/getLanguages.graphql");

                getAllTexts = File.ReadAllText(QueryPath + "config/getTexts.graphql");

                getTextsPerLanguage = File.ReadAllText(QueryPath + "config/getTextsPerLanguage.graphql");
            }
            catch (Exception exception)
            {
                Log.WriteError("Initialize Api Queries", "Api Basic Queries could not be loaded.", exception);
                Environment.Exit(-1);
            }
        }
    }
}
