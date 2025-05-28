using FWO.Data;
using FWO.Data.Report;

namespace FWO.Report
{
    public static class TreeNodeBuilder
    {
        /// <summary>
        /// Gets rules from management and device, processes them with CreateOrderNumberTree,
        /// and returns a tree structure for display.
        /// </summary>
        public static List<TreeNode<Rule>> BuildRuleTreeFromManagementAndDevice(ManagementReportController managementReport, DeviceReportController deviceReport)
        {
            // Get all rules for the gateway, which will process them through CreateOrderNumberTree
            List<Rule> orderedRules = ReportRules.GetAllRulesOfGateway(deviceReport, managementReport).ToList();

            // Build tree from the rules with hierarchical order numbers
            return ReportRules.CreateOrderNumberTree(orderedRules, deviceReport);
        }
    }
}
