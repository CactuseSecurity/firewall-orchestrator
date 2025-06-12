using FWO.Basics;
using FWO.Data;
using FWO.Data.Report;

namespace FWO.Services
{
    public class RuleTreeService : IRuleTreeService
    {
        public TreeItem<Rule> RuleTree { get; set; } = new();
        public Queue<(RulebaseLink link, RulebaseReport rulebase)> RuleTreeBuilderQueue { get; set; } = new();
        public int CreatedOrderNumbersCount { get; set; } = 0;
        public int OrderedLayerCount { get; set; } = 0;
        private List<Rule> _allRules = new();

        public RuleTreeService()
        {
            RuleTree.IsRoot = true;
        }

        /// <summary>
        /// Creates multi-level (dotted) order numbers for display, sets internal numeric order for sorting and builds the rule tree.
        /// </summary>
        public List<Rule> BuildRuleTree()
        {
            List<int> lastPosition = new();

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
                        lastPosition = lastVisitedRuleOfNextRulebase.DisplayOrderNumberString
                                                                    .Split('.')
                                                                    .Select(int.Parse)
                                                                    .ToList();
                    }
                }
            }

            return _allRules;
        }

        private List<int> HandleRulebaseLinkQueueItem((RulebaseLink link, RulebaseReport rulebase) currentQueueItem, List<int> lastPosition)
        {
            List<int>? nextPosition = null;
            TreeItem<Rule>? nextParent = null;

            // Get next link and rulebase if they exist.

            (RulebaseLink link, RulebaseReport rulebase)? nextQueueItem = TryPeekNextQueueItem();

            // Prepare creation of order numbers.

            if (currentQueueItem.link.LinkType == 2 && !currentQueueItem.link.IsGlobal)
            {
                // Create position root and header item for new ordered layer

                OrderedLayerCount++;
                nextPosition = new List<int> { OrderedLayerCount, 0 };
                nextParent = RuleTree.AddItem(header: currentQueueItem.rulebase.Name ?? "", position: nextPosition.ToList(), addToChildren: true, addToFlatList: true, setLastAddedItem: true);
                lastPosition = nextPosition;
            }
            else if (currentQueueItem.link.IsSection)
            {
                // Get the starting point for the next position.

                nextPosition = lastPosition;

                // Get parent for header item.

                if (RuleTree.LastAddedItem != null)
                {
                    nextParent = RuleTree.LastAddedItem;
                }
                else
                {
                    nextParent = RuleTree;
                }

                // Create header item for section.

                nextParent = nextParent.AddItem(header: currentQueueItem.rulebase.Name ?? "", position: nextPosition.ToList(), addToChildren: true, addToFlatList: true, setLastAddedItem: true);

            }
            else if (currentQueueItem.link.LinkType == 3)
            {
                nextParent = RuleTree.LastAddedItem;
                nextPosition = lastPosition.ToList();
                nextPosition.Add(0);
                lastPosition = nextPosition;

                // Handle sections in inline layers without direct rules.

                if (nextQueueItem?.link is RulebaseLink nextLink && currentQueueItem.rulebase.Rules.Count() == 0 && nextLink.IsSection && nextLink.FromRulebaseId == currentQueueItem.link.NextRulebaseId)
                {
                    lastPosition = HandleRulebaseLinkQueueItem(RuleTreeBuilderQueue.Dequeue(), lastPosition);
                }
            }

            // Create order number.
            List<Rule> currentRules = currentQueueItem.rulebase.Rules.ToList();
            foreach (Rule currentRule in currentRules)
            {
                // Make clone if rule was alreade processed.

                Rule rule = currentRule;

                if (_allRules.Contains(currentRule))
                {
                    rule = currentRule.CreateClone();
                }

                // Update next position.

                if (nextPosition == null)
                {
                    nextPosition = lastPosition.ToList();
                }

                // Update order number.

                nextPosition[nextPosition.Count() - 1] = nextPosition.Last() + 1;
                rule.DisplayOrderNumberString = string.Join(".", nextPosition);

                // Get and update tree item that holds currentRule as data.

                TreeItem<Rule> treeItem  = nextParent.AddItem(data: rule, addToChildren: true, addToFlatList: true, setLastAddedItem: true);

                treeItem.Position = nextPosition.ToList();
                treeItem.Identifier = $"Rule (ID/UID): {rule.Id}/{rule.Uid}";

                // Add item to tree.

                treeItem.Parent.Children.Add(treeItem);

                RuleTree.LastAddedItem = treeItem;

                // Update order number, visited rules and last position.

                CreatedOrderNumbersCount++;
                rule.OrderNumber = CreatedOrderNumbersCount;
                _allRules.Add(rule);
                lastPosition = nextPosition;

                // Handle inline layers.

                if (nextQueueItem?.link is RulebaseLink nextLink)
                {
                    if ((nextLink.LinkType == 3 && nextLink.FromRuleId == currentRule.Id)
                        || (nextLink.LinkType == 4 && nextLink.FromRulebaseId == currentRule.RulebaseId && currentRule == currentRules.LastOrDefault()))
                    {

                        if (nextLink.IsSection)
                        {
                            if (currentQueueItem.link.IsSection)
                            {
                                RuleTree.LastAddedItem = nextParent?.Parent;
                            }
                            else
                            {
                                RuleTree.LastAddedItem = RuleTree.LastAddedItem.Parent;
                            }
                        }

                        nextQueueItem = RuleTreeBuilderQueue.Dequeue();

                        lastPosition = HandleRulebaseLinkQueueItem(nextQueueItem.Value, lastPosition);

                        // Update current and next queue items in case this loop continues after handling an inline layer.

                        currentQueueItem = nextQueueItem.Value;

                        nextQueueItem = TryPeekNextQueueItem();
                    }

                }
            }

            return lastPosition;
        }

        public Queue<(RulebaseLink, RulebaseReport)>? BuildRulebaseLinkQueue(RulebaseLink[] links, RulebaseReport[] rulebases)
        {
            // Abort if their are no rulebase links or rulebases

            if (links.Count() == 0 || rulebases.Count() == 0)
            {
                return null;
            }

            Queue<(RulebaseLink, RulebaseReport)> queue = new();

            Dictionary<int, RulebaseReport> rulebaseMap = rulebases.ToDictionary(r => r.Id);

            // Make copy of link list, to be able to remove links without changing the original collection.

            List<RulebaseLink> remainingLinks = links.ToList();

            // Start with initial link.

            RulebaseLink? current = remainingLinks.FirstOrDefault(l => l.IsInitial);

            if (current == null)
            {
                throw new InvalidOperationException("No initial RulebaseLink found.");
            }


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
                    rulebase.Rules = report.Rules.ToArray();

                }


                queue.Enqueue((current, rulebase));
                remainingLinks.Remove(current);

                // Get next link.

                List<RulebaseLink>? candidates = remainingLinks
                                                    .Where(l => l.FromRulebaseId == current.NextRulebaseId)
                                                    .OrderByDescending(l => l.FromRuleId.HasValue)
                                                    .ToList();

                current = candidates.FirstOrDefault();

                if (current == null)
                {
                    current = remainingLinks.FirstOrDefault();
                }
            }

            RuleTreeBuilderQueue = queue;

            return queue;
        }

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

    }
}