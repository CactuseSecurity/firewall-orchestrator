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
using FWO.Backend.Data.API;

namespace FWO
{
    public class APIConnection
    {
        // Server URL
        private readonly string ServerURI;

        // Http/s Client
        private readonly HttpClient Client;

        //private readonly GraphQLHttpClient Client;

        //public APIConnection()
        //{
        //    //Allow all certificates | REMOVE IF SERVER GOT VALID CERTIFICATE
        //    HttpClientHandler Handler = new HttpClientHandler();
        //    Handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => { return true; };

        //    Client = new GraphQLHttpClient(new GraphQLHttpClientOptions()
        //    {
        //        EndPoint = new Uri(ServerURI),
        //        HttpMessageHandler = Handler,
        //    }, new SystemTextJsonSerializer());

        //    //Client.HttpClient. // DefaultRequestHeaders.Add("content-type", "application/json");
        //    //Client.HttpClient.DefaultRequestHeaders
        //    //Client.HttpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue( //.Add("Authorization", "st8chelt1er");
        //}

        //public async Task<T[]> SendQuery<T>(string Query, string Variables = "", string OperationName = "")
        //{
        //    GraphQLRequest request = new GraphQLRequest(Query, Variables, OperationName);
        //    GraphQLResponse<T[]> response = await Client.SendQueryAsync<T[]>(request);  

        //    if (response.Errors.Length > 0)
        //    {
        //        //Todo: Handle Errors

        //        foreach (GraphQLError error in response.Errors)
        //        {
        //            Console.WriteLine(error.Message);
        //        }
        //    }

        //    else
        //    {
        //        return response.Data;
        //    }

        //    return null;
        //}

        public APIConnection(string ServerURI)
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
