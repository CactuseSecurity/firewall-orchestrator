using FWO.Logging;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.GlobalConstants;
using FWO.Api.Data;
using FWO.Config.Api;
using System.Text.Json;
using NetTools;
using System.Reactive.Subjects;
using System.Linq;


namespace FWO.Middleware.Server
{
    /// <summary>
    /// Class handling the Area Subnet Data Import
    /// </summary>
    public class AreaSubnetDataImport : DataImportBase
    {
        private List<ModellingNetworkArea> existingAreas = [];


        /// <summary>
        /// Constructor for Area Subnet Data Import
        /// </summary>
        public AreaSubnetDataImport(ApiConnection apiConnection, GlobalConfig globalConfig) : base(apiConnection, globalConfig)
        { }

        /// <summary>
        /// Run the Area Subnet Data Import
        /// </summary>
        public async Task<bool> Run()
        {
            List<string> importfilePathAndNames = JsonSerializer.Deserialize<List<string>>(globalConfig.ImportSubnetDataPath) ?? throw new Exception("Config Data could not be deserialized.");

            List<ModellingImportNwData> AllNwData = [];

            foreach (var importfilePathAndName in importfilePathAndNames)
            {
                if (!RunImportScript(importfilePathAndName + ".py"))
                {
                    Log.WriteInfo("Import Area Subnet Data", $"Script {importfilePathAndName}.py failed but trying to import from existing file.");
                }

                try
                {
                    ReadFile(importfilePathAndName + ".json");
                    ModellingImportNwData? nwData = Import();

                    if (nwData is null)
                        continue;

                    AllNwData.Add(nwData);
                }
                catch (Exception ex)
                {
                    Log.WriteError("Import Subnet Data", $"Import could not be processed.", ex);
                }
            }

            ModellingImportNwData mergedNwData = MergeAreas(AllNwData);

            if (mergedNwData != null && mergedNwData.Areas != null)
            {
                await SaveMergedNwData(mergedNwData);
            }
            else
            {
                Log.WriteInfo("Import Area Subnet Data", $"No subnet/host/range Data found in {importFile} No changes done.");
            }


            return true;
        }

        private async Task SaveMergedNwData(ModellingImportNwData mergedNwData)
        {
            int successCounter = 0;
            int failCounter = 0;
            int deleteCounter = 0;
            int deleteFailCounter = 0;


            foreach (ModellingImportAreaData area in mergedNwData.Areas)
            {
                existingAreas = await apiConnection.SendQueryAsync<List<ModellingNetworkArea>>(ModellingQueries.getAreas);
                foreach (ModellingImportAreaData incomingArea in mergedNwData.Areas)
                {
                    if (await SaveArea(incomingArea))
                    {
                        ++successCounter;
                    }
                    else
                    {
                        ++failCounter;
                    }
                }
                foreach (ModellingNetworkArea existingArea in existingAreas)
                {
                    if (mergedNwData.Areas.FirstOrDefault(x => x.Name == existingArea.Name) == null)
                    {
                        if (await DeleteArea(existingArea))
                        {
                            ++deleteCounter;
                        }
                        else
                        {
                            ++deleteFailCounter;
                        }
                    }
                }
            }

            Log.WriteInfo("Import Area Subnet Data", $"Imported {successCounter} subnets/hosts/ranges, {failCounter} failed. Deleted {deleteCounter} areas, {deleteFailCounter} failed.");
        }

