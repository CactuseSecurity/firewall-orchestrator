using Bunit;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data.Report;
using FWO.Ui.Shared;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Tests the start rulebase selection of the management rulebases view.
    /// </summary>
    [TestFixture]
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    public class RulebaseSelectionTest : BunitContext
    {
        private readonly SimulatedUserConfig userConfig = new();

        [SetUp]
        public void SetUpServices()
        {
            Services.AddSingleton<UserConfig>(userConfig);
        }

        /// <summary>
        /// The select all / clear all button is rendered via DisplayService.DisplayButton and therefore follows the iconify setting.
        /// </summary>
        [TestCase(true, false)]
        [TestCase(true, true)]
        [TestCase(false, false)]
        [TestCase(false, true)]
        public void Render_SelectAllButtonFollowsIconifySetting(bool iconify, bool allSelected)
        {
            userConfig.ModIconify = iconify;
            RulebaseManagementSelect management = new()
            {
                Id = 1,
                Name = "Mgmt",
                Rulebases = [new RulebaseSelect { Id = 11, Name = "Policy" }]
            };
            List<RulebaseManagementSelect> managements = [management];
            List<SelectedRulebase> selection = allSelected ? RulebaseSelectionHelper.SelectAll(managements) : [];

            IRenderedComponent<RulebaseSelection> rendered = Render<RulebaseSelection>(parameters => parameters
                .Add(p => p.Managements, managements)
                .Add(p => p.SelectedRulebases, selection));

            AngleSharp.Dom.IElement button = rendered.Find("div.btn-group button");
            string expectedIcon = allSelected ? Icons.ClearAll : Icons.SelectAll;
            string expectedText = allSelected ? "Clear All" : "Select All";
            if (iconify)
            {
                Assert.That(button.QuerySelector("span")?.ClassName, Is.EqualTo(expectedIcon));
                Assert.That(button.QuerySelector("span")?.GetAttribute("title"), Is.EqualTo(expectedText));
            }
            else
            {
                Assert.That(button.QuerySelector("span"), Is.Null);
                Assert.That(button.TextContent.Trim(), Is.EqualTo(expectedText));
            }
        }
    }
}
