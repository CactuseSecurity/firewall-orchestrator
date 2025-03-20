using FWO.Logging;

namespace FWO.Api.Client.Queries
{
    public class ImportQueries : Queries
    {
 
        public static readonly string deleteImport;
        public static readonly string rollbackLastImport;
        public static readonly string deleteLatestConfig;
        public static readonly string getLastImport;
 
        static ImportQueries()
        {
            try
            {
                deleteImport = File.ReadAllText(QueryPath + "import/deleteImport.graphql");
                rollbackLastImport = File.ReadAllText(QueryPath + "import/rollback.graphql");
                deleteLatestConfig = File.ReadAllText(QueryPath + "import/deleteLatestConfig.graphql");
                getLastImport = File.ReadAllText(QueryPath + "import/getLastImport.graphql");

            }
            catch (Exception exception)
            {
                Log.WriteError("Initialize DeviceQueries", "Api DeviceQueries could not be loaded.", exception);
                Environment.Exit(-1);
            }
        }
    }
}
