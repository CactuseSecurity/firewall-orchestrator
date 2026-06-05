using FWO.Api.Client.ExceptionHandling;
using FWO.Basics;
using FWO.Basics.Exceptions;
using FWO.Logging;
using GraphQL;
using GraphQL.Client.Abstractions;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.Newtonsoft;
using GraphQL.Client.Serializer.SystemTextJson;
using Newtonsoft.Json.Linq;
using System.Net.Http.Headers;
using System.Security.Authentication;
using System.Security.Claims;
using System.Text.Json;

namespace FWO.Api.Client
{
    public class GraphQlApiConnection : ApiConnection
    {
        private const string LogCategory = "API Connections";
        // Server URL
        public string ApiServerUri { get; private set; } = "";

        private GraphQLHttpClient? graphQlClient;
        private GraphQLHttpClient? graphQlSubscriptionClient;

        private readonly Stack<string> previousRoles = new();
        private string forcedExecutionMode = "";
        private bool restrictElevatedRoleSwitches = false;

        private readonly SemaphoreSlim _reconnectLock = new(1, 1);

        private GraphQLHttpClient CreateClient(string apiServerUri)
        {
            HttpClientHandler handler = new()
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
            };

            GraphQLHttpClient client = new(new GraphQLHttpClientOptions()
            {
                EndPoint = new Uri(apiServerUri),
                HttpMessageHandler = handler,
                UseWebSocketForQueriesAndMutations = false, // TODO: Use websockets for performance reasons          
                ConfigureWebsocketOptions = webSocketOptions => webSocketOptions.RemoteCertificateValidationCallback += (message, cert, chain, errors) => true
            }, ApiConstants.UseSystemTextJsonSerializer ? new SystemTextJsonSerializer() : new NewtonsoftJsonSerializer());

            client.HttpClient.Timeout = new TimeSpan(1, 0, 0);
            return client;
        }

        private void Initialize(string ApiServerUri)
        {
            // Save Server URI
            this.ApiServerUri = ApiServerUri;
            graphQlClient = CreateClient(this.ApiServerUri);
            graphQlSubscriptionClient = CreateClient(this.ApiServerUri);
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
            ObjectDisposedException.ThrowIf(graphQlClient is null, graphQlClient);
            ObjectDisposedException.ThrowIf(graphQlSubscriptionClient is null, graphQlSubscriptionClient);

            ApplyAuthHeader(graphQlClient, jwt);
            ApplyAuthHeader(graphQlSubscriptionClient, jwt);

            InvokeOnAuthHeaderChanged(this, jwt);
        }

        private static void ApplyAuthHeader(GraphQLHttpClient client, string jwt)
        {
            client.HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
            client.Options.ConfigureWebSocketConnectionInitPayload = httpClientOptions => new { headers = new { authorization = $"Bearer {jwt}" } };
        }

        private static void ApplyRoleHeader(GraphQLHttpClient client, string role)
        {
            client.HttpClient.DefaultRequestHeaders.Remove("x-hasura-role");
            if (role != "")
            {
                client.HttpClient.DefaultRequestHeaders.Add("x-hasura-role", role);
            }
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
            ObjectDisposedException.ThrowIf(graphQlClient is null, graphQlClient);
            ObjectDisposedException.ThrowIf(graphQlSubscriptionClient is null, graphQlSubscriptionClient);

            ApplyRoleHeader(graphQlClient, role);
            ApplyRoleHeader(graphQlSubscriptionClient, role);
        }

        public bool IsActRole(string role)
        {
            return role == GetActRole();
        }

