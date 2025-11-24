using FWO.Basics;
using FWO.Data;
using FWO.Data.Report;

namespace FWO.Services.RuleTreeBuilder
{
    public class RuleTreeBuilder : IRuleTreeBuilder
    {
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
        public int CreatedOrderNumbersCount { get; set; } = 0;
        
        /// <summary>
        /// A counter to easily create the order position (/order number) for the ordered layers on the top level.
        /// </summary>
        public int OrderedLayerCount { get; set; } = 0;
        
        /// <summary>
        /// All of the processed rules.
        /// </summary>
        private readonly List<Rule> _allRules = [];

        public RuleTreeBuilder()
        {
            RuleTree.IsRoot = true;
        }

        /// <summary>
        /// Creates multi-level (dotted) order numbers for display, sets internal numeric order for sorting and builds the rule tree.
        /// </summary>
        public List<Rule> BuildRuleTree()
        {
            List<int>? lastPosition = [];

            // Start outer loop.
            while (RuleTreeBuilderQueue.TryDequeue(out (RulebaseLink link, RulebaseReport rulebase) currentQueueItem))
            {
                lastPosition = HandleRulebaseLinkQueueItem(currentQueueItem, lastPosition);

                // For concatenations: Set the returned last order number to the number of the last visited rule of the source rulebase if it differs from it.
                if (RuleTreeBuilderQueue.TryPeek(out (RulebaseLink link, RulebaseReport rulebase) nextQueueItem) && nextQueueItem.link.LinkType == 4)
                {
                    Rule? lastVisitedRuleOfNextRulebase = _allRules.LastOrDefault(rule => rule.RulebaseId == nextQueueItem.link.FromRulebaseId);

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
        private List<int>? HandleRulebaseLinkQueueItem((RulebaseLink link, RulebaseReport rulebase) currentQueueItem, List<int>? lastPosition)
        {
            // Get next link and rulebase if they exist.
            (RulebaseLink link, RulebaseReport rulebase)? nextQueueItem = TryPeekNextQueueItem();

            // Prepare creation of order numbers.

            (List<int>? nextPosition, RuleTreeItem? nextParent, lastPosition) = PrepareOrderNumberCreation(currentQueueItem, lastPosition, nextQueueItem);

            // Create order number.
            List<Rule> currentRules = [.. currentQueueItem.rulebase.Rules];
            foreach (Rule currentRule in currentRules)
            {
                // Make clone if rule was already processed.
                Rule rule = currentRule;

                if (_allRules.Contains(currentRule))
                {
                    rule = currentRule.CreateClone();
                }

                // Update next position.

                nextPosition = UpdateNextPosition(nextPosition, nextParent, lastPosition);

                // Update order number.

                nextPosition[nextPosition.Count() - 1] = nextPosition.Last() + 1;

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

                    currentQueueItem = nextQueueItem.Value;

                    nextQueueItem = TryPeekNextQueueItem();
                }
            }

            return lastPosition ?? [];
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

                if (report != null)
                {
                    rulebase.Id = report.Id;
                    rulebase.Name = report.Name;
                    rulebase.RuleChanges = report.RuleChanges;
                    rulebase.RuleStatistics = report.RuleStatistics;
                    rulebase.Rules = [.. report.Rules];
                }

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
            List<int> unchangedCopy = changedCopy.ToList();
            if (changedCopy.Last() == 0)
            {
                changedCopy.Remove(changedCopy.Last());
            }
            RuleTreeItem item = RuleTree.ElementsFlat.FirstOrDefault(x => CompareTreeItemPosition(x,changedCopy)) as RuleTreeItem ?? new RuleTreeItem();

            if (item.Data is Rule && (item.Parent as RuleTreeItem ?? new RuleTreeItem()).IsOrderedLayerHeader && unchangedCopy.Last() != 0)
            {
                return item.Parent as RuleTreeItem ?? new RuleTreeItem();
            }
            if (item.IsOrderedLayerHeader || item.IsInlineLayerRoot)
            {
                return item;
            }
            if ((item.Parent as RuleTreeItem ?? new RuleTreeItem()).IsSectionHeader)
            {
                if (unchangedCopy.Last() == 0)
                {
                    return item;
                }
                return item.Parent?.Parent as RuleTreeItem ?? new RuleTreeItem();
            }
            return new RuleTreeItem();
        }

        private (List<int>?, RuleTreeItem?, List<int>?) PrepareOrderNumberCreation((RulebaseLink link, RulebaseReport rulebase) currentQueueItem, List<int>? lastPosition,  (RulebaseLink link, RulebaseReport rulebase)? nextQueueItem)
        {
            List<int>? nextPosition = null;
            RuleTreeItem? nextParent = null;
            if (currentQueueItem.link.LinkType == 2 && !currentQueueItem.link.IsGlobal)
            {
                // Create position root and header item for new ordered layer

                OrderedLayerCount++;
                nextPosition = new List<int> { OrderedLayerCount };
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

                nextParent = nextParent.AddItem(header: currentQueueItem.rulebase.Name ?? "", position: nextPosition?.ToList(), addToChildren: true, addToFlatList: true, setLastAddedItem: true);
                nextParent.IsSectionHeader = true;
            }
            else if (currentQueueItem.link.LinkType == 3)
            {
                nextParent = RuleTree.LastAddedItem as RuleTreeItem;
                if(nextParent !=null)
                    nextParent.IsInlineLayerRoot = true;
                nextPosition = lastPosition?.ToList()?? [];
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

        private List<int> UpdateNextPosition(List<int>? nextPosition, RuleTreeItem? nextParent, List<int>? lastPosition)
        {
            if (nextPosition == null)
            {
                nextPosition = lastPosition?.ToList() ?? [];
            }
            else if (nextParent?.GetPositionString() == OrderedLayerCount.ToString() && nextParent?.Children.Count == 0)
            {
                nextPosition.Add(0);
            }

            if (nextPosition.Count == 0)
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

        protected static bool CompareTreeItemPosition(ITreeItem<Rule> treeItem, List<int> list)
        {
            return NormalizePosition(treeItem.GetPositionString()) == NormalizePosition(list);
        }
    }
}
