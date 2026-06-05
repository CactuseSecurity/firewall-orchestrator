using FWO.Api.Client;
using FWO.Basics.Exceptions;
using FWO.Data;
using FWO.Encryption;
using FWO.Logging;
using RestSharp;
using System.Net;
using System.Text;

namespace FWO.ExternalSystems.CheckPoint
{
    public class CheckPointClient : RestApiClient
    {
        private readonly ExternalTicketSystem TicketSystem;
        private readonly Management Management;

        private string? SessionId;

        public CheckPointClient(ExternalTicketSystem ticketSystem, Management management)
            : base(ticketSystem.Url, ticketSystem.ResponseTimeout)
        {
            TicketSystem = ticketSystem;
            Management = management;
        }

        // =========================================================
        // PUBLIC API
        // =========================================================

        public async Task LoginIfNeeded()
        {
            if (!HasLoginCredentials())
            {
                throw new ProcessingFailedException(
                    "CheckPoint login credentials missing. Configure export credentials on the management.");
            }

            if (!string.IsNullOrWhiteSpace(SessionId))
            {
                return;
            }

            await Login();
        }

        public virtual async Task Logout()
        {
            if (string.IsNullOrWhiteSpace(SessionId))
            {
                return;
            }

            try
            {
                var request = new RestRequest("logout", Method.Post);
                request.AddHeader("Content-Type", "application/json");
                request.AddHeader("Accept", "application/json");
                request.AddHeader("X-chkp-sid", SessionId);

                var response = await restClient.ExecuteAsync<int>(request);

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    Log.WriteWarning(
                        "CheckPointClient",
                        "Logout",
                        "CheckPoint logout failed: " + response.Content,
                        "",
                        (int)response.StatusCode
                    );
                }
            }
            finally
            {
                SessionId = null;
            }
        }

        public virtual async Task<RestResponse<int>> RestCall(RestRequest request, string restEndPoint)
        {
            request.AddHeader("Content-Type", "application/json");
            request.AddHeader("Accept", "application/json");

            await LoginIfNeeded();

            AddAuthHeader(request);

            Log.WriteDebug("API", DebugApiCallText(request, restClient, restEndPoint));

            RestResponse<int> response = await restClient.ExecuteAsync<int>(request);
            return response;
        }

        // =========================================================
        // AUTH / SESSION
        // =========================================================

        private async Task Login()
        {
            var request = new RestRequest("/login", Method.Post);

            request.AddHeader("Content-Type", "application/json");
            request.AddHeader("Accept", "application/json");

            string password = AesEnc.TryDecrypt(
                Management.ExportCredential.Secret,
                true,
                "CheckPointClient",
                $"Could not decrypt secret in export credential '{Management.ExportCredential.Name}'.",
                true
            );

            request.AddJsonBody(new
            {
                user = Management.ExportCredential.ImportUser,
                password = password
            });

            var response = await restClient.ExecuteAsync<LoginResponse>(request);

            if (response.StatusCode != HttpStatusCode.OK || response.Data?.Sid == null)
            {
                throw new ProcessingFailedException(
                    $"CheckPoint login failed: {response.StatusCode} {response.Content}");
            }

            SessionId = response.Data.Sid;
        }

        private bool HasLoginCredentials()
        {
            return !string.IsNullOrWhiteSpace(Management.ExportCredential.ImportUser)
                && !string.IsNullOrWhiteSpace(Management.ExportCredential.Secret);
        }

        private void AddAuthHeader(RestRequest request)
        {
            if (!string.IsNullOrWhiteSpace(SessionId))
            {
                request.AddHeader("X-chkp-sid", SessionId);
                return;
            }

            var auth = TicketSystem.Authorization;

            if (string.IsNullOrWhiteSpace(auth))
            {
                return;
            }

            request.AddHeader("Authorization", auth.Trim());
        }

        // =========================================================
        // DEBUGGING
        // =========================================================

        private static string DebugApiCallText(RestRequest request, RestClient restClient, string restEndPoint)
        {
            StringBuilder headers = new();
            string body = "";

            foreach (var parameter in request.Parameters)
            {
                if (parameter.Name == "")
                {
                    body = $"data: '{parameter.Value}'";
                }
                else if (parameter.Name != "Authorization" && parameter.Name != "X-chkp-sid")
                {
                    headers.AppendLine($"header: '{parameter.Name}: {parameter.Value}' ");
                }
            }

            return $"""
                Sending API Call to CheckPoint:
                request: {request.Method}
                base url: {restClient.Options.BaseUrl}
                restEndpoint: {restEndPoint}
                body: {body}
                {headers}
                """;
        }

        // =========================================================
        // INTERNAL TYPES
        // =========================================================

        private sealed class LoginResponse
        {
            public required string Sid { get; set; }
        }
    }
}
