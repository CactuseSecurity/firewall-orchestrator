using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FWO.Api.Client
{
    public abstract class ApiSubscription : IDisposable
    {
        private bool disposed = false;

        protected abstract void Dispose(bool disposing);

        public void Dispose()
        {
            if (disposed) return;
            Dispose(true);
            disposed = true;
            GC.SuppressFinalize(this);
        }

        ~ ApiSubscription()
        {
            if (disposed) return;
            Dispose(false);
        }
    }
}
