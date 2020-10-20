using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using FWO.ApiClient;
using FWO.ApiClient.Queries;
using FWO.Auth.Server.Data;
using FWO.Auth.Server.Requests;
using FWO.Config;
using FWO.Logging;

namespace FWO.Auth.Server
{
    public class AuthModule
    {
        private readonly string authServerUri;
        private readonly HttpListener listener;
        private const int maxConnectionsCount = 1000;

        private List<Ldap> connectedLdaps;

        private readonly object pendingChanges = new object(); // LOCK

        private readonly ConfigConnection config;

        private readonly RsaSecurityKey privateJWTKey;
        private readonly int hoursValid = 2;  // TODO: MOVE TO API    

        private readonly string apiUri;

        public AuthModule()
        {
            config = new ConfigConnection();
            apiUri = config.ApiServerUri;
            privateJWTKey = config.JwtPrivateKey;
            authServerUri = config.AuthServerUri;

            // Create Http Listener
            listener = new HttpListener();

            // Handle timeouts
            //HttpListenerTimeoutManager timeoutManager = listener.TimeoutManager;
            //timeoutManager.IdleConnection = TimeSpan.FromSeconds(10);
            // TODO: Timeout for Request in HandleConnectionAsync

            // Create Token Generator
            JwtWriter jwtWriter = GetNewJwtWriter();

            // Create JWT for auth-server API (relevant part is the role auth-server) calls and add it to the Api connection header. 
            APIConnection apiConn = GetNewApiConnection(GetNewSelfSignedJwt(jwtWriter));

            // Fetch all connectedLdaps via API (blocking).
            connectedLdaps = apiConn.SendQueryAsync<Ldap>(BasicQueries.getLdapConnections).Result.ToList();
            Log.WriteInfo("Found ldap connection to server", string.Join("\n", connectedLdaps.ConvertAll(ldap => $"{ldap.Address}:{ldap.Port}")));

            // Start Http Listener, todo: move to https
            RunListenerAsync(authServerUri).Wait();
        }

        private async Task RunListenerAsync(string AuthServerListenerUri)
        {
            // Add prefixes to listen to 
            listener.Prefixes.Add(AuthServerListenerUri + "/AuthenticateUser/");
            listener.Prefixes.Add(AuthServerListenerUri + "/Test/"); // TODO: REMOVE TEST PREFIX

            // Start listener
            listener.Start();
            Log.WriteInfo("Listener started", "Auth server http listener started.");

            Task[] connections = new Task[maxConnectionsCount];

            // Handle maxConnectionsCount connections at the same time
            for (int i = 0; i < maxConnectionsCount; i++)
            {
                // Wait for new incoming connection on main thread.
                HttpListenerContext context = await listener.GetContextAsync();

                // Handle incoming connection in new task.
                connections[i] = Task.Run(() => HandleConnectionAsync(context));
            }

            // Never stop listening to new incoming connections
            while (true)
            {
                // Wait for connection to be finished (One)
                int finishedTaskIndex = Task.WaitAny(connections.ToArray());

                // Add new incoming connection listener (One)
                HttpListenerContext context = await listener.GetContextAsync();
                connections[finishedTaskIndex] = Task.Run(() => HandleConnectionAsync(context));
            }
        }

        private async Task HandleConnectionAsync(HttpListenerContext context)
        {
            // Get request
            HttpListenerRequest request = context.Request;

            // Initialize status and response              
            HttpStatusCode status = HttpStatusCode.OK;
            string responseString = "";

            // Get name of request without leading and trailing '/' and '\'
            string requestName = request.Url.LocalPath.Trim('\\', '/');
            Log.WriteInfo("Request received", $"New request received: \"{requestName}\".");

            // Find correct way to handle request.
            switch (requestName)
            {
                // Authenticate user request. Returns jwt if user credentials are valid.
                case "AuthenticateUser":

                    JwtWriter jwtWriterCopy = GetNewJwtWriter();
                    List<Ldap> ldapsCopy = GetNewConnectedLdaps();
                    APIConnection apiConnectionCopy = GetNewApiConnection(GetNewSelfSignedJwt(jwtWriterCopy));

                    // Initialize Request Handler  
                    AuthenticationRequestHandler authenticationRequestHandler = new AuthenticationRequestHandler(ldapsCopy, jwtWriterCopy, apiConnectionCopy);

                    // Try to authenticate user
                    (status, responseString) = await authenticationRequestHandler.HandleRequestAsync(request);
                    break;

                // TODO: REMOVE TEST PREFIX
                case "Test":
                    await Task.Delay(5000);
                    break;

                // Listened to a request but could not handle it. In theory impossible. FATAL ERROR
                default:
                    Log.WriteError("Internal Error", $"We received a request we could not handle: {request.RawUrl}", LogStackTrace: true);
                    status = HttpStatusCode.InternalServerError;
                    break;
            }

            // Get response object.
            HttpListenerResponse response = context.Response;

            // Get response stream from response object
            Stream output = response.OutputStream;

            // Write, set status (response)
            byte[] buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
            response.StatusCode = (int)status;
            response.ContentLength64 = buffer.Length;
            output.Write(buffer, 0, buffer.Length);

            // Every request should be send over a new connection because http pipelining for the same connection is not supported
            response.KeepAlive = false;

            output.Close();

            Log.WriteDebug("Connection", "Response sent and connection closed");
        }

        private JwtWriter GetNewJwtWriter()
        {            
            // TODO: Make privateJWTKey thread safe
            return new JwtWriter(privateJWTKey, hoursValid);
        }

        private List<Ldap> GetNewConnectedLdaps()
        {
            // TODO: Replace with other clone method
            return connectedLdaps.DeepClone();
        }

        private string GetNewSelfSignedJwt(JwtWriter jwtWriter)
        {
            return jwtWriter.CreateJWT(new User { Name = "auth-server", Password = "", Roles = new string[] { "auth-server" } });
        }

        private APIConnection GetNewApiConnection(string jwt)
        {
            return new APIConnection(apiUri, jwt);
        }
    }
}
