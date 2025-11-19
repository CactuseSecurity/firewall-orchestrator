using FWO.Logging;

namespace FWO.Api.Client.Queries
{
    public class NetworkAnalysisQueries : Queries
    {
        public static readonly string pathAnalysis;

        static NetworkAnalysisQueries() 
        {
            try
            {
                pathAnalysis =
                    GetQueryText("networking/analyzePath.graphql");

            }
            catch (Exception exception)
            {
                Log.WriteError("Initialize Api Queries", "Api Object Queries could not be loaded." , exception);
#if RELEASE
                Environment.Exit(-1);
#else
                throw;
#endif
            }
        }
    }
}
