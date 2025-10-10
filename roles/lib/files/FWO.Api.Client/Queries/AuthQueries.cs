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
        public static readonly string getTenantNetworks;
        public static readonly string addTenantNetwork;
        public static readonly string deleteTenantNetwork;

        public static readonly string getUsers;
        public static readonly string getUserEmails;
        public static readonly string getUserByDn;
        public static readonly string getUserByDbId;
        public static readonly string upsertUiUser;
        public static readonly string updateUserEmail;
        public static readonly string updateUserLanguage;
        public static readonly string updateUserLastLogin;
        public static readonly string updateUserPasswordChange;
        public static readonly string deleteUser;
        public static readonly string assertUserExists;

        public static readonly string getLdapConnections;
        public static readonly string getAllLdapConnections;
        public static readonly string getLdapConnectionsSubscription;
        public static readonly string getLdapConnectionForUserSearchById;
        public static readonly string newLdapConnection;
        public static readonly string updateLdapConnection;
        public static readonly string deleteLdapConnection;

        static AuthQueries()
        {
            try
            {
                getTenantId = GetQueryText("auth/getTenantId.graphql");
                getTenants = GetQueryText("auth/getTenants.graphql");
                addTenant = GetQueryText("auth/addTenant.graphql");
                updateTenant = GetQueryText("auth/updateTenant.graphql");
                deleteTenant = GetQueryText("auth/deleteTenant.graphql");
                addDeviceToTenant = GetQueryText("auth/addDeviceToTenant.graphql");
                addTenantToManagement = GetQueryText("auth/addTenantToManagement.graphql");
                addTenantToGateway = GetQueryText("auth/addTenantToGateway.graphql");
                deleteAllGatewaysOfTenant = GetQueryText("auth/deleteAllGatewaysOfTenant.graphql");
                deleteAllManagementsOfTenant = GetQueryText("auth/deleteAllManagementsOfTenant.graphql");
                getVisibleDeviceIdsPerTenant = GetQueryText("auth/getTenantVisibleDeviceIds.graphql");
                getVisibleManagementIdsPerTenant = GetQueryText("auth/getTenantVisibleManagementIds.graphql");
                getTenantNetworks = GetQueryText("auth/getTenantNetworks.graphql");
                addTenantNetwork = GetQueryText("auth/addTenantNetwork.graphql");
                deleteTenantNetwork = GetQueryText("auth/deleteTenantNetwork.graphql");

                getUsers = GetQueryText("auth/getUsers.graphql");
                getUserEmails = GetQueryText("auth/getUserEmails.graphql");
                getUserByDn = GetQueryText("auth/getUserByDn.graphql");
                getUserByDbId = GetQueryText("auth/getUserByDbId.graphql");
                upsertUiUser = GetQueryText("auth/upsertUiUser.graphql");
                updateUserEmail = GetQueryText("auth/updateUserEmail.graphql");
                updateUserLanguage = GetQueryText("auth/updateUserLanguage.graphql");
                updateUserLastLogin = GetQueryText("auth/updateUserLastLogin.graphql");
                updateUserPasswordChange = GetQueryText("auth/updateUserPasswordChange.graphql");
                deleteUser = GetQueryText("auth/deleteUser.graphql");
                assertUserExists = GetQueryText("auth/assertUserExists.graphql");

                getLdapConnections = GetQueryText("auth/getLdapConnections.graphql");
                getAllLdapConnections = GetQueryText("auth/getAllLdapConnections.graphql");
                getLdapConnectionsSubscription = GetQueryText("auth/getLdapConnectionsSubscription.graphql");
                getLdapConnectionForUserSearchById = GetQueryText("auth/getLdapConnectionForUserSearchById.graphql");
                newLdapConnection = GetQueryText("auth/newLdapConnection.graphql");
                updateLdapConnection = GetQueryText("auth/updateLdapConnection.graphql");
                deleteLdapConnection = GetQueryText("auth/deleteLdapConnection.graphql");
            }
            catch (Exception exception)
            {
                Log.WriteError("Initialize AuthQueries", "Api AuthQueries could not be loaded.", exception);
                Environment.Exit(-1);
            }
        }
    }
}
