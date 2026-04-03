using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Basics.Enums;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Extensions;
using FWO.Ui.Display;
using NetTools;
using System.Net;

namespace FWO.Compliance
{
    public partial class ComplianceCheck
    {
        /// <summary>
        /// Extracts the IP ranges represented by a network object in all supported forms.
        /// </summary>
        public static List<IPAddressRange> ParseIpRange(NetworkObject networkObject)
        {
            List<IPAddressRange> ranges = [];

            if (networkObject.Type.Name == ObjectType.IPRange || (networkObject.Type.Name == ObjectType.Network && networkObject.IP.Equals(networkObject.IpEnd) == false))
            {
                if (IPAddress.TryParse(networkObject.IP.StripOffNetmask(), out IPAddress? ipStart) && IPAddress.TryParse(networkObject.IpEnd.StripOffNetmask(), out IPAddress? ipEnd))
                {
                    ranges.Add(new IPAddressRange(ipStart, ipEnd));
                }
            }
            else if (networkObject.Type.Name != ObjectType.Group && networkObject.ObjectGroupFlats.Length > 0)
            {
                for (int j = 0; j < networkObject.ObjectGroupFlats.Length; j++)
                {
                    if (networkObject.ObjectGroupFlats[j].Object != null)
                    {
                        ranges.AddRange(ParseIpRange(networkObject.ObjectGroupFlats[j].Object!));
                    }
                }
            }
            else if (networkObject.IP != null)
            {
                ranges.Add(IPAddressRange.Parse(networkObject.IP));
            }

            return ranges;
        }

        /// <summary>
        /// Compliance check used in current UI implementation.
        /// </summary>
        public List<(ComplianceNetworkZone, ComplianceNetworkZone)> CheckIpRangeInputCompliance(IPAddressRange? sourceIpRange, IPAddressRange? destinationIpRange, List<ComplianceNetworkZone> networkZones)
        {
            NetworkZones = networkZones;
            List<(ComplianceNetworkZone, ComplianceNetworkZone)> forbiddenCommunicationsOutput = [];

            if (sourceIpRange != null && destinationIpRange != null)
            {
                CheckMatrixCompliance([sourceIpRange], [destinationIpRange], out forbiddenCommunicationsOutput);
            }

            return forbiddenCommunicationsOutput;
        }

        private async Task<bool> CheckMatrixCompliance(Rule rule, ComplianceCriterion criterion, List<NetworkObject> resolvedSources, List<NetworkObject> resolvedDestinations)
        {
            Task<List<(NetworkObject networkObject, List<IPAddressRange> ipRanges)>> fromsTask = GetNetworkObjectsWithIpRanges(resolvedSources);
            Task<List<(NetworkObject networkObject, List<IPAddressRange> ipRanges)>> tosTask = GetNetworkObjectsWithIpRanges(resolvedDestinations);

            await Task.WhenAll(fromsTask, tosTask);

            bool ruleIsCompliant = true;

            List<(NetworkObject networkObject, List<ComplianceNetworkZone> networkZones)> sourceZones = MapZonesToNetworkObjects(fromsTask.Result);
            List<(NetworkObject networkObject, List<ComplianceNetworkZone> networkZones)> destinationZones = MapZonesToNetworkObjects(tosTask.Result);

            Dictionary<ComplianceNetworkZone, List<NetworkObject>> sourceObjectsByZone = MapObjectsByZone(sourceZones);
            Dictionary<ComplianceNetworkZone, List<NetworkObject>> destinationObjectsByZone = MapObjectsByZone(destinationZones);

            foreach ((ComplianceNetworkZone sourceZone, List<NetworkObject> sourceObjects) in sourceObjectsByZone)
            {
                foreach ((ComplianceNetworkZone destinationZone, List<NetworkObject> destinationObjects) in destinationObjectsByZone)
                {
                    if (!sourceZone.CommunicationAllowedTo(destinationZone))
                    {
                        ruleIsCompliant = false;
                        string sourceObjectsString = string.Join(", ", sourceObjects.Select(GetNwObjectString).Distinct());
                        string destinationObjectsString = string.Join(", ", destinationObjects.Select(GetNwObjectString).Distinct());
                        string details = $"{_userConfig.GetText("H5839")}: {sourceZone.Name} ({sourceObjectsString}) -> {destinationZone.Name} ({destinationObjectsString})";

                        ComplianceCheckResult complianceCheckResult = new(rule, ComplianceViolationType.MatrixViolation)
                        {
                            Criterion = criterion,
                            SourceZone = sourceZone,
                            DestinationZone = destinationZone
                        };

                        CreateViolation(ComplianceViolationType.MatrixViolation, rule, complianceCheckResult, details);
                    }
                }
            }

            return ruleIsCompliant;
        }

        private string GetNwObjectString(NetworkObject networkObject)
        {
            string networkObjectString = "";
            networkObjectString += networkObject.Name;
            networkObjectString += NwObjDisplay.DisplayIp(networkObject.IP, networkObject.IpEnd, networkObject.Type.Name, true);
            return networkObjectString;
        }

