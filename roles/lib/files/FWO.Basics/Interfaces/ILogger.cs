namespace FWO.Basics.Interfaces
{
    public interface ILogger
    {
        void TryWriteInfo(string title, string text, bool condition);
        void TryWriteDebug(string title, string text, bool condition);
        void TryWriteWarning(string title, string text, bool condition);
        void TryWriteError(string title, string text, bool condition);
        void TryWriteError(string title, Exception exception, bool condition);
        void TryWriteAudit(string title, string text, bool condition);
    }
}