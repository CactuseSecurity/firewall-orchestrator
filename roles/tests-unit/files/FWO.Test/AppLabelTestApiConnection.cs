using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data;
using FWO.Data.Labelling;
using System.Collections;
using System.Reflection;

namespace FWO.Test
{
    /// <summary>
    /// Simulated backend for the App label tests: answers the App label queries from configurable data and records
    /// the label writes.
    /// </summary>
    internal sealed class AppLabelTestApiConnection : SimulatedApiConnection
    {
        public const int kKeyId = 7;
        public const long kEventId = 55;

        public Dictionary<string, long> LabelIdsByAppId { get; } = [];
        public List<RuleOwnerApp> ActiveRuleOwnerApps { get; } = [];
        public List<RuleLabelRow> ActiveRuleAppLabels { get; } = [];
        public List<OwnerAppServerAppLabels> Owners { get; } = [];
        public bool FailReads { get; set; }

        public List<(string query, object? variables)> Calls { get; } = [];
        public List<string> UpsertedKeys { get; } = [];
        public List<string> UpsertedAppIds { get; } = [];
        public List<(long? importControlId, bool originIsUi)> ChangeEvents { get; } = [];
        public List<(long ruleId, long labelId, long eventId)> AddedRuleLabels { get; } = [];
        public List<(long ruleId, long labelId, long eventId)> ClosedRuleLabels { get; } = [];
        public List<(long ownerNetworkId, long labelId, long eventId)> AddedServerLabels { get; } = [];
        public List<(long ownerNetworkId, long labelId, long eventId)> ClosedServerLabels { get; } = [];

        public int CallsOf(string query) => Calls.Count(call => call.query == query);

        public object? LastVariables(string query) => Calls.LastOrDefault(call => call.query == query).variables;

        public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
        {
            Calls.Add((query, variables));
            if (FailReads && (query == LabellingQueries.getAllActiveRuleOwnerApps || query == LabellingQueries.getActiveRuleOwnerApps))
            {
                throw new InvalidOperationException("simulated read failure");
            }
            return Task.FromResult((QueryResponseType)(Read(query, variables) ?? Write(query, variables)));
        }

        private object? Read(string query, object? variables)
        {
            if (query == LabellingQueries.getAllActiveRuleOwnerApps)
            {
                return ActiveRuleOwnerApps;
            }
            if (query == LabellingQueries.getActiveRuleOwnerApps)
            {
                List<long> ruleIds = [.. (IEnumerable<long>)Value(variables, "ruleIds")!];
                return ActiveRuleOwnerApps.Where(mapping => ruleIds.Contains(mapping.RuleId)).ToList();
            }
            if (query == LabellingQueries.getAllActiveRuleAppLabels)
            {
                return ActiveRuleAppLabels;
            }
            if (query == LabellingQueries.getActiveRuleAppLabels)
            {
                List<long> ruleIds = [.. (IEnumerable<long>)Value(variables, "ruleIds")!];
                return ActiveRuleAppLabels.Where(label => ruleIds.Contains(label.RuleId)).ToList();
            }
            if (query == LabellingQueries.getOwnerAppServerAppLabels)
            {
                return Owners;
            }
            return null;
        }

        private object Write(string query, object? variables)
        {
            if (query == LabellingQueries.upsertAppLabelKey)
            {
                UpsertedKeys.Add((string)Value(variables, "keyName")!);
                return new LabelKey { Id = kKeyId };
            }
            if (query == LabellingQueries.upsertLabelValues)
            {
                return UpsertValues(variables);
            }
            if (query == LabellingQueries.addLabelChangeEvent)
            {
                ChangeEvents.Add(((long?)Value(variables, "importControlId"), (bool)Value(variables, "originIsUi")!));
                return new ReturnId { Id = kEventId };
            }
            RecordAssignments(query, variables);
            return new ReturnId { AffectedRows = 1 };
        }

        private LabelValueUpsertResult UpsertValues(object? variables)
        {
            LabelValueUpsertResult result = new();
            foreach (object value in (IEnumerable)Value(variables, "values")!)
            {
                string appId = (string)Value(value, "value")!;
                UpsertedAppIds.Add(appId);
                LabelIdsByAppId.TryAdd(appId, 100 + LabelIdsByAppId.Count);
                result.Returning.Add(new LabelValue { Id = LabelIdsByAppId[appId], Value = appId });
            }
            return result;
        }

        private void RecordAssignments(string query, object? variables)
        {
            if (query == LabellingQueries.addRuleLabelAssignments)
            {
                foreach (object label in (IEnumerable)Value(variables, "labels")!)
                {
                    AddedRuleLabels.Add(((long)Value(label, "rule_id")!, (long)Value(label, "label_id")!, (long)Value(label, "created_event_id")!));
                    ActiveRuleAppLabels.Add(new RuleLabelRow { RuleId = (long)Value(label, "rule_id")!, LabelId = (long)Value(label, "label_id")! });
                }
            }
            else if (query == LabellingQueries.removeRuleLabelAssignments)
            {
                long eventId = (long)Value(variables, "removedEventId")!;
                foreach (object assignment in (IEnumerable)Value(variables, "assignments")!)
                {
                    long ruleId = (long)Value(Value(assignment, "rule_id"), "_eq")!;
                    long labelId = (long)Value(Value(assignment, "label_id"), "_eq")!;
                    ClosedRuleLabels.Add((ruleId, labelId, eventId));
                    ActiveRuleAppLabels.RemoveAll(label => label.RuleId == ruleId && label.LabelId == labelId);
                }
            }
            else if (query == LabellingQueries.addOwnerNetworkLabels)
            {
                foreach (object label in (IEnumerable)Value(variables, "labels")!)
                {
                    AddedServerLabels.Add(((long)Value(label, "owner_network_id")!, (long)Value(label, "label_id")!, (long)Value(label, "created_event_id")!));
                }
            }
            else if (query == LabellingQueries.removeOwnerNetworkLabels)
            {
                long ownerNetworkId = (long)Value(variables, "ownerNetworkId")!;
                long eventId = (long)Value(variables, "removedEventId")!;
                foreach (long labelId in (IEnumerable<long>)Value(variables, "labelIds")!)
                {
                    ClosedServerLabels.Add((ownerNetworkId, labelId, eventId));
                }
            }
            else
            {
                throw new NotImplementedException($"Query not implemented in App label test api: {query}");
            }
        }

        public static object? Value(object? variables, string propertyName)
        {
            return variables?.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance)?.GetValue(variables);
        }
    }
}
