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

namespace FWO_Auth
{
    public class AuthModule
    {
        private readonly HttpListener Listener;
        public Ldap[] connectedLdaps;
        private readonly TokenGenerator TokenGenerator;
        private readonly string privateJWTKey; // in armored PEM format
        private readonly int hoursValid = 2;
        private readonly string configFile = "/etc/fworch/fworch.yaml";
        private readonly Config config;
        private readonly string privateJWTKeyFile;
        private readonly string AuthServerIp;
        private readonly string AuthServerPort;
        private readonly string ApiUri;

        public AuthModule()
        {
            try // reading config file
            { 
                config = new Config(configFile);
                ApiUri =  config.GetConfigValue("api_uri");
                privateJWTKeyFile = config.GetConfigValue("auth_JWT_key_file");
                AuthServerIp = config.GetConfigValue("auth_hostname");
                AuthServerPort = config.GetConfigValue("auth_server_port");
            }
            catch (Exception)
            {
                Console.WriteLine($"Error while trying to read config from file {configFile}\n");
                System.Environment.Exit(1); // exit with error
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
            TokenGenerator = new TokenGenerator(AuthClient.ExtractKeyFromPem(privateJWTKey, isPrivateKey), hoursValid);

            // create JWT for auth-server API (relevant part is the role auth-server) calls and add it to the Api connection header 
            APIConnection ApiConn = new APIConnection(ApiUri);
            // ApiConn.ChangeAuthHeader(TokenGenerator.CreateJWT(new User { Name = "auth-server", Password = "" }, new UserData(), new Role[] { new Role("auth-server") }));
            ApiConn.Jwt = TokenGenerator.CreateJWT(new User { Name = "auth-server", Password = "" }, new UserData(), new Role[] { new Role("auth-server") }));
            // fetch all connectedLdaps via API
            Task<Ldap[]> ldapTask = Task.Run(()=> ApiConn.SendQuery<Ldap>(Queries.LdapConnections));
            ldapTask.Wait();
            //Ldap[] connectedLdaps = ldapTask.Result;
            this.connectedLdaps = ldapTask.Result;

            foreach (Ldap connectedLdap in connectedLdaps)
            {
                Console.WriteLine($"Authmodule::Creator: found ldap connection to server {connectedLdap.Address}:{connectedLdap.Port}");
            }
            // Start Http Listener, todo: move to https
            String AuthServerListenerUri = "http://" + AuthServerIp + ":" + AuthServerPort + "/";
            StartListener(AuthServerListenerUri);
        }

        private void StartListener(string AuthServerListenerUri)
        {
            // Add prefixes to listen to 
            Listener.Prefixes.Add(AuthServerListenerUri + "jwt/");

            // Start listener.
            Listener.Start();
            Console.WriteLine($"Auth Server starting on {AuthServerListenerUri}.");

            // GetContext method block while waiting for a request.
            while (true)
            {
                HttpListenerContext context = Listener.GetContext();
                HttpListenerRequest request = context.Request;
                HttpStatusCode status = HttpStatusCode.OK;
                string responseString = "";
                try
                {
                    switch (request.Url.LocalPath)
                    {
                        case "/jwt":
                            responseString = CreateJwt(request);
                            break;

                        default:
                            status = HttpStatusCode.InternalServerError;
                            break;
                    }
                }
                catch (Exception e)
                {
                    status = HttpStatusCode.BadRequest;
                    Console.WriteLine($"Error {e.Message}    Stacktrace  {e.StackTrace}");
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

        private string CreateJwt(User User)
        {
            string responseString = "";

            if (User.Name == "")
            {
                Console.WriteLine("Logging in with anonymous user...");
                responseString = TokenGenerator.CreateJWT(User, null, new Role[] { new Role("anonymous") });
            }                    
            else
            {
                Console.WriteLine($"Try to validate as {User.Name}...");

                // first look for the (first) ldap server with role information
                Ldap roleLdap = null;
                foreach (Ldap connLdap in connectedLdaps) 
                {
                    if (connLdap.RoleSearchPath != "")
                    {
                        roleLdap = connLdap;
                        break;
                    }
                }

                if (roleLdap == null)
                {
                    // TODO: go ahead with anonymous or throw exception?
                    Console.WriteLine("No roles can be determined. Logging in with anonymous user...");
                    responseString = TokenGenerator.CreateJWT(User, null, new Role[] { new Role("anonymous") });
                }
                else
                {
                    // try all configured ldap servers for authentication:
                    responseString = "InvalidCredentials";
                    foreach (Ldap connLdap in connectedLdaps) 
                    {
                        Console.WriteLine($"CreateJwt - trying to authenticate {User} against LDAP {connLdap.Address}:{connLdap.Port} ...");
                        connLdap.Connect();
                        String UserDN = connLdap.ValidateUser(User);
                        if (UserDN!="") 
                        {   
                            // user was successfully authenticated via LDAP
                            Console.WriteLine($"Successfully validated as {User} with DN {UserDN}");
                            // User.UserDN = UserDN;

                            Tenant tenant = new Tenant();
                            tenant.Name = UserDN; //only part of it (first ou)

                            // need to make APICalls available as common library

                            // need to resolve tenant_name from DN to tenant_id first 
                            // query get_tenant_id($tenant_name: String) { tenant(where: {tenant_name: {_eq: $tenant_name}}) { tenant_id } }
                            // variables: {"tenant_name": "forti"}
                            tenant.Id = 0; // todo: replace with APICall() result

                            // get visible devices with the following queries:

                            // query get_visible_mgm_per_tenant($tenant_id:Int!){  get_visible_managements_per_tenant(args: {arg_1: $tenant_id})  id } }
                            String variables = $"\"tenant_id\":{tenant.Id}";
                            // tenant.VisibleDevices = APICall(query,variables);

                            // query get_visible_devices_per_tenant($tenant_id:Int!){ get_visible_devices_per_tenant(args: {arg_1: $tenant_id}) { id }}
                            // variables: {"tenant_id":3}
                            // tenant.VisibleDevices = APICall();
                
                            // tenantInformation.VisibleDevices = {};
                            // tenantInformation.VisibleManagements = [];

                            UserData userData = new UserData();
                            userData.tenant = tenant;
                            responseString = TokenGenerator.CreateJWT(User, userData, roleLdap.GetRoles(UserDN));
                            break;
                        }
                    }
                }
            }  
            return responseString;
        }

        private string CreateJwt(HttpListenerRequest request)
        {
            if (request.HttpMethod == HttpMethod.Post.Method)
            {
                string ParametersJson = new StreamReader(request.InputStream).ReadToEnd();
                Dictionary<string, string> Parameters = JsonSerializer.Deserialize<Dictionary<string, string>>(ParametersJson);
                User User = new User { Name = Parameters["Username"], Password = Parameters["Password"] };
                return CreateJwt(User);
            }
            return $"invalid http method {request.HttpMethod} <> POST";
        }
    }
}
