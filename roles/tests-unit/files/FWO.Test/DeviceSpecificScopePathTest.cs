using FWO.Data;
using FWO.Data.DeviceSpecificSettings;
using NUnit.Framework;

namespace FWO.Test;

/// <summary>
/// Covers building device specific settings scope paths from the current device hierarchy.
/// </summary>
[TestFixture]
[Parallelizable]
internal class DeviceSpecificScopePathTest
{
    private static readonly List<DeviceSpecificScopeType> kGatewayLevels =
        [DeviceSpecificScopeType.Global, DeviceSpecificScopeType.DeviceType, DeviceSpecificScopeType.Management, DeviceSpecificScopeType.Gateway];
    private static readonly List<string> kGatewayObjectKeys = ["global", "9", "100", "200"];
    private static readonly List<string> kGatewayDisplayNames = ["Global", "Check Point R8x", "cp-mgr", "cp-gw"];

    [Test]
    public void ForGateway_RunsFromGlobalThroughTheManagementsCurrentDeviceType()
    {
        Management management = CheckPointManagement();

        DeviceSpecificScopePath path = DeviceSpecificScopePath.ForGateway(management, management.Devices[0]);
        List<DeviceSpecificSettingsScope> scopes = path.ToScopes();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(scopes.Select(scope => scope.ScopeType), Is.EqualTo(kGatewayLevels));
            Assert.That(scopes.Select(scope => scope.ObjectKey), Is.EqualTo(kGatewayObjectKeys));
            Assert.That(scopes.Select(scope => scope.DisplayName), Is.EqualTo(kGatewayDisplayNames));
            Assert.That(scopes.All(scope => scope.NodeId == 0 && scope.ParentNodeId == null), Is.True);
            Assert.That(path.ScopeType, Is.EqualTo(DeviceSpecificScopeType.Gateway));
            Assert.That(path.ObjectKey, Is.EqualTo("200"));
        }
    }

    [Test]
    public void ForManagement_FollowsAChangedDeviceType()
    {
        Management management = CheckPointManagement();
        management.DeviceType = new DeviceType { Id = 13, Name = "Check Point", Version = "MDS R8x" };

        List<DeviceSpecificSettingsScope> scopes = DeviceSpecificScopePath.ForManagement(management).ToScopes();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(scopes, Has.Count.EqualTo(3));
            Assert.That(scopes[1].ObjectKey, Is.EqualTo("13"));
            Assert.That(scopes[2].ObjectKey, Is.EqualTo("100"));
        }
    }

    [Test]
    public void GlobalAndDeviceTypePaths_HaveTheirLevelsOnly()
    {
        List<DeviceSpecificSettingsScope> global = DeviceSpecificScopePath.Global().ToScopes();
        List<DeviceSpecificSettingsScope> deviceType = DeviceSpecificScopePath.ForDeviceType(CheckPointManagement().DeviceType).ToScopes();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(global, Has.Count.EqualTo(1));
            Assert.That(global[0].ObjectKey, Is.EqualTo(DeviceSpecificScopePath.GlobalObjectKey));
            Assert.That(deviceType, Has.Count.EqualTo(2));
            Assert.That(deviceType[1].ScopeType, Is.EqualTo(DeviceSpecificScopeType.DeviceType));
        }
    }

    [Test]
    public void ForGateway_AcceptsAGatewayReferencingTheManagement()
    {
        Management management = CheckPointManagement();
        Device gateway = new() { Id = 201, Name = "cp-gw-b", Management = management };

        Assert.That(DeviceSpecificScopePath.ForGateway(management, gateway).ObjectKey, Is.EqualTo("201"));
    }

    [Test]
    public void ForGateway_RejectsAGatewayOfAnotherManagement()
    {
        Management management = CheckPointManagement();
        Device foreignGateway = new() { Id = 300, Name = "other-gw", Management = new Management { Id = 101 } };

        Assert.Throws<ArgumentException>(() => DeviceSpecificScopePath.ForGateway(management, foreignGateway));
    }

    [Test]
    public void ToScopes_ReturnsNewScopesEveryTime()
    {
        DeviceSpecificScopePath path = DeviceSpecificScopePath.ForManagement(CheckPointManagement());

        path.ToScopes()[1].ObjectKey = "tampered";

        Assert.That(path.ToScopes()[1].ObjectKey, Is.EqualTo("9"));
    }

    private static Management CheckPointManagement()
    {
        Device gateway = new() { Id = 200, Name = "cp-gw" };
        return new Management
        {
            Id = 100,
            Name = "cp-mgr",
            DeviceType = new DeviceType { Id = 9, Name = "Check Point", Version = "R8x" },
            Devices = [gateway]
        };
    }
}
