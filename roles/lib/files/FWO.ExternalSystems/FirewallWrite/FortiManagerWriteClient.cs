using FWO.Basics.Exceptions;
using FWO.Data;
using FWO.Data.DeviceSpecificSettings;
using FWO.Logging;
using RestSharp;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace FWO.ExternalSystems.FirewallWrite
{
    /// <summary>
    /// FortiManager JSON-RPC session: login with the export credentials, the session added to every call body, success
    /// read from the status codes of the results, and logout. Group member changes are merged into the current members.
    /// </summary>
    public class FortiManagerWriteClient : FirewallWriteClient
    {
        private const string kJsonRpcPath = "/jsonrpc";
        private const string kSession = "session";
        private const string kParams = "params";
        private const string kResult = "result";

        private string? session;

        /// <summary>Creates a client for the JSON-RPC API of a FortiManager.</summary>
        public FortiManagerWriteClient(Management management, bool checkCertificates)
            : base(HostUrl(management), management, checkCertificates)
        { }

        /// <inheritdoc/>
        public override async Task Login()
        {
            (string user, string password) = ExportCredentials();
            JsonObject body = new()
            {
                ["id"] = 1,
                ["method"] = "exec",
                [kParams] = new JsonArray(new JsonObject { ["url"] = "/sys/login/user", ["data"] = new JsonObject { ["user"] = user, ["passwd"] = password } })
            };
            RestResponse response = await Send(JsonRpcRequest(body));
            session = ParseJson(response.Content)?[kSession]?.GetValue<string>();
            if (!IsSuccess(response) || string.IsNullOrWhiteSpace(session))
            {
                throw new ProcessingFailedException($"FortiManager login to '{Management.Name}' failed: {(int)response.StatusCode} {response.Content}");
            }
        }

        /// <inheritdoc/>
        public override async Task Logout()
        {
            if (string.IsNullOrWhiteSpace(session))
            {
                return;
            }
            try
            {
                JsonObject body = new() { ["method"] = "exec", [kParams] = new JsonArray(new JsonObject { ["url"] = "/sys/logout" }), [kSession] = session };
                await Send(JsonRpcRequest(body));
            }
            catch (Exception exception)
            {
                Log.WriteWarning("Firewall Write", $"FortiManager logout threw an exception: {exception.Message}");
            }
            session = null;
        }

        /// <inheritdoc/>
        protected override async Task ResolveRuntimeValues(RenderedFirewallRequest request)
        {
            if (FirewallTemplateRenderer.Contains(request, FirewallWritePlaceholders.MergedMembers))
            {
                List<string> merged = FirewallMemberMerge.Merge(await ReadMembers(request), request.Step);
                FirewallTemplateRenderer.ResolveRuntimePlaceholders(request, new Dictionary<string, FirewallPlaceholderValue>
                {
                    { FirewallWritePlaceholders.MergedMembers, new(JsonSerializer.Serialize(merged), true) }
                });
            }
            await base.ResolveRuntimeValues(request);
        }

        /// <inheritdoc/>
        protected override string? PrepareBody(RenderedFirewallRequest request)
        {
            JsonNode? body = ParseJson(request.Body) ?? throw new ConfigException($"FortiManager call {request.Operation} needs a JSON-RPC body.");
            body[kSession] = session;
            body["id"] ??= 1;
            return body.ToJsonString();
        }

        /// <inheritdoc/>
        protected override void AddAuthentication(RestRequest request)
        {
            // the session is part of the JSON-RPC body
        }

        /// <summary>Successful if the HTTP call succeeded and every JSON-RPC result has status code 0.</summary>
        protected override bool IsSuccess(RestResponse response)
        {
            if (!response.IsSuccessful || ParseJson(response.Content)?[kResult] is not JsonArray results || results.Count == 0)
            {
                return false;
            }
            return results.All(result => result?["status"]?["code"]?.GetValue<int>() == 0);
        }

        /// <summary>Reads the members of the group addressed by the url of the call.</summary>
        private async Task<List<string>> ReadMembers(RenderedFirewallRequest request)
        {
            string? url = ParseJson(request.Body?.Replace(FirewallWritePlaceholders.MergedMembers, "0"))?[kParams]?[0]?["url"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(url))
            {
                throw new ConfigException($"Template error in {request.Operation}. The JSON-RPC call has no url.");
            }
            JsonObject body = new() { ["id"] = 1, ["method"] = "get", [kParams] = new JsonArray(new JsonObject { ["url"] = url }), [kSession] = session };
            RestResponse response = await Send(JsonRpcRequest(body));
            if (!IsSuccess(response))
            {
                throw new ProcessingFailedException($"FortiManager could not read group '{request.Step.TargetName}': {response.Content}");
            }
            JsonNode? members = ParseJson(response.Content)?[kResult]?[0]?["data"]?["member"];
            return members switch
            {
                JsonArray array => [.. array.Select(member => member?.GetValue<string>() ?? "").Where(member => member.Length > 0)],
                JsonValue value => [value.GetValue<string>()],
                _ => []
            };
        }

        private static RestRequest JsonRpcRequest(JsonObject body)
        {
            RestRequest request = new(kJsonRpcPath, Method.Post);
            request.AddStringBody(body.ToJsonString(), ContentType.Json);
            return request;
        }
    }
}
