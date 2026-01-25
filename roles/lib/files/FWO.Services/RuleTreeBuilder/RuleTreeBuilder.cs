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
        /// The number of order numbers that were created during the process.
        /// </summary>
        public int CreatedOrderNumbersCount { get; set; }
        
        /// <summary>
        /// A counter to easily create the order position (/order number) for the ordered layers on the top level.
        /// </summary>
        public int OrderedLayerCount { get; set; }

        public List<RulebaseLink> RemainingLinks { get; set; } = new();  
        public List<RulebaseReport> Rulebases { get; set; } = new();
        public List<RulebaseLink> RemainingInlineLayerLinks { get; set; } = new();
        
        /// <summary>
        /// All of the processed rules.
        /// </summary>
        private readonly List<FWO.Data.Rule> _allRules = [];

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

            while (GetNextLink() is RulebaseLink nextLink)
            {
                ProcessLink(nextLink);
            }

            return RuleTree.ElementsFlat.Where(item => ((RuleTreeItem) item).IsRule).Select(item => item.Data!).ToList();
        }

        public void ProcessLink(RulebaseLink link)
        {
            RulebaseReport? rulebase = Rulebases.FirstOrDefault(rb => rb.Id == link.NextRulebaseId);

            if (rulebase == null)
            {
                throw new InvalidOperationException("Rulebase for link could not be found.");
            }

            if(RemainingInlineLayerLinks.Contains(link))
            {
                ProcessInlineLayerLink(link, rulebase);
            }
            else if (link.IsSection)
            {
                ProcessSectionLink(link, rulebase);
            }
            else if (link.LinkType == 2)
            {
                ProcessOrderedLayerLink(link, rulebase);
            }
            else if (link.LinkType == 4)
            {
                ProcessConcatenationLink(link, rulebase);
            }

            //RuleTree.ElementsFlat.Add(RuleTree.LastAddedItem ?? new RuleTreeItem());

            ProcessRulebase(rulebase, link);
        }

        public void ProcessInlineLayerLink(RulebaseLink link, RulebaseReport rulebase)
        {
            RuleTreeItem inlineLayerItem = new RuleTreeItem();
            inlineLayerItem.Header = rulebase.Name ?? "";
            inlineLayerItem.IsInlineLayerRoot = true;
            SetParentForTreeItem(inlineLayerItem, link); 
            RuleTree.LastAddedItem = inlineLayerItem;
        }

        public void ProcessSectionLink(RulebaseLink link, RulebaseReport rulebase)
        {
            RuleTreeItem sectionLinkItem = new();
            sectionLinkItem.Header = rulebase.Name ?? "";
            sectionLinkItem.IsSectionHeader = true;
            SetParentForTreeItem(sectionLinkItem, link);
            RuleTree.LastAddedItem = sectionLinkItem;
        }

        public void ProcessOrderedLayerLink(RulebaseLink link, RulebaseReport rulebase)
        {
            RuleTreeItem orderedLayerItem = new();
            OrderedLayerCount++;
            orderedLayerItem.Position = [OrderedLayerCount];
            orderedLayerItem.Header = rulebase.Name ?? "";
            orderedLayerItem.IsOrderedLayerHeader = true;
            SetParentForTreeItem(orderedLayerItem, link);
            RuleTree.LastAddedItem = orderedLayerItem;
        }

        public void ProcessConcatenationLink(RulebaseLink link, RulebaseReport rulebase)
        {
            ProcessSectionLink(link, rulebase); // TODO: Differentiate between concatenation and section if needed
        }

        public void ProcessRulebase(RulebaseReport rulebase, RulebaseLink link)
        {
            if (RuleTree.LastAddedItem != null)
            {
                RuleTreeItem lastAddedItem = (RuleTreeItem) RuleTree.LastAddedItem;
                List<int> position = new();

                IncrementPosition(lastAddedItem, position, rulebase);

                foreach (FWO.Data.Rule rule in rulebase.Rules)
                {
                    FWO.Data.Rule newRule = GetUniqueRuleObject(rule);
                    RuleTreeItem ruleItem = new();
                    ruleItem.IsRule = true;
                    ruleItem.Data = newRule;
                    
                    
                    SetParentForTreeItem(ruleItem, link, lastAddedItem);

                    try
                    {
                        if (position.Any())
                        {
                            int bottomLevelNumber = position.Last() + 1;
                            position[position.Count - 1] = bottomLevelNumber;                            
                        }

                        newRule.DisplayOrderNumberString = string.Join(".", position);
                        ruleItem.Position = position.ToList();
                        CreatedOrderNumbersCount++;
                        newRule.OrderNumber = CreatedOrderNumbersCount;
                    }
                    catch (System.Exception e)
                    {
                        Logging.Log.WriteError("Rule Tree Builder", $"Error processing rule ID {rule.Id} in rulebase ID {rulebase.Id}: {e.Message}");
                        throw;
                    }
                    
                    RuleTree.ElementsFlat.Add(ruleItem);
                    RuleTree.LastAddedItem = ruleItem;

                    if (RemainingInlineLayerLinks.Any(l => l.LinkType == 3 && l.FromRuleId == rule.Id))
                    {
                        RulebaseLink inlineLayerLink = RemainingInlineLayerLinks.First(l => l.LinkType == 3 && l.FromRuleId == rule.Id);
                        RemainingInlineLayerLinks.Remove(inlineLayerLink);
                        RemainingLinks.Remove(inlineLayerLink);
                        ProcessLink(inlineLayerLink);

                        if (rule == rulebase.Rules.Last() 
                                                    && RemainingLinks.FirstOrDefault(x => rulebase.Id == x.FromRulebaseId ) != null )
                        {
                            RulebaseLink? nextLink = GetNextLink();
                            if (nextLink != null)
                            {
                                ProcessLink(nextLink);
                            }
                            
                        }
                    }

                    // Reset position to  remain on correct level

                    position = ruleItem.Position.ToList();
                }



                if (RuleTree.LastAddedItem is ITreeItem<Rule> treeItem && treeItem.Position is List<int> treeItemPosition)
                {
                    if (treeItemPosition.Count == 2 && lastAddedItem.Position != null && lastAddedItem.Position.Count == 1
                            && treeItemPosition[0] == 3 
                            && lastAddedItem.Position[0] == 3 
                            && treeItemPosition[1] == 1)
                    {
                        return;
                    }
                }
                RuleTree.LastAddedItem = lastAddedItem;                
            }
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
            _allRules.Clear();
        }


        #endregion

        #region Methods - Private

        private FWO.Data.Rule GetUniqueRuleObject(FWO.Data.Rule rule)
        {
            if (RuleTree.ElementsFlat.FirstOrDefault(treeItem => treeItem.Data != null && treeItem.Data == rule) is Rule existingRule)
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

        /// <summary>
        /// Increments the position based on the last added item type. 
        /// </summary>
        /// <param name="lastAddedItem"></param>
        /// <param name="position"></param>
        /// <param name="rulebase"></param>
        private void IncrementPosition(RuleTreeItem lastAddedItem, List<int> position, RulebaseReport rulebase)
        {
            if (lastAddedItem.IsOrderedLayerHeader)
            {
                position.Add(OrderedLayerCount);
                position.Add(0);
            }
            else if (lastAddedItem.IsInlineLayerRoot)
            {
                position = lastAddedItem.Position?.ToList() ?? [];
                position.Add(0);
            }
            else if (lastAddedItem.IsConcatenationRoot || lastAddedItem.IsSectionHeader)
            {
                if (lastAddedItem.Parent is RuleTreeItem lastAddedItemParent)
                {
                    if (lastAddedItemParent.IsOrderedLayerHeader)
                    {
                        position.Add(OrderedLayerCount);

                        if (lastAddedItemParent.Children.Any(child => child != lastAddedItem) )
                        {
                            position.Add(0);
                        }

                    }
                    else if (lastAddedItemParent.IsInlineLayerRoot)
                    {
                        if (lastAddedItemParent.Children.Any())
                        {
                            position = lastAddedItemParent.LastAddedItem?.Position?.ToList() ?? [];
                        }
                        else
                        {
                            position = lastAddedItemParent.Position?.ToList() ?? [];
                            position.Add(0);
                        }


                        position = lastAddedItemParent.Position?.ToList() ?? [];
                        position.Add(0);
                    }
                    else
                    {
                        position = lastAddedItemParent.Position?.ToList() ?? [];
                        position.Add(0);
                    }
                }
            }
        }

        #endregion
    }
}
