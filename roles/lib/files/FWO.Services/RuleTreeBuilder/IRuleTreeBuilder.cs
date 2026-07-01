using FWO.Data;
using FWO.Data.Report;

namespace FWO.Services.RuleTreeBuilder
{
    /// <summary>
    /// Defines the public surface that report generation and UI components use to build,
    /// cache, and access rule trees.
    /// </summary>
    public interface IRuleTreeBuilder
    {
        /// <summary>
        /// Gets or sets the most recently built rule tree root.
        /// </summary>
        RuleTreeItem RuleTree { get; set; }

        /// <summary>
        /// Gets or sets the cache of fully built rule trees keyed by management and device.
        /// </summary>
        Dictionary<(int managementId, int deviceId), RuleTreeItem> RuleTreeCache { get; set; } // TODO: redundant? deviceId implies managementId

        /// <summary>
        /// Gets or sets the cache of flattened rule rows keyed by the corresponding rule tree root.
        /// </summary>
        Dictionary<RuleTreeItem, Rule[]> FlattenedRules { get; set; }

        /// <summary>
        /// Builds a rule tree from normalized rulebases and rulebase links and returns
        /// the flattened rules that reports render.
        /// </summary>
        List<Rule> BuildRuleTree(RulebaseReport[] rulebases, RulebaseLink[] links, int managementId, int deviceId, bool suppressEmptyHeaders = false);
    }
}
