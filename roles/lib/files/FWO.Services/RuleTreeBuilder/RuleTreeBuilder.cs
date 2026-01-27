using System.Data;
using System.Diagnostics;
using FWO.Basics;
using FWO.Data;
using FWO.Data.Report;
using Rule = FWO.Data.Rule;

namespace FWO.Services.RuleTreeBuilder
{
    /// <summary>
    /// Builds a rule tree and order numbers from rulebases and their links.
    /// </summary>
    public class RuleTreeBuilder : IRuleTreeBuilder
    {
        #region Properties & fields

        /// <summary>
        /// The root item for the tree structure.
        /// </summary>
        public RuleTreeItem RuleTree { get; set; } = new();

        /// <summary>
        /// The number of order numbers that were created during the process.
        /// </summary>
        public int CreatedOrderNumbersCount { get; set; }

        /// <summary>
        /// A counter to easily create the order number for the ordered layers on the top level.
        /// </summary>
        public int OrderedLayerCount { get; set; }

        /// <summary>
        /// Links that are still available to be processed.
        /// </summary>
        public List<RulebaseLink> RemainingLinks { get; set; } = new();

        /// <summary>
        /// Rulebases available to the current build.
        /// </summary>
        public List<RulebaseReport> Rulebases { get; set; } = new();

        /// <summary>
        /// Inline layer links that are still available to be processed.
        /// </summary>
        public List<RulebaseLink> RemainingInlineLayerLinks { get; set; } = new();

        #endregion

        #region Constructor

        /// <summary>
        /// Creates a new builder with an initialized root node.
        /// </summary>
        public RuleTreeBuilder()
        {
            RuleTree.IsRoot = true;
        }

        #endregion

        #region Methods - Public

        /// <summary>
        /// Builds the rule tree and returns the flattened list of rule data.
        /// </summary>
        public List<Rule> BuildRuleTree(RulebaseReport[] rulebases, RulebaseLink[] links)
        {
            Reset(rulebases, links);
            List<int>? trail = null;

            // Iterate links in processing order to build the tree and order numbers.

            while (GetNextLink() is RulebaseLink nextLink)
            {
                trail = ProcessLink(nextLink, trail);
            }

            // Returns the Rule objects of the flattened rule tree. Will be deprecated  as soon as we have the TreeView component.

            List<Rule> allRules = new();

            for (int i = 0; i < RuleTree.ElementsFlat.Count; i++)
            {
                RuleTreeItem treeItem = (RuleTreeItem)RuleTree.ElementsFlat[i];

                if ((treeItem.IsRule || treeItem.IsSectionHeader) && treeItem.Data != null)
                {
                    allRules.Add(treeItem.Data);
                }
            }

            return allRules;
        }

        /// <summary>
        /// Processes a single link and its rulebase, returning the updated trail.
        /// </summary>
        public List<int> ProcessLink(RulebaseLink link, List<int>? trail = null)
        {
            // Initialize trail if no trail is provided.

            if (trail == null || link.LinkType == 2)
            {
                trail = new();
            }

            // Get next rulebase.

            RulebaseReport? rulebase = Rulebases.FirstOrDefault(rb => rb.Id == link.NextRulebaseId);

            if (rulebase == null)
            {
                throw new InvalidOperationException("Rulebase for link could not be found.");
            }

            // Create tree item for the rulebase link type.

            if (link.LinkType == 2)
            {
                trail = ProcessOrderedLayerLink(link, rulebase, trail);
            }
            else if (RemainingInlineLayerLinks.Contains(link))
            {
                trail = ProcessInlineLayerLink(link, rulebase, trail);
            }
            else if (link.IsSection)
            {
                trail = ProcessSectionLink(link, rulebase, trail);
            }
            else if (link.LinkType == 4)
            {
                trail = ProcessConcatenationLink(link, rulebase, trail);
            }

            // Create tree items for rules and then process inline layers.

            trail = ProcessRulebase(rulebase, link, trail);

            return trail;
        }

        /// <summary>
        /// Processes an inline layer link by adding its root item to the tree.
        /// </summary>
        public List<int> ProcessInlineLayerLink(RulebaseLink link, RulebaseReport rulebase, List<int> trail)
        {
            RuleTreeItem inlineLayerItem = new RuleTreeItem();

            inlineLayerItem.Header = rulebase.Name ?? "";
            inlineLayerItem.IsInlineLayerRoot = true;
            SetParentForTreeItem(inlineLayerItem, link);
            RuleTree.LastAddedItem = inlineLayerItem;

            return trail;
        }

