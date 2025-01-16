using FWO.Basics;
using FWO.Api.Data;
using FWO.Api.Client;
using FWO.Logging;
using FWO.Rest.Client;
using System.Net;
using RestSharp;
using System.Runtime.CompilerServices;
using FWO.Api.Client.Queries;

namespace FWO.DeviceAutoDiscovery
{
    public class AutoDiscoveryCpMds : AutoDiscoveryBase
    {
        public AutoDiscoveryCpMds(Management mgm, ApiConnection apiConn) : base(mgm, apiConn) { }


        public override async Task<List<Management>> Run()
        {
            if (superManagement == null)
                return null!;
            else
            {
                List<Management> discoveredDevices = [];
                Log.WriteAudit("Autodiscovery", $"starting discovery for {superManagement.Name} (id={superManagement.Id})");

                if (superManagement.DeviceType.Name == "Check Point")
                {
                    string ManagementType = "";
                    Log.WriteDebug("Autodiscovery", $"discovering CP domains & gateways");
                    (string sessionId, CheckPointClient restClientCP) = await LoginCp(superManagement);
                    // when passing sessionId, we always need to use @ verbatim identifier for special chars in sessionId
                    if (string.IsNullOrEmpty(superManagement.Uid))  // pre v9 managements might not have a UID
                    {
                        superManagement.Uid = await GetMgmUid(restClientCP, sessionId, ManagementType, superManagement.Name);

                        // update manager UID in existing management; typically triggered in daily scheduler
                        // this update happens only once when AutoDiscovery v9.0 is run for the first time
                        var vars = new { id = superManagement.Id, uid = superManagement.Uid };
                        _ = (await apiConnection.SendQueryAsync<ReturnId>(DeviceQueries.updateManagementUid, vars)).UpdatedId;
                    }
                    RestResponse<CpDomainHelper> domainResponse = await restClientCP.GetDomains(@sessionId);

                    if (domainResponse.StatusCode == HttpStatusCode.OK && domainResponse.IsSuccessful && domainResponse.Data?.DomainList != null)
                    {
                        List<Domain> domainList = domainResponse.Data.DomainList;
                        if (domainList.Count == 0)
                        {
                            Log.WriteDebug("Autodiscovery", $"found no domains - assuming this is a standard management, adding dummy domain with empty name");
                            domainList.Add(new Domain { Name = "" });
                            ManagementType = "stand-alone";
                        }
                        else
                            ManagementType = "MDS";

                        foreach (Domain domain in domainList)
                        {
                            Log.WriteDebug("Autodiscovery", $"found domain '{domain.Name}'");
                            Management currentManagement = CreateManagement(superManagement, domain);

                            // session id pins this session to a specific domain (if domain is given during login)
                            string sessionIdPerDomain = await LoginCp(currentManagement, restClientCP);
                            if (string.IsNullOrEmpty(currentManagement.Uid))
                                currentManagement.Uid = await GetMgmUid(restClientCP, @sessionIdPerDomain, ManagementType, currentManagement.Name);

                            if (sessionIdPerDomain != "")
                            {
                                currentManagement.Devices = await GetGateways(restClientCP, @sessionIdPerDomain, ManagementType);
                                await LogoutCp(restClientCP, @sessionIdPerDomain);
                            }
                            discoveredDevices.Add(currentManagement);
                        }
                        Log.WriteDebug("Autodiscovery", $"found a total of {domainList.Count} domains");
                    }
                    else
                        Log.WriteWarning("AutoDiscovery", $"error while getting domain list: {domainResponse.ErrorMessage}");
                    await LogoutCp(restClientCP, @sessionId);
                }
                return await GetDeltas(discoveredDevices);
            }
        }

        private static Management CreateManagement(Management superManagement, Domain domain)
        {
            Management currentManagement = new()
            {
                Name = superManagement.Name + "__" + domain.Name,
                Uid = superManagement.Uid,
                ImporterHostname = superManagement.ImporterHostname,
                Hostname = superManagement.Hostname,
                ImportCredential = superManagement.ImportCredential,
                Port = superManagement.Port,
                ImportDisabled = false,
                ForceInitialImport = true,
                HideInUi = false,
                ConfigPath = domain.Name,
                DomainUid = domain.Uid,
                DebugLevel = superManagement.DebugLevel,
                SuperManagerId = superManagement.Id,
                DeviceType = new DeviceType { Id = 9 },
                Devices = []
            };
            // if super manager is just a simple management, overwrite the default (supermanager) values
            if (domain.Name == "")
            {
                currentManagement.Name = superManagement.Name;
                currentManagement.ConfigPath = "";
                currentManagement.SuperManagerId = null;
                currentManagement.DomainUid = "";
            }
            return currentManagement;        
        }

