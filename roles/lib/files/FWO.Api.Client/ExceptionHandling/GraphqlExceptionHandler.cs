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
    }
}
