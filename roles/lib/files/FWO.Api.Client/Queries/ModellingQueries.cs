using FWO.Logging;

namespace FWO.Api.Client.Queries
{
    public class ModellingQueries : Queries
    {
        public static readonly string appServerDetailsFragment;
        public static readonly string appRoleDetailsFragment;
        public static readonly string areaDetailsFragment;
        public static readonly string serviceDetailsFragment;
        public static readonly string serviceGroupDetailsFragment;
        public static readonly string connectionDetailsFragment;
        public static readonly string connectionResolvedDetailsFragment;

        public static readonly string getAreas;
        public static readonly string newArea;
        public static readonly string setAreaDeletedState;
        public static readonly string newAreaIpData;
        public static readonly string getConnectionIdsForNwGroup;

        public static readonly string getAppServers;
        public static readonly string getImportedAppServers;
        public static readonly string newAppServer;
        public static readonly string updateAppServer;
        public static readonly string setAppServerDeletedState;
        public static readonly string setAppServerType;
        public static readonly string setAppServerName;
        public static readonly string deleteAppServer;
        public static readonly string getAppRolesForAppServer;
        public static readonly string getConnectionIdsForAppServer;

        public static readonly string getPublishedInterfaces;
        public static readonly string getConnectionById;
        public static readonly string getConnections;
        public static readonly string getConnectionsResolved;
        public static readonly string getConnectionsByTicketId;
        public static readonly string getInterfaceUsers;
        public static readonly string getCommonServices;
        public static readonly string newConnection;
        public static readonly string updateConnection;
        public static readonly string updateProposedConnectionOwner;
        public static readonly string updateConnectionPublish;
        public static readonly string updateConnectionProperties;
        public static readonly string replaceUsedInterface;
        public static readonly string deleteConnection;
        public static readonly string addAppServerToConnection;
        public static readonly string removeAppServerFromConnection;
        public static readonly string addNwGroupToConnection;
        public static readonly string removeNwGroupFromConnection;
        public static readonly string addServiceToConnection;
        public static readonly string removeServiceFromConnection;
        public static readonly string addServiceGroupToConnection;
        public static readonly string removeServiceGroupFromConnection;
        public static readonly string getConnectionIdsForService;
        public static readonly string getConnectionIdsForServiceGroup;

        public static readonly string getSelectedConnections;
        public static readonly string addSelectedConnection;
        public static readonly string removeSelectedConnectionFromApp;
        public static readonly string removeSelectedConnection;

        public static readonly string getNwGroupObjects;
        public static readonly string getSelectedNwGroupObjects;
        public static readonly string addSelectedNwGroupObject;
        public static readonly string removeSelectedNwGroupObject;
        public static readonly string removeSelectedNwGroupObjectFromAllApps;

        public static readonly string getAppRoles;
        public static readonly string getNewestAppRoles;
        public static readonly string getDummyAppRole;
        public static readonly string newAppRole;
        public static readonly string updateAppRole;
        public static readonly string deleteNwGroup;
        // public static readonly string getAppServerForAppRole;
        public static readonly string addNwObjectToNwGroup;
        public static readonly string removeNwObjectFromNwGroup;

        public static readonly string getServicesForApp;
        public static readonly string getGlobalServices;
        public static readonly string newService;
        public static readonly string updateService;
        public static readonly string deleteService;

        public static readonly string getServiceGroupsForApp;
        public static readonly string getServiceGroupById;
        public static readonly string getGlobalServiceGroups;
        public static readonly string newServiceGroup;
        public static readonly string updateServiceGroup;
        public static readonly string deleteServiceGroup;
        public static readonly string addServiceToServiceGroup;
        public static readonly string removeServiceFromServiceGroup;
        public static readonly string getServiceGroupIdsForService;

        public static readonly string getHistory;
        public static readonly string getHistoryForApp;
        public static readonly string addHistoryEntry;

        public static readonly string newAppZone;
        public static readonly string getAppZonesByAppId;

