using FWO.Compliance;
using FWO.Data;
using NetTools;
using NUnit.Framework;
using System.Net;

namespace FWO.Test;

[TestFixture]
internal class ComplianceZoneResolverTest
{
    [Test]
    public void ResolveZones_AddsSyntheticInternetFallbackWhenAutoCalculationIsDisabled()
    {
        List<ComplianceNetworkZone> result = ComplianceZoneResolver.ResolveZones(
            [new IPAddressRange(IPAddress.Parse("203.0.113.10"), IPAddress.Parse("203.0.113.10"))],
            [
                new ComplianceNetworkZone
                {
                    Id = 10,
                    Name = "DMZ",
                    IPRanges = [new IPAddressRange(IPAddress.Parse("10.0.0.1"), IPAddress.Parse("10.0.0.1"))]
                }
            ],
            autoCalculatedInternetZoneActive: false,
            internetLocalZoneName: "Internet/Local");

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(result[0].Id, Is.LessThanOrEqualTo(0));
            Assert.That(result[0].Name, Is.EqualTo("Internet/Local"));
            Assert.That(result[0].IPRanges, Is.Empty);
        });
    }
}
