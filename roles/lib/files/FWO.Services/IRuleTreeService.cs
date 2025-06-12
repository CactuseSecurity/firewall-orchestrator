using FWO.Basics;
using FWO.Data;
using FWO.Data.Report;

namespace FWO.Services
{
    public interface IRuleTreeService
    {
        TreeItem<Rule> RuleTree { get; set; }
        Queue<(RulebaseLink link, RulebaseReport rulebase)> RuleTreeBuilderQueue { get; set; }
        int CreatedOrderNumbersCount { get; set; }
        int OrderedLayerCount { get; set; }

        Queue<(RulebaseLink, RulebaseReport)>? BuildRulebaseLinkQueue(RulebaseLink[] links, RulebaseReport[] rulebases);
        void BuildRuleTree();
    }
}

