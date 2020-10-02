using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;
using System.Linq;
using System.Net;

namespace FWO.Auth.Client
{
    public class AuthClient
    {
        readonly RequestSender requestSender;

        public AuthClient(string authServerUri)
        {
            requestSender = new RequestSender(authServerUri);
        }

        public async Task<(HttpStatusCode, RequestResult)> AuthenticateUser(string Username, string Password)
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

