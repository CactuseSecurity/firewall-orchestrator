using FWO.Config.Api;
using FWO.Config.Api.DeviceSpecificSettings;
using FWO.Data.DeviceSpecificSettings;
using NUnit.Framework;

namespace FWO.Test;

/// <summary>
/// Covers writing the changes of several levels with one request and reading all nodes with their values.
/// </summary>
[TestFixture]
internal class DeviceSpecificSettingsManagerChangeSetsTest
{
    [Test]
    public async Task ChangesOfSeveralLevelsAreWrittenWithOneRequest()
    {
        InMemoryDeviceSpecificSettingsApiConnection api = CreateApi();
        DeviceSpecificSettingsManager manager = new(api);
        List<DeviceSpecificStoredNode> nodes = await manager.GetAllNodesWithValuesAsync();
        DeviceSpecificSettingsChangeSet global = new DeviceSpecificSettingsChangeSet(nodes[0].Scope)
            .SetValue(DeviceSpecificSettingKeys.RequestMaxAttempts, 5)
            .Remove(DeviceSpecificSettingKeys.ScUrl);
        DeviceSpecificSettingsChangeSet deviceType = new DeviceSpecificSettingsChangeSet(nodes[1].Scope)
            .SetValue(DeviceSpecificSettingKeys.InstallOn, ProvisioningInstallOn.GatewaysInPath);

        await manager.ApplyChangeSetsAsync([global, deviceType, new DeviceSpecificSettingsChangeSet(nodes[1].Scope)]);

        Assert.Multiple(() =>
        {
            Assert.That(api.BulkPatchCallCount, Is.EqualTo(1));
            Assert.That(api.ReadValue(1, DeviceSpecificSettingKeys.RequestMaxAttempts)?.ToString(), Is.EqualTo("5"));
            Assert.That(api.ReadValue(1, DeviceSpecificSettingKeys.ScUrl), Is.Null);
            Assert.That(api.ReadValue(2, DeviceSpecificSettingKeys.InstallOn)?.ToString(), Is.EqualTo("gatewaysInPath"));
            Assert.That(nodes[0].Values.Keys, Is.EquivalentTo(new List<DeviceSpecificSettingKey> { DeviceSpecificSettingKeys.ScUrl }));
        });
    }

    [Test]
    public async Task WithoutRemovalsTheDeleteMatchesNoRow()
    {
        InMemoryDeviceSpecificSettingsApiConnection api = CreateApi();
        DeviceSpecificSettingsManager manager = new(api);
        DeviceSpecificSettingsScope scope = (await manager.GetAllNodesWithValuesAsync())[0].Scope;

        await manager.ApplyChangeSetsAsync([new DeviceSpecificSettingsChangeSet(scope).SetValue(DeviceSpecificSettingKeys.RequestMaxAttempts, 5)]);

        Dictionary<string, object> removals = (Dictionary<string, object>)api.LastBulkRemovals!;
        Assert.Multiple(() =>
        {
            Assert.That(removals.Keys, Is.EqualTo(new List<string> { "node_id" }), "no _or list reaches the delete");
            Assert.That((List<long>)((Dictionary<string, object>)removals["node_id"])["_in"], Is.Empty);
            Assert.That(api.ReadValue(1, DeviceSpecificSettingKeys.ScUrl), Is.Not.Null);
        });
    }

    [Test]
    public async Task EmptyChangeSetsSendNothing()
    {
        InMemoryDeviceSpecificSettingsApiConnection api = CreateApi();
        DeviceSpecificSettingsManager manager = new(api);
        DeviceSpecificSettingsScope scope = (await manager.GetAllNodesWithValuesAsync())[0].Scope;

        await manager.ApplyChangeSetsAsync([new DeviceSpecificSettingsChangeSet(scope)]);

        Assert.That(api.BulkPatchCallCount, Is.Zero);
    }

    [Test]
    public void AChangeSetOfAnUnpersistedLevelIsRejected()
    {
        DeviceSpecificSettingsManager manager = new(CreateApi());
        DeviceSpecificSettingsScope unpersisted = new() { ScopeType = DeviceSpecificScopeType.Management, ObjectKey = "5", ParentNodeId = 2 };

        Assert.ThrowsAsync<ArgumentException>(() => manager.ApplyChangeSetsAsync(
            [new DeviceSpecificSettingsChangeSet(unpersisted).SetValue(DeviceSpecificSettingKeys.ImporterHostName, "host")]));
    }

    [Test]
    public void SetValueChecksTypeAndLevel()
    {
        DeviceSpecificSettingsChangeSet changes = new(new DeviceSpecificSettingsScope { ScopeType = DeviceSpecificScopeType.Gateway, ObjectKey = "1" });

        Assert.Multiple(() =>
        {
            Assert.Throws<ArgumentException>(() => changes.SetValue(DeviceSpecificSettingKeys.InstallOn, "ANY"));
            Assert.Throws<ArgumentException>(() => changes.SetValue(DeviceSpecificSettingKeys.ScUrl, "https://sc"));
            Assert.That(changes.SetValue(DeviceSpecificSettingKeys.InstallOn, ProvisioningInstallOn.Any).Upserts, Has.Count.EqualTo(1));
        });
    }

    private static InMemoryDeviceSpecificSettingsApiConnection CreateApi()
    {
        InMemoryDeviceSpecificSettingsApiConnection api = new();
        api.AddNode(1, DeviceSpecificScopeType.Global, "global", displayName: "Global");
        api.SetValue(1, DeviceSpecificSettingKeys.ScUrl, "https://sc");
        api.AddNode(2, DeviceSpecificScopeType.DeviceType, "9", 1, "Check Point R8x");
        return api;
    }
}
