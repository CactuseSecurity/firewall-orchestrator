using Bunit;
using FWO.Config.Api;
using FWO.Ui.Shared;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Tests the rules view select, which must not bind its bool value with @bind (empty select, failing change event).
    /// </summary>
    [TestFixture]
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    public class RulesViewSelectTest : BunitContext
    {
        [SetUp]
        public void SetUpServices()
        {
            Services.AddSingleton<UserConfig>(new SimulatedUserConfig());
        }

        [TestCase(false, "false")]
        [TestCase(true, "true")]
        public void Render_ShowsCurrentView(bool managementView, string expectedValue)
        {
            IRenderedComponent<RulesViewSelect> rendered = Render<RulesViewSelect>(parameters => parameters
                .Add(p => p.Id, "rulesView")
                .Add(p => p.ManagementRulebaseView, managementView));

            Assert.That(rendered.Find("select#rulesView").GetAttribute("value"), Is.EqualTo(expectedValue));
        }

        [TestCase(false, "true", true)]
        [TestCase(true, "false", false)]
        public void Change_ReportsSelectedView(bool initialView, string selectedValue, bool expectedView)
        {
            bool? reportedView = null;
            IRenderedComponent<RulesViewSelect> rendered = Render<RulesViewSelect>(parameters => parameters
                .Add(p => p.ManagementRulebaseView, initialView)
                .Add(p => p.ManagementRulebaseViewChanged, (bool view) => reportedView = view));

            rendered.Find("select").Change(selectedValue);

            Assert.That(reportedView, Is.EqualTo(expectedView));
            Assert.That(rendered.Instance.ManagementRulebaseView, Is.EqualTo(expectedView));
        }
    }
}
