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

        private readonly object _lock = new();
        private bool _disposed;

        private static readonly TimeSpan kInitialRestartDelay = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan kMaxRestartDelay = TimeSpan.FromMinutes(5);
        private const string kLogTitle = "GraphQL Subscription";
        private TimeSpan _restartDelay = kInitialRestartDelay;
        private int _streamGeneration;
        private bool _restartPending;
        private CancellationTokenSource? _restartCancellation;

        public GraphQlApiSubscription(ApiConnection apiConnection, GraphQLHttpClient graphQlClient, GraphQLRequest request, Action<Exception> exceptionHandler, SubscriptionUpdate onUpdate)
        {
            _graphQlClient = graphQlClient;
            Request = request;

            OnUpdate += onUpdate;
            ExternalExceptionHandler = exceptionHandler;

            CreateSubscription();
        }

        private Action<Exception> ExternalExceptionHandler { get; }

        protected virtual void CreateSubscription()
        {
            InitializeSubscription(_graphQlClient);
        }

        internal override void Rebind(GraphQLHttpClient graphQlClient)
        {
            InitializeSubscription(graphQlClient);
        }

        private void InitializeSubscription(GraphQLHttpClient graphQlClient)
        {
            lock (_lock)
            {
                if (_disposed) return;

                _graphQlClient = graphQlClient;
                _subscription?.Dispose();
                _subscription = null;
                CancelPendingRestart();
                int generation = ++_streamGeneration;

                Log.WriteDebug("API", $"Creating API subscription {Request.OperationName}.");
                Action<Exception> subscriptionExceptionHandler = HandleSubscriptionException;
                _subscriptionStream = CreateSubscriptionStream(graphQlClient, subscriptionExceptionHandler);
                Log.WriteDebug("API", "API subscription created.");

                _subscription = _subscriptionStream.Subscribe(
                    response => Subscribe(response, generation),
                    () => ScheduleRestart(generation, "the stream was completed by the server"));
            }
        }

        private void Subscribe(GraphQLResponse<dynamic> response, int generation)
        {
            if (_disposed) return;

            if (ApiConstants.UseSystemTextJsonSerializer)
            {
                throw new NotImplementedException("System.Text.Json is not supported anymore.");
            }

            try
            {
                // Data == null: the server ended the subscription with an error, e.g. an expired JWT or a lost database
                // connection. Without a new subscription the subscriber would silently keep outdated data.
                if (response.Data == null)
                {
                    lock (_lock)
                    {
                        if (generation == _streamGeneration)
                        {
                            _subscription?.Dispose();
                            _subscription = null;
                        }
                    }
                    ScheduleRestart(generation, $"the server sent no data: {DescribeErrors(response.Errors)}");
                }
                else
                {
                    ResetRestartDelay(generation);
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

        /// <summary>
        /// Starts a new subscription after a delay that doubles with each attempt, when the stream given by the generation ended.
        /// </summary>
        /// <param name="generation">Generation of the stream that ended; ignored when a newer stream exists.</param>
        /// <param name="reason">Why the stream ended, for the log.</param>
        private void ScheduleRestart(int generation, string reason)
        {
            TimeSpan delay;
            CancellationToken cancellationToken;
            lock (_lock)
            {
                if (_disposed || _restartPending || generation != _streamGeneration)
                {
                    return;
                }
                _restartPending = true;
                delay = _restartDelay;
                _restartDelay = _restartDelay * 2 < kMaxRestartDelay ? _restartDelay * 2 : kMaxRestartDelay;
                _restartCancellation = new CancellationTokenSource();
                cancellationToken = _restartCancellation.Token;
            }

            Log.WriteWarning(kLogTitle, $"Subscription {Request.OperationName} ended because {reason}. Starting a new subscription in {delay.TotalSeconds} seconds.");
            _ = RestartAfterDelayAsync(generation, delay, cancellationToken);
        }

        private async Task RestartAfterDelayAsync(int generation, TimeSpan delay, CancellationToken cancellationToken)
        {
            try
            {
                await WaitBeforeRestartAsync(delay, cancellationToken);
                lock (_lock)
                {
                    if (_disposed || cancellationToken.IsCancellationRequested || generation != _streamGeneration)
                    {
                        return;
                    }
                    InitializeSubscription(_graphQlClient);
                }
            }
            catch (OperationCanceledException)
            {
                // a rebind or the disposal took over
            }
            catch (Exception exception)
            {
                Log.WriteError(kLogTitle, $"Subscription {Request.OperationName} could not be started again.", exception);
            }
        }

        /// <summary>
        /// Waits before a new subscription is started; overridable so tests need not wait.
        /// </summary>
        /// <param name="delay">Time to wait.</param>
        /// <param name="cancellationToken">Cancelled by a rebind or the disposal.</param>
        protected virtual Task WaitBeforeRestartAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            return Task.Delay(delay, cancellationToken);
        }

        /// <summary>
        /// Cancels a scheduled restart; the caller holds the lock.
        /// </summary>
        private void CancelPendingRestart()
        {
            _restartCancellation?.Cancel();
            _restartCancellation?.Dispose();
            _restartCancellation = null;
            _restartPending = false;
        }

        private void ResetRestartDelay(int generation)
        {
            lock (_lock)
            {
                if (generation == _streamGeneration)
                {
                    _restartDelay = kInitialRestartDelay;
                }
            }
        }

        private static string DescribeErrors(GraphQLError[]? errors)
        {
            return errors == null || errors.Length == 0 ? "no error given" : string.Join("; ", errors.Select(error => error.Message));
        }

        protected virtual IObservable<GraphQLResponse<dynamic>> CreateSubscriptionStream(GraphQLHttpClient graphQlClient, Action<Exception> exceptionHandler)
        {
            return graphQlClient.CreateSubscriptionStream<dynamic>(Request, exceptionHandler);
        }

        private void HandleSubscriptionException(Exception exception)
        {
            if (IsDisposed)
            {
                return;
            }

            ExternalExceptionHandler(exception);
        }

        protected override void Dispose(bool disposing)
        {
            if (!disposing) return;

            lock (_lock)
            {
                if (_disposed) return;
                _disposed = true;
                CancelPendingRestart();
                _subscription?.Dispose();
                _subscription = null;
                OnUpdate = null;
            }
        }
    }
}
