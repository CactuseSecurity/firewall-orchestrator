using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GraphQL;
using GraphQL.Types;
using System.IO;


namespace FWO.ApiClient.Queries
{
    public class BasicQueries : Queries
    {
        public static readonly string getTenantId;
        public static readonly string getVisibleDeviceIdsPerTenant;
        public static readonly string getVisibleManagementIdsPerTenant;
        public static readonly string getLdapConnections;

        static BasicQueries() 
        {
            getTenantId = File.ReadAllText(QueryPath + "auth/getTenantId.graphql");

            getVisibleDeviceIdsPerTenant = File.ReadAllText(QueryPath + "auth/getVisibleDeviceIdsPerTenant.graphql");

            getVisibleManagementIdsPerTenant = File.ReadAllText(QueryPath + "auth/getVisibleManagementIdsPerTenant.graphql");

            getLdapConnections = File.ReadAllText(QueryPath + "auth/getLdapConnections.graphql");
        }
    }
}
