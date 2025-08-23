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
        public static readonly string updateManagementUid;
        public static readonly string updateManagementUids;
        public static readonly string changeManagementState;
        public static readonly string deleteManagement;
        public static readonly string getDeviceDetails;
        public static readonly string newDevice;
        public static readonly string getGatewayId;
        public static readonly string updateDevice;
        public static readonly string updateGatewayUid;
        public static readonly string changeDeviceState;
        public static readonly string deleteDevice;
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
                getDevicesByManagement = GetQueryText("device/getDevicesByManagement.graphql");
                getManagementNames = GetQueryText("device/getManagementNames.graphql");
                getManagementsDetails = GetQueryText("device/getManagementsDetails.graphql")
                                        + GetQueryText("device/fragments/subManagements.graphql")
                                        + GetQueryText("device/fragments/managementDetails.graphql")
                                        + GetQueryText("device/fragments/deviceTypeDetails.graphql")
                                        + GetQueryText("device/fragments/importCredentials.graphql");
                getManagementDetailsWithoutSecrets = GetQueryText("device/getManagementDetailsWithoutSecrets.graphql")
                                        + GetQueryText("device/fragments/managementDetailsWithoutSecrets.graphql")
                                        + GetQueryText("device/fragments/deviceTypeDetails.graphql")
                                        + GetQueryText("device/fragments/importCredentialsWithoutSecrets.graphql");
                getDeviceTypeDetails = GetQueryText("device/getDeviceTypeDetails.graphql")
                                        + GetQueryText("device/fragments/deviceTypeDetails.graphql");
                newManagement = GetQueryText("device/newManagement.graphql");
                updateManagement = GetQueryText("device/updateManagement.graphql");
                updateManagementUid = GetQueryText("device/updateManagementUid.graphql");
                updateManagementUids = GetQueryText("device/updateManagementUids.graphql");
                changeManagementState = GetQueryText("device/changeManagementState.graphql");
                deleteManagement = GetQueryText("device/deleteManagement.graphql");
                getDeviceDetails = GetQueryText("device/getDeviceDetails.graphql")
                                    + GetQueryText("device/fragments/deviceDetails.graphql")
                                    + GetQueryText("device/fragments/deviceTypeDetails.graphql");

                newDevice = GetQueryText("device/newDevice.graphql");
                updateDevice = GetQueryText("device/updateDevice.graphql");
                updateGatewayUid = GetQueryText("device/updateGatewayUid.graphql");
                getGatewayId = GetQueryText("device/getGatewayId.graphql");
                changeDeviceState = GetQueryText("device/changeDeviceState.graphql");
                deleteDevice = GetQueryText("device/deleteDevice.graphql");

                getCredentials = GetQueryText("device/getCredentials.graphql")
                                    + GetQueryText("device/fragments/importCredentials.graphql");
                getCredentialsWithoutSecrets = GetQueryText("device/getCredentialsWithoutSecrets.graphql")
                                    + GetQueryText("device/fragments/importCredentialsWithoutSecrets.graphql");
                newCredential = GetQueryText("device/newCredential.graphql");
                updateCredential = GetQueryText("device/updateCredential.graphql");
                deleteCredential = GetQueryText("device/deleteCredential.graphql");
                getMgmtNumberUsingCred = GetQueryText("device/getMgmtNumberUsingCred.graphql");
            }
            catch (Exception exception)
            {
                Log.WriteError("Initialize DeviceQueries", "Api DeviceQueries could not be loaded.", exception);
                Environment.Exit(-1);
            }
        }
    }
}