        private ModellingImportNwData MergeAreas(List<ModellingImportNwData> AllNwData)
        {
            ModellingImportNwData mergedNwData = new()
            {
                Areas = []
            };

            foreach (ModellingImportNwData nwData in AllNwData)
            {
                foreach (ModellingImportAreaData area in nwData.Areas)
                {
                    for (int i = area.Subnets.Count - 1; i >= 0; i--)
                    {
                        ModellingImportAreaSubnets? newSubnet = ConvertSubnet(area.Subnets[i]);
                        area.Subnets[i] = newSubnet;
                    }
                }
            }

            foreach (ModellingImportNwData nwData in AllNwData)
            {
                foreach (ModellingImportAreaData area in nwData.Areas)
                {
                    ModellingImportAreaData? mergeNwData = mergedNwData.Areas.FirstOrDefault(_ => _.IdString == area.IdString);
                    if (mergeNwData is null)
                    {
                        mergedNwData.Areas.Add(area);
                    }
                    else
                    {
                        bool found = false;
                        ModellingImportAreaData? newArea = new();
                        foreach (ModellingImportAreaSubnets subnet in area.Subnets)
                        {
                            foreach (var subnetExisting in mergeNwData.Subnets)
                            {
                                if (subnet.Ip == subnetExisting.Ip || subnet.IpEnd == subnetExisting.IpEnd)
                                {
                                    found = true;
                                    break;
                                }
                            }
                            if (!found)
                            {
                                area.Subnets.Add(subnet);
                            }
                        }
                    }
                }
            }

            return mergedNwData;
        }

        private ModellingImportNwData? Import()
        {
            try
            {
                ModellingImportNwData? importedNwData = JsonSerializer.Deserialize<ModellingImportNwData>(importFile) ?? throw new Exception("File could not be parsed.");

                return importedNwData;
            }
            catch (Exception exc)
            {
                Log.WriteError("Import Area Subnet Data", $"File could not be processed.", exc);
                return null;
            }
        }

        private async Task<bool> SaveArea(ModellingImportAreaData incomingArea)
        {
            try
            {
                ModellingNetworkArea? existingArea = existingAreas.FirstOrDefault(x => x.IdString == incomingArea.IdString);
                if (existingArea == null)
                {
                    await NewArea(incomingArea);
                }
                else
                {
                    await UpdateArea(incomingArea, existingArea);
                }
            }
            catch (Exception exc)
            {
                Log.WriteError("Import Area Subnet Data", $"Area {incomingArea.Name}({incomingArea.IdString}) could not be processed.", exc);
                return false;
            }
            return true;
        }

        private async Task NewArea(ModellingImportAreaData incomingArea)
        {
            var AreaVar = new
            {
                name = incomingArea.Name,
                idString = incomingArea.IdString,
                creator = GlobalConst.kImportAreaSubnetData
            };
            ReturnId[]? areaIds = ( await apiConnection.SendQueryAsync<NewReturning>(ModellingQueries.newArea, AreaVar) ).ReturnIds;
            if (areaIds != null)
            {
                foreach (var subnet in incomingArea.Subnets)
                {
                    ModellingImportAreaSubnets parsedSubnet = ConvertSubnet(subnet);

                    var SubnetVar = new
                    {
                        name = parsedSubnet.Name,
                        ip = parsedSubnet.Ip,
                        ipEnd = parsedSubnet.IpEnd,
                        importSource = GlobalConst.kImportAreaSubnetData
                    };

                    ReturnId[]? subnetIds = ( await apiConnection.SendQueryAsync<NewReturning>(ModellingQueries.newAreaSubnet, SubnetVar) ).ReturnIds;
                    if (subnetIds != null)
                    {
                        var Vars = new
                        {
                            nwObjectId = subnetIds[0].NewId,
                            nwGroupId = areaIds[0].NewId
                        };
                        await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.addNwObjectToNwGroup, Vars);
                    }
                }
            }
        }

