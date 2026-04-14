using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using FWO.Services.RuleTreeBuilder;
using FWO.Data;
using FWO.Data.Report;
using FWO.Report.Data.ViewData;
using FWO.Report;
using FWO.Ui.Display;
using FWO.Config.Api;

namespace FWO.Test
{
    [TestFixture]
    internal class RuleViewDataTest
    {
        [Test]
        public void ExtractCustomFieldValue_NoMatchingKey_ReturnsDefault()
        {
            var rule = new Rule
            {
                CustomFields = "{'field-1':'abc'}"
            };

            var result = CustomFieldResolver.ExtractCustomFieldValue<string>(rule, "[\"field-2\",\"field-3\"]", out var errorMessage);

            Assert.That(result, Is.Null);
            Assert.That(errorMessage, Is.Null);
        }

        [Test]
        public void ExtractCustomFieldValue_EmptyCustomFields_ReturnsDefault()
        {
            var rule = new Rule
            {
                CustomFields = ""
            };

            var result = CustomFieldResolver.ExtractCustomFieldValue<string>(rule, "[\"field-2\"]", out var errorMessage);

            Assert.That(result, Is.Null);
            Assert.That(errorMessage, Is.Null);
        }

        [Test]
        public void GetFromCustomField_KeyOne_ReturnsCorrectValue()
        {
            RuleViewData rvd = new RuleViewData();
            Rule rule = new Rule
            {
                CustomFields = "{'field-2':'Change123','field-3':'Ado456'}"
            };

            string result = CustomFieldResolver.ExtractCustomFieldValue<string>(rule, "[\"field-2\",\"Datum-Regelpruefung\"]", out _) ?? "";

            Assert.That("Change123".Equals(result));
        }

        [Test]
        public void GetFromCustomField_KeyTwo_ReturnsCorrectValue()
        {
            RuleViewData rvd = new RuleViewData();
            Rule rule = new Rule
            {
                CustomFields = "{'Datum-Regelpruefung':'Change123','AdoIT':'Ado456'}"
            };

            string result = CustomFieldResolver.ExtractCustomFieldValue<string>(rule, "[\"field-2\",\"Datum-Regelpruefung\"]", out _) ?? "";

            Assert.That("Change123".Equals(result));
        }

        [Test]
        public void GetFromCustomField_Example_ReturnsCorrectValue()
        {
            RuleViewData rvd = new RuleViewData();
            Rule rule = new Rule
            {
                CustomFields = "{'AdoIT': \"Infr-AdoIT:X\", 'Datum-Regelpruefung': 'dd.mm.yyyy'}"
            };

            string resultDatumRegelpruefung = CustomFieldResolver.ExtractCustomFieldValue<string>(rule, "[\"field-2\",\"Datum-Regelpruefung\"]", out _) ?? "";
            string resultAdoItId = CustomFieldResolver.ExtractCustomFieldValue<string>(rule, "[\"field-3\",\"AdoIT\"]", out _) ?? "";

            Assert.That("dd.mm.yyyy".Equals(resultDatumRegelpruefung));
            Assert.That("Infr-AdoIT:X".Equals(resultAdoItId));
        }

        [Test]
        public void ExtractCustomFieldValue_InvalidJson_ReturnsDefaultAndErrorMessage()
        {
            var rule = new Rule
            {
                CustomFields = "{ invalid json }"
            };

            var result = CustomFieldResolver.ExtractCustomFieldValue<string>(rule, "[\"field-2\"]", out string? errorMessage);

            Assert.That(result, Is.Null);
            Assert.That(errorMessage, Is.Not.Null);
            Assert.That(errorMessage, Does.Contain("Error"));
        }

        [Test]
        public void ExtractCustomFieldValue_InvalidKeysJson_ReturnsDefaultAndError()
        {
            var rule = new Rule
            {
                CustomFields = "{'field-2':'abc'}"
            };

            var result = CustomFieldResolver.ExtractCustomFieldValue<string>(rule, "invalid json", out var errorMessage);

            Assert.That(result, Is.Null);
            Assert.That(errorMessage, Is.Not.Null);
        }

        [Test]
        public void ExtractCustomFieldValue_InvalidValueType_SkipsKeyAndReturnsFallback()
        {
            var rule = new Rule
            {
                CustomFields = "{'field-2':'not-a-json-object'}"
            };

            var result = CustomFieldResolver.ExtractCustomFieldValue<int>(rule, "[\"field-2\"]", out string? errorMessage);

            Assert.That(result, Is.EqualTo(0));
            Assert.That(errorMessage, Is.Not.Null);
        }

