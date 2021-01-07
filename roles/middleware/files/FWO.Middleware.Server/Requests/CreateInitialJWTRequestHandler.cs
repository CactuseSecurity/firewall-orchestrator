using FWO.Middleware.Server.Data;
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


        protected override Task<(HttpStatusCode status, string wrappedResult)> HandleRequestInternalAsync(HttpListenerRequest request)
        {

            // Create User from given parameters
            User user = new User() { Name = "", Password = "" };
            user.Dn = "anonymous";
            user.Roles = new string[] { "anonymous" };
            user.Tenant = null;

            // Authenticate user
            string jwt = tokenGenerator.CreateJWT(user);

            // Return status and result
            return Task.FromResult(WrapResult(HttpStatusCode.OK, ("jwt", jwt)));
        }
    }
}
