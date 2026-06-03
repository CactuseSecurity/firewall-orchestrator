using FWO.Logging;

namespace FWO.Api.Client.ExceptionHandling
{
    public static class GraphqlExceptionHandler
    {
        public static Action<Exception> ExceptionHandler = static exception => Log.WriteError($"Graphql Exception", exception.Message, exception);
    }
}
