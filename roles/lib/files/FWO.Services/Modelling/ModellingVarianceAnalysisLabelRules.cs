using FWO.Basics;
using FWO.Data;
using FWO.Data.Labelling;
using FWO.Data.Modelling;
using FWO.Data.Workflow;
using FWO.ExternalSystems.FirewallWrite;

namespace FWO.Services.Modelling
{
    /// <summary>
    /// Compares the sources and destinations of production rules of managements that write labels (Guardicore) with the
    /// modelled connections. Such a rule references app roles, app zones, network areas implemented as labels and other
    /// groups by their label, which the import shows as a group named "key: value", and app servers and network areas
    /// without label by their addresses, which the import shows as host, network or range objects.
    /// </summary>
    public partial class ModellingVarianceAnalysis
    {
        /// <summary>
        /// Compares a rule side of a management that writes labels with the connection side. Labels are compared by their
        /// name: their members are compared by the label variance analysis. Addresses are compared as one set, so it does
        /// not matter how they are split into objects (e.g. a range or its CIDR blocks). Modelled objects the rule misses
        /// are added to the disregarded locations, production objects the connection does not have are marked surplus.
        /// </summary>
        private void CompareLabelWriterLocations(NetworkLocation[] networkLocations, ModellingConnection conn, bool source, Management mgt,
            List<NetworkLocation> disregardedLocations)
        {
            (List<ModellingAppServerWrapper> appServers, List<ModellingAppRoleWrapper> appRoles,
                List<ModellingNetworkAreaWrapper> areas, List<ModellingNwGroupWrapper> otherGroups) = GetNwImplementationConfiguration(conn, source);
            bool areasAsLabels = AreasAsLabels(mgt);
            List<ModellingNetworkArea> modelledAreas = [.. ModellingNetworkAreaWrapper.Resolve(areas)];

            List<NetworkObject> modelledLabels = [.. ModellingAppRoleWrapper.Resolve(appRoles).Select(appRole => LabelObject(ProdGroupName(appRole, mgt)))];
            modelledLabels.AddRange(ModellingNwGroupWrapper.Resolve(otherGroups).Select(group => LabelObject(group.Name)));
            modelledLabels.AddRange(ConnectionLabelGroupNames(conn, source).Select(LabelObject));
            List<NetworkObject> modelledAddresses = [.. ModellingAppServerWrapper.Resolve(appServers).Select(ModellingAppServer.ToNetworkObject)];
            if (areasAsLabels)
            {
                modelledLabels.AddRange(modelledAreas.Select(area => LabelObject(AreaLabel(area).Name)));
            }
            else
            {
                modelledAddresses.AddRange(modelledAreas.SelectMany(AreaNetworks));
            }

            List<NetworkObject> prodLabels = [.. networkLocations.Select(location => location.Object).Where(IsLabelObject)];
            List<NetworkObject> prodAddresses = [.. networkLocations.Select(location => location.Object).Where(obj => !IsLabelObject(obj))];
            CompareLabelNames(modelledLabels, prodLabels, networkLocations, disregardedLocations);
            CompareAddressSets(modelledAddresses, prodAddresses, networkLocations, disregardedLocations);
        }

        /// <summary>
        /// Where the marker of the modelled connection is found in the rules of a management: a management that writes labels
        /// keeps it in the rule comment, as its rule name is the ruleset (app); other managements as configured.
        /// </summary>
        private string MarkerLocationOf(int mgtId) =>
            RelevantManagements.Find(mgt => mgt.Id == mgtId) is Management mgt && WritesLabels(mgt) ? MarkerLocation.Comment : userConfig.ModModelledMarkerLocation;

        /// <summary>
        /// Whether a rule of a management that writes labels lies in another policy section than the one FWO writes its
        /// rules to (Guardicore: Allow), by the name the import gives its rulebase. FWO neither writes nor deletes such rules,
        /// so they are no implementation of a connection even if they carry its marker. A rule without rulebase name counts
        /// as inside.
        /// </summary>
        private static bool IsOutsideWrittenSection(Management mgt, Rule rule)
        {
            string rulebaseName = rule.Rulebase?.Name ?? "";
            return WritesLabels(mgt) && rulebaseName.Length > 0 && !GuardicoreWriteDefaults.IsAllowRulebase(rulebaseName);
        }

