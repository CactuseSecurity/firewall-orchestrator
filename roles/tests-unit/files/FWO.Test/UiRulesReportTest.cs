using AngleSharp.Dom;
using Bunit;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Report;
using FWO.Report;
using FWO.Services.RuleTreeBuilder;
using FWO.Ui.Pages.Reporting;
using FWO.Ui.Pages.Reporting.Reports;
using FWO.Ui.Services;
using FWO.Ui.Shared;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    internal class UiRulesReportTest
    {
        private const int kManagementId = 1;
        private const int kGatewayId = 11;
        private const int kOtherGatewayId = 12;
        private const int kLayerId = 21;
        private const string kLayerName = "Layer-1";
        private const string kScrollFunction = "scrollToElementAndHighlight";
        private const string kLastModifiedTitle = "LastModified";

        private readonly SimulatedUserConfig userConfig = new();
        private readonly RuleTreeBuilder ruleTreeBuilder = new();
        private int selectionChanges;

        private BunitContext CreateContext()
        {
            BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddLocalization();
            context.Services.AddScoped<DomEventService>();
            context.Services.AddSingleton<UserConfig>(userConfig);
            context.Services.AddSingleton<IRuleTreeBuilder>(ruleTreeBuilder);
            return context;
        }

        private static RulebaseReport BuildRulebase(int id, string name, params long[] ruleIds)
        {
            return new RulebaseReport
            {
                Id = id,
                Name = name,
                Rules = [.. ruleIds.Select(ruleId => new Rule { Id = ruleId, RulebaseId = id, Name = $"R-{ruleId}", SectionHeader = null })]
            };
        }

        private static RulebaseLink InitialLink(int gatewayId, int rulebaseId)
        {
            return new RulebaseLink { GatewayId = gatewayId, NextRulebaseId = rulebaseId, LinkType = 2, IsInitial = true };
        }

        /// <summary>
        /// One management with one gateway whose ordered layer holds the given rules, with the rule tree
        /// already built as the report generation would do it.
        /// </summary>
        private ManagementReport BuildManagementWithRuleTree(params long[] ruleIds)
        {
            RulebaseReport layer = BuildRulebase(kLayerId, kLayerName, ruleIds);
            RulebaseLink[] links = [InitialLink(kGatewayId, kLayerId)];
            DeviceReport gateway = new() { Id = kGatewayId, Name = "gateway", RulebaseLinks = links };
            ruleTreeBuilder.BuildRuleTree([layer], links, kManagementId, kGatewayId);
            return new ManagementReport { Id = kManagementId, Name = "management", Devices = [gateway], Rulebases = [layer] };
        }

        private IRenderedComponent<RulesReport> Render(BunitContext context, ReportType reportType, List<ManagementReport> managements,
            bool recertification = false, List<Rule>? selectedRules = null)
        {
            return context.Render<RulesReport>(parameters => parameters
                .Add(p => p.Managements, managements)
                .Add(p => p.SelectedReportType, reportType)
                .Add(p => p.Recertification, recertification)
                .Add(p => p.SelectedRules, selectedRules ?? [])
                .Add(p => p.SelectedRulesChanged, (List<Rule> _) => selectionChanges++));
        }

        private static List<string> ColumnTitles(IRenderedComponent<RulesReport> component)
        {
            return [.. component.FindAll("thead th span:not([aria-hidden])").Select(title => title.TextContent.Trim())];
        }

        private static List<string> ShownRuleNames(IRenderedComponent<RulesReport> component)
        {
            return [.. component.FindAll("tbody tr td").Select(cell => cell.TextContent.Trim()).Where(text => text.StartsWith("R-"))];
        }

        private static IElement LayerHeaderToggle(IRenderedComponent<RulesReport> component)
        {
            return component.FindAll("tbody tr").First(row => row.TextContent.Contains(kLayerName)).QuerySelector("div[blazor\\:onclick]")!;
        }

        [Test]
        public async Task ManagementsAndGatewaysWithoutRulesAreLeftOut()
        {
            await using BunitContext context = CreateContext();
            ManagementReport withRules = BuildManagementWithRuleTree(1);
            withRules.Devices = [.. withRules.Devices, new DeviceReport { Id = kOtherGatewayId, Name = "empty gateway", RulebaseLinks = [] }];
            ManagementReport withoutRules = new() { Id = 2, Name = "empty management", Devices = [new DeviceReport { Id = 31, RulebaseLinks = [] }] };

            IRenderedComponent<RulesReport> component = Render(context, ReportType.Rules, [withRules, withoutRules]);

            Assert.That(component.FindComponents<Collapse>().Select(collapse => collapse.Instance.Title), Is.EqualTo((List<string>)["management", "gateway"]));
            Assert.That(component.Find($"#{DeviceReportExtensions.GetRuleTreeAnchorId(kManagementId, kGatewayId)}"), Is.Not.Null);
        }

        [Test]
        public async Task ARulesReportListsTheRuleTreeOfEachGateway()
        {
            await using BunitContext context = CreateContext();

            IRenderedComponent<RulesReport> component = Render(context, ReportType.Rules, [BuildManagementWithRuleTree(1, 2)]);

            Assert.That(ShownRuleNames(component), Is.EqualTo((List<string>)["R-1", "R-2"]));
            Assert.That(component.FindAll("td.bg-light").Select(cell => cell.TextContent.Trim()), Is.EqualTo((List<string>)[kLayerName]));
        }

        [Test]
        public async Task ARulesReportShowsTheLastHitAtTheEnd()
        {
            await using BunitContext context = CreateContext();

            IRenderedComponent<RulesReport> component = Render(context, ReportType.Rules, [BuildManagementWithRuleTree(1)]);

            List<string> titles = ColumnTitles(component);
            Assert.That(titles[0], Is.EqualTo(userConfig.GetText("number")));
            Assert.That(titles, Is.SupersetOf(new List<string> { "name", "source_zone", "source", "destination_zone", "destination", "services",
                "action", "track", "enabled", "enforcing_devices", "uid", "comment" }.Select(userConfig.GetText)));
            Assert.That(titles.TakeLast(2), Is.EqualTo((List<string>)[kLastModifiedTitle, userConfig.GetText("last_hit")]));
        }

        [Test]
        public async Task AnUnusedRulesReportShowsTheLastHitOnceRightAfterTheNumber()
        {
            await using BunitContext context = CreateContext();

            IRenderedComponent<RulesReport> component = Render(context, ReportType.UnusedRules, [BuildManagementWithRuleTree(1)]);

            List<string> titles = ColumnTitles(component);
            Assert.That(titles.Take(3), Is.EqualTo((List<string>)[userConfig.GetText("number"), userConfig.GetText("last_hit"), kLastModifiedTitle]));
            Assert.That(titles.Count(title => title == userConfig.GetText("last_hit")), Is.EqualTo(1));
            Assert.That(titles.Count(title => title == kLastModifiedTitle), Is.EqualTo(1));
        }

        [TestCase(ReportType.ComplianceReport)]
        [TestCase(ReportType.ComplianceDiffReport)]
        public async Task AComplianceReportShowsComplianceAndViolations(ReportType reportType)
        {
            await using BunitContext context = CreateContext();

            IRenderedComponent<RulesReport> component = Render(context, reportType, [BuildManagementWithRuleTree(1)]);

            Assert.That(ColumnTitles(component), Is.SupersetOf((List<string>)["Compliance", "Violation Details"]));
            Assert.That(ColumnTitles(component), Has.None.EqualTo(userConfig.GetText("last_hit")));
        }

        [Test]
        public async Task ANatRulesReportShowsTheTranslationInsteadOfActionAndTrack()
        {
            await using BunitContext context = CreateContext();

            IRenderedComponent<RulesReport> component = Render(context, ReportType.NatRules, [BuildManagementWithRuleTree(1)]);

            List<string> titles = ColumnTitles(component);
            Assert.That(titles, Is.SupersetOf(new List<string> { "trans_source", "trans_destination", "trans_services" }.Select(userConfig.GetText)));
            Assert.That(titles, Has.None.EqualTo(userConfig.GetText("action")));
            Assert.That(titles, Has.None.EqualTo(userConfig.GetText("track")));
        }

        [Test]
        public async Task CollapsingAllHidesTheRulesAndExpandingAllShowsThemAgain()
        {
            await using BunitContext context = CreateContext();
            IRenderedComponent<RulesReport> component = Render(context, ReportType.Rules, [BuildManagementWithRuleTree(1, 2)]);

            component.Find("button[name=btnCollapseAll]").Click();
            List<string> collapsed = ShownRuleNames(component);
            string collapsedArrow = LayerHeaderToggle(component).InnerHtml;
            component.Find("button[name=btnExpandAll]").Click();

            Assert.That(collapsed, Is.Empty);
            Assert.That(collapsedArrow, Does.StartWith(System.Net.WebUtility.HtmlDecode(Icons.HtmlArrowCollapsed)));
            Assert.That(ShownRuleNames(component), Is.EqualTo((List<string>)["R-1", "R-2"]));
            Assert.That(LayerHeaderToggle(component).InnerHtml, Does.StartWith(System.Net.WebUtility.HtmlDecode(Icons.HtmlArrowExpanded)));
        }

        [Test]
        public async Task ClickingTheLayerHeaderTogglesItsRules()
        {
            await using BunitContext context = CreateContext();
            IRenderedComponent<RulesReport> component = Render(context, ReportType.Rules, [BuildManagementWithRuleTree(1, 2)]);
            component.Find("button[name=btnExpandAll]").Click();

            LayerHeaderToggle(component).Click();
            List<string> afterFirstClick = ShownRuleNames(component);
            LayerHeaderToggle(component).Click();

            Assert.That(afterFirstClick, Is.Empty);
            Assert.That(ShownRuleNames(component), Is.EqualTo((List<string>)["R-1", "R-2"]));
        }

        [Test]
        public async Task ARuleWithoutChildrenHasNoVisibleToggleAndClickingItsNumberChangesNothing()
        {
            await using BunitContext context = CreateContext();
            IRenderedComponent<RulesReport> component = Render(context, ReportType.Rules, [BuildManagementWithRuleTree(1)]);
            component.Find("button[name=btnExpandAll]").Click();

            IElement numberToggle = component.FindAll("tbody tr span[blazor\\:onclick]")[0];
            Assert.That(numberToggle.InnerHtml, Does.Contain("visibility:hidden"));
            numberToggle.Click();

            Assert.That(ShownRuleNames(component), Is.EqualTo((List<string>)["R-1"]));
        }

        [Test]
        public async Task ClickingARuleSelectsItForItsGatewayAndClickingItAgainDeselectsIt()
        {
            await using BunitContext context = CreateContext();
            List<Rule> selection = [];
            IRenderedComponent<RulesReport> component = Render(context, ReportType.Rules, [BuildManagementWithRuleTree(1)], selectedRules: selection);

            RuleRow(component, "R-1").Click();
            List<Rule> afterFirstClick = [.. selection];
            RuleRow(component, "R-1").Click();

            Assert.That(afterFirstClick.Select(rule => (rule.Name, rule.DeviceName)), Is.EqualTo(new List<(string, string)> { ("R-1", "gateway") }));
            Assert.That(selection, Is.Empty);
            Assert.That(selectionChanges, Is.EqualTo(2));
        }

        [Test]
        public async Task ExpandingARuleShowsItsDetails()
        {
            await using BunitContext context = CreateContext();
            IRenderedComponent<RulesReport> component = Render(context, ReportType.Rules, [BuildManagementWithRuleTree(1)]);

            RuleRow(component, "R-1").QuerySelector("a[aria-expanded]")!.Click();

            Assert.That(component.FindComponent<RuleDetails>().Instance.Rule.Name, Is.EqualTo("R-1"));
        }

        [TestCase(null)]
        [TestCase("")]
        public async Task ARuleWithoutSectionHeaderShowsAllItsCells(string? sectionHeader)
        {
            await using BunitContext context = CreateContext();
            ManagementReport management = BuildManagementWithRuleTree(1);
            ruleTreeBuilder.RuleTreeCache[(kManagementId, kGatewayId)].ElementsFlat.Single(item => item.IsRule).Data.SectionHeader = sectionHeader;

            IRenderedComponent<RulesReport> component = Render(context, ReportType.Rules, [management]);

            Assert.That(RuleRow(component, "R-1").ClassList, Does.Not.Contain("hide-all-but-second-child"));
        }

        [Test]
        public async Task AGatewayWhoseRuleTreeIsMissingShowsAnEmptyTable()
        {
            await using BunitContext context = CreateContext();
            ManagementReport management = BuildManagementWithRuleTree(1);
            ruleTreeBuilder.ClearCachedRuleTrees();

            IRenderedComponent<RulesReport> component = Render(context, ReportType.Rules, [management]);

            Assert.That(ShownRuleNames(component), Is.Empty);
            Assert.That(component.FindComponents<Collapse>().Select(collapse => collapse.Instance.Title), Does.Contain("gateway"));
        }

        [Test]
        public async Task ARulebaseListedAboveLinksToItsRuleTree()
        {
            await using BunitContext context = CreateContext();
            ManagementReport management = BuildManagementWithRuleTree(1);
            RulebaseLink[] referencingLinks = [InitialLink(kOtherGatewayId, kLayerId)];
            DeviceReport referencingGateway = new()
            {
                Id = kOtherGatewayId,
                Name = "referencing gateway",
                RulebaseLinks = referencingLinks,
                ReferencedRulebaseTreeIds = new() { [kLayerId] = kGatewayId }
            };
            ruleTreeBuilder.BuildRuleTree(management.Rulebases, referencingLinks, kManagementId, kOtherGatewayId,
                referencedRulebaseIds: new HashSet<int> { kLayerId });
            management.Devices = [.. management.Devices, referencingGateway];
            IRenderedComponent<RulesReport> component = Render(context, ReportType.Rules, [management]);

            IElement link = component.Find("td.bg-light button.btn-link");
            Assert.That(link.TextContent.Trim(), Is.EqualTo("gateway"));
            Assert.That(link.ParentElement!.TextContent, Does.Contain(userConfig.GetText("rulebase_listed_above")));
            link.Click();

            JSRuntimeInvocation scroll = context.JSInterop.VerifyInvoke(kScrollFunction);
            Assert.That(scroll.Arguments[0], Is.EqualTo(DeviceReportExtensions.GetRuleTreeAnchorId(kManagementId, kGatewayId)));
        }

        [Test]
        public async Task AReferenceToAnUnknownRuleTreeNamesNoTree()
        {
            await using BunitContext context = CreateContext();
            RulebaseReport layer = BuildRulebase(kLayerId, kLayerName, 1);
            RulebaseLink[] links = [InitialLink(kOtherGatewayId, kLayerId)];
            ruleTreeBuilder.BuildRuleTree([layer], links, kManagementId, kOtherGatewayId, referencedRulebaseIds: new HashSet<int> { kLayerId });
            ManagementReport management = new()
            {
                Id = kManagementId,
                Name = "management",
                Rulebases = [layer],
                Devices = [new DeviceReport { Id = kOtherGatewayId, Name = "referencing gateway", RulebaseLinks = links }]
            };

            IRenderedComponent<RulesReport> component = Render(context, ReportType.Rules, [management]);

            Assert.That(component.Find("td.bg-light button.btn-link").TextContent.Trim(), Is.Empty);
        }

        [TestCase(ReportType.AppRules, false)]
        [TestCase(ReportType.Recertification, false)]
        [TestCase(ReportType.Rules, true)]
        public async Task RulebaseReportsAndRecertificationsListTheRulebasesInsteadOfTheGateways(ReportType reportType, bool recertification)
        {
            await using BunitContext context = CreateContext();
            ManagementReport management = BuildManagementWithRuleTree(1, 2);
            management.Rulebases = [.. management.Rulebases, BuildRulebase(22, "empty rulebase"), BuildRulebase(23, "second rulebase", 3)];

            IRenderedComponent<RulesReport> component = Render(context, reportType, [management], recertification: recertification);

            List<RuleBaseReport> rulebaseReports = [.. component.FindComponents<RuleBaseReport>().Select(report => report.Instance)];
            Assert.That(component.FindComponents<Collapse>().Select(collapse => collapse.Instance.Title),
                Is.EqualTo((List<string>)["management", kLayerName, "second rulebase"]));
            Assert.That(rulebaseReports.Select(report => report.Rules.Count), Is.EqualTo((List<int>)[2, 1]));
            Assert.That(rulebaseReports.All(report => report.Recertification == recertification && report.SelectedReportType == reportType), Is.True);
            Assert.That(component.FindAll("button[name=btnCollapseAll]"), Is.Empty);
        }

        private static IElement RuleRow(IRenderedComponent<RulesReport> component, string ruleName)
        {
            return component.FindAll("tbody tr").First(row => row.QuerySelectorAll("td").Any(cell => cell.TextContent.Trim() == ruleName));
        }
    }
}
