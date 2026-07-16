using Bunit;
using FWO.Api.Client;
using FWO.Config.Api;
using FWO.Data;
using FWO.Services;
using FWO.Ui.Pages.Compliance;
using Microsoft.Extensions.DependencyInjection;
using NetTools;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Regression tests for the compliance/checks page (issue #4924):
    /// entering two IPs and clicking check must not crash the component.
    /// </summary>
    [TestFixture]
    internal class UiComplianceChecksTest
    {
        [Test]
        public void CheckTwoIps_ForbiddenCommunication_ShowsViolation()
        {
            (ComplianceNetworkZone internalZone, ComplianceNetworkZone dmzZone) = CreateZones();
            using BunitContext context = CreateContext([internalZone, dmzZone]);

            IRenderedComponent<ComplianceChecks> page = RenderPageWithIpInput(context);
            page.Find("button").Click();

            Assert.That(page.FindAll("span.bg-danger"), Has.Count.EqualTo(1));
            Assert.That(page.Markup, Does.Contain(internalZone.Name));
            Assert.That(page.Markup, Does.Contain(dmzZone.Name));
        }

        [Test]
        public void CheckTwoIps_AllowedCommunication_ShowsCompliant()
        {
            (ComplianceNetworkZone internalZone, ComplianceNetworkZone dmzZone) = CreateZones();
            internalZone.AllowedCommunicationDestinations = [dmzZone];
            using BunitContext context = CreateContext([internalZone, dmzZone]);

            IRenderedComponent<ComplianceChecks> page = RenderPageWithIpInput(context);
            page.Find("button").Click();

            Assert.That(page.FindAll("span.bg-success"), Has.Count.EqualTo(1));
            Assert.That(page.FindAll("span.bg-danger"), Is.Empty);
        }

        /// <summary>
        /// Creates one zone for the source ip and one for the destination ip without allowed communications.
        /// </summary>
        private static (ComplianceNetworkZone internalZone, ComplianceNetworkZone dmzZone) CreateZones()
        {
            ComplianceNetworkZone internalZone = new()
            {
                Id = 1,
                Name = "Internal",
                IPRanges = [IPAddressRange.Parse("10.0.0.0/8")]
            };
            ComplianceNetworkZone dmzZone = new()
            {
                Id = 2,
                Name = "DMZ",
                IPRanges = [IPAddressRange.Parse("192.168.0.0/16")]
            };
            return (internalZone, dmzZone);
        }

        private static BunitContext CreateContext(List<ComplianceNetworkZone> networkZones)
        {
            BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddSingleton<ApiConnection>(new SimulatedApiConnection());
            context.Services.AddSingleton<UserConfig>(new SimulatedUserConfig());
            context.Services.AddSingleton(new NetworkZoneService() { NetworkZones = networkZones });
            return context;
        }

        /// <summary>
        /// Renders the page and enters a source and a destination ip like a user would.
        /// </summary>
        private static IRenderedComponent<ComplianceChecks> RenderPageWithIpInput(BunitContext context)
        {
            IRenderedComponent<ComplianceChecks> page = context.Render<ComplianceChecks>();
            IReadOnlyList<AngleSharp.Dom.IElement> ipInputs = page.FindAll("input");
            Assert.That(ipInputs, Has.Count.EqualTo(2));
            ipInputs[0].Input("10.0.0.5");
            ipInputs[1].Input("192.168.1.10");
            return page;
        }
    }
}
