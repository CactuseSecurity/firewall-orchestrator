using FWO.Logging;
using FWO.Api.Client;
using FWO.Api.Data;
using FWO.Config.Api;
using System.Text.Json;


namespace FWO.Middleware.Server
{
    /// <summary>
    /// Class handling the Area Subnet Data Import
    /// </summary>
    public class AreaSubnetDataImport : DataImportBase
    {
        private List<ModellingImportAreaData> importedAreas = new();
        private List<ModellingNetworkArea> existingAreas = new();


        /// <summary>
        /// Constructor for Area Subnet Data Import
        /// </summary>
        public AreaSubnetDataImport(ApiConnection apiConnection, GlobalConfig globalConfig) : base (apiConnection, globalConfig)
        {}

        /// <summary>
        /// Run the Area Subnet Data Import
        /// </summary>
        public async Task<bool> Run()
        {
            if(!await RunImportScript(globalConfig.ImportSubnetDataPath + ".py"))
            {
                Log.WriteInfo("Import Area Subnet Data", $"Script {globalConfig.ImportSubnetDataPath}.py failed but trying to import from existing file.");
            }
            ReadFile(globalConfig.ImportSubnetDataPath + ".json");

            int successCounter = 0;
            int failCounter = 0;
            int deleteCounter = 0;
            int deleteFailCounter = 0;
            try
            {
                List<ModellingImportNwData>? importedNwData = JsonSerializer.Deserialize<List<ModellingImportNwData>>(importFile) ?? throw new Exception("File could not be parsed.");
                if(importedNwData != null && importedNwData.Count > 0 && importedNwData[0].Areas != null)
                {
                    importedAreas = importedNwData[0].Areas;
                    existingAreas = await apiConnection.SendQueryAsync<List<ModellingNetworkArea>>(Api.Client.Queries.ModellingQueries.getAreas);
                    foreach(var incomingArea in importedAreas)
                    {
                        if(await SaveArea(incomingArea))
                        {
                            ++successCounter;
                        }
                        else
                        {
                            ++failCounter;
                        }
                    }
                    foreach(var existingArea in existingAreas)
                    {
                        if(importedAreas.FirstOrDefault(x => x.Name == existingArea.Name) == null)
                        {
                            if(await DeleteArea(existingArea))
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
                else
                {
                    Log.WriteInfo("Import Area Subnet Data", $"No Area Data found in {importFile} No changes done. ");
                }
            }
            catch (Exception exc)
            {
                Log.WriteError("Import Area Subnet Data", $"File could not be processed.", exc);
                return false;
            }
            Log.WriteInfo("Import Area Subnet Data", $"Imported {successCounter} areas, {failCounter} failed. Deleted {deleteCounter} areas, {deleteFailCounter} failed.");
            return true;
        }

        private async Task<bool> SaveArea(ModellingImportAreaData incomingArea)
        {
            try
            {
                ModellingNetworkArea? existingArea = existingAreas.FirstOrDefault(x => x.Name == incomingArea.Name);
                if(existingArea == null)
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
                creator = GlobalConfig.kImportAreaSubnetData
            };
            ReturnId[]? areaIds = (await apiConnection.SendQueryAsync<NewReturning>(Api.Client.Queries.ModellingQueries.newArea, AreaVar)).ReturnIds;
            if (areaIds != null)
            {
                foreach(var subnet in incomingArea.Subnets)
                {
                    var SubnetVar = new
                    {
                        name = subnet.Name,
                        ip = subnet.Ip,
                        ipEnd = subnet.IpEnd,
                        importSource = GlobalConfig.kImportAreaSubnetData
                    };
                    ReturnId[]? subnetIds= (await apiConnection.SendQueryAsync<NewReturning>(Api.Client.Queries.ModellingQueries.newAreaSubnet, SubnetVar)).ReturnIds;
                    if (subnetIds != null)
                    {
                        var Vars = new
                        {
                            nwObjectId = subnetIds[0].NewId,
                            nwGroupId = areaIds[0].NewId
                        };
                        await apiConnection.SendQueryAsync<ReturnId>(Api.Client.Queries.ModellingQueries.addNwObjectToNwGroup, Vars);
                    }
                }
            }
        }

        private async Task UpdateArea(ModellingImportAreaData incomingArea, ModellingNetworkArea existingArea)
        {
            List<ModellingImportAreaSubnets> subnetsToAdd = new (incomingArea.Subnets);
            List<NetworkSubnetWrapper> subnetsToDelete = new (existingArea.Subnets);
            foreach(var existingSubnet in existingArea.Subnets)
            {
                foreach(var incomingSubnet in incomingArea.Subnets)
                {
                    if(incomingSubnet.Name == existingSubnet.Content.Name && incomingSubnet.Ip == existingSubnet.Content.Ip && incomingSubnet.IpEnd == existingSubnet.Content.IpEnd)
                    {
                        subnetsToAdd.Remove(incomingSubnet);
                        subnetsToDelete.Remove(existingSubnet);
                    }
                }
            }
            foreach(var subnet in subnetsToDelete)
            {
                await apiConnection.SendQueryAsync<NewReturning>(Api.Client.Queries.OwnerQueries.deleteAreaSubnet, new { id = subnet.Content.Id });
            }
            foreach(var subnet in subnetsToAdd)
            {
                var SubnetVar = new
                {
                    name = subnet.Name,
                    ip = subnet.Ip,
                    ipEnd = subnet.IpEnd,
                    importSource = GlobalConfig.kImportAreaSubnetData
                };
                ReturnId[]? subnetIds= (await apiConnection.SendQueryAsync<NewReturning>(Api.Client.Queries.ModellingQueries.newAreaSubnet, SubnetVar)).ReturnIds;
                if (subnetIds != null)
                {
                    var Vars = new
                    {
                        nwObjectId = subnetIds[0].NewId,
                        nwGroupId = existingArea.Id,
                    };
                    await apiConnection.SendQueryAsync<ReturnId>(Api.Client.Queries.ModellingQueries.addNwObjectToNwGroup, Vars);
                }
            }
        }

        private async Task<bool> DeleteArea(ModellingNetworkArea area)
        {
            try
            {
                await apiConnection.SendQueryAsync<NewReturning>(Api.Client.Queries.ModellingQueries.deleteNwGroup, new { id = area.Id });
            }
            catch (Exception exc)
            {
                Log.WriteError("Import Area Subnet Data", $"Outdated Area {area.Name} could not be deleted.", exc);
                return false;
            }
            return true;
        }
    }
}
