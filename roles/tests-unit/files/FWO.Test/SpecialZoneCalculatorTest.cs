using NUnit.Framework;
using FWO.Compliance;
using FWO.Basics.Interfaces;
using FWO.Data;

namespace FWO.Test
{
    [TestFixture]
    public class SpecialZoneCalculatorTest
    {
        [Test]
        public void CalculateInternetZone_BasicZonesWithOutOverlaps_InternetzoneCalculatedCorrectly()
        {
            // Arrange

            ComplianceNetworkZone networkZone = new();

            SpecialZoneCalculator specialZoneCalculator = new(networkZone);

            // Act

            specialZoneCalculator.CalculateInternetZone();

            Assert.That(false);

        }

    }
}