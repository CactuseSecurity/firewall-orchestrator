using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
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

        private readonly byte[] privateKey = Encoding.UTF8.GetBytes("d105c8a1d0091ed4d2e4dba3d7bcd5e6839c852a8eaf08052dfcd7a2935b190ebdc212fc859a9998b5655ea27686539d537ba4603f3631f1298780a0e034a8c77b7de9ae03be9cf961155c969e4c031e2997d5c02617739c52e9f32755e49fcecc98d1da5e7bdd570df5faac3ce0c40d54ec5e41075e6fc37a4471e2a081ae1fb2948bc63d4075345a1c599caecc272fd64348ad4f281e860bf1bf0c35b816fa6d63382d48da08ea0a33901695ef4ad82559db39e6768560a3cc18983a68d6dd0f001df7c45605e71c06c43d5da69c4390f607616b2046c1ca3db0800e9e4ee87bdae77800b8448f2fdc682f9a3cd32739a4c9af4f0126273281906b1da05f9e");
        private readonly int daysValid = 7;

        public AuthModule()
        {
            // TODO: Get Ldap Server URI + Http Listener URI from config file

            // Get private key from
            // absolute path: "fworch_home/etc/secrets/jwt_private.key"
            // relative path:  "../../../etc/secrets"
            try
            {
                // privateKey = File.ReadAllText("../../../etc/secrets/jwt_private.key");
                privateKey = File.ReadAllBytes("/usr/local/fworch/etc/secrets/jwt_private.key");
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

                    Console.WriteLine($"Error {e.Message}    Stacktrace  {e.StackTrace}");
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
                {
                    Console.WriteLine("Logging in with fake user...");
                    responseString = await TokenGenerator.CreateJWTAsync(User, null, LdapConnection.GetRoles(User));                 
                }
                    
                // REMOVE LATER                             

                else
                {
                    Console.WriteLine($"Try to validate as {User}...");

                    if (LdapConnection.ValidateUser(User)) 
                    {
                        Console.WriteLine($"Successfully validated as {User}!");
                        responseString = await TokenGenerator.CreateJWTAsync(User, null, LdapConnection.GetRoles(User));
                    }

                    else
                    {
                        Console.WriteLine($"Invalid Credentials for User {User}!");
                    }
                }                   
            }

            return responseString;
        }
    }
}
