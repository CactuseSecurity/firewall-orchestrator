using FWO.Logging;

namespace FWO.Api.Client.Queries
{
    /// <summary>
    /// GraphQL operations for imported logging data.
    /// </summary>
    public class LogDataQueries : Queries
    {
        public static readonly string insertLogEntries;
        public static readonly string deleteExpiredLogEntries;
        public static readonly string getUnmodelledLogEntriesByOwner;

        static LogDataQueries()
        {
            try
            {
                insertLogEntries = GetQueryText("logging/insertLogEntries.graphql");
                deleteExpiredLogEntries = GetQueryText("logging/deleteExpiredLogEntries.graphql");
                getUnmodelledLogEntriesByOwner = GetQueryText("logging/getUnmodelledLogEntriesByOwner.graphql");
            }
            catch (Exception exception)
            {
                Log.WriteError("Initialize LogDataQueries", "Api log data queries could not be loaded.", exception);
#if RELEASE
                Environment.Exit(-1);
#else
                throw;
#endif
            }
        }
    }
}
