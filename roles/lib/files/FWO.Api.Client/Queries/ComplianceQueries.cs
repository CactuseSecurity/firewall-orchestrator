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

        public static readonly string addPolicy;
        public static readonly string disablePolicy;
        public static readonly string getPolicies;
        public static readonly string getPolicyById;

        public static readonly string addCriterion;
        public static readonly string removeCriterion;
        public static readonly string getCriteria;
        public static readonly string getManualMatrices;
		public static readonly string getMatrixBySource;
        
        public static readonly string addCritToPolicy;
        public static readonly string removeCritFromPolicy;
        public static readonly string getPolicyIdsForCrit;

        static ComplianceQueries()
        {
            try
            {
                addNetworkZone = File.ReadAllText(QueryPath + "compliance/addNetworkZone.graphql");
                removeNetworkZone = File.ReadAllText(QueryPath + "compliance/removeNetworkZone.graphql");
                getNetworkZonesForMatrix = File.ReadAllText(QueryPath + "compliance/getNetworkZonesForMatrix.graphql");
                updateNetworkZone = File.ReadAllText(QueryPath + "compliance/updateNetworkZone.graphql");

                updateNetworkZoneCommunication = File.ReadAllText(QueryPath + "compliance/updateNetworkZoneCommunication.graphql");

                addViolations = File.ReadAllText(QueryPath + "compliance/addViolations.graphql");
                getViolations = File.ReadAllText(QueryPath + "compliance/getViolations.graphql");
                updateViolationById = File.ReadAllText(QueryPath + "compliance/updateViolationById.graphql");
                removeViolations = File.ReadAllText(QueryPath + "compliance/removeViolations.graphql");

                addPolicy = File.ReadAllText(QueryPath + "compliance/addPolicy.graphql");
                disablePolicy = File.ReadAllText(QueryPath + "compliance/disablePolicy.graphql");
                getPolicies = File.ReadAllText(QueryPath + "compliance/getPolicies.graphql");
                getPolicyById = File.ReadAllText(QueryPath + "compliance/getPolicyById.graphql");

                addCriterion = File.ReadAllText(QueryPath + "compliance/addCriterion.graphql");
                removeCriterion = File.ReadAllText(QueryPath + "compliance/removeCriterion.graphql");
                getCriteria = File.ReadAllText(QueryPath + "compliance/getCriteria.graphql");
                getManualMatrices = File.ReadAllText(QueryPath + "compliance/getManualMatrices.graphql");
				getMatrixBySource = File.ReadAllText(QueryPath + "compliance/getMatrixBySource.graphql");

                addCritToPolicy = File.ReadAllText(QueryPath + "compliance/addCritToPolicy.graphql");
                removeCritFromPolicy = File.ReadAllText(QueryPath + "compliance/removeCritFromPolicy.graphql");
                getPolicyIdsForCrit = File.ReadAllText(QueryPath + "compliance/getPolicyIdsForCrit.graphql");
            }
            catch (Exception exception)
            {
                Log.WriteError("Initialize Compliance Queries", "Api compliance queries could not be loaded.", exception);
                Environment.Exit(-1);
            }
        }
    }
}
