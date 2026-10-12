using FWO.Api.Client;
using FWO.Basics.Exceptions;
using FWO.Data;
using FWO.Encryption;
using FWO.Logging;
using RestSharp;
using System.Net;
using System.Text;
using System.Text.Json.Serialization;

namespace FWO.ExternalSystems.CheckPoint
{
    public class CheckPointClient : RestApiClient
    {
        private readonly Management Management;

        private string? SessionId;
        public string? CurrentSessionId => SessionId;

        /// <summary>
        /// Creates a client for the CheckPoint management API addressed by the ticket system and management.
        /// </summary>
        /// <param name="ticketSystem">The ticket system, whose url is used when the management has no hostname.</param>
        /// <param name="management">The management the request is sent to.</param>
        /// <param name="certificateChecks">The certificate checking switches per connection type.</param>
        public CheckPointClient(ExternalTicketSystem ticketSystem, Management management, ExternalCertificateChecks certificateChecks)
            : base(BuildBaseUrl(ticketSystem, management), ticketSystem.ResponseTimeout, checkCertificates: ResolveCheckCertificates(management, certificateChecks))
        {
            Management = management;
        }

        /// <summary>
        /// Takes the certificate checking switch of the connection type the base url is built from.
        /// </summary>
        /// <param name="management">The management the request is sent to; without a hostname the ticket system url is used.</param>
        /// <param name="certificateChecks">The certificate checking switches per connection type.</param>
        /// <returns>True when the server certificate has to be checked.</returns>
        public static bool ResolveCheckCertificates(Management management, ExternalCertificateChecks certificateChecks)
        {
            return string.IsNullOrWhiteSpace(management.Hostname) ? certificateChecks.TicketSystems : certificateChecks.FirewallConnections;
        }

        private static string BuildBaseUrl(ExternalTicketSystem ticketSystem, Management management)
        {
            if (string.IsNullOrWhiteSpace(management.Hostname))
            {
                return ticketSystem.Url;
            }

            string host = management.Hostname.Trim();

            if (host.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                host.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                Uri uri = new(host);
                int uriPort = uri.IsDefaultPort ? 443 : uri.Port;
                return $"https://{uri.Host}:{uriPort}/web_api/";
            }

            int port = management.Port > 0 ? management.Port : 443;
            return $"https://{host}:{port}/web_api/";
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

        /// <summary>
        /// Discards the current Check Point session changes if a session is active.
        /// </summary>
        public virtual async Task Discard()
        {
            if (string.IsNullOrWhiteSpace(SessionId))
            {
                return;
            }

            try
            {

                var request = new RestRequest("discard", Method.Post);
                request.AddHeader("Content-Type", "application/json");
                request.AddHeader("Accept", "application/json");
                request.AddHeader("X-chkp-sid", SessionId);
                request.AddStringBody("{}", ContentType.Json);

                var response = await restClient.ExecuteAsync<int>(request);

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    Log.WriteWarning("CheckPointClient", "Discard", "CheckPoint discard failed: " + response.Content, "", (int)response.StatusCode);
                }
            }
            catch (Exception exception)
            {
                Log.WriteWarning("CheckPointClient", "Discard", "CheckPoint discard threw an exception: " + exception.Message, "", 0);
            }
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
                    Log.WriteWarning("CheckPointClient", "Logout", "CheckPoint logout failed: " + response.Content, "", (int)response.StatusCode);
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
            return Management.ExportCredential != null
                && !string.IsNullOrWhiteSpace(Management.ExportCredential.ImportUser)
                && !string.IsNullOrWhiteSpace(Management.ExportCredential.Secret);
        }

        private void AddAuthHeader(RestRequest request)
        {
            if (string.IsNullOrWhiteSpace(SessionId))
            {
                throw new ProcessingFailedException("CheckPoint session missing.");
            }

            request.AddHeader("X-chkp-sid", SessionId);
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
            [JsonConstructor]
            public LoginResponse(string sid)
            {
                Sid = sid;
            }
            public string Sid { get; }
        }
    }
}
