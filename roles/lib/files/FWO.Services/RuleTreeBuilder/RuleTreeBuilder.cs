using System.Data;
using System.Diagnostics;
using FWO.Basics;
using FWO.Data;
using FWO.Data.Report;

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
        /// A Queue to process rulebases by rulebase links.
        /// </summary>
        public Queue<(RulebaseLink link, RulebaseReport rulebase)> RuleTreeBuilderQueue { get; set; } = new();
        
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

        public RulebaseLink? GetNextLink(int? fromRulebaseId = 0)
        {
            if (RemainingLinks.Any(link => link.IsInitial))
            {
                RulebaseLink initialLink = RemainingLinks.First(link => link.IsInitial);
                RemainingLinks.Remove(initialLink);
                return initialLink;
            }

            return RemainingLinks.FirstOrDefault(link => link.FromRulebaseId == fromRulebaseId);
        }

        public void Reset(List<RulebaseLink> links, List<RulebaseReport> rulebases)
        {
            RemainingLinks = links.Where(link => link.LinkType != 3).ToList();
            Rulebases = rulebases;
            RemainingInlineLayerLinks = links.Where(link => link.LinkType == 3).ToList();
            RuleTree = new RuleTreeItem() { IsRoot = true };
            RuleTree.LastAddedItem = RuleTree;
            RuleTreeBuilderQueue = new Queue<(RulebaseLink link, RulebaseReport rulebase)>();
            CreatedOrderNumbersCount = 0;
            OrderedLayerCount = 0;
            _allRules.Clear();
        }

        public void ProcessLink(RulebaseLink link)
        {
            RulebaseReport? rulebase = null;

            if(RemainingInlineLayerLinks.Contains(link))
            {
                rulebase = ProcessInlineLayerLink(link);
            }
            else if (link.IsSection)
            {
                rulebase = ProcessSectionLink(link);
            }
            else if (link.LinkType == 2)
            {
                rulebase = ProcessOrderedLayerLink(link);
            }
            else if (link.LinkType == 4)
            {
                rulebase = ProcessConcatenationLink(link);
            }

            if (rulebase == null)
            {
                throw new InvalidOperationException("Rulebase for link could not be found.");
            }

            RuleTree.ElementsFlat.Add((RuleTreeItem) (RuleTree.LastAddedItem ?? new RuleTreeItem()));

            ProcessRulebase(rulebase, link);
        }

        public List<Data.Rule> BuildRuleTree(ManagementReport managementReport, DeviceReport deviceReport)
        {
            int rulebaseId = deviceReport.GetInitialRulebaseId(managementReport) ?? 0;

            while (GetNextLink(rulebaseId) is RulebaseLink nextLink)
            {
                ProcessLink(nextLink);
                rulebaseId = nextLink.NextRulebaseId;
            }

            return RuleTree.ElementsFlat.Where(item => ((RuleTreeItem) item).IsRule).Select(item => item.Data!).ToList();
        }



        #endregion

        #region Methods - Private

        private RulebaseReport? ProcessInlineLayerLink(RulebaseLink link)
        {
            RulebaseReport? rulebase = Rulebases.FirstOrDefault(rb => rb.Id == link.NextRulebaseId);

            if (rulebase != null)
            {
                RuleTreeItem inlineLayerItem = new RuleTreeItem();
                inlineLayerItem.Header = rulebase.Name ?? "";
                inlineLayerItem.IsInlineLayerRoot = true;
                SetParentForTreeItem(inlineLayerItem, link); 
                RuleTree.LastAddedItem = inlineLayerItem;
            }

            return rulebase;
        }

        private RulebaseReport? ProcessSectionLink(RulebaseLink link)
        {
            RulebaseReport? rulebase = Rulebases.FirstOrDefault(rb => rb.Id == link.NextRulebaseId);

            if (rulebase != null)
            {
                RuleTreeItem sectionLinkItem = new();
                sectionLinkItem.Header = rulebase.Name ?? "";
                sectionLinkItem.IsSectionHeader = true;
                SetParentForTreeItem(sectionLinkItem, link);
                RuleTree.LastAddedItem = sectionLinkItem;
            }

            return rulebase;
        }

        private RulebaseReport? ProcessOrderedLayerLink(RulebaseLink link)
        {
            RulebaseReport? rulebase = Rulebases.FirstOrDefault(rb => rb.Id == link.NextRulebaseId);

            if (rulebase != null)
            {
                RuleTreeItem orderedLayerItem = new();
                OrderedLayerCount++;
                orderedLayerItem.Position = [OrderedLayerCount];
                orderedLayerItem.Header = rulebase.Name ?? "";
                orderedLayerItem.IsOrderedLayerHeader = true;
                SetParentForTreeItem(orderedLayerItem, link);
                RuleTree.LastAddedItem = orderedLayerItem;
            }

            return rulebase;
        }

        private RulebaseReport? ProcessConcatenationLink(RulebaseLink link)
        {
            return ProcessSectionLink(link); // TODO: Differentiate between concatenation and section if needed
        }

        private void ProcessRulebase(RulebaseReport rulebase, RulebaseLink link)
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
                        int bottomLevelNumber = position.Last() + 1;
                        position[position.Count - 1] = bottomLevelNumber;
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
                        ProcessInlineLayerLink(inlineLayerLink);
                    }

                    // Reset position to  remain on correct level

                    position = ruleItem.Position.ToList();
                }

                RuleTree.LastAddedItem = lastAddedItem;                
            }
        }

        private FWO.Data.Rule GetUniqueRuleObject(FWO.Data.Rule rule)
        {
            List<FWO.Data.Rule> existingRules = RuleTree.ElementsFlat
                                                        .Where(item => ((RuleTreeItem) item).IsRule)
                                                        .Select(item => item.Data!)
                                                        .ToList();

            if (existingRules.Contains(rule))
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
                    if (lastAddedItem == null)
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





















        /// <summary>
        /// Creates multi-level (dotted) order numbers for display, sets internal numeric order for sorting and builds the rule tree.
        /// </summary>
        public List<FWO.Data.Rule> BuildRuleTree()
        {
            List<int> lastPosition = [];

            // Start outer loop.
            while (RuleTreeBuilderQueue.TryDequeue(out (RulebaseLink link, RulebaseReport rulebase) currentQueueItem))
            {
                lastPosition = HandleRulebaseLinkQueueItem(currentQueueItem, lastPosition);

                // For concatenations: Set the returned last order number to the number of the last visited rule of the source rulebase if it differs from it.
                if (RuleTreeBuilderQueue.TryPeek(out (RulebaseLink link, RulebaseReport rulebase) nextQueueItem) && nextQueueItem.link.LinkType == 4)
                {
                    FWO.Data.Rule? lastVisitedRuleOfNextRulebase = _allRules.LastOrDefault(rule => rule.RulebaseId == nextQueueItem.link.FromRulebaseId);

                    if (lastVisitedRuleOfNextRulebase != null && lastVisitedRuleOfNextRulebase.DisplayOrderNumberString != string.Join(".", lastPosition))
                    {
                        string input = lastVisitedRuleOfNextRulebase.DisplayOrderNumberString;
                        string result = input.Contains('|') ? input.Substring(0, input.IndexOf('|')) : input;
                        lastPosition = [.. result.Split('.').Select(int.Parse)];
                    }
                }
            }

            return _allRules;
        }


        /// <summary>
        /// Recursive method that processes a rulebase link and the target rulebase to create ordernumbers and the integrate the rules in the rule tree.
        /// </summary>
        private List<int> HandleRulebaseLinkQueueItem((RulebaseLink link, RulebaseReport rulebase) currentQueueItem, List<int> lastPosition)
        {
            // Get next link and rulebase if they exist.
            (RulebaseLink link, RulebaseReport rulebase)? nextQueueItem = TryPeekNextQueueItem();

            // Prepare creation of order numbers.

            (List<int> nextPosition, RuleTreeItem? nextParent, lastPosition) = PrepareOrderNumberCreation(currentQueueItem, lastPosition, nextQueueItem);

            // Create order number.
            List<FWO.Data.Rule> currentRules = [.. currentQueueItem.rulebase.Rules];
            foreach (FWO.Data.Rule currentRule in currentRules)
            {
                // Make clone if rule was already processed.
                FWO.Data.Rule rule = currentRule;

                if (_allRules.Contains(currentRule))
                {
                    rule = currentRule.CreateClone();
                }

                // Update next position.

                nextPosition = UpdateNextPosition(nextPosition, nextParent);

                // Update order number.

                nextPosition[nextPosition.Count - 1] = nextPosition.Last() + 1;

                // Get and update tree item that holds currentRule as data.

                RuleTreeItem treeItem = nextParent?.AddItem(addToChildren: true, addToFlatList: true, setLastAddedItem: true) ?? new RuleTreeItem();
                treeItem.Data = rule;
                RuleTree.ElementsFlat.Add(treeItem);
                treeItem.Position = [.. nextPosition];
                rule.DisplayOrderNumberString = string.Join(".", treeItem.Position);
                treeItem.IsRule = true;

                treeItem.Identifier = $"Rule (ID/UID): {rule.Id}/{rule.Uid}";

                RuleTree.LastAddedItem = treeItem;

                // Update order number, visited rules and last position.
                CreatedOrderNumbersCount++;
                rule.OrderNumber = CreatedOrderNumbersCount;
                _allRules.Add(rule);
                lastPosition = nextPosition;

                // Handle inline layers.
                
                if (nextQueueItem?.link is RulebaseLink nextLink && ((nextLink.LinkType == 3 && nextLink.FromRuleId == currentRule.Id) || nextLink.IsSection && RuleTree.LastAddedItem?.Data?.RulebaseId == nextLink.FromRulebaseId && currentRule == currentRules.Last()))
                {
                    nextQueueItem = RuleTreeBuilderQueue.Dequeue();

                    lastPosition = HandleRulebaseLinkQueueItem(nextQueueItem.Value, lastPosition);

                    // Update current and next queue items in case this loop continues after handling an inline layer.

                    nextQueueItem = TryPeekNextQueueItem();
                }
            }

            return lastPosition;
        }
        
        /// <summary>
        /// Creates the queue that is used to create the rule tree.
        /// </summary>
        public Queue<(RulebaseLink, RulebaseReport)>? BuildRulebaseLinkQueue(RulebaseLink[] links, RulebaseReport[] rulebases)
        {
            // Abort if their are no rulebase links or rulebases
            if (links.Length == 0 || rulebases.Length == 0)
            {
                return null;
            }

            Queue<(RulebaseLink, RulebaseReport)> queue = new();
            Dictionary<int, RulebaseReport> rulebaseMap = rulebases.ToDictionary(r => r.Id);

            // Make copy of link list, to be able to remove links without changing the original collection.
            List<RulebaseLink> remainingLinks = [.. links];

            // Start with initial link.
            RulebaseLink? current = remainingLinks.FirstOrDefault(l => l.IsInitial) ?? throw new InvalidOperationException("No initial RulebaseLink found.");
            while (current != null)
            {
                // Get target rulebase to current link and enqueue its rules and the link.
                if (!rulebaseMap.TryGetValue(current.NextRulebaseId, out RulebaseReport? report))
                {
                    throw new KeyNotFoundException($"No report found with ID {current.NextRulebaseId}");
                }

                RulebaseReport rulebase = new();
                
                rulebase.Id = report.Id;
                rulebase.Name = report.Name;
                rulebase.RuleChanges = report.RuleChanges;
                rulebase.RuleStatistics = report.RuleStatistics;
                rulebase.Rules = [.. report.Rules];

                queue.Enqueue((current, rulebase));
                remainingLinks.Remove(current);

                // Get next link.
                List<RulebaseLink>? candidates = [.. remainingLinks
                                                    .Where(l => l.FromRulebaseId == current.NextRulebaseId)
                                                    .OrderByDescending(l => l.FromRuleId.HasValue)];

                current = candidates.FirstOrDefault();
                current ??= remainingLinks.FirstOrDefault();
            }
            RuleTreeBuilderQueue = queue;
            return queue;
        }

        /// <summary>
        /// Returns the next queue item without removing it from the queue.
        /// </summary>
        private (RulebaseLink, RulebaseReport)? TryPeekNextQueueItem()
        {
            if (RuleTreeBuilderQueue.TryPeek(out (RulebaseLink link, RulebaseReport) peekedQueueItem))
            {
                return peekedQueueItem;
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Returns the item that should be used for the section rule tree item in creation.
        /// </summary>
        private RuleTreeItem GetSectionParent(IEnumerable<int> nextPosition)
        {
            List<int> changedCopy = nextPosition.ToList();
            bool isLastPositionZero = false;
            if (changedCopy.Last() == 0)
            {
                changedCopy.Remove(changedCopy.Last());
                isLastPositionZero = true;
            }
            RuleTreeItem item = RuleTree.ElementsFlat.FirstOrDefault(x => CompareTreeItemPosition(x, changedCopy)) as RuleTreeItem ?? new RuleTreeItem();
        
            RuleTreeItem parent = item.Parent as RuleTreeItem ?? new RuleTreeItem();
            if (item.Data is not null && parent.IsOrderedLayerHeader && !isLastPositionZero)
            {
                return parent;
            }
            if (item.IsOrderedLayerHeader)
            {
                return item;
            }
            if (parent.IsSectionHeader)
            {
                if (isLastPositionZero)
                {
                    return item;
                }
                return item.Parent?.Parent as RuleTreeItem ?? new RuleTreeItem();
            }
            if (item.IsInlineLayerRoot)
            {
                return item;
            }
            if (parent.IsInlineLayerRoot)
            {
                return item.Parent as RuleTreeItem ?? new RuleTreeItem();
            }
            return new RuleTreeItem();
        }

        private (List<int>, RuleTreeItem?, List<int>) PrepareOrderNumberCreation((RulebaseLink link, RulebaseReport rulebase) currentQueueItem, List<int> lastPosition,  (RulebaseLink link, RulebaseReport rulebase)? nextQueueItem)
        {
            List<int> nextPosition = [];
            RuleTreeItem? nextParent = null;
            if (currentQueueItem.link is { LinkType: 2, IsGlobal: false })
            {
                // Create position root and header item for new ordered layer

                OrderedLayerCount++;
                nextPosition = [OrderedLayerCount];
                nextParent = RuleTree.AddItem(header: currentQueueItem.rulebase.Name ?? "", position: nextPosition.ToList(), addToChildren: true, addToFlatList: true, setLastAddedItem: true);
                nextParent.IsOrderedLayerHeader = true;
                lastPosition = nextPosition;
            }
            else if (currentQueueItem.link.IsSection)
            {
                // Get the starting point for the next position.

                nextPosition = lastPosition;

                // Get parent for header item.

                nextParent = GetSectionParent(nextPosition);

                // Create header item for section.

                nextParent = nextParent.AddItem(header: currentQueueItem.rulebase.Name ?? "", position: nextPosition.ToList(), addToChildren: true, addToFlatList: true, setLastAddedItem: true);
                nextParent.IsSectionHeader = true;
            }
            else if (currentQueueItem.link.LinkType == 3)
            {
                nextParent = RuleTree.LastAddedItem as RuleTreeItem;
                if(nextParent !=null)
                    nextParent.IsInlineLayerRoot = true;
                nextPosition = lastPosition.ToList();
                nextPosition.Add(0);
                lastPosition = nextPosition;

                // Handle sections in inline layers without direct rules.

                if (nextQueueItem?.link is RulebaseLink nextLink && currentQueueItem.rulebase.Rules.Count() == 0 && nextLink.IsSection && nextLink.FromRulebaseId == currentQueueItem.link.NextRulebaseId)
                {
                    lastPosition = HandleRulebaseLinkQueueItem(RuleTreeBuilderQueue.Dequeue(), lastPosition);
                }
            }
            else if (currentQueueItem.link.LinkType == 4 && currentQueueItem.link.IsInitial)
            {
                nextParent = RuleTree;
            }
            
            return(nextPosition, nextParent, lastPosition);
        }

        private List<int> UpdateNextPosition(List<int> nextPosition, RuleTreeItem? nextParent)
        {
            
            if (nextParent!= null && nextParent.GetPositionString() == OrderedLayerCount.ToString() && nextParent.Children.Count == 0 || nextPosition.Count == 0) 
            {
                nextPosition.Add(0);
            }

            return nextPosition;
        }

      
        
        #region NormalizePosition
        private static string NormalizePosition(IEnumerable<string> parts)
        {
            var partsArray = parts.ToArray();
            int lastNonZeroIndex = partsArray.Length - 1;
            
            while (lastNonZeroIndex > 0 && partsArray[lastNonZeroIndex] == "0")
                lastNonZeroIndex--;
            return string.Join(".", partsArray.Take(lastNonZeroIndex + 1));
        }
        private static string NormalizePosition(IEnumerable<int> parts)
        {
            var partsArray = parts.ToArray();
            int lastNonZeroIndex = partsArray.Length - 1;
            
            while (lastNonZeroIndex > 0 && partsArray[lastNonZeroIndex] == 0)
                lastNonZeroIndex--;
            return string.Join(".", partsArray.Take(lastNonZeroIndex + 1));
        }
        private static string NormalizePosition(string position)
        {
            return NormalizePosition(position.Split('.'));
        }
        #endregion

        protected static bool CompareTreeItemPosition(ITreeItem<FWO.Data.Rule> treeItem, List<int> list)
        {
            string treeItemString = NormalizePosition(treeItem.GetPositionString());
            string listString = NormalizePosition(list);
            bool comparisonResult = treeItemString == listString;
            if (!comparisonResult)
            {
                Logging.Log.WriteDebug("Comparing Position", $"Tree Item Position: {treeItemString} | Listing Position: {listString}");
            }
            return comparisonResult;
        }


    }
}
