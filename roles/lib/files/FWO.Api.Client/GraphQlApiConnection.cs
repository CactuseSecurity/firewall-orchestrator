using System.Text.Json;
using GraphQL;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.SystemTextJson;
using GraphQL.Client.Serializer.Newtonsoft;
using GraphQL.Client.Abstractions;
using Newtonsoft.Json.Linq;
using FWO.Logging;
using System.Security.Claims;
using FWO.Basics;

namespace FWO.Api.Client
{
    public class GraphQlApiConnection : ApiConnection
    {
        private const string LogCategory = "API Connections";
        // Server URL
        public string ApiServerUri { get; private set; } = "";

        private GraphQLHttpClient graphQlClient = null!;

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
            prevRole = GetActRole();
            foreach (string role in targetRoleList)
            {
                if (HasAllowedRole(user, role))
                {
                    SetRole(role);
                    return;
                }
            }
        }

        public override void SetProperRole(System.Security.Claims.ClaimsPrincipal user, List<string> targetRoleList)
        {
            prevRole = GetActRole();

            // first look if user is already in one of the target roles 
            if (targetRoleList.Contains(prevRole))
            {
                return;
            }
            // now look if user has a target role as allowed role
            SetBestRole(user, targetRoleList);
        }

        public override void SwitchBack()
        {
            if (prevRole != "")
            {
                SetRole(prevRole);
            }
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
                else
                {
                    // DEBUG
                    // // string JsonResponse = JsonSerializer.Serialize(response, new JsonSerializerOptions { });
                    // string JsonResponse = JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
                    // Log.WriteDebug("API Response", $"API response: { JsonResponse }");

                    if (ApiConstants.UseSystemTextJsonSerializer)
                    {
                        // JsonElement.ObjectEnumerator responseObjectEnumerator = response.Data.EnumerateObject();
                        // responseObjectEnumerator.MoveNext();
                        // QueryResponseType returnValue = JsonSerializer.Deserialize<QueryResponseType>(responseObjectEnumerator.Current.Value.GetRawText()) ??
                        // throw new Exception($"Could not convert result from Json to {typeof(QueryResponseType)}.\nJson: {responseObjectEnumerator.Current.Value.GetRawText()}");
                        // return returnValue;
                    }
                    else
                    {
                        JObject data = (JObject)response.Data;
                        JProperty prop = (JProperty)(data.First ?? throw new InvalidOperationException($"Could not retrieve unique result attribute from Json.\nJson: {response.Data}"));
                        JToken result = prop.Value;
                        QueryResponseType returnValue = result.ToObject<QueryResponseType>() ??
                            throw new InvalidOperationException($"Could not convert result from Json to {typeof(QueryResponseType)}.\nJson: {response.Data}");
                        return returnValue;
                    }
                }
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

        private async Task<QueryResponseType> SendChunkedQueryAsync<QueryResponseType>(string query, object? variables, string? operationName, QueryChunkingOptions chunkingOptions)
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

            List<object?> items = ExtractChunkItems(variables, chunkingOptions.ChunkVariableName);
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
                object chunkedVariables = ReplaceChunkVariable(variables, chunkingOptions.ChunkVariableName, batch.ToList());
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

                JObject chunkData = (JObject)chunkResponse.Data;
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
                foreach (var entry in nullableDict)
                {
                    values[entry.Key] = entry.Key == propertyName ? batch : entry.Value;
                }

                if (!values.ContainsKey(propertyName))
                {
                    throw new InvalidOperationException($"Chunk variable '{propertyName}' was not found in variables.");
                }

                return values;
            }

            if (variables is IDictionary<string, object> dict)
            {
                foreach (var entry in dict)
                {
                    values[entry.Key] = entry.Key == propertyName ? batch : entry.Value;
                }

                if (!values.ContainsKey(propertyName))
                {
                    throw new InvalidOperationException($"Chunk variable '{propertyName}' was not found in variables.");
                }

                return values;
            }

            foreach (var property in variables.GetType().GetProperties())
            {
                values[property.Name] = property.Name == propertyName
                    ? batch
                    : property.GetValue(variables);
            }

            if (!values.ContainsKey(propertyName))
            {
                throw new InvalidOperationException($"Chunk variable '{propertyName}' was not found in variables.");
            }

            return values;
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
