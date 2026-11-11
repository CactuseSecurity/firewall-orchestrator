using FWO.Data.Workflow;
using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace FWO.Test
{
    internal class TimeObjectNamingHelperTest
    {
        [Test]
        public void ParseSuffixes_UsesEmptyDefaults()
        {
            Dictionary<string, string> suffixes = TimeObjectNamingHelper.ParseSuffixes(null);

            Assert.Multiple(() =>
            {
                Assert.That(suffixes["requester"], Is.Empty);
                Assert.That(suffixes["approver"], Is.Empty);
            });
        }

        [Test]
        public void ParseSuffixes_UsesConfiguredValuesCaseInsensitively()
        {
            Dictionary<string, string> suffixes = TimeObjectNamingHelper.ParseSuffixes("{\"REQUESTER\":\"_user\",\"approver\":\"_approval\"}");

            Assert.Multiple(() =>
            {
                Assert.That(suffixes["requester"], Is.EqualTo("_user"));
                Assert.That(suffixes["approver"], Is.EqualTo("_approval"));
            });
        }

        [Test]
        public void ParseSuffixes_RejectsMalformedConfigurationSafely()
        {
            Dictionary<string, string> suffixes = TimeObjectNamingHelper.ParseSuffixes("not-json");

            Assert.That(suffixes["requester"], Is.Empty);
        }

        [Test]
        public void BuildDisplayName_AppendsConfiguredOriginSuffix()
        {
            Dictionary<string, string> suffixes = new() { ["requester"] = "_user", ["approver"] = "_approval" };

            string result = TimeObjectNamingHelper.BuildDisplayName("maintenance", TimeObjectOrigin.Approver, suffixes);

            Assert.That(result, Is.EqualTo("maintenance_approval"));
        }

        [Test]
        public void BuildDisplayName_DoesNotAppendSuffixTwice()
        {
            Dictionary<string, string> suffixes = new() { ["requester"] = "_user", ["approver"] = "_approval" };

            string result = TimeObjectNamingHelper.BuildDisplayName("maintenance_approval", TimeObjectOrigin.Approver, suffixes);

            Assert.That(result, Is.EqualTo("maintenance_approval"));
        }

        [Test]
        public void BuildDisplayName_PreservesSuffixFromAnotherOrigin()
        {
            Dictionary<string, string> suffixes = new() { ["requester"] = "_user", ["approver"] = "_approval" };

            string result = TimeObjectNamingHelper.BuildDisplayName("maintenance_user", TimeObjectOrigin.Approver, suffixes);

            Assert.That(result, Is.EqualTo("maintenance_user"));
        }

        [TestCase(FlowIntegrationTimePrecisionOptions.Date, "2026-08-01 - 2026-08-02")]
        [TestCase(FlowIntegrationTimePrecisionOptions.Hours, "2026-08-01 08 h - 2026-08-02 17 h")]
        [TestCase(FlowIntegrationTimePrecisionOptions.Minutes, "2026-08-01 08:15 - 2026-08-02 17:45")]
        [TestCase(FlowIntegrationTimePrecisionOptions.Seconds, "2026-08-01 08:15:30 - 2026-08-02 17:45:45")]
        public void BuildDateBasedName_UsesConfiguredPrecision(string precision, string expected)
        {
            string result = TimeObjectNamingHelper.BuildDateBasedName(
                new DateTime(2026, 8, 1, 8, 15, 30),
                new DateTime(2026, 8, 2, 17, 45, 45), precision);

            Assert.That(result, Is.EqualTo(expected));
        }

        [Test]
        public void BuildDateBasedName_SupportsOpenBounds()
        {
            Assert.Multiple(() =>
            {
                Assert.That(TimeObjectNamingHelper.BuildDateBasedName(new DateTime(2026, 8, 1), null, FlowIntegrationTimePrecisionOptions.Date), Is.EqualTo(">= 2026-08-01"));
                Assert.That(TimeObjectNamingHelper.BuildDateBasedName(null, new DateTime(2026, 8, 2), FlowIntegrationTimePrecisionOptions.Date), Is.EqualTo("< 2026-08-02"));
                Assert.That(TimeObjectNamingHelper.BuildDateBasedName(null, null, FlowIntegrationTimePrecisionOptions.Date), Is.Empty);
            });
        }

        [TestCase(FlowIntegrationTimePrecisionOptions.Date, "01.08.2026")]
        [TestCase(FlowIntegrationTimePrecisionOptions.Hours, "01.08.2026 8 h")]
        [TestCase(FlowIntegrationTimePrecisionOptions.Minutes, "01.08.2026 08:15")]
        [TestCase(FlowIntegrationTimePrecisionOptions.Seconds, "01.08.2026 08:15:30")]
        public void DisplayValue_UsesPrecisionAndCallerDateFormat(string precision, string expected)
        {
            string result = TimeObjectDisplayHelper.FormatValue(
                new DateTime(2026, 8, 1, 8, 15, 30), precision, isEndDate: false, dateFormat: "dd.MM.yyyy");

            Assert.That(result, Is.EqualTo(expected));
        }

    }
}