        static ModellingQueries()
        {
            try
            {
                appServerDetailsFragment = File.ReadAllText(QueryPath + "modelling/fragments/appServerDetails.graphql");
                appRoleDetailsFragment = File.ReadAllText(QueryPath + "modelling/fragments/appRoleDetails.graphql");
                areaDetailsFragment = File.ReadAllText(QueryPath + "modelling/fragments/areaDetails.graphql");
                serviceDetailsFragment = File.ReadAllText(QueryPath + "modelling/fragments/serviceDetails.graphql");
                serviceGroupDetailsFragment = File.ReadAllText(QueryPath + "modelling/fragments/serviceGroupDetails.graphql");
                connectionDetailsFragment = appServerDetailsFragment + appRoleDetailsFragment + serviceDetailsFragment + serviceGroupDetailsFragment +
                    File.ReadAllText(QueryPath + "modelling/fragments/connectionDetails.graphql");
                connectionResolvedDetailsFragment = appServerDetailsFragment + appRoleDetailsFragment + areaDetailsFragment + serviceDetailsFragment + serviceGroupDetailsFragment +
                    File.ReadAllText(QueryPath + "modelling/fragments/connectionResolvedDetails.graphql");

                getAreas = areaDetailsFragment + File.ReadAllText(QueryPath + "modelling/getAreas.graphql");
                newArea = File.ReadAllText(QueryPath + "modelling/newArea.graphql");
                setAreaDeletedState = File.ReadAllText(QueryPath + "modelling/setAreaDeletedState.graphql");
                newAreaIpData = File.ReadAllText(QueryPath + "modelling/newAreaIpData.graphql");
                getConnectionIdsForNwGroup = File.ReadAllText(QueryPath + "modelling/getConnectionIdsForNwGroup.graphql");

                getAppServers = appServerDetailsFragment + File.ReadAllText(QueryPath + "modelling/getAppServers.graphql");
                getImportedAppServers = appServerDetailsFragment + File.ReadAllText(QueryPath + "modelling/getImportedAppServers.graphql");
                newAppServer = File.ReadAllText(QueryPath + "modelling/newAppServer.graphql");
                updateAppServer = File.ReadAllText(QueryPath + "modelling/updateAppServer.graphql");
                setAppServerDeletedState = File.ReadAllText(QueryPath + "modelling/setAppServerDeletedState.graphql");
                setAppServerName = File.ReadAllText(QueryPath + "modelling/setAppServerName.graphql");
                setAppServerType = File.ReadAllText(QueryPath + "modelling/setAppServerType.graphql");
                deleteAppServer = File.ReadAllText(QueryPath + "modelling/deleteAppServer.graphql");
                getAppRolesForAppServer = File.ReadAllText(QueryPath + "modelling/getAppRolesForAppServer.graphql");
                getConnectionIdsForAppServer = File.ReadAllText(QueryPath + "modelling/getConnectionIdsForAppServer.graphql");

                getPublishedInterfaces = connectionDetailsFragment + File.ReadAllText(QueryPath + "modelling/getPublishedInterfaces.graphql");
                getConnectionById = connectionDetailsFragment + File.ReadAllText(QueryPath + "modelling/getConnectionById.graphql");                
                getConnections = connectionDetailsFragment + File.ReadAllText(QueryPath + "modelling/getConnections.graphql");
                getConnectionsResolved = connectionResolvedDetailsFragment + File.ReadAllText(QueryPath + "modelling/getConnectionsResolved.graphql");
                getConnectionsByTicketId = connectionDetailsFragment + File.ReadAllText(QueryPath + "modelling/getConnectionsByTicketId.graphql");
                getInterfaceUsers = File.ReadAllText(QueryPath + "modelling/getInterfaceUsers.graphql");
                getCommonServices = connectionDetailsFragment + File.ReadAllText(QueryPath + "modelling/getCommonServices.graphql");
                newConnection = File.ReadAllText(QueryPath + "modelling/newConnection.graphql");
                updateConnection = File.ReadAllText(QueryPath + "modelling/updateConnection.graphql");
                updateProposedConnectionOwner = File.ReadAllText(QueryPath + "modelling/updateProposedConnectionOwner.graphql");
                updateConnectionPublish = File.ReadAllText(QueryPath + "modelling/updateConnectionPublish.graphql");
                updateConnectionProperties = File.ReadAllText(QueryPath + "modelling/updateConnectionProperties.graphql");
                replaceUsedInterface = File.ReadAllText(QueryPath + "modelling/replaceUsedInterface.graphql");
                deleteConnection = File.ReadAllText(QueryPath + "modelling/deleteConnection.graphql");
                addAppServerToConnection = File.ReadAllText(QueryPath + "modelling/addAppServerToConnection.graphql");
                removeAppServerFromConnection = File.ReadAllText(QueryPath + "modelling/removeAppServerFromConnection.graphql");
                addNwGroupToConnection = File.ReadAllText(QueryPath + "modelling/addNwGroupToConnection.graphql");
                removeNwGroupFromConnection = File.ReadAllText(QueryPath + "modelling/removeNwGroupFromConnection.graphql");
                addServiceToConnection = File.ReadAllText(QueryPath + "modelling/addServiceToConnection.graphql");
                removeServiceFromConnection = File.ReadAllText(QueryPath + "modelling/removeServiceFromConnection.graphql");
                addServiceGroupToConnection = File.ReadAllText(QueryPath + "modelling/addServiceGroupToConnection.graphql");
                removeServiceGroupFromConnection = File.ReadAllText(QueryPath + "modelling/removeServiceGroupFromConnection.graphql");
                getConnectionIdsForService = File.ReadAllText(QueryPath + "modelling/getConnectionIdsForService.graphql");
                getConnectionIdsForServiceGroup = File.ReadAllText(QueryPath + "modelling/getConnectionIdsForServiceGroup.graphql");

                getSelectedConnections = connectionDetailsFragment + File.ReadAllText(QueryPath + "modelling/getSelectedConnections.graphql");
                addSelectedConnection = File.ReadAllText(QueryPath + "modelling/addSelectedConnection.graphql");
                removeSelectedConnectionFromApp = File.ReadAllText(QueryPath + "modelling/removeSelectedConnectionFromApp.graphql");
                removeSelectedConnection = File.ReadAllText(QueryPath + "modelling/removeSelectedConnection.graphql");

                getNwGroupObjects = File.ReadAllText(QueryPath + "modelling/getNwGroupObjects.graphql");
                getSelectedNwGroupObjects = File.ReadAllText(QueryPath + "modelling/getSelectedNwGroupObjects.graphql");
                addSelectedNwGroupObject = File.ReadAllText(QueryPath + "modelling/addSelectedNwGroupObject.graphql");
                removeSelectedNwGroupObject = File.ReadAllText(QueryPath + "modelling/removeSelectedNwGroupObject.graphql");
                removeSelectedNwGroupObjectFromAllApps = File.ReadAllText(QueryPath + "modelling/removeSelectedNwGroupObjectFromAllApps.graphql");

                getAppRoles = appServerDetailsFragment + appRoleDetailsFragment + File.ReadAllText(QueryPath + "modelling/getAppRoles.graphql");
                getNewestAppRoles = File.ReadAllText(QueryPath + "modelling/getNewestAppRoles.graphql");
                getDummyAppRole = appServerDetailsFragment + appRoleDetailsFragment + File.ReadAllText(QueryPath + "modelling/getDummyAppRole.graphql");
                newAppRole = File.ReadAllText(QueryPath + "modelling/newAppRole.graphql");
                updateAppRole = File.ReadAllText(QueryPath + "modelling/updateAppRole.graphql");
                deleteNwGroup = File.ReadAllText(QueryPath + "modelling/deleteNwGroup.graphql");
                // getAppServerForAppRole = appServerDetailsFragment + File.ReadAllText(QueryPath + "modelling/getAppServerForAppRole.graphql");
                addNwObjectToNwGroup = File.ReadAllText(QueryPath + "modelling/addNwObjectToNwGroup.graphql");
                removeNwObjectFromNwGroup = File.ReadAllText(QueryPath + "modelling/removeNwObjectFromNwGroup.graphql");

                getServicesForApp = serviceDetailsFragment + File.ReadAllText(QueryPath + "modelling/getServicesForApp.graphql");
                getGlobalServices = serviceDetailsFragment + File.ReadAllText(QueryPath + "modelling/getGlobalServices.graphql");
                newService = File.ReadAllText(QueryPath + "modelling/newService.graphql");
                updateService = File.ReadAllText(QueryPath + "modelling/updateService.graphql");
                deleteService = File.ReadAllText(QueryPath + "modelling/deleteService.graphql");

                getServiceGroupsForApp = serviceDetailsFragment + serviceGroupDetailsFragment + File.ReadAllText(QueryPath + "modelling/getServiceGroupsForApp.graphql");
                getServiceGroupById = serviceDetailsFragment + serviceGroupDetailsFragment + File.ReadAllText(QueryPath + "modelling/getServiceGroupById.graphql");
                getGlobalServiceGroups = serviceDetailsFragment + serviceGroupDetailsFragment + File.ReadAllText(QueryPath + "modelling/getGlobalServiceGroups.graphql");
                newServiceGroup = File.ReadAllText(QueryPath + "modelling/newServiceGroup.graphql");
                updateServiceGroup = File.ReadAllText(QueryPath + "modelling/updateServiceGroup.graphql");
                deleteServiceGroup = File.ReadAllText(QueryPath + "modelling/deleteServiceGroup.graphql");
                addServiceToServiceGroup = File.ReadAllText(QueryPath + "modelling/addServiceToServiceGroup.graphql");
                removeServiceFromServiceGroup = File.ReadAllText(QueryPath + "modelling/removeServiceFromServiceGroup.graphql");
                getServiceGroupIdsForService = File.ReadAllText(QueryPath + "modelling/getServiceGroupIdsForService.graphql");

                getHistory = File.ReadAllText(QueryPath + "modelling/getHistory.graphql");
                getHistoryForApp = File.ReadAllText(QueryPath + "modelling/getHistoryForApp.graphql");
                addHistoryEntry = File.ReadAllText(QueryPath + "modelling/addHistoryEntry.graphql");

                newAppZone = File.ReadAllText(QueryPath + "modelling/addNwAppZone.graphql");
                getAppZonesByAppId = appServerDetailsFragment + File.ReadAllText(QueryPath + "modelling/getAppZonesByAppId.graphql");
            }
            catch (Exception exception)
            {
                Log.WriteError("Initialize ModellingQueries", "Api ModellingQueries could not be loaded.", exception);
                Environment.Exit(-1);
            }
        }
    }
}
