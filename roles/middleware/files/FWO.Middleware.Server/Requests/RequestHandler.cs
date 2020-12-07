using FWO.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace FWO.Middleware.Server.Requests
{
    abstract class RequestHandler
    {
        /// <summary>
        /// Parameters of request
        /// </summary>
        private Dictionary<string, JsonElement> Parameters;

        /// <summary>
        /// Calls <see cref="GetRequestParameters(HttpListenerRequest)"/> to get request parameters.
        /// Then calls <see cref="HandleRequestInternalAsync(HttpListenerRequest)"/> to handle request. Catches errors wraps them and sends them back.
        /// </summary>
        /// <param name="request">The request to handle.</param>
        /// <returns>(Status of request, Result wrapped in dictonary as Json / Errors wrapped in dictonary as Json)</returns>
        public virtual async Task<(HttpStatusCode status, string wrappedResult)> HandleRequestAsync(HttpListenerRequest request)
        { 
            try
            {
                InitializeRequestParameters(request);
                return await HandleRequestInternalAsync(request);
            }
            catch (Exception exception)
            {
                Log.WriteError($"Request \"{GetType().Name}\"",
                    $"An error occured while handling request \"{GetType().Name}\". \nSending error to requester.",
                    exception);

                return WrapResult(HttpStatusCode.BadRequest, ("error", exception.Message));
            }
        }

        /// <summary>
        /// Handles the given <paramref name="request"/> with <see cref="Parameters"/> already extracted by <see cref="HandleRequest(HttpListenerRequest)"/>. 
        /// </summary>
        /// <param name="request">Request to handle.</param>
        /// <returns>(Status of request, Result wrapped in dictonary as json)</returns>
        protected abstract Task<(HttpStatusCode status, string wrappedResult)> HandleRequestInternalAsync(HttpListenerRequest request);

        /// <summary>
        /// Reads request parameters from <paramref name="request"/> as string. Converts them and save them in <see cref="Parameters"/>.
        /// </summary>
        /// <param name="request">Request to read parameters from.</param>
        private void InitializeRequestParameters(HttpListenerRequest request)
        {
            Log.WriteDebug("Request Parameters", "Trying to unwrap request parameters...");

            try
            {
                string parametersJson = new StreamReader(request.InputStream).ReadToEnd();
                Parameters = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(parametersJson);

                Log.WriteDebug("Request Parameters", "Request Parameters successfully unwrapped.");
            }
            catch (Exception ex)
            {
                throw new ArgumentException("Request Parameters could not be unwrapped.", ex);
            }
        }

        /// <summary>
        /// Exctracts Parameter with name: <paramref name="parameterName"/> and type: <typeparamref name="ParameterType"/> from <see cref="Parameters"/>.
        /// </summary>
        /// <typeparam name="ParameterType">Type of expected parameter</typeparam>
        /// <param name="parameterName">Name of expected parameter</param>
        /// <param name="notNull">True if parameter can be null, otherwise false.</param>
        /// <returns>Specified parameter (Name: <paramref name="parameterName"/>, Type: <typeparamref name="ParameterType"/>)</returns>
        protected ParameterType GetRequestParameter<ParameterType>(string parameterName, bool notNull = false)
        {
            Log.WriteDebug("Request Parameter", $"Trying to read request parameter \"{parameterName}\".");

            try
            {
                if (Parameters.ContainsKey(parameterName))
                {                   
                    ParameterType parameter = JsonSerializer.Deserialize<ParameterType>(Parameters[parameterName].GetRawText());
                    if (parameter == null && notNull == true)
                    {
                        throw new ArgumentNullException("Expected request parameter to not be null");
                    }
                    Log.WriteDebug("Request Parameters", $"Request Parameter \"{parameterName}\" successfully read.");
                    return parameter;
                }
                else
                {
                    throw new ArgumentException("Expected request parameter could not be found.");
                }
            }
            catch (Exception ex)
            {
                throw new ArgumentException($"Request Parameters \"{parameterName}\" could not be read.", ex);
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
            Log.WriteDebug("Wrap result", $"Wrapping Result: \n {string.Join("\n", result)}");

            Dictionary<string, object> resultWrapper = new Dictionary<string, object>();

            foreach ((string key, object value) in result)
            {
                resultWrapper.Add(key, value);
            }

            return (status, JsonSerializer.Serialize(resultWrapper));
        }
    }
}
