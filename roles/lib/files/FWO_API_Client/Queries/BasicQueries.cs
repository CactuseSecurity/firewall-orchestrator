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
        public static readonly string getDevicesByManagements;
        public static readonly string getManagementsDetails;
        public static readonly string getDeviceTypeDetails;
        public static readonly string newManagement;
        public static readonly string updateManagement;
        public static readonly string deleteManagement;
        public static readonly string getDeviceDetails;
        public static readonly string newDevice;
        public static readonly string updateDevice;
        public static readonly string deleteDevice;
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

                getDevicesByManagements = File.ReadAllText(QueryPath + "device/getDevicesByManagement.graphql");

                getManagementsDetails = File.ReadAllText(QueryPath + "device/getManagementsDetails.graphql") + " " 
                                        + File.ReadAllText(QueryPath + "device/fragments/managementDetails.graphql");

                getDeviceTypeDetails = File.ReadAllText(QueryPath + "device/getDeviceTypeDetails.graphql");

                newManagement = File.ReadAllText(QueryPath + "device/newManagement.graphql");

                updateManagement = File.ReadAllText(QueryPath + "device/updateManagement.graphql");

                deleteManagement = File.ReadAllText(QueryPath + "device/deleteManagement.graphql");
                
                getDeviceDetails = File.ReadAllText(QueryPath + "device/getDeviceDetails.graphql") + " " 
                                   + File.ReadAllText(QueryPath + "device/fragments/deviceDetails.graphql");

                newDevice = File.ReadAllText(QueryPath + "device/newDevice.graphql");

                updateDevice = File.ReadAllText(QueryPath + "device/updateDevice.graphql");

                deleteDevice = File.ReadAllText(QueryPath + "device/deleteDevice.graphql");

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
