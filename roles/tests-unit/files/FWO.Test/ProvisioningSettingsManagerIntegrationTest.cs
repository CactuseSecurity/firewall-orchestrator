using FWO.Api.Client;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Config.File;
using FWO.Data;
using FWO.Data.Provisioning;
using FWO.Middleware.Server;
using FWO.Test.Helpers;
using NUnit.Framework;

namespace FWO.Test;

/// <summary>
/// Live Hasura smoke coverage for the provisioning settings schema. The test is
/// deliberately opt-in because it writes a uniquely named node and then removes it.
/// </summary>
[TestFixture]
[NonParallelizable]
[RequiresIntegrationEnvironment]
internal class ProvisioningSettingsManagerIntegrationTest
{
    private const string DeleteSmokeNode = """
        mutation deleteProvisioningSettingsSmokeNode($nodeType: String!, $objectKey: String!) {
          delete_provisioning_config_node(
            where: {
              node_type: {_eq: $nodeType}
              object_key: {_eq: $objectKey}
            }
          ) {
            affected_rows
          }
        }
        """;

    [TestCase(Roles.FwAdmin)]
    [TestCase(Roles.MiddlewareServer)]
    [Category("Integration")]
    public async Task Manager_RoundTripsJsonbAndUsesProvisioningPermissions(string role)
    {
        string objectKey = $"fwo-integration-smoke-{role}-{Guid.NewGuid():N}";
        string jwt = CreateRoleToken(role);
        using GraphQlApiConnection api = new(ConfigFile.ApiServerUri, jwt);
        ProvisioningSettingsManager manager = new(api);
        Exception? operationFailure = null;

        try
        {
            ProvisioningSettingsLevel<GlobalProvisioningSettings> global =
                await manager.LoadLevelAsync<GlobalProvisioningSettings>(new ProvisioningSettingsScope
                {
                    ScopeType = ProvisioningScopeType.Global,
                    ObjectKey = "global"
                });

            ProvisioningSettingsScope requested = new()
            {
                ScopeType = ProvisioningScopeType.DeviceType,
                ObjectKey = objectKey,
                DisplayName = $"Provisioning integration smoke ({role})",
                ParentNodeId = global.Scope.NodeId,
                SortOrder = int.MaxValue
            };
            ProvisioningSettingsChangeSet initialChanges = new ProvisioningSettingsChangeSet(requested)
                .Set(ProvisioningSettingKeys.ImplementationMode, ProvisioningImplementationMode.Manual)
                .Set(ProvisioningSettingKeys.Logging, ProvisioningLoggingMode.Log)
                .Set(ProvisioningSettingKeys.SecurityProfiles, new List<string> { "Strict", "ScanAll" })
                .Set(ProvisioningSettingKeys.ZoneFrom, "integration-zone");

            ProvisioningSettingsScope persisted = await manager.ApplyChangesAsync(initialChanges);

            // This second node/value upsert exercises the named node constraint and
            // config-value update permission, not only the insert paths.
            persisted = await manager.SetOverrideAsync(
                persisted,
                ProvisioningSettingKeys.Logging,
                ProvisioningLoggingMode.None);

            ProvisioningSettingsLevel<DeviceTypeProvisioningSettings> loaded =
                await manager.LoadLevelAsync<DeviceTypeProvisioningSettings>(persisted);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(persisted.NodeId, Is.GreaterThan(0));
                Assert.That(loaded.Settings.ImplementationMode, Is.EqualTo(ProvisioningImplementationMode.Manual));
                Assert.That(loaded.Settings.Logging, Is.EqualTo(ProvisioningLoggingMode.None));
                Assert.That(loaded.Settings.SecurityProfiles, Is.EqualTo(new[] { "Strict", "ScanAll" }));
                Assert.That(loaded.Settings.ZoneFrom, Is.EqualTo("integration-zone"));
                Assert.That(loaded.DirectOverrides, Does.Contain(ProvisioningSettingKeys.SecurityProfiles));
                Assert.That(loaded.ValueSources[ProvisioningSettingKeys.Logging].Scope?.NodeId, Is.EqualTo(persisted.NodeId));
            }

            await manager.ClearOverrideAsync(persisted, ProvisioningSettingKeys.Logging);
            ProvisioningSettingsLevel<DeviceTypeProvisioningSettings> afterClear =
                await manager.LoadLevelAsync<DeviceTypeProvisioningSettings>(persisted);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(afterClear.DirectOverrides, Does.Not.Contain(ProvisioningSettingKeys.Logging));
                Assert.That(afterClear.DirectOverrides, Does.Contain(ProvisioningSettingKeys.SecurityProfiles));
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
                    $"Provisioning smoke cleanup also failed: {cleanupException.Message}");
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
            Name = "provisioning-integration-smoke",
            DbId = 0,
            Roles = [role]
        }, TimeSpan.FromMinutes(10));
    }
}
