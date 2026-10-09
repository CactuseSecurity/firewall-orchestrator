using System.Globalization;
using FWO.Config.Api;
using FWO.Config.Api.Data;
using FWO.Data;
using FWO.Ui.Services;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class UiLogDataTitleTest
    {
        private const int kOneWeek = 604800;
        private const int kDayAndAHalf = 129600;
        private static readonly DateTimeOffset kImportTime = new(2026, 10, 5, 8, 30, 0, TimeSpan.Zero);
        private static readonly Language[] kUiLanguages =
        [
            new Language { Name = "German", CultureInfo = "de-DE" },
            new Language { Name = "English", CultureInfo = "en-US" }
        ];

        [Test]
        public void Build_UsesThePlainTitleWithoutStoredPeriod()
        {
            Assert.That(LogDataTitle.Build(null, new SimulatedUserConfig()), Is.EqualTo("log_data"));
        }

        /// <summary>
        /// A title may describe rows from one source or older imports only using their own timing.
        /// </summary>
        [Test]
        public void GetCommonPeriod_UsesTheRowsImportTime()
        {
            List<FirewallLogEntry> entries =
            [
                new OwnerFirewallLogEntry { ImportTime = kImportTime, LogTimeRangeInSeconds = kOneWeek },
                new OwnerFirewallLogEntry { ImportTime = kImportTime, LogTimeRangeInSeconds = kOneWeek }
            ];

            LogDataImportPeriod? period = LogDataTitle.GetCommonPeriod(entries);

            Assert.That(period?.ImportTime, Is.EqualTo(kImportTime));
            Assert.That(period?.LogTimeRangeInSeconds, Is.EqualTo(kOneWeek));
        }

        /// <summary>
        /// Retained rows from other runs or periods must not inherit the newest timing.
        /// </summary>
        [TestCase(true)]
        [TestCase(false)]
        public void GetCommonPeriod_RejectsMixedTiming(bool differentTime)
        {
            List<FirewallLogEntry> entries =
            [
                new OwnerFirewallLogEntry { ImportTime = kImportTime, LogTimeRangeInSeconds = kOneWeek },
                new OwnerFirewallLogEntry
                {
                    ImportTime = differentTime ? kImportTime.AddDays(-1) : kImportTime,
                    LogTimeRangeInSeconds = differentTime ? kOneWeek : kDayAndAHalf
                }
            ];

            Assert.That(LogDataTitle.GetCommonPeriod(entries), Is.Null);
        }

        /// <summary>
        /// Upgrade-era rows and empty tables cannot provide a reliable aggregation title.
        /// </summary>
        [TestCase(null)]
        [TestCase(0)]
        [TestCase(-1)]
        public void GetCommonPeriod_RejectsUnknownOrInvalidPeriod(int? seconds)
        {
            List<FirewallLogEntry> entries = [new OwnerFirewallLogEntry { ImportTime = kImportTime, LogTimeRangeInSeconds = seconds }];
            Assert.That(LogDataTitle.GetCommonPeriod(entries), Is.Null);
        }

        /// <summary>
        /// Missing import times and empty tables use the plain title.
        /// </summary>
        [Test]
        public void GetCommonPeriod_RejectsUnknownTimeAndEmptyRows()
        {
            List<FirewallLogEntry> entries = [new OwnerFirewallLogEntry { LogTimeRangeInSeconds = kOneWeek }];
            Assert.That(LogDataTitle.GetCommonPeriod(entries), Is.Null);
            entries.Clear();
            Assert.That(LogDataTitle.GetCommonPeriod(entries), Is.Null);
        }

        /// <summary>
        /// The heading defaults to a date and can include the time without changing the period.
        /// </summary>
        [TestCase(false)]
        [TestCase(true)]
        public void Build_NamesThePeriodAndTheImportDate(bool showTime)
        {
            AggregatedTitleUserConfig userConfig = new() { ShowLogImportTimeInHeading = showTime };

            string title = LogDataTitle.Build(new LogDataImportPeriod { LogTimeRangeInSeconds = kOneWeek, ImportTime = kImportTime }, userConfig);

            string expectedDate = kImportTime.ToLocalTime().DateTime.ToString(showTime ? "g" : "d", userConfig.GetUserCulture());
            Assert.That(title, Is.EqualTo($"Logs (aggregated over 1 {userConfig.GetText("Weeks")} until {expectedDate})"));
        }

        [Test]
        public void FormatTimeRange_UsesTheLocalizedUnitTexts()
        {
            SimulatedUserConfig userConfig = new();
            string weeks = userConfig.GetText("Weeks");
            string days = userConfig.GetText("Days");
            string hours = userConfig.GetText("Hours");
            string minutes = userConfig.GetText("Minutes2");
            string seconds = userConfig.GetText("Seconds");

            Assert.Multiple(() =>
            {
                Assert.That(LogDataTitle.FormatTimeRange(kOneWeek, userConfig), Is.EqualTo($"1 {weeks}"));
                Assert.That(LogDataTitle.FormatTimeRange(kDayAndAHalf, userConfig), Is.EqualTo($"1 {days} 12 {hours}"));
                Assert.That(LogDataTitle.FormatTimeRange(61, userConfig), Is.EqualTo($"1 {minutes} 1 {seconds}"));
            });
        }

        /// <summary>
        /// Both display modes retain the localized date and server-local timezone.
        /// </summary>
        [TestCase("German", "de-DE", false)]
        [TestCase("German", "de-DE", true)]
        [TestCase("English", "en-US", false)]
        [TestCase("English", "en-US", true)]
        public void FormatImportTime_UsesTheCultureOfTheUserLanguage(string language, string cultureName, bool includeTime)
        {
            using UserConfig userConfig = CreateUserConfig(language);

            string expected = kImportTime.ToLocalTime().DateTime.ToString(includeTime ? "g" : "d", CultureInfo.GetCultureInfo(cultureName));
            Assert.That(LogDataTitle.FormatImportTime(kImportTime, userConfig, includeTime), Is.EqualTo(expected));
        }

        /// <summary>
        /// Import time columns keep the time even when the heading shows only a date.
        /// </summary>
        [Test]
        public void FormatImportTime_IncludesTimeByDefault()
        {
            using UserConfig userConfig = CreateUserConfig("English");

            string expected = kImportTime.ToLocalTime().DateTime.ToString("g", userConfig.GetUserCulture());
            Assert.That(LogDataTitle.FormatImportTime(kImportTime, userConfig), Is.EqualTo(expected));
        }

        [Test]
        public void GetUserCulture_FallsBackToTheDefaultLanguageWithoutUserLanguage()
        {
            SimulatedGlobalConfig globalConfig = new() { UiLanguages = kUiLanguages, DefaultLanguage = "German" };
            using UserConfig userConfig = UserConfig.ForTextOnly(globalConfig, registerOnChangeHandler: false);

            Assert.That(userConfig.GetUserCulture().Name, Is.EqualTo("de-DE"));
        }

        [Test]
        public void GetUserCulture_UsesTheInvariantCultureForAnUnknownLanguage()
        {
            using UserConfig userConfig = CreateUserConfig("Klingon");

            Assert.That(userConfig.GetUserCulture(), Is.EqualTo(CultureInfo.InvariantCulture));
        }

        [Test]
        public void GetUserCulture_UsesTheInvariantCultureForAnInvalidCultureName()
        {
            Language[] languages = [new Language { Name = "English", CultureInfo = "not a culture!" }];
            SimulatedGlobalConfig globalConfig = new() { UiLanguages = languages };
            using UserConfig userConfig = UserConfig.ForTextOnly(globalConfig, registerOnChangeHandler: false);
            userConfig.SetLanguage("English");

            Assert.That(userConfig.GetUserCulture(), Is.EqualTo(CultureInfo.InvariantCulture));
        }

        [Test]
        public void GetUserCulture_UsesTheInvariantCultureWithoutGlobalConfig()
        {
            Assert.That(new SimulatedUserConfig().GetUserCulture(), Is.EqualTo(CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// Knows the text of the aggregated title, every other key is answered with itself.
        /// </summary>
        private sealed class AggregatedTitleUserConfig : SimulatedUserConfig
        {
            public override string GetText(string key)
            {
                return key == "log_data_aggregated" ? "Logs (aggregated over @@TIME_INTERVAL@@ until @@DATE@@)" : base.GetText(key);
            }
        }

        private static UserConfig CreateUserConfig(string language)
        {
            SimulatedGlobalConfig globalConfig = new() { UiLanguages = kUiLanguages };
            UserConfig userConfig = UserConfig.ForTextOnly(globalConfig, registerOnChangeHandler: false);
            userConfig.SetLanguage(language);
            return userConfig;
        }
    }
}