        [Test]
        public void ExtractCustomFieldValue_InvalidPrimary_UsesFallback()
        {
            var rule = new Rule
            {
                CustomFields = "{'field-2':'invalid', 'Datum-Regelpruefung':'Change123'}"
            };

            var result = CustomFieldResolver.ExtractCustomFieldValue<string>(rule, "[\"field-2\",\"Datum-Regelpruefung\"]", out var errorMessage);

            Assert.That(errorMessage, Is.Null);
        }

        [Test]
        public void ExtractCustomFieldValue_EmptyPrimaryUsesFallback()
        {
            var rule = new Rule
            {
                CustomFields = "{'field-2':'', 'Datum-Regelpruefung':'Change123'}"
            };

            var result = CustomFieldResolver.ExtractCustomFieldValue<string>(rule, "[\"field-2\",\"Datum-Regelpruefung\"]", out _);

            Assert.That(result, Is.EqualTo("Change123"));
        }

        [Test]
        public void RuleViewData_LastModified_UsesImportStartTime()
        {
            Rule rule = new Rule
            {
                LastSeenImport = new ImportControl { StartTime = new DateTime(2023, 04, 05) }
            };

            UserConfig userConfig = new UserConfig();
            NatRuleDisplayHtml ruleDisplay = new NatRuleDisplayHtml(userConfig);
            RuleViewData viewData = new RuleViewData(rule, ruleDisplay, OutputLocation.report, true);

            Assert.That(viewData.LastModified, Is.EqualTo("2023-04-05"));
        }

        [Test]
        public void RuleViewData_LastModified_UsesCreatedWhenNoLastSeen()
        {
            Rule rule = new Rule
            {
                Metadata = new RuleMetadata
                {
                    CreatedImport = new ImportControl { StartTime = new DateTime(2023, 01, 10) }
                }
            };

            UserConfig userConfig = new UserConfig();
            NatRuleDisplayHtml ruleDisplay = new NatRuleDisplayHtml(userConfig);
            RuleViewData viewData = new RuleViewData(rule, ruleDisplay, OutputLocation.report, true);

            Assert.That(viewData.LastModified, Is.EqualTo("2023-01-10"));
        }

        [Test]
        public void DisplayRuleTime_UsesFirstTimeObjectWithEndTime()
        {
            Rule rule = new Rule
            {
                RuleTimes =
                [
                    new RuleTime { TimeObj = new TimeObject { EndTime = null } },
                    new RuleTime { TimeObj = new TimeObject { EndTime = new DateTime(2026, 01, 02, 03, 04, 05) } },
                    new RuleTime { TimeObj = new TimeObject { EndTime = new DateTime(2030, 06, 07, 08, 09, 10) } }
                ]
            };

            UserConfig userConfig = new UserConfig();
            RuleDisplayHtml ruleDisplay = new RuleDisplayHtml(userConfig);

            Assert.That(ruleDisplay.DisplayRuleTime(rule), Is.EqualTo("2026-01-02 03:04:05"));
        }

        [Test]
        public void DisplayRuleTime_ReturnsEmpty_WhenNoTimeObjectHasEndTime()
        {
            Rule rule = new Rule
            {
                RuleTimes =
                [
                    new RuleTime { TimeObj = null },
                    new RuleTime { TimeObj = new TimeObject { EndTime = null } }
                ]
            };

            UserConfig userConfig = new UserConfig();
            RuleDisplayHtml ruleDisplay = new RuleDisplayHtml(userConfig);

            Assert.That(ruleDisplay.DisplayRuleTime(rule), Is.EqualTo(""));
        }

        [Test]
        public void RuleViewData_SetsRuleTime_FromDisplayRuleTime()
        {
            Rule rule = new Rule
            {
                RuleTimes =
                [
                    new RuleTime { TimeObj = new TimeObject { EndTime = new DateTime(2026, 12, 24, 11, 22, 33) } }
                ]
            };

            UserConfig userConfig = new UserConfig();
            NatRuleDisplayHtml ruleDisplay = new NatRuleDisplayHtml(userConfig);
            RuleViewData viewData = new RuleViewData(rule, ruleDisplay, OutputLocation.report, true);

            Assert.That(viewData.RuleTime, Is.EqualTo("2026-12-24 11:22:33"));
        }
    }
}
