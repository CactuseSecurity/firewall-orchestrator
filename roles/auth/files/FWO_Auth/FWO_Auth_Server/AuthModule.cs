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

        private readonly byte[] privateKey = Encoding.UTF8.GetBytes("769d910a91f5ccce38cecf976d04c47bb8906160c359936e3c321ee0f3d496009190a4ddb81d79934d15291d2e0b0ecd5f43122acb4deea0d5f52d657a44d9aa50dc6145b969d0f6ed7ab9f161f80b7dfcb158104d3097f17b487190ac18d71f3b1fa92c2862f238360ae955ab626b278763c7ae889350624532ccc07fd7ada256af826fcf6f8df91f400aca67c267afb4dc6df689a2c20f280d85cb99d9cb44615d96ecdb4a215e69403b2f1c350112b6cb8333c87b59e98f16f2748bab1ca74ca808cf1c7bf320c4914c767e40e0bc4dffef05c6b28794a73d67ee09ef9b55be2ec0d2b5e0e5a548582ae095a36245c433371a560c7e4cf0011dfd657a708e");
        private readonly int daysValid = 7;

        public AuthModule()
        {
            // TODO: Get Ldap Server URI + Http Listener URI from config file

            // Get private key from
            // absolute path: "fworch_home/etc/secrets/jwt_private.key"
            // absolute path: "fworch_home/etc/secrets/jwt_private.key"
            // relative path:  "../../../etc/secrets"

            try
            {
                string privateKeyString = File.ReadAllText("../../../etc/secrets/jwt_private.key").TrimEnd();
                privateKey = Encoding.UTF8.GetBytes(privateKeyString);

                Console.WriteLine($"Key is {privateKey.Length} Bytes long.");
                Console.WriteLine($"Key is {privateKeyString}");
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
//#if Release
            LdapConnection = new Ldap("localhost", 636);
// #else
//             LdapConnection = new Ldap("localhost", 6636);
// #endif
            // Create Token Generator
            TokenGenerator = new TokenGenerator(privateKey, daysValid);

            // Start Http Listener
            StartListener("http://localhost:8888/");
        }

        private void StartListener(string ListenerUri)
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

        private string CreateJwt(HttpListenerRequest request)
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
                    responseString = TokenGenerator.CreateJWT(User, null, LdapConnection.GetRoles(User));                 
                }
                    
                // REMOVE LATER                             

                else
                {
                    Console.WriteLine($"Try to validate as {User.Name}...");

                    if (LdapConnection.ValidateUser(User)) 
                    {
                        Console.WriteLine($"Successfully validated as {User}!");

                        responseString = TokenGenerator.CreateJWT(User, null, LdapConnection.GetRoles(User));
                        Console.WriteLine($"User {User.Name} was assigned the following roles: {responseString}");
                    }

                    else
                    {
                        Console.WriteLine($"Invalid Credentials for User {User.Name}!");
                    }
                }                   
            }

            return responseString;
        }
    }
}
