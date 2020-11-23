using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using FWO.Logging;

namespace FWO.ApiClient.Queries
{
    public class AuthQueries : Queries
    {
        public static readonly string getTenantId;
        public static readonly string getUsers;
        public static readonly string getUserByDn;
        public static readonly string getUserByUuid;
        public static readonly string addUser;
        public static readonly string updateUser;
        public static readonly string deleteUser;
        public static readonly string assertUserExists;
        public static readonly string getVisibleDeviceIdsPerTenant;
        public static readonly string getVisibleManagementIdsPerTenant;
        public static readonly string getLdapConnections;
        public static readonly string newLdapConnection;
        public static readonly string updateLdapConnection;
        public static readonly string deleteLdapConnection;

        static AuthQueries()
        {
            try
            {
                getTenantId = File.ReadAllText(QueryPath + "auth/getTenantId.graphql");
                getVisibleDeviceIdsPerTenant = File.ReadAllText(QueryPath + "auth/getVisibleDeviceIdsPerTenant.graphql");
                getVisibleManagementIdsPerTenant = File.ReadAllText(QueryPath + "auth/getVisibleManagementIdsPerTenant.graphql");
                getLdapConnections = File.ReadAllText(QueryPath + "auth/getLdapConnections.graphql");
                getUsers = File.ReadAllText(QueryPath + "auth/getUsers.graphql");
                getUserByDn = File.ReadAllText(QueryPath + "auth/getUserByDn.graphql");
                getUserByUuid = File.ReadAllText(QueryPath + "auth/getUserByUuid.graphql");
                addUser = File.ReadAllText(QueryPath + "auth/addUser.graphql");
                updateUser = File.ReadAllText(QueryPath + "auth/updateUser.graphql");
                deleteUser = File.ReadAllText(QueryPath + "auth/deleteUser.graphql");
                assertUserExists = File.ReadAllText(QueryPath + "auth/assertUserExists.graphql");
                newLdapConnection = File.ReadAllText(QueryPath + "auth/newLdapConnection.graphql");
                updateLdapConnection = File.ReadAllText(QueryPath + "auth/updateLdapConnection.graphql");
                deleteLdapConnection = File.ReadAllText(QueryPath + "auth/deleteLdapConnection.graphql");
            }
            catch (Exception exception)
            {
                Log.WriteError("Initialize AuthQueries", "Api AuthQueries could not be loaded.", exception);
                Environment.Exit(-1);
            }
        }
    }
}