        private bool CheckMatrixCompliance(List<IPAddressRange> source, List<IPAddressRange> destination, out List<(ComplianceNetworkZone, ComplianceNetworkZone)> forbiddenCommunication)
        {
            List<ComplianceNetworkZone> sourceZones = DetermineZones(source);
            List<ComplianceNetworkZone> destinationZones = DetermineZones(destination);

            forbiddenCommunication = [];

            foreach (ComplianceNetworkZone sourceZone in sourceZones)
            {
                foreach (ComplianceNetworkZone destinationZone in destinationZones.Where(d => !sourceZone.CommunicationAllowedTo(d)))
                {
                    forbiddenCommunication.Add((sourceZone, destinationZone));
                }
            }

            return forbiddenCommunication.Count == 0;
        }

        private static Task<List<(NetworkObject networkObject, List<IPAddressRange> ipRanges)>> GetNetworkObjectsWithIpRanges(List<NetworkObject> networkObjects)
        {
            List<(NetworkObject networkObject, List<IPAddressRange> ipRanges)> networkObjectsWithIpRange = [];

            foreach (NetworkObject networkObject in networkObjects)
            {
                networkObjectsWithIpRange.Add((networkObject, ParseIpRange(networkObject)));
            }

            return Task.FromResult(networkObjectsWithIpRange);
        }

        private async Task LoadNetworkZones()
        {
            if (Policy != null)
            {
                int? matrixId = Policy.Criteria.FirstOrDefault(c => c.Content.CriterionType == CriterionType.Matrix.ToString())?.Content.Id;
                if (matrixId != null)
                {
                    Logger.TryWriteInfo("Compliance Check", $"Loading network zones for Matrix {matrixId}.", LocalSettings.ComplianceCheckVerbose);
                    NetworkZones = await _apiConnection.SendQueryAsync<List<ComplianceNetworkZone>>(ComplianceQueries.getNetworkZonesForMatrix, new { criterionId = matrixId });
                    Logger.TryWriteInfo("Compliance Check", $"Loaded {NetworkZones.Count} network zones for Matrix {matrixId}.", LocalSettings.ComplianceCheckVerbose);
                }
            }
        }

        private List<(NetworkObject networkObject, List<ComplianceNetworkZone> networkZones)> MapZonesToNetworkObjects(List<(NetworkObject networkObject, List<IPAddressRange> ipRanges)> inputData)
        {
            List<(NetworkObject networkObject, List<ComplianceNetworkZone> networkZones)> map = [];

            foreach ((NetworkObject networkObject, List<IPAddressRange> ipRanges) dataItem in inputData)
            {
                List<ComplianceNetworkZone> networkZones = [];

                if (_autoCalculatedInternetZoneActive && _treatDomainAndDynamicObjectsAsInternet && (dataItem.networkObject.Type.Name == "dynamic_net_obj" || dataItem.networkObject.Type.Name == "domain"))
                {
                    List<ComplianceNetworkZone> complianceNetworkZones = NetworkZones.Where(zone => zone.IsAutoCalculatedInternetZone).ToList();
                    foreach (ComplianceNetworkZone zone in complianceNetworkZones)
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

                    networkZones = DetermineZones(dataItem.ipRanges);
                }

                map.Add((dataItem.networkObject, networkZones));
            }

            return map;
        }

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

        private List<ComplianceNetworkZone> DetermineZones(List<IPAddressRange> ranges)
        {
            List<ComplianceNetworkZone> result = [];
            List<List<IPAddressRange>> unseenIpAddressRanges = [];

            for (int i = 0; i < ranges.Count; i++)
            {
                unseenIpAddressRanges.Add([new(ranges[i].Begin, ranges[i].End)]);
            }

            foreach (ComplianceNetworkZone zone in NetworkZones.Where(z => z.OverlapExists(ranges, unseenIpAddressRanges)))
            {
                result.Add(zone);
            }

            if (_autoCalculatedInternetZoneActive)
            {
                return result;
            }

            List<IPAddressRange> undefinedIpRanges = [.. unseenIpAddressRanges.SelectMany(x => x)];
            if (undefinedIpRanges.Count > 0)
            {
                result.Add(new ComplianceNetworkZone()
                {
                    Name = _userConfig.GetText("internet_local_zone"),
                });
            }

            return result;
        }

        private List<NetworkObject> TryFilterDynamicAndDomainObjects(List<NetworkObject> networkObjects)
        {
            if (_userConfig.GlobalConfig is GlobalConfig globalConfig && globalConfig.AutoCalculateInternetZone && globalConfig.TreatDynamicAndDomainObjectsAsInternet)
            {
                networkObjects = networkObjects
                    .Where(n => !new List<string> { "domain", "dynamic_net_obj" }.Contains(n.Type.Name))
                    .ToList();
            }

            return networkObjects;
        }

        private AssessabilityIssue? TryGetAssessabilityIssue(NetworkObject networkObject)
        {
            if (networkObject.IP == null && networkObject.IpEnd == null)
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
