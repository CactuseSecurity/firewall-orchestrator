using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace FWO.Middleware.Server.Requests
{
    class DeleteLdapRequestHandler : RequestHandler
    {
        protected override Task<(HttpStatusCode status, string wrappedResult)> HandleRequestInternalAsync(HttpListenerRequest request)
        {
            throw new NotImplementedException();
        }
    }
}
