using Bunit;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Ui.Pages.Reporting;
using FWO.Ui.Services;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    internal class UiReportedRulesTest
    {
        private const string kSectionHeader = "Section A";

        private readonly SimulatedUserConfig userConfig = new();
        private List<Rule>? reportedSelection;

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
            // rules read from the api carry no section header at all
            return new Rule { Id = id, Name = name, SectionHeader = null, ManagementName = "management", DeviceName = "gateway", Comment = "rule comment" };
        }

        private IRenderedComponent<ReportedRules> Render(BunitContext context, ReportType reportType, Rule[] rules,
            bool recertification = false, bool readonlyMode = false, bool handleAsVariance = false, List<Rule>? selectedRules = null)
        {
            return context.Render<ReportedRules>(parameters => parameters
                .Add(p => p.Rules, rules)
                .Add(p => p.SelectedReportType, reportType)
                .Add(p => p.Recertification, recertification)
                .Add(p => p.ReadonlyMode, readonlyMode)
                .Add(p => p.HandleAsVariance, handleAsVariance)
                .Add(p => p.SelectedRules, selectedRules ?? [])
                .Add(p => p.SelectedRulesChanged, (List<Rule> rules) => reportedSelection = rules));
        }

        private static List<string> ColumnTitles(IRenderedComponent<ReportedRules> component)
        {
            return [.. component.FindAll("thead th span:not([aria-hidden])").Select(title => title.TextContent.Trim())];
        }

        private List<string> Titles(params string[] keys)
        {
            return [.. keys.Select(userConfig.GetText)];
        }

        [Test]
        public async Task ARulesReportShowsNumberZonesActionAndComment()
        {
            await using BunitContext context = CreateContext();

            IRenderedComponent<ReportedRules> component = Render(context, ReportType.Rules, [BuildRule(1, "first rule")]);

            List<string> titles = ColumnTitles(component);
            Assert.That(titles, Is.SupersetOf(Titles("number", "name", "source_zone", "source", "destination_zone", "destination",
                "services", "action", "track", "enabled", "uid", "comment")));
            Assert.That(titles, Has.None.EqualTo(userConfig.GetText("management")));
            Assert.That(titles, Has.None.EqualTo(userConfig.GetText("last_hit")));
            Assert.That(titles, Has.None.EqualTo(userConfig.GetText("trans_source")));
            Assert.That(component.Markup, Does.Contain("first rule"));
        }

        [Test]
        public async Task AVarianceAnalysisShowsWhereTheRuleIsImplementedInsteadOfNumberAndZones()
        {
            await using BunitContext context = CreateContext();

            IRenderedComponent<ReportedRules> component = Render(context, ReportType.VarianceAnalysis, [BuildRule(1, "first rule")]);

            List<string> titles = ColumnTitles(component);
            Assert.That(titles, Is.SupersetOf(Titles("management", "gateway")));
            Assert.That(titles, Has.None.EqualTo(userConfig.GetText("number")));
            Assert.That(titles, Has.None.EqualTo(userConfig.GetText("source_zone")));
            Assert.That(titles, Has.None.EqualTo(userConfig.GetText("comment")));
        }

        [Test]
        public async Task AppRulesShownAsVarianceKeepTheirCommentButShowNoLastHit()
        {
            await using BunitContext context = CreateContext();

            IRenderedComponent<ReportedRules> component = Render(context, ReportType.AppRules, [BuildRule(1, "first rule")], handleAsVariance: true);

            List<string> titles = ColumnTitles(component);
            Assert.That(titles, Is.SupersetOf(Titles("management", "gateway", "comment")));
            Assert.That(titles, Has.None.EqualTo(userConfig.GetText("last_hit")));
            Assert.That(titles, Has.None.EqualTo(userConfig.GetText("destination_zone")));
        }

        [TestCase(ReportType.UnusedRules)]
        [TestCase(ReportType.AppRules)]
        public async Task UsageRelatedReportsShowTheLastHit(ReportType reportType)
        {
            await using BunitContext context = CreateContext();
            Rule rule = BuildRule(1, "first rule");
            rule.Metadata.LastHit = new DateTime(2026, 5, 6, 7, 8, 0, DateTimeKind.Utc);

            IRenderedComponent<ReportedRules> component = Render(context, reportType, [rule]);

            Assert.That(ColumnTitles(component), Does.Contain(userConfig.GetText("last_hit")));
            Assert.That(component.Markup, Does.Contain(FWO.Ui.Display.RuleDisplayHtml.DisplayLastHit(rule.Metadata)));
        }

        [Test]
        public async Task ARecertificationReportShowsTheRecertificationColumns()
        {
            await using BunitContext context = CreateContext();

            IRenderedComponent<ReportedRules> component = Render(context, ReportType.Recertification, [BuildRule(1, "first rule")]);

            List<string> titles = ColumnTitles(component);
            Assert.That(titles, Is.SupersetOf(Titles("next_recert_date", "owner", "ip_matches", "last_hit")));
            // without recertification there is nothing to decide
            Assert.That(component.FindAll("input[type=radio]"), Is.Empty);
        }

        [Test]
        public async Task RecertifyingOffersADecisionPerRuleThatDefaultsToLater()
        {
            await using BunitContext context = CreateContext();
            Rule rule = BuildRule(1, "first rule");
            rule.OwnerId = 4;

            IRenderedComponent<ReportedRules> component = Render(context, ReportType.Recertification, [rule], recertification: true);

            Assert.That(component.FindAll("input[type=radio]").Select(radio => radio.Id),
                Is.EqualTo((List<string>)["recertifyRadio1:4", "decertifyRadio1:4", "noneRadio1:4"]));
            Assert.That(component.Find("#noneRadio1\\:4").HasAttribute("checked"), Is.True);
            Assert.That(rule.Metadata.Recert, Is.False);
            Assert.That(rule.Metadata.ToBeRemoved, Is.False);
        }

        [Test]
        public async Task TheRecertificationDecisionIsKeptOnTheRule()
        {
            await using BunitContext context = CreateContext();
            Rule rule = BuildRule(1, "first rule");
            rule.OwnerId = 4;
            IRenderedComponent<ReportedRules> component = Render(context, ReportType.Recertification, [rule], recertification: true);

            component.Find("#recertifyRadio1\\:4").Change(true);
            (bool recert, bool remove) afterRecertify = (rule.Metadata.Recert, rule.Metadata.ToBeRemoved);
            component.Find("#decertifyRadio1\\:4").Change(true);
            (bool recert, bool remove) afterDecertify = (rule.Metadata.Recert, rule.Metadata.ToBeRemoved);
            component.Find("#noneRadio1\\:4").Change(true);

            Assert.That(afterRecertify, Is.EqualTo((true, false)));
            Assert.That(afterDecertify, Is.EqualTo((false, true)));
            Assert.That((rule.Metadata.Recert, rule.Metadata.ToBeRemoved), Is.EqualTo((false, false)));
        }

        [Test]
        public async Task AReadOnlyRecertificationOffersNoDecision()
        {
            await using BunitContext context = CreateContext();

            IRenderedComponent<ReportedRules> component = Render(context, ReportType.Recertification, [BuildRule(1, "first rule")],
                recertification: true, readonlyMode: true);

            Assert.That(component.FindAll("input[type=radio]"), Is.Empty);
        }

        [Test]
        public async Task ANatRulesReportShowsTheTranslationInsteadOfActionAndTrack()
        {
            await using BunitContext context = CreateContext();

            IRenderedComponent<ReportedRules> component = Render(context, ReportType.NatRules, [BuildRule(1, "first rule")]);

            List<string> titles = ColumnTitles(component);
            Assert.That(titles, Is.SupersetOf(Titles("trans_source", "trans_destination", "trans_services")));
            Assert.That(titles, Has.None.EqualTo(userConfig.GetText("track")));
        }

        [Test]
        public async Task ClickingARowSelectsItAndClickingItAgainDeselectsIt()
        {
            await using BunitContext context = CreateContext();
            Rule rule = BuildRule(1, "first rule");
            List<Rule> selection = [];
            IRenderedComponent<ReportedRules> component = Render(context, ReportType.Rules, [rule], selectedRules: selection);

            component.Find("tbody tr").Click();
            List<long> selectedAfterFirstClick = [.. reportedSelection!.Select(selected => selected.Id)];
            component.Find("tbody tr").Click();

            Assert.That(selectedAfterFirstClick, Is.EqualTo((List<long>)[1]));
            Assert.That(reportedSelection, Is.Empty);
        }

        [Test]
        public async Task ASectionHeaderIsShownAsARowOfItsOwn()
        {
            await using BunitContext context = CreateContext();
            Rule header = BuildRule(2, "header rule");
            header.SectionHeader = kSectionHeader;

            IRenderedComponent<ReportedRules> component = Render(context, ReportType.Rules, [BuildRule(1, "first rule"), header]);

            AngleSharp.Dom.IElement headerCell = component.Find("td.bg-light");
            Assert.That(headerCell.TextContent.Trim(), Is.EqualTo(kSectionHeader));
            Assert.That(int.Parse(headerCell.GetAttribute("colspan")!), Is.GreaterThan(ColumnTitles(component).Count));
        }

        [TestCase(null)]
        [TestCase("")]
        public async Task ARuleWithoutSectionHeaderShowsAllItsCells(string? sectionHeader)
        {
            // a rule read by a query that does not select the header keeps the empty default; hiding all but
            // one of its cells as if it were a section header would leave a blank row in the report
            await using BunitContext context = CreateContext();
            Rule rule = BuildRule(1, "first rule");
            rule.SectionHeader = sectionHeader;

            IRenderedComponent<ReportedRules> component = Render(context, ReportType.Rules, [rule]);

            Assert.That(component.Find("tbody tr").ClassList, Does.Not.Contain("hide-all-but-second-child"));
            Assert.That(component.FindAll("td.bg-light"), Is.Empty);
        }

        [Test]
        public async Task ExpandingARowShowsTheRuleDetails()
        {
            await using BunitContext context = CreateContext();
            Rule rule = BuildRule(1, "first rule");
            rule.Metadata.CreatedImport = new ImportControl { StartTime = new DateTime(2026, 1, 2, 3, 4, 5) };
            IRenderedComponent<ReportedRules> component = Render(context, ReportType.Rules, [rule]);
            Assert.That(component.FindComponents<RuleDetails>(), Is.Empty);

            component.Find("tbody tr a[aria-expanded]").Click();

            RuleDetails details = component.FindComponent<RuleDetails>().Instance;
            Assert.That(details.Rule, Is.SameAs(rule));
            Assert.That(details.Extended, Is.False);
        }

        [Test]
        public async Task NoRulesShowAnEmptyTable()
        {
            await using BunitContext context = CreateContext();

            IRenderedComponent<ReportedRules> component = Render(context, ReportType.Rules, []);

            Assert.That(component.FindAll("tbody tr td.bg-light"), Is.Empty);
            Assert.That(component.Markup, Does.Not.Contain("first rule"));
        }
    }
}
