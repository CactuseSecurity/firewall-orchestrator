using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;

namespace FWO.Logging
{
    public static class Log
    {
        private static object logLock = new object();

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
            lock (logLock)
            {
                if (ForegroundColor != null)
                    Console.ForegroundColor = (ConsoleColor)ForegroundColor;
                if (BackgroundColor != null)
                    Console.BackgroundColor = (ConsoleColor)BackgroundColor;
                Console.Out.WriteLine(Text); // TODO: async method ?
                Console.ResetColor();
            }
        }
    }
}
