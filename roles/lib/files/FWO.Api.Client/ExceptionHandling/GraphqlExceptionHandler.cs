using FWO.Logging;

namespace FWO.Api.Client.ExceptionHandling
{
    public static class GraphqlExceptionHandler
    {
        public static Action<Exception> ExceptionHandler = exception => Log.WriteError($"Graphql Exception", exception.Message, exception);
    }
}
