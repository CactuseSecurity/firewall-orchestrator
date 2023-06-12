using GraphQL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using FWO.Api.Client;
using Newtonsoft.Json.Linq;
using FWO.Logging;
using GraphQL.Client.Abstractions;
using GraphQL.Client.Http;

namespace FWO.Api.Client
{
    public class ApiSubscription<SubscriptionResponseType> : IDisposable
    {
        public delegate void SubscriptionUpdate(SubscriptionResponseType reponse);
        public event SubscriptionUpdate OnUpdate;

        private IObservable<GraphQLResponse<dynamic>> subscriptionStream;
        private IDisposable subscription;
        private readonly GraphQLHttpClient graphQlClient;
        private readonly GraphQLRequest request;
        private readonly Action<Exception> internalExceptionHandler;

        public ApiSubscription(ApiConnection apiConnection, GraphQLHttpClient graphQlClient, GraphQLRequest request, Action<Exception> exceptionHandler, SubscriptionUpdate OnUpdate)
        {
            this.OnUpdate = OnUpdate;
            this.graphQlClient = graphQlClient;
            this.request = request;

            // handle subscription terminating exceptions
            internalExceptionHandler = (Exception exception) =>
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

            apiConnection.OnAuthHeaderChanged += ApiConnectionOnAuthHeaderChanged;
        }

        private void CreateSubscription()
        {
            Log.WriteDebug("API", $"Creating API subscription {request.OperationName}.");
            subscriptionStream = graphQlClient.CreateSubscriptionStream<dynamic>(request, internalExceptionHandler);
            Log.WriteDebug("API", "API subscription created.");

            subscription = subscriptionStream.Subscribe(response =>
            {
                if (ApiConstants.UseSystemTextJsonSerializer)
                {
                    JsonElement.ObjectEnumerator responseObjectEnumerator = response.Data.EnumerateObject();
                    responseObjectEnumerator.MoveNext();
                    SubscriptionResponseType returnValue = JsonSerializer.Deserialize<SubscriptionResponseType>(responseObjectEnumerator.Current.Value.GetRawText()) ??
                    throw new Exception($"Could not convert result from Json to {nameof(SubscriptionResponseType)}.\nJson: {responseObjectEnumerator.Current.Value.GetRawText()}"); ;
                    OnUpdate(returnValue);
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
                            subscription.Dispose();
                        }
                        else
                        {
                            JObject data = (JObject)response.Data;
                            JProperty prop = (JProperty)(data.First ?? throw new Exception($"Could not retrieve unique result attribute from Json.\nJson: {response.Data}"));
                            JToken result = prop.Value;
                            SubscriptionResponseType returnValue = result.ToObject<SubscriptionResponseType>() ?? throw new Exception($"Could not convert result from Json to {typeof(SubscriptionResponseType)}.\nJson: {response.Data}");
                            OnUpdate(returnValue);
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

        private void ApiConnectionOnAuthHeaderChanged(object? sender, string jwt)
        {
            subscription.Dispose();
            CreateSubscription();
        }

        public void Dispose()
        {
            subscription.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
