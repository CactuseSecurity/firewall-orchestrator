using FWO.Api.Client.ExceptionHandling;
using FWO.Basics;
using FWO.Basics.Exceptions;
using FWO.Config.File;
using FWO.Logging;
using GraphQL;
using GraphQL.Client.Abstractions;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.Newtonsoft;
using GraphQL.Client.Serializer.SystemTextJson;
using Newtonsoft.Json.Linq;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Runtime.ExceptionServices;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace FWO.Api.Client
{
    public class GraphQlApiConnection : ApiConnection
    {
        private const string LogCategory = "API Connections";
        // Server URL
        public string ApiServerUri { get; private set; } = "";

        private GraphQLHttpClient? graphQlClient;
        private GraphQLHttpClient? graphQlSubscriptionClient;

        private readonly AsyncLocal<List<string>?> roleStack = new();
        private string defaultRole = "";
        private List<string> allowedRoles = [];
        private string ambientRole = "";
        private string forcedExecutionMode = "";
        private bool restrictElevatedRoleSwitches = false;

        private readonly SemaphoreSlim _reconnectLock = new(1, 1);

        private static readonly TimeSpan clientCertificateRetryInterval = TimeSpan.FromSeconds(30);
        private static readonly object clientCertificateLock = new();
        private static X509Certificate2? clientCertificate;
        private static ConfigException? clientCertificateFailure;
        private static DateTime clientCertificateFailedAt = DateTime.MinValue;

        private static readonly object apiCaCertificateLock = new();
        private static X509Certificate2? apiCaCertificate;
        private static ConfigException? apiCaCertificateFailure;
        private static DateTime apiCaCertificateFailedAt = DateTime.MinValue;

        /// <summary>
        /// Returns the local FWO client identity, loading it on first use and reusing it after.
        /// </summary>
        /// <remarks>
        /// Holds unmanaged key material, so it must not be re-created per connection, and
        /// it is deliberately never disposed: it lives for the lifetime of the process and
        /// the installer restarts the services when it renews the certificate.
        /// Only a successful load is cached, so a service that starts while the certificate
        /// is still unreadable recovers instead of failing permanently. A failure is retried
        /// no more than once per <see cref="clientCertificateRetryInterval"/>, because every
        /// attempt writes a stack trace and connections are created per user session.
        /// This is a method rather than a property because it can fail (S2372).
        /// </remarks>
        /// <returns>The client identity presented to the API server.</returns>
        /// <exception cref="ConfigException">The certificate or key is missing or unreadable.</exception>
        private static X509Certificate2 GetClientCertificate()
        {
            lock (clientCertificateLock)
            {
                if (clientCertificate != null)
                {
                    return clientCertificate;
                }
                if (clientCertificateFailure != null
                    && DateTime.UtcNow - clientCertificateFailedAt < clientCertificateRetryInterval)
                {
                    // Rethrowing the stored instance directly would overwrite its stack trace
                    // with this call site on every attempt, losing where the load actually failed.
                    ExceptionDispatchInfo.Capture(clientCertificateFailure).Throw();
                }
                try
                {
                    clientCertificate = LoadClientCertificate();
                    clientCertificateFailure = null;
                    return clientCertificate;
                }
                catch (ConfigException exception)
                {
                    clientCertificateFailure = exception;
                    clientCertificateFailedAt = DateTime.UtcNow;
                    throw;
                }
            }
        }

        /// <summary>
        /// Reads the client certificate and its private key from the paths in the FWO config file.
        /// </summary>
        /// <returns>The client identity presented to the API server.</returns>
        /// <exception cref="ConfigException">The paths are not configured, or the files cannot be read.</exception>
        private static X509Certificate2 LoadClientCertificate()
        {
            string certificatePath;
            string privateKeyPath;

            // Read the paths before the load, so reporting a failure cannot trigger the
            // very config lookup that failed and replace the exception with its own.
            try
            {
                certificatePath = ConfigFile.TlsClientCertificate;
                privateKeyPath = ConfigFile.TlsClientPrivateKey;
            }
            catch (Exception exception)
            {
                throw new ConfigException("The API requires a client certificate, but tls_client_certificate " +
                    "and tls_client_private_key are not set in the FWO config file. An installation upgraded " +
                    "from before the internal CA needs the installer to add them.", exception);
            }

            try
            {
                return X509Certificate2.CreateFromPemFile(certificatePath, privateKeyPath);
            }
            catch (Exception exception)
            {
                throw new ConfigException($"Could not load the FWO client certificate from " +
                    $"tls_client_certificate ({certificatePath}) and " +
                    $"tls_client_private_key ({privateKeyPath}). " +
                    $"Check that the files exist and are readable by this service.", exception);
            }
        }

        /// <summary>
        /// Returns the configured API trust anchor for explicit server certificate validation.
        /// </summary>
        /// <returns>The CA certificate allowed to issue API server certificates.</returns>
        /// <exception cref="ConfigException">The configured CA certificate cannot be read.</exception>
        private static X509Certificate2 GetApiCaCertificate()
        {
            lock (apiCaCertificateLock)
            {
                if (apiCaCertificate != null)
                {
                    return apiCaCertificate;
                }
                // This runs on every TLS handshake, so a misconfigured path must not be
                // re-read and re-logged per connection.
                if (apiCaCertificateFailure != null
                    && DateTime.UtcNow - apiCaCertificateFailedAt < clientCertificateRetryInterval)
                {
                    ExceptionDispatchInfo.Capture(apiCaCertificateFailure).Throw();
                }
                try
                {
                    string caCertificatePath = ConfigFile.TlsCaCertificate;
                    apiCaCertificate = X509CertificateLoader.LoadCertificateFromFile(caCertificatePath);
                    apiCaCertificateFailure = null;
                    return apiCaCertificate;
                }
                catch (Exception exception)
                {
                    apiCaCertificateFailure = new ConfigException(
                        "Could not load the FWO API CA certificate configured as tls_ca_certificate. " +
                        "API server certificates cannot be validated until this is fixed.", exception);
                    apiCaCertificateFailedAt = DateTime.UtcNow;
                    Log.WriteError(LogCategory, apiCaCertificateFailure.Message);
                    throw apiCaCertificateFailure;
                }
            }
        }

        /// <summary>
        /// Validates an API server certificate against the configured CA and requested host name.
        /// </summary>
        /// <param name="certificate">The API server certificate.</param>
        /// <param name="chain">The chain supplied by the TLS stack.</param>
        /// <param name="sslPolicyErrors">Platform TLS validation errors.</param>
        /// <returns>True only when the certificate name and configured CA chain are valid.</returns>
        private static bool ValidateApiServerCertificate(X509Certificate? certificate, X509Chain? chain, SslPolicyErrors sslPolicyErrors)
        {
            if (certificate == null || sslPolicyErrors.HasFlag(SslPolicyErrors.RemoteCertificateNameMismatch))
            {
                return false;
            }

            X509Certificate2 trustAnchor;
            try
            {
                trustAnchor = GetApiCaCertificate();
            }
            catch (ConfigException)
            {
                // Throwing out of a validation callback surfaces as an opaque
                // AuthenticationException. The loader has already logged the cause, so
                // reject the certificate instead; the connection fails either way.
                return false;
            }

            using X509Certificate2 serverCertificate = new(certificate);
            using X509Chain pinnedChain = new();
            pinnedChain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
            // Disposing the chain does not dispose the custom trust store, so the cached
            // anchor stays usable for later handshakes.
            pinnedChain.ChainPolicy.CustomTrustStore.Add(trustAnchor);
            // The peer supplies its intermediates in the chain handed to this callback.
            // Without them a root -> intermediate -> leaf chain cannot be built, which is
            // the usual shape of a customer managed certificate.
            if (chain != null)
            {
                foreach (X509ChainElement element in chain.ChainElements)
                {
                    pinnedChain.ChainPolicy.ExtraStore.Add(element.Certificate);
                }
            }
            pinnedChain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            return pinnedChain.Build(serverCertificate);
        }

        private GraphQLHttpClient CreateClient(string apiServerUri)
        {
            bool useTls = new Uri(apiServerUri).Scheme == Uri.UriSchemeHttps;
            HttpClientHandler handler = CreateHttpClientHandler(useTls);

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
                        webSocketOptions.ClientCertificates.Add(GetClientCertificate());
                        webSocketOptions.RemoteCertificateValidationCallback = (_, certificate, chain, errors) => ValidateApiServerCertificate(certificate, chain, errors);
                    }
                }
            }, ApiConstants.UseSystemTextJsonSerializer ? new SystemTextJsonSerializer() : new NewtonsoftJsonSerializer());

            client.HttpClient.Timeout = new TimeSpan(1, 0, 0);
            return client;
        }

        /// <summary>
        /// Creates the message handler, presenting the client identity when TLS is used.
        /// </summary>
        /// <param name="useTls">Whether the API server is addressed over https.</param>
        /// <returns>The handler used by the GraphQL client.</returns>
        private static HttpClientHandler CreateHttpClientHandler(bool useTls)
        {
            HttpClientHandler handler = new();
            if (useTls)
            {
                handler.ClientCertificates.Add(GetClientCertificate());
                handler.ServerCertificateCustomValidationCallback = (_, certificate, chain, errors) => ValidateApiServerCertificate(certificate, chain, errors);
            }
            return handler;
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

            UpdateJwtRoleState(jwt);
            ApplyAuthHeader(graphQlClient, jwt);
            ApplyAuthHeader(graphQlSubscriptionClient, jwt);

            InvokeOnAuthHeaderChanged(this, jwt);
        }

        private void ApplyAuthHeader(GraphQLHttpClient client, string jwt)
        {
            client.HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
            client.Options.ConfigureWebSocketConnectionInitPayload = _ => CreateWebSocketConnectionInitPayload(client);
        }

        public override void SetRole(string role)
        {
            if (restrictElevatedRoleSwitches && IsForcedExecutionMode(role))
            {
                throw new AuthenticationException($"Execution mode '{GlobalConst.kUserRolesSelection}' does not allow switching to role: {role}");
            }

            PushRole(IsForcedExecutionMode(forcedExecutionMode) ? forcedExecutionMode : role);
        }

        private void ApplyExecutionMode(string role, bool restrictElevatedRoles)
        {
            forcedExecutionMode = IsForcedExecutionMode(role) ? role : "";
            restrictElevatedRoleSwitches = restrictElevatedRoles;
            ambientRole = "";
            roleStack.Value = null;
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

        public override void SetAmbientRole(ClaimsPrincipal user, List<string> targetRoleList)
        {
            if (targetRoleList.Count == 0)
            {
                ambientRole = "";
                return;
            }

            bool includeElevatedRoles = !HasSelectableUserRole(user);
            ambientRole = IsForcedExecutionMode(user)
                ? forcedExecutionMode
                : GetFirstAllowedRole(user, targetRoleList, includeElevatedRoles)
                    ?? "";
        }

        public override string GetExecutionMode()
        {
            return forcedExecutionMode == "" ? GlobalConst.kUserRolesSelection : forcedExecutionMode;
        }

        public bool IsActRole(string role)
        {
            return role == GetActRole();
        }

        public override string GetActRole()
        {
            ObjectDisposedException.ThrowIf(graphQlClient is null, graphQlClient);

            List<string>? roles = roleStack.Value;
            if (roles != null && roles.Count > 0)
            {
                return roles[^1];
            }
            if (!string.IsNullOrWhiteSpace(ambientRole))
            {
                return ambientRole;
            }
            return GetBaselineRole();
        }

        public override void SetBestRole(ClaimsPrincipal user, List<string> targetRoleList)
        {
            bool includeElevatedRoles = !HasSelectableUserRole(user);
            string targetRole = IsForcedExecutionMode(user)
                ? forcedExecutionMode
                : GetFirstAllowedRole(user, targetRoleList, includeElevatedRoles)
                    ?? throw new AuthenticationException($"User has none of the required roles: {string.Join(", ", targetRoleList)}");
            PushRole(targetRole);
        }

        private static string? GetFirstAllowedRole(ClaimsPrincipal user, List<string> targetRoleList, bool includeElevatedRoles)
        {
            foreach (string targetRole in targetRoleList)
            {
                if ((includeElevatedRoles || !IsForcedExecutionMode(targetRole)) && HasAllowedRole(user, targetRole))
                {
                    return targetRole;
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

        private string GetBaselineRole()
        {
            if (IsForcedExecutionMode(forcedExecutionMode))
            {
                return forcedExecutionMode;
            }
            if (restrictElevatedRoleSwitches && IsForcedExecutionMode(defaultRole))
            {
                return "";
            }
            return defaultRole;
        }

        private string GetRequestRole()
        {
            string role = GetActRole();
            if (!string.IsNullOrWhiteSpace(role) && HasExplicitRole())
            {
                return role;
            }
            if (IsForcedExecutionMode(forcedExecutionMode))
            {
                return role;
            }
            if (!string.IsNullOrWhiteSpace(ambientRole))
            {
                return ambientRole;
            }
            if (!string.IsNullOrWhiteSpace(role))
            {
                return role;
            }
            if (RequiresExplicitRole())
            {
                throw new AuthenticationException("GraphQL API call requires an explicit role for users with multiple application roles. Use RunWithBestRole or RunWithRole.");
            }
            return role;
        }

        private bool HasExplicitRole()
        {
            List<string>? roles = roleStack.Value;
            return roles != null && roles.Any(role => !string.IsNullOrWhiteSpace(role));
        }

        private bool RequiresExplicitRole()
        {
            return allowedRoles
                .Where(role => !FWO.Basics.RoleGroups.IsTechnicalOrAnonymous(role))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count() > 1;
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
            return ExecutionModeHelper.GetUserRoles(user).Contains(role, StringComparer.OrdinalIgnoreCase);
        }

        private void UpdateJwtRoleState(string jwt)
        {
            defaultRole = "";
            allowedRoles = [];
            try
            {
                JwtSecurityToken token = new JwtSecurityTokenHandler().ReadJwtToken(jwt);
                defaultRole = token.Claims.FirstOrDefault(claim => claim.Type == "x-hasura-default-role")?.Value ?? "";
                allowedRoles = JwtClaimParser.ExtractStringClaimValues(token.Claims, "x-hasura-allowed-roles");
            }
            catch
            {
                defaultRole = "";
                allowedRoles = [];
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

        public override GraphQlApiSubscription<SubscriptionResponseType> GetSubscription<SubscriptionResponseType>(Action<Exception> exceptionHandler, GraphQlApiSubscription<SubscriptionResponseType>.SubscriptionUpdate subscriptionUpdateHandler, string subscription, object? variables = null, string? operationName = null)
        {
            try
            {
                ObjectDisposedException.ThrowIf(graphQlSubscriptionClient is null, graphQlSubscriptionClient);

                GraphQLRequest request = CreateSubscriptionRequest(subscription, variables, operationName);
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

                ct.ThrowIfCancellationRequested();

                List<ApiSubscription> activeSubscriptions = [.. subscriptions.Where(subscription => !subscription.IsDisposed)];

                Log.WriteInfo(LogCategory, $"Reconnecting {activeSubscriptions.Count} API subscriptions after JWT refresh.");

                GraphQLHttpClient oldSubscriptionClient = graphQlSubscriptionClient;
                GraphQLHttpClient newSubscriptionClient = CreateClient(ApiServerUri);
                UpdateJwtRoleState(jwt);
                ApplyAuthHeader(graphQlClient, jwt);
                ApplyAuthHeader(newSubscriptionClient, jwt);

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

        private GraphQLHttpRequest CreateHttpRequest(string role, string query, object? variables, string? operationName)
        {
            return new RoleGraphQLHttpRequest(role, query, variables, operationName);
        }

        private object CreateWebSocketConnectionInitPayload(GraphQLHttpClient client)
        {
            string role = GetRequestRole();
            Dictionary<string, object?> headers = new()
            {
                ["authorization"] = client.HttpClient.DefaultRequestHeaders.Authorization?.ToString()
            };
            if (!string.IsNullOrWhiteSpace(role))
            {
                headers["x-hasura-role"] = role;
            }

            return new Dictionary<string, object?> { ["headers"] = headers };
        }

        private GraphQLRequest CreateSubscriptionRequest(string query, object? variables, string? operationName)
        {
            string role = GetRequestRole();
            GraphQLRequest request = new(query, variables, operationName);
            if (!string.IsNullOrWhiteSpace(role))
            {
                request.Extensions = new Dictionary<string, object?>
                {
                    ["headers"] = new Dictionary<string, object?>
                    {
                        ["x-hasura-role"] = role
                    }
                };
            }
            return request;
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
