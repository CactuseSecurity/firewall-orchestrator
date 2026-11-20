using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.DeviceSpecificSettings;
using FWO.Data.Labelling;
using FWO.Data.Modelling;
using FWO.Data.Workflow;
using FWO.ExternalSystems.FirewallWrite;
using FWO.Logging;
using System.Text.Json;

namespace FWO.Services.Modelling
{
    /// <summary>
    /// Variance analysis of managements that segment by labels (Guardicore) and the device specific settings of the
    /// managements. App roles, app zones and network areas are written there as dynamic IP labels, which the import shows
    /// as groups named "key: value" with one member per CIDR block; they are compared by their addresses instead of their
    /// members. Object types the settings exclude are left out of the analysis.
    /// </summary>
    public partial class ModellingVarianceAnalysis
    {
        private const string kLogCategory = "Variance Analysis";
        private static readonly GlobalDeviceSpecificSettings DefaultSettings = new();
        private readonly Dictionary<int, GlobalDeviceSpecificSettings> managementSettings = [];
        private readonly Dictionary<ModellingTypes.ModObjectType, HashSet<string>> currentIdStrings = [];

        /// <summary>
        /// Loads the device specific settings in effect for the relevant managements. If they cannot be loaded, the
        /// defaults apply: no object type is excluded and no network area label is written.
        /// </summary>
        private async Task LoadManagementSettings()
        {
            managementSettings.Clear();
            if (RelevantManagements.Count == 0)
            {
                return;
            }
            try
            {
                List<DeviceSpecificScopePath> paths = [.. RelevantManagements.Select(DeviceSpecificScopePath.ForManagement)];
                List<GlobalDeviceSpecificSettings> settings = await new DeviceSpecificSettingsManager(apiConnection).LoadEffectiveSettingsAsync(paths);
                for (int index = 0; index < settings.Count && index < RelevantManagements.Count; index++)
                {
                    managementSettings[RelevantManagements[index].Id] = settings[index];
                }
            }
            catch (Exception exception)
            {
                Log.WriteWarning(kLogCategory, $"Device specific settings of the managements could not be loaded, their defaults apply: {exception.Message}");
            }
        }

        private GlobalDeviceSpecificSettings SettingsOf(Management mgt) => managementSettings.GetValueOrDefault(mgt.Id) ?? DefaultSettings;

        /// <summary>Whether the device specific settings of a management exclude an object type from the analysis.</summary>
        private bool IsExcluded(Management mgt, ModellingTypes.ModObjectType objectType) => SettingsOf(mgt).IsExcludedFromProvisioning(objectType);

        /// <summary>Whether groups are written as labels to the management.</summary>
        private static bool WritesLabels(Management mgt) => FirewallVendors.WritesLabels(mgt.DeviceType.Id);

        /// <summary>Name of the production group of a modelled app role or app zone: its id string, or its label name.</summary>
        private static string ProdGroupName(ModellingAppRole appRole, Management mgt)
        {
            if (!WritesLabels(mgt))
            {
                return appRole.IdString;
            }
            ModellingTypes.ModObjectType objectType = appRole is ModellingAppZone ? ModellingTypes.ModObjectType.AppZone : ModellingTypes.ModObjectType.AppRole;
            return DynamicIpLabel.ForModelledGroup(objectType, appRole.IdString).Name;
        }

        /// <summary>
        /// Compares the modelled members of a group with the members of its label by their addresses and fills the lists of
        /// new, deleted and unchanged members: label members inside the modelled addresses stay (unchanged), the others are
        /// deleted, and modelled members not covered by the staying ones are new. Label members without valid address are
        /// ignored, as they cannot be an IP criterion.
        /// </summary>
        /// <returns>True if members are to be added or deleted.</returns>
        private bool LabelMembersChanged(List<ModellingAppServerWrapper> modelledMembers, ModellingAppRole prodGroup)
        {
            newAppServers = [];
            deletedAppServers = [];
            unchangedAppServers = [];
            IpAddressSet modelledAddresses = AddressesOf(modelledMembers);
            foreach (ModellingAppServerWrapper prodMember in prodGroup.AppServers)
            {
                if (!TryGetInterval(prodMember.Content, out IpInterval interval))
                {
                    continue;
                }
                if (modelledAddresses.Contains(interval))
                {
                    unchangedAppServers.Add(prodMember);
                }
                else
                {
                    deletedAppServers.Add(prodMember);
                }
            }
            IpAddressSet staying = AddressesOf(unchangedAppServers);
            newAppServers = [.. modelledMembers.Where(member => !TryGetInterval(member.Content, out IpInterval interval) || !staying.Contains(interval))];
            return newAppServers.Count > 0 || deletedAppServers.Count > 0;
        }

        private static IpAddressSet AddressesOf(IEnumerable<ModellingAppServerWrapper> members)
        {
            IpAddressSet addresses = new();
            foreach (ModellingAppServerWrapper member in members)
            {
                if (TryGetInterval(member.Content, out IpInterval interval))
                {
                    addresses.Add(interval);
                }
            }
            return addresses;
        }

