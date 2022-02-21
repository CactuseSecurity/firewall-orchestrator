using FWO.Api.Data;
using FWO.ApiClient;
using FWO.Logging;
using System.Text.Json;


namespace FWO.DeviceAutoDiscovery
{
    public class AutoDiscoveryBase
    {
        public Management superManagement = new Management();
        private readonly APIConnection apiConnection;

        List<Management> existingManagements = new List<Management>();


        public AutoDiscoveryBase (Management mgm, APIConnection apiConn) 
        {
            superManagement = mgm;
            apiConnection = apiConn;
        }

        public virtual Task<List<Management>> Run()
        {
            return superManagement.DeviceType.Name switch
            {
                "FortiManager" => new AutoDiscoveryFortiManager(superManagement, apiConnection).Run(),
                "CheckPoint" => new AutoDiscoveryCpMds(superManagement, apiConnection).Run(),
                _ => throw new NotSupportedException("SuperManager Type is not supported."),
            };
        } 

        public async Task<List<Management>> GetDeltas(List<Management> discoveredManagements)
        {
            List<Management> deltaManagements = new List<Management>();
            try
            {
                existingManagements = await apiConnection.SendQueryAsync<List<Management>>(FWO.ApiClient.Queries.DeviceQueries.getManagementsDetails);

                foreach (Management discoveredMgmt in discoveredManagements.Where(x => x.ConfigPath != "global"))
                {
                    Management? existMgmt = FindManagementIfExist(discoveredMgmt, existingManagements);
                    if (existMgmt == null)
                    {
                        // new management
                        discoveredMgmt.Delete = false;
                        deltaManagements.Add(discoveredMgmt);
                    }
                    else
                    {
                        Management changedMgmt = existMgmt;
                        changedMgmt.Delete = false;
                        bool foundChange = false;
                        List<Device> newDevs = new List<Device>();
                        // new devices in existing management
                        foreach (Device discoveredDev in discoveredMgmt.Devices)
                        {
                            if (checkDeviceNotInMgmt(discoveredDev, existMgmt))
                            {
                                discoveredDev.Delete = false;
                                newDevs.Add(discoveredDev);
                                foundChange = true;
                            }
                        }
                        
                        // deleted devices in existing management
                        foreach(Device existDev in existMgmt.Devices)
                        {
                            if (checkDeviceNotInMgmt(existDev, discoveredMgmt))
                            {
                                existDev.Delete = true;
                                newDevs.Add(existDev);
                                foundChange = true;
                            }
                        }
                        changedMgmt.Devices = newDevs.ToArray();

                        if(foundChange)
                        {
                            deltaManagements.Add(changedMgmt);
                        }
                    }
                }
                // deleted managements
                foreach (Management existMgmt in existingManagements.Where(mgt => mgt.SuperManagerId == superManagement.Id && mgt.ConfigPath != "global"))
                {
                    Management? foundMgmt = FindManagementIfExist(existMgmt, discoveredManagements);
                    if(foundMgmt == null)
                    {
                        existMgmt.Delete = true;
                        deltaManagements.Add(existMgmt);
                    }
                }
            }
            catch(Exception exc)
            {
                Log.WriteError("Autodiscovery", $"GetDeltas Ran into exception: ", exc);
            }
            return deltaManagements;
        }

        private Management? FindManagementIfExist(Management mgm, List<Management> mgmtList)
        {
            Management? existingManagement = mgmtList.FirstOrDefault(x =>
                x.Name == mgm.Name
                && x.ConfigPath == mgm.ConfigPath 
                && x.SuperManagerId == mgm.SuperManagerId);
            if(existingManagement != null)
            {
                return existingManagement;
            }
            return null;
        }

        private bool checkDeviceNotInMgmt(Device dev, Management mgmt)
        {
            if (mgmt.Devices.FirstOrDefault(devInMgt => 
                devInMgt.Name == dev.Name && devInMgt.LocalRulebase == dev.LocalRulebase) != null)
            {
                return false;
            }
            return true;
        }

