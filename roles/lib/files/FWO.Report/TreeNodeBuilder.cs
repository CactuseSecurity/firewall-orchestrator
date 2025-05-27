using FWO.Data;
using FWO.Data.Report;
using System.Collections.Generic;
using System.Linq;

namespace FWO.Report
{
    public static class TreeNodeBuilder
    {
        /// <summary>
        /// Builds a tree structure of rules based on their hierarchical order numbers
        /// created by BuildOrderNumberTree method.
        /// </summary>
        /// <param name="rules">List of rules with DisplayOrderNumberString set</param>
        /// <returns>A list of tree nodes representing the rule hierarchy</returns>
        public static List<TreeNode<Rule>> BuildRuleTreeFromOrderedRules(List<Rule> rules)
        {
            Dictionary<string, Rule> rulesByOrderNumber = new();
            Dictionary<string, TreeNode<Rule>> treeNodes = new();
            List<TreeNode<Rule>> rootNodes = new();

            // Create a node for each rule
            foreach (Rule rule in rules)
            {
                TreeNode<Rule> node = new()
                {
                    Item = rule,
                    Children = new List<TreeNode<Rule>>()
                };
                
                // If it has a display order number string (like "1.2.3"), use it to structure the hierarchy
                if (!string.IsNullOrEmpty(rule.DisplayOrderNumberString))
                {
                    treeNodes[rule.DisplayOrderNumberString] = node;
                    rulesByOrderNumber[rule.DisplayOrderNumberString] = rule;
                }
            }

            // Build the tree structure based on hierarchical order numbers
            foreach (string key in treeNodes.Keys.OrderBy(k => k))
            {
                TreeNode<Rule> node = treeNodes[key];
                
                // Find parent node
                int lastDotIndex = key.LastIndexOf('.');
                
                if (lastDotIndex > 0) 
                {
                    // This is a child node, find its parent
                    string parentKey = key.Substring(0, lastDotIndex);
                    if (treeNodes.ContainsKey(parentKey))
                    {
                        treeNodes[parentKey].Children.Add(node);
                    }
                    else 
                    {
                        // Fallback if parent not found - add to root
                        rootNodes.Add(node);
                    }
                }
                else 
                {
                    // This is a root level node
                    rootNodes.Add(node);
                }
            }

            // Handle rules without hierarchical order numbers or section headers
            // Group them by RulebaseId as a fallback
            IOrderedEnumerable<IGrouping<int, Rule>> rulesWithoutOrderNumber = rules
                .Where(r => string.IsNullOrEmpty(r.DisplayOrderNumberString))
                .GroupBy(r => r.RulebaseId)
                .OrderBy(g => g.Key);
                
            foreach (IGrouping<int, Rule> group in rulesWithoutOrderNumber)
            {
                // Create a node for the section, if available
                if (!string.IsNullOrEmpty(group.Key.ToString()))
                {
                    TreeNode<Rule> sectionNode = new()
                    {
                        Item = new Rule { Name = group.Key.ToString(), SectionHeader = group.Key.ToString() },
                        Children = group.Select(r => new TreeNode<Rule> { Item = r }).ToList()
                    };
                    rootNodes.Add(sectionNode);
                }
                else
                {
                    // Rules without SectionHeader directly at the top level
                    rootNodes.AddRange(group.Select(r => new TreeNode<Rule> { Item = r }));
                }
            }

            return rootNodes;
        }

        /// <summary>
        /// Gets rules from management and device, processes them with BuildOrderNumberTree, 
        /// and returns a tree structure for display.
        /// </summary>
        public static List<TreeNode<Rule>> BuildRuleTreeFromManagementAndDevice(ManagementReportController managementReport, DeviceReportController deviceReport)
        {
            // Get all rules for the gateway, which will process them through CreateOrderNumbers and BuildOrderNumberTree
            List<Rule> orderedRules = ReportRules.GetAllRulesOfGateway(deviceReport, managementReport).ToList();
            
            // Build tree from the rules with hierarchical order numbers
            return BuildRuleTreeFromOrderedRules(orderedRules);
        }
    }
}
