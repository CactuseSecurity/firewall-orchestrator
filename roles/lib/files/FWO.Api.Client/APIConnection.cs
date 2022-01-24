using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using FWO.Logging;
using GraphQL;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.SystemTextJson;
using GraphQL.Client.Serializer.Newtonsoft;
using GraphQL.Client.Abstractions;
using System.Linq;
using System.Net.WebSockets;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;
using Newtonsoft.Json.Linq;
using FWO.Api.Client;

namespace FWO.ApiClient
{
    public class APIConnection
    {
        // Server URL
        public readonly string APIServerURI;

        private readonly GraphQLHttpClient graphQlClient;

        private string? jwt;

        public APIConnection(string APIServerURI, string? jwt = null)
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
                UseWebSocketForQueriesAndMutations = false, // TODO: Use websockets for performance reasons          
                ConfigureWebsocketOptions = webSocketOptions => webSocketOptions.RemoteCertificateValidationCallback += (message, cert, chain, errors) => true
            }, ApiConstants.UseSystemTextJsonSerializer ? new SystemTextJsonSerializer() : new NewtonsoftJsonSerializer());

            // 1 hour timeout
            graphQlClient.HttpClient.Timeout = new TimeSpan(1, 0, 0);

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

        public async Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null)
        {
            try
            {
                // Log.WriteDebug("API Response", $"API Call variables: { variables }");

                //Log.WriteDebug("API call", $"Sending API call {operationName}: {query}");

                // if (variables == null)
                // {
                //     Log.WriteDebug("API variables", $"no variables set");
                // }
                // else
                // {
                //     Log.WriteDebug("API variables", $"Sending the following variables:");

                //     foreach (var propertyInfo in variables?.GetType()?.GetProperties())
                //     {
                //         if (propertyInfo!=null)
                //         {
                //             var propertyName = propertyInfo.Name;
                //             var propertyValue = propertyInfo.GetValue(variables);
                //             if (propertyName!=null && propertyValue!=null)
                //                 Log.WriteDebug("API variables", $"var {propertyName} = {propertyValue}");
                //         }
                //     }
                // }
                // Dictionary<string, object> items =  (Dictionary<string, object>) variables;
                GraphQLResponse<dynamic> response = await graphQlClient.SendQueryAsync<dynamic>(query, variables, operationName);
                // Log.WriteDebug("API call", "API response received.");

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
                    // // string JsonResponse = JsonSerializer.Serialize(response, new JsonSerializerOptions { });
                    // string JsonResponse = JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
                    // Log.WriteDebug("API Response", $"API response: { JsonResponse }");

                    if (ApiConstants.UseSystemTextJsonSerializer)
                    {
                        JsonElement.ObjectEnumerator responseObjectEnumerator = response.Data.EnumerateObject();
                        responseObjectEnumerator.MoveNext();
                        QueryResponseType returnValue = JsonSerializer.Deserialize<QueryResponseType>(responseObjectEnumerator.Current.Value.GetRawText()) ??
                        throw new Exception($"Could not convert result from Json to {typeof(QueryResponseType)}.\nJson: {responseObjectEnumerator.Current.Value.GetRawText()}");
                        return returnValue;
                    }
                    else
                    {
                        JObject data = (JObject)response.Data;
                        JProperty prop = (JProperty)(data.First ?? throw new Exception($"Could not retrieve unique result attribute from Json.\nJson: {response.Data}"));
                        JToken result = prop.Value;
                        QueryResponseType returnValue = result.ToObject<QueryResponseType>() ??
                        throw new Exception($"Could not convert result from Json to {typeof(QueryResponseType)}.\nJson: {response.Data}");
                        return returnValue;
                    }
                }
            }

            catch (Exception exception)
            {
                Log.WriteError("API Connection", $"Error while sending query to GraphQL API. Query: {(query != null ? query : "")}", exception);
                Log.WriteError("API variables", $"Sending the following variables:", null);

                //foreach (var propertyInfo in variables?.GetType()?.GetProperties())
                //{
                //    var propertyName = propertyInfo.Name;
                //    var propertyValue = propertyInfo.GetValue(variables);
                //    Log.WriteError("API variables", $"var {propertyName} = {propertyValue}", null);

                //}
                // todo: #1220 add variables readable
                throw;
            }
        }

        public ApiSubscription<SubscriptionResponseType> GetSubscription<SubscriptionResponseType>(Action<Exception> exceptionHandler, ApiSubscription<SubscriptionResponseType>.SubscriptionUpdate subscriptionUpdateHandler, string subscription, object? variables = null, string? operationName = null)
        {
            try
            {
                GraphQLRequest request = new GraphQLRequest(subscription, variables, operationName);

                Log.WriteDebug("API", $"Creating API subscription {operationName}.");
                IObservable<GraphQLResponse<dynamic>> subscriptionStream = graphQlClient.CreateSubscriptionStream<dynamic>(request, exceptionHandler);
                Log.WriteDebug("API", "API subscription created.");

                return new ApiSubscription<SubscriptionResponseType>(subscriptionStream, subscriptionUpdateHandler);
            }

            catch (Exception exception)
            {
                Log.WriteError("API Connection", "Error while creating subscription to GraphQL API.", exception);
                throw;
            }
        }
    }
}
