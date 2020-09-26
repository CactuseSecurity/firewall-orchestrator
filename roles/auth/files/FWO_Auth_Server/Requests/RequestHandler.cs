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
        protected Dictionary<string, object> Parameters;
        public List<Ldap> Ldaps { get; set; }

        protected abstract string HandleRequest(HttpListenerRequest request);

        protected Dictionary<string, object> GetRequestParameters(HttpListenerRequest request)
        {
            Log.WriteDebug("Request Parameters", "Trying to read request parameters...");

            try
            {
                string ParametersJson = new StreamReader(request.InputStream).ReadToEnd();
                Dictionary<string, object> Parameters = JsonSerializer.Deserialize<Dictionary<string, object>>(ParametersJson);

                Log.WriteDebug("Request Parameters", "Request Parameters successfully read.");
                return Parameters;
            }
            catch (Exception ex)
            {
                throw new ArgumentException("Request Parameters could not be read.", ex);
            }
        }
    }
}
