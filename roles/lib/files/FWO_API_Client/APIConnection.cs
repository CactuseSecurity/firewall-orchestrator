using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using FWO_Logging;
using GraphQL;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.SystemTextJson;

namespace FWO.Api.Client
{
    public class APIConnection
    {
        // Server URL
        private readonly string APIServerURI;

        private readonly GraphQLHttpClient Client;

        public APIConnection(string APIServerURI)
        {
            // Save Server URI
            this.APIServerURI = APIServerURI;

            // Allow all certificates | TODO: REMOVE IF SERVER GOT VALID CERTIFICATE
            HttpClientHandler Handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => { return true; }
            };

            Client = new GraphQLHttpClient(new GraphQLHttpClientOptions()
            {
                EndPoint = new Uri(APIServerURI),
                HttpMessageHandler = Handler,
            }, new SystemTextJsonSerializer());
        }

        public void SetAuthHeader(string jwt)
        {
            Client.HttpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwt); // Change jwt in auth header
        }

        public async Task<QueryResponseType[]> SendQuery<QueryResponseType>(string Query, string Variables = null, string OperationName = null)
        {
            try
            {
                GraphQLRequest request = new GraphQLRequest(Query, Variables, OperationName);
                GraphQLResponse<dynamic> response = await Client.SendQueryAsync<dynamic>(request);
                //GraphQLResponse<HasuraResponse<QueryResponseType>> response = await Client.SendQueryAsync<HasuraResponse<QueryResponseType>>(request);

                if (response.Errors != null)
                {
                    string errorMessage = "";

                    foreach (GraphQLError error in response.Errors)
                    {
                        // TODO: handle graphql errors
                        Log.WriteError("API Connection", $"Error while sending query to GraphQL API. Caught by GraphQL client library. \nMessage: {error.Message}");
                        errorMessage += $"{error.Message}\n";
                    }

                    throw new Exception(errorMessage);
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

            catch (Exception exception)
            {
                // TODO: handle unexpected errors
                Log.WriteError("API Connection", "Error while sending query to GraphQL API.", exception);
                throw exception;
            }
        }
    }
}
