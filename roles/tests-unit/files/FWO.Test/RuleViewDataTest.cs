using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using FWO.Services.RuleTreeBuilder;
using FWO.Data;
using FWO.Data.Report;
using FWO.Report.Data.ViewData;

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

        

    }
}