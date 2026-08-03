using FWO.Logging;
using GraphQL;
using GraphQL.Client.Http;
using Newtonsoft.Json.Linq;
using System.Diagnostics.CodeAnalysis;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text.RegularExpressions;

namespace FWO.Api.Client
{
    [SuppressMessage("Design", "S3060:DoNotCallOverridableMethodsInConstructors",
        Justification = "CreateSubscription is virtual for unit tests only. This is a design choice.")]
    public partial class GraphQlApiSubscription<SubscriptionResponseType> : ApiSubscription
    {
        [GeneratedRegex(@"subscription\s(?'subscriptionName'.*?)[\s\(\{]")]
        private static partial Regex SubscriptionNameRegex();

        private const string kLogCategory = "GraphQL Subscription";
        private const string kJwtExpiredMarker = "JWTExpired";

        /// <summary>
        /// Number of consecutive connection interruptions that are logged as warnings before the
        /// subscription is treated as broken and reported to the external exception handler.
        /// </summary>
        private const int kQuietTransportFailureLimit = 3;

        /// <summary>
        /// Upper bound for walking the inner exception chain when classifying an exception.
        /// </summary>
        private const int kMaxExceptionChainDepth = 10;

        public delegate void SubscriptionUpdate(SubscriptionResponseType response);
        public event SubscriptionUpdate? OnUpdate;

        private IObservable<GraphQLResponse<dynamic>>? _subscriptionStream;
        private IDisposable? _subscription;

        private readonly GraphQLHttpClient _graphQlClient;
        public GraphQLRequest Request { get; init; }
        private readonly ApiConnection _apiConnection;
        private readonly SubscriptionUpdate _subscriptionUpdateHandler;

        private readonly object _lock = new();
        private bool _disposed;
        private int _consecutiveTransportFailures;

        public GraphQlApiSubscription(ApiConnection apiConnection, GraphQLHttpClient graphQlClient, GraphQLRequest request, Action<Exception> exceptionHandler, SubscriptionUpdate onUpdate)
        {
            _apiConnection = apiConnection;
            _graphQlClient = graphQlClient;
            Request = request;
            _subscriptionUpdateHandler = onUpdate;

            OnUpdate += onUpdate;
            ExternalExceptionHandler = exceptionHandler;

            CreateSubscription();
        }

        private Action<Exception> ExternalExceptionHandler { get; }

        protected virtual void CreateSubscription()
        {
            lock (_lock)
            {
                if (_disposed) return;

                _subscription?.Dispose();
                _subscription = null;

                Log.WriteDebug("API", $"Creating API subscription {Request.OperationName}.");
                Action<Exception> subscriptionExceptionHandler = HandleSubscriptionException;
                _subscriptionStream = CreateSubscriptionStream(subscriptionExceptionHandler);
                Log.WriteDebug("API", "API subscription created.");

                _subscription = _subscriptionStream.Subscribe(Subscribe, HandleStreamFailure);
            }
        }

        private void Subscribe(GraphQLResponse<dynamic> response)
        {
            if (_disposed) return;

            if (ApiConstants.UseSystemTextJsonSerializer)
            {
                throw new NotImplementedException("System.Text.Json is not supported anymore.");
            }

            try
            {
                // If repsonse.Data == null -> Jwt expired - connection was closed
                // Leads to this method getting called again
                if (response.Data == null)
                {
                    StopStream();
                }
                else
                {
                    JObject data = (JObject)response.Data;
                    JProperty prop = (JProperty)(data.First ?? throw new Exception($"Could not retrieve unique result attribute from Json.\nJson: {response.Data}"));
                    JToken result = prop.Value;
                    SubscriptionResponseType returnValue = result.ToObject<SubscriptionResponseType>() ?? throw new Exception($"Could not convert result from Json to {typeof(SubscriptionResponseType)}.\nJson: {response.Data}");
                    Interlocked.Exchange(ref _consecutiveTransportFailures, 0);
                    OnUpdate?.Invoke(returnValue);
                }
            }
            catch (Exception ex)
            {
                // Rethrowing here would escape into the receive pipeline and be rethrown on a thread
                // where nothing can catch it, killing the subscription because of a single bad message.
                Log.WriteError(kLogCategory, $"Subscription {DescribeSubscription()} lead to exception", ex);
                ExternalExceptionHandler(ex);
            }
        }

        /// <summary>
        /// Unsubscribes from the current stream without disposing this subscription, so that it can be
        /// recreated later on (e.g. by <see cref="ApiConnection.ReconnectSubscriptionsAsync"/>).
        /// </summary>
        private void StopStream()
        {
            lock (_lock)
            {
                _subscription?.Dispose();
                _subscription = null;
            }
        }

        protected virtual IObservable<GraphQLResponse<dynamic>> CreateSubscriptionStream(Action<Exception> exceptionHandler)
        {
            return _graphQlClient.CreateSubscriptionStream<dynamic>(Request, exceptionHandler);
        }

