using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using FWO.Basics;
using FWO.Basics.Interfaces;

namespace FWO.Logging
{
    public static class Log
    {
        private static SemaphoreSlim semaphore = new(1, 1);
        private static readonly string lockFilePath = $"/var/fworch/lock/{Assembly.GetEntryAssembly()?.GetName().Name}_log.lock";
        private static readonly Random random = new();

        private readonly record struct LogLocation(string CallerName, string CallerFile, int CallerLineNumber);
        /// <summary>
        /// Encapsulates error log details for structured logging.
        /// </summary>
        public readonly record struct ErrorLogDetails(
            string? Text = null,
            Exception? Error = null,
            string? User = null,
            string? Role = null,
            bool ContainsLdapDn = false
        );
        /// <summary>
        /// Encapsulates audit log details for structured logging.
        /// </summary>
        public readonly record struct AuditLogDetails(
            string Text,
            string? UserName = null,
            string? UserDn = null,
            bool WithSeparatorLine = true,
            bool ContainsLdapDn = true
        );
        private readonly record struct LogEntry(
            string LogType,
            string Title,
            string Text,
            LogLocation Location,
            ConsoleColor? ForegroundColor = null,
            ConsoleColor? BackgroundColor = null,
            bool ContainsLdapDn = false
        );

        static Log()
        {
            Task.Factory.StartNew(async () =>
            {
                // log switch - log file locking
                bool logOwnedByExternal = false;
                Stopwatch stopwatch = new();

                while (true)
                {
                    try
                    {
                        // Open file
                        using FileStream file = await GetFile(lockFilePath);
                        // Read file content
                        using StreamReader reader = new(file);
                        string lockFileContent = (await reader.ReadToEndAsync()).Trim();

                        // Forcefully release lock after timeout
                        if (logOwnedByExternal && stopwatch.ElapsedMilliseconds > 10_000)
                        {
                            using StreamWriter writer = new(file);
                            await writer.WriteLineAsync("FORCEFULLY RELEASED");
                            stopwatch.Reset();
                            semaphore.Release();
                            logOwnedByExternal = false;
                        }
                        // GRANTED - lock was granted by us
                        else if (lockFileContent.EndsWith("GRANTED"))
                        {
                            // Request lock if it is not already requested by us
                            // (in case of restart with log already granted)
                            if (!logOwnedByExternal)
                            {
                                semaphore.Wait();
                                stopwatch.Restart();
                                logOwnedByExternal = true;
                            }
                        }
                        // REQUESTED - lock was requested by log swap process
                        else if (lockFileContent.EndsWith("REQUESTED"))
                        {
                            // only request lock if it is not already requested by us
                            if (!logOwnedByExternal)
                            {
                                semaphore.Wait();
                                stopwatch.Restart();
                                logOwnedByExternal = true;
                            }
                            using StreamWriter writer = new(file);
                            await writer.WriteLineAsync("GRANTED");
                        }
                        // RELEASED - lock was released by log swap process
                        // only release lock if it was formerly requested by us
                        else if (lockFileContent.EndsWith("RELEASED") && logOwnedByExternal)
                        {
                            stopwatch.Reset();
                            semaphore.Release();
                            logOwnedByExternal = false;
                        }
                    }
                    catch (Exception)
                    {
                        // ignore exceptions
                    }
                    await Task.Delay(1000);
                }
            }, TaskCreationOptions.LongRunning);
        }

