using FWO.Logging;

namespace FWO.Api.Client.Queries
{
    public class PathAnalysisAlgorithmQueries : Queries
    {
        public static readonly string getPathAnalysisAlgorithms;

        static PathAnalysisAlgorithmQueries()
        {
            try
            {
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
