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

            // iterate over all files
            foreach (var importfilePathAndName in importfilePathAndNames)
            {
                if (!RunImportScript(importfilePathAndName + ".py"))
                {
                    Log.WriteInfo("Import Area Network Data", $"Script {importfilePathAndName}.py failed but trying to import from existing file.");
                }

                try
                {
                    Log.WriteInfo("Importing Area Network Data from file ", $"Script {importfilePathAndName}.json.");
                    ReadFile(importfilePathAndName + ".json");
                    ModellingImportNwData? nwData = Import();

                    if (nwData != null)
                    {
                        AllNwData.Add(ConvertNwDataToRanges(nwData));
                    }

                }
                catch (Exception ex)
                {
                    Log.WriteError("Import Network Data", $"Import could not be processed.", ex);
                }
            }

            // merge all data into a single list of areas
            ModellingImportNwData mergedNwData = MergeNetworkData(AllNwData);

            if (mergedNwData != null && mergedNwData.Areas != null)
            {
                await SaveMergedNwData(mergedNwData);
            }
            else
            {
                Log.WriteInfo("Import Area Network Data", $"No valid network data found in any of the following import files {importfilePathAndNames}. No changes were made.");
            }

            return true;
        }

        private async Task SaveMergedNwData(ModellingImportNwData mergedNwData)
        {
            int successCounter = 0;
            int failCounter = 0;
            int deleteCounter = 0;
            int deleteFailCounter = 0;

            existingAreas = await apiConnection.SendQueryAsync<List<ModellingNetworkArea>>(ModellingQueries.getAreas);

            foreach (ModellingImportAreaData area in mergedNwData.Areas)
            {
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

            Log.WriteInfo("Import Area Subnet Data", $"Imported {successCounter} area successfully, {failCounter} areas failed. Deleted {deleteCounter} areas, {deleteFailCounter} failed.");
        }

        private List<ModellingImportNwData> ConvertNwDataListToRanges(List<ModellingImportNwData> AllNwData)
        {
            List<ModellingImportNwData> result = [];
            foreach (ModellingImportNwData nwData in AllNwData)
            {
                result.Add(ConvertNwDataToRanges(nwData));
            }
            return result;

        }
        private ModellingImportNwData ConvertNwDataToRanges(ModellingImportNwData nwData)
        {
            ModellingImportNwData result = new();

            foreach (ModellingImportAreaData area in nwData.Areas)
            {
                ConvertAreaToRanges(area);
                result.Areas.Add(area);
            }
            return result;
        }

        private void ConvertAreaToRanges(ModellingImportAreaData area)
        {
            for (int i = area.Subnets.Count - 1; i >= 0; i--)
            {
                ModellingImportAreaSubnets? newSubnet = ConvertSubnet(area.Subnets[i]);
                area.Subnets[i] = newSubnet;
            }
        }

        private ModellingImportAreaData MergeArea(ModellingImportAreaData area1, ModellingImportAreaData area2)
        {
            ModellingImportAreaData resultArea = area1; // make a copy of area1 including all subnets

            foreach (ModellingImportAreaSubnets ipRange in area2.Subnets)
            {
                bool found = false;
                foreach (var existingIpRange in area1.Subnets)
                {
                    if (ipRange.Ip == existingIpRange.Ip && ipRange.IpEnd == existingIpRange.IpEnd)
                    {
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    resultArea.Subnets.Add(ipRange);
                }
            }
            return resultArea;
        }

        private ModellingImportNwData MergeNetworkData(List<ModellingImportNwData> AllNwData)
        {
            ModellingImportNwData mergedNwData = new();

            foreach (ModellingImportNwData nwData in AllNwData)
            {
                foreach (ModellingImportAreaData area in nwData.Areas)
                {
                    // find the current area in the merged data
                    ModellingImportAreaData? existingArea = mergedNwData.Areas.FirstOrDefault(_ => _.IdString == area.IdString);
                    if (existingArea is null)
                    {
                        mergedNwData.Areas.Add(area);
                    }
                    else
                    {
                        // replace the existing area with a merge of existing and newly found
                        mergedNwData.Areas.Remove(existingArea);
                        mergedNwData.Areas.Add(MergeArea(existingArea, area));
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
            ReturnId[]? areaIds = (await apiConnection.SendQueryAsync<NewReturning>(ModellingQueries.newArea, AreaVar)).ReturnIds;
            if (areaIds != null)
            {
                foreach (var subnet in incomingArea.Subnets)
                {
                    // ModellingImportAreaSubnets parsedSubnet = ConvertSubnet(subnet);

                    var SubnetVar = new
                    {
                        name = subnet.Name,
                        ip = subnet.Ip,
                        ipEnd = subnet.IpEnd,
                        importSource = GlobalConst.kImportAreaSubnetData
                    };

                    ReturnId[]? subnetIds = (await apiConnection.SendQueryAsync<NewReturning>(ModellingQueries.newAreaSubnet, SubnetVar)).ReturnIds;
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
                        (incomingSubnet.IpEnd == existingSubnet.Content.IpEnd.StripOffNetmask()))
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
                ReturnId[]? subnetIds = (await apiConnection.SendQueryAsync<NewReturning>(ModellingQueries.newAreaSubnet, SubnetVar)).ReturnIds;
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
