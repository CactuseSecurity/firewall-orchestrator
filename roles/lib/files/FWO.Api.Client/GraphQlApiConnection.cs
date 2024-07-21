﻿using System.Text.Json;
using GraphQL;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.SystemTextJson;
using GraphQL.Client.Serializer.Newtonsoft;
using GraphQL.Client.Abstractions;
using Newtonsoft.Json.Linq;
using FWO.Logging;

namespace FWO.Api.Client
{
    public class GraphQlApiConnection : ApiConnection
    {
        // Server URL
        public string ApiServerUri { get; private set; }

        private GraphQLHttpClient graphQlClient;

        private string? jwt;
        private string prevRole = "";

        private void Initialize(string ApiServerUri)
        {
            // Save Server URI
            this.ApiServerUri = ApiServerUri;

            // Allow all certificates | TODO: REMOVE IF SERVER GOT VALID CERTIFICATE
            HttpClientHandler Handler = new()
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
            };

            graphQlClient = new GraphQLHttpClient(new GraphQLHttpClientOptions()
            {
                EndPoint = new Uri(this.ApiServerUri),
                HttpMessageHandler = Handler, 
                UseWebSocketForQueriesAndMutations = false, // TODO: Use websockets for performance reasons          
                ConfigureWebsocketOptions = webSocketOptions => webSocketOptions.RemoteCertificateValidationCallback += (message, cert, chain, errors) => true
            }, ApiConstants.UseSystemTextJsonSerializer ? new SystemTextJsonSerializer() : new NewtonsoftJsonSerializer());

            // 1 hour timeout
            graphQlClient.HttpClient.Timeout = new TimeSpan(1, 0, 0);
        }

        public GraphQlApiConnection(string ApiServerUri, string jwt)
        {
            Initialize(ApiServerUri);
            SetAuthHeader(jwt);
        }

        public GraphQlApiConnection(string ApiServerUri)
        {
            Initialize(ApiServerUri);
        }

        public override void SetAuthHeader(string jwt)
        {
            this.jwt = jwt;
            graphQlClient.HttpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwt); // Change jwt in auth header
            graphQlClient.Options.ConfigureWebSocketConnectionInitPayload = httpClientOptions => new { headers = new { authorization = $"Bearer {jwt}" } };
            InvokeOnAuthHeaderChanged(this, jwt);
        }

        public override void SetRole(string role)
        {
            prevRole = GetActRole();
            graphQlClient.HttpClient.DefaultRequestHeaders.Remove("x-hasura-role");
            graphQlClient.HttpClient.DefaultRequestHeaders.Add("x-hasura-role", role);
        }

        public string GetActRole()
        {
            if(graphQlClient.HttpClient.DefaultRequestHeaders.TryGetValues("x-hasura-role", out IEnumerable<string>? roles))
            {
                if(roles.Count() > 1)
                {
                    Log.WriteDebug("API call", $"More than one role in x-hasura-role: {roles}");
                }
                return roles.First();
            }
            return "";
        }

        public override void SetProperRole(System.Security.Claims.ClaimsPrincipal user, List<string> targetRoleList)
        {
            prevRole = GetActRole();

            // first look if user is already in one of the target roles 
            foreach(string role in targetRoleList)
            {
                if (user.IsInRole(role))
                {
                    SetRole(role);
                    return;
                }
            }
            // now look if user has a target role as allowed role
            foreach(string role in targetRoleList)
            {
                if(user.Claims.FirstOrDefault(claim => claim.Type == "x-hasura-allowed-roles" && claim.Value == role) != null)
                {
                    SetRole(role);
                    return;
                }
            }
        }

        public override void SwitchBack()
        {
            if(prevRole != "")
            {
                SetRole(prevRole);
            }
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
        public override async Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null)
        {
            try
            {
                Log.WriteDebug("API call", $"Sending API call {operationName} in role {GetActRole()}: {query.Substring(0, Math.Min(query.Length, 70)).Replace(Environment.NewLine, "")}... " +
                    (variables != null ? $"with variables: { JsonSerializer.Serialize(variables).Substring(0, Math.Min(JsonSerializer.Serialize(variables).Length, 50)).Replace(Environment.NewLine, "")}..." : ""));
                GraphQLResponse<dynamic> response = await graphQlClient.SendQueryAsync<dynamic>(query, variables, operationName);
                // Log.WriteDebug("API call", "API response received.");

                if (response.Errors != null)
                {
                    string errorMessage = "";

                    foreach (GraphQLError error in response.Errors)
                    {
                        if (error.Message == "Jwt expired.")
                        {
                            // JwtEventService
                        }

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
                Log.WriteError("API Connection", $"Error while sending query to GraphQL API. Query: {(query != null ? query : "")}, variables: {(variables != null ? JsonSerializer.Serialize(variables) : "")}", exception);
                throw;
            }
        }

        public override GraphQlApiSubscription<SubscriptionResponseType> GetSubscription<SubscriptionResponseType>(Action<Exception> exceptionHandler, GraphQlApiSubscription<SubscriptionResponseType>.SubscriptionUpdate subscriptionUpdateHandler, string subscription, object? variables = null, string? operationName = null)
        {
            try
            {
                GraphQLRequest request = new GraphQLRequest(subscription, variables, operationName);
                GraphQlApiSubscription<SubscriptionResponseType> newSub = 
                    new GraphQlApiSubscription<SubscriptionResponseType>(this, graphQlClient, request, exceptionHandler, subscriptionUpdateHandler);
                subscriptions.Add(newSub);
                return newSub;
            }
            catch (Exception exception)
            {
                Log.WriteError("API Connection", "Error while creating subscription to GraphQL API.", exception);
                throw;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                graphQlClient.Dispose();
                foreach (ApiSubscription subscription in subscriptions)
                {
                    subscription.Dispose();
                }
            }
        }
    }
}
