using FWO.Logging;

namespace FWO.Api.Client.Queries
{
    public class StmQueries : Queries
    {
        public static readonly string getIpProtocols;
        public static readonly string getRuleActions;
        public static readonly string getTracking;


        static StmQueries()
        {
            try
            {
                getIpProtocols = GetQueryText("stmTables/getIpProtocols.graphql");
                getRuleActions = GetQueryText("stmTables/getRuleActions.graphql");
                getTracking = GetQueryText("stmTables/getTracking.graphql");
            }
            catch (Exception exception)
            {
                Log.WriteError("Initialize StmQueries", "Api StmQueries could not be loaded.", exception);
#if RELEASE
                Environment.Exit(-1);
#else
                throw;
#endif
            }
        }
    }
}
