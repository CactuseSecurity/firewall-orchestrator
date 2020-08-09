using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using GraphQL;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.SystemTextJson;
using Microsoft.AspNetCore.Mvc.Formatters;
using System.Net;

namespace FWO
{
    public class APIConnection
    {
        const string APIHost = "localhost";
        const string APIPort = "443";
        const string APIPath = "/api/v1/graphql";
/*
        for local API testing (in visual studio without running full ansible installer), either 
            - create a local ssh tunneling to the http server on the virtual machine on an arbitrary port (here 8443) to connect to api like this:
              const string APIPort = "8443";
            - or use the demo system as api host like this: 
              const string APIHost = "demo.itsecorg.de";
*/
        // Server URL
        private const string ServerURI = "https://" + APIHost + ":" + APIPort + APIPath;

        // Http/s Client
        private readonly HttpClient Client;

        public APIConnection()
        {         
            // Allow all certificates // REMOVE IF SERVER GOT VALID CERTIFICATE
            HttpClientHandler Handler = new HttpClientHandler();
            Handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => { return true; };

            // New http/s client
            Client = new HttpClient(Handler);
        }

        //Query is structured as follow: { "query" : " 'query' ", "variables" : { 'variables' } } with 'query' as query to send and 'variables' as corresponding variables
        public async Task<string> SendQuery(string Query, string Variables = "", string OperationName = "")
        {
            // New http-body containing the query
            StringContent content = new StringContent("{ \"query\": \"" + Query + "\" , \"variables\" : { " + Variables + " } }", Encoding.UTF8);
            // Remove all standard headers
            content.Headers.Clear();
            // Add content header
            content.Headers.Add("content-type", "application/json");
            // Add auth header
            content.Headers.Add("x-hasura-admin-secret", "st8chelt1er");
#if DEBUG
            // Start time measurement
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
#endif
            HttpResponseMessage response;
            string responseString;

            try
            {
                // Send http-packet with query and header. Receive answer
                response = await Client.PostAsync(ServerURI, content);
            }
            catch (Exception e)
            {
                return "";
                //TODO: Server can not be reached
            }
#if DEBUG
            // Stop time measurement
            stopwatch.Stop();
            Debug.WriteLine("Query Server Response Time: " + stopwatch.ElapsedMilliseconds + "ms");
#endif

            try
            {
                // Convert answer to string
                responseString = await response.Content.ReadAsStringAsync();
            }
            catch (Exception e)
            {
                return "";
                //TODO: Answer can not be converted to string
            }

            // Return answer
            return responseString;

            //TODO: https://www.youtube.com/watch?v=4XlA2WDXyTo
        }

    }
}