        /// <summary>
        /// Whether a rule of a management that writes labels lies in the ruleset the writer puts the rules of the connection
        /// in by the positioning algorithm of the management (see <see cref="GuardicoreRulesets"/>), so that a rule in another
        /// ruleset, e.g. after the algorithm changed, the app was renamed or the connection became a common service, is
        /// requested again and moved. A rule whose ruleset is unknown (no rulebase name) or a connection whose app has no
        /// external app id counts as in place.
        /// </summary>
        private bool IsInTargetRuleset(Rule rule, ModellingConnection conn, Management mgt)
        {
            string? ruleset = GuardicoreRulesets.RulesetOfRulebase(rule.Rulebase?.Name);
            FwoOwner app = string.IsNullOrWhiteSpace(conn.App.ExtAppId) ? owner : conn.App;
            if (ruleset == null || string.IsNullOrWhiteSpace(app.ExtAppId))
            {
                return true;
            }
            GuardicoreRulesetApp rulesetApp = new(app.Name, app.ExtAppId, conn.IsCommonService);
            GuardicoreRulesetFormat format = new(userConfig.AppLabelValueFormat, userConfig.ModComSvcSectionPrefix);
            string targetRuleset = GuardicoreRulesets.Name(SettingsOf(mgt).PositioningAlgorithm, rulesetApp, format);
            return ruleset == targetRuleset;
        }

        /// <summary>
        /// Whether a management that writes labels has no rule of the connection. Such a management enforces on the hosts,
        /// so it needs every connection, unlike gateways, which only need the connections whose path leads through them.
        /// </summary>
        private bool IsMissingOnLabelWriter(Management mgt, ModellingConnection conn) =>
            WritesLabels(mgt) && allModelledRules.TryGetValue(mgt.Id, out List<Rule>? rules) && !rules.Exists(rule => rule.ConnId == conn.Id);

        /// <summary>Whether network areas are written as labels to a management that writes labels.</summary>
        private bool AreasAsLabels(Management mgt) =>
            SettingsOf(mgt).ModellingImplementNetworkAreaObjects && !IsExcluded(mgt, ModellingTypes.ModObjectType.NetworkArea);

        /// <summary>
        /// The additional info of a rule task of a modelled connection: the connection, its extra configurations and the
        /// connection marker, by which a management that writes labels finds its rules (the writer checks that the marker
        /// names the connection and takes the ruleset from the owner of the connection).
        /// </summary>
        private Dictionary<string, string> RuleTaskAdditionalInfo(ModellingConnection conn)
        {
            Dictionary<string, string> addInfo = ModellingRequestExtraConfigs.AdditionalInfo(conn);
            addInfo[AdditionalInfoKeys.ConnMarker] = userConfig.ModModelledMarker + conn.Id;
            return addInfo;
        }

        /// <summary>
        /// Requests the network areas of a connection for a management that writes labels: as their label if network areas
        /// are written as labels, else by the addresses of their networks.
        /// </summary>
        private void AnalyseLabelWriterAreasForRequest(ModellingConnection conn, Management mgt)
        {
            bool areasAsLabels = AreasAsLabels(mgt);
            AddLabelWriterAreaElements(conn.SourceAreas, nameof(ElemFieldType.source), areasAsLabels);
            AddLabelWriterAreaElements(conn.DestinationAreas, nameof(ElemFieldType.destination), areasAsLabels);
        }

        private void AddLabelWriterAreaElements(List<ModellingNetworkAreaWrapper> areas, string field, bool areasAsLabels)
        {
            foreach (ModellingNetworkArea area in ModellingNetworkAreaWrapper.Resolve(areas))
            {
                if (areasAsLabels)
                {
                    ReqElements.Add(new()
                    {
                        RequestAction = nameof(RequestAction.create),
                        Field = field,
                        GroupName = AreaLabel(area).Name
                    });
                    continue;
                }
                ReqElements.AddRange(AreaNetworks(area).Select(network => new WfReqElement
                {
                    RequestAction = nameof(RequestAction.create),
                    Field = field,
                    Name = network.Name,
                    IpString = network.IP,
                    IpEnd = network.IpEnd
                }));
            }
        }

