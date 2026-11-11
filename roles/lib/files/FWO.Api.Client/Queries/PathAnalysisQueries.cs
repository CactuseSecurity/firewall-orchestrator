using FWO.Logging;

namespace FWO.Api.Client.Queries
{
    public class PathAnalysisQueries : Queries
    {
        public static readonly string pathAnalysis;
        public static readonly string getPathAnalysisAlgorithms;

        static PathAnalysisQueries()
        {
            try
            {
                pathAnalysis =
                    GetQueryText("path_analysis/analyzePath.graphql");
                getPathAnalysisAlgorithms =
                    GetQueryText("path_analysis/getIdAndNameOfPathAnalysisAlgorithm.graphql");

            }
            catch (Exception exception)
            {
                Log.WriteError("Initialize Api Queries", "Api Object Queries could not be loaded.", exception);
#if RELEASE
                Environment.Exit(-1);
#else
                throw;
#endif
            }
        }
    }
}