        /// <summary>
        /// Processes a section link by adding a section header rule to the tree.
        /// </summary>
        public List<int> ProcessSectionLink(RulebaseLink link, RulebaseReport rulebase, List<int> trail)
        {
            RuleTreeItem sectionLinkItem = new();

            sectionLinkItem.Header = rulebase.Name ?? "";
            sectionLinkItem.IsSectionHeader = true;
            SetParentForTreeItem(sectionLinkItem, link);
            Rule newRule = new();
            CreatedOrderNumbersCount++;
            newRule.OrderNumber = CreatedOrderNumbersCount;
            newRule.SectionHeader = rulebase.Name;
            sectionLinkItem.Data = newRule;
            RuleTree.LastAddedItem = sectionLinkItem;
            RuleTree.ElementsFlat.Add(sectionLinkItem);

            return trail;
        }

        /// <summary>
        /// Processes an ordered layer link and adds its header to the tree.
        /// </summary>
        public List<int> ProcessOrderedLayerLink(RulebaseLink link, RulebaseReport rulebase, List<int> trail)
        {
            RuleTreeItem orderedLayerItem = new();

            orderedLayerItem.Header = rulebase.Name ?? "";
            orderedLayerItem.IsOrderedLayerHeader = true;
            SetParentForTreeItem(orderedLayerItem, link);
            trail.Add(GetOrderLayerCount());
            orderedLayerItem.Position = trail.ToList();
            RuleTree.LastAddedItem = orderedLayerItem;

            return trail;
        }

        /// <summary>
        /// Processes a concatenation link (currently handled like a section link).
        /// </summary>
        public List<int> ProcessConcatenationLink(RulebaseLink link, RulebaseReport rulebase, List<int> trail)
        {
            return ProcessSectionLink(link, rulebase, trail); // TODO: Differentiate between concatenation and section if needed
        }

        /// <summary>
        /// Processes all rules of the rulebase and attaches them to the tree.
        /// </summary>
        public List<int> ProcessRulebase(RulebaseReport rulebase, RulebaseLink link, List<int> trail)
        {
            if (RuleTree.LastAddedItem != null)
            {
                RuleTreeItem lastAddedItem = (RuleTreeItem)RuleTree.LastAddedItem;

                for (int i = 0; i < rulebase.Rules.Count(); i++)
                {
                    Rule newRule = GetUniqueRuleObject(rulebase.Rules[i]);

                    RuleTreeItem ruleItem = new();
                    ruleItem.IsRule = true;
                    ruleItem.Data = newRule;

                    SetParentForTreeItem(ruleItem, link, lastAddedItem);

                    trail = EnsureTrailStartsWithZeroForFirstRule(link, lastAddedItem, trail, i);

                    // Increment the lowest level for the next rule order number.

                    int bottomLevelNumber = trail.Last() + 1;
                    trail[trail.Count - 1] = bottomLevelNumber;

                    newRule.DisplayOrderNumberString = string.Join(".", trail);
                    ruleItem.Position = trail.ToList();
                    CreatedOrderNumbersCount++;
                    newRule.OrderNumber = CreatedOrderNumbersCount;

                    RuleTree.ElementsFlat.Add(ruleItem);
                    RuleTree.LastAddedItem = ruleItem;

                    ProcessInlineLayerLinksForRule(rulebase, i, trail);
                }

                RuleTree.LastAddedItem = lastAddedItem;
            }

            return trail;
        }

        /// <summary>
        /// Returns the next link to process, preferring initial links.
        /// </summary>
        public RulebaseLink? GetNextLink()
        {
            // Get initial first

            if (RemainingLinks.Any(link => link.IsInitial))
            {
                RulebaseLink initialLink = RemainingLinks.First(link => link.IsInitial);
                RemainingLinks.Remove(initialLink);
                return initialLink;
            }

            // Get next link in line

            RulebaseLink? nextLink = RemainingLinks.FirstOrDefault();

            if (nextLink != null)
            {
                RemainingLinks.Remove(nextLink);
            }

            return nextLink;
        }

        /// <summary>
        /// Resets all state for a new build.
        /// </summary>
        public void Reset(RulebaseReport[] rulebases, RulebaseLink[] links)
        {
            RemainingLinks = links.ToList();
            Rulebases = rulebases.ToList();
            RemainingInlineLayerLinks = links.Where(link => link.LinkType == 3).ToList();
            RuleTree = new RuleTreeItem() { IsRoot = true };
            RuleTree.LastAddedItem = RuleTree;
            CreatedOrderNumbersCount = 0;
            OrderedLayerCount = 0;
        }


