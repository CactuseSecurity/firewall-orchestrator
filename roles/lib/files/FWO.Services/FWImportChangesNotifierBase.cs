using FWO.Services.EventMediator.Events;
using FWO.Services.EventMediator.Interfaces;
using System.Threading;

namespace FWO.Services
{
    public abstract class FWImportChangesNotifierBase<TEventArgs> where TEventArgs : class, IEventArgs
    {
        private int running = 0;

        public async Task<bool> Run(TEventArgs? eventArgs = null)
        {
            if (Interlocked.Exchange(ref running, 1) == 1)
            {
                return false;
            }
            try
            {
                return await Execute(eventArgs);
            }
            finally
            {
                Interlocked.Exchange(ref running, 0);
            }
        }

        protected abstract Task<bool> Execute(TEventArgs? eventArgs = null);
    }
}
