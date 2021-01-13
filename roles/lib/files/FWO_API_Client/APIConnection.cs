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
using System.Net.WebSockets;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;

namespace FWO.ApiClient
{
    public class APIConnection
    {
        // Server URL
        public readonly string APIServerURI;

        private readonly GraphQLHttpClient graphQlClient;

        private string jwt;

        public APIConnection(string APIServerURI, string jwt = null)
        {
            // Save Server URI
            this.APIServerURI = APIServerURI;
            this.jwt = jwt;

            // Allow all certificates | TODO: REMOVE IF SERVER GOT VALID CERTIFICATE
            HttpClientHandler Handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
            };

            graphQlClient = new GraphQLHttpClient(new GraphQLHttpClientOptions()
            {
                EndPoint = new Uri(APIServerURI),
                HttpMessageHandler = Handler,
                UseWebSocketForQueriesAndMutations = false,
                ConfigureWebsocketOptions = webSocketOptions => webSocketOptions.RemoteCertificateValidationCallback += (message, cert, chain, errors) => true
            }, new SystemTextJsonSerializer());

            if (jwt != null)
            {
                SetAuthHeader(jwt);
            }
        }

        public void SetAuthHeader(string jwt)
        {
            this.jwt = jwt;
            graphQlClient.HttpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwt); // Change jwt in auth header
            graphQlClient.Options.ConfigureWebSocketConnectionInitPayload = httpClientOptions => new { headers = new { authorization = $"Bearer {jwt}" } };
        }

        /// <summary>
        /// Sends an APICall (query, mutation)
        /// NB: SendQueryAsync always returns an array of objects (even if the result is a single element)
        ///     so QueryResponseType always needs to be an array
        /// </summary>
        /// <param name="query"></param>
        /// <param name="variables"></param>
        /// <param name="operationName"></param>
        /// <returns><typeparamref name="QueryResponseType"/></returns>

        public async Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object variables = null, string operationName = null)
        {
            try
            {
                // Log.WriteDebug("API Response", $"API Call variables: { variables }");
                Log.WriteDebug("API call", $"Sending API call {operationName}.");
                GraphQLResponse<dynamic> response = await graphQlClient.SendQueryAsync<dynamic>(query, variables, operationName);
                Log.WriteDebug("API call", "API call received.");

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
                    string JsonResponse = JsonSerializer.Serialize(response, new JsonSerializerOptions {});
                    // string JsonResponse = JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
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
                throw;
            }
        }

        public ApiSubscription<SubscriptionResponseType> GetSubscription<SubscriptionResponseType>(Action<Exception> exceptionHandler, string subscription, object variables = null, string operationName = null)
        {
            try
            {
                GraphQLRequest request = new GraphQLRequest(subscription, variables, operationName);

                Log.WriteDebug("API", $"Creating API subscription {operationName}.");
                IObservable<GraphQLResponse<dynamic>> subscriptionStream = graphQlClient.CreateSubscriptionStream<dynamic>(request, exceptionHandler);
                Log.WriteDebug("API", "API subscription created.");

                return new ApiSubscription<SubscriptionResponseType>(subscriptionStream);
            }

            catch (Exception exception)
            {
                Log.WriteError("API Connection", "Error while creating subscription to GraphQL API.", exception);
                throw;
            }
        }
    }
}
