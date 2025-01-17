using System.Text.Json;

using FWO.Api.Data;
using FWO.Api.Client;
using FWO.Logging;
using FWO.Encryption;

namespace FWO.DeviceAutoDiscovery
{
    public class AutoDiscoveryBase
{
        public Management superManagement = new();
        protected readonly ApiConnection apiConnection;

        List<Management> existingManagements = [];


        public AutoDiscoveryBase(Management mgm, ApiConnection apiConn)
        {
            superManagement = mgm;

            string mainKey = AesEnc.GetMainKey();

            string decryptedSecret = superManagement.ImportCredential.Secret;

            // try to decrypt secret, keep it as is if failing
            try
            {
                decryptedSecret = AesEnc.Decrypt(superManagement.ImportCredential.Secret, mainKey);
            }
            catch (Exception)
            {
                // Log.WriteWarning("AutoDiscovery", $"Found unencrypted credential secret: {superManagement.ImportCredential.Name}.");
                Log.WriteWarning("AutoDiscovery", $"Could not decrypt secret in credential named: {superManagement.ImportCredential.Name}.");
            }

            superManagement.ImportCredential.Secret = decryptedSecret;
            apiConnection = apiConn;
        }

        public virtual Task<List<Management>> Run()
        {
            return superManagement.DeviceType.Name switch
            {
                "FortiManager" => new AutoDiscoveryFortiManager(superManagement, apiConnection).Run(),
                "CheckPoint" => new AutoDiscoveryCpMds(superManagement, apiConnection).Run(),
                "Check Point" => new AutoDiscoveryCpMds(superManagement, apiConnection).Run(),
                _ => throw new NotSupportedException("SuperManager Type is not supported."),
            };
        }

        public async Task<List<Management>> GetDeltas(List<Management> discoveredManagements)
        {
            List<Management> deltaManagements = [];
            try
            {
                existingManagements = await apiConnection.SendQueryAsync<List<Management>>(FWO.Api.Client.Queries.DeviceQueries.getManagementsDetails);

                foreach (Management discoveredMgmt in discoveredManagements.Where(x => x.ConfigPath != "global"))
                {
                    // Management? existMgmt = FindManagementIfExist(discoveredMgmt, existingManagements);
                    Management? existMgmtDisregardingUid = FindManagementIfExist(discoveredMgmt, existingManagements);
                    if (existMgmtDisregardingUid == null)
                    {
                        // new management
                        discoveredMgmt.Delete = false;
                        deltaManagements.Add(discoveredMgmt);
                    }
                    else
                    {
                        Management changedMgmt = existMgmtDisregardingUid;
                        changedMgmt.Delete = false;
                        bool foundChange = false;
                        List<Device> newDevs = [];
                        // new devices in existing management
                        foreach (Device discoveredDev in discoveredMgmt.Devices)
                        {
                            if (CheckDeviceNotInMgmt(discoveredDev, existMgmtDisregardingUid) || discoveredDev.ImportDisabled)
                            {
                                discoveredDev.Delete = false;
                                newDevs.Add(discoveredDev);
                                foundChange = true;
                            }
                        }

                        // deleted devices in existing management
                        foreach (Device existDev in existMgmtDisregardingUid.Devices)
                        {
                            if (CheckDeviceNotInMgmt(existDev, discoveredMgmt) && !existDev.ImportDisabled)
                            {
                                existDev.Delete = true;
                                newDevs.Add(existDev);
                                foundChange = true;
                            }
                        }
                        changedMgmt.Devices = newDevs.ToArray();

                        if (foundChange || changedMgmt.ImportDisabled)
                        {
                            deltaManagements.Add(changedMgmt);
                        }
                    }
                }
                // deleted managements
                foreach (Management existMgmtDisregardingUid in existingManagements.Where(mgt => mgt.SuperManagerId == superManagement.Id && mgt.ConfigPath != "global"))
                {
                    Management? foundMgmt = FindManagementIfExist(existMgmtDisregardingUid, discoveredManagements);
                    if (foundMgmt == null && !existMgmtDisregardingUid.ImportDisabled)
                    {
                        existMgmtDisregardingUid.Delete = true;
                        deltaManagements.Add(existMgmtDisregardingUid);
                    }
                }
            }
            catch (Exception exc)
            {
                Log.WriteError("Autodiscovery", $"GetDeltas Ran into exception: ", exc);
            }
            return deltaManagements;
        }

        private static Management? FindManagementIfExist(Management mgm, List<Management> mgmtList)
        {
            Management? existingManagement = mgmtList.FirstOrDefault(m => m.Equals(mgm));
            if (existingManagement != null)
            {
                return existingManagement;
            }
            return null;
        }

