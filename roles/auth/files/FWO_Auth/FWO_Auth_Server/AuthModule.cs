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

namespace FWO_Auth
{
    public class AuthModule
    {
        private readonly HttpListener Listener;
        private readonly Ldap LdapConnection;
        private readonly TokenGenerator TokenGenerator;

        private readonly string privateKey = "J6k2eVCTXDp5b97u6gNH5GaaqHDxCmzz2wv3PRPFRsuW2UavK8LGPRauC4VSeaetKTMtVmVzAC8fh8Psvp8PFybEvpYnULHfRpM8TA2an7GFehrLLvawVJdSRqh2unCnWehhh2SJMMg5bktRRapA8EGSgQUV8TCafqdSEHNWnGXTjjsMEjUpaxcADDNZLSYPMyPSfp6qe5LMcd5S9bXH97KeeMGyZTS2U8gp3LGk2kH4J4F3fsytfpe9H9qKwgjb";
        private readonly int daysValid = 7;

        public AuthModule()
        {
            // TODO: Get Ldap Server URI + Http Listener URI from config file

            // Get private key from
            // absolute path: "fworch_home/etc/secrets/jwt_private.key"
            // relative path:  "../../../etc/secrets"
            try
            {
                privateKey = File.ReadAllText("../../../etc/secrets/jwt_private.key");
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

            // Create connection to Ldap Server
            LdapConnection = new Ldap("localhost", 636);

            // Create Token Generator
            TokenGenerator = new TokenGenerator(privateKey, daysValid);

            // Start Http Listener
            StartListener("http://localhost:8888/");
        }

        private async void StartListener(string ListenerUri)
        {
            // Add prefixes to listen to 
            Listener.Prefixes.Add(ListenerUri + "jwt/");

            // Start listener.
            Listener.Start();
            Console.WriteLine("Listening...");

            while (true)
            {
                // Note: The GetContext method blocks while waiting for a request.
                HttpListenerContext context = Listener.GetContext();

                HttpListenerRequest request = context.Request;
                HttpStatusCode status = HttpStatusCode.OK;

                string responseString = "";

                try
                {
                    switch (request.Url.LocalPath)
                    {
                        case "/jwt":
                            responseString = await CreateJwt(request);
                            break;

                        default:
                            status = HttpStatusCode.InternalServerError;
                            break;
                    }
                }

                catch (Exception e)
                {
                    status = HttpStatusCode.BadRequest;

                    // Todo: Log error
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

        private async Task<string> CreateJwt(HttpListenerRequest request)
        {
            string responseString = "";

            if (request.HttpMethod == HttpMethod.Post.Method)
            {
                string ParametersJson = new StreamReader(request.InputStream).ReadToEnd();
                Dictionary<string, string> Parameters = JsonSerializer.Deserialize<Dictionary<string, string>>(ParametersJson);

                User User = new User { Name = Parameters["Username"], Password = Parameters["Password"] };

                // TODO: REMOVE LATER
                if (User.Name == "" && User.Password == "")
                    responseString = await TokenGenerator.CreateJWTAsync(User, null, LdapConnection.GetRoles(User));
                // REMOVE LATER                 

                else if (LdapConnection.ValidateUser(User))
                    responseString = await TokenGenerator.CreateJWTAsync(User, null, LdapConnection.GetRoles(User));                   
            }

            return responseString;
        }
    }
}
