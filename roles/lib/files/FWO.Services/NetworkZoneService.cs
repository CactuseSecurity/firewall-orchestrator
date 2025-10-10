using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Basics.Comparer;
using FWO.Config.Api;
using FWO.Data;
using FWO.Logging;
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
            catch (FormatException)
            {
                Log.WriteDebug("DisplayIpRange", $"Display as CIDR not possible, so display as range");
            }
            return ipAddressRange.ToString();
        }

        public static async Task AddZone(ComplianceNetworkZone networkZone, AdditionsDeletions addDel, ApiConnection apiConnection)
        {
            var variables = new
            {
                superNetworkZoneId = networkZone.Superzone?.Id,
                name = networkZone.Name,
                description = networkZone.Description,
                idString = networkZone.IdString,
                ipRanges = addDel.IpRangesToAdd.ConvertAll(range =>
                    new
                    {
                        ip_range_start = range.Begin.ToString(),
                        ip_range_end = range.End.ToString(),
                        criterion_id = networkZone.CriterionId
                    }
                ),
                communicationSources = addDel.SourceZonesToAdd.ConvertAll(zone =>
                    new
                    {
                        from_network_zone_id = zone.Id,
                        criterion_id = networkZone.CriterionId
                    }
                ),
                communicationDestinations = addDel.DestinationZonesToAdd.ConvertAll(zone =>
                    new
                    {
                        to_network_zone_id = zone.Id,
                        criterion_id = networkZone.CriterionId
                    }
                ),
                subNetworkZones = addDel.SubzonesToAdd.ConvertAll(zone =>
                    new
                    {
                        id = zone.Id
                    }
                ),
                criterionId = networkZone.CriterionId
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
                networkZoneId = networkZone.Id,
                superNetworkZoneId = networkZone.Superzone?.Id,
                name = networkZone.Name,
                description = networkZone.Description,
                addIpRanges = addDel.IpRangesToAdd.ConvertAll(range =>
                    new
                    {
                        network_zone_id = networkZone.Id,
                        ip_range_start = range.Begin.ToString(),
                        ip_range_end = range.End.ToString(),
                        criterion_id = networkZone.CriterionId
                    }
                ),
                deleteIpRangesExp = addDel.IpRangesToDelete.ConvertAll(range =>
                    new
                    {
                        ip_range_start = new { _eq = range.Begin.ToString() },
                        ip_range_end = new { _eq = range.End.ToString() },
                        criterion_id = new { _eq = networkZone.CriterionId }
                    }
                ),
                addSubZonesExp = addDel.SubzonesToAdd.ConvertAll(zone =>
                    new
                    {
                        id = new { _eq = zone.Id }
                    }
                ),
                deleteSubZonesExp = addDel.SubzonesToDelete.ConvertAll(zone =>
                    new
                    {
                        id = new { _eq = zone.Id }
                    }
                ),
                addZoneCommunication = addZoneCommunication,
                deleteZoneCommunicationExp = deleteZoneCommunicationExp,
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
                deleteZoneCommunicationExp = deleteZoneCommunicationExp,
                deleteIpRangesExp = networkZone.IPRanges.ToList().ConvertAll(range =>
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

        public static async Task UpdateSpecialZones(int matrixId, ApiConnection apiConnection, GlobalConfig globalConfig)
        {
            // Get all zones of matrix.

            List<ComplianceNetworkZone> existingZones = await apiConnection.SendQueryAsync<List<ComplianceNetworkZone>>(ComplianceQueries.getNetworkZonesForMatrix, new { criterionId = matrixId });

            // Remove existing special zones.

            foreach (ComplianceNetworkZone specialZone in existingZones.Where(zone => zone.IsInternetZone || zone.IsLocalZone))
            {
                await RemoveZone(specialZone, apiConnection);
            }

            // Add new internet zone

            ComplianceNetworkZone internetZone = new()
            {
                IdString = "SPECIAL_ZONE_INTERNET",
                Name = "Internet Zone",
                IsInternetZone = true,
                CriterionId = matrixId,
            };

            CalculateInternetZone(internetZone, existingZones.Where(zone => !zone.IsInternetZone && !zone.IsLocalZone).ToList());

            AdditionsDeletions internetZoneAddDel = new()
            {
                IpRangesToAdd = internetZone.IPRanges.ToList()
            };

            await AddZone(internetZone, internetZoneAddDel, apiConnection);

            // Add new local zone

            ComplianceNetworkZone localZone = new()
            {
                IdString = "SPECIAL_ZONE_LOCAL",
                Name = "Local Zone",
                IsLocalZone = true,
                CriterionId = matrixId,
            };

            AdditionsDeletions localZoneAddDel = new()
            {
                IpRangesToAdd = internetZone.IPRanges.ToList()
            };

            CalculateLocalZone(localZone, GetLocalZoneRanges(globalConfig), existingZones.Where(zone => !zone.IsInternetZone && !zone.IsLocalZone).ToList());

            await AddZone(localZone, localZoneAddDel, apiConnection); 

        }
        
        public static void CalculateInternetZone(ComplianceNetworkZone internetZone, List<ComplianceNetworkZone> excludedZones)
        {
            IPAddressRange fullRangeIPv4 = IPAddressRange.Parse("0.0.0.0/0");

            List<IPAddressRange> excludedZonesIPRanges = ParseNetworkZoneToListOfRanges(excludedZones, true);
            List<IPAddressRange> internetZoneIPRanges = fullRangeIPv4.Subtract(excludedZonesIPRanges);

            internetZone.IPRanges = internetZoneIPRanges.ToArray();
        }

        public static void CalculateLocalZone(ComplianceNetworkZone localZone, List<IPAddressRange> localZoneRanges, List<ComplianceNetworkZone> definedZones)
        {
            List<IPAddressRange> definedZonesIPRanges = ParseNetworkZoneToListOfRanges(definedZones, true);
            List<IPAddressRange> localZoneIPRanges = new();

            foreach (IPAddressRange range in localZoneRanges)
            {
                List<IPAddressRange> ranges = range.Subtract(definedZonesIPRanges);

                foreach (IPAddressRange newRange in ranges)
                {
                    bool exists = localZoneIPRanges.Any(r =>
                        r.Begin.Equals(newRange.Begin) &&
                        r.End.Equals(newRange.End));

                    if (!exists)
                    {
                        localZoneIPRanges.Add(newRange);
                    }                 
                }

            }

            localZone.IPRanges = localZoneIPRanges.ToArray();
        }


        private static List<IPAddressRange> ParseNetworkZoneToListOfRanges(List<ComplianceNetworkZone> networkZones, bool sort)
        {
            List<IPAddressRange> listOfRanges = new();

            // Gather ip ranges from excluded network zone list

            foreach (ComplianceNetworkZone networkZone in networkZones)
            {
                if (networkZone.IPRanges != null)
                {
                    listOfRanges.AddRange(networkZone.IPRanges);
                }
            }

            // Sort

            if (sort)
            {
                listOfRanges.Sort(new IPAddressRangeComparer());
            }

            return listOfRanges;
        }

        private static List<IPAddressRange> GetLocalZoneRanges(GlobalConfig globalConfig)
        {
            // TODO: Check global config for each localZoneIPAdressRange parameter and add IPAdressRange object to list if parameter true
            return new();
        }


    }
}
