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
            // Get all zones of the matrix.

            List<ComplianceNetworkZone> existingZones = await apiConnection.SendQueryAsync<List<ComplianceNetworkZone>>(ComplianceQueries.getNetworkZonesForMatrix, new { criterionId = matrixId });

            // Remove existing special zones.

            foreach (ComplianceNetworkZone specialZone in existingZones.Where(zone => zone.IsInternetZone || zone.IsInternalZone))
            {
                await RemoveZone(specialZone, apiConnection);
                existingZones.Remove(specialZone);
            }

            // Add new local zone

            ComplianceNetworkZone internalZone = new()
            {
                IdString = "SPECIAL_ZONE_INTERNAL",
                Name = "Undefined Internal Zone",
                IsInternalZone = true,
                CriterionId = matrixId,
            };

            CalculateInternalZone(internalZone, GetInternalZoneRanges(globalConfig), existingZones);

            AdditionsDeletions internalZoneAddDel = new()
            {
                IpRangesToAdd = internalZone.IPRanges.ToList()
            };

            await AddZone(internalZone, internalZoneAddDel, apiConnection);

            existingZones.Add(internalZone);

            // Add new internet zone

            ComplianceNetworkZone internetZone = new()
            {
                IdString = "SPECIAL_ZONE_INTERNET",
                Name = "Internet Zone",
                IsInternetZone = true,
                CriterionId = matrixId,
            };

            CalculateInternetZone(internetZone, existingZones);

            AdditionsDeletions internetZoneAddDel = new()
            {
                IpRangesToAdd = internetZone.IPRanges.ToList()
            };

            await AddZone(internetZone, internetZoneAddDel, apiConnection);



        }
        
        public static void CalculateInternetZone(ComplianceNetworkZone internetZone, List<ComplianceNetworkZone> excludedZones)
        {
            IPAddressRange fullRangeIPv4 = IPAddressRange.Parse("0.0.0.0/0");

            List<IPAddressRange> excludedZonesIPRanges = ParseNetworkZoneToListOfRanges(excludedZones, true);
            List<IPAddressRange> internetZoneIPRanges = fullRangeIPv4.Subtract(excludedZonesIPRanges);

            internetZone.IPRanges = internetZoneIPRanges.ToArray();
        }

        public static void CalculateInternalZone(ComplianceNetworkZone internalZone, List<IPAddressRange> internalZoneRanges, List<ComplianceNetworkZone> definedZones)
        {
            List<IPAddressRange> definedZonesIPRanges = ParseNetworkZoneToListOfRanges(definedZones, true);
            List<IPAddressRange> internalZoneIPRanges = new();

            foreach (IPAddressRange range in internalZoneRanges)
            {
                List<IPAddressRange> ranges = range.Subtract(definedZonesIPRanges);

                foreach (IPAddressRange newRange in ranges)
                {
                    bool exists = internalZoneIPRanges.Any(r =>
                        r.Begin.Equals(newRange.Begin) &&
                        r.End.Equals(newRange.End));

                    if (!exists)
                    {
                        bool isSubnetOfExisting = internalZoneIPRanges.Any(r =>
                            r.Contains(newRange));

                        if (!isSubnetOfExisting)
                        {

                            bool overlapsWithExisting = internalZoneIPRanges.Any(r =>
                                IpOperations.RangeOverlapExists(r, newRange));

                            if (overlapsWithExisting)
                            {
                                // TODO: Handle overlaps
                            }
                            
                            internalZoneIPRanges.Add(newRange);
                        }
                        
                    }                 
                }

            }

            internalZone.IPRanges = internalZoneIPRanges.ToArray();
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

        private static List<IPAddressRange> GetInternalZoneRanges(GlobalConfig globalConfig)
        {
            List<IPAddressRange> internalZoneRanges = new();

            TryAddToInternalZone(globalConfig.InternalZoneRange_10_0_0_0_8, IpOperations.GetIPAdressRange("10.0.0.0/8"), internalZoneRanges);
            TryAddToInternalZone(globalConfig.InternalZoneRange_172_16_0_0_12, IpOperations.GetIPAdressRange("172.16.0.0/12"), internalZoneRanges);
            TryAddToInternalZone(globalConfig.InternalZoneRange_192_168_0_0_16, IpOperations.GetIPAdressRange("192.168.0.0/16"), internalZoneRanges);
            TryAddToInternalZone(globalConfig.InternalZoneRange_0_0_0_0_8, IpOperations.GetIPAdressRange("0.0.0.0/8"), internalZoneRanges);
            TryAddToInternalZone(globalConfig.InternalZoneRange_127_0_0_0_8, IpOperations.GetIPAdressRange("127.0.0.0/8"), internalZoneRanges);
            TryAddToInternalZone(globalConfig.InternalZoneRange_169_254_0_0_16, IpOperations.GetIPAdressRange("169.254.0.0/16"), internalZoneRanges);
            TryAddToInternalZone(globalConfig.InternalZoneRange_224_0_0_0_4, IpOperations.GetIPAdressRange("224.0.0.0/4"), internalZoneRanges);
            TryAddToInternalZone(globalConfig.InternalZoneRange_240_0_0_0_4, IpOperations.GetIPAdressRange("240.0.0.0/4"), internalZoneRanges);
            TryAddToInternalZone(globalConfig.InternalZoneRange_255_255_255_255_32, IpOperations.GetIPAdressRange("255.255.255.255/32"), internalZoneRanges);
            TryAddToInternalZone(globalConfig.InternalZoneRange_192_0_2_0_24, IpOperations.GetIPAdressRange("192.0.2.0/24"), internalZoneRanges);
            TryAddToInternalZone(globalConfig.InternalZoneRange_198_51_100_0_24, IpOperations.GetIPAdressRange("198.51.100.0/24"), internalZoneRanges);
            TryAddToInternalZone(globalConfig.InternalZoneRange_203_0_113_0_24, IpOperations.GetIPAdressRange("203.0.113.0/24"), internalZoneRanges);
            TryAddToInternalZone(globalConfig.InternalZoneRange_100_64_0_0_10, IpOperations.GetIPAdressRange("100.64.0.0/10"), internalZoneRanges);
            TryAddToInternalZone(globalConfig.InternalZoneRange_192_0_0_0_24, IpOperations.GetIPAdressRange("192.0.0.0/24"), internalZoneRanges);
            TryAddToInternalZone(globalConfig.InternalZoneRange_192_88_99_0_24, IpOperations.GetIPAdressRange("192.88.99.0/24"), internalZoneRanges);
            TryAddToInternalZone(globalConfig.InternalZoneRange_198_18_0_0_15, IpOperations.GetIPAdressRange("198.18.0.0/15"), internalZoneRanges);

            return internalZoneRanges;
        }
        
        private static void TryAddToInternalZone(bool configParameter, IPAddressRange range, List<IPAddressRange> internalZoneRanges)
        {
            if (configParameter)
            {
                internalZoneRanges.Add(range);
            }
        }


    }
}
