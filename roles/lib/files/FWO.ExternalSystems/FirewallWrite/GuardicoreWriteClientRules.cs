using System.Globalization;
using System.Text.Json.Nodes;
using FWO.Data.DeviceSpecificSettings;
using FWO.Data.Labelling;
using FWO.Logging;
using RestSharp;

namespace FWO.ExternalSystems.FirewallWrite
{
    /// <summary>
    /// Writes the rules of modelled connections to the Allow section of Guardicore, one rule per protocol, see
    /// <see cref="LabelRuleStepPlanner"/>. The rules of a connection are found by its marker in the comment, so repeated
    /// requests change nothing:
    /// <list type="bullet">
    /// <item>a rule step replaces the existing rule of the connection with the same protocol (PUT on its path) unless it
    /// already is as requested, and creates the rule otherwise; the labels the sides name by "key: value" are replaced by
    /// their ids, and a label that does not exist fails the step and is named,</item>
    /// <item>the delete step deletes the rules of the connection the rule steps did not write (all of them for rule_delete),</item>
    /// <item>publish creates a policy revision only if a rule was changed, so label changes and repeated requests publish nothing.</item>
    /// </list>
    /// Every rule change is recorded with the call that undoes it. If the task fails, <see cref="Discard"/> undoes the changes
    /// made so far in reverse order (created rules are deleted, replaced ones restored, deleted ones created again), so no
    /// half-written connection waits unpublished for the next revision.
    /// </summary>
    public partial class GuardicoreWriteClient
    {
        private readonly Dictionary<string, List<JsonObject>> connectionRules = new(StringComparer.Ordinal);
        private readonly HashSet<string> writtenRuleIds = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string?> labelIdsByName = new(StringComparer.Ordinal);
        private readonly List<RuleUndo> undoSteps = [];
        private bool policyChanged;

        /// <summary>A call that undoes one rule change of the task.</summary>
        private sealed record RuleUndo(Method Method, string Path, string? Body, string Description);

        /// <summary>Undoes the rule changes of a failed task in reverse order; failures are logged, as the task has failed anyway.</summary>
        public override async Task Discard()
        {
            for (int index = undoSteps.Count - 1; index >= 0; index--)
            {
                RuleUndo undo = undoSteps[index];
                try
                {
                    RestRequest request = new(undo.Path, undo.Method);
                    if (undo.Body != null)
                    {
                        request.AddStringBody(undo.Body, ContentType.Json);
                    }
                    RestResponse response = await SendAuthenticated(request);
                    if (!response.IsSuccessful)
                    {
                        Log.WriteWarning(kLogCategory, $"Could not {undo.Description} of the failed task: {(int)response.StatusCode} {FirewallWriteErrors.NativeMessage(response)}");
                    }
                }
                catch (Exception exception)
                {
                    Log.WriteError(kLogCategory, $"Could not {undo.Description} of the failed task.", exception);
                }
            }
            undoSteps.Clear();
        }

        private static string RulePath(string ruleId) => $"{GuardicoreWriteDefaults.kRulesPath}/{Uri.EscapeDataString(ruleId)}";

        /// <summary>A rule as it can be created again: without its id.</summary>
        private static string WithoutId(JsonObject rule)
        {
            JsonObject copy = rule.DeepClone().AsObject();
            copy.Remove("id");
            return copy.ToJsonString();
        }

