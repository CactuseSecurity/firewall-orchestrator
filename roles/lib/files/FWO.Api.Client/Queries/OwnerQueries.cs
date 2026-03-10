using FWO.Logging;

namespace FWO.Api.Client.Queries
{
    public class OwnerQueries : Queries
    {
        public static readonly string ownerDetailsFragment;

        public static readonly string getOwnerById;
        public static readonly string getOwners;
        public static readonly string getOwnersForRuleOwner;
        public static readonly string getOwnersWithConn;
        public static readonly string getEditableOwners;
        public static readonly string getEditableOwnersWithConn;
        public static readonly string getOwnersForDns;
        public static readonly string getOwnersForDnsWithConn;
        public static readonly string getOwnersForDnsWithModellingWithConn;
        public static readonly string newOwner;
        public static readonly string newOwnerLifeCycle;
        public static readonly string updateOwner;
        public static readonly string updateOwnerLifeCycle;
        public static readonly string deactivateOwner;
        public static readonly string deleteOwner;
        public static readonly string deleteOwnerLifeCycle;
        public static readonly string getOwnerLifeCycleStates;
        // public static readonly string setDefaultOwner;
        public static readonly string setOwnerLastCheck;
        public static readonly string setOwnerLastRecert;
        public static readonly string getOwnersFromGroups;
        public static readonly string getOwnersForUser;
        public static readonly string getOwnersForDnsWithRecertification;
        public static readonly string getOwnerResponsibleTypes;
        public static readonly string getNetworkOwnerships;
        public static readonly string newNetworkOwnership;
        public static readonly string deleteNetworkOwnership;
        public static readonly string deleteAreaIpData;
        public static readonly string getRuleOwnerships;
        public static readonly string newRuleOwnership;
        public static readonly string deleteRuleOwnership;
        public static readonly string getOwnerId;
        public static readonly string newOwnerResponsibles;
        public static readonly string deleteOwnerResponsibles;
        public static readonly string setAllActiveRuleOwnersRemoved;
        public static readonly string setAffectedRuleOwnersRemoved;
        public static readonly string insertRuleOwners;
        public static readonly string getRuleOwnerToRemoveByRule;
        public static readonly string getRuleOwnerToRemoveByOwner;
        public static readonly string newOwnerResponsibleType;
        public static readonly string updateOwnerResponsibleType;
        public static readonly string updateChangelogOwner;
        public static readonly string getChangedOwnersForRuleOwnerMapping;


        static OwnerQueries()
        {
            try
            {
                ownerDetailsFragment = GetQueryText("owner/fragments/ownerDetails.graphql");

                getOwnerById = GetQueryText("owner/getOwnerById.graphql");
                getOwners = ownerDetailsFragment + GetQueryText("owner/getOwners.graphql");
                getOwnersForRuleOwner = GetQueryText("owner/getOwnersForRuleOwner.graphql");
                getOwnersWithConn = ownerDetailsFragment + GetQueryText("owner/getOwnersWithConn.graphql");
                getEditableOwners = ownerDetailsFragment + GetQueryText("owner/getEditableOwners.graphql");
                getEditableOwnersWithConn = ownerDetailsFragment + GetQueryText("owner/getEditableOwnersWithConn.graphql");
                getOwnersForDns = ownerDetailsFragment + GetQueryText("owner/getOwnersForDns.graphql");
                getOwnersForDnsWithConn = ownerDetailsFragment + GetQueryText("owner/getOwnersForDnsWithConn.graphql");
                getOwnersForDnsWithModellingWithConn = ownerDetailsFragment + GetQueryText("owner/getOwnersForDnsWithModellingWithConn.graphql");
                newOwner = GetQueryText("owner/newOwner.graphql");
                newOwnerLifeCycle = GetQueryText("owner/newOwnerLifeCycle.graphql");
                updateOwner = GetQueryText("owner/updateOwner.graphql");
                updateOwnerLifeCycle = GetQueryText("owner/updateOwnerLifeCycle.graphql");
                deactivateOwner = GetQueryText("owner/deactivateOwner.graphql");
                deleteOwner = GetQueryText("owner/deleteOwner.graphql");
                deleteOwnerLifeCycle = GetQueryText("owner/deleteOwnerLifeCycle.graphql");
                getOwnerLifeCycleStates = GetQueryText("owner/getOwnerLifeCycleStates.graphql");
                setOwnerLastCheck = GetQueryText("owner/setOwnerLastCheck.graphql");
                setOwnerLastRecert = GetQueryText("owner/setOwnerLastRecert.graphql");
                getOwnersFromGroups = ownerDetailsFragment + GetQueryText("owner/getOwnersFromGroups.graphql");
                getOwnersForUser = ownerDetailsFragment + GetQueryText("owner/getOwnersForUser.graphql");
                getOwnersForDnsWithRecertification = ownerDetailsFragment + GetQueryText("owner/getOwnersForDnsWithRecertification.graphql");
                getOwnerResponsibleTypes = GetQueryText("owner/getOwnerResponsibleTypes.graphql");
                getNetworkOwnerships = ownerDetailsFragment + GetQueryText("owner/getNetworkOwnerships.graphql");
                newNetworkOwnership = ownerDetailsFragment + GetQueryText("owner/newNetworkOwnership.graphql");
                deleteNetworkOwnership = ownerDetailsFragment + GetQueryText("owner/deleteNetworkOwnership.graphql");
                deleteAreaIpData = GetQueryText("owner/deleteAreaIpData.graphql");
                getRuleOwnerships = GetQueryText("owner/getRuleOwnerships.graphql");
                newRuleOwnership = ownerDetailsFragment + GetQueryText("owner/newRuleOwnership.graphql");
                deleteRuleOwnership = ownerDetailsFragment + GetQueryText("owner/deleteRuleOwnership.graphql");
                getOwnerId = GetQueryText("owner/getOwnerId.graphql");
                newOwnerResponsibles = GetQueryText("owner/newOwnerResponsibles.graphql");
                deleteOwnerResponsibles = GetQueryText("owner/deleteOwnerResponsibles.graphql");
                setAllActiveRuleOwnersRemoved = GetQueryText("owner/setAllActiveRuleOwnersRemoved.graphql");
                setAffectedRuleOwnersRemoved = GetQueryText("owner/setAffectedRuleOwnersRemoved.graphql");
                insertRuleOwners = GetQueryText("owner/insertRuleOwners.graphql");
                getRuleOwnerToRemoveByRule = GetQueryText("owner/getRuleOwnerToRemoveByRule.graphql");
                newOwnerResponsibleType = GetQueryText("owner/newOwnerResponsibleType.graphql");
                updateOwnerResponsibleType = GetQueryText("owner/updateOwnerResponsibleType.graphql");
                updateChangelogOwner = GetQueryText("owner/updateChangelogOwner.graphql");
                getChangedOwnersForRuleOwnerMapping = GetQueryText("owner/getChangedOwnersForRuleOwnerMapping.graphql");
                getRuleOwnerToRemoveByOwner = GetQueryText("owner/getRuleOwnerToRemoveByOwner.graphql");
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
