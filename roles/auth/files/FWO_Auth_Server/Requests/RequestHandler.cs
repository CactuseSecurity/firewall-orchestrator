using FWO_Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;

namespace FWO_Auth_Server.Requests
{
    abstract class RequestHandler
    {
        /// <summary>
        /// Parameters of request
        /// </summary>
        protected Dictionary<string, object> Parameters;

        /// <summary>
        /// Connected Ldaps to handle requests
        /// </summary>
        protected List<Ldap> Ldaps;

        /// <summary>
        /// Calls <see cref="GetRequestParameters(HttpListenerRequest)"/> to get request parameters.
        /// Then calls <see cref="HandleRequestInternal(HttpListenerRequest)"/> to handle request. Catches errors wraps them and sends them back.
        /// </summary>
        /// <param name="request">The request to handle.</param>
        /// <returns>(Status of request, Result wrapped in dictonary as Json / Errors wrapped in dictonary as Json)</returns>
        public virtual (HttpStatusCode status, string wrappedResult) HandleRequest(HttpListenerRequest request)
        { 
            try
            {            
                return HandleRequestInternal(request);
            }
            catch (Exception exception)
            {
                Log.WriteError($"Request \"{GetType().Name}\"",
                    $"An error occured while handling request \"{GetType().Name}\". \nSending error to requester.",
                    exception);

                return WrapResult(HttpStatusCode.BadRequest, ("error", exception));
            }
        }

        /// <summary>
        /// Handles the given <paramref name="request"/> with <see cref="Parameters"/> already extracted by <see cref="HandleRequest(HttpListenerRequest)"/>. 
        /// </summary>
        /// <param name="request">Request to handle.</param>
        /// <returns>(Status of request, Result wrapped in dictonary as json)</returns>
        protected abstract (HttpStatusCode status, string wrappedResult) HandleRequestInternal(HttpListenerRequest request);

        /// <summary>
        /// Exctracts Parameters from <paramref name="request"/>. Tries to convert them from Json to <c>Dictionary</c>.
        /// </summary>
        /// <param name="request">Request to extract parameters from.</param>
        /// <returns> Parameters as <c>Dictionary</c> </returns>
        protected Dictionary<string, object> GetRequestParameters(HttpListenerRequest request, params string[] expectedParameters)
        {
            Log.WriteDebug("Request Parameters", "Trying to read request parameters...");

            try
            {
                string parametersJson = new StreamReader(request.InputStream).ReadToEnd();
                Dictionary<string, object> parameters = JsonSerializer.Deserialize<Dictionary<string, object>>(parametersJson);

                foreach (string expectedParameter in expectedParameters)
                {
                    if (parameters.ContainsKey(expectedParameter) == false)
                    {
                        throw new ArgumentException($"Expected request parameter \"{expectedParameter}\" could not be found.");
                    }
                }

                Log.WriteDebug("Request Parameters", "Request Parameters successfully read.");
                return parameters;
            }
            catch (Exception ex)
            {
                throw new ArgumentException("Request Parameters could not be read.", ex);
            }
        }

        /// <summary>
        /// Wraps <paramref name="result"/> into a <c>Dictionary</c>. Serializes it to Json.
        /// </summary>
        /// <param name="status">Status of result.</param>
        /// <param name="result">Result to wrap.</param>
        /// <returns><paramref name="result"/> wrapped in <c>Dictionary</c> serialized to Json.</returns>
        protected (HttpStatusCode status, string wrappedResult) WrapResult(HttpStatusCode status, params (string key, object value)[] result)
        {
            Log.WriteDebug("Warp Result", $"Wrapping Result: \n {string.Join("\n", result)}");

            Dictionary<string, object> resultWrapper = new Dictionary<string, object>();

            foreach ((string key, object value) in result)
            {
                resultWrapper.Add(key, value);
            }

            return (status, JsonSerializer.Serialize(resultWrapper));
        }
    }
}
