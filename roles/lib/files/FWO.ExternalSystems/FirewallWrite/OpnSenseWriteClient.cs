using FWO.Data;
using FWO.Data.DeviceSpecificSettings;
using RestSharp;
using System.Text;
using System.Text.Json.Nodes;

namespace FWO.ExternalSystems.FirewallWrite
{
    /// <summary>
    /// OPNsense API access with API key (user) and secret of the export credential as basic authentication. Resolves
    /// alias UUIDs by name. Member changes use the alias utility, which holds the firewall's configuration lock while
    /// adding or removing one address and persisting host/network aliases; the client never replaces a stale member list.
    /// </summary>
    public class OpnSenseWriteClient : FirewallWriteClient
    {
        private const string kAliasPath = "/api/firewall/alias";
        private const string kValidations = "validations";
        private const string kResultFailed = "failed";
        private const string kStatus = "status";
        private const string kStatusDone = "done";
        private const string kStatusOk = "ok";
        private const string kAliasTypeHost = "host";
        private const string kAliasTypeNetwork = "network";
        private const char kPrefixSeparator = '/';

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
            request.AddHeader("Authorization", $"Basic {basicAuthentication ?? throw new FirewallWriteException(FirewallWriteErrorKind.Configuration, "OPNsense API key missing.")}");
        }

        /// <summary>
        /// Host aliases hold single addresses only: a task that adds or removes a network in one is refused before anything
        /// is sent, because OPNsense would reject the addition and report the removal as done without changing the alias.
        /// </summary>
        public override async Task Prepare(IReadOnlyList<RenderedFirewallRequest> requests)
        {
            IEnumerable<RenderedFirewallRequest> networkChanges = requests
                .Where(request => ChangesMembers(request) && MemberEntry(request).Contains(kPrefixSeparator))
                .DistinctBy(request => request.Step.TargetName);
            foreach (RenderedFirewallRequest request in networkChanges)
            {
                string uuid = await LookupAliasUuid(request);
                if (await AliasType(uuid, request) == kAliasTypeHost)
                {
                    throw new FirewallWriteException(FirewallWriteErrorKind.Unsupported,
                        $"OPNsense alias '{request.Step.TargetName}' has type host and cannot hold the network '{MemberEntry(request)}'.");
                }
            }
        }

        /// <summary>The single address or network a per-address utility call adds or removes.</summary>
        private static string MemberEntry(RenderedFirewallRequest request) =>
            request.Step.Values.TryGetValue(FirewallWritePlaceholders.MemberName, out FirewallPlaceholderValue? entry) ? entry.Value : "";

        /// <summary>Per-address utility calls must explicitly report completion, including when HTTP status is 200.</summary>
        public override async Task<FirewallStepResult> Execute(RenderedFirewallRequest request)
        {
            if (!ChangesMembers(request))
            {
                return await base.Execute(request);
            }
            await ResolveRuntimeValues(request);
            FirewallTemplateRenderer.EnsureResolved(request);
            RestResponse response = await SendRendered(request);
            bool completed = IsSuccess(response) && Status(ParseJson(response.Content)) == kStatusDone;
            return ToResult(request, response, completed);
        }

        /// <summary>Whether the request changes the members of an existing alias.</summary>
        private static bool ChangesMembers(RenderedFirewallRequest request) =>
            request.Operation is FirewallWriteOperation.GroupAddMembers or FirewallWriteOperation.GroupRemoveMembers;

        /// <summary>
        /// The "status" of an answer, trimmed and in lower case: configd actions answer e.g. "OK\n\n" (filter/apply),
        /// the alias utility "done" and alias/reconfigure "ok".
        /// </summary>
        private static string? Status(JsonNode? content) => content?[kStatus]?.ToString().Trim().ToLowerInvariant();

        /// <summary>Failed if the HTTP call failed, the API reports validation errors, a failed result or a status other than done/ok.</summary>
        protected override bool IsSuccess(RestResponse response)
        {
            if (!response.IsSuccessful)
            {
                return false;
            }
            JsonNode? content = ParseJson(response.Content);
            string? result = content?["result"]?.ToString();
            string? status = Status(content);
            return content?[kValidations] == null && result != kResultFailed && (status == null || status is kStatusDone or kStatusOk);
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
            string? status = Status(content);
            if (status != null && status is not kStatusDone and not kStatusOk)
            {
                return (FirewallWriteErrorKind.Rejected, status);
            }
            return result == kResultFailed ? (FirewallWriteErrorKind.Rejected, result) : (null, null);
        }

        /// <inheritdoc/>
        protected override async Task ResolveRuntimeValues(RenderedFirewallRequest request)
        {
            Dictionary<string, FirewallPlaceholderValue> values = [];
            bool changesMembers = ChangesMembers(request);
            if (FirewallTemplateRenderer.Contains(request, FirewallWritePlaceholders.MergedMembers))
            {
                throw new FirewallWriteException(FirewallWriteErrorKind.Unsupported,
                    "OPNsense cannot safely replace merged alias members. Reset member endpoints and templates to the shipped per-address defaults.");
            }
            if (FirewallTemplateRenderer.Contains(request, FirewallWritePlaceholders.ObjectUuid) || changesMembers)
            {
                string uuid = await LookupAliasUuid(request);
                values[FirewallWritePlaceholders.ObjectUuid] = new(uuid, false);
                if (changesMembers)
                {
                    await EnsurePersistentAlias(uuid, request);
                }
                FirewallTemplateRenderer.ResolveRuntimePlaceholders(request, values);
            }
            await base.ResolveRuntimeValues(request);
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

        /// <summary>Only host/network aliases persist address changes; external and other alias types are refused.</summary>
        private async Task EnsurePersistentAlias(string uuid, RenderedFirewallRequest request)
        {
            if (await AliasType(uuid, request) is not kAliasTypeHost and not kAliasTypeNetwork)
            {
                throw new FirewallWriteException(FirewallWriteErrorKind.Unsupported,
                    $"OPNsense alias '{request.Step.TargetName}' must have type host or network for persistent per-address changes.");
            }
        }

        /// <summary>Reads the selected type of an alias, e.g. host, network or external.</summary>
        private async Task<string?> AliasType(string uuid, RenderedFirewallRequest request)
        {
            RestResponse response = await SendAuthenticated(new RestRequest($"{kAliasPath}/getItem/{Uri.EscapeDataString(uuid)}", Method.Get));
            if (!response.IsSuccessful)
            {
                throw ReadFailure(response, request, $"the type of alias '{uuid}'");
            }
            JsonNode? type = ParseJson(response.Content)?["alias"]?["type"];
            return type is JsonObject options
                ? options.FirstOrDefault(entry => entry.Value?["selected"]?.ToString() is "1" or "true").Key
                : type?.GetValue<string>();
        }
    }
}
