using FWO.Logging;
using System.Runtime.CompilerServices;

namespace FWO.Api.Client.ExceptionHandling
{
    public static class GraphqlExceptionHandler
    {
        public static void Handle(Exception exception)
        {
            Log.WriteError("Graphql Exception", exception.Message, exception);
        }

        public static void Handle(Exception exception, [CallerMemberName] string callerName = "")
        {
            Log.WriteError("Graphql Exception", exception.Message, exception, callerName: callerName);
        }

        public static void Handle(Exception exception, [CallerMemberName] string callerName = "", params Span<Exception> ignoreExceptions)
        {
            if (!ignoreExceptions.Contains(exception))
            {
                Log.WriteError("Graphql Exception", exception.Message, exception, callerName: callerName);
            }
        }
    }
}