        /// <summary>
        /// The AND-groups of the labels of a connection side (e.g. "OS: Linux &amp; Stage: Prod"), as the import of a rule
        /// names its group of ANDed labels; none while label-based modelling is off.
        /// </summary>
        private List<string> ConnectionLabelGroupNames(ModellingConnection conn, bool source) =>
            [.. ConnectionLabelGroups(conn, source).Select(group => group.Name)];

        /// <summary>The AND-groups of the labels of a connection side with their labels; none while label-based modelling is off.</summary>
        private List<LabelAndGroup> ConnectionLabelGroups(ModellingConnection conn, bool source)
        {
            if (!userConfig.LabelBasedModelling)
            {
                return [];
            }
            List<ConnectionLabel> sideLabels = source ? conn.SourceLabels : conn.DestinationLabels;
            return LabelAndGroups.Groups(sideLabels);
        }

        /// <summary>Requests the AND-groups of the labels of a connection for a management that writes labels.</summary>
        private void AnalyseConnectionLabelsForRequest(ModellingConnection conn, Management mgt)
        {
            if (!WritesLabels(mgt))
            {
                return;
            }
            AddLabelGroupElements(ConnectionLabelGroups(conn, true), nameof(ElemFieldType.source));
            AddLabelGroupElements(ConnectionLabelGroups(conn, false), nameof(ElemFieldType.destination));
        }

        /// <summary>
        /// Adds one element per label of each AND-group: the label as name, the group as group name. The labels are passed on
        /// one by one, so a writer never has to split the group name, whose separator may occur in a label value.
        /// </summary>
        private void AddLabelGroupElements(List<LabelAndGroup> groups, string field)
        {
            ReqElements.AddRange(groups.SelectMany(group => group.LabelNames.Select(labelName => new WfReqElement
            {
                RequestAction = nameof(RequestAction.create),
                Field = field,
                Name = labelName,
                GroupName = group.Name
            })));
        }

        /// <summary>
        /// Whether a connection cannot be written to a management that writes no labels, as one of its sides consists of
        /// labels only. Mixed sides are requested with their objects only.
        /// </summary>
        private bool HasLabelOnlySideForObjectWriter(Management mgt, ModellingConnection conn) =>
            !WritesLabels(mgt) && userConfig.LabelBasedModelling
            && ((!conn.SourceFilled() && conn.SourceLabels.Count > 0) || (!conn.DestinationFilled() && conn.DestinationLabels.Count > 0));

        /// <summary>Whether an imported rule object stands for a label: a group named "key: value".</summary>
        private static bool IsLabelObject(NetworkObject networkObject) =>
            networkObject.Type.Name == ObjectType.Group && networkObject.Name.Contains(DynamicIpLabel.kKeyValueSeparator, StringComparison.Ordinal);

        private static NetworkObject LabelObject(string name) => new() { Name = name, Type = new() { Name = ObjectType.Group } };

        private static IEnumerable<NetworkObject> AreaNetworks(ModellingNetworkArea area) =>
            area.IpData.Select(network => new NetworkObject
            {
                Name = network.Content.Name,
                IP = network.Content.Ip,
                IpEnd = network.Content.IpEnd,
                Type = new() { Name = ObjectType.Network }
            });

        /// <summary>
        /// Compares the label names of a rule side. A production label of an app role, app zone or network area whose value
        /// stands for the id of a modelled one counts as that label, even if it has another name or format: the rule
        /// references the label by its id, which the rename of the label keeps.
        /// </summary>
        private void CompareLabelNames(List<NetworkObject> modelledLabels, List<NetworkObject> prodLabels, NetworkLocation[] networkLocations,
            List<NetworkLocation> disregardedLocations)
        {
            Dictionary<(string Key, string Id), string> modelledNamesById = ModelledLabelNamesById(modelledLabels);
            HashSet<string> prodNames = [.. prodLabels.Select(label => ComparedLabelName(label.Name, modelledNamesById))];
            HashSet<string> modelledNames = [.. modelledLabels.Select(label => label.Name)];
            disregardedLocations.AddRange(modelledLabels.Where(label => !prodNames.Contains(label.Name))
                .DistinctBy(label => label.Name).Select(label => new NetworkLocation(new(), label)));
            foreach (NetworkObject surplus in prodLabels.Where(label => !modelledNames.Contains(ComparedLabelName(label.Name, modelledNamesById))))
            {
                MarkSurplus(networkLocations, surplus);
            }
        }

