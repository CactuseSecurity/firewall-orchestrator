using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using FWO.ApiClient;
using FWO.ApiClient.Queries;
using FWO.Middleware.Server.Requests;
using FWO.Config;
using FWO.Logging;
using FWO.Report;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Threading;
using FWO.Middleware.Client;

namespace FWO.Middleware.Server
{
    public class MiddlewareServer
    {
        private readonly string middlewareServerNativeUri;
        private readonly HttpListener listener;
        private const int maxConnectionsCount = 1000;

        private ApiSubscription<List<Ldap>> connectedLdapsSubscription;
        private List<Ldap> connectedLdaps;

        private readonly object changesLock = new object(); // LOCK

        private readonly ConfigFile config;

        private readonly RsaSecurityKey privateJWTKey;
        private readonly int JwtMinutesValid = 240;  // TODO: MOVE TO API/Config    
        // private readonly int JwtMinutesValid = 1;    

        private readonly string apiUri;

        private ReportScheduler reportScheduler;

        public MiddlewareServer()
        {
            config = new ConfigFile();
            apiUri = config.ApiServerUri;
            privateJWTKey = config.JwtPrivateKey;
            middlewareServerNativeUri = config.MiddlewareServerNativeUri;

            string uriToCall = middlewareServerNativeUri;
            if (middlewareServerNativeUri[middlewareServerNativeUri.Length - 1] != '/')
                uriToCall += "/";

            // Create Http Listener
            listener = new HttpListener();


            // Handle timeouts
            //HttpListenerTimeoutManager timeoutManager = listener.TimeoutManager;
            //timeoutManager.IdleConnection = TimeSpan.FromSeconds(10);
            // TODO: Timeout for Request in HandleConnectionAsync

            //listener.AuthenticationSchemes = AuthenticationSchemes.Basic;

            // Create Token Generator
            JwtWriter jwtWriter = GetNewJwtWriter();

            // Create JWT for middleware-server API calls (relevant part is the role middleware-server) and add it to the Api connection header. 
            APIConnection apiConn = GetNewApiConnection(GetNewSelfSignedJwt(jwtWriter));

            // Fetch all connectedLdaps via API (blocking).
            connectedLdaps = apiConn.SendQueryAsync<List<Ldap>>(AuthQueries.getLdapConnections).Result;
            connectedLdapsSubscription = apiConn.GetSubscription<List<Ldap>>(HandleSubscriptionException, AuthQueries.getLdapConnectionsSubscription);
            connectedLdapsSubscription.OnUpdate += ConnectedLdapsSubscriptionUpdate;
            Log.WriteInfo("Found ldap connection to server", string.Join("\n", connectedLdaps.ConvertAll(ldap => $"{ldap.Address}:{ldap.Port}")));

            // Create and start report scheduler
            Task.Factory.StartNew(() =>
            {
                reportScheduler = new ReportScheduler(apiConn, jwtWriter, connectedLdapsSubscription);
            }, TaskCreationOptions.LongRunning);

            // Start Http Listener, todo: move to https
            RunListenerAsync(uriToCall).Wait();
        }

