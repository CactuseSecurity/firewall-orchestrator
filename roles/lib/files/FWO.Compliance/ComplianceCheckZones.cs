using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Basics.Enums;
using FWO.Config.Api;
using FWO.Data;
using FWO.Ui.Display;
using NetTools;
using System.Net;

namespace FWO.Compliance
{
    /// <summary>
    /// Resolves the network objects, IP ranges and network zones a compliance check works on.
    /// </summary>
    public partial class ComplianceCheck
    {
        /// <summary>
        /// Extracts the IP ranges represented by a network object in all supported forms.
        /// </summary>
        /// <param name="networkObject">Network object to parse.</param>
        /// <returns>List of ranges (empty if parsing is not possible).</returns>
        public static List<IPAddressRange> ParseIpRange(NetworkObject networkObject)
        {
            List<IPAddressRange> ranges = [];

            if ((networkObject.Type.Name == ObjectType.IPRange || networkObject.Type.Name == ObjectType.Network)
                && !string.IsNullOrWhiteSpace(networkObject.IP)
                && !string.IsNullOrWhiteSpace(networkObject.IpEnd)
                && (networkObject.Type.Name == ObjectType.IPRange || !string.Equals(networkObject.IP, networkObject.IpEnd, StringComparison.Ordinal)))
            {
                if (IPAddress.TryParse(networkObject.IP.StripOffNetmask(), out IPAddress? ipStart) && IPAddress.TryParse(networkObject.IpEnd.StripOffNetmask(), out IPAddress? ipEnd))
                {
                    ranges.Add(new IPAddressRange(ipStart, ipEnd));
                }
            }
            else if (networkObject.Type.Name != ObjectType.Group && networkObject.ObjectGroupFlats.Length > 0)
            {
                foreach (NetworkObject groupMember in networkObject.ObjectGroupFlats
                    .Select(groupFlat => groupFlat.Object)
                    .OfType<NetworkObject>())
                {
                    ranges.AddRange(ParseIpRange(groupMember));
                }
            }
            else if (networkObject.IP != null)
            {
                // CIDR notation or single (host) IP can be parsed directly
                ranges.Add(IPAddressRange.Parse(networkObject.IP));
            }

            return ranges;
        }

        /// <summary>
        /// Returns a readable representation of a network object including its IP range.
        /// </summary>
        /// <param name="networkObject">Network object to display.</param>
        private string GetNwObjectString(NetworkObject networkObject)
        {
            string networkObjectString = "";

            networkObjectString += networkObject.Name;
            networkObjectString += NwObjDisplay.DisplayIp(networkObject.IP, networkObject.IpEnd, networkObject.Type.Name, true);

            return networkObjectString;
        }

        /// <summary>
        /// Builds a helper structure combining network objects with the IP ranges they represent.
        /// </summary>
        /// <param name="networkObjects">Objects that should be resolved to ranges.</param>
        private static Task<List<(NetworkObject networkObject, List<IPAddressRange> ipRanges)>> GetNetworkObjectsWithIpRanges(List<NetworkObject> networkObjects)
        {
            List<(NetworkObject networkObject, List<IPAddressRange> ipRanges)> networkObjectsWithIpRange = [];

            foreach (NetworkObject networkObject in networkObjects)
            {
                networkObjectsWithIpRange.Add((networkObject, ParseIpRange(networkObject)));
            }

            return Task.FromResult(networkObjectsWithIpRange);
        }

        /// <summary>
        /// Loads all network zones referenced by the policy matrix criterion.
        /// </summary>
        private async Task LoadNetworkZonesAsync()
        {
            if (Policy != null)
            {
                List<int> matrixIds = [.. Policy.Criteria
                    .Where(c => c.Content.CriterionType == CriterionType.Matrix.ToString() && c.Content.Id > 0)
                    .Select(c => c.Content.Id)
                    .Distinct()];

                foreach (int matrixId in matrixIds)
                {
                    Logger.TryWriteInfo("Compliance Check", $"Loading network zones for Matrix {matrixId}.", LocalSettings.ComplianceCheckVerbose);
                    List<ComplianceNetworkZone> networkZones = await _apiConnection.SendQueryAsync<List<ComplianceNetworkZone>>(ComplianceQueries.getNetworkZonesForMatrix, new { criterionId = matrixId });
                    _networkZonesByCriterion[matrixId] = networkZones;
                    Logger.TryWriteInfo("Compliance Check", $"Loaded {networkZones.Count} network zones for Matrix {matrixId}.", LocalSettings.ComplianceCheckVerbose);
                }

                NetworkZones = matrixIds.Count > 0 && _networkZonesByCriterion.TryGetValue(matrixIds[0], out List<ComplianceNetworkZone>? firstMatrixZones)
                    ? firstMatrixZones
                    : [];
            }
        }

