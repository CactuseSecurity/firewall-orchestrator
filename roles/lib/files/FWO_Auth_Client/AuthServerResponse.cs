using FWO.Logging;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Text.Json;

namespace FWO.Auth.Client
{
    public class AuthServerResponse
    {
        private Dictionary<string, JsonElement> resultsJson;

        public readonly HttpStatusCode Status;

        public string Error { get; internal set; }

        internal AuthServerResponse(HttpStatusCode status, Dictionary<string, JsonElement> resultsJson = null)
        {
            this.resultsJson = resultsJson;
            Status = status;

            try
            {
                if (resultsJson.ContainsKey("error"))
                {
                    Error = JsonSerializer.Deserialize<string>(resultsJson["error"].GetRawText());
                }
            }
            catch (Exception)
            {
                Error = "Server side error could not be read.";
            }
        }

        internal AuthServerResponse(HttpStatusCode status, string errorMessage)
        {
            Status = status;
            Error = errorMessage;
        }

        public ResultType GetResult<ResultType>(string resultName)
        {
            try
            {
                if (resultsJson.ContainsKey(resultName))
                {
                    return JsonSerializer.Deserialize<ResultType>(resultsJson[resultName].GetRawText());
                }
                else
                {
                    throw new NotSupportedException($"Result could not be found.");
                }
            }
            catch (Exception e)
            {
                throw new Exception($"Could not read result \"{resultName}\".", e);
            }
        }
    }
}
