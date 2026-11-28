using Bunit;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Ui.Pages.Reporting;
using FWO.Ui.Pages.Reporting.Reports;
using FWO.Ui.Services;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    internal class UiRuleBaseReportTest
    {
        private const string kLastModifiedTitle = "LastModified";
        private const string kSectionHeader = "Section A";
        private const int kOwnerId = 4;

        private readonly SimulatedUserConfig userConfig = new();
        private int selectionChanges;

        private BunitContext CreateContext()
        {
            BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddLocalization();
            context.Services.AddScoped<DomEventService>();
            context.Services.AddSingleton<UserConfig>(userConfig);
            return context;
        }

        private static Rule BuildRule(long id, string name)
        {
            return new Rule { Id = id, Name = name, SectionHeader = null, OwnerId = kOwnerId, Comment = "rule comment" };
        }

        private IRenderedComponent<RuleBaseReport> Render(BunitContext context, ReportType reportType, List<Rule> rules,
            bool recertification = false, bool readonlyMode = false, List<Rule>? selectedRules = null)
        {
            return context.Render<RuleBaseReport>(parameters => parameters
                .Add(p => p.Rules, rules)
                .Add(p => p.SelectedReportType, reportType)
                .Add(p => p.Recertification, recertification)
                .Add(p => p.ReadonlyMode, readonlyMode)
                .Add(p => p.SelectedRules, selectedRules ?? [])
                .Add(p => p.SelectedRulesChanged, (List<Rule> _) => selectionChanges++));
        }

        private static List<string> ColumnTitles(IRenderedComponent<RuleBaseReport> component)
        {
            return [.. component.FindAll("thead th span:not([aria-hidden])").Select(title => title.TextContent.Trim())];
        }

        private List<string> Titles(params string[] keys)
        {
            return [.. keys.Select(userConfig.GetText)];
        }

        [Test]
        public async Task ARulebaseShowsTheRulePropertiesAndWhenTheyChangedLast()
        {
            await using BunitContext context = CreateContext();

            IRenderedComponent<RuleBaseReport> component = Render(context, ReportType.AppRules, [BuildRule(1, "first rule")]);

            List<string> titles = ColumnTitles(component);
            Assert.That(titles, Is.SupersetOf(Titles("name", "source_zone", "source", "destination_zone", "destination", "services",
                "action", "track", "enabled", "enforcing_devices", "uid", "comment")));
            Assert.That(titles[^1], Is.EqualTo(kLastModifiedTitle));
            Assert.That(titles, Has.None.EqualTo(userConfig.GetText("next_recert_date")));
            Assert.That(component.Markup, Does.Contain("first rule"));
        }

        [Test]
        public async Task ARecertificationShowsTheRecertificationDataFirst()
        {
            await using BunitContext context = CreateContext();

            IRenderedComponent<RuleBaseReport> component = Render(context, ReportType.Recertification, [BuildRule(1, "first rule")]);

            List<string> titles = ColumnTitles(component);
            Assert.That(titles.Take(5), Is.EqualTo(Titles("next_recert_date", "owner", "ip_matches", "last_hit").Append(kLastModifiedTitle)));
            Assert.That(titles.Count(title => title == kLastModifiedTitle), Is.EqualTo(1));
            Assert.That(component.FindAll("input[type=radio]"), Is.Empty);
        }

        [Test]
        public async Task ANatRulebaseHasNoActionOrTrack()
        {
            await using BunitContext context = CreateContext();

            IRenderedComponent<RuleBaseReport> component = Render(context, ReportType.NatRules, [BuildRule(1, "first rule")]);

            List<string> titles = ColumnTitles(component);
            Assert.That(titles, Has.None.EqualTo(userConfig.GetText("action")));
            Assert.That(titles, Has.None.EqualTo(userConfig.GetText("track")));
        }

        [Test]
        public async Task RecertifyingOffersADecisionPerRuleThatDefaultsToLater()
        {
            await using BunitContext context = CreateContext();

            IRenderedComponent<RuleBaseReport> component = Render(context, ReportType.Recertification, [BuildRule(1, "first rule")], recertification: true);

            Assert.That(component.FindAll("input[type=radio]").Select(radio => radio.Id),
                Is.EqualTo((List<string>)["recertifyRadio1:4", "decertifyRadio1:4", "noneRadio1:4"]));
            Assert.That(component.Find("#noneRadio1\\:4").HasAttribute("checked"), Is.True);
        }

        [Test]
        public async Task ADecisionIsKeptOnTheRuleAndSelectsItOnce()
        {
            await using BunitContext context = CreateContext();
            Rule rule = BuildRule(1, "first rule");
            List<Rule> selection = [];
            IRenderedComponent<RuleBaseReport> component = Render(context, ReportType.Recertification, [rule], recertification: true, selectedRules: selection);

            component.Find("#recertifyRadio1\\:4").Change(true);
            (bool recert, bool remove) afterRecertify = (rule.Metadata.Recert, rule.Metadata.ToBeRemoved);
            component.Find("#decertifyRadio1\\:4").Change(true);
            (bool recert, bool remove) afterDecertify = (rule.Metadata.Recert, rule.Metadata.ToBeRemoved);
            component.Find("#noneRadio1\\:4").Change(true);

            Assert.That(afterRecertify, Is.EqualTo((true, false)));
            Assert.That(afterDecertify, Is.EqualTo((false, true)));
            Assert.That((rule.Metadata.Recert, rule.Metadata.ToBeRemoved), Is.EqualTo((false, false)));
            // the decision selects the rule the first time only, so later decisions do not deselect it again
            Assert.That(selection, Is.EqualTo(new List<Rule> { rule }));
            Assert.That(selectionChanges, Is.EqualTo(1));
        }

        [Test]
        public async Task AReadOnlyRecertificationOffersNoDecision()
        {
            await using BunitContext context = CreateContext();

            IRenderedComponent<RuleBaseReport> component = Render(context, ReportType.Recertification, [BuildRule(1, "first rule")],
                recertification: true, readonlyMode: true);

            Assert.That(component.FindAll("input[type=radio]"), Is.Empty);
        }

        [Test]
        public async Task ClickingARowSelectsItAndClickingItAgainDeselectsIt()
        {
            await using BunitContext context = CreateContext();
            Rule rule = BuildRule(1, "first rule");
            List<Rule> selection = [];
            IRenderedComponent<RuleBaseReport> component = Render(context, ReportType.AppRules, [rule], selectedRules: selection);

            component.Find("tbody tr").Click();
            List<Rule> afterFirstClick = [.. selection];
            component.Find("tbody tr").Click();

            Assert.That(afterFirstClick, Is.EqualTo(new List<Rule> { rule }));
            Assert.That(selection, Is.Empty);
            Assert.That(selectionChanges, Is.EqualTo(2));
        }

        [Test]
        public async Task ASectionHeaderIsShownAsARowOfItsOwn()
        {
            await using BunitContext context = CreateContext();
            Rule header = BuildRule(2, "header rule");
            header.SectionHeader = kSectionHeader;

            IRenderedComponent<RuleBaseReport> component = Render(context, ReportType.AppRules, [BuildRule(1, "first rule"), header]);

            Assert.That(component.Find("td.bg-light").TextContent.Trim(), Is.EqualTo(kSectionHeader));
        }

        [TestCase(null)]
        [TestCase("")]
        public async Task ARuleWithoutSectionHeaderShowsAllItsCells(string? sectionHeader)
        {
            await using BunitContext context = CreateContext();
            Rule rule = BuildRule(1, "first rule");
            rule.SectionHeader = sectionHeader;

            IRenderedComponent<RuleBaseReport> component = Render(context, ReportType.AppRules, [rule]);

            Assert.That(component.Find("tbody tr").ClassList, Does.Not.Contain("hide-all-but-second-child"));
        }

        [Test]
        public async Task ExpandingARowShowsTheRuleDetails()
        {
            await using BunitContext context = CreateContext();
            Rule rule = BuildRule(1, "first rule");
            IRenderedComponent<RuleBaseReport> component = Render(context, ReportType.AppRules, [rule]);

            component.Find("tbody tr a[aria-expanded]").Click();

            Assert.That(component.FindComponent<RuleDetails>().Instance.Rule, Is.SameAs(rule));
        }
    }
}
