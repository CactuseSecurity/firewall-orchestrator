using FWO.Basics;
using FWO.Data;
using FWO.Ui.Display;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Verifies that the JSON rule export keeps imported values inside their JSON string.
    /// </summary>
    [TestFixture]
    internal class RuleDisplayJsonTest
    {
        private const string kInjectedHeader = "x\", \"injected\": \"1\" } { \"\\";
        private static readonly List<string> kSectionHeaderOnly = ["section header"];

        [Test]
        public void DisplayRuleJsonObject_SectionHeaderWithJsonPayload_StaysOneStringValue()
        {
            RuleDisplayJson ruleDisplayJson = new(new SimulatedUserConfig());
            Rule rule = new() { SectionHeader = kInjectedHeader };

            string json = ruleDisplayJson.DisplayRuleJsonObject(rule, ReportType.Rules).TrimEnd().TrimEnd(',');
            JObject parsed = JObject.Parse(json);

            Assert.Multiple(() =>
            {
                Assert.That(parsed.Properties().Select(property => property.Name), Is.EquivalentTo(kSectionHeaderOnly));
                Assert.That(parsed["section header"]!.Value<string>(), Is.EqualTo(kInjectedHeader));
            });
        }
    }
}
