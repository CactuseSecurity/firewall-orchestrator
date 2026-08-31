using GraphQL.Client.Http;

namespace FWO.Api.Client
{
    public abstract class ApiSubscription : IDisposable
    {
        private bool _disposed;

        protected internal bool IsDisposed => _disposed;

        internal abstract ApiSubscription Recreate(GraphQLHttpClient graphQlClient);

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
