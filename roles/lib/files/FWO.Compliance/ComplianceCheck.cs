using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using NetTools;


namespace FWO.Compliance
{
    public class ComplianceCheck(UserConfig userConfig, ApiConnection? apiConnection = null)
    {
        ComplianceNetworkZone[] NetworkZones = [];

        public async Task CheckAll()
        {
            if (apiConnection != null)
            {
                List<FwoOwner> owners = await apiConnection.SendQueryAsync<List<FwoOwner>>(OwnerQueries.getOwners);
                // get NetworkZones
                // checkApps
                // write result to database
            }
        }

        public async Task<List<(ComplianceNetworkZone, ComplianceNetworkZone)>> CheckApps(List<int> appsIds)
        {
            List<(ComplianceNetworkZone, ComplianceNetworkZone)> result = [];


            return result;
        }

        public List<(ComplianceNetworkZone, ComplianceNetworkZone)> CheckIpRangeInputCompliance(IPAddressRange? sourceIpRange, IPAddressRange? destinationIpRange, ComplianceNetworkZone[] networkZones)
        {
            NetworkZones = networkZones;
            List<(ComplianceNetworkZone, ComplianceNetworkZone)> forbiddenCommunicationsOutput = [];
            if (sourceIpRange != null && destinationIpRange != null)
            {
                CheckCompliance
                (
                    [sourceIpRange],
                    [destinationIpRange],
                    out forbiddenCommunicationsOutput
                );
            }
            return forbiddenCommunicationsOutput;
        }

        public bool CheckRuleCompliance(Rule rule, out List<(ComplianceNetworkZone, ComplianceNetworkZone)> forbiddenCommunication)
        {
            List<IPAddressRange> froms = [];
            List<IPAddressRange> tos = [];

            foreach (NetworkLocation networkLocation in rule.Froms)
            {
                // Determine all source ip ranges
                froms.AddRange(ParseIpRange(networkLocation.Object));
            }
            foreach (NetworkLocation networkLocation in rule.Tos)
            {
                // Determine all destination ip ranges
                tos.AddRange(ParseIpRange(networkLocation.Object));
            }

            return CheckCompliance(froms, tos, out forbiddenCommunication);
        }

        private bool CheckCompliance(List<IPAddressRange> source, List<IPAddressRange> destination, out List<(ComplianceNetworkZone, ComplianceNetworkZone)> forbiddenCommunication)
        {
            // Determine all matching source zones
            List<ComplianceNetworkZone> sourceZones = DetermineZones(source);

            // Determine all macthing destination zones
            List<ComplianceNetworkZone> destinationZones = DetermineZones(destination);

            forbiddenCommunication = [];

            foreach (ComplianceNetworkZone sourceZone in sourceZones)
            {
                foreach (ComplianceNetworkZone destinationZone in destinationZones)
                {
                    if (!sourceZone.CommunicationAllowedTo(destinationZone))
                    {
                        forbiddenCommunication.Add((sourceZone, destinationZone));
                    }
                }
            }

            return forbiddenCommunication.Count == 0;
        }


        private List<ComplianceNetworkZone> DetermineZones(List<IPAddressRange> ranges)
        {
            List<ComplianceNetworkZone> result = [];
            List<List<IPAddressRange>> unseenIpAddressRanges = [];

            for (int i = 0; i < ranges.Count; i++)
            {
                unseenIpAddressRanges.Add(
                [
                    new(ranges[i].Begin, ranges[i].End)
                ]);
            }

            foreach (ComplianceNetworkZone zone in NetworkZones.Where(z => z.OverlapExists(ranges, unseenIpAddressRanges)))
            {
                result.Add(zone);
            }

            // Get ip ranges that are not in any zone
            List<IPAddressRange> undefinedIpRanges = [.. unseenIpAddressRanges.SelectMany(x => x)];
            if (undefinedIpRanges.Count > 0)
            {
                result.Add
                (
                    new ComplianceNetworkZone()
                    {
                        Name = userConfig.GetText("internet_local_zone"),
                    }
                );
            }

            return result;
        }

        private static List<IPAddressRange> ParseIpRange(NetworkObject networkObject)
        {
            List<IPAddressRange> ranges = [];

            if (networkObject.Type == new NetworkObjectType() { Name = ObjectType.IPRange })
            {
                ranges.Add(IPAddressRange.Parse($"{networkObject.IP}-{networkObject.IpEnd}"));
            }
            else if (networkObject.Type != new NetworkObjectType() { Name = ObjectType.Group })
            {
                for (int j = 0; j < networkObject.ObjectGroupFlats.Length; j++)
                {
                    if (networkObject.ObjectGroupFlats[j].Object != null)
                    {
                        ranges.AddRange(ParseIpRange(networkObject.ObjectGroupFlats[j].Object!));
                    }
                }
            }
            else
            {
                // CIDR notation or single (host) IP can be parsed directly
                ranges.Add(IPAddressRange.Parse(networkObject.IP));
            }

            return ranges;
        }
    }
}
