using FWO.Logging;

namespace FWO.Api.Client.Queries
{
    /// <summary>
    /// GraphQL operations for imported logging data.
    /// </summary>
    public class LogDataQueries : Queries
    {
        public static readonly string insertLogEntries;
        public static readonly string replaceLogEntries;
        public static readonly string deleteLogEntriesOfOwners;
        public static readonly string deleteExpiredLogEntries;
        public static readonly string getUnmodelledLogEntriesByOwner;
        public static readonly string getIpMetadataSources;
        public static readonly string deleteOrphanedIpMetadata;

        static LogDataQueries()
        {
            try
            {
                insertLogEntries = GetQueryText("logging/insertLogEntries.graphql");
                replaceLogEntries = GetQueryText("logging/replaceLogEntries.graphql");
                deleteLogEntriesOfOwners = GetQueryText("logging/deleteLogEntriesOfOwners.graphql");
                deleteExpiredLogEntries = GetQueryText("logging/deleteExpiredLogEntries.graphql");
                getUnmodelledLogEntriesByOwner = GetQueryText("logging/getUnmodelledLogEntriesByOwner.graphql");
                getIpMetadataSources = GetQueryText("logging/getIpMetadataSources.graphql");
                deleteOrphanedIpMetadata = GetQueryText("logging/deleteOrphanedIpMetadata.graphql");
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
