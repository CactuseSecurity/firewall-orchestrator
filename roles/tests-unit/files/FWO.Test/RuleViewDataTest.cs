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
        public void GetFromCustomField_KeyOne_ReturnsCorrectValue()
        {
            RuleViewData rvd = new RuleViewData();
            Rule rule = new Rule
            {
                CustomFields = "{'field-2':'Change123','field-3':'Ado456'}"
            };

            string result = rvd.GetFromCustomField(rule, ["field-2", "Datum-Regelpruefung"]);

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

            string result = rvd.GetFromCustomField(rule, ["field-2", "Datum-Regelpruefung"]);

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

            string resultDatumRegelpruefung = rvd.GetFromCustomField(rule, ["field-2", "Datum-Regelpruefung"]);
            string resultAdoItId = rvd.GetFromCustomField(rule, ["field-3", "AdoIT"]);

            Assert.That("dd.mm.yyyy".Equals(resultDatumRegelpruefung));
            Assert.That("Infr-AdoIT:X".Equals(resultAdoItId));
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

    }
}