        /// <summary>
        /// Loads pre-fetched network zones for the current policy evaluation.
        /// </summary>
        /// <param name="networkZonesByCriterion">Request-scoped network zone cache.</param>
        private void LoadPreloadedNetworkZones(IReadOnlyDictionary<int, List<ComplianceNetworkZone>> networkZonesByCriterion)
        {
            if (Policy == null)
            {
                NetworkZones = [];
                return;
            }

            List<int> matrixIds = [.. Policy.Criteria
                .Where(c => c.Content.CriterionType == CriterionType.Matrix.ToString() && c.Content.Id > 0)
                .Select(c => c.Content.Id)
                .Distinct()];

            foreach (int matrixId in matrixIds)
            {
                if (networkZonesByCriterion.TryGetValue(matrixId, out List<ComplianceNetworkZone>? networkZones))
                {
                    _networkZonesByCriterion[matrixId] = networkZones;
                }
            }

            NetworkZones = matrixIds.Count > 0 && _networkZonesByCriterion.TryGetValue(matrixIds[0], out List<ComplianceNetworkZone>? firstMatrixZones)
                ? firstMatrixZones
                : [];
        }

        /// <summary>
        /// Maps previously resolved IP ranges to their matching compliance zones.
        /// </summary>
        /// <param name="inputData">Pairs of network objects and IP ranges.</param>
        /// <param name="networkZonesForCriterion">Zones configured for the criterion under test.</param>
        /// <param name="notAssessableObjects">Objects holding ranges that no zone of the criterion can cover.</param>
        private List<(NetworkObject networkObject, List<ComplianceNetworkZone> networkZones)> MapZonesToNetworkObjects(
            List<(NetworkObject networkObject, List<IPAddressRange> ipRanges)> inputData,
            List<ComplianceNetworkZone> networkZonesForCriterion,
            out List<NetworkObject> notAssessableObjects)
        {
            List<(NetworkObject networkObject, List<ComplianceNetworkZone> networkZones)> map = [];
            notAssessableObjects = [];

            foreach ((NetworkObject networkObject, List<IPAddressRange> ipRanges) dataItem in inputData)
            {
                List<ComplianceNetworkZone> networkZones = [];

                if (_autoCalculatedInternetZoneActive && _treatDomainAndDynamicObjectsAsInternet && ObjectType.IsDynamicallyResolvedObject(dataItem.networkObject.Type.Name))
                {
                    List<ComplianceNetworkZone> autoCalculatedInternetZones = [.. networkZonesForCriterion.Where(zone => zone.IsAutoCalculatedInternetZone)];

                    foreach (ComplianceNetworkZone zone in autoCalculatedInternetZones)
                    {
                        networkZones.Add(zone);
                    }
                }
                else if (dataItem.ipRanges.Count > 0)
                {
                    if (TryGetAssessabilityIssue(dataItem.networkObject) != null)
                    {
                        continue;
                    }

                    networkZones = DetermineZones(dataItem.ipRanges, out List<IPAddressRange> unassignableRanges, networkZonesForCriterion);

                    if (unassignableRanges.Count > 0)
                    {
                        notAssessableObjects.Add(dataItem.networkObject);
                    }
                }

                map.Add((dataItem.networkObject, networkZones));
            }

            return map;
        }

        /// <summary>
        /// Groups network objects by their associated compliance zone.
        /// </summary>
        /// <param name="objectsWithZones">Network objects enriched by their zones.</param>
        private Dictionary<ComplianceNetworkZone, List<NetworkObject>> MapObjectsByZone(List<(NetworkObject networkObject, List<ComplianceNetworkZone> networkZones)> objectsWithZones)
        {
            Dictionary<ComplianceNetworkZone, List<NetworkObject>> map = new();

            foreach ((NetworkObject networkObject, List<ComplianceNetworkZone> networkZones) item in objectsWithZones)
            {
                if (item.networkZones == null || item.networkZones.Count == 0)
                {
                    continue;
                }

                foreach (ComplianceNetworkZone zone in item.networkZones)
                {
                    if (!map.TryGetValue(zone, out List<NetworkObject>? objectsInZone))
                    {
                        objectsInZone = [];
                        map.Add(zone, objectsInZone);
                    }

                    objectsInZone.Add(item.networkObject);
                }
            }

            return map;
        }

