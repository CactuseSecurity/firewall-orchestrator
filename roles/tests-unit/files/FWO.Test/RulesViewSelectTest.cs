using Bunit;
using FWO.Config.Api;
using FWO.Ui.Services;
using FWO.Ui.Shared;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Tests the rules view selector and its bool binding.
    /// </summary>
    [TestFixture]
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    public class RulesViewSelectTest : BunitContext
    {
        [SetUp]
        public void SetUpServices()
        {
            JSInterop.Mode = JSRuntimeMode.Loose;
            Services.AddSingleton<DomEventService>();
            Services.AddSingleton<UserConfig>(new SimulatedUserConfig());
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Render_ShowsCurrentView(bool managementView)
        {
            UserConfig userConfig = Services.GetRequiredService<UserConfig>();
            IRenderedComponent<RulesViewSelect> rendered = Render<RulesViewSelect>(parameters => parameters
                .Add(p => p.Id, "rulesView")
                .Add(p => p.ManagementRulebaseView, managementView));

            string expectedText = userConfig.GetText(managementView ? "management_rulebases_view" : "gateway_rules_view");
            Assert.Multiple(() =>
            {
                Assert.That(rendered.FindComponent<Dropdown<bool>>(), Is.Not.Null);
                Assert.That(rendered.Find("input#dropdown-input-rulesView").GetAttribute("value"), Is.EqualTo(expectedText));
            });
            rendered.FindComponent<Dropdown<bool>>().Find("input").Focus();
            Assert.That(rendered.FindAll("button.dropdown-item"), Is.Not.Empty);
        }

        [Test]
        public void Render_WithoutId_UsesUniqueGeneratedDropdownId()
        {
            IRenderedComponent<RulesViewSelect> first = Render<RulesViewSelect>();
            IRenderedComponent<RulesViewSelect> second = Render<RulesViewSelect>();

            string? firstId = first.Find("input").Id;
            string? secondId = second.Find("input").Id;

            Assert.Multiple(() =>
            {
                Assert.That(firstId, Does.StartWith("dropdown-input-"));
                Assert.That(firstId, Is.Not.EqualTo("dropdown-input-"));
                Assert.That(secondId, Does.StartWith("dropdown-input-"));
                Assert.That(secondId, Is.Not.EqualTo("dropdown-input-"));
                Assert.That(secondId, Is.Not.EqualTo(firstId));
            });
        }

        [TestCase(false, true)]
        [TestCase(true, false)]
        public void Change_ReportsSelectedView(bool initialView, bool expectedView)
        {
            bool? reportedView = null;
            UserConfig userConfig = Services.GetRequiredService<UserConfig>();
            IRenderedComponent<RulesViewSelect> rendered = Render<RulesViewSelect>(parameters => parameters
                .Add(p => p.ManagementRulebaseView, initialView)
                .Add(p => p.ManagementRulebaseViewChanged, (bool view) => reportedView = view));

            string expectedText = userConfig.GetText(expectedView ? "management_rulebases_view" : "gateway_rules_view");
            rendered.FindComponent<Dropdown<bool>>().Find("input").Focus();
            rendered.FindAll("button.dropdown-item")
                .Single(button => button.TextContent.Trim() == expectedText)
                .Click();

            Assert.That(reportedView, Is.EqualTo(expectedView));
            Assert.That(rendered.Instance.ManagementRulebaseView, Is.EqualTo(expectedView));
        }

        [Test]
        public void Change_DoesNotNotifyWhenViewIsUnchanged()
        {
            int notificationCount = 0;
            UserConfig userConfig = Services.GetRequiredService<UserConfig>();
            IRenderedComponent<RulesViewSelect> rendered = Render<RulesViewSelect>(parameters => parameters
                .Add(p => p.ManagementRulebaseView, true)
                .Add(p => p.ManagementRulebaseViewChanged, (bool _) => notificationCount++));

            string managementText = userConfig.GetText("management_rulebases_view");
            rendered.FindComponent<Dropdown<bool>>().Find("input").Focus();
            rendered.FindAll("button.dropdown-item")
                .Single(button => button.TextContent.Trim() == managementText)
                .Click();

            Assert.That(notificationCount, Is.Zero);
        }
    }
}
