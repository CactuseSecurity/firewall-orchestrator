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
        private static readonly List<int> SelectedPolicyAStartIds = [110];
        private static readonly List<int> SelectedPolicyBStartIds = [150];
        private static readonly List<int> SelectedBothPolicyStartIds = [110, 150];
        private static readonly List<int> SelectedWithoutGatewayStartIds = [190];
        private static readonly List<int> ExpectedPolicyARulebaseIds = [110, 120, 130];
        private static readonly List<int> ExpectedPolicyBRulebaseIds = [150, 120, 140];
        private static readonly List<int> ExpectedBothPolicyRulebaseIds = [110, 120, 130, 150, 140];
        private static readonly List<int> ExpectedWithoutGatewayRulebaseIds = [190];
        private static readonly List<int> SelectedSharedPolicyStartIds = [210];
        private static readonly List<int> ExpectedSharedPolicyRulebaseIds = [210, 220, 230];
        private static readonly List<int> SharedPolicyGatewayIds = [1, 2, 3];
        private static readonly List<int> SelectedSharedStartIds = [300];
        private static readonly List<int> ExpectedSharedStartRulebaseIds = [300, 310, 320, 330];
        private static readonly List<int> ExpectedTreeIds = [-1, -2];
        private static readonly List<int> ExpectedSecondPolicyReferencedIds = [120, 125];
        private static readonly List<int> ExpectedSharedStartReferencedIds = [300, 310];
        private static readonly List<string> ExpectedSharedStartTreeNames = ["Shared start (GW-A)", "Shared start (GW-B)"];
        private static readonly List<string> ExpectedSecondPolicyTreeLinks = ["initial->150", "150->120", "120->140"];
        private static readonly List<string> ExpectedSharedStartSecondTreeLinks = ["initial->300", "300->310", "310->330"];
        private const int kGatewayA = 1;
        private const int kGatewayB = 2;

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

        [Test]
        public void GetRulebaseIdsReachableFrom_FollowsSharedRulebaseOnlyAlongGatewaysOfStartRulebase()
        {
            ManagementReport managementReport = CreateSharedLayerManagementReport();

            Assert.Multiple(() =>
            {
                Assert.That(RulebaseChainScope.GetRulebaseIdsReachableFrom(managementReport, SelectedPolicyAStartIds), Is.EqualTo(ExpectedPolicyARulebaseIds));
                Assert.That(RulebaseChainScope.GetRulebaseIdsReachableFrom(managementReport, SelectedPolicyBStartIds), Is.EqualTo(ExpectedPolicyBRulebaseIds));
                Assert.That(RulebaseChainScope.GetRulebaseIdsReachableFrom(managementReport, SelectedBothPolicyStartIds), Is.EqualTo(ExpectedBothPolicyRulebaseIds));
            });
        }

        [Test]
        public void GetRulebaseIdsReachableFrom_KeepsRulebaseWithoutGatewayAlone()
        {
            ManagementReport managementReport = CreateSharedLayerManagementReport();

            List<int> rulebaseIds = RulebaseChainScope.GetRulebaseIdsReachableFrom(managementReport, SelectedWithoutGatewayStartIds);

            Assert.That(rulebaseIds, Is.EqualTo(ExpectedWithoutGatewayRulebaseIds));
        }

        [Test]
        public void ScopeToSelectedRulebases_CreatesTreePerStartAndReferencesSharedLayerInLaterTree()
        {
            ManagementReport managementReport = CreateSharedLayerWithInlineManagementReport();

            RulebaseChainScope.ScopeToSelectedRulebases(managementReport, SelectedBothPolicyStartIds);

            DeviceReport secondTree = managementReport.Devices[1];
            Assert.Multiple(() =>
            {
                Assert.That(managementReport.Devices.Select(tree => tree.Id), Is.EqualTo(ExpectedTreeIds));
                Assert.That(managementReport.Devices[0].ReferencedRulebaseTreeIds, Is.Empty);
                Assert.That(secondTree.ReferencedRulebaseTreeIds.Keys.Order(), Is.EqualTo(ExpectedSecondPolicyReferencedIds));
                Assert.That(secondTree.ReferencedRulebaseTreeIds.Values.Distinct().Single(), Is.EqualTo(-1));
                Assert.That(secondTree.RulebaseLinks.Select(DescribeLink), Is.EqualTo(ExpectedSecondPolicyTreeLinks));
                Assert.That(secondTree.RulebaseLinks.All(link => link.GatewayId == secondTree.Id), Is.True);
                Assert.That(managementReport.Rulebases.All(rulebase => rulebase.IncomingLinks == null), Is.True);
            });
        }

        [Test]
        public void ScopeToSelectedRulebases_PolicyOnSeveralGatewaysCreatesOneTreeWithoutReferences()
        {
            ManagementReport managementReport = new()
            {
                Rulebases =
                [
                    new RulebaseReport { Id = 210, IncomingLinks = [.. SharedPolicyGatewayIds.Select(gatewayId => CreateLink(gatewayId, null, true))] },
                    new RulebaseReport { Id = 220, IncomingLinks = [.. SharedPolicyGatewayIds.Select(gatewayId => CreateLink(gatewayId, 210))] },
                    new RulebaseReport { Id = 230, IncomingLinks = [.. SharedPolicyGatewayIds.Select(gatewayId => CreateLink(gatewayId, 220))] }
                ]
            };

            RulebaseChainScope.ScopeToSelectedRulebases(managementReport, SelectedSharedPolicyStartIds);

            Assert.That(managementReport.Rulebases.Select(rulebase => rulebase.Id), Is.EqualTo(ExpectedSharedPolicyRulebaseIds));
            Assert.That(managementReport.Devices, Has.Length.EqualTo(1));
            Assert.That(managementReport.Devices[0].ReferencedRulebaseTreeIds, Is.Empty);
            Assert.That(managementReport.Devices[0].RulebaseLinks, Has.Length.EqualTo(3));
        }

        [Test]
        public void ScopeToSelectedRulebases_SharedStartWithDifferentChainsCreatesTreePerChainNamedByGateways()
        {
            ManagementReport managementReport = new()
            {
                Devices = [new DeviceReport { Id = kGatewayA, Name = "GW-A" }, new DeviceReport { Id = kGatewayB, Name = "GW-B" }],
                Rulebases =
                [
                    new RulebaseReport { Id = 300, Name = "Shared start", IncomingLinks = [CreateLink(kGatewayA, null, true), CreateLink(kGatewayB, null, true)] },
                    new RulebaseReport { Id = 310, IncomingLinks = [CreateLink(kGatewayA, 300), CreateLink(kGatewayB, 300)] },
                    new RulebaseReport { Id = 320, IncomingLinks = [CreateLink(kGatewayA, 310)] },
                    new RulebaseReport { Id = 330, IncomingLinks = [CreateLink(kGatewayB, 310)] }
                ]
            };

            RulebaseChainScope.ScopeToSelectedRulebases(managementReport, SelectedSharedStartIds);

            Assert.Multiple(() =>
            {
                Assert.That(managementReport.Rulebases.Select(rulebase => rulebase.Id), Is.EqualTo(ExpectedSharedStartRulebaseIds));
                Assert.That(managementReport.Devices.Select(tree => tree.Name), Is.EqualTo(ExpectedSharedStartTreeNames));
                Assert.That(managementReport.Devices[1].ReferencedRulebaseTreeIds.Keys.Order(), Is.EqualTo(ExpectedSharedStartReferencedIds));
                Assert.That(managementReport.Devices[1].RulebaseLinks.Select(DescribeLink), Is.EqualTo(ExpectedSharedStartSecondTreeLinks));
            });
        }

        [Test]
        public void ScopeToSelectedRulebases_KeepsInitialLinkTypeAndHandlesRulebaseWithoutGateway()
        {
            ManagementReport managementReport = new()
            {
                Rulebases =
                [
                    new RulebaseReport { Id = 400, IncomingLinks = [new RulebaseLink { GatewayId = kGatewayA, IsInitial = true, LinkType = RulebaseLinkTypes.Policy }] },
                    new RulebaseReport { Id = 190, Name = "Without gateway" }
                ]
            };
            List<int> startRulebaseIds = [400, 190];

            RulebaseChainScope.ScopeToSelectedRulebases(managementReport, startRulebaseIds);

            Assert.That(managementReport.Devices[0].RulebaseLinks.Single().LinkType, Is.EqualTo(RulebaseLinkTypes.Policy));
            Assert.That(managementReport.Devices[1].Name, Is.EqualTo("Without gateway"));
            Assert.That(managementReport.Devices[1].RulebaseLinks.Single().IsInitial, Is.True);
            Assert.That(managementReport.Devices[1].RulebaseLinks.Single().NextRulebaseId, Is.EqualTo(190));
        }

        private static string DescribeLink(RulebaseLink link)
        {
            return link.IsInitial ? $"initial->{link.NextRulebaseId}" : $"{link.FromRulebaseId}->{link.NextRulebaseId}";
        }

        /// <summary>
        /// Like <see cref="CreateSharedLayerManagementReport"/>, with an inline layer 125 called from rule 5 of the shared layer 120.
        /// </summary>
        private static ManagementReport CreateSharedLayerWithInlineManagementReport()
        {
            ManagementReport managementReport = CreateSharedLayerManagementReport();
            RulebaseLink inlineA = new() { GatewayId = kGatewayA, LinkType = RulebaseLinkTypes.Inline, FromRuleId = 5, FromRule = new Rule { Id = 5, RulebaseId = 120 } };
            RulebaseLink inlineB = new() { GatewayId = kGatewayB, LinkType = RulebaseLinkTypes.Inline, FromRuleId = 5, FromRule = new Rule { Id = 5, RulebaseId = 120 } };
            managementReport.Rulebases = [.. managementReport.Rulebases, new RulebaseReport { Id = 125, IncomingLinks = [inlineA, inlineB] }];
            return managementReport;
        }

        /// <summary>
        /// Creates two policies sharing layer 120: gateway A 110 -> 120 -> 130, gateway B 150 -> 120 -> 140,
        /// plus rulebase 190 without any gateway.
        /// </summary>
        private static ManagementReport CreateSharedLayerManagementReport()
        {
            return new()
            {
                Rulebases =
                [
                    new RulebaseReport { Id = 110, IncomingLinks = [CreateLink(kGatewayA, null, true)] },
                    new RulebaseReport { Id = 150, IncomingLinks = [CreateLink(kGatewayB, null, true)] },
                    new RulebaseReport { Id = 120, IncomingLinks = [CreateLink(kGatewayA, 110), CreateLink(kGatewayB, 150)] },
                    new RulebaseReport { Id = 130, IncomingLinks = [CreateLink(kGatewayA, 120)] },
                    new RulebaseReport { Id = 140, IncomingLinks = [CreateLink(kGatewayB, 120)] },
                    new RulebaseReport { Id = 190, Name = "Without gateway" }
                ]
            };
        }

        private static RulebaseLink CreateLink(int gatewayId, int? fromRulebaseId, bool isInitial = false)
        {
            return new RulebaseLink { GatewayId = gatewayId, LinkType = RulebaseLinkTypes.Ordered, FromRulebaseId = fromRulebaseId, IsInitial = isInitial };
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