        /// <summary>The names of the modelled app role, app zone and network area labels by key and the id in their value.</summary>
        private Dictionary<(string Key, string Id), string> ModelledLabelNamesById(List<NetworkObject> modelledLabels)
        {
            Dictionary<(string Key, string Id), string> namesById = [];
            foreach (NetworkObject modelledLabel in modelledLabels)
            {
                DynamicIpLabel? label = DynamicIpLabel.FromName(modelledLabel.Name);
                string? id = label?.ObjectType == null ? null : GroupLabelFormat.IdCandidates(label.Value).FirstOrDefault();
                if (label != null && id != null)
                {
                    namesById.TryAdd((label.Key, id), modelledLabel.Name);
                }
            }
            return namesById;
        }

        /// <summary>The name a production label is compared by: the name of the modelled label its value stands for, else its own name.</summary>
        private string ComparedLabelName(string prodName, Dictionary<(string Key, string Id), string> modelledNamesById)
        {
            DynamicIpLabel? label = DynamicIpLabel.FromName(prodName);
            if (label?.ObjectType == null || modelledNamesById.ContainsValue(prodName))
            {
                return prodName;
            }
            foreach (string id in GroupLabelFormat.IdCandidates(label.Value))
            {
                if (modelledNamesById.TryGetValue((label.Key, id), out string? modelledName))
                {
                    return modelledName;
                }
            }
            return prodName;
        }

        /// <summary>
        /// Compares the addresses of the modelled objects with those of the production objects as sets. A modelled object
        /// is missing if the production objects do not hold all its addresses, a production object is surplus if the
        /// modelled objects do not hold all of its addresses. Objects without valid address are missing or surplus.
        /// </summary>
        private static void CompareAddressSets(List<NetworkObject> modelledObjects, List<NetworkObject> prodObjects, NetworkLocation[] networkLocations,
            List<NetworkLocation> disregardedLocations)
        {
            IpAddressSet modelledAddresses = AddressesOfObjects(modelledObjects);
            IpAddressSet prodAddresses = AddressesOfObjects(prodObjects);
            disregardedLocations.AddRange(modelledObjects.Where(obj => !IsCoveredBy(obj, prodAddresses))
                .Select(obj => new NetworkLocation(new(), obj)));
            foreach (NetworkObject surplus in prodObjects.Where(obj => !IsCoveredBy(obj, modelledAddresses)))
            {
                MarkSurplus(networkLocations, surplus);
            }
        }

        private static IpAddressSet AddressesOfObjects(IEnumerable<NetworkObject> networkObjects)
        {
            IpAddressSet addresses = new();
            foreach (IpInterval interval in networkObjects.SelectMany(AddressIntervals).OfType<IpInterval>())
            {
                addresses.Add(interval);
            }
            return addresses;
        }

        private static bool IsCoveredBy(NetworkObject networkObject, IpAddressSet addresses)
        {
            List<IpInterval?> intervals = AddressIntervals(networkObject);
            return intervals.Count > 0 && intervals.TrueForAll(interval => interval is IpInterval valid && addresses.Contains(valid));
        }

        /// <summary>
        /// The addresses of an object: its own address, or those of its members for a group (e.g. a range the import
        /// splits into CIDR blocks). An entry is null for an address that cannot be parsed.
        /// </summary>
        private static List<IpInterval?> AddressIntervals(NetworkObject networkObject)
        {
            if (networkObject.Type.Name != ObjectType.Group)
            {
                return [IpAddressSet.TryParseInterval(networkObject.IP, networkObject.IpEnd, out IpInterval interval) ? interval : null];
            }
            return [.. GetResolvedGroupMembers(networkObject).SelectMany(AddressIntervals)];
        }
    }
}
