using FWO.Logging;

namespace FWO.Api.Client.ExceptionHandling
{
    public static class GraphqlExceptionHandler
    {
        /// <summary>
        /// Logs a GraphQL exception with the shared application logger.
        /// </summary>
        /// <param name="exception">The exception to log.</param>
        public static void Handle(Exception exception)
        {
            Log.WriteError("Graphql Exception", exception.Message, exception);
        }

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
                Handle(exception);
            }
            else
            {
                exceptionHandler.Invoke(exception);
            }

            displayMessageInUi?.Invoke();
        }
    }
}
