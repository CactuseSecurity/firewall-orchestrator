using FWO.Data;
using FWO.ExternalSystems.FirewallWrite;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class FirewallRuleLayersTest
    {
        private static readonly Rulebase kLayer = new() { Id = 1, Uid = "layer-uid", Name = "Network" };
        private static readonly Rulebase kSection = new() { Id = 2, Uid = "section-uid", Name = "Section A" };
        private static readonly Rulebase kSubSection = new() { Id = 3, Uid = "sub-section-uid", Name = "Section B" };
        private static readonly List<RulebaseLink> kNoLinks = [];
        private static readonly Rulebase kOtherLayer = new() { Id = 4, Uid = "other-layer-uid", Name = "Other" };

        internal static Rule RuleIn(string uid, Rulebase rulebase) => new() { Uid = uid, RulebaseId = (int)rulebase.Id, Rulebase = rulebase };

        internal static RulebaseLink SectionLink(Rulebase parent, Rulebase section) =>
            new() { FromRulebaseId = (int)parent.Id, FromRulebase = parent, ToRulebase = section, IsSection = true };

        private static FirewallRuleLayerQueryResult Result(List<Rule> rules, List<RulebaseLink> sectionLinks) =>
            new() { Rules = rules, SectionLinks = sectionLinks };

        [Test]
        public void RuleDirectlyInALayerGetsThatLayer()
        {
            List<Rule> rules = [RuleIn("r1", kLayer), RuleIn("r2", kOtherLayer)];
            Dictionary<string, string> layers = FirewallRuleLayers.Resolve(Result(rules, kNoLinks));
            using (Assert.EnterMultipleScope())
            {
                Assert.That(layers["r1"], Is.EqualTo("layer-uid"));
                Assert.That(layers["r2"], Is.EqualTo("other-layer-uid"));
            }
        }

        [Test]
        public void RuleInNestedSectionsGetsTheLayerAbove()
        {
            List<Rule> rules = [RuleIn("r1", kSubSection), RuleIn("r2", kSection)];
            List<RulebaseLink> links = [SectionLink(kLayer, kSection), SectionLink(kSection, kSubSection), SectionLink(kLayer, kSection)];
            Dictionary<string, string> layers = FirewallRuleLayers.Resolve(Result(rules, links));
            using (Assert.EnterMultipleScope())
            {
                Assert.That(layers["r1"], Is.EqualTo("layer-uid"));
                Assert.That(layers["r2"], Is.EqualTo("layer-uid"));
            }
        }

        [Test]
        public void RuleWithoutUidOrRulebaseUidIsLeftOut()
        {
            Rulebase withoutUid = new() { Id = 9, Uid = "" };
            List<Rule> rules = [RuleIn("", kLayer), RuleIn("r1", withoutUid)];
            Assert.That(FirewallRuleLayers.Resolve(Result(rules, kNoLinks)), Is.Empty);
        }

        [Test]
        public void SectionWithUnknownParentLeavesTheRuleOut()
        {
            RulebaseLink link = new() { FromRulebaseId = 99, ToRulebase = kSection, IsSection = true };
            List<Rule> rules = [RuleIn("r1", kSection)];
            List<RulebaseLink> links = [link];
            Assert.That(FirewallRuleLayers.Resolve(Result(rules, links)), Is.Empty);
        }

        [Test]
        public void SectionBelowTwoRulebasesFails()
        {
            List<Rule> rules = [RuleIn("r1", kSection)];
            List<RulebaseLink> links = [SectionLink(kLayer, kSection), SectionLink(kOtherLayer, kSection)];
            Assert.Throws<FirewallWriteException>(() => FirewallRuleLayers.Resolve(Result(rules, links)));
        }

        [Test]
        public void RuleInTwoLayersFails()
        {
            List<Rule> rules = [RuleIn("r1", kLayer), RuleIn("r1", kOtherLayer)];
            Assert.Throws<FirewallWriteException>(() => FirewallRuleLayers.Resolve(Result(rules, kNoLinks)));
        }

        [Test]
        public void TooManyRulesFail()
        {
            List<Rule> rules = [.. Enumerable.Range(0, FirewallRuleLayers.kMaxRules + 1).Select(index => RuleIn($"r{index}", kLayer))];
            Assert.Throws<FirewallWriteException>(() => FirewallRuleLayers.Resolve(Result(rules, kNoLinks)));
        }

        [Test]
        public void SectionCycleFails()
        {
            List<Rule> rules = [RuleIn("r1", kSection)];
            List<RulebaseLink> links = [SectionLink(kSubSection, kSection), SectionLink(kSection, kSubSection)];
            Assert.Throws<FirewallWriteException>(() => FirewallRuleLayers.Resolve(Result(rules, links)));
        }
    }
}
