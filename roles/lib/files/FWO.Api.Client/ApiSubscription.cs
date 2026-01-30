namespace FWO.Api.Client
{
    public abstract class ApiSubscription : IDisposable
    {
        private bool _disposed;

        protected bool IsDisposed => _disposed;

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
