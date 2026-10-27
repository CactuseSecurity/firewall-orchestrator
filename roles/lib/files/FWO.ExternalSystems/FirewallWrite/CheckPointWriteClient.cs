using FWO.Basics.Exceptions;
using FWO.Data;
using FWO.Data.DeviceSpecificSettings;
using FWO.Logging;
using RestSharp;
using System.Text.Json.Nodes;

namespace FWO.ExternalSystems.FirewallWrite
{
    /// <summary>
    /// Check Point management API session: login (into the domain of an MDS management), session id header, discard on
    /// error and logout. Object creation that fails only because of a duplicate address warning is retried with
    /// ignore-warnings, like the existing Check Point ticket handling does.
    /// </summary>
    public class CheckPointWriteClient : FirewallWriteClient
    {
        private const string kSessionHeader = "X-chkp-sid";
        private const string kIgnoreWarnings = "ignore-warnings";

        private static readonly List<FirewallWriteOperation> RetryableOperations =
            [FirewallWriteOperation.HostCreate, FirewallWriteOperation.NetworkCreate, FirewallWriteOperation.AddressRangeCreate];
        private static readonly List<string> DuplicateAddressWarnings =
        [
            "multiple IP", "same IP address", "same IPv4 address", "same IPv6 address", "More than one object has the same",
            "More than one network has the same", "More than one host has the same", "More than one address range has the same"
        ];

        private string? sessionId;

        /// <summary>Creates a client for the web_api of a Check Point management.</summary>
        public CheckPointWriteClient(Management management, bool checkCertificates)
            : base(HostUrl(management, "/web_api/"), management, checkCertificates)
        { }

        /// <inheritdoc/>
        public override async Task Login()
        {
            (string user, string password) = ExportCredentials();
            JsonObject body = new() { ["user"] = user, ["password"] = password };
            string domain = !string.IsNullOrWhiteSpace(Management.DomainUid) ? Management.DomainUid : Management.ConfigPath ?? "";
            if (!string.IsNullOrWhiteSpace(domain))
            {
                body["domain"] = domain;
            }

            RestRequest request = new("login", Method.Post);
            request.AddStringBody(body.ToJsonString(), ContentType.Json);
            RestResponse response = await Send(request);
            sessionId = ParseJson(response.Content)?["sid"]?.GetValue<string>();
            if (!response.IsSuccessful || string.IsNullOrWhiteSpace(sessionId))
            {
                throw new ProcessingFailedException($"Check Point login to '{Management.Name}' failed: {(int)response.StatusCode} {response.Content}");
            }
        }

        /// <inheritdoc/>
        public override async Task Discard() => await SessionCall("discard");

        /// <inheritdoc/>
        public override async Task Logout()
        {
            await SessionCall("logout");
            sessionId = null;
        }

        /// <inheritdoc/>
        public override async Task<FirewallStepResult> Execute(RenderedFirewallRequest request)
        {
            FirewallStepResult result = await base.Execute(request);
            if (result.Success || !RetryableOperations.Contains(request.Operation) || !IsDuplicateAddressWarning(result.Message))
            {
                return result;
            }

            JsonNode? body = ParseJson(request.Body);
            if (body is not JsonObject bodyObject || bodyObject.ContainsKey(kIgnoreWarnings))
            {
                return result;
            }
            Log.WriteInfo("Firewall Write", $"Retrying {request.Operation} on '{request.Step.TargetName}' with {kIgnoreWarnings}.");
            bodyObject[kIgnoreWarnings] = true;
            request.Body = bodyObject.ToJsonString();
            RestResponse retryResponse = await SendRendered(request);
            return ToResult(request, retryResponse, IsSuccess(retryResponse));
        }

        /// <inheritdoc/>
        protected override void AddAuthentication(RestRequest request)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                throw new ProcessingFailedException("Check Point session missing.");
            }
            request.AddHeader(kSessionHeader, sessionId);
        }

        private static bool IsDuplicateAddressWarning(string content) =>
            DuplicateAddressWarnings.Any(warning => content.Contains(warning, StringComparison.OrdinalIgnoreCase));

        /// <summary>Sends a session call; failures are only logged, because they run during cleanup.</summary>
        private async Task SessionCall(string command)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                return;
            }
            try
            {
                RestRequest request = new(command, Method.Post);
                request.AddStringBody("{}", ContentType.Json);
                RestResponse response = await SendAuthenticated(request);
                if (!response.IsSuccessful)
                {
                    Log.WriteWarning("Firewall Write", $"Check Point {command} failed: {(int)response.StatusCode} {response.Content}");
                }
            }
            catch (Exception exception)
            {
                Log.WriteWarning("Firewall Write", $"Check Point {command} threw an exception: {exception.Message}");
            }
        }
    }
}
