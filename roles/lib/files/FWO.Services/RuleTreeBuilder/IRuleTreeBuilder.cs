using FWO.Basics;
using FWO.Data;
using FWO.Data.Report;

namespace FWO.Services.RuleTreeBuilder
{
    public interface IRuleTreeBuilder
    {
        RuleTreeItem RuleTree { get; set; }
        Queue<(RulebaseLink link, RulebaseReport rulebase)> RuleTreeBuilderQueue { get; set; }
        int CreatedOrderNumbersCount { get; set; }
        int OrderedLayerCount { get; set; }

        Queue<(RulebaseLink, RulebaseReport)>? BuildRulebaseLinkQueue(RulebaseLink[] links, RulebaseReport[] rulebases);
        List<Rule> BuildRuleTree();
    }
}

