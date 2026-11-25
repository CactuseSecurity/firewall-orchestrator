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
    /// members. Object types the settings exclude are left out of the analysis. The label values have the configured
    /// format (see <see cref="LabelValueFormat"/>); an existing label is found by the id in its value, so that a label of a
    /// renamed group or of a former format is renamed instead of created again.
    /// </summary>
    public partial class ModellingVarianceAnalysis
    {
        private const string kLogCategory = "Variance Analysis";
        private static readonly GlobalDeviceSpecificSettings DefaultSettings = new();
        private readonly Dictionary<int, GlobalDeviceSpecificSettings> managementSettings = [];
        private readonly Dictionary<ModellingTypes.ModObjectType, HashSet<string>> currentIdStrings = [];
        // the label groups of a management by label key and the ids their values may stand for
        private readonly Dictionary<int, Dictionary<(string Key, string Id), List<ModellingAppRole>>> prodLabelsById = [];
        private LabelValueFormat? groupLabelFormat;

        /// <summary>The configured format of the values of app role, app zone and network area labels.</summary>
        private LabelValueFormat GroupLabelFormat => groupLabelFormat ??= new(userConfig.GroupLabelValueFormat);

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

        /// <summary>
        /// Whether the services of service groups are requested for the management instead of the groups: as the device
        /// specific setting resolveServiceGroups says, and always for managements that write labels, which know no service groups.
        /// </summary>
        private bool ResolvesServiceGroups(Management mgt) => WritesLabels(mgt) || SettingsOf(mgt).ResolveServiceGroups;

        /// <summary>Name of the production group of a modelled app role or app zone: its id string, or its label name.</summary>
        private string ProdGroupName(ModellingAppRole appRole, Management mgt)
        {
            return WritesLabels(mgt) ? AppRoleLabel(appRole).Name : appRole.IdString;
        }

        /// <summary>The label of a modelled app role or app zone, with the value in the configured format.</summary>
        private DynamicIpLabel AppRoleLabel(ModellingAppRole appRole)
        {
            ModellingTypes.ModObjectType objectType = appRole is ModellingAppZone ? ModellingTypes.ModObjectType.AppZone : ModellingTypes.ModObjectType.AppRole;
            return DynamicIpLabel.ForModelledGroup(objectType, appRole.IdString, appRole.Name, GroupLabelFormat);
        }

        /// <summary>The label of a network area, with the value in the configured format.</summary>
        private DynamicIpLabel AreaLabel(ModellingNetworkArea area)
        {
            return DynamicIpLabel.ForModelledGroup(ModellingTypes.ModObjectType.NetworkArea, area.IdString, area.Name, GroupLabelFormat);
        }

        /// <summary>
        /// The existing label of a modelled group on a management: the label of its name, else a label of its key whose
        /// value stands for its id string (written with a former name or format, or by others), the first by name.
        /// </summary>
        private ModellingAppRole? FindProdLabel(Management mgt, DynamicIpLabel label, string idString)
        {
            List<ModellingAppRole> prodGroups = allProdAppRoles.GetValueOrDefault(mgt.Id) ?? [];
            return prodGroups.FirstOrDefault(group => group.Name == label.Name)
                ?? ProdLabelsById(mgt.Id).GetValueOrDefault((label.Key, idString.Trim()))?.FirstOrDefault();
        }

        /// <summary>All existing labels of a key on a management whose value stands for the id string, ordered by name.</summary>
        private List<ModellingAppRole> FindProdLabels(Management mgt, string key, string idString)
        {
            return ProdLabelsById(mgt.Id).GetValueOrDefault((key, idString.Trim())) ?? [];
        }

        private Dictionary<(string Key, string Id), List<ModellingAppRole>> ProdLabelsById(int mgtId)
        {
            if (!prodLabelsById.TryGetValue(mgtId, out Dictionary<(string Key, string Id), List<ModellingAppRole>>? labelsById))
            {
                labelsById = [];
                foreach (ModellingAppRole group in (allProdAppRoles.GetValueOrDefault(mgtId) ?? []).OrderBy(group => group.Name, StringComparer.Ordinal))
                {
                    DynamicIpLabel? label = DynamicIpLabel.FromName(group.Name);
                    if (label?.ObjectType == null)
                    {
                        continue;
                    }
                    foreach (string id in GroupLabelFormat.IdCandidates(label.Value))
                    {
                        if (!labelsById.TryGetValue((label.Key, id), out List<ModellingAppRole>? groups))
                        {
                            groups = [];
                            labelsById[(label.Key, id)] = groups;
                        }
                        groups.Add(group);
                    }
                }
                prodLabelsById[mgtId] = labelsById;
            }
            return labelsById;
        }

        /// <summary>
        /// The additional info naming the label a group task writes and, if the existing label has another name, the label
        /// to rename.
        /// </summary>
        private static void AddLabelInfo(Dictionary<string, string> addInfo, DynamicIpLabel label, ModellingAppRole? prodLabel)
        {
            addInfo[AdditionalInfoKeys.LabelName] = label.Name;
            if (prodLabel != null && prodLabel.Name != label.Name)
            {
                addInfo[AdditionalInfoKeys.FormerLabelName] = prodLabel.Name;
            }
        }

        /// <summary>Whether an existing label has another name than the label of its modelled group, so that it is to be renamed.</summary>
        private static bool IsRenamed(ModellingAppRole? prodLabel, DynamicIpLabel label) => prodLabel != null && prodLabel.Name != label.Name;

        /// <summary>
        /// A task that renames the label of a group whose members are unchanged: it names all members as unchanged, so that
        /// the write only renames the label.
        /// </summary>
        private WfReqTask LabelRenameTask(DynamicIpLabel label, ModellingAppRole prodLabel, Management mgt, List<ModellingAppServerWrapper> members,
            Dictionary<string, string> addInfo)
        {
            string idString = addInfo.GetValueOrDefault(AdditionalInfoKeys.GrpName, "");
            AddLabelInfo(addInfo, label, prodLabel);
            return new()
            {
                Title = RenameTitle(label, prodLabel),
                TaskType = WfTaskType.group_modify.ToString(),
                RequestAction = RequestAction.modify.ToString(),
                ManagementId = mgt.Id,
                OnManagement = mgt,
                Elements = [.. LabelMembers(members, RequestAction.unchanged, idString)],
                AdditionalInfo = JsonSerializer.Serialize(addInfo)
            };
        }

        private string RenameTitle(DynamicIpLabel label, ModellingAppRole prodLabel) => $"{userConfig.GetText("rename_label")}{prodLabel.Name} -> {label.Name}";

        /// <summary>Adds the rename task of a label, unless the same task is requested already.</summary>
        private void RequestLabelRename(DynamicIpLabel label, ModellingAppRole prodLabel, Management mgt, List<ModellingAppServerWrapper> members,
            Dictionary<string, string> addInfo)
        {
            string title = RenameTitle(label, prodLabel);
            if (!TaskList.Exists(task => task.Title == title && task.OnManagement?.Id == mgt.Id))
            {
                TaskList.Add(LabelRenameTask(label, prodLabel, mgt, members, addInfo));
            }
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
            else if (IsRenamed(prodAppZone, AppRoleLabel(appZone)))
            {
                RequestLabelRename(AppRoleLabel(appZone), prodAppZone, mgt, appZone.AppServers, AppRoleAddInfo(appZone, mgt));
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
            DynamicIpLabel label = AreaLabel(area);
            ModellingAppRole? prodGroup = FindProdLabel(mgt, label, area.IdString);
            if (prodGroup == null)
            {
                newAppServers = areaMembers;
                deletedAppServers = [];
                unchangedAppServers = [];
                TaskList.Add(NetworkAreaLabelTask(area, mgt, WfTaskType.group_create, "new_nw_area_label", null));
            }
            else if (LabelMembersChanged(areaMembers, prodGroup))
            {
                TaskList.Add(NetworkAreaLabelTask(area, mgt, WfTaskType.group_modify, "update_nw_area_label", prodGroup));
            }
            else if (IsRenamed(prodGroup, label))
            {
                RequestLabelRename(label, prodGroup, mgt, areaMembers, NetworkAreaAddInfo(area));
            }
        }

        private static Dictionary<string, string> NetworkAreaAddInfo(ModellingNetworkArea area)
        {
            return new()
            {
                { AdditionalInfoKeys.GrpName, area.IdString },
                { AdditionalInfoKeys.GrpType, nameof(ModellingTypes.ModObjectType.NetworkArea) }
            };
        }

        /// <summary>
        /// One task with all members of a network area label: new and unchanged members to keep and deleted ones to remove.
        /// </summary>
        private WfReqTask NetworkAreaLabelTask(ModellingNetworkArea area, Management mgt, WfTaskType taskType, string titleKey, ModellingAppRole? prodLabel)
        {
            List<WfReqElement> members =
            [
                .. LabelMembers(newAppServers, RequestAction.create, area.IdString),
                .. LabelMembers(unchangedAppServers, RequestAction.unchanged, area.IdString),
                .. LabelMembers(deletedAppServers, RequestAction.delete, area.IdString)
            ];
            Dictionary<string, string> addInfo = NetworkAreaAddInfo(area);
            AddLabelInfo(addInfo, AreaLabel(area), prodLabel);
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
        /// e.g. after it was deleted or got another id string. The existing labels are found by the id in their value, so
        /// labels of every name and format the id was written with are deleted. Label types the settings exclude or do not
        /// implement on the management (app zones, network areas) are left alone.
        /// </summary>
        private async Task AnalyseObsoleteLabelsForRequest(Management mgt)
        {
            List<ModellingTypes.ModObjectType> labelTypes = ObsoleteLabelTypes(mgt);
            if (labelTypes.Count == 0 || !allProdAppRoles.ContainsKey(mgt.Id))
            {
                return;
            }
            try
            {
                List<DynamicIpLabel> requestedLabels = await LoadRequestedLabels(mgt);
                foreach (ModellingTypes.ModObjectType labelType in labelTypes)
                {
                    HashSet<string> current = await LoadCurrentIdStrings(labelType);
                    // the existing labels of the obsolete ids, whatever name or format they were written with
                    List<(DynamicIpLabel Requested, ModellingAppRole Existing)> obsoleteLabels = [.. requestedLabels
                        .Where(label => label.ObjectType == labelType && !current.Contains(label.Value))
                        .SelectMany(label => FindProdLabels(mgt, label.Key, label.Value).Select(existing => (label, existing)))
                        .DistinctBy(obsolete => obsolete.existing.Name)
                        .OrderBy(obsolete => obsolete.existing.Name, StringComparer.Ordinal)];
                    DeleteObjectTasksList.AddRange(obsoleteLabels.Select(obsolete => ObsoleteLabelTask(obsolete.Requested, obsolete.Existing, mgt)));
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

        private WfReqTask ObsoleteLabelTask(DynamicIpLabel label, ModellingAppRole existingLabel, Management mgt)
        {
            string idString = label.Value;
            string labelName = existingLabel.Name;
            List<WfReqElement> elements =
            [
                new() { RequestAction = RequestAction.delete.ToString(), Field = ElemFieldType.source.ToString(), Name = labelName, GroupName = labelName }
            ];
            Dictionary<string, string> addInfo = new()
            {
                { AdditionalInfoKeys.GrpName, idString },
                { AdditionalInfoKeys.GrpType, label.ObjectType?.ToString() ?? "" },
                { AdditionalInfoKeys.LabelName, labelName }
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
