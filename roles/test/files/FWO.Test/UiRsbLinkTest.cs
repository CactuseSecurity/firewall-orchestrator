
using Bunit;
using Bunit.TestDoubles;
using NUnit.Framework;
using FWO.Api.Client;
using FWO.Config.Api;
using FWO.Report;
using FWO.Ui.Shared;
using Microsoft.Extensions.DependencyInjection;
using AngleSharp.Dom;
using AngleSharp.Css.Dom;
using System.Text.RegularExpressions;
using FWO.Logging;

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
        public async Task RSBComponentRenders()
        {
            // Arrange
            Services.AddSingleton(userConfig);
            Services.AddSingleton(apiConnection);
            Services.AddLocalization();

            var objToFind = currentReport.ReportData.ManagementData[0].Objects[1];
            var simObjHtml = ReportDevicesBase.ConstructLink(ObjCatString.NwObj, "", 0, objToFind.Id, objToFind.Name, OutputLocation.report, currentReport.ReportData.ManagementData[0].Id, "");
            var hrefValue = Regex.Match(simObjHtml, "href=\"([^\"]*)\"").Groups[1].Value;
            var link = $"https://localhost/{hrefValue}";

            var navigationManager = Services.GetRequiredService<FakeNavigationManager>();
            navigationManager.NavigateTo(link);

            // Mock JS interop
            JSInterop.Setup<string>("getCurrentUrl").SetResult(link);
            var scrollIntoRSBViewInvocation = JSInterop.Setup<bool>("scrollIntoRSBView", _ => true).SetResult(true);
            var removeUrlFragmentInvocation = JSInterop.SetupVoid("removeUrlFragment");

            // Act
            var cut = RenderComponent<RightSidebar>(parameters => parameters
                .Add(p => p.CurrentReport, currentReport)
                .Add(p => p.SelectedRules, [currentReport.ReportData.ManagementData[0].Devices[0].Rules![0]]));

            // Assert
            Assert.That(scrollIntoRSBViewInvocation.Invocations, Is.Not.Empty, "scrollIntoRSBView should have been called");
            var invocation = scrollIntoRSBViewInvocation.Invocations.First();
            var parameter = invocation.Arguments[0];
            Assert.That(parameter, Is.Not.Null, "scrollIntoRSBView was called with a null parameter");
            Assert.That(parameter, Is.InstanceOf<string>(), "scrollIntoRSBView was called with a non-string parameter");
            Assert.That((string)parameter!, Is.Not.Empty, "scrollIntoRSBView was called with an empty string");

            var element = cut.Find($"#{parameter}");
            Assert.That(element, Is.Not.Null, "Element with id {parameter} not found in right sidebar");
            Assert.That(IsElementVisible(element), Is.True, "Element is not visible (might be incorrect tab or uncollapsed)");
        }

        private bool IsElementVisible(IElement? element)
        {
            while (element != null)
            {
                var display = element.GetStyle().GetPropertyValue("display");
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