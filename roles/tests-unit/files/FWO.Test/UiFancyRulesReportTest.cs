using AngleSharp.Dom;
using Bunit;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Report;
using FWO.Report;
using FWO.Services.RuleTreeBuilder;
using FWO.Ui.Pages.Reporting.Reports;
using FWO.Ui.Services;
using FWO.Ui.Shared;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    internal class UiFancyRulesReportTest
    {
        private const int kManagementId = 1;
        private const int kGatewayId = 11;
        private const int kOtherGatewayId = 12;
        private const int kLayerId = 21;
        private const int kInlineLayerId = 22;
        private const string kLayerName = "Layer-1";
        private const string kScrollFunction = "scrollToElementAndHighlight";

        private static readonly List<string> kRulesReportColumns =
        [
            "number", "name", "source_zone", "source", "destination_zone", "destination", "services", "action", "track",
            "enabled", "enforcing_devices", "uid", "comment", "last_modified", "last_hit"
        ];

        private readonly SimulatedUserConfig userConfig = new();
        private readonly RuleTreeBuilder ruleTreeBuilder = new();

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
        /// One management with one gateway whose ordered layer holds the given rules, with the rule tree built.
        /// </summary>
        private ManagementReport BuildManagementWithRuleTree(params long[] ruleIds)
        {
            RulebaseReport layer = BuildRulebase(kLayerId, kLayerName, ruleIds);
            RulebaseLink[] links = [InitialLink(kGatewayId, kLayerId)];
            ruleTreeBuilder.BuildRuleTree([layer], links, kManagementId, kGatewayId);
            return new ManagementReport
            {
                Id = kManagementId,
                Name = "management",
                Rulebases = [layer],
                Devices = [new DeviceReport { Id = kGatewayId, Name = "gateway", RulebaseLinks = links }]
            };
        }

        private static IRenderedComponent<FancyRulesReport> Render(BunitContext context, ReportType reportType, List<ManagementReport> managements,
            bool recertification = false)
        {
            return context.Render<FancyRulesReport>(parameters => parameters
                .Add(p => p.Managements, managements)
                .Add(p => p.SelectedReportType, reportType)
                .Add(p => p.Recertification, recertification));
        }

        private static List<string> ColumnKeys(IRenderedComponent<FancyRulesReport> component)
        {
            return [.. component.FindAll("thead th[data-column-key]").Select(header => header.GetAttribute("data-column-key")!)];
        }

        private static List<string> ShownRuleNames(IRenderedComponent<FancyRulesReport> component)
        {
            return [.. component.FindAll("tbody td").Select(cell => cell.TextContent.Trim()).Where(text => text.StartsWith("R-"))];
        }

        private static IElement LayerHeaderToggle(IRenderedComponent<FancyRulesReport> component)
        {
            return component.Find("tbody tr.table-warning button");
        }

        [Test]
        public async Task ManagementsAndGatewaysWithoutRulesAreLeftOut()
        {
            await using BunitContext context = CreateContext();
            ManagementReport withRules = BuildManagementWithRuleTree(1);
            withRules.Devices = [.. withRules.Devices, new DeviceReport { Id = kOtherGatewayId, Name = "empty gateway", RulebaseLinks = [] }];
            ManagementReport withoutRules = new() { Id = 2, Name = "empty management", Devices = [new DeviceReport { Id = 31, RulebaseLinks = [] }] };

            IRenderedComponent<FancyRulesReport> component = Render(context, ReportType.Rules, [withRules, withoutRules]);

            Assert.That(component.FindComponents<Collapse>().Select(collapse => collapse.Instance.Title), Is.EqualTo((List<string>)["management", "gateway"]));
            Assert.That(component.Find($"#{DeviceReportExtensions.GetRuleTreeAnchorId(kManagementId, kGatewayId)}"), Is.Not.Null);
        }

        [Test]
        public async Task ARulesReportShowsTheRulePropertiesWithTheLastHitAtTheEnd()
        {
            await using BunitContext context = CreateContext();

            IRenderedComponent<FancyRulesReport> component = Render(context, ReportType.Rules, [BuildManagementWithRuleTree(1)]);

            Assert.That(ColumnKeys(component), Is.EqualTo(kRulesReportColumns));
            Assert.That(ShownRuleNames(component), Is.EqualTo((List<string>)["R-1"]));
        }

        [TestCase(ReportType.UnusedRules)]
        [TestCase(ReportType.AppRules)]
        public async Task UsageReportsShowTheLastHitOnceRightAfterTheNumber(ReportType reportType)
        {
            // the app rules report lists rulebases on this page, so its columns are checked on the column list itself
            await using BunitContext context = CreateContext();

            IRenderedComponent<FancyRulesReport> component = Render(context, reportType, [BuildManagementWithRuleTree(1)]);
            List<string> keys = ColumnKeysOf(component.Instance);

            Assert.That(keys.Take(3), Is.EqualTo((List<string>)["number", "last_hit_pre", "last_modified_pre"]));
            Assert.That(keys, Has.None.EqualTo("last_hit"));
            Assert.That(keys, Has.None.EqualTo("last_modified"));
        }

        [TestCase(ReportType.ComplianceReport)]
        [TestCase(ReportType.ComplianceDiffReport)]
        public async Task AComplianceReportShowsComplianceAndViolations(ReportType reportType)
        {
            await using BunitContext context = CreateContext();

            IRenderedComponent<FancyRulesReport> component = Render(context, reportType, [BuildManagementWithRuleTree(1)]);

            Assert.That(ColumnKeys(component).Skip(1).Take(2), Is.EqualTo((List<string>)["compliance", "violation_details"]));
            Assert.That(ColumnKeys(component), Has.None.EqualTo("last_hit"));
        }

        [Test]
        public async Task ANatRulesReportShowsTheTranslationInsteadOfActionAndTrack()
        {
            await using BunitContext context = CreateContext();

            IRenderedComponent<FancyRulesReport> component = Render(context, ReportType.NatRules, [BuildManagementWithRuleTree(1)]);

            List<string> keys = ColumnKeys(component);
            Assert.That(keys, Is.SupersetOf((List<string>)["trans_source", "trans_destination", "trans_services"]));
            Assert.That(keys, Has.None.EqualTo("action"));
            Assert.That(keys, Has.None.EqualTo("track"));
        }

        [Test]
        public async Task ChangingTheReportTypeRebuildsTheColumns()
        {
            await using BunitContext context = CreateContext();
            IRenderedComponent<FancyRulesReport> component = Render(context, ReportType.Rules, [BuildManagementWithRuleTree(1)]);

            component.Render(parameters => parameters.Add(p => p.SelectedReportType, ReportType.NatRules));

            Assert.That(ColumnKeys(component), Does.Contain("trans_source"));
        }

        [Test]
        public async Task TheLayerHeaderShowsItsNameAndHowManyRulesItHolds()
        {
            await using BunitContext context = CreateContext();

            IRenderedComponent<FancyRulesReport> component = Render(context, ReportType.Rules, [BuildManagementWithRuleTree(1, 2)]);

            IElement headerRow = component.Find("tbody tr.table-warning");
            Assert.That(headerRow.TextContent, Does.Contain(kLayerName));
            Assert.That(headerRow.QuerySelector("span.text-muted")!.TextContent, Is.EqualTo($"(2 {userConfig.GetText("rules")})"));
        }

        [Test]
        public async Task ANumberedSectionHeaderShowsItsNumber()
        {
            await using BunitContext context = CreateContext();
            ManagementReport management = BuildManagementWithRuleTree(1);
            RuleTreeItem header = ruleTreeBuilder.RuleTreeCache[(kManagementId, kGatewayId)].Children.Single();
            header.Data.DisplayOrderNumberString = "7";

            IRenderedComponent<FancyRulesReport> component = Render(context, ReportType.Rules, [management]);

            Assert.That(component.Find("tbody tr.table-warning span.me-2").TextContent, Is.EqualTo("7"));
        }

        [Test]
        public async Task ClickingTheLayerHeaderTogglesItsRules()
        {
            await using BunitContext context = CreateContext();
            // the tree starts fully expanded
            IRenderedComponent<FancyRulesReport> component = Render(context, ReportType.Rules, [BuildManagementWithRuleTree(1, 2)]);
            string expandedState = LayerHeaderToggle(component).GetAttribute("aria-expanded")!;

            LayerHeaderToggle(component).Click();

            Assert.That(expandedState, Is.EqualTo("true"));
            Assert.That(ShownRuleNames(component), Is.Empty);
            Assert.That(LayerHeaderToggle(component).GetAttribute("aria-expanded"), Is.EqualTo("false"));
        }

        [Test]
        public async Task CollapsingAllHidesTheRulesAndExpandingAllShowsThemAgain()
        {
            await using BunitContext context = CreateContext();
            IRenderedComponent<FancyRulesReport> component = Render(context, ReportType.Rules, [BuildManagementWithRuleTree(1, 2)]);
            bool expandDisabledAtStart = component.Find("button[name=btnExpandAll]").HasAttribute("disabled");

            component.Find("button[name=btnCollapseAll]").Click();
            List<string> collapsed = ShownRuleNames(component);
            bool collapseDisabled = component.Find("button[name=btnCollapseAll]").HasAttribute("disabled");
            component.Find("button[name=btnExpandAll]").Click();

            Assert.That(expandDisabledAtStart, Is.True);
            Assert.That(collapsed, Is.Empty);
            Assert.That(collapseDisabled, Is.True);
            Assert.That(ShownRuleNames(component), Is.EqualTo((List<string>)["R-1", "R-2"]));
            Assert.That(component.Find("button[name=btnExpandAll]").HasAttribute("disabled"), Is.True);
        }

        [Test]
        public async Task RulesOfAnInlineLayerAreShownBelowTheRuleThatCallsIt()
        {
            await using BunitContext context = CreateContext();
            RulebaseReport layer = BuildRulebase(kLayerId, kLayerName, 10);
            RulebaseReport inlineLayer = BuildRulebase(kInlineLayerId, "Inline", 20);
            RulebaseLink[] links =
            [
                InitialLink(kGatewayId, kLayerId),
                new RulebaseLink { GatewayId = kGatewayId, FromRulebaseId = kLayerId, FromRuleId = 10, NextRulebaseId = kInlineLayerId, LinkType = 3 }
            ];
            ruleTreeBuilder.BuildRuleTree([layer, inlineLayer], links, kManagementId, kGatewayId);
            ManagementReport management = new()
            {
                Id = kManagementId,
                Name = "management",
                Rulebases = [layer, inlineLayer],
                Devices = [new DeviceReport { Id = kGatewayId, Name = "gateway", RulebaseLinks = links }]
            };

            IRenderedComponent<FancyRulesReport> component = Render(context, ReportType.Rules, [management]);

            Assert.That(ShownRuleNames(component), Is.EqualTo((List<string>)["R-10", "R-20"]));
        }

        [Test]
        public async Task TreeNodesWithoutRuleAreLeftOut()
        {
            await using BunitContext context = CreateContext();
            ManagementReport management = BuildManagementWithRuleTree(1);
            ruleTreeBuilder.RuleTreeCache[(kManagementId, kGatewayId)].Children.Add(new RuleTreeItem { Data = null!, IsRule = true });

            IRenderedComponent<FancyRulesReport> component = Render(context, ReportType.Rules, [management]);

            Assert.That(component.FindAll("tbody tr.table-warning"), Has.Count.EqualTo(1));
            Assert.That(ShownRuleNames(component), Is.EqualTo((List<string>)["R-1"]));
        }

        [Test]
        public async Task AGatewayWhoseRuleTreeIsMissingShowsNoRows()
        {
            await using BunitContext context = CreateContext();
            ManagementReport management = BuildManagementWithRuleTree(1);
            ruleTreeBuilder.ClearCachedRuleTrees();

            IRenderedComponent<FancyRulesReport> component = Render(context, ReportType.Rules, [management]);

            Assert.That(ShownRuleNames(component), Is.Empty);
            Assert.That(component.FindAll("tbody tr.table-warning"), Is.Empty);
        }

        [Test]
        public async Task ARulebaseListedAboveLinksToItsRuleTree()
        {
            await using BunitContext context = CreateContext();
            ManagementReport management = BuildManagementWithRuleTree(1);
            RulebaseLink[] referencingLinks = [InitialLink(kOtherGatewayId, kLayerId)];
            ruleTreeBuilder.BuildRuleTree(management.Rulebases, referencingLinks, kManagementId, kOtherGatewayId,
                referencedRulebaseIds: new HashSet<int> { kLayerId });
            management.Devices =
            [
                .. management.Devices,
                new DeviceReport
                {
                    Id = kOtherGatewayId,
                    Name = "referencing gateway",
                    RulebaseLinks = referencingLinks,
                    ReferencedRulebaseTreeIds = new() { [kLayerId] = kGatewayId }
                }
            ];
            IRenderedComponent<FancyRulesReport> component = Render(context, ReportType.Rules, [management]);

            IElement link = component.Find("td.bg-light button.btn-link");
            Assert.That(link.TextContent.Trim(), Is.EqualTo("gateway"));
            Assert.That(link.ParentElement!.TextContent, Does.Contain(userConfig.GetText("rulebase_listed_above")));
            link.Click();

            JSRuntimeInvocation scroll = context.JSInterop.VerifyInvoke(kScrollFunction);
            Assert.That(scroll.Arguments[0], Is.EqualTo(DeviceReportExtensions.GetRuleTreeAnchorId(kManagementId, kGatewayId)));
        }

        [TestCase(ReportType.AppRules, false)]
        [TestCase(ReportType.Recertification, false)]
        [TestCase(ReportType.Rules, true)]
        public async Task RulebaseReportsAndRecertificationsListTheRulebasesInsteadOfTheGateways(ReportType reportType, bool recertification)
        {
            await using BunitContext context = CreateContext();
            ManagementReport management = BuildManagementWithRuleTree(1, 2);
            management.Rulebases = [.. management.Rulebases, BuildRulebase(23, "empty rulebase")];

            IRenderedComponent<FancyRulesReport> component = Render(context, reportType, [management], recertification);

            Assert.That(component.FindComponents<Collapse>().Select(collapse => collapse.Instance.Title), Is.EqualTo((List<string>)["management", kLayerName]));
            Assert.That(component.FindComponent<RuleBaseReport>().Instance.Rules, Has.Count.EqualTo(2));
            Assert.That(component.FindAll("thead th[data-column-key]"), Is.Empty);
        }

        /// <summary>
        /// Reads the column keys the component prepared, for report types whose rows are not shown as a tree.
        /// </summary>
        private static List<string> ColumnKeysOf(FancyRulesReport component)
        {
            System.Reflection.FieldInfo field = typeof(FancyRulesReport).GetField("_ruleColumns", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("_ruleColumns field not found");
            System.Collections.IEnumerable columns = (System.Collections.IEnumerable)field.GetValue(component)!;
            return [.. columns.Cast<object>().Select(column => (string)column.GetType().GetProperty("Key")!.GetValue(column)!)];
        }
    }
}
