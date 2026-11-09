using FWO.Data;
using FWO.ExternalSystems.FirewallWrite;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class FirewallGatewayRulebasesTest
    {
        internal const int kLinkTypeOrdered = 2;
        internal const int kLinkTypeDomain = 5;
        internal const int kLinkTypePolicy = 7;
        private const int kPackageId = 10;
        private const int kLayerId = 11;
        private const int kSecondLayerId = 12;
        private static readonly List<RulebaseLink> kNoLinks = [];

        internal static RulebaseLink Link(int linkType, long toId, string toName, int? fromRulebaseId = null, bool isGlobal = false) => new()
        {
            LinkType = linkType,
            FromRulebaseId = fromRulebaseId,
            IsGlobal = isGlobal,
            ToRulebase = new Rulebase { Id = toId, Name = toName, Uid = $"uid-{toId}" }
        };

        /// <summary>Package of a Check Point gateway and its first ordered layer, as linked by the importer.</summary>
        internal static List<RulebaseLink> CheckPointLinks() =>
        [
            Link(kLinkTypeOrdered, kSecondLayerId, "Second Layer", kLayerId),
            Link(kLinkTypeOrdered, kLayerId, "Network", kPackageId),
            Link(kLinkTypePolicy, kPackageId, "Standard")
        ];

        [Test]
        public void FromLinks_FollowsPolicyLinkToFirstOrderedLayer()
        {
            FirewallGatewayRulebases rulebases = FirewallGatewayRulebases.FromLinks(CheckPointLinks());
            using (Assert.EnterMultipleScope())
            {
                Assert.That(rulebases.PolicyName, Is.EqualTo("Standard"));
                Assert.That(rulebases.FirstLayerName, Is.EqualTo("Network"));
            }
        }

        [Test]
        public void FromLinks_BelowGlobalPolicyUsesTheDomainLink()
        {
            List<RulebaseLink> links =
            [
                Link(kLinkTypePolicy, 1, "Global Package", isGlobal: true),
                Link(kLinkTypePolicy, kPackageId, "Domain Package"),
                Link(kLinkTypeDomain, kLayerId, "Domain Layer", 1)
            ];
            FirewallGatewayRulebases rulebases = FirewallGatewayRulebases.FromLinks(links);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(rulebases.PolicyName, Is.EqualTo("Domain Package"));
                Assert.That(rulebases.FirstLayerName, Is.EqualTo("Domain Layer"));
            }
        }

        [Test]
        public void FromLinks_WithoutLinksKnowsNothing()
        {
            List<RulebaseLink> onlyGlobal = [Link(kLinkTypePolicy, 1, "Global Package", isGlobal: true)];
            using (Assert.EnterMultipleScope())
            {
                Assert.That(FirewallGatewayRulebases.FromLinks(kNoLinks), Is.EqualTo(FirewallGatewayRulebases.None));
                Assert.That(FirewallGatewayRulebases.FromLinks(onlyGlobal), Is.EqualTo(FirewallGatewayRulebases.None));
            }
        }
    }
}
