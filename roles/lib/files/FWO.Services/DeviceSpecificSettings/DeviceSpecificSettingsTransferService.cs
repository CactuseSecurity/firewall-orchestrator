using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.DeviceSpecificSettings;

namespace FWO.Services.DeviceSpecificSettings;

/// <summary>
/// Exports the device specific settings of this installation as a portable package and imports a package: validates it
/// completely, previews the changes and writes all of them in one transaction.
/// </summary>
public sealed class DeviceSpecificSettingsTransferService
{
    private readonly ApiConnection apiConnection;
    private readonly DeviceSpecificSettingsManager manager;
    private readonly string fwoVersion;
    private readonly TimeProvider timeProvider;

    /// <summary>Creates the service.</summary>
    /// <param name="apiConnection">Connection used to read device types, managements and credentials.</param>
    /// <param name="manager">Reads and writes the settings; uses its own connection.</param>
    /// <param name="fwoVersion">FWO version written into exported packages.</param>
    /// <param name="timeProvider">Clock for the export time; the system clock if null.</param>
    public DeviceSpecificSettingsTransferService(ApiConnection apiConnection, DeviceSpecificSettingsManager manager, string fwoVersion,
        TimeProvider? timeProvider = null)
    {
        this.apiConnection = apiConnection ?? throw new ArgumentNullException(nameof(apiConnection));
        this.manager = manager ?? throw new ArgumentNullException(nameof(manager));
        this.fwoVersion = fwoVersion ?? "";
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>Exports the stored settings; see <see cref="DeviceSpecificSettingsExporter.Export"/>.</summary>
    public async Task<DeviceSpecificSettingsExportResult> ExportAsync()
    {
        DeviceSpecificSettingsTransferContext context = await LoadContextAsync();
        return DeviceSpecificSettingsExporter.Export(context, timeProvider.GetUtcNow());
    }

    /// <summary>
    /// Validates a package against the current state of this installation and, unless it is a dry run, applies it.
    /// Nothing is written if the package has an error. The returned plan describes the changes made (or, for a dry run,
    /// the changes that would be made) or lists the errors.
    /// </summary>
    /// <param name="package">The package.</param>
    /// <param name="mode">Merge or replace.</param>
    /// <param name="dryRun">True to only preview the changes.</param>
    public async Task<DeviceSpecificSettingsImportPlan> ImportAsync(DeviceSpecificSettingsPackage? package,
        DeviceSpecificSettingsImportMode mode, bool dryRun)
    {
        DeviceSpecificSettingsTransferContext context = await LoadContextAsync();
        DeviceSpecificSettingsImportPlanResult result = DeviceSpecificSettingsImportPlanner.Plan(package, mode, context);
        if (!dryRun && !result.Plan.HasErrors && result.Writes.Count > 0)
        {
            await WriteAsync(result.Writes, context);
        }
        return result.Plan;
    }

    /// <summary>Loads the objects of this installation the packages are mapped against.</summary>
    public async Task<DeviceSpecificSettingsTransferContext> LoadContextAsync()
    {
        List<DeviceType> deviceTypes = await apiConnection.SendQueryAsync<List<DeviceType>>(DeviceQueries.getDeviceTypeDetails) ?? [];
        List<Management> managements = await apiConnection.SendQueryAsync<List<Management>>(DeviceQueries.getManagementDetailsWithoutSecrets) ?? [];
        List<Credential> credentials = await apiConnection.SendQueryAsync<List<Credential>>(DeviceQueries.getCredentialsWithoutSecrets) ?? [];
        return new DeviceSpecificSettingsTransferContext(deviceTypes, managements, credentials,
            await manager.GetAllNodesWithValuesAsync(), fwoVersion);
    }

    /// <summary>
    /// Places the levels that get new values along their current device hierarchy, creating missing levels and moving
    /// misplaced ones like the settings page does, then writes the values of all levels with one request.
    /// </summary>
    private async Task WriteAsync(List<DeviceSpecificSettingsNodeWrite> writes, DeviceSpecificSettingsTransferContext context)
    {
        Dictionary<(DeviceSpecificScopeType, string), DeviceSpecificSettingsScope> persisted =
            context.StoredNodes.ToDictionary(node => (node.Scope.ScopeType, node.Scope.ObjectKey), node => node.Scope);

        List<DeviceSpecificSettingsChangeSet> changeSets = [];
        foreach (DeviceSpecificSettingsNodeWrite write in writes)
        {
            DeviceSpecificSettingsScope scope = write.Upserts.Count > 0
                ? await PlaceAlongPathAsync(write.Path, persisted)
                : persisted[(write.Path.ScopeType, write.Path.ObjectKey)];
            DeviceSpecificSettingsChangeSet changes = new(scope);
            foreach ((DeviceSpecificSettingKey key, object value) in write.Upserts)
            {
                changes.SetValue(key, value);
            }
            foreach (DeviceSpecificSettingKey key in write.Removals)
            {
                changes.Remove(key);
            }
            changeSets.Add(changes);
        }
        await manager.ApplyChangeSetsAsync(changeSets);
    }

    /// <summary>Makes sure every level of the path is stored below the level above it and returns the scope of the last level.</summary>
    private async Task<DeviceSpecificSettingsScope> PlaceAlongPathAsync(DeviceSpecificScopePath path,
        Dictionary<(DeviceSpecificScopeType, string), DeviceSpecificSettingsScope> persisted)
    {
        long? parentNodeId = null;
        DeviceSpecificSettingsScope? current = null;
        foreach (DeviceSpecificSettingsScope level in path.ToScopes())
        {
            (DeviceSpecificScopeType, string) naturalKey = (level.ScopeType, level.ObjectKey);
            if (!persisted.TryGetValue(naturalKey, out current))
            {
                level.ParentNodeId = parentNodeId;
                current = await manager.EnsureNodeAsync(level);
            }
            else if (current.ParentNodeId != parentNodeId && parentNodeId is long newParentNodeId)
            {
                current = await manager.MoveNodeAsync(level, newParentNodeId);
            }
            persisted[naturalKey] = current;
            parentNodeId = current.NodeId;
        }
        return current!;
    }
}
