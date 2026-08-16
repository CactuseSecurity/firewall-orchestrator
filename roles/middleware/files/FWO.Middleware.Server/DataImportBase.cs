using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Data;
using FWO.Config.Api;
using FWO.Config.File;
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
        private static readonly TimeSpan kDefaultImportScriptTimeout = TimeSpan.FromMinutes(60);
        private static readonly TimeSpan kStoppedScriptOutputTimeout = TimeSpan.FromSeconds(10);

        // Severity markers of the log format the customizing scripts use (see get_logger in
        // basic_helpers.py). Python truncates the level name to five characters.
        private static readonly List<string> kScriptErrorMarkers = ["[ERROR]", "[CRITI]"];
        private static readonly List<string> kScriptWarningMarkers = ["[WARNI]"];

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
        /// Time an import script may run before it is stopped, configurable as importScriptTimeout
        /// (in minutes). A script which waits for input it can never get - a git credential prompt
        /// for instance - would otherwise keep the calling scheduler job blocked until the
        /// middleware is restarted. An installation with a legitimately long running script raises
        /// the setting; a value below one minute falls back to the default, so a misconfiguration
        /// cannot stop every script right after it was started.
        /// </summary>
        protected virtual TimeSpan ImportScriptTimeout => globalConfig.ImportScriptTimeout >= 1
            ? TimeSpan.FromMinutes(globalConfig.ImportScriptTimeout)
            : kDefaultImportScriptTimeout;


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
        protected void ReadFile(string filepath, bool validateImportFile = true)
        {
            try
            {
                if (validateImportFile)
                {
                    ImportPathPolicy.ValidateExistingImportFile(filepath, ConfigFile.AllowedCustomizationRoots);
                    LogFileHash("Read Import File", filepath);
                }
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
        protected bool RunImportScript(string importScriptFile, string? scriptArguments = null, bool validateImportFile = true)
        {
            try
            {
                if (File.Exists(importScriptFile))
                {
                    if (validateImportFile)
                    {
                        ImportPathPolicy.ValidateExistingImportFile(importScriptFile, ConfigFile.AllowedCustomizationRoots);
                    }
                    LogFileHash("Run Import Script", importScriptFile);
                    ProcessStartInfo start = new()
                    {
                        FileName = importScriptFile,
                        UseShellExecute = false,
                        RedirectStandardInput = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };
                    AddScriptArguments(start, scriptArguments);
                    Process? process = Process.Start(start);
                    if (process is null)
                    {
                        Log.WriteError("Run Import Script", $"Import Script {importScriptFile} could not be started.");
                        return false;
                    }

                    // a script must not be able to wait for input: with the standard input of the
                    // middleware a tool asking for credentials would block the import instead of failing
                    process.StandardInput.Close();
                    // both streams have to be read before waiting, otherwise a script writing more
                    // than the pipe buffer holds blocks forever. Scripts log to stderr, so the
                    // error output is the interesting part when a script fails.
                    Task<string> outputReader = process.StandardOutput.ReadToEndAsync();
                    Task<string> errorReader = process.StandardError.ReadToEndAsync();
                    if (!process.WaitForExit((int)ImportScriptTimeout.TotalMilliseconds))
                    {
                        return StopTimedOutScript(process, importScriptFile, outputReader, errorReader);
                    }

                    string output = outputReader.GetAwaiter().GetResult();
                    string errorOutput = errorReader.GetAwaiter().GetResult();
                    int exitCode = process.ExitCode;
                    process.Close();

                    Log.WriteInfo("Run Import Script", $"Executed Import Script {importScriptFile}. Exit code: {exitCode}. Result: {output}");
                    LogScriptOutput(importScriptFile, errorOutput, exitCode);
                    return exitCode == 0;
                }
            }
            catch (Exception Exception)
            {
                Log.WriteError("Run Import Script", $"File {importScriptFile} could not be executed.", Exception);
            }
            return false;
        }

        /// <summary>
        /// Stop a script which did not finish in time and report it as a failed run.
        /// The whole process tree is stopped, otherwise a git command left behind by the script
        /// would keep waiting for an answer nobody can give it. What the script reported before it
        /// got stuck is logged as well: that output usually names the reason it never finished.
        /// </summary>
        private bool StopTimedOutScript(Process process, string importScriptFile, Task<string> outputReader, Task<string> errorReader)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (Exception exception)
            {
                Log.WriteError("Run Import Script", $"Import Script {importScriptFile} could not be stopped after its timeout.", exception);
            }

            // the readers finish as soon as the stopped process closes its pipes; the wait is
            // bounded anyway, a pipe kept open by something the kill did not reach must not
            // block the calling import a second time
            string output = ReadRemainingOutput(outputReader, importScriptFile);
            string errorOutput = ReadRemainingOutput(errorReader, importScriptFile);
            process.Close();

            Log.WriteError("Run Import Script", $"Import Script {importScriptFile} did not finish within" +
                $" {ImportScriptTimeout.TotalMinutes} minutes and was stopped. Result: {output}. Reported: {errorOutput}");
            return false;
        }

        /// <summary>
        /// Collect what a stopped script had written so far.
        /// </summary>
        private static string ReadRemainingOutput(Task<string> reader, string importScriptFile)
        {
            try
            {
                return reader.Wait(kStoppedScriptOutputTimeout) ? reader.GetAwaiter().GetResult() : "";
            }
            catch (Exception exception)
            {
                Log.WriteWarning("Run Import Script", $"Output of the stopped Import Script {importScriptFile}" +
                    $" could not be read: {exception.Message}");
                return "";
            }
        }

        /// <summary>
        /// Report what a script wrote to its error output. The customizing scripts log everything
        /// there, so a run ending with exit code 0 can still report that it could not do its work -
        /// a failed git login while pushing for instance. Such a run must not stay silent, so the
        /// severity the script itself reported decides how the output is logged.
        /// </summary>
        private static void LogScriptOutput(string importScriptFile, string errorOutput, int exitCode)
        {
            if (exitCode != 0)
            {
                Log.WriteError("Run Import Script", $"Import Script {importScriptFile} failed with exit code {exitCode}: {errorOutput}");
                return;
            }

            if (string.IsNullOrWhiteSpace(errorOutput))
            {
                return;
            }

            string message = $"Import Script {importScriptFile} reported: {errorOutput}";
            switch (GetScriptOutputLogType(errorOutput))
            {
                case LogType.Error:
                    Log.WriteError("Run Import Script", message);
                    break;
                case LogType.Warning:
                    Log.WriteWarning("Run Import Script", message);
                    break;
                default:
                    Log.WriteInfo("Run Import Script", message);
                    break;
            }
        }

        /// <summary>
        /// Determine how severe a script reported its own output to be.
        /// </summary>
        protected static LogType GetScriptOutputLogType(string errorOutput)
        {
            if (kScriptErrorMarkers.Exists(marker => errorOutput.Contains(marker, StringComparison.Ordinal)))
            {
                return LogType.Error;
            }
            return kScriptWarningMarkers.Exists(marker => errorOutput.Contains(marker, StringComparison.Ordinal))
                ? LogType.Warning
                : LogType.Info;
        }

        /// <summary>
        /// Validate a configured extensionless import source.
        /// </summary>
        protected static List<string> ValidateConfiguredImportSource(string importfilePathAndName)
        {
            string normalizedPath = ImportPathPolicy.RemoveAllowedExtension(importfilePathAndName);
            return ImportPathPolicy.GetValidatedExistingImportFiles(normalizedPath, ConfigFile.AllowedCustomizationRoots);
        }

        /// <summary>
        /// Calculates and writes a stable SHA-256 hash for executed/read import files.
        /// </summary>
        protected static void LogFileHash(string title, string filePath)
        {
            string sha256 = ImportPathPolicy.CalculateSha256(filePath);
            Log.WriteInfo(title, $"Import file '{filePath}' sha256={sha256} at {DateTimeOffset.Now:O}");
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
                if (TryAppendEscapedCharacter(currentCharacter, currentArgument, ref isEscaped))
                {
                    continue;
                }

                if (TryStartEscapeSequence(currentCharacter, ref isEscaped))
                {
                    continue;
                }

                if (TryHandleQuotedCharacter(currentCharacter, currentArgument, ref inQuotes, quoteCharacter))
                {
                    continue;
                }

                if (TryStartQuotedArgument(currentCharacter, ref inQuotes, ref quoteCharacter))
                {
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

        private static bool TryAppendEscapedCharacter(
            char currentCharacter,
            StringBuilder currentArgument,
            ref bool isEscaped)
        {
            if (!isEscaped)
            {
                return false;
            }

            currentArgument.Append(currentCharacter);
            isEscaped = false;
            return true;
        }

        private static bool TryStartEscapeSequence(char currentCharacter, ref bool isEscaped)
        {
            if (currentCharacter != '\\')
            {
                return false;
            }

            isEscaped = true;
            return true;
        }

        private static bool TryHandleQuotedCharacter(
            char currentCharacter,
            StringBuilder currentArgument,
            ref bool inQuotes,
            char quoteCharacter)
        {
            if (!inQuotes)
            {
                return false;
            }

            if (currentCharacter == quoteCharacter)
            {
                inQuotes = false;
            }
            else
            {
                currentArgument.Append(currentCharacter);
            }

            return true;
        }

        private static bool TryStartQuotedArgument(char currentCharacter, ref bool inQuotes, ref char quoteCharacter)
        {
            if (currentCharacter != '"' && currentCharacter != '\'')
            {
                return false;
            }

            inQuotes = true;
            quoteCharacter = currentCharacter;
            return true;
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
