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
        public static readonly string setNwGroupDeletedState;
        public static readonly string newAreaIpData;
        public static readonly string getConnectionIdsForNwGroup;

        public static readonly string getAppServersByIp;
        public static readonly string getAppServersByName;
        public static readonly string getAppServersForOwner;
        public static readonly string getAppServersBySource;
        public static readonly string getAllAppServers;
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
        public static readonly string getDeletedConnections;
        public static readonly string getInterfaceUsers;
        public static readonly string getCommonServices;
        public static readonly string newConnection;
        public static readonly string updateConnection;
        public static readonly string updateProposedConnectionOwner;
        public static readonly string updateConnectionPublish;
        public static readonly string updateConnectionProperties;
        public static readonly string replaceUsedInterface;
        public static readonly string updateConnectionFwRequested;
        public static readonly string updateConnectionRemove;
        public static readonly string updateConnectionDecommission;
        public static readonly string deleteConnection;
        public static readonly string addAppServerToConnection;
        public static readonly string removeAppServerFromConnection;
        public static readonly string removeAllAppServersFromConnection;
        public static readonly string updateNwObjectInConnection;
        public static readonly string addNwGroupToConnection;
        public static readonly string removeNwGroupFromConnection;
        public static readonly string removeAllNwGroupsFromConnection;
        public static readonly string addServiceToConnection;
        public static readonly string removeServiceFromConnection;
        public static readonly string removeAllServicesFromConnection;
        public static readonly string addServiceGroupToConnection;
        public static readonly string removeServiceGroupFromConnection;
        public static readonly string removeAllServiceGroupsFromConnection;
        public static readonly string getConnectionIdsForService;
        public static readonly string getConnectionIdsForServiceGroup;
        public static readonly string getConnectionsForNwGroup;

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
        public static readonly string updateNwObjectInNwGroup;

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
                appServerDetailsFragment = GetQueryText("modelling/fragments/appServerDetails.graphql");
                appRoleDetailsFragment = GetQueryText("modelling/fragments/appRoleDetails.graphql");
                areaDetailsFragment = GetQueryText("modelling/fragments/areaDetails.graphql");
                serviceDetailsFragment = GetQueryText("modelling/fragments/serviceDetails.graphql");
                serviceGroupDetailsFragment = GetQueryText("modelling/fragments/serviceGroupDetails.graphql");
                connectionDetailsFragment = appServerDetailsFragment + appRoleDetailsFragment + serviceDetailsFragment + serviceGroupDetailsFragment +
                    GetQueryText("modelling/fragments/connectionDetails.graphql");
                connectionResolvedDetailsFragment = appServerDetailsFragment + appRoleDetailsFragment + areaDetailsFragment + serviceDetailsFragment + serviceGroupDetailsFragment +
                    GetQueryText("modelling/fragments/connectionResolvedDetails.graphql");

                getAreas = areaDetailsFragment + GetQueryText("modelling/getAreas.graphql");
                newArea = GetQueryText("modelling/newArea.graphql");
                setNwGroupDeletedState = GetQueryText("modelling/setNwGroupDeletedState.graphql");
                // setAreaDeletedState = GetQueryText("modelling/setAreaDeletedState.graphql");
                newAreaIpData = GetQueryText("modelling/newAreaIpData.graphql");
                getConnectionIdsForNwGroup = GetQueryText("modelling/getConnectionIdsForNwGroup.graphql");
                getConnectionsForNwGroup = connectionDetailsFragment + GetQueryText("modelling/getConnectionsForNwGroup.graphql");

                getAppServersByIp = appServerDetailsFragment + GetQueryText("modelling/getAppServersByIp.graphql");
                getAppServersByName = appServerDetailsFragment + GetQueryText("modelling/getAppServersByName.graphql");
                getAppServersForOwner = appServerDetailsFragment + GetQueryText("modelling/getAppServersForOwner.graphql");
                getAppServersBySource = appServerDetailsFragment + GetQueryText("modelling/getAppServersBySource.graphql");
                getAllAppServers = appServerDetailsFragment + GetQueryText("modelling/getAllAppServers.graphql");
                newAppServer = GetQueryText("modelling/newAppServer.graphql");
                updateAppServer = GetQueryText("modelling/updateAppServer.graphql");
                setAppServerDeletedState = GetQueryText("modelling/setAppServerDeletedState.graphql");
                setAppServerName = GetQueryText("modelling/setAppServerName.graphql");
                setAppServerType = GetQueryText("modelling/setAppServerType.graphql");
                deleteAppServer = GetQueryText("modelling/deleteAppServer.graphql");
                getAppRolesForAppServer = GetQueryText("modelling/getAppRolesForAppServer.graphql");
                getConnectionIdsForAppServer = GetQueryText("modelling/getConnectionIdsForAppServer.graphql");

                getPublishedInterfaces = connectionDetailsFragment + GetQueryText("modelling/getPublishedInterfaces.graphql");
                getConnectionById = connectionDetailsFragment + GetQueryText("modelling/getConnectionById.graphql");
                getConnections = connectionDetailsFragment + GetQueryText("modelling/getConnections.graphql");
                getConnectionsResolved = connectionResolvedDetailsFragment + GetQueryText("modelling/getConnectionsResolved.graphql");
                getConnectionsByTicketId = connectionDetailsFragment + GetQueryText("modelling/getConnectionsByTicketId.graphql");
                getDeletedConnections = connectionDetailsFragment + GetQueryText("modelling/getDeletedConnections.graphql");
                getInterfaceUsers = GetQueryText("modelling/getInterfaceUsers.graphql");
                getCommonServices = connectionDetailsFragment + GetQueryText("modelling/getCommonServices.graphql");
                newConnection = GetQueryText("modelling/newConnection.graphql");
                updateConnection = GetQueryText("modelling/updateConnection.graphql");
                updateProposedConnectionOwner = GetQueryText("modelling/updateProposedConnectionOwner.graphql");
                updateConnectionPublish = GetQueryText("modelling/updateConnectionPublish.graphql");
                updateConnectionProperties = GetQueryText("modelling/updateConnectionProperties.graphql");
                replaceUsedInterface = GetQueryText("modelling/replaceUsedInterface.graphql");
                updateConnectionFwRequested = GetQueryText("modelling/updateConnectionFwRequested.graphql");
                updateConnectionRemove = GetQueryText("modelling/updateConnectionRemove.graphql");
                deleteConnection = GetQueryText("modelling/deleteConnection.graphql");
                addAppServerToConnection = GetQueryText("modelling/addAppServerToConnection.graphql");
                removeAppServerFromConnection = GetQueryText("modelling/removeAppServerFromConnection.graphql");
                removeAllAppServersFromConnection = GetQueryText("modelling/removeAllAppServersFromConnection.graphql");
                updateNwObjectInConnection = GetQueryText("modelling/updateNwObjectInConnection.graphql");
                addNwGroupToConnection = GetQueryText("modelling/addNwGroupToConnection.graphql");
                removeNwGroupFromConnection = GetQueryText("modelling/removeNwGroupFromConnection.graphql");
                removeAllNwGroupsFromConnection = GetQueryText("modelling/removeAllNwGroupsFromConnection.graphql");
                addServiceToConnection = GetQueryText("modelling/addServiceToConnection.graphql");
                removeServiceFromConnection = GetQueryText("modelling/removeServiceFromConnection.graphql");
                removeAllServicesFromConnection = GetQueryText("modelling/removeAllServicesFromConnection.graphql");
                addServiceGroupToConnection = GetQueryText("modelling/addServiceGroupToConnection.graphql");
                removeServiceGroupFromConnection = GetQueryText("modelling/removeServiceGroupFromConnection.graphql");
                removeAllServiceGroupsFromConnection = GetQueryText("modelling/removeAllServiceGroupsFromConnection.graphql");
                getConnectionIdsForService = GetQueryText("modelling/getConnectionIdsForService.graphql");
                getConnectionIdsForServiceGroup = GetQueryText("modelling/getConnectionIdsForServiceGroup.graphql");
                updateConnectionDecommission = string.Empty;

                getSelectedConnections = connectionDetailsFragment + GetQueryText("modelling/getSelectedConnections.graphql");
                addSelectedConnection = GetQueryText("modelling/addSelectedConnection.graphql");
                removeSelectedConnectionFromApp = GetQueryText("modelling/removeSelectedConnectionFromApp.graphql");
                removeSelectedConnection = GetQueryText("modelling/removeSelectedConnection.graphql");

                getNwGroupObjects = GetQueryText("modelling/getNwGroupObjects.graphql");
                getSelectedNwGroupObjects = GetQueryText("modelling/getSelectedNwGroupObjects.graphql");
                addSelectedNwGroupObject = GetQueryText("modelling/addSelectedNwGroupObject.graphql");
                removeSelectedNwGroupObject = GetQueryText("modelling/removeSelectedNwGroupObject.graphql");
                removeSelectedNwGroupObjectFromAllApps = GetQueryText("modelling/removeSelectedNwGroupObjectFromAllApps.graphql");

                getAppRoles = appServerDetailsFragment + appRoleDetailsFragment + GetQueryText("modelling/getAppRoles.graphql");
                getNewestAppRoles = GetQueryText("modelling/getNewestAppRoles.graphql");
                getDummyAppRole = appServerDetailsFragment + appRoleDetailsFragment + GetQueryText("modelling/getDummyAppRole.graphql");
                newAppRole = GetQueryText("modelling/newAppRole.graphql");
                updateAppRole = GetQueryText("modelling/updateAppRole.graphql");
                deleteNwGroup = GetQueryText("modelling/deleteNwGroup.graphql");
                // getAppServerForAppRole = appServerDetailsFragment + GetQueryText("modelling/getAppServerForAppRole.graphql");
                addNwObjectToNwGroup = GetQueryText("modelling/addNwObjectToNwGroup.graphql");
                removeNwObjectFromNwGroup = GetQueryText("modelling/removeNwObjectFromNwGroup.graphql");
                updateNwObjectInNwGroup = GetQueryText("modelling/updateNwObjectInNwGroup.graphql");

                getServicesForApp = serviceDetailsFragment + GetQueryText("modelling/getServicesForApp.graphql");
                getGlobalServices = serviceDetailsFragment + GetQueryText("modelling/getGlobalServices.graphql");
                newService = GetQueryText("modelling/newService.graphql");
                updateService = GetQueryText("modelling/updateService.graphql");
                deleteService = GetQueryText("modelling/deleteService.graphql");

                getServiceGroupsForApp = serviceDetailsFragment + serviceGroupDetailsFragment + GetQueryText("modelling/getServiceGroupsForApp.graphql");
                getServiceGroupById = serviceDetailsFragment + serviceGroupDetailsFragment + GetQueryText("modelling/getServiceGroupById.graphql");
                getGlobalServiceGroups = serviceDetailsFragment + serviceGroupDetailsFragment + GetQueryText("modelling/getGlobalServiceGroups.graphql");
                newServiceGroup = GetQueryText("modelling/newServiceGroup.graphql");
                updateServiceGroup = GetQueryText("modelling/updateServiceGroup.graphql");
                deleteServiceGroup = GetQueryText("modelling/deleteServiceGroup.graphql");
                addServiceToServiceGroup = GetQueryText("modelling/addServiceToServiceGroup.graphql");
                removeServiceFromServiceGroup = GetQueryText("modelling/removeServiceFromServiceGroup.graphql");
                getServiceGroupIdsForService = GetQueryText("modelling/getServiceGroupIdsForService.graphql");

                getHistory = GetQueryText("modelling/getHistory.graphql");
                getHistoryForApp = GetQueryText("modelling/getHistoryForApp.graphql");
                addHistoryEntry = GetQueryText("modelling/addHistoryEntry.graphql");

                newAppZone = GetQueryText("modelling/addNwAppZone.graphql");
                getAppZonesByAppId = appServerDetailsFragment + GetQueryText("modelling/getAppZonesByAppId.graphql");
            }
            catch (Exception exception)
            {
                Log.WriteError("Initialize ModellingQueries", "Api ModellingQueries could not be loaded.", exception);
                Environment.Exit(-1);
            }
        }
    }
}