        /// <summary>
        /// Finds every compliance zone overlapped by the provided IP ranges (plus implicit internet zone when necessary).
        /// </summary>
        /// <param name="ranges">Ranges to look up.</param>
        /// <param name="networkZonesOverride">Zones to use instead of the zones of the whole check.</param>
        private List<ComplianceNetworkZone> DetermineZones(List<IPAddressRange> ranges, List<ComplianceNetworkZone>? networkZonesOverride = null)
        {
            return DetermineZones(ranges, out _, networkZonesOverride);
        }

        /// <summary>
        /// Finds every compliance zone overlapped by the provided IP ranges (plus implicit internet zone when
        /// necessary) and reports the ranges that no zone can cover.
        /// </summary>
        /// <param name="ranges">Ranges to look up.</param>
        /// <param name="unassignableRanges">Ranges that matched no zone and that the internet zone cannot cover either.</param>
        /// <param name="networkZonesOverride">Zones to use instead of the zones of the whole check.</param>
        private List<ComplianceNetworkZone> DetermineZones(List<IPAddressRange> ranges, out List<IPAddressRange> unassignableRanges, List<ComplianceNetworkZone>? networkZonesOverride = null)
        {
            List<ComplianceNetworkZone> activeNetworkZones = networkZonesOverride ?? NetworkZones;
            return ComplianceZoneResolver.ResolveZones(
                ranges,
                activeNetworkZones,
                _autoCalculatedInternetZoneActive,
                _userConfig.GetText("internet_local_zone"),
                out unassignableRanges);
        }

        /// <summary>
        /// Resolves the matrix zones that belong to the provided criterion.
        /// </summary>
        /// <param name="criterion">Matrix criterion whose zones are needed.</param>
        private List<ComplianceNetworkZone> GetNetworkZonesForCriterion(ComplianceCriterion criterion)
        {
            if (criterion.Id > 0 && _networkZonesByCriterion.TryGetValue(criterion.Id, out List<ComplianceNetworkZone>? networkZones))
            {
                return networkZones;
            }

            return NetworkZones;
        }

        /// <summary>
        /// Removes dynamic/domain objects when the feature treats them implicitly as internet.
        /// </summary>
        /// <param name="networkObjects">Network objects to filter.</param>
        private List<NetworkObject> TryFilterDynamicAndDomainObjects(List<NetworkObject> networkObjects)
        {
            if (_userConfig.GlobalConfig is GlobalConfig globalConfig && globalConfig.AutoCalculateInternetZone && globalConfig.TreatDynamicAndDomainObjectsAsInternet)
            {
                networkObjects = networkObjects
                    .Where(n => !ObjectType.IsDynamicallyResolvedObject(n.Type.Name))
                    .ToList();
            }

            return networkObjects;
        }

        /// <summary>
        /// Detects assessability issues (like overly broad objects) for a given network object.
        /// </summary>
        /// <param name="networkObject">Network object to evaluate.</param>
        private AssessabilityIssue? TryGetAssessabilityIssue(NetworkObject networkObject)
        {
            if (networkObject.IP == null || networkObject.IpEnd == null)
                return AssessabilityIssue.IPNull;

            if (networkObject.IP == "::/128" && networkObject.IpEnd == "::/128")
                return AssessabilityIssue.IPNull;

            if (networkObject.IP == "0.0.0.0/32" && networkObject.IpEnd == "255.255.255.255/32")
                return AssessabilityIssue.AllIPs;

            if (networkObject.IP == "::/128" && networkObject.IpEnd == "ffff:ffff:ffff:ffff:ffff:ffff:ffff:ffff/128")
                return AssessabilityIssue.AllIPs;

            if (networkObject.IP == "255.255.255.255/32" && networkObject.IpEnd == "255.255.255.255/32")
                return AssessabilityIssue.Broadcast;

            if (networkObject.IP == "0.0.0.0/32" && networkObject.IpEnd == "0.0.0.0/32")
                return AssessabilityIssue.HostAddress;

            return null;
        }
    }
}
