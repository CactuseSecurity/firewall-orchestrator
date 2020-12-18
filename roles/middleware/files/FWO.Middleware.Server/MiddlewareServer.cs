using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using FWO.ApiClient;
using FWO.ApiClient.Queries;
using FWO.Middleware.Server.Data;
using FWO.Middleware.Server.Requests;
using FWO.Config;
using FWO.Logging;

namespace FWO.Middleware.Server
{
    public class MiddlewareServer
    {
        private readonly string middlewareServerUri;
        private readonly HttpListener listener;
        private const int maxConnectionsCount = 1000;

        private List<Ldap> connectedLdaps;

        // private readonly object pendingChanges = new object(); // LOCK

        private readonly ConfigFile config;

        private readonly RsaSecurityKey privateJWTKey;
        private readonly int hoursValid = 4;  // TODO: MOVE TO API/Config    

        private readonly string apiUri;

        public MiddlewareServer()
        {
            config = new ConfigFile();
            apiUri = config.ApiServerUri;
            privateJWTKey = config.JwtPrivateKey;
            middlewareServerUri = config.MiddlewareServerUri;

            // Create Http Listener
            listener = new HttpListener();

            // Handle timeouts
            //HttpListenerTimeoutManager timeoutManager = listener.TimeoutManager;
            //timeoutManager.IdleConnection = TimeSpan.FromSeconds(10);
            // TODO: Timeout for Request in HandleConnectionAsync

            // Create Token Generator
            JwtWriter jwtWriter = GetNewJwtWriter();

            // Create JWT for middleware-server API calls (relevant part is the role middleware-server) and add it to the Api connection header. 
            APIConnection apiConn = GetNewApiConnection(GetNewSelfSignedJwt(jwtWriter));

            // Fetch all connectedLdaps via API (blocking).
            connectedLdaps = apiConn.SendQueryAsync<Ldap[]>(AuthQueries.getLdapConnections).Result.ToList();
            Log.WriteInfo("Found ldap connection to server", string.Join("\n", connectedLdaps.ConvertAll(ldap => $"{ldap.Address}:{ldap.Port}")));

            // Start Http Listener, todo: move to https
            RunListenerAsync(middlewareServerUri).Wait();
        }

        private async Task RunListenerAsync(string middlewareListenerUri)
        {
            // Add prefixes to listen to 
            listener.Prefixes.Add(middlewareListenerUri + "AuthenticateUser/");
            listener.Prefixes.Add(middlewareListenerUri + "GetAllRoles/");
            listener.Prefixes.Add(middlewareListenerUri + "GetUsers/");
            listener.Prefixes.Add(middlewareListenerUri + "AddUserToRole/");
            listener.Prefixes.Add(middlewareListenerUri + "RemoveUserFromRole/");
            listener.Prefixes.Add(middlewareListenerUri + "Test/"); // TODO: REMOVE TEST PREFIX

            // Start listener
            listener.Start();
            Log.WriteInfo("Listener started", "Middleware server http listener started.");

            List<Task> connections = new List<Task>(maxConnectionsCount);

            // Handle maxConnectionsCount connections at the same time
            for (int i = 0; i < maxConnectionsCount; i++)
            {
                // Wait for new incoming connection on main thread.
                HttpListenerContext context = await listener.GetContextAsync();

                // Handle incoming connection in new task.
                connections.Add(Task.Run(() => HandleConnectionAsync(context)));
            }

            // Never stop listening to new incoming connections
            while (true)
            {
                // Wait for connection to be finished (One)
                Task finishedTask = await Task.WhenAny(connections.ToArray());
                connections.Remove(finishedTask);

                // Add new incoming connection listener (One)
                HttpListenerContext context = await listener.GetContextAsync();
                connections.Add(Task.Run(() => HandleConnectionAsync(context)));
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

            JwtWriter jwtWriterCopy = GetNewJwtWriter();
            List<Ldap> ldapsCopy = GetNewConnectedLdaps();
            APIConnection apiConnectionCopy = GetNewApiConnection(GetNewSelfSignedJwt(jwtWriterCopy));

            // Find correct way to handle request.
            switch (requestName)
            {
                // Authenticate user request. Returns jwt if user credentials are valid.
                case "AuthenticateUser":

                    // Initialize Request Handler  
                    AuthenticationRequestHandler authenticationRequestHandler = new AuthenticationRequestHandler(ldapsCopy, jwtWriterCopy, apiConnectionCopy);

                    // Try to authenticate user
                    (status, responseString) = await authenticationRequestHandler.HandleRequestAsync(request);
                    break;

                case "GetAllRoles":

                    // Initialize Request Handler  
                    GetAllRolesRequestHandler getAllRolesRequestHandler = new GetAllRolesRequestHandler(ldapsCopy, apiConnectionCopy);

                    // Try to get all roles with users
                    (status, responseString) = await getAllRolesRequestHandler.HandleRequestAsync(request);
                    break;

                case "GetUsers":

                    // Initialize Request Handler  
                    GetUsersRequestHandler getUsersRequestHandler = new GetUsersRequestHandler(ldapsCopy, apiConnectionCopy);

                    // Try to get all users from Ldap
                    (status, responseString) = await getUsersRequestHandler.HandleRequestAsync(request);
                    break;

                case "AddUserToRole":

                    // Initialize Request Handler  
                    AddUserToRoleRequestHandler addUserToRoleRequestHandler = new AddUserToRoleRequestHandler(ldapsCopy, apiConnectionCopy);

                    // Try to add user to role
                    (status, responseString) = await addUserToRoleRequestHandler.HandleRequestAsync(request);
                    break;

                case "RemoveUserFromRole":

                    // Initialize Request Handler  
                    RemoveUserFromRoleRequestHandler removeUserFromRoleRequestHandler = new RemoveUserFromRoleRequestHandler(ldapsCopy, apiConnectionCopy);

                    // Try to remove user from role
                    (status, responseString) = await removeUserFromRoleRequestHandler.HandleRequestAsync(request);
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
            // return jwtWriter.CreateJWT(new User { Name = "middleware-server", Password = "", Roles = new string[] { "middleware-server" } });
            return jwtWriter.CreateJWTMiddlewareServer();
        }

        private APIConnection GetNewApiConnection(string jwt)
        {
            return new APIConnection(apiUri, jwt);
        }
    }
}
