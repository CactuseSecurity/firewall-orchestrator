using System.Net.WebSockets;

namespace FWO.Ui.Services
{
    /// <summary>
    /// Helper for suppressing expected subscription errors during UI shutdown.
    /// </summary>
    public static class SubscriptionExceptionHelper
    {
        /// <summary>
        /// Returns true when a subscription exception should be ignored because the UI circuit is already closing.
        /// </summary>
        public static bool ShouldIgnoreDuringCircuitShutdown(Exception exception, bool circuitIsClosing)
        {
            if (!circuitIsClosing)
            {
                return false;
            }

            return exception is WebSocketException
                || exception is OperationCanceledException
                || exception is ObjectDisposedException
                || exception.Message.Contains("close handshake", StringComparison.OrdinalIgnoreCase);
        }
    }
}
