using FWO.Logging;

namespace FWO.Api.Client.Queries
{
    public class OwnerQueries : Queries
    {
        public static readonly string ownerDetailsFragment;

        public static readonly string getOwners;
        public static readonly string getOwnersWithConn;
        public static readonly string getEditableOwners;
        public static readonly string getEditableOwnersWithConn;
        public static readonly string newOwner;
        public static readonly string newLifeCycle;
        public static readonly string updateOwner;
        public static readonly string updateLifeCycle;
        public static readonly string deactivateOwner;
        public static readonly string deleteOwner;
        public static readonly string deleteLifeCycle;
        public static readonly string getOwnerLifeCycleStates;
        // public static readonly string setDefaultOwner;
        public static readonly string setOwnerLastCheck;
        public static readonly string setOwnerLastRecert;
        public static readonly string getOwnersFromGroups;
        public static readonly string getOwnersForUser;
        public static readonly string getNetworkOwnerships;
        public static readonly string newNetworkOwnership;
        public static readonly string deleteNetworkOwnership;
        public static readonly string deleteAreaIpData;
        public static readonly string getRuleOwnerships;
        public static readonly string newRuleOwnership;
        public static readonly string deleteRuleOwnership;
        public static readonly string getOwnerId;


        static OwnerQueries()
        {
            try
            {
                ownerDetailsFragment = GetQueryText("owner/fragments/ownerDetails.graphql");

                getOwners = ownerDetailsFragment + GetQueryText("owner/getOwners.graphql");
                getOwnersWithConn = ownerDetailsFragment + GetQueryText("owner/getOwnersWithConn.graphql");
                getEditableOwners = ownerDetailsFragment + GetQueryText("owner/getEditableOwners.graphql");
                getEditableOwnersWithConn = ownerDetailsFragment + GetQueryText("owner/getEditableOwnersWithConn.graphql");
                newOwner = GetQueryText("owner/newOwner.graphql");
                newLifeCycle = GetQueryText("owner/newLifeCycle.graphql");
                updateOwner = GetQueryText("owner/updateOwner.graphql");
                updateLifeCycle = GetQueryText("owner/updateLifeCycle.graphql");
                deactivateOwner = GetQueryText("owner/deactivateOwner.graphql");
                deleteOwner = GetQueryText("owner/deleteOwner.graphql");
                deleteLifeCycle = GetQueryText("owner/deleteLifeCycle.graphql");
                getOwnerLifeCycleStates = GetQueryText("owner/getOwnerLifeCycleStates.graphql");
                //setDefaultOwner = GetQueryText("owner/setDefaultOwner.graphql");
                setOwnerLastCheck = GetQueryText("owner/setOwnerLastCheck.graphql");
                setOwnerLastRecert = GetQueryText("owner/setOwnerLastRecert.graphql");
                getOwnersFromGroups = ownerDetailsFragment + GetQueryText("owner/getOwnersFromGroups.graphql");
                getOwnersForUser = ownerDetailsFragment + GetQueryText("owner/getOwnersForUser.graphql");
                getNetworkOwnerships = ownerDetailsFragment + GetQueryText("owner/getNetworkOwnerships.graphql");
                newNetworkOwnership = ownerDetailsFragment + GetQueryText("owner/newNetworkOwnership.graphql");
                deleteNetworkOwnership = ownerDetailsFragment + GetQueryText("owner/deleteNetworkOwnership.graphql");
                deleteAreaIpData = GetQueryText("owner/deleteAreaIpData.graphql");
                getRuleOwnerships = ownerDetailsFragment + GetQueryText("owner/getRuleOwnerships.graphql");
                newRuleOwnership = ownerDetailsFragment + GetQueryText("owner/newRuleOwnership.graphql");
                deleteRuleOwnership = ownerDetailsFragment + GetQueryText("owner/deleteRuleOwnership.graphql");
                getOwnerId = GetQueryText("/owner/getOwnerId.graphql");
            }
            catch (Exception exception)
            {
                Log.WriteError("Initialize OwnerQueries", "Api OwnerQueries could not be loaded.", exception);
#if RELEASE
                Environment.Exit(-1);
#else
                throw;
#endif
            }
        }
    }
}
