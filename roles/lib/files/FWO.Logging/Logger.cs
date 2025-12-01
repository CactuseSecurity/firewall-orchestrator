using FWO.Basics.Interfaces;

namespace FWO.Logging
{
    public class Logger : ILogger
    {
        public void TryWriteInfo(string title, string text, bool condition)
        {
            if(condition)
            {
                Log.WriteInfo(title, text);
            }
        }

        public void TryWriteDebug(string title, string text, bool condition)
        {
            if(condition)
            {
                Log.WriteDebug(title, text);
            }
        }

        public void TryWriteWarning(string title, string text, bool condition)
        {
            if (condition)
            {
                Log.WriteWarning(title, text);
            }
        }

        public void TryWriteError(string title, string text, bool condition)
        {
            if (condition)
            {
                Log.WriteError(title, text);
            }
        }

        public void TryWriteError(string title, Exception exception, bool condition)
        {
            if (condition)
            {
                Log.WriteError(title, Error: exception);
            }
        }

        public void TryWriteAudit(string title, string text, bool condition)
        {
            if (condition)
            {
                Log.WriteAudit(title, text);
            }
        }
    }
}