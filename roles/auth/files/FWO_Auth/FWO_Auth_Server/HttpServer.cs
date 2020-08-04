using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;

namespace FWO_Auth
{
    public class HttpServer
    {
        HttpListener Listener;

        public HttpServer()
        {
            Start();
        }

        private void Start()
        {
            // testing github...
            // Create listener.
            Listener = new HttpListener();

            // Add the prefixes.
            Listener.Prefixes.Add("http://localhost:8888/jwt/");

            // Start listener.
            Listener.Start();
            Console.WriteLine("Listening...");

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
                        string ParametersJson = new StreamReader(request.InputStream).ReadToEnd();
                        Dictionary<string, string> Parameters = JsonSerializer.Deserialize<Dictionary<string, string>>(ParametersJson);
                        status = HttpStatusCode.OK;
                        responseString = "jwt stub " + Parameters["Username"] + " " + Parameters["Password"];
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
            Listener.Stop();
        }
    }
}
