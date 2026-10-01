using FWO.Data;
using FWO.Data.Report;
using FWO.Report;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class RulebaseChainScopeTest
    {
        private static readonly List<int> SelectedChainStartRulebaseIds = [10];
        private static readonly List<int> SelectedTwoChainStartRulebaseIds = [50, 10, 99];
        private static readonly List<int> ExpectedChainRulebaseIds = [10, 20, 30];
        private static readonly List<int> ExpectedTwoChainRulebaseIds = [50, 10, 20, 30];
        private static readonly List<int> ExpectedCyclicRulebaseIds = [10, 20];

        [Test]
        public void GetRulebaseIdsReachableFrom_FollowsRulebaseAndRuleLinksButNotNat()
        {
            ManagementReport managementReport = CreateLinkedRulebasesManagementReport();

            List<int> rulebaseIds = RulebaseChainScope.GetRulebaseIdsReachableFrom(managementReport, SelectedChainStartRulebaseIds);

            Assert.That(rulebaseIds, Is.EqualTo(ExpectedChainRulebaseIds));
        }

        [Test]
        public void GetRulebaseIdsReachableFrom_OrdersChainsByStartAndIgnoresUnknownStarts()
        {
            ManagementReport managementReport = CreateLinkedRulebasesManagementReport();

            List<int> rulebaseIds = RulebaseChainScope.GetRulebaseIdsReachableFrom(managementReport, SelectedTwoChainStartRulebaseIds);

            Assert.That(rulebaseIds, Is.EqualTo(ExpectedTwoChainRulebaseIds));
        }

        [Test]
        public void GetRulebaseIdsReachableFrom_StopsOnCyclicLinks()
        {
            ManagementReport managementReport = new()
            {
                Rulebases =
                [
                    new RulebaseReport { Id = 10, IncomingLinks = [new RulebaseLink { LinkType = RulebaseLinkTypes.Ordered, FromRulebaseId = 20, NextRulebaseId = 10 }] },
                    new RulebaseReport { Id = 20, IncomingLinks = [new RulebaseLink { LinkType = RulebaseLinkTypes.Ordered, FromRulebaseId = 10, NextRulebaseId = 20 }] }
                ]
            };

            List<int> rulebaseIds = RulebaseChainScope.GetRulebaseIdsReachableFrom(managementReport, SelectedChainStartRulebaseIds);

            Assert.That(rulebaseIds, Is.EqualTo(ExpectedCyclicRulebaseIds));
        }

        [Test]
        public void ScopeToSelectedRulebases_KeepsOnlySelectedChainsInOrder()
        {
            ManagementReport managementReport = CreateLinkedRulebasesManagementReport();

            RulebaseChainScope.ScopeToSelectedRulebases(managementReport, SelectedChainStartRulebaseIds);

            Assert.That(managementReport.Rulebases.Select(rulebase => rulebase.Id), Is.EqualTo(ExpectedChainRulebaseIds));
            Assert.That(managementReport.Rulebases.All(rulebase => rulebase.IncomingLinks == null), Is.True);
        }

        /// <summary>
        /// Creates rulebases 10 -> 20 (ordered), rule in 20 -> 30 (inline), 10 -> 40 (NAT) and unlinked 50.
        /// </summary>
        private static ManagementReport CreateLinkedRulebasesManagementReport()
        {
            return new()
            {
                Rulebases =
                [
                    new RulebaseReport { Id = 50, Name = "Unlinked" },
                    new RulebaseReport { Id = 40, Name = "NAT", IncomingLinks = [new RulebaseLink { LinkType = RulebaseLinkTypes.Nat, FromRulebaseId = 10, NextRulebaseId = 40 }] },
                    new RulebaseReport { Id = 30, Name = "Inline", IncomingLinks = [new RulebaseLink { LinkType = RulebaseLinkTypes.Inline, FromRuleId = 5, FromRule = new Rule { Id = 5, RulebaseId = 20 }, NextRulebaseId = 30 }] },
                    new RulebaseReport { Id = 20, Name = "Layer 2", IncomingLinks = [new RulebaseLink { LinkType = RulebaseLinkTypes.Ordered, FromRulebaseId = 10, NextRulebaseId = 20 }] },
                    new RulebaseReport { Id = 10, Name = "Layer 1", IncomingLinks = [new RulebaseLink { IsInitial = true, LinkType = RulebaseLinkTypes.Ordered, NextRulebaseId = 10 }] }
                ]
            };
        }
    }
}
