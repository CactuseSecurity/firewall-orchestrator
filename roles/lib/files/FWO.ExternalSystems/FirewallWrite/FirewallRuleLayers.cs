using FWO.Data;
using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace FWO.ExternalSystems.FirewallWrite
{
    /// <summary>Answer of the query for the imported rulebases of the rules of a task and the section links above them.</summary>
    public sealed class FirewallRuleLayerQueryResult
    {
        /// <summary>The current access rules with the uids of the task, each with its rulebase.</summary>
        [JsonProperty("rules"), JsonPropertyName("rules")]
        public List<Rule> Rules { get; set; } = [];

        /// <summary>The current section links of the management, from the parent rulebase to the section.</summary>
        [JsonProperty("sectionLinks"), JsonPropertyName("sectionLinks")]
        public List<RulebaseLink> SectionLinks { get; set; } = [];
    }

    /// <summary>
    /// Determines the layer of existing rules from the imported configuration, which a rule to modify or delete is
    /// addressed in. The import stores a section as rulebase of its own below its layer, so a rule in a section is traced
    /// back along the section links to the layer above.
    /// </summary>
    public static class FirewallRuleLayers
    {
        /// <summary>Most rules of one task whose layer is looked up.</summary>
        public const int kMaxRules = 1000;

        /// <summary>Most section links of one management that are read.</summary>
        public const int kMaxSectionLinks = 10000;

        /// <summary>Most sections nested above a rule.</summary>
        public const int kMaxSectionDepth = 100;

        /// <summary>Returns the layer uid of each rule found in the import, by rule uid.</summary>
        /// <exception cref="FirewallWriteException">Thrown if the import data exceeds the limits or is ambiguous.</exception>
        public static Dictionary<string, string> Resolve(FirewallRuleLayerQueryResult result)
        {
            ArgumentNullException.ThrowIfNull(result);
            if (result.Rules.Count > kMaxRules || result.SectionLinks.Count > kMaxSectionLinks)
            {
                throw new FirewallWriteException(FirewallWriteErrorKind.Configuration,
                    $"The imported configuration holds more than {kMaxRules} rules or {kMaxSectionLinks} section links to look up the layer of a rule in.");
            }
            Dictionary<long, Rulebase> rulebasesById = RulebasesOfLinks(result.SectionLinks);
            Dictionary<long, long> parentBySectionId = ParentsOfSections(result.SectionLinks);
            Dictionary<string, string> layerByRuleUid = [];
            foreach (Rule rule in result.Rules.Where(rule => !string.IsNullOrWhiteSpace(rule.Uid)))
            {
                string? layerUid = LayerOf(rule.Rulebase, rulebasesById, parentBySectionId);
                if (string.IsNullOrWhiteSpace(layerUid))
                {
                    continue;
                }
                if (layerByRuleUid.TryGetValue(rule.Uid!, out string? knownLayerUid) && knownLayerUid != layerUid)
                {
                    throw new FirewallWriteException(FirewallWriteErrorKind.Configuration,
                        $"The imported rule '{rule.Uid}' lies in more than one layer.");
                }
                layerByRuleUid[rule.Uid!] = layerUid;
            }
            return layerByRuleUid;
        }

        /// <summary>The rulebases named by the section links, by id: the parent layers and the sections.</summary>
        private static Dictionary<long, Rulebase> RulebasesOfLinks(List<RulebaseLink> sectionLinks)
        {
            Dictionary<long, Rulebase> rulebasesById = [];
            foreach (RulebaseLink link in sectionLinks)
            {
                if (link.FromRulebase != null)
                {
                    rulebasesById[link.FromRulebase.Id] = link.FromRulebase;
                }
                if (link.ToRulebase != null)
                {
                    rulebasesById[link.ToRulebase.Id] = link.ToRulebase;
                }
            }
            return rulebasesById;
        }

        /// <summary>The parent rulebase of each section; the links of several gateways name the same parent.</summary>
        private static Dictionary<long, long> ParentsOfSections(List<RulebaseLink> sectionLinks)
        {
            Dictionary<long, long> parentBySectionId = [];
            foreach (RulebaseLink link in sectionLinks.Where(link => link.ToRulebase != null && link.FromRulebaseId != null))
            {
                long sectionId = link.ToRulebase!.Id;
                long parentId = link.FromRulebaseId!.Value;
                if (parentBySectionId.TryGetValue(sectionId, out long knownParentId) && knownParentId != parentId)
                {
                    throw new FirewallWriteException(FirewallWriteErrorKind.Configuration,
                        $"The imported section '{link.ToRulebase.Uid}' lies below more than one rulebase.");
                }
                parentBySectionId[sectionId] = parentId;
            }
            return parentBySectionId;
        }

        /// <summary>The uid of the layer a rulebase belongs to, traced up along the section links to the first rulebase that is no section; null if the trace breaks or is too deep.</summary>
        private static string? LayerOf(Rulebase rulebase, Dictionary<long, Rulebase> rulebasesById, Dictionary<long, long> parentBySectionId)
        {
            Rulebase current = rulebase;
            for (int depth = 0; depth <= kMaxSectionDepth; depth++)
            {
                if (!parentBySectionId.TryGetValue(current.Id, out long parentId))
                {
                    return current.Uid;
                }
                if (!rulebasesById.TryGetValue(parentId, out Rulebase? parent))
                {
                    return null;
                }
                current = parent;
            }
            throw new FirewallWriteException(FirewallWriteErrorKind.Configuration,
                $"The imported rulebase '{rulebase.Uid}' is nested in more than {kMaxSectionDepth} sections.");
        }
    }
}