        private async Task UpdateArea(ModellingImportAreaData incomingArea, ModellingNetworkArea existingArea)
        {
            if (existingArea.IsDeleted)
            {
                await ReactivateArea(existingArea);
            }
            List<ModellingImportAreaSubnets> subnetsToAdd = new(incomingArea.Subnets);
            List<NetworkSubnetWrapper> subnetsToDelete = new(existingArea.Subnets);
            foreach (var existingSubnet in existingArea.Subnets)
            {
                foreach (var incomingSubnet in incomingArea.Subnets)
                {
                    if (incomingSubnet.Name == existingSubnet.Content.Name && incomingSubnet.Ip == existingSubnet.Content.Ip.StripOffNetmask() &&
                        ( incomingSubnet.IpEnd == existingSubnet.Content.IpEnd.StripOffNetmask() ))
                    {
                        existingSubnet.Content.Ip = existingSubnet.Content.Ip.StripOffNetmask();
                        existingSubnet.Content.IpEnd = existingSubnet.Content.IpEnd.StripOffNetmask();
                        subnetsToAdd.Remove(incomingSubnet);
                        subnetsToDelete.Remove(existingSubnet);
                    }
                }
            }
            foreach (var subnet in subnetsToDelete)
            {
                await apiConnection.SendQueryAsync<NewReturning>(OwnerQueries.deleteAreaSubnet, new { id = subnet.Content.Id });
            }
            foreach (var subnet in subnetsToAdd)
            {
                var SubnetVar = new
                {
                    name = subnet.Name,
                    ip = subnet.Ip,
                    ipEnd = subnet.IpEnd,
                    importSource = GlobalConst.kImportAreaSubnetData
                };
                ReturnId[]? subnetIds = ( await apiConnection.SendQueryAsync<NewReturning>(ModellingQueries.newAreaSubnet, SubnetVar) ).ReturnIds;
                if (subnetIds != null)
                {
                    var Vars = new
                    {
                        nwObjectId = subnetIds[0].NewId,
                        nwGroupId = existingArea.Id,
                    };
                    await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.addNwObjectToNwGroup, Vars);
                }
            }
        }

        private ModellingImportAreaSubnets ConvertSubnet(ModellingImportAreaSubnets importAreaSubnet)
        {
            ModellingImportAreaSubnets subnet = new()
            {
                Name = importAreaSubnet.Name,
            };

            if (importAreaSubnet.Ip.TryGetNetmask(out _))
            {
                (string Start, string End) ip = GlobalFunc.IpOperations.CidrToRangeString(importAreaSubnet.Ip);
                subnet.Ip = ip.Start;
                subnet.IpEnd = ip.End;
            }
            else if (importAreaSubnet.Ip.TrySplit('-', 1, out _) && IPAddressRange.TryParse(importAreaSubnet.Ip, out IPAddressRange ipRange))
            {
                subnet.Ip = ipRange.Begin.ToString();
                subnet.IpEnd = ipRange.End.ToString();
            }
            else
            {
                subnet.Ip = importAreaSubnet.Ip;
                subnet.IpEnd = importAreaSubnet.Ip;
            }

            subnet.Ip = subnet.Ip.StripOffNetmask();
            subnet.IpEnd = subnet.IpEnd.StripOffNetmask();

            return subnet;
        }

        private async Task<bool> DeleteArea(ModellingNetworkArea area)
        {
            try
            {
                await apiConnection.SendQueryAsync<NewReturning>(ModellingQueries.setAreaDeletedState, new { id = area.Id, deleted = true });
                await apiConnection.SendQueryAsync<NewReturning>(ModellingQueries.removeSelectedNwGroupObjectFromAllApps, new { nwGroupId = area.Id });
            }
            catch (Exception exc)
            {
                Log.WriteError("Import Area Subnet Data", $"Outdated Area {area.Name} could not be deleted.", exc);
                return false;
            }
            return true;
        }

        private async Task ReactivateArea(ModellingNetworkArea area)
        {
            try
            {
                await apiConnection.SendQueryAsync<NewReturning>(ModellingQueries.setAreaDeletedState, new { id = area.Id, deleted = false });
            }
            catch (Exception exc)
            {
                Log.WriteError("Reactivate Area", $"Area {area.Name}({area.IdString}) could not be reactivated.", exc);
            }
        }
    }
}
