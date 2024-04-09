using System;
using System.IO;
using FWO.Logging;

namespace FWO.Api.Client.Queries
{
    public class AuthQueries : Queries
    {
        public static readonly string getTenantId;
        public static readonly string getTenants;
        public static readonly string addTenant;
        public static readonly string updateTenant;
        public static readonly string deleteTenant;
        public static readonly string addDeviceToTenant;
        public static readonly string addTenantToManagement;
        public static readonly string addTenantToGateway;
        public static readonly string deleteAllGatewaysOfTenant;
        public static readonly string deleteAllManagementsOfTenant;
        public static readonly string getVisibleDeviceIdsPerTenant;
        public static readonly string getVisibleManagementIdsPerTenant;
        public static readonly string getUnfilteredDeviceIdsPerTenant;
        public static readonly string getUnfilteredManagementIdsPerTenant;
        public static readonly string getTenantNetworks;
        public static readonly string addTenantNetwork;
        public static readonly string deleteTenantNetwork;

        public static readonly string getUsers;
        public static readonly string getUserEmails;
        public static readonly string getUserByDn;
        public static readonly string getUserByDbId;
        public static readonly string addUser;
        public static readonly string updateUserEmail;
        public static readonly string updateUserLanguage;
        public static readonly string updateUserLastLogin;
        public static readonly string updateUserPasswordChange;
        public static readonly string deleteUser;
        public static readonly string assertUserExists;

        public static readonly string getLdapConnections;
        public static readonly string getAllLdapConnections;
        public static readonly string getLdapConnectionsSubscription;
        public static readonly string newLdapConnection;
        public static readonly string updateLdapConnection;
        public static readonly string deleteLdapConnection;

        static AuthQueries()
        {
            try
            {
                getTenantId = File.ReadAllText(QueryPath + "auth/getTenantId.graphql");
                getTenants = File.ReadAllText(QueryPath + "auth/getTenants.graphql");
                addTenant = File.ReadAllText(QueryPath + "auth/addTenant.graphql");
                updateTenant = File.ReadAllText(QueryPath + "auth/updateTenant.graphql");
                deleteTenant = File.ReadAllText(QueryPath + "auth/deleteTenant.graphql");
                addDeviceToTenant = File.ReadAllText(QueryPath + "auth/addDeviceToTenant.graphql");
                addTenantToManagement = File.ReadAllText(QueryPath + "auth/addTenantToManagement.graphql");
                addTenantToGateway = File.ReadAllText(QueryPath + "auth/addTenantToGateway.graphql");
                deleteAllGatewaysOfTenant = File.ReadAllText(QueryPath + "auth/deleteAllGatewaysOfTenant.graphql");
                deleteAllManagementsOfTenant = File.ReadAllText(QueryPath + "auth/deleteAllManagementsOfTenant.graphql");
                getVisibleDeviceIdsPerTenant = File.ReadAllText(QueryPath + "auth/getTenantVisibleDeviceIds.graphql");
                getVisibleManagementIdsPerTenant = File.ReadAllText(QueryPath + "auth/getTenantVisibleManagementIds.graphql");
                getTenantNetworks = File.ReadAllText(QueryPath + "auth/getTenantNetworks.graphql");
                addTenantNetwork = File.ReadAllText(QueryPath + "auth/addTenantNetwork.graphql");
                deleteTenantNetwork = File.ReadAllText(QueryPath + "auth/deleteTenantNetwork.graphql");

                getUsers = File.ReadAllText(QueryPath + "auth/getUsers.graphql");
                getUserEmails = File.ReadAllText(QueryPath + "auth/getUserEmails.graphql");
                getUserByDn = File.ReadAllText(QueryPath + "auth/getUserByDn.graphql");
                getUserByDbId = File.ReadAllText(QueryPath + "auth/getUserByDbId.graphql");
                addUser = File.ReadAllText(QueryPath + "auth/addUser.graphql");
                updateUserEmail = File.ReadAllText(QueryPath + "auth/updateUserEmail.graphql");
                updateUserLanguage = File.ReadAllText(QueryPath + "auth/updateUserLanguage.graphql");
                updateUserLastLogin = File.ReadAllText(QueryPath + "auth/updateUserLastLogin.graphql");
                updateUserPasswordChange = File.ReadAllText(QueryPath + "auth/updateUserPasswordChange.graphql");
                deleteUser = File.ReadAllText(QueryPath + "auth/deleteUser.graphql");
                assertUserExists = File.ReadAllText(QueryPath + "auth/assertUserExists.graphql");

                getLdapConnections = File.ReadAllText(QueryPath + "auth/getLdapConnections.graphql");
                getAllLdapConnections = File.ReadAllText(QueryPath + "auth/getAllLdapConnections.graphql");
                getLdapConnectionsSubscription = File.ReadAllText(QueryPath + "auth/getLdapConnectionsSubscription.graphql");
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
