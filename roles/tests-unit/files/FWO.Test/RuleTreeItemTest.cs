using FWO.Data;
using FWO.Services.RuleTreeBuilder;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class RuleTreeItemTest
    {
        [Test]
        public void SetExpandedRecursively_False_CollapsesAllExpandableDescendants()
        {
            RuleTreeItem root = BuildNestedTree(out RuleTreeItem parentRule, out RuleTreeItem childRule, out RuleTreeItem leafRule);

            RuleTreeItem.SetExpandedRecursively(root, false);

            Assert.That(parentRule.IsExpanded, Is.False);
            Assert.That(childRule.IsExpanded, Is.False);
            Assert.That(parentRule.IsVisible, Is.True);
            Assert.That(childRule.IsVisible, Is.False);
            Assert.That(leafRule.IsVisible, Is.False);
            Assert.That(leafRule.IsExpanded, Is.False);
        }

        [Test]
        public void SetExpandedRecursively_True_ExpandsAllExpandableDescendants()
        {
            RuleTreeItem root = BuildNestedTree(out RuleTreeItem parentRule, out RuleTreeItem childRule, out RuleTreeItem leafRule);
            RuleTreeItem.SetExpandedRecursively(root, false);

            RuleTreeItem.SetExpandedRecursively(root, true);

            Assert.That(parentRule.IsExpanded, Is.True);
            Assert.That(childRule.IsExpanded, Is.True);
            Assert.That(parentRule.IsVisible, Is.True);
            Assert.That(childRule.IsVisible, Is.True);
            Assert.That(leafRule.IsVisible, Is.True);
            Assert.That(leafRule.IsExpanded, Is.False);
        }

        private static RuleTreeItem BuildNestedTree(out RuleTreeItem parentRule, out RuleTreeItem childRule, out RuleTreeItem leafRule)
        {
            RuleTreeItem root = new() { IsRoot = true, IsVisible = true };
            parentRule = new RuleTreeItem { Data = new Rule { Id = 1 }, IsRule = true, IsVisible = true };
            childRule = new RuleTreeItem { Data = new Rule { Id = 2 }, IsRule = true, IsVisible = true };
            leafRule = new RuleTreeItem { Data = new Rule { Id = 3 }, IsRule = true, IsVisible = true };

            AttachChild(root, parentRule);
            AttachChild(parentRule, childRule);
            AttachChild(childRule, leafRule);

            parentRule.IsExpanded = true;
            childRule.IsExpanded = true;

            return root;
        }

        private static void AttachChild(RuleTreeItem parent, RuleTreeItem child)
        {
            child.Parent = parent;
            parent.Children.Add(child);
        }
    }
}
