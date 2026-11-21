using System.Collections.Immutable;
using FWO.Basics;
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
    /// <item>the static asset assignments of the labels FWO owns (app roles, app zones, network areas) are removed when the
    /// label is written, those of other labels are kept; addresses to remove that no IP criterion holds fail the task
    /// (see <see cref="GuardicoreLabelCriteria.PlanUpdate"/>),</item>
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
        private static readonly ImmutableArray<string> TokenFields = ["access_token", "token"];
        private static readonly ImmutableArray<string> TotalFields = ["total_count", "total"];
        private static readonly ImmutableArray<string> FailureFields = ["failed", "errors", "failed_items", "rejected", "invalid_labels"];

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
            bool ownsAssignments = new DynamicIpLabel(key, value).ObjectType != null;
            GuardicoreLabelUpdate update = GuardicoreLabelCriteria.PlanUpdate(label, GuardicoreLabelCriteria.ParseEntries(request.Step.MembersToAdd),
                GuardicoreLabelCriteria.ParseEntries(request.Step.MembersToRemove), request.Step.ReplacesMembers, ownsAssignments);
            if (update.WritesNothing && update.NotRemovable.Count == 0)
            {
                return Succeeded(request, $"{kAlreadyPresentNote} The label already has the requested IP criteria.");
            }
            FirewallStepResult result = update.WritesNothing
                ? new FirewallStepResult(request.Operation, request.Step.TargetName, true, kStatusOk, "")
                : await UpdateLabel(request, key, value, label, update);
            return result.Success && update.NotRemovable.Count > 0 ? NotRemovableFailure(request, result, update.NotRemovable) : result;
        }

        /// <summary>Replaces the criteria of a label by the bulk update (override, not append).</summary>
        private async Task<FirewallStepResult> UpdateLabel(RenderedFirewallRequest request, string key, string value, JsonObject label, GuardicoreLabelUpdate update)
        {
            RestRequest updateRequest = new(GuardicoreWriteDefaults.kLabelsBulkPath, Method.Post);
            updateRequest.AddQueryParameter(kOverrideParameter, "true");
            updateRequest.AddQueryParameter(kAppendParameter, "false");
            updateRequest.AddStringBody(GuardicoreLabelCriteria.UpdateJson(key, value, label, update.IpCriteria, !update.RemovesStaticAssignments), ContentType.Json);
            string staticNote = update.RemovesStaticAssignments ? " and removing its static asset assignments" : "";
            Log.WriteDebug(kLogCategory, $"{request.Operation}: replacing the criteria of label '{request.Step.TargetName}'{staticNote}");
            RestResponse response = await SendAuthenticated(updateRequest);
            return ToResult(request, response, IsSuccess(response));
        }

        /// <summary>
        /// The result of a label write that could not remove all requested addresses: they would be requested again
        /// after every import, so the task fails and names them instead of reporting success.
        /// </summary>
        private static FirewallStepResult NotRemovableFailure(RenderedFirewallRequest request, FirewallStepResult written, List<IpInterval> notRemovable)
        {
            string addresses = string.Join(", ", notRemovable.Select(IpAddressSet.Format));
            string message = $"The label '{request.Step.TargetName}' keeps the addresses {addresses}: no IP criterion of the label holds them, so they belong " +
                "to assets that carry the label through another of their addresses, an implicit criterion or a static assignment FWO does not remove. " +
                "Add them to the modelled object or change these assets in Guardicore.";
            Log.WriteWarning(kLogCategory, $"{request.Operation} on '{request.Step.TargetName}' failed: {message}");
            FirewallWriteError error = new(FirewallWriteErrorKind.Unsupported, message, written.StatusCode, null, request.Operation);
            return written with { Success = false, Message = message, Error = error };
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

        /// <summary>
        /// Looks a label up by key and value; null if it does not exist. The API may cap the page size below the requested
        /// limit, so paging goes on until the total the response names is reached; only without a total a short page ends it.
        /// </summary>
        private async Task<JsonObject?> FindLabel(RenderedFirewallRequest request, string key, string value)
        {
            int readCount = 0;
            for (int page = 0; page < kMaxPages; page++)
            {
                RestRequest listRequest = new(GuardicoreWriteDefaults.kLabelsPath, Method.Get);
                listRequest.AddQueryParameter("key", key);
                listRequest.AddQueryParameter("value", value);
                listRequest.AddQueryParameter("offset", readCount.ToString(CultureInfo.InvariantCulture));
                listRequest.AddQueryParameter("limit", kPageSize.ToString(CultureInfo.InvariantCulture));
                RestResponse response = await SendAuthenticated(listRequest);
                if (!response.IsSuccessful)
                {
                    throw ReadFailure(response, request, $"the label '{key}{DynamicIpLabel.kKeyValueSeparator}{value}'");
                }
                JsonNode? content = ParseJson(response.Content);
                JsonArray objects = content?["objects"] as JsonArray ?? [];
                readCount += objects.Count;
                JsonObject? match = objects.OfType<JsonObject>().FirstOrDefault(label => GuardicoreLabelCriteria.Text(label["key"]) == key
                    && GuardicoreLabelCriteria.Text(label["value"]) == value);
                if (match != null || IsLastPage(objects.Count, readCount, TotalOf(content)))
                {
                    return match;
                }
            }
            throw new FirewallWriteException(FirewallWriteErrorKind.FirewallError,
                $"Looking up the label '{key}{DynamicIpLabel.kKeyValueSeparator}{value}' did not end after {kMaxPages} pages.");
        }

        /// <summary>Whether paging is done: a page is empty, the total is reached, or without a total a page is short.</summary>
        private static bool IsLastPage(int pageCount, int readCount, int? total)
        {
            if (pageCount == 0)
            {
                return true;
            }
            return total is int knownTotal ? readCount >= knownTotal : pageCount < kPageSize;
        }

        /// <summary>The total number of objects a list response names, null if it names none.</summary>
        private static int? TotalOf(JsonNode? content)
        {
            foreach (string field in TotalFields)
            {
                if (content?[field] is JsonValue total && total.TryGetValue(out int count))
                {
                    return count;
                }
            }
            return null;
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
