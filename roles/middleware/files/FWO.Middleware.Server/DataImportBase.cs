using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data;
using FWO.Config.Api;
using FWO.Logging;
using System.Diagnostics;
using System.Text;

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
        protected bool RunImportScript(string importScriptFile, string? scriptArguments = null)
        {
            try
            {
                if (File.Exists(importScriptFile))
                {
                    ProcessStartInfo start = new()
                    {
                        FileName = importScriptFile,
                        UseShellExecute = false,
                        RedirectStandardOutput = true
                    };
                    AddScriptArguments(start, scriptArguments);
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
        /// Parse a configured command line into discrete process arguments.
        /// </summary>
        protected static void AddScriptArguments(ProcessStartInfo start, string? scriptArguments)
        {
            foreach (string argument in ParseCommandLineArguments(scriptArguments))
            {
                start.ArgumentList.Add(argument);
            }
        }

        /// <summary>
        /// Split a command line string while preserving quoted values.
        /// </summary>
        protected static List<string> ParseCommandLineArguments(string? commandLine)
        {
            List<string> arguments = [];
            if (string.IsNullOrWhiteSpace(commandLine))
            {
                return arguments;
            }

            StringBuilder currentArgument = new();
            bool inQuotes = false;
            char quoteCharacter = '\0';
            bool isEscaped = false;

            foreach (char currentCharacter in commandLine)
            {
                if (isEscaped)
                {
                    currentArgument.Append(currentCharacter);
                    isEscaped = false;
                    continue;
                }

                if (currentCharacter == '\\')
                {
                    isEscaped = true;
                    continue;
                }

                if (inQuotes)
                {
                    if (currentCharacter == quoteCharacter)
                    {
                        inQuotes = false;
                    }
                    else
                    {
                        currentArgument.Append(currentCharacter);
                    }
                    continue;
                }

                if (currentCharacter == '"' || currentCharacter == '\'')
                {
                    inQuotes = true;
                    quoteCharacter = currentCharacter;
                    continue;
                }

                if (char.IsWhiteSpace(currentCharacter))
                {
                    AppendCompletedArgument(arguments, currentArgument);
                    continue;
                }

                currentArgument.Append(currentCharacter);
            }

            if (isEscaped)
            {
                currentArgument.Append('\\');
            }

            AppendCompletedArgument(arguments, currentArgument);
            return arguments;
        }

        private static void AppendCompletedArgument(List<string> arguments, StringBuilder currentArgument)
        {
            if (currentArgument.Length > 0)
            {
                arguments.Add(currentArgument.ToString());
                currentArgument.Clear();
            }
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
