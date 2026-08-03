using FWO.Config.Api;
using FWO.Report;
using FWO.Report.Filter;
using FWO.Services.RuleTreeBuilder;

namespace FWO.Test.Mocks
{
    public class MockReportNatRules : ReportNatRules
    {
        public MockReportNatRules(DynGraphqlQuery query, UserConfig userConfig, ReportType reportType, IRuleTreeBuilder? ruleTreeBuilder = null)
            : base(query, userConfig, reportType, ruleTreeBuilder) { }

        public void TryBuildMockRuleTree()
        {
            TryBuildRuleTree();
        }
    }
}