        private static bool TryGetInterval(ModellingAppServer appServer, out IpInterval interval) =>
            IpAddressSet.TryParseInterval(appServer.Ip, appServer.IpEnd, out interval);

        /// <summary>
        /// Requests the app zone label of the owner on a management that segments by labels: a missing label is created
        /// with all app servers of the owner, a label with other addresses is changed.
        /// </summary>
        private void AnalyseAppZoneLabelForRequest(ModellingAppZone? appZone, Management mgt)
        {
            if (appZone == null)
            {
                return;
            }
            ModellingAppRole? prodAppZone = ResolveProdAppRole(appZone, mgt);
            if (prodAppZone == null)
            {
                RequestNewAppRole(appZone, mgt);
            }
            else if (LabelMembersChanged(appZone.AppServers, prodAppZone))
            {
                RequestUpdateAppRole(appZone, mgt);
            }
        }

        /// <summary>
        /// Requests the network area labels of the connections on a management that segments by labels, if the device
        /// specific settings implement network area objects and do not exclude network areas: missing labels are created,
        /// labels with other addresses than the area are changed.
        /// </summary>
        private void AnalyseNetworkAreaLabelsForRequest(Management mgt, IEnumerable<ModellingConnection> connections)
        {
            if (!WritesLabels(mgt) || !SettingsOf(mgt).ModellingImplementNetworkAreaObjects || IsExcluded(mgt, ModellingTypes.ModObjectType.NetworkArea))
            {
                return;
            }
            List<ModellingNetworkArea> areas = [.. connections
                .SelectMany(conn => ModellingNetworkAreaWrapper.Resolve(conn.SourceAreas).Concat(ModellingNetworkAreaWrapper.Resolve(conn.DestinationAreas)))
                .DistinctBy(area => area.IdString)
                .OrderBy(area => area.IdString)];
            foreach (ModellingNetworkArea area in areas)
            {
                AnalyseNetworkAreaLabelForRequest(area, mgt);
            }
        }

        private void AnalyseNetworkAreaLabelForRequest(ModellingNetworkArea area, Management mgt)
        {
            List<ModellingAppServerWrapper> areaMembers = [.. NetworkDataWrapper.Resolve(area.IpData)
                .Select(subnet => new ModellingAppServerWrapper { Content = new() { Name = subnet.Name, Ip = subnet.Ip ?? "", IpEnd = subnet.IpEnd ?? "" } })];
            string labelName = DynamicIpLabel.ForModelledGroup(ModellingTypes.ModObjectType.NetworkArea, area.IdString).Name;
            ModellingAppRole? prodGroup = allProdAppRoles.GetValueOrDefault(mgt.Id)?.FirstOrDefault(group => group.Name == labelName);
            if (prodGroup == null)
            {
                newAppServers = areaMembers;
                deletedAppServers = [];
                unchangedAppServers = [];
                TaskList.Add(NetworkAreaLabelTask(area, mgt, WfTaskType.group_create, "new_nw_area_label"));
            }
            else if (LabelMembersChanged(areaMembers, prodGroup))
            {
                TaskList.Add(NetworkAreaLabelTask(area, mgt, WfTaskType.group_modify, "update_nw_area_label"));
            }
        }

        /// <summary>
        /// One task with all members of a network area label: new and unchanged members to keep and deleted ones to remove.
        /// </summary>
        private WfReqTask NetworkAreaLabelTask(ModellingNetworkArea area, Management mgt, WfTaskType taskType, string titleKey)
        {
            List<WfReqElement> members =
            [
                .. LabelMembers(newAppServers, RequestAction.create, area.IdString),
                .. LabelMembers(unchangedAppServers, RequestAction.unchanged, area.IdString),
                .. LabelMembers(deletedAppServers, RequestAction.delete, area.IdString)
            ];
            Dictionary<string, string> addInfo = new()
            {
                { AdditionalInfoKeys.GrpName, area.IdString },
                { AdditionalInfoKeys.GrpType, nameof(ModellingTypes.ModObjectType.NetworkArea) }
            };
            return new()
            {
                Title = userConfig.GetText(titleKey) + area.IdString,
                TaskType = taskType.ToString(),
                RequestAction = taskType == WfTaskType.group_create ? RequestAction.create.ToString() : RequestAction.modify.ToString(),
                ManagementId = mgt.Id,
                OnManagement = mgt,
                Elements = members,
                AdditionalInfo = JsonSerializer.Serialize(addInfo)
            };
        }

        private static IEnumerable<WfReqElement> LabelMembers(List<ModellingAppServerWrapper> members, RequestAction action, string groupName)
        {
            return members.Select(member => new WfReqElement
            {
                RequestAction = action.ToString(),
                Field = ElemFieldType.source.ToString(),
                Name = member.Content.Name,
                IpString = member.Content.Ip,
                IpEnd = member.Content.IpEnd,
                GroupName = groupName
            });
        }

