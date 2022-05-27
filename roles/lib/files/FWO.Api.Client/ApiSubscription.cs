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

namespace FWO.Api.Client
{
    public class ApiSubscription<SubscriptionResponseType> : IDisposable
    {
        public delegate void SubscriptionUpdate(SubscriptionResponseType reponse);
        public event SubscriptionUpdate OnUpdate;

        private readonly IObservable<GraphQLResponse<dynamic>> subscriptionStream;
        private readonly IDisposable subscription;

        public ApiSubscription(IObservable<GraphQLResponse<dynamic>> subscriptionStream, SubscriptionUpdate OnUpdate)
        {
            this.subscriptionStream = subscriptionStream;
            this.OnUpdate = OnUpdate;

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
                        JObject data = (JObject)response.Data;
                        JProperty prop = (JProperty)(data.First ?? throw new Exception($"Could not retrieve unique result attribute from Json.\nJson: {response.Data}"));
                        JToken result = prop.Value;
                        SubscriptionResponseType returnValue = result.ToObject<SubscriptionResponseType>() ?? throw new Exception($"Could not convert result from Json to {typeof(SubscriptionResponseType)}.\nJson: {response.Data}");
                        OnUpdate(returnValue);
                    }
                    catch (Exception ex)
                    {
                        Log.WriteError("GraphQL Subscription", "Subscription lead to exception", ex);
                        throw;
                    }
                }
            });
        }

        public void Dispose()
        {
            subscription.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
