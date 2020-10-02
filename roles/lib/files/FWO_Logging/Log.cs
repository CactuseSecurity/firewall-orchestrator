using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace FWO_Logging
{
    public class Log
    {
        public static void WriteDebug(string Title, string Text, [CallerMemberName] string callerName = "", [CallerFilePath] string callerFile = "", [CallerLineNumber] int callerLineNumber = 0)
        {
#if DEBUG
            WriteLog("Debug", Title, Text, callerName, callerFile, callerLineNumber, ConsoleColor.White);
#endif
        }

        public static void WriteInfo(string Title, string Text, [CallerMemberName] string callerName = "", [CallerFilePath] string callerFile = "", [CallerLineNumber] int callerLineNumber = 0)
        {
            WriteLog("Info", Title, Text, callerName, callerFile, callerLineNumber, ConsoleColor.Cyan);
        }

        public static void WriteWarning(string Title, string Text, [CallerMemberName] string callerName = "", [CallerFilePath] string callerFile = "", [CallerLineNumber] int callerLineNumber = 0)
        {
            WriteLog("Warning", Title, Text, callerName, callerFile, callerLineNumber, ConsoleColor.Yellow);
        }

        public static void WriteError(string Title, string Text = null, Exception Error = null, [CallerMemberName] string callerName = "", [CallerFilePath] string callerFile = "", [CallerLineNumber] int callerLineNumber = 0)
        {
            string DisplayText =
                (Text != null ?
                $"{Text}"
                : "") +
                (Error != null ?
                "\n ---\n" +
                $"Exception thrown: \n {Error.GetType().Name} \n" +
                $"Message: \n {Error.Message.TrimStart()} \n" +
                $"Stack Trace: \n {Error.StackTrace.TrimStart()}"
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

        private static void WriteLog(string LogType, string Title, string Text, string Method, string Path, int Line, ConsoleColor? ForegroundColor = null, ConsoleColor? BackgroundColor = null)
        {
            // Extract file from path
            string File = Path.Split('\\', '/').Last();
            ConsoleColor StandardBackgroundColor = Console.BackgroundColor;
            ConsoleColor StandardForegroundColor = Console.ForegroundColor;
            WriteInColor($"{LogType} - {Title} ({File} in line {Line}: {Text}", StandardForegroundColor, StandardBackgroundColor, ForegroundColor, BackgroundColor);
            Console.WriteLine("");
        }

        private static void WriteInColor(string Text, ConsoleColor StandardForegroundColor, ConsoleColor StandardBackgroundColor, ConsoleColor? ForegroundColor = null, ConsoleColor? BackgroundColor = null)
        {
            Console.ForegroundColor = ForegroundColor ?? StandardForegroundColor;
            Console.BackgroundColor = BackgroundColor ?? StandardBackgroundColor;
            Console.WriteLine(Text);
            Console.ForegroundColor = StandardForegroundColor;
            Console.BackgroundColor = StandardBackgroundColor;
        }
    }
}
