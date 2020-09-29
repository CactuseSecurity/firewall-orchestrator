using FWO_Logging;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace FWO_Auth_Client.Requests
{
    class RequestSender
    {
        readonly HttpClient httpClient;
        readonly string authServerUri;

        public RequestSender(string authServerUri)
        {
            httpClient = new HttpClient();
            this.authServerUri = authServerUri;
        }

        public virtual async Task<(HttpStatusCode status, Dictionary<string, object> result)> SendRequest(Dictionary<string, object> parameters, string request)
        {
            try
            {
                // Wrap parameters
                string wrappedParameters = JsonSerializer.Serialize(parameters);
                StringContent requestContent = new StringContent(wrappedParameters);

                // Send request, Receive answer
                HttpResponseMessage httpResponse = await httpClient.PostAsync(authServerUri + $"/{request}/", requestContent);
                
                // Unwrap result
                string wrappedResult = await httpResponse.Content.ReadAsStringAsync();
                Dictionary<string, object> result = JsonSerializer.Deserialize<Dictionary<string, object>>(wrappedResult);

                // Return result
                return (httpResponse.StatusCode, result);
            }
            catch (Exception exception)
            {
                Log.WriteError($"Request \"{GetType().Name}\"",
                    $"An error occured while sending request \"{GetType().Name}\".",
                    exception);

                return (HttpStatusCode.BadRequest, new Dictionary<string, object> { { "error", exception } });
            }
        }
    }
}
