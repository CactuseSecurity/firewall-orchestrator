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
                deleteImport = GetQueryText("import/deleteImport.graphql");
                rollbackLastImport = GetQueryText("import/rollback.graphql");
                deleteLatestConfig = GetQueryText("import/deleteLatestConfig.graphql");
                getLastImport = GetQueryText("import/getLastImport.graphql");

            }
            catch (Exception exception)
            {
                Log.WriteError("Initialize DeviceQueries", "Api DeviceQueries could not be loaded.", exception);
                Environment.Exit(-1);
            }
        }
    }
}
