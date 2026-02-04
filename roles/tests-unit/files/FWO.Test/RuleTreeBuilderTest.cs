using System.Collections.Generic;
using System.Linq;
using FWO.Services.RuleTreeBuilder;
using FWO.Data;
using FWO.Data.Report;
using FWO.Basics;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class RuleTreeBuilderTest
    {
        private RuleTreeBuilder _ruleTreeBuilder = default!;

        [SetUp]
        public void SetUpTestMethod()
        {
            _ruleTreeBuilder = new RuleTreeBuilder();
        }

        [Test]
        public void BuildRuleTree_OrderedLayerOnly_ReturnsAllRules()
        {
            // Arrange
            RulebaseReport[] rulebases =
            [
                Rulebase(1, "Layer-1", 10, 11)
            ];
            RulebaseLink[] links =
            [
                OrderedLayerInitialLink(gatewayId: 1, nextRulebaseId: 1)
            ];

            // Act
            List<Rule> resultRules = _ruleTreeBuilder.BuildRuleTree(rulebases, links);

            // Assert
            Assert.That(resultRules.Count == 2);
            Assert.That(_ruleTreeBuilder.RuleTree.ElementsFlat.Count(element => ((RuleTreeItem)element).IsRule) == 2);
            Assert.That(_ruleTreeBuilder.RuleTree.ElementsFlat.Count(element => ((RuleTreeItem)element).IsSectionHeader) == 0);
            Assert.That(FindOrderedLayerRoots(_ruleTreeBuilder.RuleTree).Count == 1);
        }

        [Test]
        public void BuildRuleTree_SectionAndConcatenationLinks_CreateSectionHeaders()
        {
            // Arrange
            RulebaseReport[] rulebases =
            [
                Rulebase(1, "Layer-1", 10),
                Rulebase(2, "Section-A", 20),
                Rulebase(3, "Concat-B", 30)
            ];
            RulebaseLink[] links =
            [
                OrderedLayerInitialLink(gatewayId: 1, nextRulebaseId: 1),
                SectionLink(gatewayId: 1, fromRulebaseId: 1, nextRulebaseId: 2),
                ConcatenationLink(gatewayId: 1, fromRulebaseId: 2, nextRulebaseId: 3)
            ];

            // Act
            List<Rule> resultRules = _ruleTreeBuilder.BuildRuleTree(rulebases, links);

            // Assert
            Assert.That(resultRules.Count == 5);
            Assert.That(_ruleTreeBuilder.RuleTree.ElementsFlat.Count(element => ((RuleTreeItem)element).IsRule) == 3);
            Assert.That(_ruleTreeBuilder.RuleTree.ElementsFlat.Count(element => ((RuleTreeItem)element).IsSectionHeader) == 2);
            Assert.That(FindOrderedLayerRoots(_ruleTreeBuilder.RuleTree).Count == 1);
        }

        [Test]
        public void BuildRuleTree_InlineLayerFromRule_AddsInlineLayerAndRules()
        {
            // Arrange
            RulebaseReport[] rulebases =
            [
                Rulebase(1, "Layer-1", 10, 11),
                Rulebase(2, "Inline-1", 20, 21)
            ];
            RulebaseLink[] links =
            [
                OrderedLayerInitialLink(gatewayId: 1, nextRulebaseId: 1),
                InlineLayerLink(gatewayId: 1, fromRulebaseId: 1, fromRuleId: 10, nextRulebaseId: 2)
            ];

            // Act
            List<Rule> resultRules = _ruleTreeBuilder.BuildRuleTree(rulebases, links);

            // Assert
            Assert.That(resultRules.Count == 4);
            Assert.That(_ruleTreeBuilder.RuleTree.ElementsFlat.Count(element => ((RuleTreeItem)element).IsRule) == 4);
        }

        [Test]
        public void BuildRuleTree_InlineLayerWithSection_AddsSectionHeader()
        {
            // Arrange
            RulebaseReport[] rulebases =
            [
                Rulebase(1, "Layer-1", 10),
                Rulebase(2, "Inline-1", 20),
                Rulebase(3, "Inline-Section", 30)
            ];
            RulebaseLink[] links =
            [
                OrderedLayerInitialLink(gatewayId: 1, nextRulebaseId: 1),
                InlineLayerLink(gatewayId: 1, fromRulebaseId: 1, fromRuleId: 10, nextRulebaseId: 2),
                SectionLink(gatewayId: 1, fromRulebaseId: 2, nextRulebaseId: 3)
            ];

            // Act
            List<Rule> resultRules = _ruleTreeBuilder.BuildRuleTree(rulebases, links);

            // Assert
            Assert.That(resultRules.Count == 4);
            Assert.That(_ruleTreeBuilder.RuleTree.ElementsFlat.Count(element => ((RuleTreeItem)element).IsRule) == 3);
            Assert.That(_ruleTreeBuilder.RuleTree.ElementsFlat.Count(element => ((RuleTreeItem)element).IsSectionHeader) == 1);
        }

        private static RulebaseReport Rulebase(int id, string name, params int[] ruleIds)
        {
            return new RulebaseReport
            {
                Id = id,
                Name = name,
                Rules = ruleIds.Select(ruleId => new Rule { Id = ruleId, RulebaseId = id, Name = $"R-{ruleId}" }).ToArray()
            };
        }

        private static RulebaseLink OrderedLayerInitialLink(int gatewayId, int nextRulebaseId)
        {
            return new RulebaseLink
            {
                GatewayId = gatewayId,
                FromRulebaseId = null,
                FromRuleId = null,
                NextRulebaseId = nextRulebaseId,
                LinkType = 2,
                IsInitial = true,
                IsGlobal = false,
                IsSection = false
            };
        }

        private static RulebaseLink SectionLink(int gatewayId, int fromRulebaseId, int nextRulebaseId)
        {
            return new RulebaseLink
            {
                GatewayId = gatewayId,
                FromRulebaseId = fromRulebaseId,
                FromRuleId = null,
                NextRulebaseId = nextRulebaseId,
                LinkType = 4,
                IsInitial = false,
                IsGlobal = false,
                IsSection = true
            };
        }

        private static RulebaseLink ConcatenationLink(int gatewayId, int fromRulebaseId, int nextRulebaseId)
        {
            return new RulebaseLink
            {
                GatewayId = gatewayId,
                FromRulebaseId = fromRulebaseId,
                FromRuleId = null,
                NextRulebaseId = nextRulebaseId,
                LinkType = 4,
                IsInitial = false,
                IsGlobal = false,
                IsSection = false
            };
        }

        private static RulebaseLink InlineLayerLink(int gatewayId, int fromRulebaseId, int fromRuleId, int nextRulebaseId)
        {
            return new RulebaseLink
            {
                GatewayId = gatewayId,
                FromRulebaseId = fromRulebaseId,
                FromRuleId = fromRuleId,
                NextRulebaseId = nextRulebaseId,
                LinkType = 3,
                IsInitial = false,
                IsGlobal = false,
                IsSection = false
            };
        }

        private static List<RuleTreeItem> FindOrderedLayerRoots(RuleTreeItem root)
        {
            List<RuleTreeItem> results = new();
            Queue<RuleTreeItem> queue = new();
            queue.Enqueue(root);

            while (queue.Count > 0)
            {
                RuleTreeItem current = queue.Dequeue();
                if (current.IsOrderedLayerHeader)
                {
                    results.Add(current);
                }

                foreach (ITreeItem<Rule> child in current.Children)
                {
                    queue.Enqueue((RuleTreeItem)child);
                }
            }

            return results;
        }
    }
}
