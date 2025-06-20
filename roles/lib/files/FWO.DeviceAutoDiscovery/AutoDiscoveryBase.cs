using FWO.Api.Client;
using FWO.Data;
using FWO.Encryption;
using FWO.Logging;
using System.Text.Json;

namespace FWO.DeviceAutoDiscovery
{
    public class AutoDiscoveryBase
    {
        public Management SuperManagement { get; set; }
        private readonly ApiConnection apiConnection;


        public AutoDiscoveryBase(Management mgm, ApiConnection apiConn)
        {
            SuperManagement = mgm;

            string mainKey = AesEnc.GetMainKey();

            string decryptedSecret = SuperManagement.ImportCredential.Secret;

            // try to decrypt secret, keep it as is if failing
            try
            {
                decryptedSecret = AesEnc.Decrypt(SuperManagement.ImportCredential.Secret, mainKey);
            }
            catch (Exception)
            {
                Log.WriteWarning("AutoDiscovery", $"Could not decrypt secret in credential named '{SuperManagement.ImportCredential.Name}'.");
            }

            SuperManagement.ImportCredential.Secret = decryptedSecret;
            apiConnection = apiConn;
        }

        public virtual Task<List<Management>> Run()
        {
            return SuperManagement.DeviceType.Name switch
            {
                "FortiManager" => new AutoDiscoveryFortiManager(SuperManagement, apiConnection).Run(),
                "CheckPoint" => new AutoDiscoveryCpMds(SuperManagement, apiConnection).Run(),
                "Check Point" => new AutoDiscoveryCpMds(SuperManagement, apiConnection).Run(),
                _ => throw new NotSupportedException("SuperManager Type is not supported."),
            };
        }

        public async Task<List<Management>> GetDeltas(List<Management> discoveredManagements)
        {
            List<Management> deltaManagements = [];
            try
            {
                List<Management> existingManagements = await apiConnection.SendQueryAsync<List<Management>>(FWO.Api.Client.Queries.DeviceQueries.getManagementsDetails);

                foreach (Management discoveredMgmt in discoveredManagements.Where(x => x.ConfigPath != "global"))
                {
                    HandleDiscoveredManagement(discoveredMgmt, deltaManagements, existingManagements);
                Management? existMgmtDisregardingUid = FindManagementIfExist(discoveredMgmt, existingManagements);

                // deleted managements
                foreach (Management existMgmt in existingManagements.Where(mgt => mgt.SuperManagerId == SuperManagement.Id && mgt.ConfigPath != "global"))
                {
                    Management? foundMgmt = FindManagementIfExist(existMgmtDisregardingUid, discoveredManagements);
                    if (foundMgmt == null && !existMgmtDisregardingUid.ImportDisabled)
                    {
                        existMgmtDisregardingUid.Delete = true;
                        deltaManagements.Add(existMgmtDisregardingUid);
                    }
                }
                }
            }
            catch (Exception exc)
            {
                Log.WriteError("Autodiscovery", $"GetDeltas Ran into exception: ", exc);
            }
            return deltaManagements;
        }

