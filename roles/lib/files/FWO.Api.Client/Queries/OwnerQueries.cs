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
        public static readonly string updateOwner;
        public static readonly string deactivateOwner;
        public static readonly string deleteOwner;
        // public static readonly string setDefaultOwner;
        public static readonly string setOwnerLastCheck;
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

                getOwners = ownerDetailsFragment + File.ReadAllText(QueryPath + "owner/getOwners.graphql");
                getOwnersWithConn = ownerDetailsFragment + File.ReadAllText(QueryPath + "owner/getOwnersWithConn.graphql");
                getEditableOwners = ownerDetailsFragment + File.ReadAllText(QueryPath + "owner/getEditableOwners.graphql");
                getEditableOwnersWithConn = ownerDetailsFragment + File.ReadAllText(QueryPath + "owner/getEditableOwnersWithConn.graphql");
                newOwner = File.ReadAllText(QueryPath + "owner/newOwner.graphql");
                updateOwner = File.ReadAllText(QueryPath + "owner/updateOwner.graphql");
                deactivateOwner = File.ReadAllText(QueryPath + "owner/deactivateOwner.graphql");
                deleteOwner = File.ReadAllText(QueryPath + "owner/deleteOwner.graphql");
                //setDefaultOwner = File.ReadAllText(QueryPath + "owner/setDefaultOwner.graphql");
                setOwnerLastCheck = File.ReadAllText(QueryPath + "owner/setOwnerLastCheck.graphql");
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
