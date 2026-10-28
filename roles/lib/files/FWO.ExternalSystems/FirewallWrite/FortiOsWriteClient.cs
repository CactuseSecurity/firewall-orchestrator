using FWO.Data;
using RestSharp;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace FWO.ExternalSystems.FirewallWrite
{
    /// <summary>
    /// FortiOS REST API access with the API token of the export credential (secret) as bearer token. There is no
    /// session; group member changes are merged into the current members.
    /// </summary>
    public class FortiOsWriteClient : FirewallWriteClient
    {
        private const string kErrorAlreadyExists = "-5";
        private const string kErrorNotFound = "-3";
        private const string kErrorInvalidAddress = "-8";
        private const string kErrorInvalidNetmask = "-9";
        private const string kErrorOutOfRange = "-650";
        private const string kErrorInvalidInput = "-651";

        private string? apiToken;

        /// <summary>Creates a client for the REST API of a FortiOS device.</summary>
        public FortiOsWriteClient(Management management, bool checkCertificates)
            : base(HostUrl(management), management, checkCertificates)
        { }

        /// <inheritdoc/>
        public override Task Login()
        {
            apiToken = ExportCredentials().Secret;
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        protected override void AddAuthentication(RestRequest request)
        {
            request.AddHeader("Authorization", $"Bearer {apiToken ?? throw new FirewallWriteException(FirewallWriteErrorKind.Configuration, "FortiOS API token missing.")}");
        }

        /// <inheritdoc/>
        protected override async Task ResolveRuntimeValues(RenderedFirewallRequest request)
        {
            if (FirewallTemplateRenderer.Contains(request, FirewallWritePlaceholders.MergedMembers))
            {
                List<string> merged = FirewallMemberMerge.Merge(await ReadMembers(request), request.Step);
                List<Dictionary<string, string>> members = [.. merged.Select(name => new Dictionary<string, string> { { "name", name } })];
                FirewallTemplateRenderer.ResolveRuntimePlaceholders(request, new Dictionary<string, FirewallPlaceholderValue>
                {
                    { FirewallWritePlaceholders.MergedMembers, new(JsonSerializer.Serialize(members), true) }
                });
            }
            await base.ResolveRuntimeValues(request);
        }

        /// <summary>
        /// Maps native FortiOS errors before the HTTP status: validation errors can arrive with HTTP 500 but cannot be
        /// corrected by retrying the same request. Unrecognized errors fall back to the HTTP status.
        /// </summary>
        protected override (FirewallWriteErrorKind? Kind, string? NativeCode) ClassifyVendorError(JsonNode? content)
        {
            string? code = content?["error"]?.ToString();
            FirewallWriteErrorKind? kind = code switch
            {
                kErrorAlreadyExists => FirewallWriteErrorKind.AlreadyExists,
                kErrorNotFound => FirewallWriteErrorKind.NotFound,
                kErrorInvalidAddress or kErrorInvalidNetmask or kErrorOutOfRange or kErrorInvalidInput => FirewallWriteErrorKind.Rejected,
                _ => null
            };
            return (kind, code);
        }

        /// <summary>Reads the members of the group at the path of the call.</summary>
        private async Task<List<string>> ReadMembers(RenderedFirewallRequest request)
        {
            RestResponse response = await SendAuthenticated(new RestRequest(request.Path, Method.Get));
            if (!response.IsSuccessful)
            {
                throw ReadFailure(response, request, "the members of the group");
            }
            JsonArray? members = ParseJson(response.Content)?["results"]?[0]?["member"] as JsonArray;
            return [.. (members ?? []).Select(member => member?["name"]?.GetValue<string>() ?? "").Where(name => name.Length > 0)];
        }
    }
}
