using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using FWO.Logging;
using GraphQL;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.SystemTextJson;
using GraphQL.Client.Abstractions;
using System.Linq;

namespace FWO.ApiClient
{
    public class APIConnection
    {
        // Server URL
        private readonly string APIServerURI;

        private readonly GraphQLHttpClient Client;

        public APIConnection(string APIServerURI, string jwt = null)
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

            if (jwt != null)
            {
                SetAuthHeader(jwt);
            }
        }

        public void SetAuthHeader(string jwt)
        {
            Client.HttpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwt); // Change jwt in auth header
        }

        /// <summary>
        /// Sends an APICall (query, mutation)
        /// NB: SendQueryAsync always returns an array of objects (even if the result is a single element)
        ///     so QueryResponseType always needs to be an array
        /// </summary>
        /// <param name="query"></param>
        /// <param name="variables"></param>
        /// <param name="operationName"></param>
        /// <returns>object of specified type</returns>

        public async Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object variables = null, string operationName = null)
        {
            try
            {
                // Log.WriteDebug("API Response", $"API Call variables: { variables }");
                GraphQLResponse<dynamic> response = await Client.SendQueryAsync<dynamic>(query, variables, operationName);

                if (response.Errors != null)
                {
                    string errorMessage = "";

                    foreach (GraphQLError error in response.Errors)
                    {
                        Log.WriteError("API Connection", $"Error while sending query to GraphQL API. Caught by GraphQL client library. \nMessage: {error.Message}");
                        errorMessage += $"{error.Message}\n";
                    }

                    throw new Exception(errorMessage);
                }
                else
                {
                    // DEBUG
                    string JsonResponse = JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
                    // Log.WriteDebug("API Response", $"API response: { JsonResponse }");

                    JsonElement.ObjectEnumerator responseObjectEnumerator = response.Data.EnumerateObject();
                    responseObjectEnumerator.MoveNext();
                    QueryResponseType returnValue = JsonSerializer.Deserialize<QueryResponseType>(responseObjectEnumerator.Current.Value.GetRawText());
                    return returnValue;
                }
            }

            catch (Exception exception)
            {
                Log.WriteError("API Connection", "Error while sending query to GraphQL API.", exception);
                throw exception;
            }
        }
    }
}
