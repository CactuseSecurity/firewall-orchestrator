using FWO.Basics.Exceptions;
using FWO.Data;
using RestSharp;
using System.Text;
using System.Text.Json.Nodes;

namespace FWO.ExternalSystems.FirewallWrite
{
    /// <summary>
    /// OPNsense API access with API key (user) and secret of the export credential as basic authentication. Resolves
    /// alias UUIDs by name and merges group member changes into the current alias content.
    /// </summary>
    public class OpnSenseWriteClient : FirewallWriteClient
    {
        private const string kAliasPath = "/api/firewall/alias";
        private const string kMemberSeparator = "\n";

        private string? basicAuthentication;

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
            request.AddHeader("Authorization", $"Basic {basicAuthentication ?? throw new ProcessingFailedException("OPNsense API key missing.")}");
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
            return content?["validations"] == null && result != "failed";
        }

        /// <inheritdoc/>
        protected override async Task ResolveRuntimeValues(RenderedFirewallRequest request)
        {
            Dictionary<string, FirewallPlaceholderValue> values = [];
            if (FirewallTemplateRenderer.Contains(request, FirewallWritePlaceholders.ObjectUuid) ||
                FirewallTemplateRenderer.Contains(request, FirewallWritePlaceholders.MergedMembers))
            {
                string uuid = await LookupAliasUuid(request.Step.TargetName);
                values[FirewallWritePlaceholders.ObjectUuid] = new(uuid, false);
                if (FirewallTemplateRenderer.Contains(request, FirewallWritePlaceholders.MergedMembers))
                {
                    List<string> merged = FirewallMemberMerge.Merge(await ReadAliasContent(uuid), request.Step);
                    values[FirewallWritePlaceholders.MergedMembers] = new(string.Join(kMemberSeparator, merged), false);
                }
                FirewallTemplateRenderer.ResolveRuntimePlaceholders(request, values);
            }
            await base.ResolveRuntimeValues(request);
        }

        private async Task<string> LookupAliasUuid(string name)
        {
            RestResponse response = await SendAuthenticated(new RestRequest($"{kAliasPath}/getAliasUUID/{Uri.EscapeDataString(name)}", Method.Get));
            string? uuid = response.IsSuccessful && ParseJson(response.Content) is JsonObject content ? content["uuid"]?.GetValue<string>() : null;
            return string.IsNullOrWhiteSpace(uuid)
                ? throw new ProcessingFailedException($"OPNsense alias '{name}' not found: {(int)response.StatusCode} {response.Content}")
                : uuid;
        }

        /// <summary>Reads the selected entries of the content of an alias.</summary>
        private async Task<List<string>> ReadAliasContent(string uuid)
        {
            RestResponse response = await SendAuthenticated(new RestRequest($"{kAliasPath}/getItem/{Uri.EscapeDataString(uuid)}", Method.Get));
            if (!response.IsSuccessful)
            {
                throw new ProcessingFailedException($"OPNsense could not read alias '{uuid}': {(int)response.StatusCode} {response.Content}");
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
