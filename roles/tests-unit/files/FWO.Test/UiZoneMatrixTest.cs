using Bunit;
using Bunit.TestDoubles;
using FWO.Ui.Pages.Compliance;
using NUnit.Framework;
using Microsoft.Extensions.DependencyInjection;
using FWO.Config.Api;
using FWO.Services;
using FWO.Data;
using AngleSharp.Dom;

namespace FWO.Test
{
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    public class UiZoneMatrixTest : Bunit.TestContext
    {
        private UserConfig _userConfig = new();
        private NetworkZoneService _networkZoneService = new();
	    private List<(ComplianceNetworkZone, ComplianceNetworkZone)> _addCommunication = [];
	    private List<(ComplianceNetworkZone, ComplianceNetworkZone)> _deleteCommunication = [];

        Task<IRenderedComponent<ZonesMatrix>> RenderZoneMatrix(bool editMode = true, bool sortById = true)
        {
            // Set up user config.

            SimulatedGlobalConfig globalConfig = new();
            globalConfig.ComplianceCheckSortMatrixByID = sortById;
            _userConfig = new(globalConfig);

            // Set up NetworkZoneService.

            _networkZoneService = new();

            _networkZoneService.NetworkZones.AddRange(
                [
                    new ComplianceNetworkZone
                    {
                        Id = 1,
                        IdString = "zone_one",
                        Name = "Zone 1"
                    },
                    new ComplianceNetworkZone
                    {
                        Id = 2,
                        IdString = "zone_two",
                        Name = "Zone 2"
                    },
                    new ComplianceNetworkZone
                    {
                        Id = 3,
                        IdString = "zone_three",
                        Name = "Zone 3"
                    },
                    new ComplianceNetworkZone
                    {
                        Id = 4,
                        IdString = "AUTO_CALCULATED_ZONE_UNDEFINED_INTERNAL",
                        Name = "Auto-calculated Undefined-internal Zone",
                        IsAutoCalculatedUndefinedInternalZone = true
                    },
                    new ComplianceNetworkZone
                    {
                        Id = 5,
                        IdString = "AUTO_CALCULATED_ZONE_INTERNET",
                        Name = "Auto-calculated Internet Zone",
                        IsAutoCalculatedInternetZone = true
                    }
                ]
            );

            // Add injected Services to DI container.

            Services.AddScoped(_ => _userConfig);
            Services.AddScoped(_ => _networkZoneService);

            // Reset _addCommunication and _deleteCommunication.

            _addCommunication = [];
            _deleteCommunication = [];

            // Render component with parameters.

            var cut = RenderComponent<ZonesMatrix>(parameters => parameters
                .Add(p => p.EditMode, editMode)
                .Add(p => p.AddCommunication, _addCommunication)
                .Add(p => p.DeleteCommunication, _deleteCommunication)
            );

            return Task.FromResult(cut);
        }

        [Test]
        public async Task ClickZoneElement_ZoneToUndefinedInternal_Unchanged()
        {
            // Arrange

            IRenderedComponent<ZonesMatrix> zoneMatrixComponent = await RenderZoneMatrix();
            IElement matrixElement = zoneMatrixComponent.Find("#matrix-element-zone_one-to-AUTO_CALCULATED_ZONE_UNDEFINED_INTERNAL");

            // Act

            matrixElement.Click();

            // Assert

            Assert.That(_addCommunication.Count == 0);
            Assert.That(_deleteCommunication.Count == 0);
        }

        [Test]
        public async Task ClickZoneElement_UndefinedInternalToZoneOne_Unchanged()
        {
            // Arrange

            IRenderedComponent<ZonesMatrix> zoneMatrixComponent = await RenderZoneMatrix();
            IElement matrixElement = zoneMatrixComponent.Find("#matrix-element-AUTO_CALCULATED_ZONE_UNDEFINED_INTERNAL-to-zone_one");

            // Act

            matrixElement.Click();

            // Assert

            Assert.That(_addCommunication.Count == 0);
            Assert.That(_deleteCommunication.Count == 0);
        }

        [Test]
        public async Task ClickZoneElement_UndefinedInternalToZone_Unchanged()
        {
            // Arrange

            IRenderedComponent<ZonesMatrix> zoneMatrixComponent = await RenderZoneMatrix();
            IElement matrixElement = zoneMatrixComponent.Find("#matrix-element-AUTO_CALCULATED_ZONE_UNDEFINED_INTERNAL-to-zone_one");

            // Act

            matrixElement.Click();

            // Assert

            Assert.That(_addCommunication.Count == 0);
            Assert.That(_deleteCommunication.Count == 0);
        }

        [Test]
        public async Task ClickZoneElement_NotAllowed_CommunicationInAddCommunication()
        {
            // Arrange

            IRenderedComponent<ZonesMatrix> zoneMatrixComponent = await RenderZoneMatrix();
            IElement matrixElement = zoneMatrixComponent.Find("#matrix-element-zone_one-to-zone_two");

            // Act

            matrixElement.Click();

            // Assert

            Assert.That(_addCommunication.Count == 1);
            Assert.That(_addCommunication.First().Item1.IdString == "zone_one");
            Assert.That(_addCommunication.First().Item2.IdString == "zone_two");
            Assert.That(_deleteCommunication.Count == 0);
        }

        [Test]
        public async Task ClickZoneElement_Allowed_CommunicationInDeleteCommunication()
        {
            // Arrange

            IRenderedComponent<ZonesMatrix> zoneMatrixComponent = await RenderZoneMatrix();
            _networkZoneService.NetworkZones.First().AllowedCommunicationDestinations = [_networkZoneService.NetworkZones.ElementAt(1)];
            IElement matrixElement = zoneMatrixComponent.Find("#matrix-element-zone_one-to-zone_two");

            // Act

            matrixElement.Click();

            // Assert

            Assert.That(_addCommunication.Count == 0);
            Assert.That(_deleteCommunication.Count == 1);
            Assert.That(_deleteCommunication.First().Item1.IdString == "zone_one");
            Assert.That(_deleteCommunication.First().Item2.IdString == "zone_two");
        }

        [Test]
        public async Task ClickZoneElement_NotAllowedInEdit_CommunicationUnchanged()
        {
            // Arrange

            IRenderedComponent<ZonesMatrix> zoneMatrixComponent = await RenderZoneMatrix();
            _networkZoneService.NetworkZones.First().AllowedCommunicationDestinations = [_networkZoneService.NetworkZones.ElementAt(1)];
            _deleteCommunication.Add((_networkZoneService.NetworkZones.First(), _networkZoneService.NetworkZones.ElementAt(1)));
            IElement matrixElement = zoneMatrixComponent.Find("#matrix-element-zone_one-to-zone_two");

            // Act

            matrixElement.Click();

            // Assert

            Assert.That(_addCommunication.Count == 0);
            Assert.That(_deleteCommunication.Count == 0);
        }

        [Test]
        public async Task ClickZoneElement_AllowedInEdit_Unchanged()
        {
            // Arrange

            IRenderedComponent<ZonesMatrix> zoneMatrixComponent = await RenderZoneMatrix();
            _addCommunication.Add((_networkZoneService.NetworkZones.First(), _networkZoneService.NetworkZones.ElementAt(1)));
            IElement matrixElement = zoneMatrixComponent.Find("#matrix-element-zone_one-to-zone_two");

            // Act

            matrixElement.Click();

            // Assert

            Assert.That(_addCommunication.Count == 0);
            Assert.That(_deleteCommunication.Count == 0);
        }


        

//$"matrix-element-{sourceZone.IdString.Replace(" ", "")}-to-{destinationZone.IdString.Replace(" ", "")}"
    }

}