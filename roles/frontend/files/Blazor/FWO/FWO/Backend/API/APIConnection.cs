//using Newtonsoft.Json.Linq;
using System;
//using System.Collections.Generic;
using System.Diagnostics;
//using System.Linq;
using System.Net.Http;
using System.Text;
//using System.Text.Json;
using System.Threading.Tasks;
//using GraphQL;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.SystemTextJson;
//using Microsoft.AspNetCore.Mvc.Formatters;

namespace FWO
{
    public class APIConnection
    {
        // Server URL
        private const string ServerURI = "https://demo.itsecorg.de/api/v1/graphql";

        // Http/s Client
        private readonly GraphQLHttpClient Client;

        public APIConnection()
        {
            // Allow all certificates // REMOVE IF SERVER GOT VALID CERTIFICATE
            //ServicePointManager.ServerCertificateValidationCallback += (sender, cert, chain, sslPolicyErrors) => true;

            // New http/s client
            Client = new GraphQLHttpClient(ServerURI, new SystemTextJsonSerializer());
        }

        //Query is structured as follow: { "query" : " 'query' ", "variables" : { 'variables' } } with 'query' as query to send and 'variables' as corresponding variables
        public async Task<string> SendQuery(string Query, object Variables = null, string OperationName = "")
        {
            //int a = 0;
            //int b = 0;

            //new GraphQLRequest("test", a, b, "a");
            //Client.SendQueryAsync(new GraphQLRequest(Query, , ))

            // New http-body containing the query
            StringContent content = new StringContent(Query, Encoding.UTF8);
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
            //HttpResponseMessage response;
            //string responseString;

            try
            {
                // Send http-packet with query and header. Receive answer
                //response = await Client.PostAsync(ServerURI, content);
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
                //responseString = await response.Content.ReadAsStringAsync();
            }
            catch (Exception e)
            {
                return "";
                //TODO: Answer can not be converted to string
            }

            // Return answer
            return "";//responseString;

            //TODO: https://www.youtube.com/watch?v=4XlA2WDXyTo
        }
    }
}
