using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace FWO.Middleware.Server.Requests
{
    class CreateInitialJWTRequestHandler : RequestHandler
    {
        private readonly JwtWriter tokenGenerator;

        public CreateInitialJWTRequestHandler(JwtWriter tokenGenerator)
        {
            this.tokenGenerator = tokenGenerator;
        }

        protected override async Task<(HttpStatusCode status, string wrappedResult)> HandleRequestInternalAsync(HttpListenerRequest request)
        {
            string jwt = await tokenGenerator.CreateJWT();

            // Return status and result
            return WrapResult(HttpStatusCode.OK, ("jwt", jwt));
        }
    }
}
