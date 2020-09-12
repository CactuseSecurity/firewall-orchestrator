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
        private readonly string privateJWTKey = "b2c6843feafd1d52eb689a5233a12588957c7682b581d6e06113adc8da70e7cebc08f6f5a7dd515796cd1f6b87912cc0093b80165ff8c7df77fb0ef6124d5ac0b607a2cf8515352d4fe509df42c687da87b08262571e3650e71e2ecc09db4ad7b154dd4630b8482ec12ae72715de99887cef338fdd46b2994336ca72ede588c77ddf9b2aaa25d9b1c4b3c4038795b4355d24370d7e4bac6e8a724ef959ccbe38b9cbc0ee99ebc705d8f450b601a9465f83dd643c926b17858be55d312abfbbf0ae9916a1de0fe9ab6e3a1489e66f9f1f4d844db412de40c2f3266eb175bae32e013d166c520b56050b1489c2780eb820ce8b3fb69a9b754e17ba426f0116a3b7";
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
            Task<Ldap[]> ldapTask = Task.Run(()=> ApiConn.SendQuery<Ldap>(Queries.LdapConnections));
            ldapTask.Wait();
            //Ldap[] ldapConnections = ldapTask.Result;
            this.LdapConnection = ldapTask.Result;

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