        /// <summary>
        /// Handles exceptions raised inside the receive pipeline of the GraphQL client.
        /// </summary>
        /// <remarks>
        /// This method must never throw. The client recreates the connection when the handler returns
        /// normally, whereas an exception thrown by the handler fails the observable sequence permanently
        /// and leaves the subscription dead until the next token refresh recreates it.
        /// </remarks>
        /// <param name="exception">The exception reported by the receive pipeline.</param>
        private void HandleSubscriptionException(Exception exception)
        {
            if (IsDisposed)
            {
                return;
            }

            if (IsJwtExpired(exception))
            {
                // Reconnecting would just repeat the same rejected token, so the stream is stopped
                // and picked up again by the next token refresh.
                Log.WriteWarning(kLogCategory, $"Subscription {DescribeSubscription()} was closed because the JWT expired. It is recreated with the next token refresh.");
                StopStream();
                return;
            }

            if (IsTransportInterruption(exception))
            {
                HandleTransportInterruption(exception);
                return;
            }

            Interlocked.Exchange(ref _consecutiveTransportFailures, 0);
            ExternalExceptionHandler(exception);
        }

        /// <summary>
        /// Reports a lost connection. Single drops are expected during normal operation because the
        /// client reconnects on its own, so they are logged as warnings without a stack trace. Only a
        /// connection that stays down is escalated to the external exception handler.
        /// </summary>
        /// <param name="exception">The transport exception that interrupted the stream.</param>
        private void HandleTransportInterruption(Exception exception)
        {
            int failureCount = Interlocked.Increment(ref _consecutiveTransportFailures);

            if (failureCount <= kQuietTransportFailureLimit)
            {
                Log.WriteWarning(kLogCategory, $"Connection for subscription {DescribeSubscription()} was interrupted " +
                    $"(attempt {failureCount} of {kQuietTransportFailureLimit}), reconnecting: {exception.Message}");
                return;
            }

            Log.WriteError(kLogCategory, $"Connection for subscription {DescribeSubscription()} could not be " +
                $"reestablished after {failureCount} attempts.", exception);
            ExternalExceptionHandler(exception);
        }

        /// <summary>
        /// Handles a failed observable sequence. Without an error handler on the subscription, Rx rethrows
        /// the exception on the producer thread, where it surfaces as an unhandled background exception.
        /// </summary>
        /// <param name="exception">The exception that terminated the stream.</param>
        private void HandleStreamFailure(Exception exception)
        {
            if (IsDisposed)
            {
                return;
            }

            StopStream();

            Log.WriteError(kLogCategory, $"Subscription {DescribeSubscription()} stopped after an unrecoverable " +
                "stream error. It is recreated with the next token refresh.", exception);
            ExternalExceptionHandler(exception);
        }

        /// <summary>
        /// Checks whether an exception was caused by the API rejecting an expired JWT.
        /// </summary>
        /// <param name="exception">The exception to classify.</param>
        /// <returns>True if the exception chain reports an expired JWT.</returns>
        private static bool IsJwtExpired(Exception exception)
        {
            return EnumerateExceptionChain(exception)
                .Any(inner => inner.Message.Contains(kJwtExpiredMarker, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Checks whether an exception is a connection interruption that the GraphQL client recovers
        /// from by reconnecting, rather than a genuine subscription failure.
        /// </summary>
        /// <param name="exception">The exception to classify.</param>
        /// <returns>True if the exception chain contains a transport level failure.</returns>
        private static bool IsTransportInterruption(Exception exception)
        {
            return EnumerateExceptionChain(exception)
                .Any(inner => inner is WebSocketException or SocketException or IOException
                    or HttpRequestException or OperationCanceledException);
        }

        /// <summary>
        /// Walks an exception and its inner exceptions up to a bounded depth.
        /// </summary>
        /// <param name="exception">The exception to start from.</param>
        /// <returns>The exception followed by its inner exceptions.</returns>
        private static IEnumerable<Exception> EnumerateExceptionChain(Exception exception)
        {
            Exception? current = exception;

            for (int depth = 0; current != null && depth < kMaxExceptionChainDepth; depth++)
            {
                yield return current;
                current = current.InnerException;
            }
        }

        /// <summary>
        /// Builds a readable name for this subscription for log messages.
        /// </summary>
        /// <returns>The subscription name, the operation name or the generic type name.</returns>
        private string DescribeSubscription()
        {
            if (TryGetSubscriptionNameFromQuery(Request.Query, out string subscriptionName))
            {
                return subscriptionName;
            }

            if (!string.IsNullOrWhiteSpace(Request.OperationName))
            {
                return Request.OperationName;
            }

            return $"{nameof(GraphQlApiSubscription<>)}<{nameof(SubscriptionResponseType)}>";
        }

        private static bool TryGetSubscriptionNameFromQuery(string? query, out string subscriptionName)
        {
            subscriptionName = "";

            if (string.IsNullOrEmpty(query))
            {
                return false;
            }

            Match match = SubscriptionNameRegex().Match(query);

            if (match.Success && !string.IsNullOrWhiteSpace(match.Groups[1].Value))
            {
                subscriptionName = match.Groups["subscriptionName"].Value;
            }

            return match.Success;
        }

        internal override ApiSubscription Recreate(GraphQLHttpClient graphQlClient)
        {
            Log.WriteInfo(kLogCategory, $"Recreating {DescribeSubscription()}");

            return new GraphQlApiSubscription<SubscriptionResponseType>(
                _apiConnection,
                graphQlClient,
                Request,
                ExternalExceptionHandler,
                _subscriptionUpdateHandler);
        }

        protected override void Dispose(bool disposing)
        {
            if (!disposing) return;

            lock (_lock)
            {
                if (_disposed) return;
                _disposed = true;
                _subscription?.Dispose();
                _subscription = null;
                OnUpdate = null;
            }
        }
    }
}