        private static bool CheckDeviceNotInMgmt(Device dev, Management mgmt)
        {
            if (mgmt.Devices.FirstOrDefault(devInMgt => devInMgt.Equals(dev)) != null)
            {
                return false;
            }
            return true;
        }

        protected virtual Management CreateManagement(Management superManagement, string domainName, string domainUid) { return new(); }


        public List<ActionItem> ConvertToActions(List<Management> diffList)
        {
            List<ActionItem> actions = [];
            int counter = 0;
            try
            {
                foreach (Management changedMgmt in diffList)
                {
                    if (changedMgmt.Delete)
                    {
                        actions.Add(new ActionItem
                        {
                            Number = ++counter,
                            Supermanager = superManagement.Name,
                            ActionType = ActionCode.DeleteManagement.ToString(),
                            ManagementId = changedMgmt.Id,
                            DeviceId = null,
                            JsonData = null
                        });
                        foreach (Device dev in changedMgmt.Devices)
                        {
                            actions.Add(new ActionItem
                            {
                                Number = ++counter,
                                Supermanager = superManagement.Name,
                                ActionType = ActionCode.DeleteGateway.ToString(),
                                ManagementId = changedMgmt.Id,
                                DeviceId = dev?.Id,
                                JsonData = null
                            });
                        }
                    }
                    else if (changedMgmt.Id == 0)   // adding new management
                    {
                        DeviceType devtype = new();
                        if (changedMgmt.DeviceType != null || changedMgmt.DeviceType?.Id == 0)
                            devtype = changedMgmt.DeviceType;
                        else
                            devtype = new DeviceType() { Id = superManagement.DeviceType.GetManagementTypeId() };

                        Management MgtVariables = new()
                        {
                            Hostname = superManagement.Hostname,
                            ImportCredential = superManagement.ImportCredential,
                            ImporterHostname = superManagement.ImporterHostname,
                            DebugLevel = superManagement.DebugLevel,
                            Port = superManagement.Port,
                            ImportDisabled = false,
                            ForceInitialImport = true,
                            HideInUi = false,
                            ConfigPath = changedMgmt.ConfigPath,
                            DomainUid = changedMgmt.DomainUid,
                            Name = changedMgmt.Name,
                            Uid = changedMgmt.Uid,
                            DeviceType = devtype,
                            SuperManagerId = superManagement.Id
                        };
                        actions.Add(new ActionItem
                        {
                            Number = ++counter,
                            Supermanager = superManagement.Name,
                            ActionType = ActionCode.AddManagement.ToString(),
                            ManagementId = null,
                            DeviceId = null,
                            JsonData = JsonSerializer.Serialize(MgtVariables)
                        });

                        foreach (Device dev in changedMgmt.Devices)
                        {
                            dev.DeviceType.Id = superManagement.DeviceType.GetGatewayTypeId();
                            dev.Management.Id = 0;
                            actions.Add(new ActionItem
                            {
                                Number = ++counter,
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
                        if (changedMgmt.ImportDisabled)
                        {
                            actions.Add(new ActionItem
                            {
                                Number = ++counter,
                                Supermanager = superManagement.Name,
                                ActionType = ActionCode.ReactivateManagement.ToString(),
                                ManagementId = changedMgmt.Id,
                                DeviceId = null,
                                JsonData = null
                            });
                        }
                        foreach (Device dev in changedMgmt.Devices)
                        {
                            if (dev.Delete)
                            {
                                actions.Add(new ActionItem
                                {
                                    Number = ++counter,
                                    Supermanager = superManagement.Name,
                                    ActionType = ActionCode.DeleteGateway.ToString(),
                                    ManagementId = changedMgmt.Id,
                                    DeviceId = dev?.Id,
                                    JsonData = null
                                });
                            }
                            else if (dev.ImportDisabled)
                            {
                                actions.Add(new ActionItem
                                {
                                    Number = ++counter,
                                    Supermanager = superManagement.Name,
                                    ActionType = ActionCode.ReactivateGateway.ToString(),
                                    ManagementId = changedMgmt.Id,
                                    DeviceId = dev.Id,
                                    JsonData = null
                                });
                            }
                            else
                            {
                                dev.DeviceType.Id = superManagement.DeviceType.GetGatewayTypeId();
                                dev.Management.Id = changedMgmt.Id;
                                actions.Add(new ActionItem
                                {
                                    Number = ++counter,
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
            catch (Exception exc)
            {
                Log.WriteError("Autodiscovery", $"ConvertToActions Ran into exception: ", exc);
            }
            return actions;
        }

    }
}

