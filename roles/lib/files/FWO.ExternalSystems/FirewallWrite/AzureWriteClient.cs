using FWO.Basics.Exceptions;
using FWO.Data;
using FWO.Data.DeviceSpecificSettings;
using RestSharp;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace FWO.ExternalSystems.FirewallWrite
{
    /// <summary>
    /// Azure Resource Manager access with a service principal: client id (user) and client secret of the export credential,
    /// tenant of the management. Resolves the location of the resource group, merges IP group members, and merges rules
    /// into their rule collection group, because ARM only replaces whole resources.
    /// </summary>
    public class AzureWriteClient : FirewallWriteClient
    {
        private const string kArmBaseUrl = "https://management.azure.com";
        private const string kLoginUrl = "https://login.microsoftonline.com/{0}/oauth2/v2.0/token";
        private const string kArmScope = "https://management.azure.com/.default";
        private const string kResourceGroupApiVersion = "2021-04-01";
        private const string kProperties = "properties";
        private const string kRuleCollections = "ruleCollections";
        private const int kDefaultRuleCollectionGroupPriority = 10000;

        private static readonly List<FirewallWriteOperation> RuleOperations =
            [FirewallWriteOperation.RuleCreate, FirewallWriteOperation.RuleModify, FirewallWriteOperation.RuleDelete];

        private string? accessToken;

        /// <summary>Creates a client for Azure Resource Manager.</summary>
        public AzureWriteClient(Management management, bool checkCertificates)
            : base(kArmBaseUrl, management, checkCertificates)
        { }

        /// <inheritdoc/>
        public override async Task Login()
        {
            if (string.IsNullOrWhiteSpace(Management.CloudTenantId))
            {
                throw new ProcessingFailedException($"Azure management '{Management.Name}' has no tenant id.");
            }
            (string clientId, string clientSecret) = ExportCredentials();
            RestRequest request = new(string.Format(System.Globalization.CultureInfo.InvariantCulture, kLoginUrl, Uri.EscapeDataString(Management.CloudTenantId)), Method.Post);
            request.AddParameter("grant_type", "client_credentials");
            request.AddParameter("client_id", clientId);
            request.AddParameter("client_secret", clientSecret);
            request.AddParameter("scope", kArmScope);
            RestResponse response = await Send(request);
            accessToken = ParseJson(response.Content)?["access_token"]?.GetValue<string>();
            if (!response.IsSuccessful || string.IsNullOrWhiteSpace(accessToken))
            {
                throw new ProcessingFailedException($"Azure login for '{Management.Name}' failed: {(int)response.StatusCode}");
            }
        }

        /// <inheritdoc/>
        public override async Task<FirewallStepResult> Execute(RenderedFirewallRequest request)
        {
            if (!RuleOperations.Contains(request.Operation))
            {
                return await base.Execute(request);
            }
            FirewallTemplateRenderer.EnsureResolved(request);
            JsonObject fragment = ParseJson(request.Body) as JsonObject
                ?? throw new ConfigException($"Template error in {request.Operation}. The rule template must be a JSON object.");
            JsonObject ruleCollectionGroup = await ReadRuleCollectionGroup(request.Path, fragment);
            AzureRuleCollectionMerge.Apply(ruleCollectionGroup, request.Operation, fragment);
            request.Body = ruleCollectionGroup.ToJsonString();
            RestResponse response = await SendRendered(request);
            return ToResult(request, response, IsSuccess(response));
        }

        /// <inheritdoc/>
        protected override void AddAuthentication(RestRequest request)
        {
            request.AddHeader("Authorization", $"Bearer {accessToken ?? throw new ProcessingFailedException("Azure access token missing.")}");
        }

        /// <inheritdoc/>
        protected override async Task ResolveRuntimeValues(RenderedFirewallRequest request)
        {
            Dictionary<string, FirewallPlaceholderValue> values = [];
            if (FirewallTemplateRenderer.Contains(request, FirewallWritePlaceholders.Location))
            {
                values[FirewallWritePlaceholders.Location] = new(await ReadLocation(request.Step), false);
            }
            if (FirewallTemplateRenderer.Contains(request, FirewallWritePlaceholders.MergedMembers))
            {
                List<string> merged = FirewallMemberMerge.Merge(await ReadIpAddresses(request), request.Step);
                values[FirewallWritePlaceholders.MergedMembers] = new(JsonSerializer.Serialize(merged), true);
            }
            if (values.Count > 0)
            {
                FirewallTemplateRenderer.ResolveRuntimePlaceholders(request, values);
            }
            await base.ResolveRuntimeValues(request);
        }

        private async Task<string> ReadLocation(FirewallWriteStep step)
        {
            string subscription = step.Values.GetValueOrDefault(FirewallWritePlaceholders.SubscriptionId)?.Value ?? "";
            string resourceGroup = step.Values.GetValueOrDefault(FirewallWritePlaceholders.ResourceGroup)?.Value ?? "";
            RestRequest request = new($"/subscriptions/{Uri.EscapeDataString(subscription)}/resourcegroups/{Uri.EscapeDataString(resourceGroup)}?api-version={kResourceGroupApiVersion}", Method.Get);
            RestResponse response = await SendAuthenticated(request);
            string? location = response.IsSuccessful ? ParseJson(response.Content)?["location"]?.GetValue<string>() : null;
            return string.IsNullOrWhiteSpace(location)
                ? throw new ProcessingFailedException($"Azure resource group '{resourceGroup}' not found: {(int)response.StatusCode}")
                : location;
        }

        /// <summary>Reads the addresses of the IP group at the path of the call.</summary>
        private async Task<List<string>> ReadIpAddresses(RenderedFirewallRequest request)
        {
            RestResponse response = await SendAuthenticated(new RestRequest(request.Path, Method.Get));
            if (!response.IsSuccessful)
            {
                throw new ProcessingFailedException($"Azure could not read IP group '{request.Step.TargetName}': {(int)response.StatusCode} {response.Content}");
            }
            JsonArray? addresses = ParseJson(response.Content)?[kProperties]?["ipAddresses"] as JsonArray;
            return [.. (addresses ?? []).Select(address => address?.GetValue<string>() ?? "").Where(address => address.Length > 0)];
        }

        /// <summary>Reads the rule collection group, or starts a new one if it does not exist yet.</summary>
        private async Task<JsonObject> ReadRuleCollectionGroup(string path, JsonObject fragment)
        {
            RestResponse response = await SendAuthenticated(new RestRequest(path, Method.Get));
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                int priority = fragment["ruleCollectionGroupPriority"]?.GetValue<int>() ?? kDefaultRuleCollectionGroupPriority;
                return new JsonObject { [kProperties] = new JsonObject { ["priority"] = priority, [kRuleCollections] = new JsonArray() } };
            }
            if (!response.IsSuccessful || ParseJson(response.Content)?[kProperties] is not JsonObject properties)
            {
                throw new ProcessingFailedException($"Azure could not read rule collection group: {(int)response.StatusCode} {response.Content}");
            }
            JsonObject writable = new() { ["priority"] = properties["priority"]?.DeepClone(), [kRuleCollections] = properties[kRuleCollections]?.DeepClone() ?? new JsonArray() };
            return new JsonObject { [kProperties] = writable };
        }
    }
}
