using System;
using System.Threading.Tasks;

namespace FWO_Logging
{
    public class Log
    {
        public static void WriteInfo(string Title, string Text)
        {
            WriteLog("Info", Title, Text, ConsoleColor.Cyan);
        }

        public static void WriteWarning(string Title, string Text)
        {
            WriteLog("Warning", Title, Text, ConsoleColor.Yellow);
        }

        public static void WriteError(string Title, string Text = null, Exception Error = null)
        {
            string DisplayText =
                (Text != null ?
                $"{Text}"
                : "") +
                (Error != null ?
                "\n ---\n" +
                $"Exception thrown: \n { Error.GetType().Name} \n" +
                $"Message: \n {Error.Message.TrimStart()} \n" +
                $"Stack Trace: \n {Error.StackTrace.TrimStart()}"
                : "");


            WriteLog("Error", Title, DisplayText, ConsoleColor.Red);

        }

        private static void WriteLog(string LogType, string Title, string Text, ConsoleColor? ForegroundColor = null, ConsoleColor? BackgroundColor = null)
        {
            ConsoleColor StandardBackgroundColor = Console.BackgroundColor;
            ConsoleColor StandardForegroundColor = Console.ForegroundColor;

            Console.WriteLine("");

            WriteInColor($"### {LogType} --- {Title} ###", StandardForegroundColor, StandardBackgroundColor, ForegroundColor, BackgroundColor);

            Console.WriteLine(Text);

            WriteInColor($"### {LogType} ###", StandardForegroundColor, StandardBackgroundColor, ForegroundColor, BackgroundColor);

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
