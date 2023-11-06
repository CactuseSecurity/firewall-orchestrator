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
    public class AreaSubnetDataImport
    {
        private readonly ApiConnection apiConnection;
        private GlobalConfig globalConfig;
        private string importFile { get; set; } = "";
        private List<ModellingImportAreaData> importedAreas = new();
        private List<ModellingNetworkArea> existingAreas = new();


        /// <summary>
        /// Constructor for Area Subnet Data Import
        /// </summary>
        public AreaSubnetDataImport(ApiConnection apiConnection, GlobalConfig globalConfig)
        {
            this.apiConnection = apiConnection;
            this.globalConfig = globalConfig;
        }

        /// <summary>
        /// Run the Area Subnet Data Import
        /// </summary>
        public async Task<bool> Run()
        {
            await RunImportScript(globalConfig.ImportSubnetDataPath + ".py");
            ReadFile(globalConfig.ImportSubnetDataPath + ".json");

            int successCounter = 0;
            int failCounter = 0;
            int deleteCounter = 0;
            int deleteFailCounter = 0;
            try
            {
                importedAreas = JsonSerializer.Deserialize<List<ModellingImportAreaData>>(importFile) ?? throw new Exception("File could not be parsed.");
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
            catch (Exception exc)
            {
                Log.WriteError("Import Area Subnet Data", $"File could not be processed.", exc);
                return false;
            }
            Log.WriteDebug("Import Area Subnet Data", $"Imported {successCounter} areas, {failCounter} failed. Deleted {deleteCounter} areas, {deleteFailCounter} failed.");
            return true;
        }

        private async Task RunImportScript(string importScriptFile)
        {
            if(File.Exists(importScriptFile))
            {

            }
        }

        private void ReadFile(string filepath)
        {
            try
            {
                // /usr/local/fworch/etc/qip-export.json
                importFile = File.ReadAllText(filepath).Trim();
            }
            catch (Exception fileReadException)
            {
                Log.WriteError("Read file", $"File could not be found at {filepath}.", fileReadException);
                throw;
            }
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
                Log.WriteError("Import Area Subnet Data", $"Area {incomingArea.Name} could not be processed.", exc);
                return false;
            }
            return true;
        }

        private async Task NewArea(ModellingImportAreaData incomingArea)
        {
            ReturnId[]? returnIds = (await apiConnection.SendQueryAsync<NewReturning>(FWO.Api.Client.Queries.ModellingQueries.newArea, new { name = incomingArea.Name })).ReturnIds;
            if (returnIds != null)
            {
                foreach(var subnet in incomingArea.Subnets)
                {
                    var Variables = new
                    {
                        name = subnet.Name,
                        areaId = returnIds[0].NewId,
                        network = subnet.Network
                    };
                    await apiConnection.SendQueryAsync<NewReturning>(FWO.Api.Client.Queries.ModellingQueries.newAreaSubnet, Variables);
                }
            }
        }

        private async Task UpdateArea(ModellingImportAreaData incomingArea, ModellingNetworkArea existingArea)
        {
            List<ModellingImportAreaSubnets> subnetsToAdd = new (incomingArea.Subnets);
            List<NetworkSubnet> subnetsToDelete = new (existingArea.Subnets);
            foreach(var existingSubnet in existingArea.Subnets)
            {
                foreach(var incomingSubnet in incomingArea.Subnets)
                {
                    if(incomingSubnet.Name == existingSubnet.Name && incomingSubnet.Network == existingSubnet.Network)
                    {
                        subnetsToAdd.Remove(incomingSubnet);
                        subnetsToDelete.Remove(existingSubnet);
                    }
                }
            }
            foreach(var subnet in subnetsToDelete)
            {
                await apiConnection.SendQueryAsync<NewReturning>(Api.Client.Queries.ModellingQueries.deleteAreaSubnet, new { id = subnet.Id });
            }
            foreach(var subnet in subnetsToAdd)
            {
                var Variables = new
                {
                    name = subnet.Name,
                    areaId = existingArea.Id,
                    network = subnet.Network
                };
                await apiConnection.SendQueryAsync<NewReturning>(Api.Client.Queries.ModellingQueries.newAreaSubnet, Variables);
            }
        }

        private async Task<bool> DeleteArea(ModellingNetworkArea area)
        {
            try
            {
                await apiConnection.SendQueryAsync<NewReturning>(Api.Client.Queries.ModellingQueries.deleteArea, new { id = area.Id });
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
