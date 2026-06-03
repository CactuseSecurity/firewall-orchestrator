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
        /// <param name="exception">The exception to log.</param>
        /// <param name="displayMessageInUi">Optional callback that runs after logging.</param>
        /// <param name="exceptionHandler">Optional override used by tests to inspect the logging flow.</param>
        public static void Handle(Exception exception, Action? displayMessageInUi = null, Action<Exception>? exceptionHandler = null)
        {
            if (exceptionHandler is null)
            {
                GraphqlExceptionHandler.Handle(exception);
            }
            else
            {
                exceptionHandler(exception);
            }

            displayMessageInUi?.Invoke();
        }
    }
}
