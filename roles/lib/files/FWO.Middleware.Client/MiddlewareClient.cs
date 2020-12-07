using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;
using System.Linq;
using System.Net;

namespace FWO.Middleware.Client
{
    public class MiddlewareClient
    {
        readonly RequestSender requestSender;

        public MiddlewareClient(string middlewareServerUri)
        {
            requestSender = new RequestSender(middlewareServerUri);
        }

        public async Task<MiddlewareServerResponse> AuthenticateUser(string Username, string Password)
        {
            Dictionary<string, object> parameters = new Dictionary<string, object>
            {
                { "Username", Username },
                { "Password", Password }
            };

            return await requestSender.SendRequest(parameters, "AuthenticateUser");
        }
    }
}

