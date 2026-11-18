using FWO.Data;
using FWO.Data.Labelling;
using FWO.Data.DeviceSpecificSettings;
using FWO.Logging;
using RestSharp;
using System.Globalization;
using System.Text.Json.Nodes;

namespace FWO.ExternalSystems.FirewallWrite
{
    /// <summary>
    /// Writes labels with IP criteria to Akamai Guardicore Segmentation. The client logs in with the export credentials
    /// (bearer token) and looks every label up by key and value first, so that repeated requests change nothing:
    /// <list type="bullet">
    /// <item>a label that does not exist yet is created with the rendered <see cref="FirewallWriteOperation.LabelWrite"/> call,</item>
    /// <item>the IP criteria of an existing label are set to the target addresses (see <see cref="GuardicoreLabelCriteria.TargetIpCriteria"/>)
    /// by the bulk update, which replaces all criteria of the label, so the criteria FWO does not manage are written back
    /// unchanged; a label that already has the target addresses is left alone,</item>
    /// <item>a label value is deleted by the rendered <see cref="FirewallWriteOperation.LabelDelete"/> call with its looked up id;
    /// a label that does not exist any more counts as deleted. Label keys are never deleted.</item>
    /// </list>
    /// </summary>
    public class GuardicoreWriteClient : FirewallWriteClient
    {
        private const string kVendor = "Guardicore";
        private const string kLogCategory = "Firewall Write";
        private const int kPageSize = 1000;
        private const int kMaxPages = 100;
        private const int kStatusOk = 200;
        private const string kOverrideParameter = "override";
        private const string kAppendParameter = "append";
        private static readonly List<string> TokenFields = ["access_token", "token"];
        private static readonly List<string> FailureFields = ["failed", "errors", "failed_items", "rejected", "invalid_labels"];

        private string token = "";

        /// <summary>Creates a client for the management's host.</summary>
        public GuardicoreWriteClient(Management management, bool checkCertificates = true, int? responseTimeout = null)
            : base(HostUrl(management), management, checkCertificates, responseTimeout)
        {
        }

        /// <inheritdoc/>
        public override async Task Login()
        {
            (string user, string secret) = ExportCredentials();
            RestRequest request = new(GuardicoreWriteDefaults.kAuthenticatePath, Method.Post);
            request.AddHeader("Accept", "application/json");
            request.AddStringBody(new JsonObject { ["username"] = user, ["password"] = secret }.ToJsonString(), ContentType.Json);
            RestResponse response = await Send(request);
            if (!response.IsSuccessful)
            {
                throw LoginFailure(response, kVendor);
            }
            JsonNode? content = ParseJson(response.Content);
            token = TokenFields.Select(field => GuardicoreLabelCriteria.Text(content?[field])).FirstOrDefault(value => value.Length > 0)
                ?? throw new FirewallWriteException(FirewallWriteErrorKind.Authentication, $"{kVendor} login to '{Management.Name}' returned no token.");
        }

        /// <inheritdoc/>
        protected override void AddAuthentication(RestRequest request)
        {
            request.AddHeader("Authorization", $"Bearer {token}");
        }

        /// <inheritdoc/>
        public override async Task<FirewallStepResult> Execute(RenderedFirewallRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            return request.Operation switch
            {
                FirewallWriteOperation.LabelWrite => await WriteLabel(request),
                FirewallWriteOperation.LabelDelete => await DeleteLabel(request),
                _ => throw new FirewallWriteException(FirewallWriteErrorKind.Unsupported, $"{request.Operation} is not supported for {kVendor}.")
            };
        }

        /// <summary>A bulk call answers with success even if items failed; those are listed in the response.</summary>
        protected override bool IsSuccess(RestResponse response) => response.IsSuccessful && FailedItems(ParseJson(response.Content)) == null;

        /// <inheritdoc/>
        protected override (FirewallWriteErrorKind? Kind, string? NativeCode) ClassifyVendorError(JsonNode? content)
        {
            return FailedItems(content) == null ? (null, null) : (FirewallWriteErrorKind.Rejected, null);
        }

