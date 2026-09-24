using FWO.Basics;
using FWO.Data;
using FWO.Report;
using FWO.Report.Filter;
using FWO.Ui.Display;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class NatRuleDisplayHtmlTest
    {
        private NatRuleDisplayHtml _display = null!;

        [SetUp]
        public void SetUp()
        {
            _display = new NatRuleDisplayHtml(new SimulatedUserConfig());
        }

        private static Rule MakeNatRule(NatData natData) =>
            new Rule { Id = 1, MgmtId = 1, NatData = natData };

        // ── DisplayTranslatedSource ──────────────────────────────────────────

        [Test]
        public void DisplayTranslatedSource_EmptyFroms_ReturnsEmpty()
        {
            var rule = MakeNatRule(new NatData { TranslatedFroms = [] });

            var result = _display.DisplayTranslatedSource(rule, OutputLocation.export);

            Assert.That(result.Trim(), Is.Empty);
        }

        [Test]
        public void DisplayTranslatedSource_NegatedWithEmptyFroms_ContainsNegatedText()
        {
            var rule = MakeNatRule(new NatData { TranslatedSourceNegated = true, TranslatedFroms = [] });

            var result = _display.DisplayTranslatedSource(rule, OutputLocation.export);

            Assert.That(result, Does.Contain("not"));
        }

        [Test]
        public void DisplayTranslatedSource_NotNegated_DoesNotContainNegatedText()
        {
            var rule = MakeNatRule(new NatData { TranslatedSourceNegated = false, TranslatedFroms = [] });

            var result = _display.DisplayTranslatedSource(rule, OutputLocation.export);

            Assert.That(result, Does.Not.Contain("not"));
        }

        // ── DisplayTranslatedDestination ─────────────────────────────────────

        [Test]
        public void DisplayTranslatedDestination_EmptyTos_ReturnsEmpty()
        {
            var rule = MakeNatRule(new NatData { TranslatedTos = [] });

            var result = _display.DisplayTranslatedDestination(rule, OutputLocation.export);

            Assert.That(result.Trim(), Is.Empty);
        }

        [Test]
        public void DisplayTranslatedDestination_NegatedWithEmptyTos_ContainsNegatedText()
        {
            var rule = MakeNatRule(new NatData { TranslatedDestinationNegated = true, TranslatedTos = [] });

            var result = _display.DisplayTranslatedDestination(rule, OutputLocation.export);

            Assert.That(result, Does.Contain("not"));
        }

        [Test]
        public void DisplayTranslatedDestination_NotNegated_DoesNotContainNegatedText()
        {
            var rule = MakeNatRule(new NatData { TranslatedDestinationNegated = false, TranslatedTos = [] });

            var result = _display.DisplayTranslatedDestination(rule, OutputLocation.export);

            Assert.That(result, Does.Not.Contain("not"));
        }

        // ── DisplayTranslatedService ─────────────────────────────────────────

        [Test]
        public void DisplayTranslatedService_EmptyServices_ReturnsEmpty()
        {
            var rule = MakeNatRule(new NatData { TranslatedServices = [] });

            var result = _display.DisplayTranslatedService(rule, OutputLocation.export);

            Assert.That(result.Trim(), Is.Empty);
        }

        [Test]
        public void DisplayTranslatedService_NegatedWithEmptyServices_ContainsNegatedText()
        {
            var rule = MakeNatRule(new NatData { TranslatedServiceNegated = true, TranslatedServices = [] });

            var result = _display.DisplayTranslatedService(rule, OutputLocation.export);

            Assert.That(result, Does.Contain("not"));
        }

        [Test]
        public void DisplayTranslatedService_NotNegated_DoesNotContainNegatedText()
        {
            var rule = MakeNatRule(new NatData { TranslatedServiceNegated = false, TranslatedServices = [] });

            var result = _display.DisplayTranslatedService(rule, OutputLocation.export);

            Assert.That(result, Does.Not.Contain("not"));
        }

        [Test]
        public void DisplayTranslatedService_WithOneService_ContainsServiceName()
        {
            var rule = MakeNatRule(new NatData
            {
                TranslatedServices =
                [
                    new ServiceWrapper { Content = new NetworkService { Name = "HTTPS", DestinationPort = 443, Protocol = new() { Id = 6, Name = "TCP" } } }
                ]
            });

            var result = _display.DisplayTranslatedService(rule, OutputLocation.export);

            Assert.That(result, Does.Contain("HTTPS"));
        }
    }
}
