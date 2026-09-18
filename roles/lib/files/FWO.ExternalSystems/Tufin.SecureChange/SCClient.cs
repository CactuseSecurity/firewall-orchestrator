using FWO.Api.Client;
using FWO.Data;
using FWO.Logging;
using RestSharp;
using System.Text;

namespace FWO.ExternalSystems.Tufin.SecureChange
{
    public class SCClient : RestApiClient
    {
        readonly ExternalTicketSystem TicketSystem;

        // checkCertificates: false keeps the behaviour this client has always had - a
        // SecureChange installation typically presents its own enterprise or self-signed
        // certificate, which no FWO host trusts. Only the FWO internal REST leg validates
        // by default.
        public SCClient(ExternalTicketSystem ticketSystem) : base(ticketSystem.Url, ticketSystem.ResponseTimeout, checkCertificates: false)
        {
            TicketSystem = ticketSystem;
        }

        public virtual async Task<RestResponse<int>> RestCall(RestRequest request, string restEndPoint)
        {
            request.AddHeader("Content-Type", "application/json");
            request.AddHeader("Authorization", TicketSystem.Authorization);
            request.AddHeader("Accept", "application/json");

            // Debugging SecureChange API call
            Log.WriteDebug("API", DebugApiCallText(request, restClient, restEndPoint));

            // send API call
            return await restClient.ExecuteAsync<int>(request);
        }

        private static string DebugApiCallText(RestRequest request, RestClient restClient, string restEndPoint)
        {
            StringBuilder headers = new();
            string body = "";
            foreach (Parameter p in request.Parameters)
            {
                if (p.Name == "")
                {
                    body = $"data: '{p.Value}'";
                }
                else if (p.Name != "Authorization") // avoid logging of credentials
                {
                    headers.AppendLine($"header: '{p.Name}: {p.Value}' ");
                }
            }
            return $"Sending API Call to SecureChange:\nrequest: {request.Method}, base url: {restClient.Options.BaseUrl}, restEndpoint: {restEndPoint}, body: {body}, {headers}";
        }
    }
}
