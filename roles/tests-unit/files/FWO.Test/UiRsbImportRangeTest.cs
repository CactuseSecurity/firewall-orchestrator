using Bunit;
using FWO.Api.Client;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Report;
using FWO.Report;
using FWO.Ui.Services;
using FWO.Ui.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// The rule and report tab show the state of the generated report, independent of the all tab and its time filter.
    /// </summary>
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    public class UiRsbImportRangeTest : BunitContext
    {
        private const int kManagementId = 1;
        private const int kSuperManagerId = 9;
        private const long kAllTabImportId = 500;

        /// <summary>Rule returned by the simulated api.</summary>
        private const long kKnownRuleId = 1;

        private readonly UiRsbTestApiConn api = new() { RelevantImportId = kAllTabImportId };
        private readonly List<string> messages = [];

        [TestCase(true, TestName = "RuleTabUsesTheImportsOfTheReportWithAllTab")]
        [TestCase(false, TestName = "RuleTabUsesTheImportsOfTheReportWithoutAllTab")]
        public void RuleTabUsesTheImportsOfTheReport(bool allTabVisible)
        {
            RenderSidebar(BuildReport(new ImportRange(40, 42)), allTabVisible, BuildRule(kKnownRuleId, kManagementId));

            AssertLastRuleQuery(kKnownRuleId, new ImportRange(40, 42));
            Assert.That(messages, Is.Empty);
        }

        [Test]
        public void RuleTabUsesTheImportsOfTheReportAfterSelectingATenant()
        {
            SimulatedReport report = BuildReport(new ImportRange(40, 42));
            IRenderedComponent<RightSidebar> cut = RenderSidebar(report, true);

            Rerender(cut, report, false, BuildRule(21, kManagementId));

            AssertLastRuleQuery(21, new ImportRange(40, 42));
        }

        [Test]
        public void RuleTabUsesTheImportsOfTheRegeneratedReport()
        {
            IRenderedComponent<RightSidebar> cut = RenderSidebar(BuildReport(new ImportRange(40, 40)), true, BuildRule(21, kManagementId));
            AssertLastRuleQuery(21, new ImportRange(40, 40));

            // generating a report clears the selection; the time filter is unchanged, so the all tab keeps its imports
            SimulatedReport regeneratedReport = BuildReport(new ImportRange(55, 55));
            Rerender(cut, regeneratedReport, true);
            Rerender(cut, regeneratedReport, true, BuildRule(21, kManagementId));

            AssertLastRuleQuery(21, new ImportRange(55, 55));
        }

        [Test]
        public void RuleOfASuperManagerUsesTheImportsOfTheSuperManager()
        {
            SimulatedReport report = BuildReport(new ImportRange(40, 42));
            report.ImportRanges[kSuperManagerId] = new ImportRange(30, 31);

            RenderSidebar(report, false, BuildRule(22, kSuperManagerId));

            AssertLastRuleQuery(22, new ImportRange(30, 31));
        }

        [Test]
        public void ImportsOfTheAllTabAreTheFallback()
        {
            RenderSidebar(BuildReport(null), true, BuildRule(21, kManagementId));

            AssertLastRuleQuery(21, new ImportRange(kAllTabImportId, kAllTabImportId));
        }

        [Test]
        public void UnknownImportsAreReportedInsteadOfFetchingNothing()
        {
            RenderSidebar(BuildReport(null), false, BuildRule(kKnownRuleId, kManagementId));

            Assert.That(api.RuleQueryVariables, Is.Empty);
            Assert.That(messages, Is.Not.Empty.And.All.Contains(new SimulatedUserConfig().GetText("object_fetch")));
        }

        [TestCase(true, TestName = "ReportTabUsesTheImportsOfTheReportWithAllTab")]
        [TestCase(false, TestName = "ReportTabUsesTheImportsOfTheReportWithoutAllTab")]
        public async Task ReportTabUsesTheImportsOfTheReport(bool allTabVisible)
        {
            SimulatedReport report = BuildReport(new ImportRange(40, 42));
            IRenderedComponent<RightSidebar> cut = RenderSidebar(report, allTabVisible);

            await cut.InvokeAsync(() => cut.Instance.FetchContent(RsbTab.report, ObjCategory.nobj, () => Task.CompletedTask, kManagementId));

            Assert.That(report.LastObjectQueryVariables[QueryVar.ImportIdStart], Is.EqualTo(40L));
            Assert.That(report.LastObjectQueryVariables[QueryVar.ImportIdEnd], Is.EqualTo(42L));
        }

        private static SimulatedReport BuildReport(ImportRange? importRange)
        {
            SimulatedReport report = SimulatedReport.DetailedReport(ReportType.Rules);
            if (importRange != null)
            {
                report.ImportRanges[kManagementId] = importRange.Value;
            }
            return report;
        }

        private static Rule BuildRule(long id, int managementId)
        {
            return new() { Id = id, MgmtId = managementId };
        }

        private void AssertLastRuleQuery(long ruleId, ImportRange expectedRange)
        {
            Assert.That(api.RuleQueryVariables, Is.Not.Empty);
            Dictionary<string, object> variables = api.RuleQueryVariables[^1];
            Assert.That(variables[QueryVar.RuleId], Is.EqualTo(ruleId));
            Assert.That(variables[QueryVar.ImportIdStart], Is.EqualTo(expectedRange.Start));
            Assert.That(variables[QueryVar.ImportIdEnd], Is.EqualTo(expectedRange.End));
        }

        private IRenderedComponent<RightSidebar> RenderSidebar(ReportBase report, bool allTabVisible, params Rule[] selectedRules)
        {
            DomEventService domEventService = new();
            domEventService.InvokeNavbarHeightChanged(0);
            Services.AddSingleton<UserConfig>(new SimulatedUserConfig());
            Services.AddSingleton<ApiConnection>(api);
            Services.AddSingleton(domEventService);
            Services.AddScoped(_ => JSInterop.JSRuntime);
            Services.AddLocalization();
            JSInterop.Mode = JSRuntimeMode.Loose;
            Action<Exception?, string, string, bool> displayMessage = (_, title, message, _) => messages.Add($"{title}: {message}");

            return Render<RightSidebar>(parameters => parameters
                .AddCascadingValue(displayMessage)
                .Add(p => p.CurrentReport, report)
                .Add(p => p.SelectedReportType, ReportType.Rules)
                .Add(p => p.AllTabVisible, allTabVisible)
                .Add(p => p.SelectedRules, [.. selectedRules]));
        }

        private static void Rerender(IRenderedComponent<RightSidebar> cut, ReportBase report, bool allTabVisible, params Rule[] selectedRules)
        {
            RenderedComponentRenderExtensions.Render(cut, parameters => parameters
                .Add(p => p.CurrentReport, report)
                .Add(p => p.SelectedReportType, ReportType.Rules)
                .Add(p => p.AllTabVisible, allTabVisible)
                .Add(p => p.SelectedRules, [.. selectedRules]));
        }
    }
}
