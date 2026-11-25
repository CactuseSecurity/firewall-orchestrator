using System.Collections.Frozen;
using System.Globalization;
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
    /// <param name="apiConnection">Api connection.</param>
    /// <param name="commonServicePrefix">Configured prefix of the Guardicore rulesets of common services (modComSvcSectionPrefix).</param>
    public sealed class ApiFirewallWriteDataSource(ApiConnection apiConnection, string? commonServicePrefix = null) : IFirewallWriteDataSource
    {
        private static readonly FrozenSet<string> kGroupTaskTypes =
            [nameof(WfTaskType.group_create), nameof(WfTaskType.group_modify), nameof(WfTaskType.group_delete)];
        private static readonly FrozenSet<string> kRuleTaskTypes =
            [nameof(WfTaskType.access), nameof(WfTaskType.rule_modify), nameof(WfTaskType.rule_delete)];

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
            WfReqTask? requestTask = await LoadRequestTaskInfo(implTask);
            (string? connectionMarker, GuardicoreRulesetApp? app) = await LoadModelledConnection(implTask, requestTask, management);
            bool isGroupTask = kGroupTaskTypes.Contains(implTask.TaskType);
            return new FirewallWriteContext
            {
                Management = management,
                Gateway = gateway,
                Settings = settings[0],
                Rulebases = rulebases,
                RuleLayerUids = ruleLayerUids,
                GroupObjectType = isGroupTask
                    ? DynamicIpLabel.ParseObjectType(requestTask?.GetAddInfoValue(AdditionalInfoKeys.GrpType))
                    : null,
                LabelName = isGroupTask ? NonBlank(requestTask?.GetAddInfoValue(AdditionalInfoKeys.LabelName)) : null,
                FormerLabelName = isGroupTask ? NonBlank(requestTask?.GetAddInfoValue(AdditionalInfoKeys.FormerLabelName)) : null,
                ConnectionMarker = connectionMarker,
                ExtAppId = NonBlank(app?.ExtAppId),
                AppName = NonBlank(app?.Name),
                IsCommonService = app?.IsCommonService ?? false,
                CommonServicePrefix = commonServicePrefix
            };
        }

        /// <summary>
        /// Loads the additional info of the request task of a group or rule task: the modelling object type of a group
        /// (<c>GrpType</c>) and the label it is written as (<c>LabelName</c>, <c>FormerLabelName</c>), and the marker and app of the modelled connection of a rule (<c>ConnMarker</c>, <c>ExtAppId</c>);
        /// null for other tasks and tasks without request task.
        /// </summary>
        private async Task<WfReqTask?> LoadRequestTaskInfo(WfImplTask implTask)
        {
            if ((!kGroupTaskTypes.Contains(implTask.TaskType) && !kRuleTaskTypes.Contains(implTask.TaskType)) || implTask.ReqTaskId <= 0)
            {
                return null;
            }
            List<WfReqTask> requestTasks = await apiConnection.SendQueryAsync<List<WfReqTask>>(RequestQueries.getRequestTaskAdditionalInfo,
                new { id = implTask.ReqTaskId });
            return requestTasks.FirstOrDefault();
        }

        /// <summary>
        /// Loads the modelled connection of a rule task of a management that writes labels, whose writer finds, replaces and
        /// deletes the rules of the connection by its marker: the marker of the request task (<c>ConnMarker</c>) and the
        /// owner of the connection (name and external app id) and whether it is a common service, which name the ruleset of
        /// its rules (see <see cref="GuardicoreRulesets"/>). The request task data can be changed by
        /// several roles, so the marker has to name exactly the connection of the request task (<c>ConnId</c>), and the
        /// connection has to belong to an owner of the request task. Other tasks and tasks without marker have none.
        /// </summary>
        /// <exception cref="FirewallWriteException">Thrown if the marker names another connection or the connection does not
        /// exist or belongs to none of the owners of the request task.</exception>
        private async Task<(string? Marker, GuardicoreRulesetApp? App)> LoadModelledConnection(WfImplTask implTask, WfReqTask? requestTask, Management management)
        {
            string? marker = NonBlank(requestTask?.GetAddInfoValue(AdditionalInfoKeys.ConnMarker));
            if (requestTask == null || marker == null || !kRuleTaskTypes.Contains(implTask.TaskType) || !FirewallVendors.WritesLabels(management.DeviceType.Id))
            {
                return (null, null);
            }
            string connectionIdText = NonBlank(requestTask.GetAddInfoValue(AdditionalInfoKeys.ConnId)) ?? "";
            if (!GuardicoreRules.TryParseMarker(marker, out string markedId) || markedId != connectionIdText
                || !int.TryParse(connectionIdText, NumberStyles.None, CultureInfo.InvariantCulture, out int connectionId))
            {
                throw new FirewallWriteException(FirewallWriteErrorKind.InvalidTaskData,
                    $"Implementation task {implTask.Id}: the connection marker of its request task does not name its connection '{connectionIdText}'.");
            }
            List<ModellingConnection> connections = await apiConnection.SendQueryAsync<List<ModellingConnection>>(ModellingQueries.getConnectionById,
                new { id = connectionId });
            ModellingConnection connection = connections.FirstOrDefault()
                ?? throw new FirewallWriteException(FirewallWriteErrorKind.InvalidTaskData,
                    $"Implementation task {implTask.Id}: the connection {connectionId} of its request task does not exist.");
            if (connection.AppId is not int appId || !requestTask.Owners.Exists(owner => owner.Owner.Id == appId))
            {
                throw new FirewallWriteException(FirewallWriteErrorKind.InvalidTaskData,
                    $"Implementation task {implTask.Id}: the connection {connectionId} belongs to none of the owners of its request task.");
            }
            return (marker, new GuardicoreRulesetApp(connection.App.Name, connection.App.ExtAppId, connection.IsCommonService));
        }

        private static string? NonBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

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