        private async Task<(string, CheckPointClient)> LoginCp(Management mgm)
        {
            CheckPointClient restClientCP = new(mgm);
            return (await LoginCp(mgm, restClientCP), restClientCP);
        }
        private async Task<string> LoginCp(Management mgm, CheckPointClient restClientCP)
        {
            string? domainString = mgm.ConfigPath;
            string sessionId = "";
            if (mgm.DomainUid != null && mgm.DomainUid != "")
                domainString = mgm.DomainUid;
            RestResponse<CpSessionAuthInfo> sessionResponse = await restClientCP.AuthenticateUser(mgm.ImportCredential.ImportUser, mgm.ImportCredential.Secret, domainString);
            if (sessionResponse.StatusCode == HttpStatusCode.OK && sessionResponse.IsSuccessful && sessionResponse.Data?.SessionId != null && sessionResponse.Data?.SessionId != "")
            {
                if (sessionResponse?.Data?.SessionId == null || sessionResponse.Data.SessionId == "")
                {
                    Log.WriteWarning("Autodiscovery", $"Did not receive a correct session ID when trying to login to manager {superManagement.Name} (id={superManagement.Id})");
                }
                else
                {
                    sessionId = sessionResponse.Data.SessionId;
                    Log.WriteDebug("Autodiscovery", $"successful CP Manager login, got SessionID: {sessionId}");
                }
            }
            else
            {
                string errorTxt = $"error while logging in to CP Manager: {sessionResponse.ErrorMessage} ";
                if (sessionResponse?.Data?.SessionId == "")
                    errorTxt += "could not authenticate to CP manager - got empty session ID";
                Log.WriteWarning("AutoDiscovery", errorTxt);
                throw new Exception(errorTxt);
            }
            return sessionId;
        }

        private static async Task<bool> LogoutCp(CheckPointClient restClientCP, string sessionId)
        {
            RestResponse<CpSessionAuthInfo> sessionResponse = await restClientCP.DeAuthenticateUser(@sessionId);
            if (sessionResponse.StatusCode == HttpStatusCode.OK)
            {
                Log.WriteDebug("Autodiscovery", $"successful CP Manager logout");
                return true;
            }
            else
            {
                Log.WriteWarning("Autodiscovery", $"error while logging out from CP Manager: {sessionResponse.ErrorMessage}");
                return false;
            }
        }

        private async Task<Device[]> GetGateways(CheckPointClient restClientCP, string sessionIdPerDomain, string ManagementType)
        {
            // now fetching per device information
            List<CpDevice> devList = await restClientCP.GetGateways(@sessionIdPerDomain, ManagementType);
            List<Device> devices = [];

            // add devices to currentManagement
            foreach (CpDevice cpDev in devList)
            {
                if (cpDev.CpDevType != "checkpoint-host")   // leave out the management host?!
                {
                    Device dev = new()
                    {
                        Name = cpDev.Name,
                        Uid = cpDev.Uid,
                        DeviceType = new DeviceType { Id = 9 } // CheckPoint GW
                    };
                    devices.Add(dev);
                }
            }
            return devices.ToArray();
        }

        private static async Task<string> GetMgmUid(CheckPointClient restClientCP, string sessionIdPerDomain, string ManagementType, string mgmName)
        {
            List<CpDevice> devList = await restClientCP.GetGateways(@sessionIdPerDomain, ManagementType);
            foreach (CpDevice cpDev in devList)
            {
                if (cpDev.CpDevType == "checkpoint-host" && cpDev.Name == mgmName)
                    return cpDev.Uid;
            }
            Log.WriteDebug("Autodiscovery", $"Did not find management host {mgmName} in device list - could not set UID");
            return "";
        }

    }

}
