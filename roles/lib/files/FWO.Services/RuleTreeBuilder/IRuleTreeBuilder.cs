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
        List<RulebaseLink> RemainingLinks { get; set;}  
        List<RulebaseReport> Rulebases { get; set; }
        List<Rule> BuildRuleTree(ManagementReport managementReport, DeviceReport deviceReport);

        Queue<(RulebaseLink, RulebaseReport)>? BuildRulebaseLinkQueue(RulebaseLink[] links, RulebaseReport[] rulebases);
        List<Rule> BuildRuleTree();
        RulebaseLink? GetNextLink(int? fromRulebaseId);
        void ProcessLink(RulebaseLink link);
        void Reset(List<RulebaseLink> links, List<RulebaseReport> rulebases);
    }
}