        /// <summary>
        /// Requests the deletion of the app role, app zone and network area labels that were requested for the owner on a
        /// management that segments by labels and still exist there, but whose modelled object does not exist any more,
        /// e.g. after it was deleted or got another id string. Label types the settings exclude or do not implement on the
        /// management (app zones, network areas) are left alone.
        /// </summary>
        private async Task AnalyseObsoleteLabelsForRequest(Management mgt)
        {
            List<ModellingTypes.ModObjectType> labelTypes = ObsoleteLabelTypes(mgt);
            if (labelTypes.Count == 0 || !allProdAppRoles.TryGetValue(mgt.Id, out List<ModellingAppRole>? prodGroups))
            {
                return;
            }
            try
            {
                HashSet<string> prodGroupNames = [.. prodGroups.Select(group => group.Name)];
                List<DynamicIpLabel> requestedLabels = await LoadRequestedLabels(mgt);
                foreach (ModellingTypes.ModObjectType labelType in labelTypes)
                {
                    HashSet<string> current = await LoadCurrentIdStrings(labelType);
                    List<DynamicIpLabel> obsoleteLabels = [.. requestedLabels
                        .Where(label => label.ObjectType == labelType && !current.Contains(label.Value) && prodGroupNames.Contains(label.Name))
                        .OrderBy(label => label.Value, StringComparer.Ordinal)];
                    DeleteObjectTasksList.AddRange(obsoleteLabels.Select(label => ObsoleteLabelTask(label, mgt)));
                }
            }
            catch (Exception exception)
            {
                Log.WriteError(kLogCategory, $"Obsolete labels on management {mgt.Name} could not be determined.", exception);
            }
        }

        /// <summary>
        /// The label types whose obsolete labels are deleted on a management. None while the settings do not let the
        /// firewall writer delete objects: the writer would skip the deletion and every run would request it again.
        /// </summary>
        private List<ModellingTypes.ModObjectType> ObsoleteLabelTypes(Management mgt)
        {
            List<ModellingTypes.ModObjectType> labelTypes = [];
            GlobalDeviceSpecificSettings settings = SettingsOf(mgt);
            if (!WritesLabels(mgt) || !settings.FwWriteDeleteObjects)
            {
                return labelTypes;
            }
            labelTypes.Add(ModellingTypes.ModObjectType.AppRole);
            if (settings.ModellingCreateAppZones)
            {
                labelTypes.Add(ModellingTypes.ModObjectType.AppZone);
            }
            if (settings.ModellingImplementNetworkAreaObjects)
            {
                labelTypes.Add(ModellingTypes.ModObjectType.NetworkArea);
            }
            labelTypes.RemoveAll(labelType => IsExcluded(mgt, labelType));
            return labelTypes;
        }

        private WfReqTask ObsoleteLabelTask(DynamicIpLabel label, Management mgt)
        {
            string idString = label.Value;
            string labelName = label.Name;
            List<WfReqElement> elements =
            [
                new() { RequestAction = RequestAction.delete.ToString(), Field = ElemFieldType.source.ToString(), Name = labelName, GroupName = labelName }
            ];
            Dictionary<string, string> addInfo = new()
            {
                { AdditionalInfoKeys.GrpName, idString },
                { AdditionalInfoKeys.GrpType, label.ObjectType?.ToString() ?? "" }
            };
            return new()
            {
                Title = userConfig.GetText("delete_obsolete_label") + labelName,
                TaskType = WfTaskType.group_delete.ToString(),
                RequestAction = RequestAction.delete.ToString(),
                ManagementId = mgt.Id,
                OnManagement = mgt,
                Elements = elements,
                AdditionalInfo = JsonSerializer.Serialize(addInfo)
            };
        }

        /// <summary>The id strings of all modelled objects of a type that are not deleted, of all owners.</summary>
        private async Task<HashSet<string>> LoadCurrentIdStrings(ModellingTypes.ModObjectType objectType)
        {
            if (!currentIdStrings.TryGetValue(objectType, out HashSet<string>? idStrings))
            {
                List<ModellingAppRole> groups = await apiConnection.SendQueryAsync<List<ModellingAppRole>>(ModellingQueries.getNwGroupObjects,
                    new { grpType = (int)objectType });
                idStrings = [.. groups.Select(group => group.IdString)];
                currentIdStrings[objectType] = idStrings;
            }
            return idStrings;
        }

        /// <summary>
        /// The app role, app zone and network area labels that group_create tasks requested for the owner on a management,
        /// each once.
        /// </summary>
        private async Task<List<DynamicIpLabel>> LoadRequestedLabels(Management mgt)
        {
            List<WfReqTask> tasks = await apiConnection.SendQueryAsync<List<WfReqTask>>(RequestQueries.getRequestedGroupsOfOwner,
                new { ownerId = owner.Id, mgmId = mgt.Id });
            return [.. tasks
                .Select(task => DynamicIpLabel.FromGroupName(task.GetAddInfoValue(AdditionalInfoKeys.GrpName),
                    DynamicIpLabel.ParseObjectType(task.GetAddInfoValue(AdditionalInfoKeys.GrpType))))
                .OfType<DynamicIpLabel>()
                .Where(label => label.ObjectType != null)
                .Distinct()];
        }
    }
}