        private async Task RunListenerAsync(string middlewareListenerUri)
        {
            try
            {
                // Add prefixes to listen to 
                listener.Prefixes.Add(middlewareListenerUri + "CreateInitialJWT/");
                listener.Prefixes.Add(middlewareListenerUri + "AuthenticateUser/");
                listener.Prefixes.Add(middlewareListenerUri + "GetAllRoles/");
                listener.Prefixes.Add(middlewareListenerUri + "GetGroups/");
                listener.Prefixes.Add(middlewareListenerUri + "GetUsers/");
                listener.Prefixes.Add(middlewareListenerUri + "AddUser/");
                listener.Prefixes.Add(middlewareListenerUri + "UpdateUser/");
                listener.Prefixes.Add(middlewareListenerUri + "DeleteUser/");
                listener.Prefixes.Add(middlewareListenerUri + "AddUserToRole/");
                listener.Prefixes.Add(middlewareListenerUri + "RemoveUserFromRole/");
                listener.Prefixes.Add(middlewareListenerUri + "RemoveUserFromAllRoles/");
                listener.Prefixes.Add(middlewareListenerUri + "AddLdap/");
                listener.Prefixes.Add(middlewareListenerUri + "AddReportSchedule/");
                listener.Prefixes.Add(middlewareListenerUri + "EditReportSchedule/");
                listener.Prefixes.Add(middlewareListenerUri + "DeleteReportSchedule/");
                listener.Prefixes.Add(middlewareListenerUri + "Test/"); // TODO: REMOVE TEST PREFIX
            }
            catch (Exception exception)
            {
                Log.WriteError("Listener URI", "Could not listen to required middleware URIs.", exception);
                Environment.Exit(-1);
            }

            try
            {
                // Start listener
                listener.Start();
                Log.WriteInfo("Listener started", "Middleware server http listener started.");
            }
            catch (Exception exception)
            {
                Log.WriteError("Start Listener", "Could not start middleware listener.", exception);
                Environment.Exit(-1);
            }

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

            JwtWriter jwtWriterCopy;
            List<Ldap> ldapsCopy;
            APIConnection apiConnectionCopy;
            lock (changesLock) // TODO: Optimize
            {
                jwtWriterCopy = GetNewJwtWriter();
                ldapsCopy = GetNewConnectedLdaps();
                apiConnectionCopy = GetNewApiConnection(GetNewSelfSignedJwt(jwtWriterCopy));
            }

            // checking JWT header to make sure the user is authorized to send the request to the middlware server
            if (requestName == "AuthenticateUser")
            {
                // if user is not authenticated yet, we do not need to check JWT
                // Authenticate user request. Returns jwt if user credentials are valid.
                // Initialize Request Handler  
                AuthenticationRequestHandler authenticationRequestHandler = new AuthenticationRequestHandler(ldapsCopy, jwtWriterCopy, apiConnectionCopy);

                // Try to authenticate user
                (status, responseString) = await authenticationRequestHandler.HandleRequestAsync(request);
            }
            else if (requestName == "CreateInitialJWT")
            {
                // Initialize Request Handler  
                CreateInitialJWTRequestHandler createInitialJWTRequestHandler = new CreateInitialJWTRequestHandler(jwtWriterCopy);

                (status, responseString) = await createInitialJWTRequestHandler.HandleRequestAsync(request);
            }
            else
            {
                // check JWT: jwt must be valid and must contain allowed role admin
                //Client.HttpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwt); // Change jwt in auth header

                // JwtReader jwt = new JwtReader(apiConnectionCopy.GetAuthHeader());
                //HttpListenerBasicIdentity identity = (HttpListenerBasicIdentity)context.User.Identity;

                try
                {
                    JwtReader jwt = new JwtReader(request.Headers.Get("Authorization").Replace("auth ", "").Trim());

                    if (jwt.Validate())
                    {
                        // Find correct way to handle request.
                        if (jwt.JwtContainsAdminRole())
                        {
                            // first the operations allowed for the admin
                            switch (requestName)
                            {
                                case "GetAllRoles":

                                    // Initialize Request Handler  
                                    GetAllRolesRequestHandler getAllRolesRequestHandler = new GetAllRolesRequestHandler(ldapsCopy, apiConnectionCopy);

                                    // Try to get all roles with users
                                    (status, responseString) = await getAllRolesRequestHandler.HandleRequestAsync(request);
                                    break;

                                case "GetGroups":

                                    // Initialize Request Handler  
                                    GetGroupsRequestHandler getGroupsRequestHandler = new GetGroupsRequestHandler(ldapsCopy, apiConnectionCopy);

                                    // Try to get all users from Ldap
                                    (status, responseString) = await getGroupsRequestHandler.HandleRequestAsync(request);
                                    break;

                                case "GetUsers":

                                    // Initialize Request Handler  
                                    GetUsersRequestHandler getUsersRequestHandler = new GetUsersRequestHandler(ldapsCopy, apiConnectionCopy);

                                    // Try to get all users from Ldap
                                    (status, responseString) = await getUsersRequestHandler.HandleRequestAsync(request);
                                    break;

                                case "AddUser":

                                    // Initialize Request Handler  
                                    AddUserRequestHandler addUserRequestHandler = new AddUserRequestHandler(ldapsCopy, apiConnectionCopy);

                                    // Try to add user to role
                                    (status, responseString) = await addUserRequestHandler.HandleRequestAsync(request);
                                    break;

                                case "UpdateUser":

                                    // Initialize Request Handler  
                                    UpdateUserRequestHandler updateUserRequestHandler = new UpdateUserRequestHandler(ldapsCopy, apiConnectionCopy);

                                    // Try to add user to role
                                    (status, responseString) = await updateUserRequestHandler.HandleRequestAsync(request);
                                    break;

                                case "DeleteUser":

                                    // Initialize Request Handler  
                                    DeleteUserRequestHandler deleteUserRequestHandler = new DeleteUserRequestHandler(ldapsCopy, apiConnectionCopy);

                                    // Try to add user to role
                                    (status, responseString) = await deleteUserRequestHandler.HandleRequestAsync(request);
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

                                //case "AddLdap":
                                //    lock (changesLock)
                                //    {
                                //        // Initilaize Request Handler
                                //        AddLdapRequestHandler addLdapRequestHandler = new AddLdapRequestHandler(apiUri, ref connectedLdaps);
                                //        // Try to add new ldap connection
                                //        (status, responseString) = addLdapRequestHandler.HandleRequestAsync(request).Result;
                                //    }
                                //    break;

                                case "RemoveUserFromAllRoles":

                                    // Initialize Request Handler  
                                    RemoveUserFromAllRolesRequestHandler removeUserFromAllRolesRequestHandler = new RemoveUserFromAllRolesRequestHandler(ldapsCopy, apiConnectionCopy);

                                    // Try to remove user from all roles
                                    (status, responseString) = await removeUserFromAllRolesRequestHandler.HandleRequestAsync(request);
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
                        }
                        else if (jwt.JwtContainsAuditorRole())
                        {
                            // read operations allowed also for auditor
                            switch (requestName)
                            {
                                case "GetAllRoles":

                                    // Initialize Request Handler  
                                    GetAllRolesRequestHandler getAllRolesRequestHandler = new GetAllRolesRequestHandler(ldapsCopy, apiConnectionCopy);

                                    // Try to get all roles with users
                                    (status, responseString) = await getAllRolesRequestHandler.HandleRequestAsync(request);
                                    break;

                                case "GetGroups":

                                    // Initialize Request Handler  
                                    GetGroupsRequestHandler getGroupsRequestHandler = new GetGroupsRequestHandler(ldapsCopy, apiConnectionCopy);

                                    // Try to get all users from Ldap
                                    (status, responseString) = await getGroupsRequestHandler.HandleRequestAsync(request);
                                    break;

                                case "GetUsers":

                                    // Initialize Request Handler  
                                    GetUsersRequestHandler getUsersRequestHandler = new GetUsersRequestHandler(ldapsCopy, apiConnectionCopy);

                                    // Try to get all users from Ldap
                                    (status, responseString) = await getUsersRequestHandler.HandleRequestAsync(request);
                                    break;

                                default:
                                    Log.WriteError("Internal Error", $"We received a request we could not handle: {request.RawUrl}", LogStackTrace: true);
                                    status = HttpStatusCode.InternalServerError;
                                    break;
                            }
                        }
                    }
                }
                catch (Exception exception)
                {
                    Log.WriteError("Middleware Server", "Request could not be handled.", exception);
                    status = HttpStatusCode.NotFound;
                }
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
            return new JwtWriter(privateJWTKey, JwtMinutesValid);
        }

        private List<Ldap> GetNewConnectedLdaps()
        {
            // TODO: Replace with other clone method
            return connectedLdaps.DeepClone();
        }

        private string GetNewSelfSignedJwt(JwtWriter jwtWriter)
        {
            return jwtWriter.CreateJWTMiddlewareServer();
        }

        private APIConnection GetNewApiConnection(string jwt)
        {
            return new APIConnection(apiUri, jwt);
        }

        private void HandleSubscriptionException(Exception exception)
        {
            Log.WriteError("Subscription", "Subscription lead to exception.", exception);
        }

        private void ConnectedLdapsSubscriptionUpdate(List<Ldap> ldapsChanges)
        {
            lock (changesLock)
            {
                connectedLdaps = ldapsChanges;
            }
        }
    }
}
