using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics.Exceptions;
using FWO.Data;
using FWO.Data.Workflow;

namespace FWO.Services.Workflow
{
    /// <summary>
    /// Gateways an existing rule takes effect on according to the imported configuration (rule_enforced_on_gateway, on
    /// Check Point derived from install-on). A rule_modify or rule_delete changes the rule on all of them. Only the
    /// middleware may read them; the workflow roles have no access to the imported rules.
    /// </summary>
    public static class RuleEnforcingGateways
    {
        /// <summary>Most rules of one management whose gateways are read at once.</summary>
        public const int kMaxRules = 1000;

        /// <summary>Loads the gateways of the given rules of a management, by rule uid; rules not found are left out.</summary>
        /// <exception cref="CollectionLimitExceededException">Thrown for more than <see cref="kMaxRules"/> rules.</exception>
        public static async Task<Dictionary<string, List<Device>>> Load(ApiConnection apiConnection, int managementId, List<string> ruleUids)
        {
            if (ruleUids.Count == 0)
            {
                return [];
            }
            if (ruleUids.Count > kMaxRules)
            {
                throw new CollectionLimitExceededException(kMaxRules);
            }
            List<Rule> rules = await apiConnection.SendQueryAsync<List<Rule>>(RuleQueries.getRuleEnforcingGateways,
                new { managementId, ruleUids, ruleLimit = kMaxRules + 1 });
            if (rules.Count > kMaxRules)
            {
                throw new CollectionLimitExceededException(kMaxRules);
            }
            Dictionary<string, List<Device>> gatewaysByRuleUid = [];
            foreach (Rule rule in rules.Where(rule => !string.IsNullOrWhiteSpace(rule.Uid)))
            {
                List<Device> gateways = gatewaysByRuleUid.TryGetValue(rule.Uid!, out List<Device>? known) ? known : [];
                gateways.AddRange(rule.EnforcingGateways.Select(wrapper => wrapper.Content).Where(gateway => gateways.All(known => known.Id != gateway.Id)));
                gatewaysByRuleUid[rule.Uid!] = gateways;
            }
            return gatewaysByRuleUid;
        }

        /// <summary>Loads the gateways the rules of one implementation task take effect on, ordered by name.</summary>
        public static async Task<List<Device>> LoadOfImplTask(ApiConnection apiConnection, int managementId, WfImplTask implTask)
        {
            Dictionary<string, List<Device>> gatewaysByRuleUid = await Load(apiConnection, managementId, implTask.GetRuleUids());
            return [.. gatewaysByRuleUid.Values.SelectMany(gateways => gateways).DistinctBy(gateway => gateway.Id)
                .OrderBy(gateway => gateway.Name, StringComparer.OrdinalIgnoreCase)];
        }
    }
}
