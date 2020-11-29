using FWO.Logging;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace FWO.Middleware.Client
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

        public virtual async Task<AuthServerResponse> SendRequest(Dictionary<string, object> parameters, string request)
        {
            AuthServerResponse result;

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
                result = new AuthServerResponse(httpResponse.StatusCode, jsonResults);
            }
            catch (Exception exception)
            {
                Log.WriteError($"Request \"{GetType().Name}\"",
                    $"An error occured while sending request \"{GetType().Name}\".",
                    exception);

                // Inform requester about errors
                result = new AuthServerResponse(HttpStatusCode.BadRequest, "An error occured while sending request.");
            }

            // Return result
            return result;
        }
    }
}