        private async Task<FirewallStepResult> WriteRule(RenderedFirewallRequest request)
        {
            string marker = StepText(request, FirewallWritePlaceholders.RuleComment);
            string protocol = StepText(request, FirewallWritePlaceholders.RuleProtocol);
            JsonObject body = ParseJson(request.Body) as JsonObject
                ?? throw new FirewallWriteException(FirewallWriteErrorKind.Configuration, $"{request.Operation} on '{request.Step.TargetName}': the body is no JSON object.");
            List<string> missingLabels = await ReplaceLabelNamesByIds(request, body);
            if (missingLabels.Count > 0)
            {
                return MissingLabelsFailure(request, missingLabels);
            }
            request.Body = body.ToJsonString();
            List<JsonObject> rules = await ConnectionRules(request, marker);
            JsonObject? existing = rules.FirstOrDefault(rule => !writtenRuleIds.Contains(GuardicoreRules.Id(rule)) && GuardicoreRules.HasOnlyProtocol(rule, protocol));
            if (existing == null)
            {
                FirewallStepResult created = await base.Execute(request);
                if (created.Success)
                {
                    policyChanged = true;
                    RecordCreatedRule(request, created);
                }
                return created;
            }
            string ruleId = GuardicoreRules.Id(existing);
            writtenRuleIds.Add(ruleId);
            if (GuardicoreRules.Matches(existing, body))
            {
                return Succeeded(request, $"{kAlreadyPresentNote} The rule {ruleId} is unchanged.");
            }
            RestRequest replaceRequest = new(RulePath(ruleId), Method.Put);
            replaceRequest.AddStringBody(request.Body, ContentType.Json);
            Log.WriteDebug(kLogCategory, $"{request.Operation}: replacing rule {ruleId} of '{request.Step.TargetName}'");
            RestResponse response = await SendAuthenticated(replaceRequest);
            FirewallStepResult replaced = ToResult(request, response, IsSuccess(response));
            if (replaced.Success)
            {
                policyChanged = true;
                undoSteps.Add(new RuleUndo(Method.Put, RulePath(ruleId), WithoutId(existing), $"restore the rule {ruleId}"));
            }
            return replaced;
        }

        /// <summary>Records how to delete a created rule again, by the id its creation answered.</summary>
        private void RecordCreatedRule(RenderedFirewallRequest request, FirewallStepResult created)
        {
            string createdId = GuardicoreRules.Id(ParseJson(created.Message));
            if (createdId.Length > 0)
            {
                undoSteps.Add(new RuleUndo(Method.Delete, RulePath(createdId), null, $"delete the created rule {createdId}"));
            }
            else
            {
                Log.WriteWarning(kLogCategory, $"{request.Operation} on '{request.Step.TargetName}': the answer names no rule id, so the rule cannot be deleted again if the task fails.");
            }
        }

        /// <summary>Deletes the rules of the connection that the rule steps of the task did not write.</summary>
        private async Task<FirewallStepResult> DeleteOtherRules(RenderedFirewallRequest request)
        {
            string marker = StepText(request, FirewallWritePlaceholders.RuleComment);
            List<JsonObject> otherRules = [.. (await ConnectionRules(request, marker)).Where(rule => GuardicoreRules.Id(rule).Length > 0 && !writtenRuleIds.Contains(GuardicoreRules.Id(rule)))];
            List<string> ruleIds = [.. otherRules.Select(GuardicoreRules.Id)];
            if (ruleIds.Count == 0)
            {
                return Succeeded(request, "The connection has no other rule.");
            }
            foreach (JsonObject rule in otherRules)
            {
                string ruleId = GuardicoreRules.Id(rule);
                RenderedFirewallRequest deleteRequest = new() { Step = request.Step, Method = request.Method, Path = request.Path, Body = request.Body };
                Dictionary<string, FirewallPlaceholderValue> values = new() { { FirewallWritePlaceholders.RuleId, new(ruleId, false) } };
                FirewallTemplateRenderer.ResolveRuntimePlaceholders(deleteRequest, values);
                FirewallStepResult result = await base.Execute(deleteRequest);
                if (!result.Success && result.Error?.Kind != FirewallWriteErrorKind.NotFound)
                {
                    return result;
                }
                policyChanged = true;
                undoSteps.Add(new RuleUndo(Method.Post, GuardicoreWriteDefaults.kRulesPath, WithoutId(rule), $"create the deleted rule {ruleId} again"));
            }
            return Succeeded(request, $"Deleted the rules {string.Join(", ", ruleIds)}.");
        }

        /// <summary>Publishes a revision if a rule was changed; otherwise there is nothing to publish.</summary>
        private async Task<FirewallStepResult> PublishIfChanged(RenderedFirewallRequest request)
        {
            return policyChanged ? await base.Execute(request) : Succeeded(request, "Nothing to publish: no rule was changed.");
        }