        public string GetActRole()
        {
            ObjectDisposedException.ThrowIf(graphQlClient is null, graphQlClient);

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

        public override void SetBestRole(ClaimsPrincipal user, List<string> targetRoleList)
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

        public override void SetProperRole(ClaimsPrincipal user, List<string> targetRoleList)
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
        /// <param name="chunkingOptions"></param>
        /// <returns><typeparamref name="QueryResponseType"/></returns>
        public override async Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
        {
            try
            {
                ObjectDisposedException.ThrowIf(graphQlClient is null, graphQlClient);

                if (chunkingOptions != null && chunkingOptions.Enabled)
                {
                    return await SendChunkedQueryAsync<QueryResponseType>(query, variables, operationName, chunkingOptions);
                }

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
                ObjectDisposedException.ThrowIf(graphQlClient is null, graphQlClient);

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

        private async Task<QueryResponseType> SendChunkedQueryAsync<QueryResponseType>(string query, object? variables, string? operationName, QueryChunkingOptions chunkingOptions)
        {
            ValidateChunkingOptions(variables, chunkingOptions);

            List<object?> items = ExtractChunkItems(variables!, chunkingOptions.ChunkVariableName);
            if (items.Count == 0)
            {
                return await SendQueryAsync<QueryResponseType>(query, variables, operationName, null);
            }

            int chunkCount = (int)Math.Ceiling((double)items.Count / chunkingOptions.ChunkSize);
            if (chunkCount > 1 && chunkingOptions.MergeMode == ChunkMergeMode.None)
            {
                throw new InvalidOperationException(
                    $"Chunking for variable '{chunkingOptions.ChunkVariableName}' produced {chunkCount} chunks, but MergeMode is None.");
            }

            JObject? mergedResponse = null;

            foreach (object?[] batch in items.Chunk(chunkingOptions.ChunkSize))
            {
                JObject chunkData = await SendSingleChunkAsync(query, variables!, operationName, chunkingOptions, batch);
                mergedResponse = MergeChunkedResponse(mergedResponse, chunkData, chunkingOptions);
            }

            if (mergedResponse == null)
            {
                throw new InvalidOperationException("Chunked query produced no response.");
            }

            return ConvertChunkResponse<QueryResponseType>(mergedResponse);
        }

        public override GraphQlApiSubscription<SubscriptionResponseType> GetSubscription<SubscriptionResponseType>(Action<Exception> exceptionHandler, GraphQlApiSubscription<SubscriptionResponseType>.SubscriptionUpdate subscriptionUpdateHandler, string subscription, object? variables = null, string? operationName = null)
        {
            try
            {
                ObjectDisposedException.ThrowIf(graphQlSubscriptionClient is null, graphQlSubscriptionClient);

                GraphQLRequest request = new(subscription, variables, operationName);
                GraphQlApiSubscription<SubscriptionResponseType> newSub = new(this, graphQlSubscriptionClient, request, exceptionHandler, subscriptionUpdateHandler);
                subscriptions.Add(newSub);

                return newSub;
            }
            catch (Exception exception)
            {
                Log.WriteError(LogCategory, "Error while creating subscription to GraphQL API.", exception);
                throw;
            }
        }

        public override async Task ReconnectSubscriptionsAsync(string jwt, CancellationToken ct)
        {
            await _reconnectLock.WaitAsync(ct);

            try
            {
                ObjectDisposedException.ThrowIf(graphQlClient is null, graphQlClient);
                ObjectDisposedException.ThrowIf(graphQlSubscriptionClient is null, graphQlSubscriptionClient);
                List<ApiSubscription> activeSubscriptions = subscriptions.Where(subscription => !subscription.IsDisposed).ToList();
                Log.WriteInfo(LogCategory, $"Reconnecting {activeSubscriptions.Count} API subscriptions after JWT refresh.");

                GraphQLHttpClient oldSubscriptionClient = graphQlSubscriptionClient;
                GraphQLHttpClient newSubscriptionClient = CreateClient(ApiServerUri);
                ApplyAuthHeader(graphQlClient, jwt);
                ApplyRoleHeader(graphQlClient, GetActRole());
                ApplyAuthHeader(newSubscriptionClient, jwt);
                ApplyRoleHeader(newSubscriptionClient, GetActRole());

                List<ApiSubscription> recreatedSubscriptions = [];
                graphQlSubscriptionClient = newSubscriptionClient;

                foreach (ApiSubscription subscription in activeSubscriptions)
                {
                    recreatedSubscriptions.Add(subscription.Recreate(newSubscriptionClient));
                }

                subscriptions.Clear();
                subscriptions.AddRange(recreatedSubscriptions);

                foreach (ApiSubscription subscription in activeSubscriptions)
                {
                    subscription.Dispose();
                }

                oldSubscriptionClient.Dispose();
            }
            catch (TaskCanceledException)
            {
                Log.WriteDebug(LogCategory, $"{nameof(ReconnectSubscriptionsAsync)} was cancelled.");
            }
            catch (Exception ex) when (ex is ObjectDisposedException)
            {
                Log.WriteError(LogCategory, "Error while reconnecting subscription", ex);
            }
            finally
            {
                _reconnectLock.Release();
            }
        }

        private static List<object?> ExtractChunkItems(object variables, string variableName)
        {
            if (!TryGetVariableValue(variables, variableName, out object? value))
            {
                throw new InvalidOperationException($"Chunk variable '{variableName}' was not found in variables.");
            }

            if (value == null)
            {
                throw new InvalidOperationException($"Chunk variable '{variableName}' is null.");
            }

            if (value is string)
            {
                throw new InvalidOperationException($"Chunk variable '{variableName}' must be a non-string enumerable.");
            }

            if (value is not System.Collections.IEnumerable enumerable)
            {
                throw new InvalidOperationException($"Chunk variable '{variableName}' must be a non-string enumerable.");
            }

            List<object?> items = [];
            foreach (object? item in enumerable)
            {
                items.Add(item);
            }

            return items;
        }

        private static bool TryGetVariableValue(object variables, string variableName, out object? value)
        {
            value = null;

            if (variables is IDictionary<string, object?> nullableDict && nullableDict.TryGetValue(variableName, out object? nullableValue))
            {
                value = nullableValue;
                return true;
            }

            if (variables is IDictionary<string, object> dict && dict.TryGetValue(variableName, out object? dictValue))
            {
                value = dictValue;
                return true;
            }

            if (variables is System.Collections.IDictionary nonGenericDict && nonGenericDict.Contains(variableName))
            {
                value = nonGenericDict[variableName];
                return true;
            }

            var property = variables.GetType().GetProperty(variableName);
            if (property == null)
            {
                return false;
            }

            value = property.GetValue(variables);
            return true;
        }

        private static object ReplaceChunkVariable(object variables, string propertyName, List<object?> batch)
        {
            Dictionary<string, object?> values = new(StringComparer.Ordinal);

            if (variables is IDictionary<string, object?> nullableDict)
            {
                CopyDictionaryValues(values, nullableDict, propertyName, batch);
            }
            else if (variables is IDictionary<string, object> dict)
            {
                CopyDictionaryValues(values, dict!, propertyName, batch);
            }
            else
            {
                CopyPropertyValues(values, variables, propertyName, batch);
            }

            if (!values.ContainsKey(propertyName))
            {
                throw new InvalidOperationException($"Chunk variable '{propertyName}' was not found in variables.");
            }

            return values;
        }

        private static void CopyDictionaryValues(Dictionary<string, object?> target, IEnumerable<KeyValuePair<string, object?>> source, string propertyName, List<object?> batch)
        {
            foreach (KeyValuePair<string, object?> entry in source)
            {
                target[entry.Key] = entry.Key == propertyName ? batch : entry.Value;
            }
        }

        private static void CopyPropertyValues(Dictionary<string, object?> target, object variables, string propertyName, List<object?> batch)
        {
            foreach (var property in variables.GetType().GetProperties())
            {
                target[property.Name] = property.Name == propertyName
                    ? batch
                    : property.GetValue(variables);
            }
        }

        private async Task<JObject> SendSingleChunkAsync(string query, object variables, string? operationName, QueryChunkingOptions chunkingOptions, object?[] batch)
        {
            ObjectDisposedException.ThrowIf(graphQlClient is null, graphQlClient);

            object chunkedVariables = ReplaceChunkVariable(variables!, chunkingOptions.ChunkVariableName, [.. batch]);
            GraphQLResponse<dynamic> chunkResponse = await graphQlClient.SendQueryAsync<dynamic>(query, chunkedVariables, operationName);

            if (chunkResponse.Errors != null)
            {
                string errorMessage = "";

                foreach (GraphQLError error in chunkResponse.Errors)
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

            return (JObject)chunkResponse.Data;
        }

        private static JObject MergeChunkedResponse(JObject? mergedResponse, JObject chunkData, QueryChunkingOptions chunkingOptions)
        {
            if (mergedResponse == null)
            {
                return (JObject)chunkData.DeepClone();
            }

            JProperty mergedProp = GetSingleTopLevelProperty(mergedResponse, "merged response");
            JProperty chunkProp = GetSingleTopLevelProperty(chunkData, "chunk response");
            ValidateSingleTopLevelFieldMatch(mergedProp, chunkProp);

            return MergeSingleTopLevelField(mergedProp, chunkProp, chunkingOptions.MergeMode);
        }

        private static JProperty GetSingleTopLevelProperty(JObject responseData, string context)
        {
            List<JProperty> properties = responseData.Properties().ToList();
            if (properties.Count != 1)
            {
                throw new InvalidOperationException(
                    $"Chunked {context} must contain exactly one top-level field. Json: {responseData}");
            }

            return properties[0];
        }

        private static void ValidateSingleTopLevelFieldMatch(JProperty mergedProp, JProperty chunkProp)
        {
            if (!string.Equals(mergedProp.Name, chunkProp.Name, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Chunked responses returned different top-level fields: '{mergedProp.Name}' and '{chunkProp.Name}'.");
            }
        }

        private static JObject MergeSingleTopLevelField(JProperty mergedProp, JProperty chunkProp, ChunkMergeMode mergeMode)
        {
            return mergeMode switch
            {
                ChunkMergeMode.MutationAffectedRowsOnly => new JObject(
                    new JProperty(
                        mergedProp.Name,
                        MergeMutationAffectedRowsOnly(
                            mergedProp.Value as JObject
                                ?? throw new InvalidOperationException($"Field '{mergedProp.Name}' must be an object."),
                            chunkProp.Value as JObject
                                ?? throw new InvalidOperationException($"Field '{chunkProp.Name}' must be an object."),
                            mergedProp.Name))),

                ChunkMergeMode.TopLevelArrayConcat => new JObject(
                    new JProperty(
                        mergedProp.Name,
                        MergeTopLevelArrays(mergedProp.Value, chunkProp.Value, mergedProp.Name))),

                ChunkMergeMode.MutationAffectedRowsAndReturning => new JObject(
                    new JProperty(
                        mergedProp.Name,
                        MergeMutationAffectedRowsAndReturning(
                            mergedProp.Value as JObject
                                ?? throw new InvalidOperationException($"Field '{mergedProp.Name}' must be an object."),
                            chunkProp.Value as JObject
                                ?? throw new InvalidOperationException($"Field '{chunkProp.Name}' must be an object."),
                            mergedProp.Name))),

                _ => throw new InvalidOperationException($"Unsupported chunk merge mode '{mergeMode}'.")
            };
        }

        private static JObject MergeMutationAffectedRowsOnly(JObject mergedObject, JObject chunkObject, string fieldName)
        {
            JToken? mergedAffectedRows = mergedObject["affected_rows"];
            JToken? chunkAffectedRows = chunkObject["affected_rows"];

            if (mergedAffectedRows == null || chunkAffectedRows == null)
            {
                throw new InvalidOperationException(
                    $"Chunk merge mode MutationAffectedRowsOnly requires field '{fieldName}' to contain 'affected_rows'.");
            }

            return new JObject
            {
                ["affected_rows"] = mergedAffectedRows.Value<long>() + chunkAffectedRows.Value<long>()
            };
        }

        private static JArray MergeTopLevelArrays(JToken mergedToken, JToken chunkToken, string fieldName)
        {
            if (mergedToken is not JArray mergedArray || chunkToken is not JArray chunkArray)
            {
                throw new InvalidOperationException(
                    $"Chunk merge mode TopLevelArrayConcat requires top-level field '{fieldName}' to be an array in every chunk.");
            }

            JArray result = [];
            foreach (JToken item in mergedArray)
            {
                result.Add(item.DeepClone());
            }

            foreach (JToken item in chunkArray)
            {
                result.Add(item.DeepClone());
            }

            return result;
        }

        private static JObject MergeMutationAffectedRowsAndReturning(JObject mergedObject, JObject chunkObject, string fieldName)
        {
            JToken? mergedAffectedRows = mergedObject["affected_rows"];
            JToken? chunkAffectedRows = chunkObject["affected_rows"];
            JToken? mergedReturning = mergedObject["returning"];
            JToken? chunkReturning = chunkObject["returning"];

            if (mergedAffectedRows == null || chunkAffectedRows == null || mergedReturning == null || chunkReturning == null)
            {
                throw new InvalidOperationException(
                    $"Chunk merge mode MutationAffectedRowsAndReturning requires field '{fieldName}' to contain 'affected_rows' and 'returning'.");
            }

            if (mergedReturning is not JArray mergedReturningArray || chunkReturning is not JArray chunkReturningArray)
            {
                throw new InvalidOperationException(
                    $"Chunk merge mode MutationAffectedRowsAndReturning requires field '{fieldName}.returning' to be an array.");
            }

            JArray mergedReturningResult = [];
            foreach (JToken item in mergedReturningArray)
            {
                mergedReturningResult.Add(item.DeepClone());
            }

            foreach (JToken item in chunkReturningArray)
            {
                mergedReturningResult.Add(item.DeepClone());
            }

            return new JObject
            {
                ["affected_rows"] = mergedAffectedRows.Value<long>() + chunkAffectedRows.Value<long>(),
                ["returning"] = mergedReturningResult
            };
        }

        private static QueryResponseType ConvertChunkResponse<QueryResponseType>(JObject mergedResponse)
        {
            JProperty prop = GetSingleTopLevelProperty(mergedResponse, "merged response");
            JToken result = prop.Value;

            QueryResponseType returnValue = result.ToObject<QueryResponseType>() ??
                throw new InvalidOperationException($"Could not convert merged chunk response to {typeof(QueryResponseType)}.\nJson: {mergedResponse}");

            return returnValue;
        }

        private static void ValidateChunkingOptions(object? variables, QueryChunkingOptions chunkingOptions)
        {

            if (variables == null)
            {
                throw new ArgumentNullException(nameof(variables), "Chunking requires variables.");
            }

            if (string.IsNullOrWhiteSpace(chunkingOptions.ChunkVariableName))
            {
                throw new ArgumentException("ChunkVariableName is required when chunking is enabled.", nameof(chunkingOptions));
            }

            if (chunkingOptions.ChunkSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(chunkingOptions), "ChunkSize must be greater than zero.");
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (ApiSubscription subscription in subscriptions)
                {
                    subscription.Dispose();
                }

                subscriptions.Clear();

                graphQlClient?.Dispose();
                graphQlClient = null;
                graphQlSubscriptionClient?.Dispose();
                graphQlSubscriptionClient = null;
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
