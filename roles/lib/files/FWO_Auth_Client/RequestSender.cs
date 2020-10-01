using FWO_Logging;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace FWO.Auth.Client
{
    internal class RequestSender
    {
        readonly HttpClient httpClient;
        readonly string authServerUri;

        public RequestSender(string authServerUri)
        {
            httpClient = new HttpClient();
            this.authServerUri = authServerUri;
        }

        public virtual async Task<(HttpStatusCode status, RequestResult result)> SendRequest(Dictionary<string, object> parameters, string request)
        {
            RequestResult result;

            try
            {
                // Wrap parameters
                string wrappedParameters = JsonSerializer.Serialize(parameters);
                StringContent requestContent = new StringContent(wrappedParameters);

                // Send request, Receive answer
                HttpResponseMessage httpResponse = await httpClient.PostAsync($"{authServerUri}/{request}/", requestContent);
                
                // Unwrap result
                string wrappedResult = await httpResponse.Content.ReadAsStringAsync();
                Dictionary<string, JsonElement> jsonResults = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(wrappedResult);
                result = new RequestResult(jsonResults);

                // Return result
                return (httpResponse.StatusCode, result);
            }
            catch (Exception exception)
            {
                Log.WriteError($"Request \"{GetType().Name}\"",
                    $"An error occured while sending request \"{GetType().Name}\".",
                    exception);

                result = new RequestResult();
                result.SetClientSideError("An error occured while sending request.", exception);
                return (HttpStatusCode.BadRequest, result);
            }
        }
    }
}
