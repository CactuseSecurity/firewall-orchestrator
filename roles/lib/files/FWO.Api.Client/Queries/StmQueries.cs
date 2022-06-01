using FWO.Logging;

namespace FWO.Api.Client.Queries
{
    public class StmQueries : Queries
    {
        public static readonly string getIpProtocols;
        public static readonly string getRuleActions;


        static StmQueries()
        {
            try
            {
                getIpProtocols = File.ReadAllText(QueryPath + "stmTables/getIpProtocols.graphql");
                getRuleActions = File.ReadAllText(QueryPath + "stmTables/getRuleActions.graphql");
            }
            catch (Exception exception)
            {
                Log.WriteError("Initialize StmQueries", "Api StmQueries could not be loaded.", exception);
                Environment.Exit(-1);
            }
        }
    }
}
