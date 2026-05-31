using System.Text.Json;
using GraphQL;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.SystemTextJson;
using GraphQL.Client.Serializer.Newtonsoft;
using GraphQL.Client.Abstractions;
using Newtonsoft.Json.Linq;
using FWO.Logging;
using System.Security.Claims;
using System.Security.Authentication;
using System.IdentityModel.Tokens.Jwt;

namespace FWO.Api.Client
{
    public class GraphQlApiConnection : ApiConnection
    {
        private const string LogCategory = "API Connections";
        // Server URL
        public string ApiServerUri { get; private set; } = "";

        private GraphQLHttpClient graphQlClient = null!;

        private readonly AsyncLocal<List<string>> roleStack = new();
        private string defaultRole = "";

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
            graphQlClient.HttpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwt); // Change jwt in auth header
            graphQlClient.Options.ConfigureWebSocketConnectionInitPayload = httpClientOptions => new { headers = new { authorization = $"Bearer {jwt}" } };
            defaultRole = GetDefaultRoleFromJwt(jwt);
            InvokeOnAuthHeaderChanged(this, jwt);
        }

        public override void SetRole(string role)
        {
            PushRole(role);
        }

        public bool IsActRole(string role)
        {
            return role == GetActRole();
        }

        public string GetActRole()
        {
            List<string>? roles = roleStack.Value;
            return roles == null || roles.Count == 0 ? defaultRole : roles[^1];
        }

        public override void SetBestRole(System.Security.Claims.ClaimsPrincipal user, List<string> targetRoleList)
        {
            if (!TryGetFirstAllowedRole(user, targetRoleList, out string role))
            {
                throw new AuthenticationException($"User has none of the required roles: {string.Join(", ", targetRoleList)}");
            }
            PushRole(role);
        }

        public override void SetProperRole(System.Security.Claims.ClaimsPrincipal user, List<string> targetRoleList)
        {
            string actRole = GetActRole();

            // first look if user is already in one of the target roles 
            if (targetRoleList.Contains(actRole))
            {
                PushRole(actRole);
                return;
            }
            // now look if user has a target role as allowed role
            if (!TryGetFirstAllowedRole(user, targetRoleList, out string role))
            {
                throw new AuthenticationException($"User has none of the required roles: {string.Join(", ", targetRoleList)}");
            }
            PushRole(role);
        }

        private static bool TryGetFirstAllowedRole(ClaimsPrincipal user, List<string> targetRoleList, out string role)
        {
            foreach (string targetRole in targetRoleList)
            {
                if (HasAllowedRole(user, targetRole))
                {
                    role = targetRole;
                    return true;
                }
            }
            role = "";
            return false;
        }

        public override void SwitchBack()
        {
            List<string>? roles = roleStack.Value;
            if (roles == null || roles.Count == 0)
            {
                return;
            }

            List<string> newRoles = [.. roles];
            newRoles.RemoveAt(newRoles.Count - 1);
            roleStack.Value = newRoles;
        }

        private void PushRole(string role)
        {
            List<string>? roles = roleStack.Value;
            List<string> newRoles = roles == null ? [] : [.. roles];
            newRoles.Add(role);
            roleStack.Value = newRoles;
        }

        private static bool HasAllowedRole(ClaimsPrincipal user, string role)
        {
            if (user.IsInRole(role))
            {
                return true;
            }

            foreach (Claim claim in user.Claims.Where(currentClaim => IsHasuraAllowedRolesClaim(currentClaim.Type)))
            {
                if (claim.Value == role)
                {
                    return true;
                }

                if (TryParseAllowedRoles(claim.Value, out List<string> parsedRoles)
                    && parsedRoles.Contains(role, StringComparer.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsHasuraAllowedRolesClaim(string claimType)
        {
            if (claimType.Equals("x-hasura-allowed-roles", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return claimType.EndsWith("/x-hasura-allowed-roles", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryParseAllowedRoles(string claimValue, out List<string> parsedRoles)
        {
            parsedRoles = [];
            if (string.IsNullOrWhiteSpace(claimValue))
            {
                return false;
            }

            try
            {
                string[]? roleArray = JsonSerializer.Deserialize<string[]>(claimValue);
                if (roleArray == null)
                {
                    return false;
                }
                parsedRoles = roleArray.Where(role => !string.IsNullOrWhiteSpace(role)).ToList();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string GetDefaultRoleFromJwt(string jwt)
        {
            try
            {
                JwtSecurityToken token = new JwtSecurityTokenHandler().ReadJwtToken(jwt);
                return token.Claims.FirstOrDefault(claim => claim.Type == "x-hasura-default-role")?.Value ?? "";
            }
            catch
            {
                return "";
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
                    (variables != null ? "with variables: <redacted>" : ""));
                GraphQLResponse<dynamic> response = await graphQlClient.SendQueryAsync<dynamic>(CreateHttpRequest(query, variables, operationName));
                // Log.WriteDebug("API call", "API response received.");

                if (response.Errors != null)
                {
                    string errorMessage = "";

                    foreach (GraphQLError error in response.Errors)
                    {
                        Log.WriteError(LogCategory, $"Error while sending query to GraphQL API. Caught by GraphQL client library. \nMessage: {error.Message}");
                        errorMessage += $"{error.Message}\n";
                    }

                    throw new InvalidOperationException(errorMessage);
                }

                if (ApiConstants.UseSystemTextJsonSerializer)
                {
                    throw new NotImplementedException("System.Text.Json is not supported anymore.");
                }

                JObject data = (JObject)response.Data;
                JProperty prop = (JProperty)(data.First ?? throw new InvalidOperationException($"Could not retrieve unique result attribute from Json.\nJson: {response.Data}"));
                JToken result = prop.Value;
                QueryResponseType returnValue = result.ToObject<QueryResponseType>() ??
                    throw new InvalidOperationException($"Could not convert result from Json to {typeof(QueryResponseType)}.\nJson: {response.Data}");
                return returnValue;
            }

            catch (Exception exception)
            {
                Log.WriteError(LogCategory, $"Error while sending query to GraphQL API. Query: {query}, variables: {(variables != null ? "<redacted>" : "")}", exception);
                throw;
            }
        }

        /// <summary>
        /// Sends an API call and returns a non-throwing response wrapper containing data or errors.
        /// </summary>
        public override async Task<ApiResponse<QueryResponseType>> SendQuerySafeAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null)
        {
            try
            {
                Log.WriteDebug("API call", $"Sending API call {operationName} in role {GetActRole()}: {query.Substring(0, Math.Min(query.Length, 70)).Replace(Environment.NewLine, "")}... " +
                    (variables != null ? "with variables: <redacted>" : ""));
                GraphQLResponse<dynamic> response = await graphQlClient.SendQueryAsync<dynamic>(CreateHttpRequest(query, variables, operationName));

                if (response.Errors != null)
                {
                    List<string> errorMessages = response.Errors.Select(error =>
                    {
                        Log.WriteError(LogCategory, $"Error while sending query to GraphQL API. Caught by GraphQL client library. \nMessage: {error.Message}");
                        return error.Message;
                    }).ToList();
                    return new ApiResponse<QueryResponseType>(errorMessages.ToArray());
                }

                if (ApiConstants.UseSystemTextJsonSerializer)
                {
                    throw new NotImplementedException("System.Text.Json is not supported anymore.");
                }

                JObject data = (JObject)response.Data;
                JProperty prop = (JProperty)(data.First ?? throw new InvalidOperationException($"Could not retrieve unique result attribute from Json.\nJson: {response.Data}"));
                JToken result = prop.Value;
                QueryResponseType returnValue = result.ToObject<QueryResponseType>() ??
                    throw new InvalidOperationException($"Could not convert result from Json to {typeof(QueryResponseType)}.\nJson: {response.Data}");
                return new ApiResponse<QueryResponseType>(returnValue);
            }
            catch (Exception exception)
            {
                Log.WriteError(LogCategory, $"Error while sending query to GraphQL API. Query: {query}, variables: {(variables != null ? "<redacted>" : "")}", exception);
                return new ApiResponse<QueryResponseType>(exception.Message);
            }
        }

        public override GraphQlApiSubscription<SubscriptionResponseType> GetSubscription<SubscriptionResponseType>(Action<Exception> exceptionHandler, GraphQlApiSubscription<SubscriptionResponseType>.SubscriptionUpdate subscriptionUpdateHandler, string subscription, object? variables = null, string? operationName = null)
        {
            try
            {
                GraphQLRequest request = new(subscription, variables, operationName);
                GraphQlApiSubscription<SubscriptionResponseType> newSub =
                    new(this, graphQlClient, request, exceptionHandler, subscriptionUpdateHandler);
                subscriptions.Add(newSub);

                return newSub;
            }
            catch (Exception exception)
            {
                Log.WriteError(LogCategory, "Error while creating subscription to GraphQL API.", exception);
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

        public override void DisposeSubscriptions<T>()
        {
            foreach (ApiSubscription subscription in subscriptions.Where(_ => _.GetType() == typeof(T)))
            {
                subscription.Dispose();
            }

            subscriptions.RemoveAll(_ => _.GetType() == typeof(T));
        }

        private GraphQLHttpRequest CreateHttpRequest(string query, object? variables, string? operationName)
        {
            return new RoleGraphQLHttpRequest(GetActRole(), query, variables, operationName);
        }

        private sealed class RoleGraphQLHttpRequest(string role, string query, object? variables = null, string? operationName = null)
            : GraphQLHttpRequest(query, variables, operationName)
        {
            public override HttpRequestMessage ToHttpRequestMessage(GraphQLHttpClientOptions options, IGraphQLJsonSerializer serializer)
            {
                HttpRequestMessage request = base.ToHttpRequestMessage(options, serializer);
                if (!string.IsNullOrWhiteSpace(role))
                {
                    request.Headers.Remove("x-hasura-role");
                    request.Headers.Add("x-hasura-role", role);
                }
                return request;
            }
        }
    }
}
