using AngleSharp.Css.Dom;
using AngleSharp.Dom;
using Bunit;
using Bunit.TestDoubles;
using FWO.Api.Client;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Logging;
using FWO.Data.Report;
using FWO.Report;
using FWO.Report.Filter;
using FWO.Ui.Services;
using FWO.Ui.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using NUnit.Framework;

namespace FWO.Test
{
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    public class UiRsbLinkTest : BunitContext
    {
        static readonly UserConfig userConfig = new SimulatedUserConfig
        {
            ModNamingConvention = "{\"networkAreaRequired\":true,\"fixedPartLength\":4,\"freePartLength\":5,\"networkAreaPattern\":\"NA\",\"appRolePattern\":\"AR\"}"
        };
        static readonly ApiConnection apiConnection = new UiRsbTestApiConn();
        static readonly ReportBase currentReport = SimulatedReport.DetailedReport();

        [Test]
        public async Task ObjShouldBeVisibleAfterNavigation()
        {
            Data.NetworkObject objToFind = currentReport.ReportData.ManagementData[0].Objects[1];
            Rule selectedRule = currentReport.ReportData.ManagementData[0].Rulebases[0].Rules[0];

            List<Rule> selectedRules = [selectedRule];

            string elementId = await NavigateToObjectLink(currentReport, currentReport.ReportType, objToFind.Id, selectedRules);

            Assert.That(elementId, Does.StartWith("report-"));
        }

        [Test]
        public async Task ChangeReportObjShouldBeVisibleInReportTabAfterNavigation()
        {
            ReportChanges changeReport = BuildChangeReport();
            Rule changedRule = changeReport.ReportData.ManagementData[0].RuleChanges![0].NewRule;
            long objId = changedRule.Tos[0].Object.Id;

            string elementId = await NavigateToObjectLink(changeReport, ReportType.Changes, objId, []);

            Assert.That(elementId, Is.EqualTo($"report-m{changedRule.MgmtId}-{ObjCatString.NwObj}0x{objId}"));
        }

        [Test]
        public async Task ScrollToLinkTargetIsRetriedAfterLazilyFetchedObjectsAreRendered()
        {
            ReportChanges changeReport = BuildChangeReport();
            long objId = changeReport.ReportData.ManagementData[0].RuleChanges![0].NewRule.Tos[0].Object.Id;
            // the target is never reported as found, so every scroll attempt stays visible in the invocations
            (IRenderedComponent<RightSidebar> cut, JSRuntimeInvocationHandler<bool> scrollHandler) = RenderSidebarAtObjectLink(changeReport, ReportType.Changes, objId, [], false);

            IRenderedComponent<AnchorNavToRSB> anchorNavToRSB = cut.FindComponent<AnchorNavToRSB>();
            await anchorNavToRSB.InvokeAsync(() => anchorNavToRSB.Instance.NavigateAndScrollToFragment());

            // initial attempt + attempt during the fetch + retry after the fetched objects were rendered
            cut.WaitForAssertion(() => Assert.That(scrollHandler.Invocations, Has.Count.GreaterThanOrEqualTo(3)));
            Assert.That(anchorNavToRSB.Instance.HasPendingScrollTarget, Is.True);
            // the navigation tries a smooth scroll, the retries around the lazy fetch are instant (a render would cancel a smooth scroll)
            Assert.That(scrollHandler.Invocations.Count(invocation => Equals(invocation.Arguments[1], true)), Is.EqualTo(1));
            Assert.That(scrollHandler.Invocations.Count(invocation => Equals(invocation.Arguments[1], false)), Is.GreaterThanOrEqualTo(2));
        }

        [Test]
        public async Task PendingScrollTargetIsClearedOnceTheLinkedObjectIsFound()
        {
            ReportChanges changeReport = BuildChangeReport();
            long objId = changeReport.ReportData.ManagementData[0].RuleChanges![0].NewRule.Tos[0].Object.Id;
            (IRenderedComponent<RightSidebar> cut, _) = RenderSidebarAtObjectLink(changeReport, ReportType.Changes, objId, [], true);

            IRenderedComponent<AnchorNavToRSB> anchorNavToRSB = cut.FindComponent<AnchorNavToRSB>();
            await anchorNavToRSB.InvokeAsync(() => anchorNavToRSB.Instance.NavigateAndScrollToFragment());

            Assert.That(anchorNavToRSB.Instance.HasPendingScrollTarget, Is.False);
        }

        [Test]
        public async Task FetchedReportTabObjectsSurviveClearingTheRuleSelectionButNotANewReport()
        {
            Data.NetworkObject objToFind = currentReport.ReportData.ManagementData[0].Objects[1];
            List<Rule> selectedRules = [currentReport.ReportData.ManagementData[0].Rulebases[0].Rules[0]];
            (IRenderedComponent<RightSidebar> cut, _) = RenderSidebarAtObjectLink(currentReport, currentReport.ReportType, objToFind.Id, selectedRules, true);
            IRenderedComponent<AnchorNavToRSB> anchorNavToRSB = cut.FindComponent<AnchorNavToRSB>();
            await anchorNavToRSB.InvokeAsync(() => anchorNavToRSB.Instance.NavigateAndScrollToFragment());
            string elementSelector = $"#report-m{currentReport.ReportData.ManagementData[0].Id}-{ObjCatString.NwObj}0x{objToFind.Id}";
            cut.WaitForAssertion(() => Assert.That(cut.FindAll(elementSelector), Is.Not.Empty));

            // "clear all" in the rule tab makes the report page pass an empty selection
            RenderedComponentRenderExtensions.Render(cut, parameters => parameters
                .Add(p => p.CurrentReport, currentReport)
                .Add(p => p.SelectedReportType, currentReport.ReportType)
                .Add(p => p.SelectedRules, []));

            Assert.That(cut.FindAll(elementSelector), Is.Not.Empty, "objects fetched into the report tab must be kept");

            RenderedComponentRenderExtensions.Render(cut, parameters => parameters
                .Add(p => p.CurrentReport, SimulatedReport.DetailedReport())
                .Add(p => p.SelectedReportType, currentReport.ReportType)
                .Add(p => p.SelectedRules, []));

            Assert.That(cut.FindAll(elementSelector), Is.Empty, "a new report must not show the objects fetched for the previous one");
        }

        /// <summary>
        /// Navigates to the link the report builds for a network object and returns the id of the right sidebar
        /// element scrolled to, after asserting that this element exists and is visible.
        /// </summary>
        private async Task<string> NavigateToObjectLink(ReportBase report, ReportType selectedReportType, long objId, List<Rule> selectedRules)
        {
            (IRenderedComponent<RightSidebar> cut, JSRuntimeInvocationHandler<bool> scrollIntoRSBViewInvocation) = RenderSidebarAtObjectLink(report, selectedReportType, objId, selectedRules, true);

            IRenderedComponent<AnchorNavToRSB> anchorNavToRSB = cut.FindComponent<AnchorNavToRSB>();
            await anchorNavToRSB.InvokeAsync(() => anchorNavToRSB.Instance.NavigateAndScrollToFragment());

            Assert.That(scrollIntoRSBViewInvocation.Invocations, Is.Not.Empty, "scrollIntoRSBView should have been called");
            Assert.That(removeUrlFragmentInvocation!.Invocations, Is.Not.Empty, "removeUrlFragment should have been called");
            JSRuntimeInvocation invocation = scrollIntoRSBViewInvocation.Invocations.First();
            object? parameter = invocation.Arguments[0];
            Assert.That(parameter, Is.InstanceOf<string>(), "scrollIntoRSBView was called with a non-string parameter");
            string elementId = (string)parameter!;
            Assert.That(elementId, Is.Not.Empty, "scrollIntoRSBView was called with an empty string");
            IElement element = cut.Find($"#{elementId}");
            Assert.That(IsElementVisible(element), Is.True, "Element is not visible (might be incorrect tab or collapsed)");
            return elementId;
        }

        private JSRuntimeInvocationHandler? removeUrlFragmentInvocation;

        /// <summary>
        /// Renders the right sidebar while the browser is at the link the report builds for a network object.
        /// </summary>
        private (IRenderedComponent<RightSidebar>, JSRuntimeInvocationHandler<bool>) RenderSidebarAtObjectLink(ReportBase report,
            ReportType selectedReportType, long objId, List<Rule> selectedRules, bool scrollFindsTarget)
        {
            // Event Service
            DomEventService eventService = new DomEventService();
            eventService.InvokeNavbarHeightChanged(50); // Simulate initial navbar height change

            // Arrange
            Services.AddSingleton(userConfig);
            Services.AddSingleton(apiConnection);
            Services.AddSingleton(eventService);
            Services.AddScoped(_ => JSInterop.JSRuntime);
            Services.AddLocalization();

            int mgmtId = report.ReportData.ManagementData[0].Id;
            string hrefValue = ReportDevicesBase.GetReportDevicesLinkAddress(OutputLocation.report, mgmtId, ObjCatString.NwObj, 0, objId, report.ReportType);
            string link = $"https://localhost/{hrefValue}";

            BunitNavigationManager navigationManager = Services.GetRequiredService<BunitNavigationManager>();
            navigationManager.NavigateTo(link);
            Assert.That(navigationManager.Uri, Is.EqualTo(link));

            // Mock JS interop
            JSInterop.SetupVoid("initializeEventHandlers", _ => true).SetVoidResult();
            JSInterop.Setup<string>("getCurrentUrl").SetResult(link);
            JSRuntimeInvocationHandler<bool> scrollIntoRSBViewInvocation = JSInterop.Setup<bool>("scrollIntoRSBView", _ => true).SetResult(scrollFindsTarget);
            removeUrlFragmentInvocation = JSInterop.SetupVoid("removeUrlFragment").SetVoidResult();

            // Act
            IRenderedComponent<RightSidebar> cut = Render<RightSidebar>(parameters => parameters
                .Add(p => p.CurrentReport, report)
                .Add(p => p.SelectedReportType, selectedReportType)
                .Add(p => p.SelectedRules, selectedRules));
            return (cut, scrollIntoRSBViewInvocation);
        }

        /// <summary>
        /// Change report of the simulated management: one modified rule whose new version references a further object.
        /// </summary>
        private static ReportChanges BuildChangeReport()
        {
            ManagementReport simulatedManagement = SimulatedReport.DetailedReport().ReportData.ManagementData[0];
            Data.NetworkObject sourceObject = simulatedManagement.ReportObjects[0];
            Data.NetworkObject destinationObject = simulatedManagement.ReportObjects[1];
            Rule oldRule = new()
            {
                Id = 11,
                MgmtId = simulatedManagement.Id,
                Froms = [new NetworkLocation(new NetworkUser(), sourceObject)],
                Tos = [new NetworkLocation(new NetworkUser(), sourceObject)]
            };
            Rule newRule = new()
            {
                Id = 12,
                MgmtId = simulatedManagement.Id,
                Froms = [new NetworkLocation(new NetworkUser(), sourceObject)],
                Tos = [new NetworkLocation(new NetworkUser(), destinationObject)]
            };
            return new ReportChanges(new DynGraphqlQuery(""), userConfig, ReportType.Changes, new TimeFilter(), false)
            {
                ReportData = new ReportData
                {
                    ManagementData =
                    [
                        new ManagementReport
                        {
                            Id = simulatedManagement.Id,
                            Name = simulatedManagement.Name,
                            Devices = simulatedManagement.Devices,
                            RuleChanges = [new RuleChange { ChangeAction = 'C', OldRule = oldRule, NewRule = newRule }]
                        }
                    ]
                }
            };
        }

        private static bool IsElementVisible(IElement? element)
        {
            while (element != null)
            {
                ICssStyleDeclaration? computedStyle = element.Owner?.DefaultView?.GetComputedStyle(element);
                string? display = computedStyle?.GetPropertyValue("display");
                if (display == "none")
                {
                    Log.WriteError("Test UI RSB", $"Element {element.TagName} is not visible");
                    return false;
                }
                element = element.ParentElement;
            }
            return true;
        }
    }
}
