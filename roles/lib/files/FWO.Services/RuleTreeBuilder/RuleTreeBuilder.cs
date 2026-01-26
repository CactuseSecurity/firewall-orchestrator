using System.Data;
using System.Diagnostics;
using FWO.Basics;
using FWO.Data;
using FWO.Data.Report;
using Rule = FWO.Data.Rule;

namespace FWO.Services.RuleTreeBuilder
{
    public class RuleTreeBuilder : IRuleTreeBuilder
    {
        #region Properties & fields

        /// <summary>
        /// The root item for the tree structure.
        /// </summary>
        public RuleTreeItem RuleTree { get; set; } = new();
        
        /// <summary>
        /// The number of order numbers (i.e. number of processed rules) that were created during the process.
        /// </summary>
        public int CreatedOrderNumbersCount { get; set; }
        
        /// <summary>
        /// A counter to easily create the order number for the ordered layers on the top level.
        /// </summary>
        public int OrderedLayerCount { get; set; }

        public List<RulebaseLink> RemainingLinks { get; set; } = new();  
        public List<RulebaseReport> Rulebases { get; set; } = new();
        public List<RulebaseLink> RemainingInlineLayerLinks { get; set; } = new();
        
        #endregion

        #region Constructor

        public RuleTreeBuilder()
        {
            RuleTree.IsRoot = true;
        }

        #endregion

        #region Methods - Public

        public List<Rule> BuildRuleTree(RulebaseReport[] rulebases, RulebaseLink[] links)
        {
            Reset(rulebases, links);
            List<int>? trail = null;
            
            while (GetNextLink() is RulebaseLink nextLink)
            {
                
                trail = ProcessLink(nextLink, trail);
            }

            return RuleTree.ElementsFlat.Where(item => ((RuleTreeItem) item).IsRule).Select(item => item.Data!).ToList();
        }

        public List<int> ProcessLink(RulebaseLink link, List<int>? trail = null)
        {
            // Initialize trail if no trail is provided.

            if (trail == null || link.LinkType == 2)
            {
                trail = new();
            }

            // Get next rulebease.

            RulebaseReport? rulebase = Rulebases.FirstOrDefault(rb => rb.Id == link.NextRulebaseId);

            if (rulebase == null)
            {
                throw new InvalidOperationException("Rulebase for link could not be found.");
            }

            // Create tree item for rulebase.

            if (link.LinkType == 2)
            {
                trail = ProcessOrderedLayerLink(link, rulebase, trail);
            }
            else if(RemainingInlineLayerLinks.Contains(link))
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

           // Create tree items for rules and for process inline layers.

            trail = ProcessRulebase(rulebase, link, trail);

            return trail;
        }

        public List<int> ProcessInlineLayerLink(RulebaseLink link, RulebaseReport rulebase, List<int> trail)
        {
            RuleTreeItem inlineLayerItem = new RuleTreeItem();

            inlineLayerItem.Header = rulebase.Name ?? "";
            inlineLayerItem.IsInlineLayerRoot = true;
            SetParentForTreeItem(inlineLayerItem, link); 
            RuleTree.LastAddedItem = inlineLayerItem;

            return trail;
        }

        public List<int> ProcessSectionLink(RulebaseLink link, RulebaseReport rulebase, List<int> trail)
        {
            RuleTreeItem sectionLinkItem = new();

            sectionLinkItem.Header = rulebase.Name ?? "";
            sectionLinkItem.IsSectionHeader = true;
            SetParentForTreeItem(sectionLinkItem, link);
            RuleTree.LastAddedItem = sectionLinkItem;

            return trail;
        }

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

        public List<int> ProcessConcatenationLink(RulebaseLink link, RulebaseReport rulebase, List<int> trail)
        {
            return ProcessSectionLink(link, rulebase, trail); // TODO: Differentiate between concatenation and section if needed
        }

