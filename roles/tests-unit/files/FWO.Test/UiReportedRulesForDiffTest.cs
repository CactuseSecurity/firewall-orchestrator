using Bunit;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Ui.Pages.Reporting;
using FWO.Ui.Services;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    internal class UiReportedRulesForDiffTest
    {
        private readonly SimulatedUserConfig userConfig = new();

        private BunitContext CreateContext()
        {
            BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddLocalization();
            context.Services.AddScoped<DomEventService>();
            context.Services.AddSingleton<UserConfig>(userConfig);
            return context;
        }

        private static IRenderedComponent<ReportedRulesForDiff> Render(BunitContext context, params Rule[] implementedRules)
        {
            ModProdDifference difference = new()
            {
                ModelledConnection = new ModellingConnection { Id = 1, Name = "modelled connection" },
                ImplementedRules = [.. implementedRules]
            };
            return context.Render<ReportedRulesForDiff>(parameters => parameters.Add(p => p.Difference, difference));
        }

        private static Rule BuildRule(long id, string name)
        {
            return new Rule { Id = id, Name = name, ManagementName = "management", DeviceName = "gateway" };
        }

        private static List<string> ColumnTitles(IRenderedComponent<ReportedRulesForDiff> component)
        {
            return [.. component.FindAll("thead th span:not([aria-hidden])").Select(title => title.TextContent.Trim())];
        }

        [Test]
        public async Task TheImplementedRulesAreShownWithWhereTheyAreImplemented()
        {
            await using BunitContext context = CreateContext();

            IRenderedComponent<ReportedRulesForDiff> component = Render(context, BuildRule(1, "implementing rule"));

            List<string> titles = ColumnTitles(component);
            Assert.That(titles, Is.SupersetOf(new List<string> { "management", "gateway", "source_zone", "source", "services", "destination_zone", "destination" }
                .Select(userConfig.GetText)));
            Assert.That(component.Find("tbody tr").TextContent, Does.Contain("management").And.Contain("gateway"));
        }

        [Test]
        public async Task WithoutUnusedObjectsNoMissingObjectsColumnIsShown()
        {
            await using BunitContext context = CreateContext();

            IRenderedComponent<ReportedRulesForDiff> component = Render(context, BuildRule(1, "implementing rule"));

            Assert.That(ColumnTitles(component), Has.None.EqualTo(userConfig.GetText("missing_objects")));
        }

        [Test]
        public async Task UnusedObjectsOfAnyRuleAreListedAsMissing()
        {
            await using BunitContext context = CreateContext();
            Rule complete = BuildRule(1, "complete rule");
            Rule incomplete = BuildRule(2, "incomplete rule");
            incomplete.UnusedSpecialUserObjects = ["special user"];
            incomplete.UnusedUpdatableObjects = ["updatable object"];

            IRenderedComponent<ReportedRulesForDiff> component = Render(context, complete, incomplete);

            Assert.That(ColumnTitles(component), Does.Contain(userConfig.GetText("missing_objects")));
            Assert.That(component.FindAll("span.text-danger").Select(missing => missing.TextContent),
                Is.EqualTo((List<string>)["", "special user, updatable object"]));
        }

        [Test]
        public async Task OnlyUpdatableObjectsAlsoCountAsMissing()
        {
            await using BunitContext context = CreateContext();
            Rule incomplete = BuildRule(1, "incomplete rule");
            incomplete.UnusedUpdatableObjects = ["updatable object"];

            IRenderedComponent<ReportedRulesForDiff> component = Render(context, incomplete);

            Assert.That(component.Find("span.text-danger").TextContent, Is.EqualTo("updatable object"));
        }

        [Test]
        public async Task ExpandingARuleShowsItsExtendedDetails()
        {
            await using BunitContext context = CreateContext();
            Rule rule = BuildRule(1, "implementing rule");
            IRenderedComponent<ReportedRulesForDiff> component = Render(context, rule);

            component.Find("tbody tr a[aria-expanded]").Click();

            RuleDetails details = component.FindComponent<RuleDetails>().Instance;
            Assert.That(details.Rule, Is.SameAs(rule));
            Assert.That(details.Extended, Is.True);
        }
    }
}
