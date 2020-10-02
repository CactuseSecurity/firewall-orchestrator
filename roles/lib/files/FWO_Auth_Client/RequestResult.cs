using FWO_Logging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace FWO.Auth.Client
{
    public class RequestResult
    {
        private Dictionary<string, JsonElement> resultsJson;

        internal RequestResult(Dictionary<string, JsonElement> resultsJson = null)
        {
            this.resultsJson = resultsJson;
        }

        public Exception GetError()
        {
            // TODO: Handle Client Side and Server Side Error
            throw new NotImplementedException();
        }

        internal void SetClientSideError(string message, Exception error)
        {
            // TODO: Allow Client Side to add Errors 
            throw new NotImplementedException();
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