        public List<ActionItem> ConvertToActions(List<Management> diffList)
        {
            List<ActionItem> actions = new List<ActionItem>();
            try
            {
                foreach(Management changedMgmt in diffList)
                {
                    if(changedMgmt.Delete)
                    {
                        actions.Add(new ActionItem 
                        {
                            Supermanager = superManagement.Name, 
                            ActionType = ActionCode.DeleteManagement.ToString(), 
                            ManagementId = changedMgmt.Id, 
                            DeviceId = null, 
                            JsonData = null
                        });
                        foreach(Device dev in changedMgmt.Devices)
                        {
                            actions.Add(new ActionItem 
                            {
                                Supermanager = superManagement.Name, 
                                ActionType = ActionCode.DeleteGateway.ToString(), 
                                ManagementId = changedMgmt.Id, 
                                DeviceId = dev?.Id, 
                                JsonData = null
                            });
                        }
                    }
                    else if (changedMgmt.Id == 0)
                    {
                        DeviceType devtype = new DeviceType(){ Id = superManagement.DeviceType.GetManagementTypeId() };
                        Management MgtVariables = new Management
                        {
                            Hostname = superManagement.Hostname,
                            ImportUser = superManagement.ImportUser,
                            Password = superManagement.Password, // no legacy managements are supermanager
                            ImporterHostname = superManagement.ImporterHostname,
                            DebugLevel = superManagement.DebugLevel,
                            Port = superManagement.Port,
                            ImportDisabled = false,
                            ForceInitialImport = true,
                            HideInUi = false,
                            ConfigPath = changedMgmt.ConfigPath,
                            Name = changedMgmt.Name,
                            DeviceType = devtype,
                            SuperManagerId = superManagement.Id
                        };
                        actions.Add(new ActionItem 
                        {
                            Supermanager = superManagement.Name, 
                            ActionType = ActionCode.AddManagement.ToString(), 
                            ManagementId = null, 
                            DeviceId = null, 
                            JsonData = JsonSerializer.Serialize(MgtVariables)
                        });

                        foreach(Device dev in changedMgmt.Devices)
                        {
                            dev.DeviceType.Id = superManagement.DeviceType.GetGatewayTypeId();
                            dev.Management.Id = 0;
                            actions.Add(new ActionItem 
                            {
                                Supermanager = superManagement.Name, 
                                ActionType = ActionCode.AddGatewayToNewManagement.ToString(), 
                                ManagementId = null, 
                                DeviceId = null, 
                                JsonData = JsonSerializer.Serialize(dev)
                            });
                        }
                    }
                    else
                    {
                        foreach(Device dev in changedMgmt.Devices)
                        {
                            if(dev.Delete)
                            {
                                actions.Add(new ActionItem 
                                {
                                    Supermanager = superManagement.Name, 
                                    ActionType = ActionCode.DeleteGateway.ToString(), 
                                    ManagementId = changedMgmt.Id, 
                                    DeviceId = dev?.Id, 
                                    JsonData = null
                                });
                            }
                            else
                            {
                                dev.DeviceType.Id = superManagement.DeviceType.GetGatewayTypeId();
                                dev.Management.Id = changedMgmt.Id;
                                actions.Add(new ActionItem 
                                {
                                    Supermanager = superManagement.Name, 
                                    ActionType = ActionCode.AddGatewayToExistingManagement.ToString(), 
                                    ManagementId = changedMgmt.Id, 
                                    DeviceId = null, 
                                    JsonData = JsonSerializer.Serialize(dev)
                                });
                            }
                        }
                    }
                }
            }
            catch(Exception exc)
            {
                Log.WriteError("Autodiscovery", $"ConvertToActions Ran into exception: ", exc);
            }
            return actions;
        }
    }
}
