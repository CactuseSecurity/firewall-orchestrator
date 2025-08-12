using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data;
using NetTools;

namespace FWO.Services
{
    public class NetworkZoneService
    {
        public List<ComplianceNetworkZone> NetworkZones { get; set; } = [];

        public delegate void ZoneAddEventArgs();
        public delegate void ZoneModificationEventArgs(ComplianceNetworkZone networkZone);
        public event ZoneModificationEventArgs? OnEditZone;
        public event ZoneModificationEventArgs? OnDeleteZone;

        public struct AdditionsDeletions
        {
            public List<ComplianceNetworkZone> SourceZonesToAdd = [];
            public List<ComplianceNetworkZone> SourceZonesToDelete = [];
            public List<ComplianceNetworkZone> DestinationZonesToAdd = [];
            public List<ComplianceNetworkZone> DestinationZonesToDelete = [];
            public List<ComplianceNetworkZone> SubzonesToAdd = [];
            public List<ComplianceNetworkZone> SubzonesToDelete = [];
            public List<IPAddressRange> IpRangesToDelete = [];
            public List<IPAddressRange> IpRangesToAdd = [];

            public AdditionsDeletions() { }
        }

        public void InvokeOnEditZone(ComplianceNetworkZone networkZone)
        {
            OnEditZone?.Invoke(networkZone);
        }

        public void InvokeOnDeleteZone(ComplianceNetworkZone networkZone)
        {
            OnDeleteZone?.Invoke(networkZone);
        }

        /// <summary>
        /// Display the IP address range in CIDR notation if possible and it is not a single IP address
        /// otherwise display it in the format "first_ip-last_ip".
        /// </summary>
        /// <param name="ipAddressRange"></param>
        /// <returns>IP address range in CIDR / first-last notation</returns>
        public static string DisplayIpRange(IPAddressRange ipAddressRange)
        {
            try
            {
                int prefixLength = ipAddressRange.GetPrefixLength();
                if (prefixLength != 32)
                {
                    return ipAddressRange.ToCidrString();
                }
            }
            catch (FormatException) { }
            return ipAddressRange.ToString();
        }

        public static async Task AddZone(ComplianceNetworkZone networkZone, AdditionsDeletions addDel, ApiConnection apiConnection)
        {
            var variables = new
            {
                super_network_zone_id = networkZone.Superzone?.Id,
                name = networkZone.Name,
                description = networkZone.Description,
                ip_ranges = addDel.IpRangesToAdd.ConvertAll(range =>
                    new
                    {
                        ip_range_start = range.Begin.ToString(),
                        ip_range_end = range.End.ToString(),
                        criterion_id = networkZone.CriterionId
                    }
                ),
                communication_sources = addDel.SourceZonesToAdd.ConvertAll(zone =>
                    new
                    {
                        from_network_zone_id = zone.Id,
                        criterion_id = networkZone.CriterionId
                    }
                ),
                communication_destinations = addDel.DestinationZonesToAdd.ConvertAll(zone =>
                    new
                    {
                        to_network_zone_id = zone.Id,
                        criterion_id = networkZone.CriterionId
                    }
                ),
                sub_network_zones = addDel.SubzonesToAdd.ConvertAll(zone =>
                    new
                    {
                        id = zone.Id
                    }
                ),
                criterion_id = networkZone.CriterionId
            };

            await apiConnection.SendQueryAsync<dynamic>(ComplianceQueries.addNetworkZone, variables);
        }

        public static async Task UpdateZone(ComplianceNetworkZone networkZone, AdditionsDeletions addDel, ApiConnection apiConnection)
        {
            var addZoneCommunication = addDel.SourceZonesToAdd.ConvertAll(zone =>
                new
                {
                    from_network_zone_id = zone.Id,
                    to_network_zone_id = networkZone.Id,
                    criterion_id = networkZone.CriterionId
                }
            );
            addZoneCommunication.AddRange(addDel.DestinationZonesToAdd.ConvertAll(zone =>
                new
                {
                    from_network_zone_id = networkZone.Id,
                    to_network_zone_id = zone.Id,
                    criterion_id = networkZone.CriterionId
                }
            ));

            var deleteZoneCommunicationExp = addDel.SourceZonesToDelete.ConvertAll(zone =>
                new
                {
                    from_network_zone_id = new { _eq = zone.Id },
                    to_network_zone_id = new { _eq = networkZone.Id },
                    criterion_id = new { _eq = networkZone.CriterionId }
                }
            );
            deleteZoneCommunicationExp.AddRange(addDel.DestinationZonesToDelete.ConvertAll(zone =>
                new
                {
                    from_network_zone_id = new { _eq = networkZone.Id },
                    to_network_zone_id = new { _eq = zone.Id },
                    criterion_id = new { _eq = networkZone.CriterionId }
                }
            ));

            var variables = new
            {
                network_zone_id = networkZone.Id,
                super_network_zone_id = networkZone.Superzone?.Id,
                name = networkZone.Name,
                description = networkZone.Description,
                add_ip_ranges = addDel.IpRangesToAdd.ConvertAll(range =>
                    new
                    {
                        network_zone_id = networkZone.Id,
                        ip_range_start = range.Begin.ToString(),
                        ip_range_end = range.End.ToString(),
                        criterion_id = networkZone.CriterionId
                    }
                ),
                delete_ip_ranges_exp = addDel.IpRangesToDelete.ConvertAll(range =>
                    new
                    {
                        ip_range_start = new { _eq = range.Begin.ToString() },
                        ip_range_end = new { _eq = range.End.ToString() },
                        criterion_id = new { _eq = networkZone.CriterionId }
                    }
                ),
                add_sub_zones_exp = addDel.SubzonesToAdd.ConvertAll(zone =>
                    new
                    {
                        id = new { _eq = zone.Id }
                    }
                ),
                delete_sub_zones_exp = addDel.SubzonesToDelete.ConvertAll(zone =>
                    new
                    {
                        id = new { _eq = zone.Id }
                    }
                ),
                add_zone_communication = addZoneCommunication,
                delete_zone_communication_exp = deleteZoneCommunicationExp,
                removed = DateTime.UtcNow
            };

            await apiConnection.SendQueryAsync<dynamic>(ComplianceQueries.updateNetworkZone, variables);
        }

        public static async Task RemoveZone(ComplianceNetworkZone networkZone, ApiConnection apiConnection)
        {
            var deleteZoneCommunicationExp = networkZone.AllowedCommunicationSources.ToList().ConvertAll(zone =>
                new
                {
                    from_network_zone_id = new { _eq = zone.Id },
                    to_network_zone_id = new { _eq = networkZone.Id }
                }
            );
            deleteZoneCommunicationExp.AddRange(networkZone.AllowedCommunicationDestinations.ToList().ConvertAll(zone =>
                new
                {
                    from_network_zone_id = new { _eq = networkZone.Id },
                    to_network_zone_id = new { _eq = zone.Id }
                }
            ));
            var variables = new
            {
                delete_zone_communication_exp = deleteZoneCommunicationExp,
                delete_ip_ranges_exp = networkZone.IPRanges.ToList().ConvertAll(range =>
                    new
                    {
                        ip_range_start = new { _eq = range.Begin.ToString() },
                        ip_range_end = new { _eq = range.End.ToString() }
                    }
                ),
                id = networkZone.Id,
                removed = DateTime.UtcNow
            };
            await apiConnection.SendQueryAsync<dynamic>(ComplianceQueries.removeNetworkZone, variables);
        }
    }
}
