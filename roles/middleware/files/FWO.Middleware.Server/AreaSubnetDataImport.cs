using FWO.Logging;
using FWO.Api.Client;
using FWO.Api.Data;
using FWO.Config.Api;
using System.Text.Json;


namespace FWO.Middleware.Server
{
    public class AreaSubnetDataImport
    {
        private readonly ApiConnection apiConnection;
        private GlobalConfig globalConfig;
        private string importFile { get; set; }
        private List<ModellingImportAreaData> importedAreas = new();
        private List<ModellingNetworkArea> existingAreas = new();



        public AreaSubnetDataImport(ApiConnection apiConnection, GlobalConfig globalConfig)
        {
            this.apiConnection = apiConnection;
            this.globalConfig = globalConfig;
            Read();
        }

        private void Read()
        {
            try
            {
                // /usr/local/fworch/etc/qip-export.csv
                importFile = File.ReadAllText(globalConfig.ImportSubnetDataPath).Trim();
            }
            catch (Exception fileReadException)
            {
                Log.WriteError("Read file", $"File could not be found at {globalConfig.ImportSubnetDataPath}.", fileReadException);
                throw;
            }
        }

        public async Task<bool> Run()
        {
            int successCounter = 0;
            int failCounter = 0;
            int deleteCounter = 0;
            int deleteFailCounter = 0;
            try
            {
                importedAreas = JsonSerializer.Deserialize<List<ModellingImportAreaData>>(importFile) ?? throw new Exception("File could not be parsed.");

                // ExtractFile();
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

        // private void ExtractFile()
        // {
        //     // Todo: move to predefined import format
        //     importedAreas = new List<ModellingNetworkArea>();
        //     var lines = importFile.Split('\n');
        //     ModellingNetworkArea newArea = new();
        //     foreach(var line in lines.Skip(1))
        //     {
        //         var values = line.Split(',');
        //         string subnetName = values[0].Replace("\"", "");
        //         string areaName = subnetName.Substring(0, 4).Remove(1, 1).Insert(1, "A");
        //         string ipAddr = values[1].Replace("\"", "");
        //         string subnetMask = values[7].Replace("\"", "");

        //         IPAddressRange newRange = IPAddressRange.Parse($"{ipAddr}/{subnetMask}");
        //         NetworkSubnet newSubnet = new NetworkSubnet(){ Name = subnetName, Network = newRange.ToCidrString() };

        //         ModellingNetworkArea? startedArea = importedAreas.FirstOrDefault(x => x.Name == areaName);
        //         if(startedArea != null)
        //         {
        //             startedArea.Subnets.Add(newSubnet);
        //         }
        //         else
        //         {
        //             importedAreas.Add(new ModellingNetworkArea(){ Name = areaName, Subnets = new List<NetworkSubnet>(){ newSubnet }});
        //         }
        //     }
        // }

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
