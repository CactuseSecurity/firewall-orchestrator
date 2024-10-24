using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using FWO.Logging;

namespace FWO.Api.Client.Queries
{
    public class DeviceQueries : Queries
    {
        public static readonly string getDevicesByManagement;
        public static readonly string getManagementNames;
        public static readonly string getManagementsDetails;
        public static readonly string getManagementDetailsWithoutSecrets;
        public static readonly string getDeviceTypeDetails;
        public static readonly string newManagement;
        public static readonly string updateManagement;
        public static readonly string changeManagementState;
        public static readonly string deleteManagement;
        public static readonly string getDeviceDetails;
        public static readonly string newDevice;
        public static readonly string updateDevice;
        public static readonly string changeDeviceState;
        public static readonly string deleteDevice;
        public static readonly string deleteImport;
        public static readonly string getCredentials;
        public static readonly string getCredentialsWithoutSecrets;
        public static readonly string newCredential;
        public static readonly string updateCredential;
        public static readonly string deleteCredential;
        public static readonly string getMgmtNumberUsingCred;

        static DeviceQueries()
        {
            try
            {
                getDevicesByManagement = File.ReadAllText(QueryPath + "device/getDevicesByManagement.graphql");
                getManagementNames = File.ReadAllText(QueryPath + "device/getManagementNames.graphql");
                getManagementsDetails = File.ReadAllText(QueryPath + "device/getManagementsDetails.graphql") + " " 
                                        + File.ReadAllText(QueryPath + "device/fragments/managementDetails.graphql") + " "
                                        + File.ReadAllText(QueryPath + "device/fragments/deviceTypeDetails.graphql") + " "
                                        + File.ReadAllText(QueryPath + "device/fragments/importCredentials.graphql");
                getManagementDetailsWithoutSecrets = File.ReadAllText(QueryPath + "device/getManagementDetailsWithoutSecrets.graphql") + " " 
                                        + File.ReadAllText(QueryPath + "device/fragments/managementDetailsWithoutSecrets.graphql") + " "
                                        + File.ReadAllText(QueryPath + "device/fragments/deviceTypeDetails.graphql") + " "
                                        + File.ReadAllText(QueryPath + "device/fragments/importCredentialsWithoutSecrets.graphql");
                getDeviceTypeDetails = File.ReadAllText(QueryPath + "device/getDeviceTypeDetails.graphql") + " "
                                        + File.ReadAllText(QueryPath + "device/fragments/deviceTypeDetails.graphql");
                newManagement = File.ReadAllText(QueryPath + "device/newManagement.graphql");
                updateManagement = File.ReadAllText(QueryPath + "device/updateManagement.graphql");
                changeManagementState = File.ReadAllText(QueryPath + "device/changeManagementState.graphql");
                deleteManagement = File.ReadAllText(QueryPath + "device/deleteManagement.graphql");
                getDeviceDetails = File.ReadAllText(QueryPath + "device/getDeviceDetails.graphql") + " " 
                                    + File.ReadAllText(QueryPath + "device/fragments/deviceDetails.graphql") + " "
                                    + File.ReadAllText(QueryPath + "device/fragments/deviceTypeDetails.graphql");

                newDevice = File.ReadAllText(QueryPath + "device/newDevice.graphql");
                updateDevice = File.ReadAllText(QueryPath + "device/updateDevice.graphql");
                changeDeviceState = File.ReadAllText(QueryPath + "device/changeDeviceState.graphql");
                deleteDevice = File.ReadAllText(QueryPath + "device/deleteDevice.graphql");
                deleteImport = File.ReadAllText(QueryPath + "device/deleteImport.graphql");

                getCredentials = File.ReadAllText(QueryPath + "device/getCredentials.graphql") + " " 
                                    + File.ReadAllText(QueryPath + "device/fragments/importCredentials.graphql");
                getCredentialsWithoutSecrets = File.ReadAllText(QueryPath + "device/getCredentialsWithoutSecrets.graphql") + " " 
                                    + File.ReadAllText(QueryPath + "device/fragments/importCredentialsWithoutSecrets.graphql");
                newCredential = File.ReadAllText(QueryPath + "device/newCredential.graphql");
                updateCredential = File.ReadAllText(QueryPath + "device/updateCredential.graphql");
                deleteCredential = File.ReadAllText(QueryPath + "device/deleteCredential.graphql");
                getMgmtNumberUsingCred = File.ReadAllText(QueryPath + "device/getMgmtNumberUsingCred.graphql");
            }
            catch (Exception exception)
            {
                Log.WriteError("Initialize DeviceQueries", "Api DeviceQueries could not be loaded.", exception);
                Environment.Exit(-1);
            }
        }
    }
}
