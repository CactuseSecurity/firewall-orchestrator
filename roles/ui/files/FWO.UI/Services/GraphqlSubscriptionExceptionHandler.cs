using FWO.Api.Client.ExceptionHandling;

namespace FWO.Ui.Services
{
    /// <summary>
    /// Logs GraphQL subscription exceptions centrally and optionally forwards them to a UI callback.
    /// </summary>
    public static class GraphqlSubscriptionExceptionHandler
    {
        /// <summary>
        /// Logs the exception via the shared GraphQL handler and executes an optional UI callback afterwards.
        /// </summary>
        public static void Handle(Exception exception, Action? displayMessageInUi = null)
        {
            GraphqlExceptionHandler.ExceptionHandler(exception);
            displayMessageInUi?.Invoke();
        }
    }
}
