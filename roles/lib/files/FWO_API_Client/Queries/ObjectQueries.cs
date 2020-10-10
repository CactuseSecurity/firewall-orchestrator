using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;

namespace FWO.ApiClient.Queries
{
    class ObjectQueries : Queries {
        public static readonly string getNetworkObjectDetails;
        public static readonly string getNetworkServiceObjectDetails;
        public static readonly string getUserDetails;
        static ObjectQueries() 
        {
            getNetworkObjectDetails = 
                File.ReadAllText(QueryPath + "networkObject/fragments/networkObjectDetails.graphql") + 
                File.ReadAllText(QueryPath + "networkObject/getNetworkObjectDetails.graphql");
            getNetworkServiceObjectDetails = 
                File.ReadAllText(QueryPath + "serviceObject/fragments/serviceObjectDetails.graphql") + 
                File.ReadAllText(QueryPath + "serviceObject/getNetworkServiceObjectDetails.graphql");
            getNetworkObjectDetails = 
                File.ReadAllText(QueryPath + "user/fragments/userDetails.graphql") + 
                File.ReadAllText(QueryPath + "user/getUserDetails.graphql");
        }
    }
}
