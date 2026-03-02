using FWO.Logging;

namespace FWO.Api.Client.Queries
{
    public class ImportQueries : Queries
    {

        public static readonly string deleteImport;
        public static readonly string rollbackImport;
        public static readonly string deleteLatestConfigOfManagement;
        public static readonly string getLastImport;
        public static readonly string getMaxImportId;
        public static readonly string addImportForRuleOwner;
        public static readonly string updateImportControlForRuleOwnerFull;
        public static readonly string updateImportControlForRuleOwnerInc;
        public static readonly string getLastImportControl;
        public static readonly string getPendingRuleOwnerImports;
        public static readonly string addImportForOwner;

        static ImportQueries()
        {
            try
            {
                deleteImport = GetQueryText("import/deleteImport.graphql");
                rollbackImport = GetQueryText("import/rollbackImport.graphql");
                deleteLatestConfigOfManagement = GetQueryText("import/deleteLatestConfigOfManagement.graphql");
                getLastImport = GetQueryText("import/getLastImport.graphql");
                getMaxImportId = GetQueryText("import/getMaxImportId.graphql");
                addImportForRuleOwner = GetQueryText("import/addImportForRuleOwner.graphql");
                updateImportControlForRuleOwnerFull = GetQueryText("import/updateImportControlForRuleOwnerFull.graphql");
                getLastImportControl = GetQueryText("import/getLastImportControl.graphql");
                getPendingRuleOwnerImports = GetQueryText("import/getPendingRuleOwnerImports.graphql");
                updateImportControlForRuleOwnerInc = GetQueryText("import/updateImportControlForRuleOwnerInc.graphql");
                addImportForOwner = GetQueryText("import/addImportForOwner.graphql");
            }
            catch (Exception exception)
            {
                Log.WriteError("Initialize DeviceQueries", "Api DeviceQueries could not be loaded.", exception);
#if RELEASE
                Environment.Exit(-1);
#else
                throw;
#endif
            }
        }
    }
}