        #endregion

        #region Methods - Private

        /// <summary>
        /// Ensures a rule with a duplicate ID gets cloned instead of reused.
        /// </summary>
        private Rule GetUniqueRuleObject(Rule rule)
        {
            if (RuleTree.ElementsFlat.FirstOrDefault(treeItem => treeItem.Data != null && treeItem.Data.Id == rule.Id)?.Data is Rule existingRule)
            {
                return rule.CreateClone();
            }
            else
            {
                return rule;
            }
        }

        /// <summary>
        /// Ensures the trail starts with a zero for the first rule in a sequence.
        /// </summary>
        private List<int> EnsureTrailStartsWithZeroForFirstRule(RulebaseLink link, RuleTreeItem lastAddedItem, List<int> trail, int index)
        {
            if (index == 0
                    && (!(link.LinkType == 4)
                        || lastAddedItem.Parent?.Children.Count() == 1))
            {
                trail = trail.ToList();
                trail.Add(0);
            }

            return trail;
        }

        /// <summary>
        /// Processes any inline layer link that starts from the current rule.
        /// </summary>
        private void ProcessInlineLayerLinksForRule(RulebaseReport rulebase, int ruleIndex, List<int> trail)
        {
            if (RemainingInlineLayerLinks.Any(l => l.LinkType == 3 && l.FromRuleId == rulebase.Rules[ruleIndex].Id))
            {
                RulebaseLink inlineLayerLink = RemainingInlineLayerLinks.First(l => l.LinkType == 3 && l.FromRuleId == rulebase.Rules[ruleIndex].Id);
                RemainingInlineLayerLinks.Remove(inlineLayerLink);
                RemainingLinks.Remove(inlineLayerLink);
                List<int> innerTrail = ProcessLink(inlineLayerLink, trail);

                if (RemainingLinks.FirstOrDefault(x => inlineLayerLink.NextRulebaseId == x.FromRulebaseId) != null)
                {
                    RulebaseLink? nextLink = GetNextLink();
                    if (nextLink != null)
                    {
                        ProcessLink(nextLink, innerTrail);
                    }
                }
            }
        }

        /// <summary>
        /// Determines the correct parent for a tree item based on the link type.
        /// </summary>
        private void SetParentForTreeItem(RuleTreeItem item, RulebaseLink link, RuleTreeItem? parentOverride = null)
        {
            if (item.IsRule && parentOverride != null)
            {
                SetParentForTreeItem(parentOverride, item);
                return;
            }

            if (RuleTree.LastAddedItem is RuleTreeItem lastAddedItem && (lastAddedItem.Parent is RuleTreeItem parent || lastAddedItem.IsRoot))
            {
                if (RemainingInlineLayerLinks.Contains(link))
                {
                    SetParentForTreeItem(lastAddedItem, item);
                }
                else if (link.IsSection)
                {
                    SetParentForSectionTreeItem(lastAddedItem, item);
                }
                else if (link.LinkType == 2)
                {
                    SetParentForTreeItem(RuleTree, item);
                }
                else if (link.LinkType == 4 && lastAddedItem.Parent != null)
                {
                    SetParentForTreeItem((RuleTreeItem)lastAddedItem.Parent, item);
                }
            }
        }

        /// <summary>
        /// Sets the parent for a section item, handling root and section headers.
        /// </summary>
        private void SetParentForSectionTreeItem(RuleTreeItem lastAddedItem, RuleTreeItem item)
        {
            if (lastAddedItem.IsRoot)
            {
                SetParentForTreeItem(RuleTree, item);
            }
            else if (lastAddedItem.IsSectionHeader == true && lastAddedItem.Parent != null)
            {
                SetParentForTreeItem((RuleTreeItem)lastAddedItem.Parent, item);
            }
            else
            {
                SetParentForTreeItem(lastAddedItem, item);
            }
        }

        /// <summary>
        /// Assigns parent-child relationship and updates last-added pointer.
        /// </summary>
        private void SetParentForTreeItem(RuleTreeItem parent, RuleTreeItem item)
        {
            item.Parent = parent;
            parent.Children.Add(item);
            parent.LastAddedItem = item;
        }

        /// <summary>
        /// Counts top-level ordered layer headers to build their order number.
        /// </summary>
        private int GetOrderLayerCount()
        {
            return RuleTree.Children.Count(treeItem => ((RuleTreeItem)treeItem).IsOrderedLayerHeader);
        }

        #endregion
    }
}
