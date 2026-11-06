using Bunit;
using FWO.Config.Api;
using FWO.Ui.Services;
using FWO.Ui.Shared;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Reflection;

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
        }

        [TestCase(false, true)]
        [TestCase(true, false)]
        public async Task Change_ReportsSelectedView(bool initialView, bool expectedView)
        {
            bool? reportedView = null;
            IRenderedComponent<RulesViewSelect> rendered = Render<RulesViewSelect>(parameters => parameters
                .Add(p => p.ManagementRulebaseView, initialView)
                .Add(p => p.ManagementRulebaseViewChanged, (bool view) => reportedView = view));

            MethodInfo onChange = typeof(RulesViewSelect).GetMethod("OnChange", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMethodException(typeof(RulesViewSelect).FullName, "OnChange");
            object?[] arguments = [expectedView];
            await rendered.InvokeAsync(() => (Task)onChange.Invoke(rendered.Instance, arguments)!);

            Assert.That(reportedView, Is.EqualTo(expectedView));
            Assert.That(rendered.Instance.ManagementRulebaseView, Is.EqualTo(expectedView));
        }
    }
}
