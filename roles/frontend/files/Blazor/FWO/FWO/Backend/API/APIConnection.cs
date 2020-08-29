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
using FWO.Backend.Auth;
using GraphQL.Client.Abstractions;
using System.Text.Json.Serialization;

namespace FWO
{
    public class APIConnection
    {
        // Server URL
        private readonly string APIServerURI;

        private readonly GraphQLHttpClient Client;

        public string Jwt { get; set; }

        public APIConnection(string APIServerURI)
        {
            // Save Server URI
            this.APIServerURI = APIServerURI;

            // Allow all certificates | TODO: REMOVE IF SERVER GOT VALID CERTIFICATE
            HttpClientHandler Handler = new HttpClientHandler();
            Handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => { return true; };

            Client = new GraphQLHttpClient(new GraphQLHttpClientOptions()
            {
                EndPoint = new Uri(APIServerURI),
                HttpMessageHandler = Handler,
            }, new SystemTextJsonSerializer());
        }

        public void ChangeAuthHeader(string Jwt)
        {
            this.Jwt = Jwt;
            Client.HttpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", Jwt);
        }

        public async Task<QueryResponseType[]> SendQuery<QueryResponseType>(string Query, string Variables = null, string OperationName = null)
        {
            GraphQLRequest request = new GraphQLRequest(Query, Variables, OperationName);          
            GraphQLResponse<dynamic> response = await Client.SendQueryAsync<dynamic>(request);            

            if (response.Errors != null)
            {
                //Todo: Handle Errors

                foreach (GraphQLError error in response.Errors)
                {
                    Console.WriteLine(error.Message);
                }

                // TODO: handle graphql errors
                throw new Exception("");
            }

            else
            {
                string JsonResponse = JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });

                JsonElement.ObjectEnumerator responseObjectEnumerator = response.Data.EnumerateObject();
                responseObjectEnumerator.MoveNext();

                QueryResponseType[] result = JsonSerializer.Deserialize<QueryResponseType[]>(responseObjectEnumerator.Current.Value.GetRawText());

                return result;
            }
        }
    }
}
