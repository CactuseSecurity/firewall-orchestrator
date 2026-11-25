using FWO.Data.DeviceSpecificSettings;
using FWO.ExternalSystems.FirewallWrite;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Rulesets of the Guardicore rules of a connection: the app id by default, "app name (app id)" with the app ruleset
    /// algorithm, with the configured prefix for common services.
    /// </summary>
    [TestFixture]
    internal class GuardicoreRulesetsTest
    {
        private const ProvisioningPositioningAlgorithm kAppRulesets = ProvisioningPositioningAlgorithm.GuardicoreEndOfAppRulesetDistinguishCommonServices;

        [TestCase(ProvisioningPositioningAlgorithm.DefaultEndOfRulebase, "Exchange", false, "COM-1611")]
        [TestCase(ProvisioningPositioningAlgorithm.DefaultEndOfRulebase, "Exchange", true, "COM-1611")]
        [TestCase(kAppRulesets, " Exchange ", false, "Exchange (COM-1611)")]
        [TestCase(kAppRulesets, "Exchange", true, "ComSvc Exchange (COM-1611)")]
        [TestCase(kAppRulesets, " ", false, "COM-1611")]
        [TestCase(kAppRulesets, null, true, "ComSvc COM-1611")]
        public void Name_FollowsThePositioningAlgorithm(ProvisioningPositioningAlgorithm algorithm, string? appName, bool isCommonService, string expected)
        {
            Assert.That(GuardicoreRulesets.Name(algorithm, new(appName, " COM-1611 ", isCommonService), null), Is.EqualTo(expected));
        }

        [Test]
        public void Name_TakesTheConfiguredPrefix()
        {
            Assert.That(GuardicoreRulesets.Name(kAppRulesets, new("Exchange", "COM-1611", true), " CS "), Is.EqualTo("CS Exchange (COM-1611)"));
        }

        [TestCase(null)]
        [TestCase(" ")]
        public void Name_NeedsAnAppId(string? appId)
        {
            Assert.Throws<ArgumentException>(() => GuardicoreRulesets.Name(kAppRulesets, new("Exchange", appId, false), null));
        }

        [TestCase(null, GuardicoreRulesets.kDefaultCommonServicePrefix)]
        [TestCase(" ", GuardicoreRulesets.kDefaultCommonServicePrefix)]
        [TestCase(" Shared ", "Shared")]
        public void EffectivePrefix_FallsBackToTheDefault(string? prefix, string expected)
        {
            Assert.That(GuardicoreRulesets.EffectivePrefix(prefix), Is.EqualTo(expected));
        }

        [TestCase("04 Allow Exchange (COM-1611)", "Exchange (COM-1611)")]
        [TestCase("04 Allow ComSvc Exchange (COM-1611)", "ComSvc Exchange (COM-1611)")]
        [TestCase("04 Allow", "")]
        [TestCase("06 Block Exchange (COM-1611)", null)]
        [TestCase("04 Allowed", null)]
        [TestCase(null, null)]
        public void RulesetOfRulebase_ReadsTheRulesetOfAllowRulebases(string? rulebaseName, string? expected)
        {
            Assert.That(GuardicoreRulesets.RulesetOfRulebase(rulebaseName), Is.EqualTo(expected));
        }
    }
}
