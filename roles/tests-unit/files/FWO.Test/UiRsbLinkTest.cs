
using AngleSharp.Css.Dom;
using AngleSharp.Dom;
using Bunit;
using Bunit.TestDoubles;
using FWO.Api.Client;
using FWO.Config.Api;
using FWO.Logging;
using FWO.Report;
using FWO.Ui.Services;
using FWO.Ui.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using NUnit.Framework;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace FWO.Test
{
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    public class UiRsbLinkTest : Bunit.TestContext
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
            // Event Service
            DomEventService eventService = new DomEventService();
            eventService.InvokeNavbarHeightChanged(50); // Simulate initial navbar height change

            // Arrange
            Services.AddSingleton(userConfig);
            Services.AddSingleton(apiConnection);
            Services.AddSingleton(eventService);
            Services.AddScoped(_ => JSInterop.JSRuntime);
            Services.AddLocalization();

            var objToFind = currentReport.ReportData.ManagementData[0].Objects[1];
            var hrefValue = ReportDevicesBase.GetReportDevicesLinkAddress(OutputLocation.report, currentReport.ReportData.ManagementData[0].Id, ObjCatString.NwObj, 0, objToFind.Id, currentReport.ReportType);
            var link = $"https://localhost/{hrefValue}";

            var navigationManager = Services.GetRequiredService<FakeNavigationManager>();
            navigationManager.NavigateTo(link);

            // Mock JS interop
            JSInterop.Setup<string>("getCurrentUrl").SetResult(link);
            var scrollIntoRSBViewInvocation = JSInterop.Setup<bool>("scrollIntoRSBView", _ => true).SetResult(true);
            var removeUrlFragmentInvocation = JSInterop.SetupVoid("removeUrlFragment");

            // Act
            var cut = RenderComponent<RightSidebar>(parameters => parameters
                .Add(p => p.CurrentReport, currentReport));

            // manually trigger 
            var anchorNavToRSB = cut.FindComponent<AnchorNavToRSB>();
            Task timeout = Task.Delay(2000);
            Task scrollTask = anchorNavToRSB.InvokeAsync(() => anchorNavToRSB.Instance.NavigateAndScrollToFragment());
            Task completedTask = await Task.WhenAny(scrollTask, timeout);
            if (completedTask == timeout)
            {
                Log.WriteDebug("Test UI RSB", "NavigateAndScrollToFragment does not complete timely (circle dependency through state changes?)");
            }
            // Assert
            Assert.That(scrollIntoRSBViewInvocation.Invocations, Is.Not.Empty, "scrollIntoRSBView should have been called");
            var invocation = scrollIntoRSBViewInvocation.Invocations.First();
            var parameter = invocation.Arguments[0];
            Assert.That(parameter, Is.Not.Null, "scrollIntoRSBView was called with a null parameter");
            Assert.That(parameter, Is.InstanceOf<string>(), "scrollIntoRSBView was called with a non-string parameter");
            Assert.That((string)parameter!, Is.Not.Empty, "scrollIntoRSBView was called with an empty string");
            var element = cut.Find($"#{parameter}");
            Assert.That(IsElementVisible(element), Is.True, "Element is not visible (might be incorrect tab or collapsed)");
        }

        private bool IsElementVisible(IElement? element)
        {
            while (element != null)
            {
                var computedStyle = element.Owner?.DefaultView?.GetComputedStyle(element);
                var display = computedStyle?.GetPropertyValue("display");
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