        public List<int> ProcessRulebase(RulebaseReport rulebase, RulebaseLink link, List<int> trail)
        {
            if (RuleTree.LastAddedItem != null)
            {
                RuleTreeItem lastAddedItem = (RuleTreeItem) RuleTree.LastAddedItem;

                for (int i = 0; i < rulebase.Rules.Count(); i++)
                {
                    Rule newRule = GetUniqueRuleObject(rulebase.Rules[i]);

                    RuleTreeItem ruleItem = new();
                    ruleItem.IsRule = true;
                    ruleItem.Data = newRule;
                    
                    SetParentForTreeItem(ruleItem, link, lastAddedItem);

                    if (i == 0 
                            && (!(link.LinkType == 4) 
                                || lastAddedItem.Parent?.Children.Count() ==1) )
                    {
                        trail = trail.ToList();
                        trail.Add(0);                        
                    }

                    int bottomLevelNumber = trail.Last() + 1;
                    trail[trail.Count - 1] = bottomLevelNumber;      

                    newRule.DisplayOrderNumberString = string.Join(".", trail);
                    ruleItem.Position = trail.ToList();
                    CreatedOrderNumbersCount++;
                    newRule.OrderNumber = CreatedOrderNumbersCount;
                    
                    RuleTree.ElementsFlat.Add(ruleItem);
                    RuleTree.LastAddedItem = ruleItem;

                    if (RemainingInlineLayerLinks.Any(l => l.LinkType == 3 && l.FromRuleId == rulebase.Rules[i].Id))
                    {
                        RulebaseLink inlineLayerLink = RemainingInlineLayerLinks.First(l => l.LinkType == 3 && l.FromRuleId == rulebase.Rules[i].Id);
                        RemainingInlineLayerLinks.Remove(inlineLayerLink);
                        RemainingLinks.Remove(inlineLayerLink);
                        List<int> innerTrail = ProcessLink(inlineLayerLink, trail);

                        if ( RemainingLinks.FirstOrDefault(x => inlineLayerLink.NextRulebaseId == x.FromRulebaseId ) != null )
                        {
                            RulebaseLink? nextLink = GetNextLink();
                            if (nextLink != null)
                            {
                                innerTrail = ProcessLink(nextLink, innerTrail);
                            }
                            
                        }
                    }
                }

                RuleTree.LastAddedItem = lastAddedItem;                
            }

            return trail;
        }

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

        private FWO.Data.Rule GetUniqueRuleObject(FWO.Data.Rule rule)
        {
            if (RuleTree.ElementsFlat.FirstOrDefault(treeItem => treeItem.Data != null && treeItem.Data.Id == rule.Id) is Rule existingRule)
            {
                return rule.CreateClone();
            }
            else
            {
                return rule;
            }
        }

        private void SetParentForTreeItem(RuleTreeItem item, RulebaseLink link, RuleTreeItem? parentOverride = null)
        {
            if (item.IsRule && parentOverride != null)
            {
                SetParentForTreeItem(parentOverride, item);
                return;
            }
            
            if (RuleTree.LastAddedItem is RuleTreeItem lastAddedItem && (lastAddedItem.Parent is RuleTreeItem parent || lastAddedItem.IsRoot) )
            {
                if(RemainingInlineLayerLinks.Contains(link))
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
                else if (link.LinkType == 4)
                {
                    if(lastAddedItem.Parent != null)
                    {
                        SetParentForTreeItem((RuleTreeItem) lastAddedItem.Parent, item); 
                    }
                }                
            }            
        }

        private void SetParentForSectionTreeItem(RuleTreeItem lastAddedItem, RuleTreeItem item)
        {
            if (lastAddedItem.IsRoot)
            {
                SetParentForTreeItem(RuleTree, item);
            }
            else if (lastAddedItem.IsSectionHeader == true)
            {
                if (lastAddedItem.Parent != null)
                {
                    SetParentForTreeItem((RuleTreeItem) lastAddedItem.Parent, item); 
                } 
            }
            else
            {
                SetParentForTreeItem(lastAddedItem, item);
            }
        }

        private void SetParentForTreeItem(RuleTreeItem parent, RuleTreeItem item)
        {
            item.Parent = parent;
            parent.Children.Add(item);
            parent.LastAddedItem = item;
        }

        private int GetOrderLayerCount()
        {
           return RuleTree.Children.Count(treeItem => ((RuleTreeItem )treeItem).IsOrderedLayerHeader);
        }

        #endregion
    }
}
