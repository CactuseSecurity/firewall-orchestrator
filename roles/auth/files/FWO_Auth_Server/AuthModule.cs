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
        private List<Ldap> LdapConnection = new List<Ldap>();
        private readonly TokenGenerator TokenGenerator;
        private readonly string privateJWTKey = "8f4ce02dabb2a4ffdb2137802b82d1283f297d959604451fd7b7287aa307dd298668cd68a432434d85f9bcff207311a833dd5b522870baf457c565c7a716e7eaf6be9a32bd9cd5420a0ebaa9bace623b54c262dcdf35debdb5388490008b9bc61facfd237c1c7058f5287881a37492f523992a2a120a497771954daf27666de2461a63117c8347fe760464e3a58b3a5151af56a0375c8b34921101c91425b65097fc69049f85589a58bb5e5570139c98d3edb179a400b3d142a30e32d1c8e9bbdb90d799fb81b4fa6fb7751acfb3529c7af022590cbb845a8390b906f725f079967b269cff8d2e6c8dbcc561b37c4bdd1928c662b79f42fe56fe108a0cf21e08";
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
                Console.WriteLine($"JWT Key fread from file is {privateJWTKey.Length} Bytes long.");
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
            Task<LdapConnectionApi[]> ldapTask = Task.Run(()=> ApiConn.SendQuery<LdapConnectionApi>(Queries.LdapConnections));
            ldapTask.Wait();
            LdapConnectionApi[] ldapConnections = ldapTask.Result;

            // Create connections to ldap servers
            foreach (LdapConnectionApi conn in ldapConnections)
            {
                LdapConnection.Add(new Ldap(conn.Server, conn.Port)); // todo: add all necessary parameters from LdapConnectionApi, make different ldapconnection classes inherit from each other
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
