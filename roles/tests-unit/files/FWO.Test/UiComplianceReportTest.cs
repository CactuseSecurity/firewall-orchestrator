using AngleSharp.Dom;
using Bunit;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Report;
using FWO.Report.Data.ViewData;
using FWO.Ui.Pages.Reporting;
using FWO.Ui.Pages.Reporting.Reports;
using FWO.Ui.Services;
using FWO.Ui.Shared;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Reflection;

namespace FWO.Test
{
    [TestFixture]
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    internal class UiComplianceReportTest
    {
        private const string kSectionHeader = "Section A";

        private static readonly List<string> kAllColumns =
        [
            "MgmtId", "MgmtName", "Uid", "Name", "Source", "Source (Short)", "Destination", "Destination (short)", "Services", "Services (short)",
            "Action", "InstallOn", "Compliance", "ViolationDetails", "ChangeID", "AdoITID", "Comment", "LastModified", "ExpirationTime",
            "RulebaseId", "RulebaseName", "Enabled"
        ];

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

        private static RuleViewData BuildRule(string name, bool show = true, string? sectionHeader = null)
        {
            return new RuleViewData
            {
                Name = name,
                Uid = $"uid-{name}",
                Source = "<b>source</b>",
                Compliance = "violated",
                Show = show,
                DataObject = new Rule { Name = name, SectionHeader = sectionHeader }
            };
        }

        private IRenderedComponent<ComplianceReport> Render(BunitContext context, ReportData reportData, bool showAnyRule = false,
            List<RuleViewData>? selectedRules = null)
        {
            return context.Render<ComplianceReport>(parameters => parameters
                .Add(p => p.ReportData, reportData)
                .Add(p => p.ShowAnyRule, showAnyRule)
                .Add(p => p.SelectedRules, selectedRules ?? [])
                .Add(p => p.SelectedRulesChanged, (List<RuleViewData> _) => selectionChanges++));
        }

        private static ReportData Data(params RuleViewData[] rules)
        {
            return new ReportData { RuleViewData = rules };
        }

        private static List<string> ColumnTitles(IRenderedComponent<ComplianceReport> component)
        {
            return [.. component.FindAll("thead th span:not([aria-hidden])").Select(title => title.TextContent.Trim())];
        }

        private static List<string> ShownRuleNames(IRenderedComponent<ComplianceReport> component)
        {
            int nameColumn = ColumnTitles(component).IndexOf("Name") + 1;
            return [.. component.FindAll("tbody tr")
                .Where(row => row.QuerySelector("td.bg-light") == null)
                .Select(row => row.QuerySelectorAll("td")[nameColumn].TextContent.Trim())];
        }

        private static IElement RuleRow(IRenderedComponent<ComplianceReport> component, string ruleName)
        {
            return component.FindAll("tbody tr").First(row => row.QuerySelectorAll("td").Any(cell => cell.TextContent.Trim() == ruleName));
        }

        /// <summary>
        /// Gives the user config a global config, which the test user config does not have.
        /// </summary>
        private void SetGlobalConfig(GlobalConfig globalConfig)
        {
            FieldInfo field = typeof(UserConfig).GetField("globalConfig", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("globalConfig field not found");
            field.SetValue(userConfig, globalConfig);
        }

        [Test]
        public async Task TheReportShowsEveryColumnIncludingTheShortOnes()
        {
            await using BunitContext context = CreateContext();

            IRenderedComponent<ComplianceReport> component = Render(context, Data(BuildRule("first")));

            Assert.That(component.FindComponent<Collapse>().Instance.Title, Is.EqualTo("Compliance Report"));
            Assert.That(ColumnTitles(component), Is.EqualTo(kAllColumns));
            Assert.That(RuleRow(component, "first").InnerHtml, Does.Contain("<b>source</b>"));
        }

        [Test]
        public async Task TheShortColumnsCanBeSwitchedOff()
        {
            SetGlobalConfig(new SimulatedGlobalConfig { ShowShortColumnsInComplianceReports = false });
            await using BunitContext context = CreateContext();

            IRenderedComponent<ComplianceReport> component = Render(context, Data(BuildRule("first")));

            Assert.That(ColumnTitles(component), Is.EqualTo(kAllColumns.Where(title => !title.Contains("(short)", StringComparison.OrdinalIgnoreCase)).ToList()));
        }

        [Test]
        public async Task AFailedExistingViolationsFilterIsPointedOut()
        {
            await using BunitContext context = CreateContext();
            ReportData reportData = Data(BuildRule("first"));
            reportData.ExistingViolationsFilterFailed = true;

            IRenderedComponent<ComplianceReport> component = Render(context, reportData);

            Assert.That(component.Find("div.alert-warning").TextContent, Is.EqualTo(userConfig.GetText("existing_violations_filter_failed")));
        }

        [Test]
        public async Task ByDefaultOnlyTheRulesMarkedForDisplayAreShown()
        {
            await using BunitContext context = CreateContext();

            IRenderedComponent<ComplianceReport> component = Render(context, Data(BuildRule("violating"), BuildRule("any match", show: false)));

            Assert.That(component.FindAll("div.alert-warning"), Is.Empty);
            Assert.That(ShownRuleNames(component), Is.EqualTo((List<string>)["violating"]));
        }

        [Test]
        public async Task ShowingAnyRuleAlsoShowsTheRulesNotMarkedForDisplay()
        {
            await using BunitContext context = CreateContext();

            IRenderedComponent<ComplianceReport> component = Render(context, Data(BuildRule("violating"), BuildRule("any match", show: false)), showAnyRule: true);

            Assert.That(ShownRuleNames(component), Is.EquivalentTo((List<string>)["violating", "any match"]));
        }

        [Test]
        public async Task NewReportDataReplacesTheShownRules()
        {
            await using BunitContext context = CreateContext();
            IRenderedComponent<ComplianceReport> component = Render(context, Data(BuildRule("old")));

            await component.InvokeAsync(() => component.Instance.UpdateRuleViewData(Data(BuildRule("new"), BuildRule("hidden", show: false))));
            component.Render();

            Assert.That(ShownRuleNames(component), Is.EqualTo((List<string>)["new"]));
            Assert.That(component.Instance.ReportData.RuleViewData.Count(), Is.EqualTo(2));
        }

        [Test]
        public async Task RefreshingWithoutNewDataKeepsTheCurrentRules()
        {
            await using BunitContext context = CreateContext();
            ReportData reportData = Data(BuildRule("current"));
            IRenderedComponent<ComplianceReport> component = Render(context, reportData);

            await component.InvokeAsync(() => component.Instance.UpdateRuleViewData());
            component.Render();

            Assert.That(component.Instance.ReportData, Is.SameAs(reportData));
            Assert.That(ShownRuleNames(component), Is.EqualTo((List<string>)["current"]));
        }

        [Test]
        public async Task ClickingARuleSelectsItAndClickingItAgainDeselectsIt()
        {
            await using BunitContext context = CreateContext();
            RuleViewData rule = BuildRule("first");
            List<RuleViewData> selection = [];
            IRenderedComponent<ComplianceReport> component = Render(context, Data(rule), selectedRules: selection);

            RuleRow(component, "first").Click();
            List<RuleViewData> afterFirstClick = [.. selection];
            RuleRow(component, "first").Click();

            Assert.That(afterFirstClick, Is.EqualTo(new List<RuleViewData> { rule }));
            Assert.That(selection, Is.Empty);
            Assert.That(selectionChanges, Is.EqualTo(2));
        }

        [Test]
        public async Task ASectionHeaderIsShownAsARowOfItsOwn()
        {
            await using BunitContext context = CreateContext();

            IRenderedComponent<ComplianceReport> component = Render(context, Data(BuildRule("first"), BuildRule("header", sectionHeader: kSectionHeader)));

            Assert.That(component.Find("td.bg-light").TextContent.Trim(), Is.EqualTo(kSectionHeader));
        }

        [TestCase(null)]
        [TestCase("")]
        public async Task ARuleWithoutSectionHeaderShowsAllItsCells(string? sectionHeader)
        {
            await using BunitContext context = CreateContext();

            IRenderedComponent<ComplianceReport> component = Render(context, Data(BuildRule("first", sectionHeader: sectionHeader)));

            Assert.That(RuleRow(component, "first").ClassList, Does.Not.Contain("hide-all-but-second-child"));
        }

        [Test]
        public async Task ExpandingARuleShowsTheDetailsOfTheUnderlyingRule()
        {
            await using BunitContext context = CreateContext();
            RuleViewData rule = BuildRule("first");
            IRenderedComponent<ComplianceReport> component = Render(context, Data(rule));

            RuleRow(component, "first").QuerySelector("a[aria-expanded]")!.Click();

            Assert.That(component.FindComponent<RuleDetails>().Instance.Rule, Is.SameAs(rule.DataObject));
        }
    }
}