        private async Task<FirewallStepResult> WriteLabel(RenderedFirewallRequest request)
        {
            (string key, string value) = LabelOf(request);
            JsonObject? label = await FindLabel(request, key, value);
            if (label == null)
            {
                return await base.Execute(request);
            }
            List<GuardicoreCriterion>? ipCriteria = GuardicoreLabelCriteria.TargetIpCriteria(GuardicoreLabelCriteria.ReadCriteria(label),
                GuardicoreLabelCriteria.ParseEntries(request.Step.MembersToAdd), GuardicoreLabelCriteria.ParseEntries(request.Step.MembersToRemove),
                request.Step.ReplacesMembers);
            if (ipCriteria == null)
            {
                return Succeeded(request, $"{kAlreadyPresentNote} The label already has the requested IP criteria.");
            }
            RestRequest updateRequest = new(GuardicoreWriteDefaults.kLabelsBulkPath, Method.Post);
            updateRequest.AddQueryParameter(kOverrideParameter, "true");
            updateRequest.AddQueryParameter(kAppendParameter, "false");
            updateRequest.AddStringBody(GuardicoreLabelCriteria.UpdateJson(key, value, label, ipCriteria), ContentType.Json);
            Log.WriteDebug(kLogCategory, $"{request.Operation}: replacing the criteria of label '{request.Step.TargetName}'");
            RestResponse response = await SendAuthenticated(updateRequest);
            return ToResult(request, response, IsSuccess(response));
        }

        private async Task<FirewallStepResult> DeleteLabel(RenderedFirewallRequest request)
        {
            (string key, string value) = LabelOf(request);
            JsonObject? label = await FindLabel(request, key, value);
            if (label == null)
            {
                return Succeeded(request, $"{kAlreadyDeletedNote} The label does not exist.");
            }
            Dictionary<string, FirewallPlaceholderValue> values = new()
            {
                { FirewallWritePlaceholders.LabelId, new(GuardicoreLabelCriteria.Text(label["id"]), false) }
            };
            FirewallTemplateRenderer.ResolveRuntimePlaceholders(request, values);
            FirewallStepResult result = await base.Execute(request);
            if (result.Error?.Kind == FirewallWriteErrorKind.NotFound)
            {
                return result with { Success = true, Error = null, Message = $"{kAlreadyDeletedNote} {result.Message}" };
            }
            return result;
        }

        /// <summary>Looks a label up by key and value; null if it does not exist.</summary>
        private async Task<JsonObject?> FindLabel(RenderedFirewallRequest request, string key, string value)
        {
            for (int page = 0; page < kMaxPages; page++)
            {
                RestRequest listRequest = new(GuardicoreWriteDefaults.kLabelsPath, Method.Get);
                listRequest.AddQueryParameter("key", key);
                listRequest.AddQueryParameter("value", value);
                listRequest.AddQueryParameter("offset", (page * kPageSize).ToString(CultureInfo.InvariantCulture));
                listRequest.AddQueryParameter("limit", kPageSize.ToString(CultureInfo.InvariantCulture));
                RestResponse response = await SendAuthenticated(listRequest);
                if (!response.IsSuccessful)
                {
                    throw ReadFailure(response, request, $"the label '{key}{DynamicIpLabel.kKeyValueSeparator}{value}'");
                }
                List<JsonObject> labels = ParseJson(response.Content)?["objects"] is JsonArray objects ? [.. objects.OfType<JsonObject>()] : [];
                JsonObject? match = labels.FirstOrDefault(label => GuardicoreLabelCriteria.Text(label["key"]) == key
                    && GuardicoreLabelCriteria.Text(label["value"]) == value);
                if (match != null || labels.Count < kPageSize)
                {
                    return match;
                }
            }
            throw new FirewallWriteException(FirewallWriteErrorKind.FirewallError,
                $"Looking up the label '{key}{DynamicIpLabel.kKeyValueSeparator}{value}' did not end after {kMaxPages} pages.");
        }

        private static (string Key, string Value) LabelOf(RenderedFirewallRequest request)
        {
            string key = request.Step.Values.GetValueOrDefault(FirewallWritePlaceholders.LabelKey)?.Value ?? "";
            string value = request.Step.Values.GetValueOrDefault(FirewallWritePlaceholders.LabelValue)?.Value ?? "";
            return key.Length > 0 && value.Length > 0
                ? (key, value)
                : throw new FirewallWriteException(FirewallWriteErrorKind.InvalidTaskData, $"{request.Operation} on '{request.Step.TargetName}' names no label.");
        }

        private static FirewallStepResult Succeeded(RenderedFirewallRequest request, string message)
        {
            Log.WriteInfo(kLogCategory, $"{request.Operation} on '{request.Step.TargetName}': {message}");
            return new FirewallStepResult(request.Operation, request.Step.TargetName, true, kStatusOk, message);
        }

        /// <summary>Returns the first non-empty list of failed items of a bulk response, null if there is none.</summary>
        private static JsonArray? FailedItems(JsonNode? content)
        {
            if (content is not JsonObject response)
            {
                return null;
            }
            return FailureFields.Select(field => response[field]).OfType<JsonArray>().FirstOrDefault(items => items.Count > 0);
        }
    }
}
