using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics.Exceptions;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.DeviceSpecificSettings;
using FWO.Data.Workflow;

namespace FWO.ExternalSystems.FirewallWrite
{
    /// <summary>Loads the target of an implementation task.</summary>
    public interface IFirewallWriteDataSource
    {
        /// <summary>Returns management, gateway and the device specific settings in effect for the task's target.</summary>
        /// <exception cref="ConfigException">Thrown if the task has no management or the management or gateway does not exist.</exception>
        Task<FirewallWriteContext> LoadContext(WfImplTask implTask);
    }

    /// <summary>
    /// Loads the target through the API: the management with its export credentials and gateways, and the device
    /// specific settings resolved along Global, device type, management and gateway.
    /// </summary>
    public sealed class ApiFirewallWriteDataSource(ApiConnection apiConnection) : IFirewallWriteDataSource
    {
        /// <inheritdoc/>
        public async Task<FirewallWriteContext> LoadContext(WfImplTask implTask)
        {
            ArgumentNullException.ThrowIfNull(implTask);
            if (implTask.ManagementId == null)
            {
                throw new ConfigException($"Implementation task {implTask.Id} has no management.");
            }

            List<Management> managements = await apiConnection.SendQueryAsync<List<Management>>(DeviceQueries.getManagementById, new { id = implTask.ManagementId.Value });
            Management management = managements.FirstOrDefault()
                ?? throw new ConfigException($"Management {implTask.ManagementId} of implementation task {implTask.Id} not found.");
            Device? gateway = null;
            if (implTask.DeviceId != null)
            {
                gateway = management.Devices.FirstOrDefault(device => device.Id == implTask.DeviceId)
                    ?? throw new ConfigException($"Gateway {implTask.DeviceId} of implementation task {implTask.Id} does not belong to management {management.Id}.");
            }

            DeviceSpecificScopePath path = gateway == null
                ? DeviceSpecificScopePath.ForManagement(management)
                : DeviceSpecificScopePath.ForGateway(management, gateway);
            List<DeviceSpecificScopePath> paths = [path];
            List<GlobalDeviceSpecificSettings> settings = await new DeviceSpecificSettingsManager(apiConnection).LoadEffectiveSettingsAsync(paths);
            return new FirewallWriteContext { Management = management, Gateway = gateway, Settings = settings[0] };
        }
    }
}
