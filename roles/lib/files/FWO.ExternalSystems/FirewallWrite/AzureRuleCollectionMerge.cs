using FWO.Basics.Exceptions;
using FWO.Data.DeviceSpecificSettings;
using System.Text.Json.Nodes;

namespace FWO.ExternalSystems.FirewallWrite
{
    /// <summary>
    /// Applies a rendered Azure rule template to a rule collection group. The template carries the rule ("rule"), the
    /// collection it belongs to ("ruleCollectionName", "ruleCollectionPriority", "ruleCollectionAction") or, for a
    /// deletion, only the rule name ("ruleName"). Rules are matched by name; creating a rule whose name is already used in
    /// the group fails instead of replacing that rule.
    /// </summary>
    public static class AzureRuleCollectionMerge
    {
        private const string kProperties = "properties";
        private const string kRuleCollections = "ruleCollections";
        private const string kRules = "rules";
        private const string kName = "name";
        private const string kDefaultCollectionName = "FWO";
        private const int kDefaultCollectionPriority = 10000;

        /// <summary>Changes the rule collection group in place.</summary>
        /// <exception cref="ConfigException">
        /// Thrown if the template lacks the rule, the rule to change does not exist or the rule to create already exists.
        /// </exception>
        public static void Apply(JsonObject ruleCollectionGroup, FirewallWriteOperation operation, JsonObject fragment)
        {
            JsonArray collections = ruleCollectionGroup[kProperties]?[kRuleCollections] as JsonArray
                ?? throw new ConfigException("The Azure rule collection group has no rule collections.");
            if (operation == FirewallWriteOperation.RuleDelete)
            {
                string ruleName = fragment["ruleName"]?.GetValue<string>() ?? throw new ConfigException("The Azure rule delete template needs \"ruleName\".");
                if (!collections.OfType<JsonObject>().Any(collection => RemoveRule(collection, ruleName)))
                {
                    throw new ConfigException($"Azure rule '{ruleName}' not found.");
                }
                return;
            }

            JsonObject rule = fragment["rule"]?.DeepClone() as JsonObject ?? throw new ConfigException("The Azure rule template needs a \"rule\" object.");
            string name = rule[kName]?.GetValue<string>() ?? throw new ConfigException("The Azure rule template needs a rule name.");
            if (operation == FirewallWriteOperation.RuleModify)
            {
                JsonObject existingCollection = collections.OfType<JsonObject>().FirstOrDefault(collection => FindRule(collection, name) >= 0)
                    ?? throw new ConfigException($"Azure rule '{name}' not found.");
                Rules(existingCollection)[FindRule(existingCollection, name)] = rule;
                return;
            }

            if (collections.OfType<JsonObject>().Any(collection => FindRule(collection, name) >= 0))
            {
                throw new ConfigException($"Azure rule '{name}' already exists in the rule collection group.");
            }
            Rules(FindOrAddCollection(collections, fragment)).Add(rule);
        }

        private static JsonObject FindOrAddCollection(JsonArray collections, JsonObject fragment)
        {
            string collectionName = fragment["ruleCollectionName"]?.GetValue<string>() ?? kDefaultCollectionName;
            JsonObject? collection = collections.OfType<JsonObject>().FirstOrDefault(candidate => candidate[kName]?.GetValue<string>() == collectionName);
            if (collection != null)
            {
                return collection;
            }
            collection = new JsonObject
            {
                ["ruleCollectionType"] = "FirewallPolicyFilterRuleCollection",
                [kName] = collectionName,
                ["priority"] = fragment["ruleCollectionPriority"]?.GetValue<int>() ?? kDefaultCollectionPriority,
                ["action"] = new JsonObject { ["type"] = fragment["ruleCollectionAction"]?.GetValue<string>() ?? "Allow" },
                [kRules] = new JsonArray()
            };
            collections.Add(collection);
            return collection;
        }

        private static JsonArray Rules(JsonObject collection)
        {
            if (collection[kRules] is not JsonArray rules)
            {
                rules = [];
                collection[kRules] = rules;
            }
            return rules;
        }

        private static int FindRule(JsonObject collection, string ruleName)
        {
            JsonArray rules = Rules(collection);
            for (int index = 0; index < rules.Count; index++)
            {
                if (rules[index]?[kName]?.GetValue<string>() == ruleName)
                {
                    return index;
                }
            }
            return -1;
        }

        private static bool RemoveRule(JsonObject collection, string ruleName)
        {
            int index = FindRule(collection, ruleName);
            if (index < 0)
            {
                return false;
            }
            Rules(collection).RemoveAt(index);
            return true;
        }
    }
}
