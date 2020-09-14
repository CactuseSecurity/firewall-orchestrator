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
using FWO.Api;

//using FWO_Auth_Server.Config;

namespace FWO_Auth
{
    public class AuthModule
    {
        private readonly HttpListener Listener;
        public Ldap[] LdapConnection;
        private readonly TokenGenerator TokenGenerator;
        private readonly string privateJWTKey = "bde957bc743e5dd9e0b0a8a1a5cd53c5d12a3e1fa99e0af7883e0326fd595539103bc1c0b740845b73af30cd2bebd2ed7d3b53d1aa7e0385609499e67091175d3655a5d95ed5b4e813aed08eea6133c3db1c2b8be9d3df0e5d32793bb8e308485b8d58c6cb68f60e0130457a957b929b02eedadc4b39acf0c0bd544dd534ffc9dbf5422d739a0b016475ca91ff5e45b874e728c0110ffd5188922ef547c4e2564209b5927f58a7cc19f9eb579432063c91d64cda9f58550c76fb22dac71541a40c347b9b8406b83fe68dbcabaff7bb7e8314f5d69e24653688b3cef43d76513c030334f2903d39be0b072a5ecaf99b483c2cc29003fcd8a16b957273beb32a1f";
        private readonly int daysValid = 7;
        private readonly string configFile = "../../../../../etc/fworch.yaml";  // todo: replace with abs path in release?
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
            catch (Exception eConfigFileRead)
            {
                Console.WriteLine($"Error while trying to read config from file {configFile}\n");
                System.Environment.Exit(1); // exit with error
            }

            try  // move to database and access via Api?
            {
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
            TokenGenerator = new TokenGenerator(privateJWTKey, daysValid);

            // create JWT for auth-server API (relevant part is the role auth-server) calls and add it to the Api connection header 
            APIConnection ApiConn = new APIConnection(ApiUri);
            ApiConn.ChangeAuthHeader(TokenGenerator.CreateJWT(new User { Name = "auth-server", Password = "" }, new UserData(), new Role[] { new Role("auth-server") }));
            
            // fetch all LdapConnections via API
            Task<Ldap[]> ldapTask = Task.Run(()=> ApiConn.SendQuery<Ldap>(Queries.LdapConnections));
            ldapTask.Wait();
            //Ldap[] ldapConnections = ldapTask.Result;
            this.LdapConnection = ldapTask.Result;

            foreach (Ldap connection in LdapConnection)
            {
                Console.WriteLine($"Authmodule::Creator: found ldap connection to server {connection.Address}:{connection.Port}");
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

                // try all configured ldap servers for authentication:
                foreach (Ldap ldapConn in LdapConnection) 
                {
                    ldapConn.Connect();
                    String UserDN = ldapConn.ValidateUser(User);
                    if (UserDN!="") 
                    {   // user was successfully auhtenticated via LDAP
                        Console.WriteLine($"Successfully validated as {User} with DN {UserDN}");
                        // User.UserDN = UserDN;

                        Tenant tenant = new Tenant();
                        tenant.tenantName = UserDN; //only part of it (first ou)

                        // need to make APICalls available as common library

                        // need to resolve tenant_name from DN to tenant_id first 
                        // query get_tenant_id($tenant_name: String) { tenant(where: {tenant_name: {_eq: $tenant_name}}) { tenant_id } }
                        // variables: {"tenant_name": "forti"}
                        tenant.tenantId = 0; // todo: replace with APICall() result

                        // get visible devices with the following queries:

                        // query get_visible_mgm_per_tenant($tenant_id:Int!){  get_visible_managements_per_tenant(args: {arg_1: $tenant_id})  id } }
                        String variables = $"\"tenant_id\":{tenant.tenantId}";
                        // tenant.VisibleDevices = APICall(query,variables);

                        // query get_visible_devices_per_tenant($tenant_id:Int!){ get_visible_devices_per_tenant(args: {arg_1: $tenant_id}) { id }}
                        // variables: {"tenant_id":3}
                        // tenant.VisibleDevices = APICall();
            
                        // tenantInformation.VisibleDevices = {};
                        // tenantInformation.VisibleManagements = [];

                        UserData userData = new UserData();
                        userData.tenant = tenant;
                        responseString = TokenGenerator.CreateJWT(User, userData, ldapConn.GetRoles(UserDN));
                    }

                    else
                    {
                        responseString = "InvalidCredentials";
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
