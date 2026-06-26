using System.Collections.Generic;
using System.Linq;
using FWO.Basics;
using FWO.Data;
using FWO.Data.Report;
using FWO.Services.RuleTreeBuilder;
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
        public void BuildRuleTree_OrderedLayerOnly_ReturnsOrderedHeaderAndRules()
        {
            RulebaseReport[] rulebases =
            [
                Rulebase(1, "Layer-1", 10, 11)
            ];

            RulebaseLink[] links =
            [
                OrderedLayerInitialLink(gatewayId: 1, nextRulebaseId: 1)
            ];

            List<Rule> flattenedRules = _ruleTreeBuilder.BuildRuleTree(rulebases, links, 1, 1);

            Assert.That(flattenedRules.Count, Is.EqualTo(3));
            Assert.That(flattenedRules[0].SectionHeader, Is.EqualTo("Layer-1"));
            Assert.That(flattenedRules[1].DisplayOrderNumberString, Is.EqualTo("1.1"));
            Assert.That(flattenedRules[2].DisplayOrderNumberString, Is.EqualTo("1.2"));
            Assert.That(_ruleTreeBuilder.RuleTree.ElementsFlat.Count(element => element.IsOrderedLayerHeader), Is.EqualTo(1));
            Assert.That(_ruleTreeBuilder.RuleTree.ElementsFlat.Count(element => element.IsRule), Is.EqualTo(2));
        }

        [Test]
        public void BuildRuleTree_ShuffledSectionAndLayerLinks_FollowsGraphNotInputOrder()
        {
            RulebaseReport[] rulebases =
            [
                Rulebase(1, "Layer-A", 10),
                Rulebase(2, "Section-A", 20),
                Rulebase(3, "Layer-B", 30)
            ];

            RulebaseLink[] links =
            [
                OrderedLayerLink(gatewayId: 1, fromRulebaseId: 1, nextRulebaseId: 3),
                SectionLink(gatewayId: 1, fromRulebaseId: 1, nextRulebaseId: 2),
                OrderedLayerInitialLink(gatewayId: 1, nextRulebaseId: 1)
            ];

            List<Rule> flattenedRules = _ruleTreeBuilder.BuildRuleTree(rulebases, links, 1, 1);
            Rule[] realRules = flattenedRules.Where(rule => string.IsNullOrEmpty(rule.SectionHeader)).ToArray();

            Assert.That(realRules.Select(rule => rule.RulebaseId), Is.EqualTo(new[] { 1, 2, 3 }));
            Assert.That(flattenedRules.Select(rule => rule.DisplayOrderNumberString), Is.EqualTo(new[]
            {
                "1", "1.1", string.Empty, "1.2", "2", "2.1"
            }));
        }

        [Test]
        public void BuildRuleTree_InlineLayerWithSection_BuildsNestedGraph()
        {
            RulebaseReport[] rulebases =
            [
                Rulebase(1, "Layer-1", 10),
                Rulebase(2, "Inline-1", 20),
                Rulebase(3, "Inline-Section", 30)
            ];

            RulebaseLink[] links =
            [
                SectionLink(gatewayId: 1, fromRulebaseId: 2, nextRulebaseId: 3),
                OrderedLayerInitialLink(gatewayId: 1, nextRulebaseId: 1),
                InlineLayerLink(gatewayId: 1, fromRulebaseId: 1, fromRuleId: 10, nextRulebaseId: 2)
            ];

            List<Rule> flattenedRules = _ruleTreeBuilder.BuildRuleTree(rulebases, links, 1, 1);
            Rule[] realRules = flattenedRules.Where(rule => string.IsNullOrEmpty(rule.SectionHeader)).ToArray();

            Assert.That(realRules.Select(rule => rule.RulebaseId), Is.EqualTo(new[] { 1, 2, 3 }));
            Assert.That(realRules.Select(rule => rule.DisplayOrderNumberString), Is.EqualTo(new[]
            {
                "1.1", "1.1.1", "1.1.2"
            }));
            Assert.That(flattenedRules.Single(rule => rule.SectionHeader == "Inline-Section").DisplayOrderNumberString, Is.EqualTo(string.Empty));
            Assert.That(_ruleTreeBuilder.RuleTree.ElementsFlat.Count(element => element.IsSectionHeader), Is.EqualTo(1));
        }

        [Test]
        public void BuildRuleTree_InlineLayerRulesBecomeVisibleAgainAfterCollapseAndExpand()
        {
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

            _ruleTreeBuilder.BuildRuleTree(rulebases, links, 1, 1);

            RuleTreeItem root = _ruleTreeBuilder.RuleTree;
            RuleTreeItem layerNode = root.Children.Single();
            RuleTreeItem owningRuleNode = layerNode.Children.Single(child => child.IsRule);
            RuleTreeItem inlineSectionHeaderNode = root.ElementsFlat.Single(node => node.IsSectionHeader && node.Data.SectionHeader == "Inline-Section");
            RuleTreeItem inlineRuleNode = root.ElementsFlat.Single(node => node.IsRule && node.Data.RulebaseId == 2);
            RuleTreeItem inlineSectionRuleNode = root.ElementsFlat.Single(node => node.IsRule && node.Data.RulebaseId == 3);

            RuleTreeItem.SetExpandedRecursively(root, false);
            layerNode.IsExpanded = true;
            owningRuleNode.IsExpanded = true;

            Assert.That(inlineRuleNode.IsVisible, Is.True);
            Assert.That(inlineSectionHeaderNode.IsVisible, Is.True);
            Assert.That(inlineSectionRuleNode.IsVisible, Is.False);

            inlineSectionHeaderNode.IsExpanded = true;

            Assert.That(inlineSectionRuleNode.IsVisible, Is.True);
        }

        [Test]
        public void BuildRuleTree_TwoOrderedLayers_PreservesTopLevelLayerChain()
        {
            RulebaseReport[] rulebases =
            [
                Rulebase(1, "Layer-1", 10),
                Rulebase(2, "Layer-2", 20)
            ];

            RulebaseLink[] links =
            [
                OrderedLayerLink(gatewayId: 1, fromRulebaseId: 1, nextRulebaseId: 2),
                OrderedLayerInitialLink(gatewayId: 1, nextRulebaseId: 1)
            ];

            List<Rule> flattenedRules = _ruleTreeBuilder.BuildRuleTree(rulebases, links, 1, 1);

            Assert.That(flattenedRules.Select(rule => rule.DisplayOrderNumberString), Is.EqualTo(new[]
            {
                "1", "1.1", "2", "2.1"
            }));
        }

        [Test]
        public void BuildRuleTree_DuplicateRuleIds_Throws()
        {
            RulebaseReport[] rulebases =
            [
                Rulebase(1, "Layer-1", 10),
                Rulebase(2, "Layer-2", 10)
            ];

            RulebaseLink[] links =
            [
                OrderedLayerInitialLink(gatewayId: 1, nextRulebaseId: 1),
                OrderedLayerLink(gatewayId: 1, fromRulebaseId: 1, nextRulebaseId: 2)
            ];

            Assert.That(
                () => _ruleTreeBuilder.BuildRuleTree(rulebases, links, 1, 1),
                Throws.InvalidOperationException.With.Message.Contains("encountered more than once"));
        }

        [Test]
        public void BuildRuleTree_MissingInitialLink_Throws()
        {
            RulebaseReport[] rulebases =
            [
                Rulebase(1, "Layer-1", 10)
            ];

            RulebaseLink[] links =
            [
                OrderedLayerLink(gatewayId: 1, fromRulebaseId: 99, nextRulebaseId: 1)
            ];

            Assert.That(
                () => _ruleTreeBuilder.BuildRuleTree(rulebases, links, 1, 1),
                Throws.InvalidOperationException.With.Message.Contains("none were found"));
        }

        [Test]
        public void BuildRuleTree_MultipleInitialLinks_Throws()
        {
            RulebaseReport[] rulebases =
            [
                Rulebase(1, "Layer-1", 10),
                Rulebase(2, "Layer-2", 20)
            ];

            RulebaseLink[] links =
            [
                OrderedLayerInitialLink(gatewayId: 1, nextRulebaseId: 1),
                OrderedLayerInitialLink(gatewayId: 1, nextRulebaseId: 2)
            ];

            Assert.That(
                () => _ruleTreeBuilder.BuildRuleTree(rulebases, links, 1, 1),
                Throws.InvalidOperationException.With.Message.Contains("multiple were found"));
        }

        [Test]
        public void BuildRuleTree_MissingOrderedLayerRulebase_Throws()
        {
            RulebaseLink[] links =
            [
                OrderedLayerInitialLink(gatewayId: 1, nextRulebaseId: 999)
            ];

            Assert.That(
                () => _ruleTreeBuilder.BuildRuleTree([], links, 1, 1),
                Throws.InvalidOperationException.With.Message.Contains("Rulebase 999"));
        }

        [Test]
        public void BuildRuleTree_MissingSectionRulebase_Throws()
        {
            RulebaseReport[] rulebases =
            [
                Rulebase(1, "Layer-1", 10)
            ];

            RulebaseLink[] links =
            [
                OrderedLayerInitialLink(gatewayId: 1, nextRulebaseId: 1),
                SectionLink(gatewayId: 1, fromRulebaseId: 1, nextRulebaseId: 999)
            ];

            Assert.That(
                () => _ruleTreeBuilder.BuildRuleTree(rulebases, links, 1, 1),
                Throws.InvalidOperationException.With.Message.Contains("Rulebase 999"));
        }

        [Test]
        public void BuildRuleTree_MissingInlineRulebase_Throws()
        {
            RulebaseReport[] rulebases =
            [
                Rulebase(1, "Layer-1", 10)
            ];

            RulebaseLink[] links =
            [
                OrderedLayerInitialLink(gatewayId: 1, nextRulebaseId: 1),
                InlineLayerLink(gatewayId: 1, fromRulebaseId: 1, fromRuleId: 10, nextRulebaseId: 999)
            ];

            Assert.That(
                () => _ruleTreeBuilder.BuildRuleTree(rulebases, links, 1, 1),
                Throws.InvalidOperationException.With.Message.Contains("Rulebase 999"));
        }

        [Test]
        public void BuildRuleTree_UnresolvedLinks_DoNotFailBuild()
        {
            RulebaseReport[] rulebases =
            [
                Rulebase(1, "Layer-1", 10),
                Rulebase(2, "Unused-Concat", 20)
            ];

            RulebaseLink[] links =
            [
                OrderedLayerInitialLink(gatewayId: 1, nextRulebaseId: 1),
                ConcatenationLink(gatewayId: 1, fromRulebaseId: 1, nextRulebaseId: 2)
            ];

            List<Rule> flattenedRules = _ruleTreeBuilder.BuildRuleTree(rulebases, links, 1, 1);

            Assert.That(flattenedRules.Count, Is.EqualTo(2));
            Assert.That(_ruleTreeBuilder.LinksToBeProcessed.Count, Is.EqualTo(1));
        }

        [Test]
        public void BuildRuleTree_FinalFlatteningAssignsSequentialOrderNumbers()
        {
            RulebaseReport[] rulebases =
            [
                Rulebase(1, "Layer-1", 10),
                Rulebase(2, "Section-A", 20)
            ];

            RulebaseLink[] links =
            [
                OrderedLayerInitialLink(gatewayId: 1, nextRulebaseId: 1),
                SectionLink(gatewayId: 1, fromRulebaseId: 1, nextRulebaseId: 2)
            ];

            List<Rule> flattenedRules = _ruleTreeBuilder.BuildRuleTree(rulebases, links, 1, 1);

            Assert.That(flattenedRules.Select(rule => rule.OrderNumber), Is.EqualTo(new double[] { 1, 2, 3, 4 }));
            Assert.That(flattenedRules.Select(rule => rule.DisplayOrderNumber), Is.EqualTo(new[] { 1, 2, 3, 4 }));
        }

        [Test]
        public void BuildRuleTree_SectionHeadersStayUnnumberedWhileRulesContinueSiblingNumbering()
        {
            RulebaseReport[] rulebases =
            [
                Rulebase(1, "Layer-1", 10, 11),
                Rulebase(2, "Section-A", 20),
                Rulebase(3, "Section-B", 30)
            ];

            RulebaseLink[] links =
            [
                OrderedLayerInitialLink(gatewayId: 1, nextRulebaseId: 1),
                SectionLink(gatewayId: 1, fromRulebaseId: 1, nextRulebaseId: 2),
                SectionLink(gatewayId: 1, fromRulebaseId: 2, nextRulebaseId: 3)
            ];

            List<Rule> flattenedRules = _ruleTreeBuilder.BuildRuleTree(rulebases, links, 1, 1);
            Rule[] sectionHeaders = flattenedRules.Where(rule => !string.IsNullOrEmpty(rule.SectionHeader)).ToArray();
            Rule[] realRules = flattenedRules.Where(rule => string.IsNullOrEmpty(rule.SectionHeader)).ToArray();

            Assert.That(sectionHeaders.Select(rule => rule.DisplayOrderNumberString), Is.EqualTo(new[] { "1", string.Empty, string.Empty }));
            Assert.That(realRules.Select(rule => rule.DisplayOrderNumberString), Is.EqualTo(new[] { "1.1", "1.2", "1.3", "1.4" }));
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

        private static RulebaseLink OrderedLayerLink(int gatewayId, int fromRulebaseId, int nextRulebaseId)
        {
            return new RulebaseLink
            {
                GatewayId = gatewayId,
                FromRulebaseId = fromRulebaseId,
                FromRuleId = null,
                NextRulebaseId = nextRulebaseId,
                LinkType = 2,
                IsInitial = false,
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
    }
}
