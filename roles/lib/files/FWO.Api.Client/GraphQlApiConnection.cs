using GraphQL;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.SystemTextJson;
using GraphQL.Client.Serializer.Newtonsoft;
using GraphQL.Client.Abstractions;
using Newtonsoft.Json.Linq;
using FWO.Basics;
using FWO.Logging;
using System.Security.Claims;
using System.Security.Authentication;
using System.Text.Json;

namespace FWO.Api.Client
{
    public class GraphQlApiConnection : ApiConnection
    {
        private const string LogCategory = "API Connections";
        // Server URL
        public string ApiServerUri { get; private set; } = "";

        private GraphQLHttpClient graphQlClient = null!;

        private readonly Stack<string> previousRoles = new();
        private string forcedExecutionMode = "";
        private bool restrictElevatedRoleSwitches = false;

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
            InvokeOnAuthHeaderChanged(this, jwt);
        }

        public override void SetRole(string role)
        {
            if (restrictElevatedRoleSwitches && IsForcedExecutionMode(role))
            {
                throw new AuthenticationException($"Execution mode '{GlobalConst.kUserRolesSelection}' does not allow switching to role: {role}");
            }

            previousRoles.Push(GetActRole());
            SetRoleHeader(IsForcedExecutionMode(forcedExecutionMode) ? forcedExecutionMode : role);
        }

        private void ApplyExecutionMode(string role, bool restrictElevatedRoles)
        {
            forcedExecutionMode = IsForcedExecutionMode(role) ? role : "";
            restrictElevatedRoleSwitches = restrictElevatedRoles;
            SetRoleHeader(role);
        }

        public override void SetExecutionMode(ClaimsPrincipal user, string role)
        {
            if (IsForcedExecutionMode(role) && !HasAllowedRole(user, role))
            {
                throw new AuthenticationException($"User is not allowed to use execution mode: {role}");
            }

            List<string> userRoles = ExecutionModeHelper.GetUserRoles(user);
            string selectedExecutionMode = ExecutionModeHelper.NormalizeExecutionMode(userRoles, role);
            string normalizedRole = selectedExecutionMode.Equals(GlobalConst.kUserRolesSelection, StringComparison.OrdinalIgnoreCase) ? "" : selectedExecutionMode;
            ApplyExecutionMode(normalizedRole, normalizedRole == "" && HasSelectableUserRole(user));
            InvokeOnExecutionModeChanged(this, GetExecutionMode());
        }

        public override string GetExecutionMode()
        {
            return forcedExecutionMode == "" ? GlobalConst.kUserRolesSelection : forcedExecutionMode;
        }

        private void SetRoleHeader(string role)
        {
            graphQlClient.HttpClient.DefaultRequestHeaders.Remove("x-hasura-role");
            if (role != "")
            {
                graphQlClient.HttpClient.DefaultRequestHeaders.Add("x-hasura-role", role);
            }
        }

        public bool IsActRole(string role)
        {
            return role == GetActRole();
        }

        public string GetActRole()
        {
            if (graphQlClient.HttpClient.DefaultRequestHeaders.TryGetValues("x-hasura-role", out IEnumerable<string>? roles))
            {
                if (roles.Count() > 1)
                {
                    Log.WriteDebug("API call", $"More than one role in x-hasura-role: {roles}");
                }
                return roles.First();
            }
            return "";
        }

        public override void SetBestRole(System.Security.Claims.ClaimsPrincipal user, List<string> targetRoleList)
        {
            string actRole = GetActRole();
            bool includeElevatedRoles = !HasSelectableUserRole(user);
            string targetRole = IsForcedExecutionMode(user)
                ? forcedExecutionMode
                : GetFirstAllowedRole(user, targetRoleList, includeElevatedRoles)
                    ?? throw new AuthenticationException($"User has none of the required roles: {string.Join(", ", targetRoleList)}");
            previousRoles.Push(actRole);
            SetRoleHeader(targetRole);
        }

        public override void SetProperRole(System.Security.Claims.ClaimsPrincipal user, List<string> targetRoleList)
        {
            string actRole = GetActRole();
            string? targetRole = null;
            bool includeElevatedRoles = !HasSelectableUserRole(user);

            if (IsForcedExecutionMode(user))
            {
                targetRole = forcedExecutionMode;
            }
            else if ((includeElevatedRoles || !IsForcedExecutionMode(actRole)) && targetRoleList.Contains(actRole))
            {
                targetRole = actRole;
            }
            else
            {
                targetRole = GetFirstAllowedRole(user, targetRoleList, includeElevatedRoles);
            }

            if (targetRole == null)
            {
                throw new AuthenticationException($"User has none of the required roles: {string.Join(", ", targetRoleList)}");
            }

            previousRoles.Push(actRole);
            SetRoleHeader(targetRole);
        }

        private static string? GetFirstAllowedRole(ClaimsPrincipal user, List<string> targetRoleList, bool includeElevatedRoles)
        {
            foreach (string role in targetRoleList)
            {
                if ((includeElevatedRoles || !IsForcedExecutionMode(role)) && HasAllowedRole(user, role))
                {
                    return role;
                }
            }
            return null;
        }

        private bool IsForcedExecutionMode(ClaimsPrincipal user)
        {
            return IsForcedExecutionMode(forcedExecutionMode) && HasAllowedRole(user, forcedExecutionMode);
        }

        private static bool IsForcedExecutionMode(string role)
        {
            return role.Equals(FWO.Basics.Roles.Admin, StringComparison.OrdinalIgnoreCase)
                || role.Equals(FWO.Basics.Roles.Auditor, StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasSelectableUserRole(ClaimsPrincipal user)
        {
            return ExecutionModeHelper.GetUserRoles(user).Any(role => !IsForcedExecutionMode(role) && !FWO.Basics.RoleGroups.IsTechnicalOrAnonymous(role));
        }

        public override void SwitchBack()
        {
            if (previousRoles.TryPop(out string? previousRole))
            {
                SetRoleHeader(previousRole);
            }
        }

        private static bool HasAllowedRole(ClaimsPrincipal user, string role)
        {
            return ExecutionModeHelper.GetUserRoles(user).Contains(role, StringComparer.OrdinalIgnoreCase);
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
                    (variables != null ? $"with variables: {JsonSerializer.Serialize(variables).Substring(0, Math.Min(JsonSerializer.Serialize(variables).Length, 50)).Replace(Environment.NewLine, "")}..." : ""));
                GraphQLResponse<dynamic> response = await graphQlClient.SendQueryAsync<dynamic>(query, variables, operationName);
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
                Log.WriteError(LogCategory, $"Error while sending query to GraphQL API. Query: {query}, variables: {(variables != null ? JsonSerializer.Serialize(variables) : "")}", exception);
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
                    (variables != null ? $"with variables: {JsonSerializer.Serialize(variables).Substring(0, Math.Min(JsonSerializer.Serialize(variables).Length, 50)).Replace(Environment.NewLine, "")}..." : ""));
                GraphQLResponse<dynamic> response = await graphQlClient.SendQueryAsync<dynamic>(query, variables, operationName);

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
                Log.WriteError(LogCategory, $"Error while sending query to GraphQL API. Query: {query}, variables: {(variables != null ? JsonSerializer.Serialize(variables) : "")}", exception);
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
    }
}