        private static void HandleDiscoveredManagement(Management discoveredMgmt, List<Management> deltaManagements, List<Management> existingManagements)
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
                HandleChangedManagement(discoveredMgmt, existMgmt, deltaManagements);
            }
        }

        private static void HandleChangedManagement(Management discoveredMgmt, Management existMgmt, List<Management> deltaManagements)
        {
            Management changedMgmt = existMgmt;
            changedMgmt.Delete = false;
            bool foundChange = false;
            List<Device> newDevs = [];
            // new devices in existing management
            foreach (Device discoveredDev in discoveredMgmt.Devices)
            {
                if (CheckDeviceNotInMgmt(discoveredDev, existMgmt) || discoveredDev.ImportDisabled)
                {
                    discoveredDev.Delete = false;
                    newDevs.Add(discoveredDev);
                    foundChange = true;
                }
            }

            // deleted devices in existing management
            foreach (Device existDev in existMgmt.Devices)
            {
                if (CheckDeviceNotInMgmt(existDev, discoveredMgmt) && !existDev.ImportDisabled)
                {
                    existDev.Delete = true;
                    newDevs.Add(existDev);
                    foundChange = true;
                }
            }
            changedMgmt.Devices = [.. newDevs];

            if (foundChange || changedMgmt.ImportDisabled)
            {
                deltaManagements.Add(changedMgmt);
            }
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
                        DeleteManagement(changedMgmt, actions, counter);
                    }
                    else if (changedMgmt.Id == 0)   // adding new management
                    {

                        AddManagement(changedMgmt, actions, counter);
                    }
                    else
                    {
                        ChangeManagement(changedMgmt, actions, counter);
                    }
                }
            }
            catch (Exception exc)
            {
                Log.WriteError("Autodiscovery", $"ConvertToActions Ran into exception: ", exc);
            }
            return actions;
        }

        private void DeleteManagement(Management changedMgmt, List<ActionItem> actions, int counter)
        {
            actions.Add(new ActionItem
            {
                Number = ++counter,
                Supermanager = SuperManagement.Name,
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
                    Supermanager = SuperManagement.Name,
                    ActionType = ActionCode.DeleteGateway.ToString(),
                    ManagementId = changedMgmt.Id,
                    DeviceId = dev?.Id,
                    JsonData = null
                });
            }
        }

        private void AddManagement(Management changedMgmt, List<ActionItem> actions, int counter)
        {
            DeviceType devtype;
            if (changedMgmt.DeviceType != null || changedMgmt.DeviceType?.Id == 0)
                devtype = changedMgmt.DeviceType;
            else
                devtype = new DeviceType() { Id = SuperManagement.DeviceType.GetManagementTypeId() };

            Management MgtVariables = new()
            {
                Hostname = SuperManagement.Hostname,
                ImportCredential = SuperManagement.ImportCredential,
                ImporterHostname = SuperManagement.ImporterHostname,
                DebugLevel = SuperManagement.DebugLevel,
                Port = SuperManagement.Port,
                ImportDisabled = false,
                ForceInitialImport = true,
                HideInUi = false,
                ConfigPath = changedMgmt.ConfigPath,
                DomainUid = changedMgmt.DomainUid,
                Name = changedMgmt.Name,
                DeviceType = devtype,
                SuperManagerId = SuperManagement.Id
            };
            actions.Add(new ActionItem
            {
                Number = ++counter,
                Supermanager = SuperManagement.Name,
                ActionType = ActionCode.AddManagement.ToString(),
                ManagementId = null,
                DeviceId = null,
                JsonData = JsonSerializer.Serialize(MgtVariables)
            });

            foreach (Device dev in changedMgmt.Devices)
            {
                dev.DeviceType.Id = SuperManagement.DeviceType.GetGatewayTypeId();
                dev.Management.Id = 0;
                actions.Add(new ActionItem
                {
                    Number = ++counter,
                    Supermanager = SuperManagement.Name,
                    ActionType = ActionCode.AddGatewayToNewManagement.ToString(),
                    ManagementId = null,
                    DeviceId = null,
                    JsonData = JsonSerializer.Serialize(dev)
                });
            }
        }

        private void ChangeManagement(Management changedMgmt, List<ActionItem> actions, int counter)
        {
            if (changedMgmt.ImportDisabled)
            {
                actions.Add(new ActionItem
                {
                    Number = ++counter,
                    Supermanager = SuperManagement.Name,
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
                        Supermanager = SuperManagement.Name,
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
                        Supermanager = SuperManagement.Name,
                        ActionType = ActionCode.ReactivateGateway.ToString(),
                        ManagementId = changedMgmt.Id,
                        DeviceId = dev.Id,
                        JsonData = null
                    });
                }
                else
                {
                    dev.DeviceType.Id = SuperManagement.DeviceType.GetGatewayTypeId();
                    dev.Management.Id = changedMgmt.Id;
                    actions.Add(new ActionItem
                    {
                        Number = ++counter,
                        Supermanager = SuperManagement.Name,
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

