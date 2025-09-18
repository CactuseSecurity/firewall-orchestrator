﻿using FWO.Logging;

namespace FWO.Api.Client.Queries
{
    public class ImportQueries : Queries
    {

        public static readonly string deleteImport;
        public static readonly string rollbackImport;
        public static readonly string deleteLatestConfigOfManagement;
        public static readonly string getLastImport;

        static ImportQueries()
        {
            try
            {
                deleteImport = GetQueryText("import/deleteImport.graphql");
                rollbackImport = GetQueryText("import/rollbackImport.graphql");
                deleteLatestConfigOfManagement = GetQueryText("import/deleteLatestConfigOfManagement.graphql");
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
