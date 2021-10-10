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
        readonly string middlewareServerUri;

        public RequestSender(string middlewareServerUri)
        {

            var handler = new HttpClientHandler();
            handler.ClientCertificateOptions = ClientCertificateOption.Manual;
            handler.ServerCertificateCustomValidationCallback = 
                (httpRequestMessage, cert, cetChain, policyErrors) =>
            {
                return true;
            };

            httpClient = new HttpClient(handler);


            this.middlewareServerUri = middlewareServerUri;
        }

        public virtual async Task<MiddlewareServerResponse> SendRequest(Dictionary<string, object> parameters, string request, string jwt = null)
        {
            MiddlewareServerResponse result;

            try
            {
                if (jwt != null)
                {
                    httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("auth", jwt);
                }

                // Wrap parameters
                string wrappedParameters = JsonSerializer.Serialize(parameters);
                StringContent requestContent = new StringContent(wrappedParameters);

                // Send request, Receive answer
                // sanitize uri
                string uriToCall = middlewareServerUri;
                if (middlewareServerUri[middlewareServerUri.Length-1] != '/')
                    uriToCall += "/";
                uriToCall += request;
                if (request[request.Length-1] != '/')
                    uriToCall += "/";

                HttpResponseMessage httpResponse = await httpClient.PostAsync(uriToCall, requestContent);
                
                // Unwrap result
                string wrappedResult = await httpResponse.Content.ReadAsStringAsync();
                Dictionary<string, JsonElement> jsonResults = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(wrappedResult);
                result = new MiddlewareServerResponse(httpResponse.StatusCode, jsonResults);
            }
            catch (Exception exception)
            {
                Log.WriteError($"Request \"{request}\"",
                    $"An error occured while sending request \"{request}\".",
                    exception);

                // Inform requester about errors
                result = new MiddlewareServerResponse(HttpStatusCode.BadRequest, "An error occured while sending request.");
            }

            // Return result
            return result;
        }
    }
}
