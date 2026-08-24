using FWO.Api.Client.ExceptionHandling;
using FWO.Basics;
using FWO.Logging;
using GraphQL;
using GraphQL.Client.Abstractions;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.Newtonsoft;
using GraphQL.Client.Serializer.SystemTextJson;
using Newtonsoft.Json.Linq;

namespace FWO.Api.Client
{
    public partial class GraphQlApiConnection : ApiConnection
    {
        private const string LogCategory = "API Connections";
        // Server URL
        public string ApiServerUri { get; private set; } = "";

        private GraphQLHttpClient? graphQlClient;

        private GraphQLHttpClient CreateClient(string apiServerUri)
        {
            bool useTls = new Uri(apiServerUri).Scheme == Uri.UriSchemeHttps;
            HttpClientHandler handler = GraphQlTlsCertificateSupport.CreateHttpClientHandler(useTls);

            GraphQLHttpClient client = new(new GraphQLHttpClientOptions()
            {
                EndPoint = new Uri(apiServerUri),
                HttpMessageHandler = handler,
                UseWebSocketForQueriesAndMutations = false, // TODO: Use websockets for performance reasons
                // Subscriptions run over websockets, which need the same client identity as
                // the http requests. Certificate validation is left at the platform default.
                ConfigureWebsocketOptions = webSocketOptions =>
                {
                    if (useTls)
                    {
                        webSocketOptions.ClientCertificates.Add(GraphQlTlsCertificateSupport.GetClientCertificate());
                        webSocketOptions.RemoteCertificateValidationCallback = (_, certificate, chain, errors) => GraphQlTlsCertificateSupport.ValidateApiServerCertificate(certificate, chain, errors);
                    }
                }
            }, ApiConstants.UseSystemTextJsonSerializer ? new SystemTextJsonSerializer() : new NewtonsoftJsonSerializer());

            client.HttpClient.Timeout = new TimeSpan(1, 0, 0);
            return client;
        }

        private void Initialize(string ApiServerUri)
        {
            // Save Server URI
            this.ApiServerUri = ApiServerUri;
            graphQlClient = CreateClient(this.ApiServerUri);
            InitializeSubscriptionClient();
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

                string requestRole = GetRequestRole();
                Log.WriteDebug("API call", $"Sending API call {operationName} in role {requestRole}: {query.Substring(0, Math.Min(query.Length, 70)).Replace(Environment.NewLine, "")}... " +
                    (variables != null ? "with variables: <redacted>" : ""));
                GraphQLResponse<dynamic> response = await graphQlClient.SendQueryAsync<dynamic>(CreateHttpRequest(requestRole, query, variables, operationName));
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
                ObjectDisposedException.ThrowIf(graphQlClient is null, graphQlClient);

                string requestRole = GetRequestRole();
                Log.WriteDebug("API call", $"Sending API call {operationName} in role {requestRole}: {query.Substring(0, Math.Min(query.Length, 70)).Replace(Environment.NewLine, "")}... " +
                    (variables != null ? "with variables: <redacted>" : ""));
                GraphQLResponse<dynamic> response = await graphQlClient.SendQueryAsync<dynamic>(CreateHttpRequest(requestRole, query, variables, operationName));

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
            string requestRole = GetRequestRole();
            GraphQLResponse<dynamic> chunkResponse = await graphQlClient.SendQueryAsync<dynamic>(CreateHttpRequest(requestRole, query, chunkedVariables, operationName));

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
                graphQlClient?.Dispose();
                graphQlClient = null;
                DisposeSubscriptionResources();
            }
        }

        private GraphQLHttpRequest CreateHttpRequest(string role, string query, object? variables, string? operationName)
        {
            return new RoleGraphQLHttpRequest(role, query, variables, operationName);
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
