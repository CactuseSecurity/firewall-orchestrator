using GraphQL.Client.Http;

namespace FWO.Api.Client
{
    public abstract class ApiSubscription : IDisposable
    {
        private bool _disposed;

        protected internal bool IsDisposed => _disposed;

        internal abstract void Rebind(GraphQLHttpClient graphQlClient);

        /// <summary>
        /// Marks the subscription as disposed. Derived classes release their stream resources.
        /// </summary>
        /// <param name="disposing">True when called from <see cref="Dispose()"/>, false when called from a finalizer.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        /// <summary>
        /// Releases the subscription.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
