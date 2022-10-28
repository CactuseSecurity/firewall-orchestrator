using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace FWO.Logging
{
    public static class Log
    {
        private static SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);
        private static string lockFilePath = $"/var/fworch/lock/{Assembly.GetEntryAssembly()?.GetName().Name}_log.lock";
        private static Random random = new Random();

        static Log() 
        {
            Task.Factory.StartNew(async () =>
            {
                // log switch - log file locking
                DateTime lastLockFileRead = new DateTime(0);
                bool logOwned = false;

                while (true)
                {
                    try
                    {
                        DateTime lastLockFileChange = File.GetLastWriteTime(lockFilePath);

                        if (lastLockFileRead != lastLockFileChange)
                        {
                            using FileStream file = await GetFile(lockFilePath);
                            // read file content
                            using StreamReader reader = new StreamReader(file);
                            string lockFileContent = (await reader.ReadToEndAsync()).Trim();

                            // REQUESTED - lock was requested by log swap process
                            // GRANTED - lock was granted by us
                            // RELEASED - lock was released by log swap process
                            // ACKNOWLEDGED - lock release was acknowledged by us
                            if (lockFileContent.EndsWith("REQUESTED"))
                            {
                                // only request lock if it is not already requested by us
                                if (!logOwned)
                                {
                                    semaphore.Wait();
                                    logOwned = true;
                                }
                                using StreamWriter writer = new StreamWriter(file);
                                await writer.WriteLineAsync("GRANTED");
                            }
                            if (lockFileContent.EndsWith("RELEASED"))
                            {
                                // only release lock if it was formerly requested by us
                                if (logOwned) 
                                { 
                                    semaphore.Release();
                                    logOwned = false;
                                }
                                using StreamWriter writer = new StreamWriter(file);
                                await writer.WriteLineAsync("ACKNOWLEDGED");
                            }

                            lastLockFileRead = lastLockFileChange;
                        }

                        await Task.Delay(1000);
                    }
                    catch (Exception e)
                    {
                        //WriteError("Log file locking", "Error while accessing log lock file.", e);
                    }
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
                catch (Exception e) 
                { 
                    //WriteDebug("Log file locking", $"Could not access log lock file: {e.Message}.");
                }
                await Task.Delay(random.Next(100));
            }
        }

        [Conditional("DEBUG")]
        public static void WriteDebug(string Title, string Text, [CallerMemberName] string callerName = "", [CallerFilePath] string callerFile = "", [CallerLineNumber] int callerLineNumber = 0)
        {
            WriteLog("Debug", Title, Text, callerName, callerFile, callerLineNumber, ConsoleColor.White);
        }

        public static void WriteInfo(string Title, string Text, [CallerMemberName] string callerName = "", [CallerFilePath] string callerFile = "", [CallerLineNumber] int callerLineNumber = 0)
        {
            WriteLog("Info", Title, Text, callerName, callerFile, callerLineNumber, ConsoleColor.Cyan);
        }

        public static void WriteWarning(string Title, string Text, [CallerMemberName] string callerName = "", [CallerFilePath] string callerFile = "", [CallerLineNumber] int callerLineNumber = 0)
        {
            WriteLog("Warning", Title, Text, callerName, callerFile, callerLineNumber, ConsoleColor.DarkYellow);
        }

        public static void WriteError(string Title, string? Text = null, Exception? Error = null, [CallerMemberName] string callerName = "", [CallerFilePath] string callerFile = "", [CallerLineNumber] int callerLineNumber = 0)
        {
            string DisplayText =
                (Text != null ?
                $"{Text}"
                : "") +
                (Error != null ?
                "\n ---\n" +
                $"Exception thrown: \n {Error?.GetType().Name} \n" +
                $"Message: \n {Error?.Message.TrimStart()} \n" +
                $"Stack Trace: \n {Error?.StackTrace?.TrimStart()}"
                : "");


            WriteLog("Error", Title, DisplayText, callerName, callerFile, callerLineNumber, ConsoleColor.Red);
        }

        public static void WriteError(string Title, string Text, bool LogStackTrace, [CallerMemberName] string callerName = "", [CallerFilePath] string callerFile = "", [CallerLineNumber] int callerLineNumber = 0)
        {
            string DisplayText =
                (Text != null ?
                $"{Text}"
                : "") +
                (LogStackTrace ?
                "\n ---\n" +
                $"Stack Trace: \n {Environment.StackTrace}"
                : "");

            WriteLog("Error", Title, DisplayText, callerName, callerFile, callerLineNumber, ConsoleColor.Red);
        }

        public static void WriteAudit(string Title, string Text, [CallerMemberName] string callerName = "", [CallerFilePath] string callerFile = "", [CallerLineNumber] int callerLineNumber = 0)
        {
            WriteLog("Audit", Title, Text, callerName, callerFile, callerLineNumber, ConsoleColor.Yellow);
        }

        private static void WriteLog(string LogType, string Title, string Text, string Method, string Path, int Line, ConsoleColor? ForegroundColor = null, ConsoleColor? BackgroundColor = null)
        {
            string File = Path.Split('\\', '/').Last(); // do not show the full file path, just the basename
            WriteInColor($"{DateTime.Now.ToString("yyyy-MM-ddTHH:mm:sszzz")} {LogType} - {Title} ({File} in line {Line}), {Text}", ForegroundColor, BackgroundColor);
        }

        public static void WriteAlert(string Title, string Text)
        {
            // fixed format to be further processed (e.g. splunk)
            WriteInColor($"FWORCHAlert - {Title}, {Text}");
        }

        private static void WriteInColor(string Text, ConsoleColor? ForegroundColor = null, ConsoleColor? BackgroundColor = null)
        {
            semaphore.Wait();
            if (ForegroundColor != null)
                Console.ForegroundColor = (ConsoleColor)ForegroundColor;
            if (BackgroundColor != null)
                Console.BackgroundColor = (ConsoleColor)BackgroundColor;
            Console.Out.WriteLine(Text); // TODO: async method ?
            Console.ResetColor();
            semaphore.Release();
        }
    }
}
