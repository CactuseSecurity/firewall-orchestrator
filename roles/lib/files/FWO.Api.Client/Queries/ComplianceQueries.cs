using FWO.Logging;

namespace FWO.Api.Client.Queries
{
    public class ComplianceQueries : Queries
    {
        public static readonly string addNetworkZone;
        public static readonly string deleteNetworkZone;
        public static readonly string getNetworkZones;
        public static readonly string getNetworkZonesForMatrix;
        public static readonly string updateNetworkZones;

        public static readonly string modifyNetworkZoneCommunication;

        public static readonly string addViolations;
        public static readonly string getViolations;

        public static readonly string addPolicy;
        public static readonly string disablePolicy;
        public static readonly string getPolicies;

        public static readonly string addCriterion;
        public static readonly string setCriterionRemoved;
        public static readonly string getCriteria;
        public static readonly string getMatrices;
        
        public static readonly string addCritToPolicy;
        public static readonly string removeCritFromPolicy;

        static ComplianceQueries()
        {
            try
            {
                addNetworkZone = File.ReadAllText(QueryPath + "compliance/addNetworkZone.graphql");
                deleteNetworkZone = File.ReadAllText(QueryPath + "compliance/deleteNetworkZone.graphql");
                getNetworkZones = File.ReadAllText(QueryPath + "compliance/getNetworkZones.graphql");
                getNetworkZonesForMatrix = File.ReadAllText(QueryPath + "compliance/getNetworkZonesForMatrix.graphql");
                updateNetworkZones = File.ReadAllText(QueryPath + "compliance/updateNetworkZone.graphql");

                modifyNetworkZoneCommunication = File.ReadAllText(QueryPath + "compliance/updateNetworkZoneCommunication.graphql");

                addViolations = File.ReadAllText(QueryPath + "compliance/addViolations.graphql");
                getViolations = File.ReadAllText(QueryPath + "compliance/getViolations.graphql");

                addPolicy = File.ReadAllText(QueryPath + "compliance/addPolicy.graphql");
                disablePolicy = File.ReadAllText(QueryPath + "compliance/disablePolicy.graphql");
                getPolicies = File.ReadAllText(QueryPath + "compliance/getPolicies.graphql");

                addCriterion = File.ReadAllText(QueryPath + "compliance/addCriterion.graphql");
                setCriterionRemoved = File.ReadAllText(QueryPath + "compliance/setCriterionRemoved.graphql");
                getCriteria = File.ReadAllText(QueryPath + "compliance/getCriteria.graphql");
                getMatrices = File.ReadAllText(QueryPath + "compliance/getMatrices.graphql");

                addCritToPolicy = File.ReadAllText(QueryPath + "compliance/addCritToPolicy.graphql");
                removeCritFromPolicy = File.ReadAllText(QueryPath + "compliance/removeCritFromPolicy.graphql");
            }
            catch (Exception exception)
            {
                Log.WriteError("Initialize Compliance Queries", "Api compliance queries could not be loaded.", exception);
                Environment.Exit(-1);
            }
        }
    }
}
