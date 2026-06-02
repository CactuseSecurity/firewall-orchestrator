using FWO.Logging;
using GraphQL;
using GraphQL.Client.Http;
using Newtonsoft.Json.Linq;
using System.Diagnostics.CodeAnalysis;
using System.Net.WebSockets;

namespace FWO.Api.Client
{
    [SuppressMessage("Design", "S3060:DoNotCallOverridableMethodsInConstructors",
        Justification = "CreateSubscription is virtual for unit tests only. This is a design choice.")]
    public class GraphQlApiSubscription<SubscriptionResponseType> : ApiSubscription
    {
        public delegate void SubscriptionUpdate(SubscriptionResponseType response);
        public event SubscriptionUpdate? OnUpdate;

        private IObservable<GraphQLResponse<dynamic>>? _subscriptionStream;
        private IDisposable? _subscription;

        private readonly GraphQLHttpClient _graphQlClient;
        private readonly GraphQLRequest _request;
        private readonly ApiConnection _apiConnection;

        private readonly object _lock = new();
        private bool _disposed;
        private int _subscriptionVersion;

        public GraphQlApiSubscription(ApiConnection apiConnection, GraphQLHttpClient graphQlClient, GraphQLRequest request, Action<Exception> exceptionHandler, SubscriptionUpdate onUpdate)
        {
            _apiConnection = apiConnection;
            _graphQlClient = graphQlClient;
            _request = request;

            OnUpdate += onUpdate;
            ExternalExceptionHandler = exceptionHandler;

            CreateSubscription();

            _apiConnection.OnAuthHeaderChanged += ApiConnectionOnAuthHeaderChanged;
        }

        private Action<Exception> ExternalExceptionHandler { get; }

        protected virtual void CreateSubscription()
        {
            lock (_lock)
            {
                if (_disposed) return;

                int subscriptionVersion = ++_subscriptionVersion;
                _subscription?.Dispose();
                _subscription = null;

                Log.WriteDebug("API", $"Creating API subscription {_request.OperationName}.");
                Action<Exception> subscriptionExceptionHandler = exception => HandleSubscriptionException(exception, subscriptionVersion);
                _subscriptionStream = CreateSubscriptionStream(subscriptionVersion, subscriptionExceptionHandler);
                Log.WriteDebug("API", "API subscription created.");

                _subscription = _subscriptionStream.Subscribe(response =>
                {
                    Subscribe(response, subscriptionVersion);
                });
            }
        }

        private void Subscribe(GraphQLResponse<dynamic> response, int subscriptionVersion)
        {
            if (_disposed || subscriptionVersion != _subscriptionVersion) return;

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
                    if (subscriptionVersion != _subscriptionVersion)
                    {
                        return;
                    }

                    // Terminate subscription
                    lock (_lock)
                    {
                        if (subscriptionVersion == _subscriptionVersion)
                        {
                            _subscription?.Dispose();
                            _subscription = null;
                        }
                    }
                }
                else
                {
                    JObject data = (JObject)response.Data;
                    JProperty prop = (JProperty)(data.First ?? throw new Exception($"Could not retrieve unique result attribute from Json.\nJson: {response.Data}"));
                    JToken result = prop.Value;
                    SubscriptionResponseType returnValue = result.ToObject<SubscriptionResponseType>() ?? throw new Exception($"Could not convert result from Json to {typeof(SubscriptionResponseType)}.\nJson: {response.Data}");
                    OnUpdate?.Invoke(returnValue);
                }
            }
            catch (Exception ex)
            {
                Log.WriteError("GraphQL Subscription", "Subscription lead to exception", ex);
                throw;
            }
        }

        protected virtual IObservable<GraphQLResponse<dynamic>> CreateSubscriptionStream(int subscriptionVersion, Action<Exception> exceptionHandler)
        {
            return _graphQlClient.CreateSubscriptionStream<dynamic>(_request, exceptionHandler);
        }

        private void ApiConnectionOnAuthHeaderChanged(object? sender, string jwt)
        {
            // Recreate subscription (CreateSubscription handles disposal + locking)
            CreateSubscription();
        }

        private void HandleSubscriptionException(Exception exception, int subscriptionVersion)
        {
            if (subscriptionVersion != _subscriptionVersion && IsExpectedReconnectException(exception))
            {
                Log.WriteDebug("GraphQL Subscription", $"Ignoring expected subscription reconnect failure for {_request.OperationName}: {exception.Message}");
                return;
            }

            if (exception.Message.Contains("JWTExpired"))
            {
                throw exception;
            }

            ExternalExceptionHandler.Invoke(exception);
        }

        private static bool IsExpectedReconnectException(Exception exception)
        {
            return exception is WebSocketException
                || exception is OperationCanceledException
                || exception is ObjectDisposedException
                || exception.Message.Contains("close handshake", StringComparison.OrdinalIgnoreCase);
        }

        protected override void Dispose(bool disposing)
        {
            if (!disposing) return;

            lock (_lock)
            {
                if (_disposed) return;
                _disposed = true;

                // Important: detach from ApiConnection event to avoid keeping this subscription alive.
                _apiConnection.OnAuthHeaderChanged -= ApiConnectionOnAuthHeaderChanged;
                _subscription?.Dispose();
                _subscription = null;
                OnUpdate = null;
            }
        }
    }
}
