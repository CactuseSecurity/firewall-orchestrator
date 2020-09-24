using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using FWO_Auth_Server;

namespace FWO_Auth
{
    public class HttpServer
    {
        HttpListener Listener;
        LdapServerConnection LdapConnection;

        public HttpServer()
        {
            LdapConnection = new LdapServerConnection("localhost", 20224);
            LdapConnection.ValidateUser("", "");
            Start();
        }

        private void Start()
        {
            // Create listener.
            Listener = new HttpListener();

            // Add the prefixes.
            Listener.Prefixes.Add("http://localhost:8888/jwt/");

            // Start listener.
            Listener.Start();
            Console.WriteLine("Listening...");

            while (true)
            {
                // Note: The GetContext method blocks while waiting for a request.
                HttpListenerContext context = Listener.GetContext();

                HttpListenerRequest request = context.Request;
                HttpStatusCode status = HttpStatusCode.NotFound;

                string responseString = "";

                switch (request.Url.LocalPath)
                {
                    case "/jwt":
                        if (request.HttpMethod == HttpMethod.Post.Method)
                        {
                            status = HttpStatusCode.OK;
                            string ParametersJson = new StreamReader(request.InputStream).ReadToEnd();
                            Dictionary<string, string> Parameters = JsonSerializer.Deserialize<Dictionary<string, string>>(ParametersJson);
                            responseString = LdapConnection.ValidateUser(Parameters["Username"], Parameters["Password"]) ? "ok" : "wrong";
                        }
                        break;

                    default:
                        break;
                }

                // Obtain a response object.
                HttpListenerResponse response = context.Response;

                byte[] buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
                // Get a response stream and write the response to it.
                response.StatusCode = (int)status;
                response.ContentLength64 = buffer.Length;
                Stream output = response.OutputStream;
                output.Write(buffer, 0, buffer.Length);
                // You must close the output stream.
                output.Close();
            }
        }
    }
}
