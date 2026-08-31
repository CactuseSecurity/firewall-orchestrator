using GraphQL.Client.Http;

namespace FWO.Api.Client
{
    internal interface IRebindableApiSubscription
    {
        void Rebind(GraphQLHttpClient graphQlClient);
    }

    public abstract class ApiSubscription : IDisposable
    {
        private bool _disposed;

        protected internal bool IsDisposed => _disposed;

        internal abstract ApiSubscription Recreate(GraphQLHttpClient graphQlClient);

        protected abstract void Dispose(bool disposing);

        public void Dispose()
        {
            if (_disposed) return;
            Dispose(true);
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
