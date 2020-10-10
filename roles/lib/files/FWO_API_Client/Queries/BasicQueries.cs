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
        public static readonly string getLdapConnections;

        static BasicQueries() 
        {
            getTenantId = File.ReadAllText(QueryPath + "auth/getTenantId.graphql");

            getLdapConnections = File.ReadAllText(QueryPath + "auth/getLdapConnections.graphql");
        }
    }
}