        private static async Task<FileStream> GetFile(string path)
        {
            while (true)
            {
                try
                {
                    return File.Open(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                }
                catch (Exception)
                {
                    //WriteDebug("Log file locking", $"Could not access log lock file: {e.Message}.");
                }
                await Task.Delay(random.Next(100));
            }
        }

        [Conditional("DEBUG")]
        public static void WriteDebug(string Title, string Text, bool containsLdapDn = false, [CallerMemberName] string callerName = "", [CallerFilePath] string callerFile = "", [CallerLineNumber] int callerLineNumber = 0)
        {
            WriteLog(new LogEntry("Debug", Title, Text, CreateLocation(callerName, callerFile, callerLineNumber), ConsoleColor.White, null, containsLdapDn));
        }

        public static void WriteInfo(string Title, string Text, bool containsLdapDn = false, [CallerMemberName] string callerName = "", [CallerFilePath] string callerFile = "", [CallerLineNumber] int callerLineNumber = 0)
        {
            WriteLog(new LogEntry("Info", Title, Text, CreateLocation(callerName, callerFile, callerLineNumber), ConsoleColor.Cyan, null, containsLdapDn));
        }

        public static void WriteWarning(string Title, string Text, bool containsLdapDn = false, [CallerMemberName] string callerName = "", [CallerFilePath] string callerFile = "", [CallerLineNumber] int callerLineNumber = 0)
        {
            WriteLog(new LogEntry("Warning", Title, Text, CreateLocation(callerName, callerFile, callerLineNumber), ConsoleColor.DarkYellow, null, containsLdapDn));
        }

        /// <summary>
        /// Writes an error log entry with optional user context and exception details.
        /// </summary>
        /// <param name="Title">The title of the error log entry.</param>
        /// <param name="Text">The content of the error log entry.</param>
        /// <param name="Error">The exception to include in the log entry.</param>
        /// <param name="User">The user associated with the error.</param>
        /// <param name="Role">The user role associated with the error.</param>
        /// <param name="containsLdapDn">The error log entry contains ldap DN data so, do not strip ldap dn delimters (,/=).</param>
        /// <param name="callerName">The name of the calling method (automatically supplied).</param>
        /// <param name="callerFile">The file path of the calling method (automatically supplied).</param>
        /// <param name="callerLineNumber">The line number in the source file at which the method is called (automatically supplied).</param>
        public static void WriteError(string Title, string? Text = null, Exception? Error = null, string? User = null, string? Role = null, bool containsLdapDn = false, [CallerMemberName] string callerName = "", [CallerFilePath] string callerFile = "", [CallerLineNumber] int callerLineNumber = 0)
        {
            WriteError(Title, new ErrorLogDetails(Text, Error, User, Role, containsLdapDn), callerName, callerFile, callerLineNumber);
        }

        public static void WriteError(string Title, ErrorLogDetails details, [CallerMemberName] string callerName = "", [CallerFilePath] string callerFile = "", [CallerLineNumber] int callerLineNumber = 0)
        {
            string DisplayText =
                (details.User != null ? $"User: {details.User}, " : "") +
                (details.Role != null ? $"Role: {details.Role}, " : "") +
                (details.Text != null ? $"{details.Text}" : "") +
                (details.Error != null ?
                "\n ---\n" +
                $"Exception thrown: \n {details.Error?.GetType().Name} \n" +
                $"Message: \n {details.Error?.Message.TrimStart()} \n" +
                $"Stack Trace: \n {details.Error?.StackTrace?.TrimStart()}"
                : "");

            WriteLog(new LogEntry("Error", Title, DisplayText, CreateLocation(callerName, callerFile, callerLineNumber), ConsoleColor.Red, null, details.ContainsLdapDn));
        }

        public static void WriteError(string Title, string Text, bool LogStackTrace, bool containsLdapDn = false, [CallerMemberName] string callerName = "", [CallerFilePath] string callerFile = "", [CallerLineNumber] int callerLineNumber = 0)
        {
            string DisplayText =
                (Text != null ?
                $"{Text}"
                : "") +
                (LogStackTrace ?
                "\n ---\n" +
                $"Stack Trace: \n {Environment.StackTrace}"
                : "");

            WriteLog(new LogEntry("Error", Title, DisplayText, CreateLocation(callerName, callerFile, callerLineNumber), ConsoleColor.Red, null, containsLdapDn));
        }

        /// <summary>
        /// Writes an audit log entry with the specified title and text.
        /// Optionally appends a separator line to the log entry.
        /// </summary>
        /// <param name="Title">The title of the audit log entry.</param>
        /// <param name="Text">The content of the audit log entry.</param>
        /// <param name="UserName">The name of the user performing the action.</param>
        /// <param name="UserDN">The distinguished name (DN) of the user.</param>
        /// <param name="containsLdapDn">The audit log entry contains ldap DN data so, do not strip ldap dn delimters (,/=).</param>
        /// <param name="WithSeparatorLine">Whether to append a separator line to the log entry. Default is true.</param>
        /// <param name="callerName">The name of the calling method (automatically supplied).</param>
        /// <param name="callerFile">The file path of the calling method (automatically supplied).</param>
        /// <param name="callerLineNumber">The line number in the source file at which the method is called (automatically supplied).</param>
        public static void WriteAudit(string Title, string Text, string? UserName = null, string? UserDN = null, bool WithSeparatorLine = true, bool containsLdapDn = true, [CallerMemberName] string callerName = "", [CallerFilePath] string callerFile = "", [CallerLineNumber] int callerLineNumber = 0)
        {
            WriteAudit(Title, new AuditLogDetails(Text, UserName, UserDN, WithSeparatorLine, containsLdapDn), callerName, callerFile, callerLineNumber);
        }

        /// <summary>
        /// Writes an audit log entry using pre-composed audit details.
        /// </summary>
        /// <param name="Title">The title of the audit log entry.</param>
        /// <param name="details">The audit log details payload.</param>
        /// <param name="callerName">The name of the calling method (automatically supplied).</param>
        /// <param name="callerFile">The file path of the calling method (automatically supplied).</param>
        /// <param name="callerLineNumber">The line number in the source file at which the method is called (automatically supplied).</param>
        public static void WriteAudit(string Title, AuditLogDetails details, [CallerMemberName] string callerName = "", [CallerFilePath] string callerFile = "", [CallerLineNumber] int callerLineNumber = 0)
        {
            string text = details.Text;
            if (!string.IsNullOrEmpty(details.UserName))
            {
                text += $" by User: {details.UserName}";
            }

            if (!string.IsNullOrEmpty(details.UserDn))
            {
                text += $" (DN: {details.UserDn})";
            }

            if (details.WithSeparatorLine)
            {
                text += $"{Environment.NewLine}----{Environment.NewLine}";
            }

            WriteLog(new LogEntry("Audit", Title, text, CreateLocation(callerName, callerFile, callerLineNumber), ConsoleColor.Yellow, null, details.ContainsLdapDn));
        }

        private static LogLocation CreateLocation(string callerName, string callerFile, int callerLineNumber)
        {
            return new LogLocation(callerName, callerFile, callerLineNumber);
        }

        private static void WriteLog(LogEntry entry)
        {
            string File = entry.Location.CallerFile.Split('\\', '/').Last(); // do not show the full file path, just the basename
            WriteInColor($"{DateTime.Now.ToString("yyyy-MM-ddTHH:mm:sszzz")} {entry.LogType} - {entry.Title} ({File} in line {entry.Location.CallerLineNumber}), {entry.Text}", entry.ForegroundColor, entry.BackgroundColor, entry.ContainsLdapDn);
        }

        public static void WriteAlert(string Title, string Text)
        {
            // fixed format to be further processed (e.g. splunk)
            WriteInColor($"FWORCHAlert - {Title}, {Text}");
        }

        private static void WriteInColor(string Text, ConsoleColor? ForegroundColor = null, ConsoleColor? BackgroundColor = null, bool containsLdapDn = false)
        {
            semaphore.Wait();
            if (ForegroundColor != null)
                Console.ForegroundColor = (ConsoleColor)ForegroundColor;
            if (BackgroundColor != null)
                Console.BackgroundColor = (ConsoleColor)BackgroundColor;
            Console.Out.WriteLine(Text.SanitizeMand(containsLdapDn)); // TODO: async method ?
            Console.ResetColor();
            semaphore.Release();
        }

        public static void TryWriteLog(LogType logType, string title, string text, bool condition)
        {
            if (condition)
            {
                switch (logType)
                {
                    case LogType.Debug:
                        WriteDebug(title, text);
                        break;
                    case LogType.Info:
                        WriteInfo(title, text);
                        break;
                    case LogType.Warning:
                        WriteWarning(title, text);
                        break;
                    case LogType.Error:
                        WriteError(title, text);
                        break;
                    case LogType.Audit:
                        WriteAudit(title, text);
                        break;
                }
            }
        }
    }
}
