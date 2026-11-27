using Bunit;
using FWO.Api.Client;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data.Report;
using FWO.Report.Filter;
using FWO.Ui.Services;
using FWO.Ui.Shared;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// The all tab shows one point in time for rule reports and the selected time range for change reports (#4730).
    /// </summary>
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    public class UiRsbAllTabTimeTest : BunitContext
    {
        private readonly UiRsbTestApiConn api = new();
        private readonly TimeFilter timeFilter = new();

        [Test]
        public void RuleReportUsesOnePointInTime()
        {
            RenderSidebar(ReportType.Rules);

            AssertOnePointInTime();
        }

        [Test]
        public void ChangeReportUsesTheTimeRange()
        {
            IRenderedComponent<RightSidebar> cut = RenderSidebar(ReportType.Rules);

            Rerender(cut, ReportType.Changes);

            AssertTimeRange();
        }

        [Test]
        public void SwitchingBackToARuleReportUsesOnePointInTimeAgain()
        {
            IRenderedComponent<RightSidebar> cut = RenderSidebar(ReportType.Rules);
            Rerender(cut, ReportType.Changes);
            AssertTimeRange();

            Rerender(cut, ReportType.Rules);

            AssertOnePointInTime();
        }

        [Test]
        public void SwitchingBetweenRuleReportsKeepsTheTime()
        {
            IRenderedComponent<RightSidebar> cut = RenderSidebar(ReportType.Rules);
            int requestsOfFirstReport = api.RequestedImportTimes.Count;

            Rerender(cut, ReportType.ResolvedRules);

            Assert.That(api.RequestedImportTimes, Has.Count.EqualTo(requestsOfFirstReport));
        }

        private void AssertOnePointInTime()
        {
            Assert.That(api.RequestedImportTimes, Has.Count.AtLeast(2));
            Assert.That(api.RequestedImportTimes[^2], Is.EqualTo(api.RequestedImportTimes[^1]));
        }

        private void AssertTimeRange()
        {
            (string expectedStart, string expectedEnd) = DynGraphqlQuery.ResolveTimeRange(timeFilter);
            Assert.That(api.RequestedImportTimes, Has.Count.AtLeast(2));
            Assert.That(api.RequestedImportTimes[^2], Is.EqualTo(expectedStart));
            Assert.That(api.RequestedImportTimes[^1], Is.EqualTo(expectedEnd));
        }

        private IRenderedComponent<RightSidebar> RenderSidebar(ReportType reportType)
        {
            DomEventService domEventService = new();
            domEventService.InvokeNavbarHeightChanged(0);
            Services.AddSingleton<UserConfig>(new SimulatedUserConfig());
            Services.AddSingleton<ApiConnection>(api);
            Services.AddSingleton(domEventService);
            Services.AddScoped(_ => JSInterop.JSRuntime);
            Services.AddLocalization();
            JSInterop.Mode = JSRuntimeMode.Loose;

            return Render<RightSidebar>(parameters => parameters
                .Add(p => p.SelectedReportType, reportType)
                .Add(p => p.LSBTime, timeFilter)
                .Add(p => p.AllTabVisible, true));
        }

        private void Rerender(IRenderedComponent<RightSidebar> cut, ReportType reportType)
        {
            RenderedComponentRenderExtensions.Render(cut, parameters => parameters
                .Add(p => p.SelectedReportType, reportType)
                .Add(p => p.LSBTime, timeFilter)
                .Add(p => p.AllTabVisible, true));
        }
    }
}
