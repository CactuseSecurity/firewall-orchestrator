using FWO.Logging;

namespace FWO.Api.Client.Queries
{
    public class ModellingQueries : Queries
    {
        public static readonly string getAreas;
        public static readonly string getConnections;
        public static readonly string getInterfaces;
        public static readonly string getAppServer;
        public static readonly string getAppRoles;
        public static readonly string getAppServerForAppRole;
        public static readonly string getServicesForApp;
        public static readonly string getServiceGroupsForApp;
        public static readonly string getGlobalServiceGroups;
        public static readonly string newServiceGroup;
        public static readonly string updateServiceGroup;
        public static readonly string addServiceToServiceGroup;
        public static readonly string removeServiceFromServiceGroup;
        public static readonly string deleteServiceGroup;
        


        static ModellingQueries()
        {
            try
            {
                getAreas = File.ReadAllText(QueryPath + "modelling/getAreas.graphql");
                getConnections = File.ReadAllText(QueryPath + "modelling/getConnections.graphql");
                getInterfaces = File.ReadAllText(QueryPath + "modelling/getInterfaces.graphql");
                getAppServer = File.ReadAllText(QueryPath + "modelling/getAppServer.graphql");
                getAppRoles = File.ReadAllText(QueryPath + "modelling/getAppRoles.graphql");
                getAppServerForAppRole = File.ReadAllText(QueryPath + "modelling/getAppServerForAppRole.graphql");
                getServicesForApp = File.ReadAllText(QueryPath + "modelling/getServicesForApp.graphql");

                getServiceGroupsForApp = File.ReadAllText(QueryPath + "modelling/getServiceGroupsForApp.graphql");
                getGlobalServiceGroups = File.ReadAllText(QueryPath + "modelling/getGlobalServiceGroups.graphql");
                newServiceGroup = File.ReadAllText(QueryPath + "modelling/newServiceGroup.graphql");
                updateServiceGroup = File.ReadAllText(QueryPath + "modelling/updateServiceGroup.graphql");
                addServiceToServiceGroup = File.ReadAllText(QueryPath + "modelling/addServiceToServiceGroup.graphql");
                removeServiceFromServiceGroup = File.ReadAllText(QueryPath + "modelling/removeServiceFromServiceGroup.graphql");
                deleteServiceGroup = File.ReadAllText(QueryPath + "modelling/deleteServiceGroup.graphql");
            }
            catch (Exception exception)
            {
                Log.WriteError("Initialize ModellingQueries", "Api ModellingQueries could not be loaded.", exception);
                Environment.Exit(-1);
            }
        }
    }
}
