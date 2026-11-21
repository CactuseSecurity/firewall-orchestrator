using FWO.Basics;
using FWO.Data;
using FWO.Data.Labelling;
using FWO.Data.Modelling;

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
            bool areasAsLabels = SettingsOf(mgt).ModellingImplementNetworkAreaObjects;
            List<ModellingNetworkArea> modelledAreas = [.. ModellingNetworkAreaWrapper.Resolve(areas)];

            List<NetworkObject> modelledLabels = [.. ModellingAppRoleWrapper.Resolve(appRoles).Select(appRole => LabelObject(ProdGroupName(appRole, mgt)))];
            modelledLabels.AddRange(ModellingNwGroupWrapper.Resolve(otherGroups).Select(group => LabelObject(group.Name)));
            List<NetworkObject> modelledAddresses = [.. ModellingAppServerWrapper.Resolve(appServers).Select(ModellingAppServer.ToNetworkObject)];
            if (areasAsLabels)
            {
                modelledLabels.AddRange(modelledAreas.Select(area =>
                    LabelObject(DynamicIpLabel.ForModelledGroup(ModellingTypes.ModObjectType.NetworkArea, area.IdString).Name)));
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

        private static void CompareLabelNames(List<NetworkObject> modelledLabels, List<NetworkObject> prodLabels, NetworkLocation[] networkLocations,
            List<NetworkLocation> disregardedLocations)
        {
            HashSet<string> prodNames = [.. prodLabels.Select(label => label.Name)];
            HashSet<string> modelledNames = [.. modelledLabels.Select(label => label.Name)];
            disregardedLocations.AddRange(modelledLabels.Where(label => !prodNames.Contains(label.Name))
                .DistinctBy(label => label.Name).Select(label => new NetworkLocation(new(), label)));
            foreach (NetworkObject surplus in prodLabels.Where(label => !modelledNames.Contains(label.Name)))
            {
                MarkSurplus(networkLocations, surplus);
            }
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
