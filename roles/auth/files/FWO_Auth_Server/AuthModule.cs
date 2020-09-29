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

namespace FWO_Auth
{
    public class AuthModule
    {
        private readonly HttpListener Listener;
        private List<Ldap> connectedLdaps;
        private readonly JwtWriter jwtGenerator;
        private readonly string privateJWTKey;
        private readonly int hoursValid = 2;
        private readonly string configFile = "../../../../../etc/fworch.yaml";  // todo: replace with abs path in release?
        private readonly Config config;
        private readonly string privateJWTKeyFile;
        private readonly string AuthServerIp;
        private readonly string AuthServerPort;
        private readonly string ApiUri;

        private readonly AuthenticationRequestHandler authenticationRequestHandler;


        public AuthModule()
        {
            try // reading config file
            {
                config = new Config(configFile);
                ApiUri = config.GetConfigValue("api_uri");
                privateJWTKeyFile = config.GetConfigValue("auth_JWT_key_file");
                AuthServerIp = config.GetConfigValue("auth_hostname");
                AuthServerPort = config.GetConfigValue("auth_server_port");
            }
            catch (Exception exception)
            {
                Log.WriteError("Config file loading", $"Error while trying to read config from file { configFile}\n", exception);
                Environment.Exit(1); // exit with error
            }

            try  // move to database and access via Api?
            {
                // privateJWTKey = new StreamReader(privateJWTKeyFile);
                privateJWTKey = File.ReadAllText(privateJWTKeyFile).TrimEnd();
                Console.WriteLine($"JWT Key read from file is {privateJWTKey.Length} bytes long: {privateJWTKey}");
            }
            catch (Exception e)
            {
                ConsoleColor StandardConsoleColor = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Out.WriteAsync($"Error while trying to read private key : \n Message \n ### \n {e.Message} \n ### \n StackTrace \n ### \n {e.StackTrace} \n ### \n");
                Console.Out.WriteAsync($"Using fallback key! \n");
                Console.ForegroundColor = StandardConsoleColor;
            }

            // Create Http Listener
            Listener = new HttpListener();

            // Create Token Generator
            bool isPrivateKey = true;
            jwtGenerator = new JwtWriter(AuthClient.ExtractKeyFromPem(privateJWTKey, isPrivateKey), hoursValid);

            // create JWT for auth-server API (relevant part is the role auth-server) calls and add it to the Api connection header 
            APIConnection ApiConn = new APIConnection(ApiUri);
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
            string AuthServerListenerUri = "http://" + AuthServerIp + ":" + AuthServerPort + "/";
            StartListener(AuthServerListenerUri);
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

                // Try to handle request
                try
                {
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
                }
                catch (Exception exception)
                {
                    status = HttpStatusCode.BadRequest;

                    Log.WriteError($"Request \"{requestName}\"",
                        $"An error occured while handling request \"{requestName}\" from \"{context.User.Identity.Name}\". \nSending error to requester.",
                        exception);

                    Dictionary<string, Exception> errorWraper = new Dictionary<string, Exception>
                    {
                        { "error", exception }
                    };

                    responseString = JsonSerializer.Serialize(errorWraper);
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
