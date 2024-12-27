
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
        public void RSBComponentRenders()
        {
            // Arrange
            Services.AddSingleton(userConfig);
            Services.AddSingleton(apiConnection);
            Services.AddLocalization();

            var objToFind = currentReport.ReportData.ManagementData[0].Objects[1];
            var link = $"https://localhost/{ReportDevicesBase.ConstructLink(ObjCatString.NwObj, "", 0, objToFind.Id, objToFind.Name, OutputLocation.report, currentReport.ReportData.ManagementData[0].Id, "")}";

            var navigationManager = Services.GetRequiredService<FakeNavigationManager>();
            navigationManager.NavigateTo(link);

            // Mock JS interop
            JSInterop.Setup<string>("getCurrentUrl").SetResult(link);
            var scrollIntoRSBViewInvocation = JSInterop.Setup<bool>("scrollIntoRSBView", _ => true).SetResult(true);
            JSInterop.SetupVoid("removeUrlFragment()");

            // Act
            var cut = RenderComponent<RightSidebar>(parameters => parameters
                .Add(p => p.CurrentReport, currentReport)
                .Add(p => p.SelectedRules, [currentReport.ReportData.ManagementData[0].Devices[0].Rules![0]]));

            // Assert
            var invocation = scrollIntoRSBViewInvocation.Invocations.First();
            var parameter = invocation.Arguments[0];
            Assert.That(parameter, Is.Not.Null);
            Assert.That(parameter, Is.InstanceOf<string>());
            Assert.That((string)parameter!, Is.Not.Empty);

            var element = cut.Find((string)parameter!);
            Assert.That(element, Is.Not.Null);
            Assert.That(IsElementVisible(element), Is.True);
        }

        private bool IsElementVisible(IElement? element)
        {
            while (element != null)
            {
                var display = element.GetStyle().GetPropertyValue("display");
                if (display == "none")
                {
                    return false;
                }
                element = element.ParentElement;
            }
            return true;
        }
    }
}