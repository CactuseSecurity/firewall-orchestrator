using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using GraphQL;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.SystemTextJson;

namespace FWO.Api
{
    public class APIConnection
    {
        // Server URL
        private readonly string APIServerURI;

        private readonly GraphQLHttpClient Client;

        public string Jwt
        {
            set
            {
                Jwt = value; // Save jwt for debug purpose
                Client.HttpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", value); // Change jwt in auth header
            }
        }

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
            try
            {
                GraphQLRequest request = new GraphQLRequest(Query, Variables, OperationName);
                GraphQLResponse<dynamic> response = await Client.SendQueryAsync<dynamic>(request);

                if (response.Errors != null)
                {
                    foreach (GraphQLError error in response.Errors)
                    {
                        // TODO: handle graphql errors
                        Console.WriteLine(error.Message);
                    }
                    
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

            catch (Exception e)
            {
                // TODO: handle unexpected errors
                Console.WriteLine(e.Message);
                throw e;
            }
        }
    }
}
