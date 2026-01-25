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

        private readonly GraphQLHttpClient _graphQlClient;
        private readonly GraphQLRequest _request;
        private readonly Action<Exception> _internalExceptionHandler;
        private readonly ApiConnection _apiConnection;

        private readonly object _lock = new();
        private bool _disposed;

        public GraphQlApiSubscription(ApiConnection apiConnection, GraphQLHttpClient graphQlClient, GraphQLRequest request, Action<Exception> exceptionHandler, SubscriptionUpdate onUpdate)
        {
            _apiConnection = apiConnection;
            _graphQlClient = graphQlClient;
            _request = request;

            OnUpdate += onUpdate;

            // handle subscription terminating exceptions
            _internalExceptionHandler = (Exception exception) =>
            {
                // Case: Jwt expired
                if (exception.Message.Contains("JWTExpired"))
                {
                    // Quit subscription by throwing exception.
                    // This does NOT lead to a real thrown exception within the application but is instead handled by the graphql library
                    throw exception;
                }
                exceptionHandler(exception);
            };

            CreateSubscription();

            _apiConnection.OnAuthHeaderChanged += ApiConnectionOnAuthHeaderChanged;
        }

        protected virtual void CreateSubscription()
        {
            lock (_lock)
            {
                if (_disposed) return;

                _subscription?.Dispose();
                _subscription = null;

                Log.WriteDebug("API", $"Creating API subscription {_request.OperationName}.");
                _subscriptionStream = _graphQlClient.CreateSubscriptionStream<dynamic>(_request, _internalExceptionHandler);
                Log.WriteDebug("API", "API subscription created.");

                _subscription = _subscriptionStream.Subscribe(response =>
                {
                    if (_disposed) return;

                    if (ApiConstants.UseSystemTextJsonSerializer)
                    {
                        throw new NotImplementedException("System.Text.Json is not supported anymore.");
                    }
                    else
                    {
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
                });
            }
        }

        private void ApiConnectionOnAuthHeaderChanged(object? sender, string jwt)
        {
            // Recreate subscription (CreateSubscription handles disposal + locking)
            CreateSubscription();
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
