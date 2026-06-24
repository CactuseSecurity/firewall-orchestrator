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
    }
}
