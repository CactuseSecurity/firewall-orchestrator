using GraphQL;
using GraphQL.Client.Http;
using FWO.Logging;

namespace FWO.Api.Client
{
    public partial class GraphQlApiConnection
    {
        private readonly SemaphoreSlim reconnectLock = new(1, 1);
        private GraphQLHttpClient? graphQlSubscriptionClient;

        /// <summary>
        /// Creates the GraphQL client used for subscriptions.
        /// </summary>
        private void InitializeSubscriptionClient()
        {
            graphQlSubscriptionClient = CreateClient(ApiServerUri);
        }

        /// <summary>
        /// Creates and starts a subscription to the GraphQL API.
        /// </summary>
        /// <typeparam name="SubscriptionResponseType">The type of data received by the subscription.</typeparam>
        /// <param name="exceptionHandler">Handles subscription errors.</param>
        /// <param name="subscriptionUpdateHandler">Handles received subscription data.</param>
        /// <param name="subscription">The GraphQL subscription query.</param>
        /// <param name="variables">Optional query variables.</param>
        /// <param name="operationName">Optional GraphQL operation name.</param>
        /// <returns>The created subscription.</returns>
        public override GraphQlApiSubscription<SubscriptionResponseType> GetSubscription<SubscriptionResponseType>(Action<Exception> exceptionHandler, GraphQlApiSubscription<SubscriptionResponseType>.SubscriptionUpdate subscriptionUpdateHandler, string subscription, object? variables = null, string? operationName = null)
        {
            try
            {
                GraphQlApiSubscription<SubscriptionResponseType> newSubscription;

                // The client is read and the subscription registered in one atomic step, so a
                // concurrent reconnect or disposal cannot swap the client in between and leave
                // this subscription bound to a client nothing tracks any more.
                lock (subscriptionsLock)
                {
                    ObjectDisposedException.ThrowIf(graphQlSubscriptionClient is null, graphQlSubscriptionClient);

                    GraphQLRequest request = CreateSubscriptionRequest(subscription, variables, operationName);
                    newSubscription = new(this, graphQlSubscriptionClient, request, exceptionHandler, subscriptionUpdateHandler);
                    subscriptions.Add(newSubscription);
                }

                return newSubscription;
            }
            catch (Exception exception)
            {
                Log.WriteError(LogCategory, "Error while creating subscription to GraphQL API.", exception);
                throw;
            }
        }

        /// <summary>
        /// Rebinds active subscriptions to a client configured for the refreshed JWT.
        /// </summary>
        /// <remarks>
        /// The subscriptions are rebound rather than recreated and disposed, so a caller
        /// holding one keeps a live subscription across a JWT refresh. The swap of the shared
        /// state happens under subscriptionsLock; the rebinding and the disposal of the
        /// previous client run outside it, because both reach back into this instance.
        /// </remarks>
        /// <param name="jwt">The refreshed JWT.</param>
        /// <param name="ct">Cancels waiting for the reconnect lock.</param>
        public override async Task ReconnectSubscriptionsAsync(string jwt, CancellationToken ct)
        {
            await reconnectLock.WaitAsync(ct);

            try
            {
                ct.ThrowIfCancellationRequested();

                GraphQLHttpClient oldSubscriptionClient;
                GraphQLHttpClient newSubscriptionClient;
                List<ApiSubscription> activeSubscriptions;

                lock (subscriptionsLock)
                {
                    ObjectDisposedException.ThrowIf(_disposed, this);
                    ObjectDisposedException.ThrowIf(graphQlClient is null, graphQlClient);
                    ObjectDisposedException.ThrowIf(graphQlSubscriptionClient is null, graphQlSubscriptionClient);

                    subscriptions.RemoveAll(subscription => subscription.IsDisposed);
                    activeSubscriptions = [.. subscriptions];
                    oldSubscriptionClient = graphQlSubscriptionClient;
                    newSubscriptionClient = CreateSubscriptionClient(ApiServerUri);
                    UpdateJwtRoleState(jwt);
                    ApplyAuthHeader(graphQlClient, jwt);
                    ApplyAuthHeader(newSubscriptionClient, jwt);

                    graphQlSubscriptionClient = newSubscriptionClient;
                }

                Log.WriteInfo(LogCategory, $"Reconnecting {activeSubscriptions.Count} API subscriptions after JWT refresh.");

                foreach (ApiSubscription subscription in activeSubscriptions)
                {
                    subscription.Rebind(newSubscriptionClient);
                }

                oldSubscriptionClient.Dispose();
            }
            catch (TaskCanceledException)
            {
                Log.WriteDebug(LogCategory, $"{nameof(ReconnectSubscriptionsAsync)} was cancelled.");
            }
            catch (ObjectDisposedException exception)
            {
                Log.WriteError(LogCategory, "Error while reconnecting subscription", exception);
            }
            finally
            {
                reconnectLock.Release();
            }
        }

        /// <summary>
        /// Disposes and removes subscriptions with the requested exact runtime type.
        /// </summary>
        /// <typeparam name="T">The exact subscription type to remove.</typeparam>
        public override void DisposeSubscriptions<T>()
        {
            List<ApiSubscription> subscriptionsToDispose;

            // Snapshot and remove under the lock, dispose outside it: disposing a subscription
            // reaches back into this instance, which would deadlock against the lock itself.
            lock (subscriptionsLock)
            {
                subscriptionsToDispose = [.. subscriptions.Where(subscription => subscription.GetType() == typeof(T))];
                subscriptions.RemoveAll(subscription => subscription.GetType() == typeof(T));
            }

            foreach (ApiSubscription subscription in subscriptionsToDispose)
            {
                subscription.Dispose();
            }
        }

        /// <summary>
        /// Creates the WebSocket initialization payload for the active role.
        /// </summary>
        /// <param name="client">The GraphQL client owning the WebSocket connection.</param>
        /// <returns>The WebSocket initialization payload.</returns>
        private Dictionary<string, object?> CreateWebSocketConnectionInitPayload(GraphQLHttpClient client)
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

        /// <summary>
        /// Creates a subscription request with the active role in its extension headers.
        /// </summary>
        /// <param name="query">The GraphQL subscription query.</param>
        /// <param name="variables">Optional query variables.</param>
        /// <param name="operationName">Optional GraphQL operation name.</param>
        /// <returns>The prepared subscription request.</returns>
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
    }
}
