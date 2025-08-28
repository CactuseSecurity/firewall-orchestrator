using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data;
using FWO.Config.Api;
using FWO.Logging;
using System.Diagnostics; 

namespace FWO.Middleware.Server
{
    /// <summary>
    /// Class handling the Data Import
    /// </summary>
    public class DataImportBase
    {
        /// <summary>
        /// Api Connection
        /// </summary>
        protected readonly ApiConnection apiConnection;

        /// <summary>
        /// Global Config
        /// </summary>
        protected GlobalConfig globalConfig;

        /// <summary>
        /// Import File
        /// </summary>
        protected string importFile { get; set; } = "";


        /// <summary>
        /// Constructor for Data Import
        /// </summary>
        public DataImportBase(ApiConnection apiConnection, GlobalConfig globalConfig)
        {
            this.apiConnection = apiConnection;
            this.globalConfig = globalConfig;
        }

        /// <summary>
        /// Read the Import Data File
        /// </summary>
        protected void ReadFile(string filepath)
        {
            try
            {
                importFile = File.ReadAllText(filepath).Trim();
            }
            catch (Exception)
            {
                Log.WriteError("Read file", $"File could not be read from {filepath}.");
                throw;
            }
        }

        /// <summary>
        /// Execute the Data Import Script
        /// </summary>
        protected bool RunImportScript(string importScriptFile)
        {
            try
            {
                if (File.Exists(importScriptFile))
                {
                    ProcessStartInfo start = new()
                    {
                        FileName = importScriptFile,
                        Arguments = "", // args,
                        UseShellExecute = false,
                        RedirectStandardOutput = true
                    };
                    Process? process = Process.Start(start);
                    StreamReader? reader = process?.StandardOutput;
                    string? result = reader?.ReadToEnd();
                    process?.WaitForExit();
                    process?.Close();
                    Log.WriteInfo("Run Import Script", $"Executed Import Script {importScriptFile}. Result: {result ?? ""}");
                    return true;
                }
            }
            catch (Exception Exception)
            {
                Log.WriteError("Run Import Script", $"File {importScriptFile} could not be executed.", Exception);
            }
            return false;
        }
        
        /// <summary>
        /// Add a log entry
        /// </summary>
        /// <param name="source"></param>
        /// <param name="severity"></param>
        /// <param name="level"></param>
        /// <param name="description"></param>
        /// <returns></returns>
        public async Task AddLogEntry(string source, int severity, string level, string description)
        {
            try
            {
                var Variables = new
                {
                    user = 0,
                    source = source,
                    severity = severity,
                    suspectedCause = level,
                    description = description
                };
                ReturnId[]? returnIds = (await apiConnection.SendQueryAsync<ReturnIdWrapper>(MonitorQueries.addDataImportLogEntry, Variables)).ReturnIds;
                if (returnIds == null)
                {
                    Log.WriteError("Write Log", "Log could not be written to database");
                }
            }
            catch (Exception exc)
            {
                Log.WriteError("Write Log", $"Could not write log: ", exc);
            }
        }
    }
}
