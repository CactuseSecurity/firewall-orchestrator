using FWO.Basics;
using FWO.Data;
using FWO.Data.Report;

namespace FWO.Services.RuleTreeBuilder
{
    public interface IRuleTreeBuilder
    {
        RuleTreeItem RuleTree { get; set; }
        int CreatedOrderNumbersCount { get; set; }
        int OrderedLayerCount { get; set; }
        List<RulebaseLink> RemainingLinks { get; set;}  
        List<RulebaseReport> Rulebases { get; set; }
        List<Rule> BuildRuleTree(RulebaseReport[] rulebases, RulebaseLink[] links);
        RulebaseLink? GetNextLink();
        List<int> ProcessLink(RulebaseLink link, List<int>? trail = null);
        void Reset(RulebaseReport[] rulebases, RulebaseLink[] links);
    }
}

