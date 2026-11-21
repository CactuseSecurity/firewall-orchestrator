using System.Collections.Frozen;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.DeviceSpecificSettings;
using FWO.Data.Labelling;
using FWO.Data.Modelling;
using FWO.Data.Workflow;

namespace FWO.ExternalSystems.FirewallWrite
{
    /// <summary>Loads the target of an implementation task.</summary>
    public interface IFirewallWriteDataSource
    {
        /// <summary>Returns management, gateway and the device specific settings in effect for the task's target.</summary>
        /// <exception cref="FirewallWriteException">Thrown if the task has no management or the management or gateway does not exist.</exception>
        Task<FirewallWriteContext> LoadContext(WfImplTask implTask);
    }

    /// <summary>
    /// Loads the target through the API: the management with its export credentials and gateways, and the device
    /// specific settings resolved along Global, device type, management and gateway.
    /// </summary>
    public sealed class ApiFirewallWriteDataSource(ApiConnection apiConnection) : IFirewallWriteDataSource
    {
        private static readonly FrozenSet<string> kGroupTaskTypes =
            [nameof(WfTaskType.group_create), nameof(WfTaskType.group_modify), nameof(WfTaskType.group_delete)];

        /// <inheritdoc/>
        public async Task<FirewallWriteContext> LoadContext(WfImplTask implTask)
        {
            ArgumentNullException.ThrowIfNull(implTask);

            Device? gatewayTarget = null;
            int managementId;
            if (implTask.DeviceId is > 0)
            {
                List<Device> gateways = await apiConnection.SendQueryAsync<List<Device>>(
                    DeviceQueries.getImplementationTaskGatewayTarget,
                    new { gatewayId = implTask.DeviceId.Value });
                if (gateways.Count != 1 ||
                    !WfImplTaskTargetManagementResolver.TryResolveManagementId(
                        null,
                        implTask,
                        gateways[0],
                        out managementId))
                {
                    throw new FirewallWriteException(
                        FirewallWriteErrorKind.InvalidTaskData,
                        $"Gateway {implTask.DeviceId} of implementation task {implTask.Id} is unavailable or inconsistent.");
                }
                gatewayTarget = gateways[0];
            }
            else if (!WfImplTaskTargetManagementResolver.TryResolveManagementId(
                null,
                implTask,
                null,
                out managementId))
            {
                throw new FirewallWriteException(
                    FirewallWriteErrorKind.InvalidTaskData,
                    $"Implementation task {implTask.Id} has no target management.");
            }

            List<Management> managements = await apiConnection.SendQueryAsync<List<Management>>(
                DeviceQueries.getManagementById,
                new { id = managementId });
            Management management = managements.FirstOrDefault()
                ?? throw new FirewallWriteException(FirewallWriteErrorKind.InvalidTaskData,
                    $"Management {managementId} of implementation task {implTask.Id} not found.");
            Device? gateway = null;
            if (gatewayTarget != null)
            {
                gateway = management.Devices.FirstOrDefault(device => device.Id == implTask.DeviceId)
                    ?? throw new FirewallWriteException(FirewallWriteErrorKind.InvalidTaskData,
                        $"Gateway {implTask.DeviceId} of implementation task {implTask.Id} does not belong to management {management.Id}.");
            }

            DeviceSpecificScopePath path = gateway == null
                ? DeviceSpecificScopePath.ForManagement(management)
                : DeviceSpecificScopePath.ForGateway(management, gateway);
            List<DeviceSpecificScopePath> paths = [path];
            List<GlobalDeviceSpecificSettings> settings = await new DeviceSpecificSettingsManager(apiConnection).LoadEffectiveSettingsAsync(paths);
            FirewallGatewayRulebases rulebases = gateway == null ? FirewallGatewayRulebases.None : await LoadRulebases(gateway);
            Dictionary<string, string> ruleLayerUids = await LoadRuleLayers(implTask, management);
            return new FirewallWriteContext
            {
                Management = management,
                Gateway = gateway,
                Settings = settings[0],
                Rulebases = rulebases,
                RuleLayerUids = ruleLayerUids,
                GroupObjectType = await LoadGroupObjectType(implTask)
            };
        }

        /// <summary>
        /// Loads the modelling object type the request task of a group task names for its group (additional info
        /// <c>GrpType</c>); other tasks and groups requested without a type have none.
        /// </summary>
        private async Task<ModellingTypes.ModObjectType?> LoadGroupObjectType(WfImplTask implTask)
        {
            if (!kGroupTaskTypes.Contains(implTask.TaskType) || implTask.ReqTaskId <= 0)
            {
                return null;
            }
            List<WfReqTask> requestTasks = await apiConnection.SendQueryAsync<List<WfReqTask>>(RequestQueries.getRequestTaskAdditionalInfo,
                new { id = implTask.ReqTaskId });
            string? objectType = requestTasks.FirstOrDefault()?.GetAddInfoValue(AdditionalInfoKeys.GrpType);
            return DynamicIpLabel.ParseObjectType(objectType);
        }

        /// <summary>
        /// Loads the layers of the existing rules a rule_modify or rule_delete task names from the imported configuration,
        /// only for vendors that address rules in their layer; other tasks name no existing rule.
        /// </summary>
        private async Task<Dictionary<string, string>> LoadRuleLayers(WfImplTask implTask, Management management)
        {
            FirewallVendor vendor = FirewallVendors.FromDeviceTypeId(management.DeviceType.Id);
            if (!WfTaskBase.IsRuleChangeOrDelete(implTask.TaskType) || vendor == FirewallVendor.Unsupported || !FirewallVendorProfile.For(vendor).AddressesRulesInImportedLayer)
            {
                return [];
            }
            int managementId = management.Id;
            List<string> ruleUids = implTask.GetRuleUids();
            if (ruleUids.Count == 0)
            {
                return [];
            }
            // one query per root field: the api connection returns the first root field of a response only
            List<Rule> rules = await apiConnection.SendQueryAsync<List<Rule>>(RuleQueries.getRuleLayers,
                new { managementId, ruleUids, ruleLimit = FirewallRuleLayers.kMaxRules + 1 });
            List<RulebaseLink> sectionLinks = await apiConnection.SendQueryAsync<List<RulebaseLink>>(RuleQueries.getSectionLinks,
                new { managementId, linkLimit = FirewallRuleLayers.kMaxSectionLinks + 1 });
            return FirewallRuleLayers.Resolve(new FirewallRuleLayerQueryResult { Rules = rules, SectionLinks = sectionLinks });
        }

        /// <summary>
        /// Loads the package name and the current rulebase links of the gateway; the management query leaves both out. The
        /// package name is copied to the gateway, where it takes precedence over the package derived from the links.
        /// </summary>
        private async Task<FirewallGatewayRulebases> LoadRulebases(Device gateway)
        {
            List<Device> devices = await apiConnection.SendQueryAsync<List<Device>>(DeviceQueries.getGatewayRulebaseLinks, new { gwId = gateway.Id });
            Device? linked = devices.FirstOrDefault();
            if (linked == null)
            {
                return FirewallGatewayRulebases.None;
            }
            gateway.Package = linked.Package;
            return FirewallGatewayRulebases.FromLinks(linked.RulebaseLinks);
        }
    }
}
