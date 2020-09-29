using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using FWO_Auth_Server;
using FWO.Auth.Client;
using FWO.Api;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;
using FWO_Logging;
using FWO_Auth_Server.Requests;
using FWO.Config;

namespace FWO_Auth
{
    public class AuthModule
    {
        private readonly HttpListener Listener;
        private List<Ldap> connectedLdaps;

        private readonly ConfigConnection config;

        private readonly JwtWriter jwtGenerator;
        private readonly RsaSecurityKey privateJWTKey;
        private readonly int hoursValid = 2;

        private readonly string authServerUri;
        private readonly string apiUri;

        private readonly AuthenticationRequestHandler authenticationRequestHandler;

        public AuthModule()
        {
            config = new ConfigConnection();
            apiUri = config.ApiServerUri;
            privateJWTKey = config.JwtPrivateKey;
            authServerUri = config.AuthServerUri;

            // Create Http Listener
            Listener = new HttpListener();

            // Create Token Generator
            jwtGenerator = new JwtWriter(privateJWTKey, hoursValid);

            // create JWT for auth-server API (relevant part is the role auth-server) calls and add it to the Api connection header 
            APIConnection ApiConn = new APIConnection(apiUri);
            ApiConn.SetAuthHeader(jwtGenerator.CreateJWT(new User { Name = "auth-server", Password = "", Roles = new Role[] { new Role("auth-server") } }));

            // fetch all connectedLdaps via API. Blocking wait via result.
            connectedLdaps = ApiConn.SendQuery<Ldap>(Queries.LdapConnections).Result.ToList();

            foreach (Ldap connectedLdap in connectedLdaps)
            {
                Log.WriteInfo("Found ldap connection to server", $"{connectedLdap.Address}:{connectedLdap.Port}");
            }

            // Initialize Request Handler          
            authenticationRequestHandler = new AuthenticationRequestHandler(ref connectedLdaps, jwtGenerator);

            // Start Http Listener, todo: move to https
            StartListener(authServerUri);
        }

        private void StartListener(string AuthServerListenerUri)
        {
            // Add prefixes to listen to 
            Listener.Prefixes.Add(AuthServerListenerUri + "AuthenticateUser/");

            // Start listener
            Listener.Start();
            Log.WriteInfo("Listener started", "Auth server http listener started.");

            // Handle an infinite amount of requests
            while (true)
            {
                // Blocking wait for request
                HttpListenerContext context = Listener.GetContext();

                // Get request
                HttpListenerRequest request = context.Request;

                // Initialize status and response              
                HttpStatusCode status = HttpStatusCode.OK;
                string responseString = "";

                // Get name of request without "/" as first character
                string requestName = request.Url.LocalPath.Remove(0, 1);

                // Log that a request was received
                Log.WriteInfo("Request received", $"New request received: \"{requestName}\".");

                // Find correct way to handle request.
                switch (requestName)
                {
                    // Authenticate user request. Returns jwt if user credentials are valid.
                    case "AuthenticateUser":
                        // Try to authenticate user
                        (status, responseString) = authenticationRequestHandler.HandleRequest(request);
                        break;

                    // Listened to a request but could not handle it. In theory impossible. FATAL ERROR
                    default:
                        Log.WriteError("Internal Error", "We listend to a request we could not handle. How could this happen?", LogStackTrace: true);
                        status = HttpStatusCode.InternalServerError;
                        break;
                }

                // Get response object.
                HttpListenerResponse response = context.Response;
                // Get response stream from response object
                Stream output = response.OutputStream;
                byte[] buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
                response.StatusCode = (int)status;
                response.ContentLength64 = buffer.Length;
                output.Write(buffer, 0, buffer.Length);
                output.Close();
            }
        }
    }
}
