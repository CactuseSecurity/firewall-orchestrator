using FWO.Logging;

namespace FWO.Api.Client.Queries
{
    public class ComplianceQueries : Queries
    {
        public static readonly string getCriteria;

        public static readonly string addNetworkZone;
        public static readonly string deleteNetworkZone;
        public static readonly string getNetworkZones;
        public static readonly string updateNetworkZones;

        public static readonly string modifyNetworkZoneCommunication;

        public static readonly string getPolicies;

        public static readonly string addViolations;
        public static readonly string getViolations;

        static ComplianceQueries()
        {
            try
            {
                getCriteria = File.ReadAllText(QueryPath + "compliance/getCriteria.graphql");

                addNetworkZone = File.ReadAllText(QueryPath + "compliance/addNetworkZone.graphql");
                deleteNetworkZone = File.ReadAllText(QueryPath + "compliance/deleteNetworkZone.graphql");
                getNetworkZones = File.ReadAllText(QueryPath + "compliance/getNetworkZones.graphql");
                updateNetworkZones = File.ReadAllText(QueryPath + "compliance/updateNetworkZone.graphql");

                modifyNetworkZoneCommunication = File.ReadAllText(QueryPath + "compliance/updateNetworkZoneCommunication.graphql");

                getPolicies = File.ReadAllText(QueryPath + "compliance/getPolicies.graphql");
                
                addViolations = File.ReadAllText(QueryPath + "compliance/addViolations.graphql");
                getViolations = File.ReadAllText(QueryPath + "compliance/getViolations.graphql");
            }
            catch (Exception exception)
            {
                Log.WriteError("Initialize Compliance Queries", "Api compliance queries could not be loaded.", exception);
                Environment.Exit(-1);
            }
        }
    }
}
