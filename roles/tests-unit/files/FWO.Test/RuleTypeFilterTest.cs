using System.Text.Json;
using FWO.Data;
using FWO.Middleware.Server.Services;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class RuleTypeFilterTest
    {
        private const string kPath = "options.filter.ruleTypes";
        private const string kAccessExpressionJson = "{\"access_rule\":{\"_eq\":true}}";
        private const string kNatExpressionJson = "{\"nat_rule\":{\"_eq\":true},\"xlate_rule\":{\"_is_null\":false}}";
        private static readonly List<string?> kAccessOnly = [RuleTypeFilter.kAccess];
        private static readonly List<string?> kNatOnly = [RuleTypeFilter.kNat];
        private static readonly List<string?> kAccessAndNat = [RuleTypeFilter.kAccess, RuleTypeFilter.kNat];
        private static readonly List<string?> kDuplicateNat = [RuleTypeFilter.kNat, RuleTypeFilter.kNat];
        private static readonly List<string?> kEmpty = [];
        private static readonly List<string?> kTooMany = [RuleTypeFilter.kAccess, RuleTypeFilter.kNat, RuleTypeFilter.kAccess];
        private static readonly List<string?> kUpperCase = ["NAT"];
        private static readonly List<string?> kUnknownAndNull = ["xlate", null];
        private static readonly List<string?> kTooManyAndInvalid = ["a", "b", "c"];

        [Test]
        public void Validate_AcceptsNullAndEveryAllowedCombination()
        {
            Assert.Multiple(() =>
            {
                Assert.That(RuleTypeFilter.Validate(null, kPath), Is.Empty);
                Assert.That(RuleTypeFilter.Validate(kAccessOnly, kPath), Is.Empty);
                Assert.That(RuleTypeFilter.Validate(kNatOnly, kPath), Is.Empty);
                Assert.That(RuleTypeFilter.Validate(kAccessAndNat, kPath), Is.Empty);
                Assert.That(RuleTypeFilter.Validate(kDuplicateNat, kPath), Is.Empty);
            });
        }

        [Test]
        public void Validate_RejectsEmptyList()
        {
            List<string> errors = RuleTypeFilter.Validate(kEmpty, kPath);

            Assert.That(errors, Has.Count.EqualTo(1));
            Assert.That(errors[0], Does.StartWith(kPath).And.Contain("at least one value"));
        }

        [Test]
        public void Validate_RejectsMoreValuesThanTheMaximum()
        {
            List<string> errors = RuleTypeFilter.Validate(kTooMany, kPath);

            Assert.That(errors, Has.Count.EqualTo(1));
            Assert.That(errors[0], Does.Contain($"more than {RuleTypeFilter.kMaxRuleTypeValues} values"));
        }

        [Test]
        public void Validate_RejectsUnknownNullAndWrongCaseValues()
        {
            List<string> unknownErrors = RuleTypeFilter.Validate(kUnknownAndNull, kPath);
            List<string> upperCaseErrors = RuleTypeFilter.Validate(kUpperCase, kPath);

            Assert.Multiple(() =>
            {
                Assert.That(unknownErrors, Has.Count.EqualTo(1));
                Assert.That(unknownErrors[0], Does.Contain("invalid: xlate, null"));
                Assert.That(upperCaseErrors, Has.Count.EqualTo(1));
                Assert.That(upperCaseErrors[0], Does.Contain("invalid: NAT"));
            });
        }

        [Test]
        public void Validate_ReportsCountAndValueErrorsTogether()
        {
            List<string> errors = RuleTypeFilter.Validate(kTooManyAndInvalid, kPath);

            Assert.That(errors, Has.Count.EqualTo(2));
        }

        [Test]
        public void Resolve_DefaultsToAccessRulesOnly()
        {
            Assert.That(RuleTypeFilter.Resolve(null), Is.EqualTo(new RuleTypeSelection(true, false)));
        }

        [Test]
        public void Resolve_SelectsTheListedTypes()
        {
            Assert.Multiple(() =>
            {
                Assert.That(RuleTypeFilter.Resolve(kNatOnly), Is.EqualTo(new RuleTypeSelection(false, true)));
                Assert.That(RuleTypeFilter.Resolve(kAccessAndNat), Is.EqualTo(new RuleTypeSelection(true, true)));
            });
        }

        [Test]
        public void BuildRuleBoolExp_MapsEachSelectionToTheRuleColumns()
        {
            string accessJson = JsonSerializer.Serialize(RuleTypeFilter.BuildRuleBoolExp(new RuleTypeSelection(true, false)));
            string natJson = JsonSerializer.Serialize(RuleTypeFilter.BuildRuleBoolExp(new RuleTypeSelection(false, true)));
            string bothJson = JsonSerializer.Serialize(RuleTypeFilter.BuildRuleBoolExp(new RuleTypeSelection(true, true)));

            Assert.Multiple(() =>
            {
                Assert.That(accessJson, Is.EqualTo(kAccessExpressionJson));
                Assert.That(natJson, Is.EqualTo(kNatExpressionJson));
                Assert.That(bothJson, Is.EqualTo($"{{\"_or\":[{kAccessExpressionJson},{kNatExpressionJson}]}}"));
            });
        }

        [Test]
        public void BuildRuleBoolExp_RejectsEmptySelection()
        {
            Assert.That(() => RuleTypeFilter.BuildRuleBoolExp(new RuleTypeSelection(false, false)), Throws.ArgumentException);
        }

        [Test]
        public void GetRuleType_TreatsOnlyTheOriginalNatRowAsNat()
        {
            Rule accessRule = new() { AccessRule = true };
            Rule natRule = new() { AccessRule = false, NatRule = true, XlateRule = 2 };
            Rule translationRow = new() { AccessRule = false, NatRule = true, XlateRule = null };

            Assert.Multiple(() =>
            {
                Assert.That(RuleTypeFilter.GetRuleType(accessRule), Is.EqualTo(RuleTypeFilter.kAccess));
                Assert.That(RuleTypeFilter.GetRuleType(natRule), Is.EqualTo(RuleTypeFilter.kNat));
                Assert.That(RuleTypeFilter.GetRuleType(translationRow), Is.EqualTo(RuleTypeFilter.kAccess));
                Assert.That(RuleTypeFilter.IsNatRule(natRule), Is.True);
                Assert.That(RuleTypeFilter.IsNatRule(translationRow), Is.False);
            });
        }
    }
}
