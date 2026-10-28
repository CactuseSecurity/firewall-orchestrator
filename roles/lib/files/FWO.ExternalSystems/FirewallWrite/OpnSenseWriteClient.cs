using FWO.Data;
using RestSharp;
using System.Text;
using System.Text.Json.Nodes;

namespace FWO.ExternalSystems.FirewallWrite
{
    /// <summary>
    /// OPNsense API access with API key (user) and secret of the export credential as basic authentication. Resolves
    /// alias UUIDs by name and merges group member changes into the current alias content, checking right before the write
    /// that the content did not change in the meantime.
    /// </summary>
    public class OpnSenseWriteClient : FirewallWriteClient
    {
        private const string kAliasPath = "/api/firewall/alias";
        private const string kMemberSeparator = "\n";

        private const string kValidations = "validations";
        private const string kResultFailed = "failed";

        private string? basicAuthentication;

        /// <summary>Alias content read for a call that replaces it with merged members, checked again before the call is sent.</summary>
        private readonly Dictionary<RenderedFirewallRequest, (string Uuid, List<string> Content)> mergedFrom = [];

        /// <summary>Creates a client for the API of an OPNsense firewall.</summary>
        public OpnSenseWriteClient(Management management, bool checkCertificates)
            : base(HostUrl(management), management, checkCertificates)
        { }

        /// <inheritdoc/>
        public override Task Login()
        {
            (string key, string secret) = ExportCredentials();
            basicAuthentication = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{key}:{secret}"));
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        protected override void AddAuthentication(RestRequest request)
        {
            request.AddHeader("Authorization", $"Basic {basicAuthentication ?? throw new FirewallWriteException(FirewallWriteErrorKind.Configuration, "OPNsense API key missing.")}");
        }

        /// <summary>Failed if the HTTP call failed, the API reports validation errors or a failed result.</summary>
        protected override bool IsSuccess(RestResponse response)
        {
            if (!response.IsSuccessful)
            {
                return false;
            }
            JsonNode? content = ParseJson(response.Content);
            string? result = content?["result"]?.ToString();
            return content?[kValidations] == null && result != kResultFailed;
        }

        /// <summary>
        /// OPNsense answers validation errors with HTTP 200 and a "validations" object, other failures with result "failed";
        /// both mean the firewall rejected the content of the call.
        /// </summary>
        protected override (FirewallWriteErrorKind? Kind, string? NativeCode) ClassifyVendorError(JsonNode? content)
        {
            if (content?[kValidations] != null)
            {
                return (FirewallWriteErrorKind.Rejected, kValidations);
            }
            string? result = content?["result"]?.ToString();
            return result == kResultFailed ? (FirewallWriteErrorKind.Rejected, result) : (null, null);
        }

        /// <inheritdoc/>
        protected override async Task ResolveRuntimeValues(RenderedFirewallRequest request)
        {
            Dictionary<string, FirewallPlaceholderValue> values = [];
            if (FirewallTemplateRenderer.Contains(request, FirewallWritePlaceholders.ObjectUuid) ||
                FirewallTemplateRenderer.Contains(request, FirewallWritePlaceholders.MergedMembers))
            {
                string uuid = await LookupAliasUuid(request);
                values[FirewallWritePlaceholders.ObjectUuid] = new(uuid, false);
                if (FirewallTemplateRenderer.Contains(request, FirewallWritePlaceholders.MergedMembers))
                {
                    List<string> current = await ReadAliasContent(uuid, request);
                    mergedFrom[request] = (uuid, current);
                    List<string> merged = FirewallMemberMerge.Merge(current, request.Step);
                    values[FirewallWritePlaceholders.MergedMembers] = new(string.Join(kMemberSeparator, merged), false);
                }
                FirewallTemplateRenderer.ResolveRuntimePlaceholders(request, values);
            }
            await base.ResolveRuntimeValues(request);
        }

        /// <summary>
        /// The OPNsense API can only replace the whole alias content, without a condition. To keep a change made on the
        /// firewall in the meantime from being overwritten, the content is read again right before the call; if it differs
        /// from the content the members were merged into, the call is not sent and fails as a retryable conflict. A change
        /// between this last read and the call itself cannot be detected.
        /// </summary>
        protected override async Task BeforeSend(RenderedFirewallRequest request)
        {
            if (!mergedFrom.Remove(request, out (string Uuid, List<string> Content) read))
            {
                return;
            }
            List<string> now = await ReadAliasContent(read.Uuid, request);
            if (!new HashSet<string>(now).SetEquals(read.Content))
            {
                string message = $"OPNsense alias '{request.Step.TargetName}' was changed on the firewall in the meantime; it is not overwritten.";
                throw new FirewallWriteException(new FirewallWriteError(FirewallWriteErrorKind.Conflict, string.Join(kMemberSeparator, now),
                    Operation: request.Operation), message);
            }
        }

        private async Task<string> LookupAliasUuid(RenderedFirewallRequest request)
        {
            string name = request.Step.TargetName;
            RestResponse response = await SendAuthenticated(new RestRequest($"{kAliasPath}/getAliasUUID/{Uri.EscapeDataString(name)}", Method.Get));
            if (!response.IsSuccessful)
            {
                throw ReadFailure(response, request, $"the uuid of alias '{name}'");
            }
            string? uuid = ParseJson(response.Content) is JsonObject content ? content["uuid"]?.GetValue<string>() : null;
            return string.IsNullOrWhiteSpace(uuid)
                ? throw new FirewallWriteException(new FirewallWriteError(FirewallWriteErrorKind.NotFound, FirewallWriteErrors.NativeMessage(response),
                    (int)response.StatusCode, Operation: request.Operation), $"OPNsense alias '{name}' not found.")
                : uuid;
        }

        /// <summary>Reads the selected entries of the content of an alias.</summary>
        private async Task<List<string>> ReadAliasContent(string uuid, RenderedFirewallRequest request)
        {
            RestResponse response = await SendAuthenticated(new RestRequest($"{kAliasPath}/getItem/{Uri.EscapeDataString(uuid)}", Method.Get));
            if (!response.IsSuccessful)
            {
                throw ReadFailure(response, request, $"the content of alias '{uuid}'");
            }
            if (ParseJson(response.Content)?["alias"]?["content"] is not JsonObject content)
            {
                return [];
            }
            return [.. content
                .Where(entry => entry.Value?["selected"]?.ToString() is "1" or "true")
                .Select(entry => entry.Key)];
        }
    }
}
