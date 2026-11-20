using Bunit;
using FWO.Api.Client;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data.Report;
using FWO.Report;
using FWO.Report.Filter;
using FWO.Ui.Services;
using FWO.Ui.Shared;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FWO.Test
{
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    public class UiRsbTabOrderTest : BunitContext
    {
        private static readonly string[] kAllOnly = ["All"];
        private static readonly string[] kAllReportRule = ["All", "Report", "Rule"];
        private static readonly string[] kReportRule = ["Report", "Rule"];

        [Test]
        public void OnlyAllTabIsShownBeforeAReportIsGenerated()
        {
            IRenderedComponent<RightSidebar> cut = RenderSidebar(null, ReportType.Rules, true);

            cut.WaitForAssertion(() => Assert.That(TabTitles(cut), Is.EqualTo(kAllOnly)));
        }

        [Test]
        public void RuleReportShowsReportTabBeforeRuleTab()
        {
            IRenderedComponent<RightSidebar> cut = RenderSidebar(SimulatedReport.DetailedReport(ReportType.Rules), ReportType.Rules, true);

            cut.WaitForAssertion(() => Assert.That(TabTitles(cut), Is.EqualTo(kAllReportRule)));
        }

        [Test]
        public void ChangeReportShowsReportTabBeforeRuleTab()
        {
            ReportChanges changeReport = new(new DynGraphqlQuery(""), new SimulatedUserConfig(), ReportType.Changes, new TimeFilter(), false)
            {
                ReportData = SimulatedReport.DetailedReport().ReportData
            };

            IRenderedComponent<RightSidebar> cut = RenderSidebar(changeReport, ReportType.Changes, true);

            cut.WaitForAssertion(() => Assert.That(TabTitles(cut), Is.EqualTo(kAllReportRule)));
        }

        [Test]
        public void ReportTabComesFirstWithoutAllTab()
        {
            IRenderedComponent<RightSidebar> cut = RenderSidebar(SimulatedReport.DetailedReport(ReportType.Rules), ReportType.Rules, false);

            cut.WaitForAssertion(() => Assert.That(TabTitles(cut), Is.EqualTo(kReportRule)));
        }

        [Test]
        public void ReportAndRuleTabAppearAfterTheReportWasGeneratedInTheRightOrder()
        {
            IRenderedComponent<RightSidebar> cut = RenderSidebar(null, ReportType.Rules, true);
            cut.WaitForAssertion(() => Assert.That(TabTitles(cut), Is.EqualTo(kAllOnly)));

            RenderedComponentRenderExtensions.Render(cut, parameters => parameters
                .Add(p => p.CurrentReport, SimulatedReport.DetailedReport(ReportType.Rules))
                .Add(p => p.SelectedReportType, ReportType.Rules)
                .Add(p => p.AllTabVisible, true));

            cut.WaitForAssertion(() => Assert.That(TabTitles(cut), Is.EqualTo(kAllReportRule)));
        }

        private IRenderedComponent<RightSidebar> RenderSidebar(ReportBase? report, ReportType reportType, bool allTabVisible)
        {
            DomEventService domEventService = new();
            domEventService.InvokeNavbarHeightChanged(0);
            Services.AddSingleton<UserConfig>(new SimulatedUserConfig());
            Services.AddSingleton<ApiConnection>(new UiRsbTestApiConn());
            Services.AddSingleton(domEventService);
            Services.AddScoped(_ => JSInterop.JSRuntime);
            Services.AddLocalization();
            JSInterop.Mode = JSRuntimeMode.Loose;

            return Render<RightSidebar>(parameters => parameters
                .Add(p => p.CurrentReport, report)
                .Add(p => p.SelectedReportType, reportType)
                .Add(p => p.AllTabVisible, allTabVisible)
                .Add(p => p.SelectedRules, new List<Data.Rule>()));
        }

        private static List<string> TabTitles(IRenderedComponent<RightSidebar> cut)
        {
            return [.. cut.FindComponent<TabSet>().Instance.Tabs.Select(tab => tab.Title)];
        }
    }
}
