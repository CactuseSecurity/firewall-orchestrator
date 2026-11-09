using FWO.Basics;
using FWO.Config.Api.Data;
using FWO.Data.Enums;
using FWO.Services;
using FWO.Ui.Services;
using NUnit.Framework;
using System.Collections.Generic;

namespace FWO.Test
{
    /// <summary>
    /// Covers the configurable logging of rule_owner mapping issues: which messages a level still lets
    /// through, and how the setting travels between the editor and the configuration.
    /// </summary>
    [TestFixture]
    [Parallelizable]
    internal class RuleOwnerMappingLogLevelTest
    {
        [TestCase(RuleOwnerMappingLogLevel.None, false, false, false, false)]
        [TestCase(RuleOwnerMappingLogLevel.Error, true, false, false, false)]
        [TestCase(RuleOwnerMappingLogLevel.Warning, true, true, false, false)]
        [TestCase(RuleOwnerMappingLogLevel.Info, true, true, true, false)]
        [TestCase(RuleOwnerMappingLogLevel.Debug, true, true, true, true)]
        public void IsEnabled_LetsThroughEverythingUpToTheConfiguredLevel(RuleOwnerMappingLogLevel configuredLevel,
            bool error, bool warning, bool info, bool debug)
        {
            RuleOwnerMappingLogger logger = new(configuredLevel);

            Assert.Multiple(() =>
            {
                Assert.That(logger.IsEnabled(RuleOwnerMappingLogLevel.Error), Is.EqualTo(error));
                Assert.That(logger.IsEnabled(RuleOwnerMappingLogLevel.Warning), Is.EqualTo(warning));
                Assert.That(logger.IsEnabled(RuleOwnerMappingLogLevel.Info), Is.EqualTo(info));
                Assert.That(logger.IsEnabled(RuleOwnerMappingLogLevel.Debug), Is.EqualTo(debug));
            });
        }

        [Test]
        public void Default_KeepsTheBehaviourOfTheDefaultSetting()
        {
            // used where no configuration is reachable, so it must not start logging more than configured by default
            Assert.Multiple(() =>
            {
                Assert.That(RuleOwnerMappingLogger.Default.IsEnabled(RuleOwnerMappingLogLevel.Warning), Is.True);
                Assert.That(RuleOwnerMappingLogger.Default.IsEnabled(RuleOwnerMappingLogLevel.Info), Is.False);
            });
        }

        [Test]
        public void ConfigData_DefaultsToWarning()
        {
            // an installation that never wrote the setting keeps logging unmappable rules as before
            Assert.That(new ConfigData().RuleOwnerMappingLogLevel, Is.EqualTo(RuleOwnerMappingLogLevel.Warning));
        }

        [Test]
        public void Handler_OffersEveryLevel()
        {
            OwnerMappingSourceHandler handler = new();

            Assert.That(handler.LogLevels, Is.EquivalentTo(new List<RuleOwnerMappingLogLevel?>
            {
                RuleOwnerMappingLogLevel.None,
                RuleOwnerMappingLogLevel.Error,
                RuleOwnerMappingLogLevel.Warning,
                RuleOwnerMappingLogLevel.Info,
                RuleOwnerMappingLogLevel.Debug
            }));
        }

        [Test]
        public void ApplyTo_WritesTheSelectedLevelWithoutRequestingARebuild()
        {
            OwnerMappingSourceHandler handler = new();
            ConfigData configData = new() { OwnerSoruceMappingID = (int)OwnerMappingSourceStm.NameField };
            handler.Init(configData);

            handler.LogLevel = RuleOwnerMappingLogLevel.None;
            bool rebuildRequired = handler.ApplyTo(configData);

            Assert.Multiple(() =>
            {
                Assert.That(configData.RuleOwnerMappingLogLevel, Is.EqualTo(RuleOwnerMappingLogLevel.None));
                Assert.That(rebuildRequired, Is.False, "the log level does not influence the mappings, so no rebuild is needed");
            });
        }

        [Test]
        public void DiscardEdits_RestoresTheStoredLevel()
        {
            OwnerMappingSourceHandler handler = new();
            ConfigData configData = new() { RuleOwnerMappingLogLevel = RuleOwnerMappingLogLevel.Debug };
            handler.Init(configData);

            handler.LogLevel = RuleOwnerMappingLogLevel.None;
            handler.DiscardEdits();

            Assert.That(handler.LogLevel, Is.EqualTo(RuleOwnerMappingLogLevel.Debug));
        }
    }
}
