using FWO.Logging;

namespace FWO.Api.Client.Queries
{
    public class ComplianceQueries : Queries
    {
        public static readonly string addNetworkZone;
        public static readonly string removeNetworkZone;
        public static readonly string getNetworkZonesForMatrix;
        public static readonly string updateNetworkZone;

        public static readonly string updateNetworkZoneCommunication;

        public static readonly string addViolations;
        public static readonly string getViolations;
        public static readonly string updateViolationById;
        public static readonly string removeViolations;
        public static readonly string getViolationsChunk;
        public static readonly string getViolationsByRuleID;
        public static readonly string getViolationsByRuleUid;

        public static readonly string addPolicy;
        public static readonly string disablePolicy;
        public static readonly string getPolicies;
        public static readonly string getPolicyById;

        public static readonly string addCriterion;
        public static readonly string removeCriterion;
        public static readonly string updateCriterionMetadata;
        public static readonly string getCriteria;
        public static readonly string getMatrices;
        public static readonly string getMatrixByName;
        
        public static readonly string addCritToPolicy;
        public static readonly string removeCritFromPolicy;
        public static readonly string getPolicyIdsForCrit;

        static ComplianceQueries()
        {
            try
            {
                addNetworkZone = GetQueryText("compliance/addNetworkZone.graphql");
                removeNetworkZone = GetQueryText("compliance/removeNetworkZone.graphql");
                getNetworkZonesForMatrix = GetQueryText("compliance/getNetworkZonesForMatrix.graphql");
                updateNetworkZone = GetQueryText("compliance/updateNetworkZone.graphql");

                updateNetworkZoneCommunication = GetQueryText("compliance/updateNetworkZoneCommunication.graphql");

                addViolations = GetQueryText("compliance/addViolations.graphql");
                getViolations = GetQueryText("compliance/getViolations.graphql");
                updateViolationById = GetQueryText("compliance/updateViolationById.graphql");
                removeViolations = GetQueryText("compliance/removeViolations.graphql");

                addPolicy = GetQueryText("compliance/addPolicy.graphql");
                disablePolicy = GetQueryText("compliance/disablePolicy.graphql");
                getPolicies = GetQueryText("compliance/getPolicies.graphql");
                getPolicyById = GetQueryText("compliance/getPolicyById.graphql");

                addCriterion = GetQueryText("compliance/addCriterion.graphql");
                removeCriterion = GetQueryText("compliance/removeCriterion.graphql");
                updateCriterionMetadata = GetQueryText("compliance/updateCriterionMetadata.graphql");
                getCriteria = GetQueryText("compliance/getCriteria.graphql");
                getMatrices = GetQueryText("compliance/getMatrices.graphql");
                getMatrixByName = GetQueryText("compliance/getMatrixByName.graphql");

                addCritToPolicy = GetQueryText("compliance/addCritToPolicy.graphql");
                removeCritFromPolicy = GetQueryText("compliance/removeCritFromPolicy.graphql");
                getPolicyIdsForCrit = GetQueryText("compliance/getPolicyIdsForCrit.graphql");

                getViolationsChunk = GetQueryText("compliance/getViolationsChunk.graphql");
                getViolationsByRuleID = GetQueryText("compliance/getViolationsByRuleID.graphql");
                getViolationsByRuleUid = GetQueryText("compliance/getViolationsByRuleUid.graphql");
            }
            catch (Exception exception)
            {
                Log.WriteError("Initialize Compliance Queries", "Api compliance queries could not be loaded.", exception);
                Environment.Exit(-1);
            }
        }
    }
}
