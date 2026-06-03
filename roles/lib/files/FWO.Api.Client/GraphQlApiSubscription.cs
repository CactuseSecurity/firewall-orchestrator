using FWO.Logging;
using GraphQL;
using GraphQL.Client.Http;
using Newtonsoft.Json.Linq;
using System.Diagnostics.CodeAnalysis;

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

        private GraphQLHttpClient _graphQlClient;
        public GraphQLRequest Request { get; init; }
        private readonly ApiConnection _apiConnection;
        private readonly SubscriptionUpdate _subscriptionUpdateHandler;

        private readonly object _lock = new();
        private bool _disposed;

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

                _subscription = _subscriptionStream.Subscribe(Subscribe);
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
                    // Terminate subscription
                    lock (_lock)
                    {
                        _subscription?.Dispose();
                        _subscription = null;
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

        protected virtual IObservable<GraphQLResponse<dynamic>> CreateSubscriptionStream(Action<Exception> exceptionHandler)
        {
            return _graphQlClient.CreateSubscriptionStream<dynamic>(Request, exceptionHandler);
        }

        private void HandleSubscriptionException(Exception exception)
        {
            if (IsDisposed)
            {
                return;
            }

            if (exception.Message.Contains("JWTExpired"))
            {
                throw exception;
            }

            ExternalExceptionHandler(exception);
        }

        internal override ApiSubscription Recreate(GraphQLHttpClient graphQlClient)
        {
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