        /// <summary>
        /// The rules of the Allow section that name the marker of the connection, read once per client; the rule list is
        /// paged until the total it names is reached.
        /// </summary>
        private async Task<List<JsonObject>> ConnectionRules(RenderedFirewallRequest request, string marker)
        {
            if (connectionRules.TryGetValue(marker, out List<JsonObject>? cached))
            {
                return cached;
            }
            List<JsonObject> rules = [];
            int readCount = 0;
            for (int page = 0; page < kMaxPages; page++)
            {
                RestRequest listRequest = new(GuardicoreWriteDefaults.kRulesPath, Method.Get);
                listRequest.AddQueryParameter("offset", readCount.ToString(CultureInfo.InvariantCulture));
                listRequest.AddQueryParameter("limit", kPageSize.ToString(CultureInfo.InvariantCulture));
                RestResponse response = await SendAuthenticated(listRequest);
                if (!response.IsSuccessful)
                {
                    throw ReadFailure(response, request, "the segmentation rules");
                }
                JsonNode? content = ParseJson(response.Content);
                JsonArray objects = content?["objects"] as JsonArray ?? [];
                readCount += objects.Count;
                rules.AddRange(objects.OfType<JsonObject>().Where(rule => GuardicoreRules.BelongsTo(rule, GuardicoreWriteDefaults.kAllowSection, marker)));
                if (IsLastPage(objects.Count, readCount, TotalOf(content)))
                {
                    connectionRules[marker] = rules;
                    return rules;
                }
            }
            throw new FirewallWriteException(FirewallWriteErrorKind.FirewallError, $"Reading the segmentation rules did not end after {kMaxPages} pages.");
        }

        /// <summary>
        /// Replaces the labels the sides name by "key: value" by their ids, looked up once per label.
        /// </summary>
        /// <returns>The names of the labels that do not exist.</returns>
        private async Task<List<string>> ReplaceLabelNamesByIds(RenderedFirewallRequest request, JsonObject body)
        {
            List<string> missing = [];
            List<JsonNode?> sides = [body["source"], body["destination"]];
            foreach (JsonArray andLabels in sides
                .Select(side => side?["labels"]?["or_labels"]).OfType<JsonArray>()
                .SelectMany(orLabels => orLabels).Select(group => group?["and_labels"]).OfType<JsonArray>())
            {
                for (int index = 0; index < andLabels.Count; index++)
                {
                    string name = GuardicoreLabelCriteria.Text(andLabels[index]);
                    string? labelId = await LabelIdOf(request, name);
                    if (labelId == null)
                    {
                        missing.Add(name);
                        continue;
                    }
                    andLabels[index] = JsonValue.Create(labelId);
                }
            }
            return [.. missing.Distinct(StringComparer.Ordinal)];
        }

        private async Task<string?> LabelIdOf(RenderedFirewallRequest request, string name)
        {
            if (labelIdsByName.TryGetValue(name, out string? cached))
            {
                return cached;
            }
            DynamicIpLabel? label = DynamicIpLabel.FromGroupName(name, null);
            string? labelId = null;
            if (label != null)
            {
                JsonObject? found = await FindLabel(request, label.Key, label.Value);
                labelId = found == null ? null : GuardicoreRules.Id(found);
            }
            labelIdsByName[name] = string.IsNullOrEmpty(labelId) ? null : labelId;
            return labelIdsByName[name];
        }

        /// <summary>A rule that names labels Guardicore does not have fails and names them, instead of being written without them.</summary>
        private static FirewallStepResult MissingLabelsFailure(RenderedFirewallRequest request, List<string> missingLabels)
        {
            string message = $"The rule '{request.Step.TargetName}' is not written: the labels {string.Join(", ", missingLabels)} do not exist in Guardicore. " +
                "Provision them first (they are requested with the connection) or create them in Guardicore.";
            Log.WriteWarning(kLogCategory, message);
            FirewallWriteError error = new(FirewallWriteErrorKind.NotFound, message, kStatusNotFound, null, request.Operation);
            return new FirewallStepResult(request.Operation, request.Step.TargetName, false, kStatusNotFound, message, error);
        }

        private static string StepText(RenderedFirewallRequest request, string placeholder)
        {
            string value = request.Step.Values.GetValueOrDefault(placeholder)?.Value ?? "";
            return value.Length > 0
                ? value
                : throw new FirewallWriteException(FirewallWriteErrorKind.InvalidTaskData, $"{request.Operation} on '{request.Step.TargetName}' names no {placeholder}.");
        }
    }
}
