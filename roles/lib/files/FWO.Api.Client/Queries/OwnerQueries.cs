using FWO.Logging;

namespace FWO.Api.Client.Queries
{
    public class OwnerQueries : Queries
    {
        public static readonly string ownerDetailsFragment;

        public static readonly string getOwnerById;
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
                ownerDetailsFragment = File.ReadAllText(QueryPath + "owner/fragments/ownerDetails.graphql");

                getOwnerById = File.ReadAllText(QueryPath + "owner/getOwnerById.graphql");
                getOwners = ownerDetailsFragment + File.ReadAllText(QueryPath + "owner/getOwners.graphql");
                getOwnersWithConn = ownerDetailsFragment + File.ReadAllText(QueryPath + "owner/getOwnersWithConn.graphql");
                getEditableOwners = ownerDetailsFragment + File.ReadAllText(QueryPath + "owner/getEditableOwners.graphql");
                getEditableOwnersWithConn = ownerDetailsFragment + File.ReadAllText(QueryPath + "owner/getEditableOwnersWithConn.graphql");
                newOwner = File.ReadAllText(QueryPath + "owner/newOwner.graphql");
                newLifeCycle = File.ReadAllText(QueryPath + "owner/newLifeCycle.graphql");
                updateOwner = File.ReadAllText(QueryPath + "owner/updateOwner.graphql");
                updateLifeCycle = File.ReadAllText(QueryPath + "owner/updateLifeCycle.graphql");
                deactivateOwner = File.ReadAllText(QueryPath + "owner/deactivateOwner.graphql");
                deleteOwner = File.ReadAllText(QueryPath + "owner/deleteOwner.graphql");
                deleteLifeCycle = File.ReadAllText(QueryPath + "owner/deleteLifeCycle.graphql");
                getOwnerLifeCycleStates = File.ReadAllText(QueryPath + "owner/getOwnerLifeCycleStates.graphql");
                //setDefaultOwner = File.ReadAllText(QueryPath + "owner/setDefaultOwner.graphql");
                setOwnerLastCheck = File.ReadAllText(QueryPath + "owner/setOwnerLastCheck.graphql");
                setOwnerLastRecert = File.ReadAllText(QueryPath + "owner/setOwnerLastRecert.graphql");
                getOwnersFromGroups = ownerDetailsFragment + File.ReadAllText(QueryPath + "owner/getOwnersFromGroups.graphql");
                getOwnersForUser = ownerDetailsFragment + File.ReadAllText(QueryPath + "owner/getOwnersForUser.graphql");
                getNetworkOwnerships = ownerDetailsFragment + File.ReadAllText(QueryPath + "owner/getNetworkOwnerships.graphql");
                newNetworkOwnership = ownerDetailsFragment + File.ReadAllText(QueryPath + "owner/newNetworkOwnership.graphql");
                deleteNetworkOwnership = ownerDetailsFragment + File.ReadAllText(QueryPath + "owner/deleteNetworkOwnership.graphql");
                deleteAreaIpData = File.ReadAllText(QueryPath + "owner/deleteAreaIpData.graphql");
                getRuleOwnerships = ownerDetailsFragment + File.ReadAllText(QueryPath + "owner/getRuleOwnerships.graphql");
                newRuleOwnership = ownerDetailsFragment + File.ReadAllText(QueryPath + "owner/newRuleOwnership.graphql");
                deleteRuleOwnership = ownerDetailsFragment + File.ReadAllText(QueryPath + "owner/deleteRuleOwnership.graphql");
                getOwnerId = File.ReadAllText(QueryPath + "/owner/getOwnerId.graphql");
            }
            catch (Exception exception)
            {
                Log.WriteError("Initialize OwnerQueries", "Api OwnerQueries could not be loaded.", exception);
                Environment.Exit(-1);
            }
        }
    }
}
