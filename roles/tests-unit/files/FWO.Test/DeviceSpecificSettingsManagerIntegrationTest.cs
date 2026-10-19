using FWO.Api.Client;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Config.File;
using FWO.Data;
using FWO.Data.DeviceSpecificSettings;
using FWO.Middleware.Server;
using FWO.Test.Helpers;
using NUnit.Framework;

namespace FWO.Test;

/// <summary>
/// Live Hasura smoke coverage for the device specific settings schema. The test is
/// deliberately opt-in because it writes a uniquely named node and then removes it.
/// </summary>
[TestFixture]
[NonParallelizable]
[RequiresIntegrationEnvironment]
internal class DeviceSpecificSettingsManagerIntegrationTest
{
    private static readonly string[] kExpectedSecurityProfiles = ["Strict", "ScanAll"];

    /// <summary>Smoke device types use IDs far above any real device type, so they never meet a real node.</summary>
    private const int kSmokeDeviceTypeIdMin = 1_000_000_000;

    private const string DeleteSmokeNode = """
        mutation deleteDeviceSpecificSettingsSmokeNode($nodeType: String!, $objectKey: String!) {
          delete_device_specific_config_node(
            where: {
              node_type: {_eq: $nodeType}
              object_key: {_eq: $objectKey}
            }
          ) {
            affected_rows
          }
        }
        """;

    [TestCase(Roles.Admin)]
    [TestCase(Roles.MiddlewareServer)]
    [Category("Integration")]
    public async Task Manager_RoundTripsJsonbAndUsesDeviceSpecificSettingsPermissions(string role)
    {
        DeviceType smokeDeviceType = new()
        {
            Id = Random.Shared.Next(kSmokeDeviceTypeIdMin, int.MaxValue),
            Name = "Device specific settings integration smoke",
            Version = role
        };
        DeviceSpecificScopePath smokePath = DeviceSpecificScopePath.ForDeviceType(smokeDeviceType);
        string objectKey = smokePath.ObjectKey;
        string jwt = CreateRoleToken(role);
        using GraphQlApiConnection api = new(ConfigFile.ApiServerUri, jwt);
        DeviceSpecificSettingsManager manager = new(api);
        Exception? operationFailure = null;

        try
        {
            DeviceSpecificSettingsLevel<GlobalDeviceSpecificSettings> global =
                await manager.LoadLevelAsync<GlobalDeviceSpecificSettings>(DeviceSpecificScopePath.Global());

            DeviceSpecificSettingsScope requested = new()
            {
                ScopeType = DeviceSpecificScopeType.DeviceType,
                ObjectKey = objectKey,
                DisplayName = $"Device specific settings integration smoke ({role})",
                ParentNodeId = global.Scope.NodeId,
                SortOrder = int.MaxValue
            };
            DeviceSpecificSettingsChangeSet initialChanges = new DeviceSpecificSettingsChangeSet(requested)
                .Set(DeviceSpecificSettingKeys.ImplementationMode, ProvisioningImplementationMode.Manual)
                .Set(DeviceSpecificSettingKeys.RuleLoggingOverride, ProvisioningLoggingMode.Log)
                .Set(DeviceSpecificSettingKeys.SecurityProfiles, new List<string> { "Strict", "ScanAll" })
                .Set(DeviceSpecificSettingKeys.RuleZoneFromOverride, "integration-zone");

            DeviceSpecificSettingsScope persisted = await manager.ApplyChangesAsync(initialChanges);

            // This second node/value upsert exercises the named node constraint and
            // config-value update permission, not only the insert paths.
            persisted = await manager.SetOverrideAsync(
                persisted,
                DeviceSpecificSettingKeys.RuleLoggingOverride,
                ProvisioningLoggingMode.None);

            DeviceSpecificSettingsLevel<DeviceTypeDeviceSpecificSettings> loaded =
                await manager.LoadLevelAsync<DeviceTypeDeviceSpecificSettings>(smokePath);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(persisted.NodeId, Is.GreaterThan(0));
                Assert.That(loaded.Settings.ImplementationMode, Is.EqualTo(ProvisioningImplementationMode.Manual));
                Assert.That(loaded.Settings.RuleLoggingOverride, Is.EqualTo(ProvisioningLoggingMode.None));
                Assert.That(loaded.Settings.SecurityProfiles, Is.EqualTo(kExpectedSecurityProfiles));
                Assert.That(loaded.Settings.RuleZoneFromOverride, Is.EqualTo("integration-zone"));
                Assert.That(loaded.DirectOverrides, Does.Contain(DeviceSpecificSettingKeys.SecurityProfiles));
                Assert.That(loaded.ValueSources[DeviceSpecificSettingKeys.RuleLoggingOverride].Scope?.NodeId, Is.EqualTo(persisted.NodeId));
            }

            await manager.ClearOverrideAsync(persisted, DeviceSpecificSettingKeys.RuleLoggingOverride);
            DeviceSpecificSettingsLevel<DeviceTypeDeviceSpecificSettings> afterClear =
                await manager.LoadLevelAsync<DeviceTypeDeviceSpecificSettings>(smokePath);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(afterClear.DirectOverrides, Does.Not.Contain(DeviceSpecificSettingKeys.RuleLoggingOverride));
                Assert.That(afterClear.DirectOverrides, Does.Contain(DeviceSpecificSettingKeys.SecurityProfiles));
            }
        }
        catch (Exception exception)
        {
            operationFailure = exception;
            throw;
        }
        finally
        {
            try
            {
                await api.SendQueryAsync<ReturnId>(DeleteSmokeNode, new
                {
                    nodeType = "device_type",
                    objectKey
                });
            }
            catch (Exception cleanupException) when (operationFailure is not null)
            {
                TestContext.Progress.WriteLine(
                    $"Device specific settings smoke cleanup also failed: {cleanupException.Message}");
            }
        }
    }

    private static string CreateRoleToken(string role)
    {
        JwtWriter writer = new(ConfigFile.JwtPrivateKey);
        if (role == Roles.MiddlewareServer)
        {
            return writer.CreateJWTMiddlewareServer(TimeSpan.FromMinutes(10));
        }

        return writer.CreateJWT(new UiUser
        {
            Name = "device-specific-settings-integration-smoke",
            DbId = 0,
            Roles = [role]
        }, TimeSpan.FromMinutes(10));
    }
}
