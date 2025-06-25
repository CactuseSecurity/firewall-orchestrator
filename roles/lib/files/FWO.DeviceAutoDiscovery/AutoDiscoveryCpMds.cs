using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Data;
using FWO.Logging;
using MailKit.Security;
using RestSharp;
using System.Net;

namespace FWO.DeviceAutoDiscovery
{
    public class AutoDiscoveryCpMds : AutoDiscoveryBase
    {
        private readonly string Autodiscovery = "Autodiscovery";
        private readonly string CheckpointHost = "checkpoint-host";

        public AutoDiscoveryCpMds(Management mgm, ApiConnection apiConn) : base(mgm, apiConn) { }


        override public async Task<List<Management>> Run()
        {
            List<Management> discoveredDevices = [];
            if (SuperManagement == null)
            {
                return null!;
            }                
            Log.WriteAudit(Autodiscovery, $"starting discovery for {SuperManagement.Name} (id={SuperManagement.Id})");

            if (SuperManagement.DeviceType.Name == "Check Point")
            {
                Log.WriteDebug(Autodiscovery, $"discovering CP domains & gateways");

                (string sessionId, CheckPointClient restClientCP) = await LoginCp(SuperManagement);

                // when passing sessionId, we always need to use @ verbatim identifier for special chars in sessionId
                if (string.IsNullOrEmpty(SuperManagement.Uid) ||
                    (SuperManagement.DeviceType.CanBeSupermanager() && string.IsNullOrEmpty(SuperManagement.DomainUid)))  // pre v9 managements might not have a UID
                {
                    // update manager Uid in existing management; typically triggered in daily scheduler
                    await UpdateMgmUids(SuperManagement, restClientCP, @sessionId);
                }

                List<Domain> domainList = await restClientCP.GetDomains(@sessionId);
                discoveredDevices = await DiscoverDomainDevices(domainList, restClientCP);
                await LogoutCp(restClientCP, @sessionId);
            }
            return await GetDeltas(discoveredDevices);
        }

        override protected Management CreateManagement(Management superManagement, string domainName, string domainUid)
        {
            Management currentManagement = new()
            {
                Name = superManagement.Name + "__" + domainName,
                Uid = superManagement.Uid, 
                ImporterHostname = superManagement.ImporterHostname,
                Hostname = superManagement.Hostname,
                ImportCredential = superManagement.ImportCredential,
                Port = superManagement.Port,
                ImportDisabled = false,
                ForceInitialImport = true,
                HideInUi = false,
                ConfigPath = domainName,
                DomainUid = domainUid,
                DebugLevel = superManagement.DebugLevel,
                SuperManagerId = superManagement.Id,
                DeviceType = new DeviceType { Id = 9 },
                Devices = []
            };
            // if super manager is just a simple management, overwrite the default (supermanager) values
            if (domainName == "")
            {
                currentManagement.Name = superManagement.Name;
                currentManagement.ConfigPath = "";
                currentManagement.SuperManagerId = null;
                currentManagement.DomainUid = "";
                currentManagement.IsSupermanager = false;
            }
            return currentManagement;
        }

        private async Task<List<Management>> DiscoverDomainDevices(List<Domain> domainList, CheckPointClient restClientCP)
        {
            List<Management> discoveredDevices = [];
            foreach (Domain domain in domainList)
            {
                Log.WriteDebug(Autodiscovery, $"found domain '{domain.Name}'");
                Management currentManagement = CreateManagement(SuperManagement, domain.Name, domain.Uid);
                currentManagement.IsSupermanager = false;
                // session id pins this session to a specific domain (if domain is given during login)
                string sessionIdPerDomain = await LoginCp(currentManagement, restClientCP);
                currentManagement.Uid = await GetMgmUid(restClientCP, @sessionIdPerDomain, currentManagement.Hostname);

                if (sessionIdPerDomain != "")
                {
                    currentManagement.Devices = await GetGateways(restClientCP, @sessionIdPerDomain);
                    await LogoutCp(restClientCP, @sessionIdPerDomain);
                }
                discoveredDevices.Add(currentManagement);
            }
            return discoveredDevices;
        }

        private async Task UpdateMgmUids(Management mgm, CheckPointClient restClientCP, string sessionId)
        {
            if (mgm.DeviceType.Id == 13)    // MDS
            {
                mgm.Uid = await restClientCP.GetMdsUid(mgm);
            }
            else    // single management
            {
                mgm.Uid = await GetMgmUid(restClientCP, sessionId, SuperManagement.Hostname);
            }

            var vars = new { id = mgm.Id, uid = mgm.Uid, domainUid = mgm.DomainUid };
            _ = (await apiConnection.SendQueryAsync<ReturnId>(DeviceQueries.updateManagementUids, vars)).UpdatedId;
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
                    Log.WriteWarning(Autodiscovery, $"Did not receive a correct session ID when trying to login to manager {SuperManagement.Name} (id={SuperManagement.Id})");
                }
                else
                {
                    sessionId = sessionResponse.Data.SessionId;
                    Log.WriteDebug(Autodiscovery, $"successful CP Manager login, got SessionID: {sessionId}");
                }
            }
            else
            {
                string errorTxt = $"error while logging in to CP Manager: {sessionResponse.ErrorMessage} ";
                if (sessionResponse?.Data?.SessionId == "")
                    errorTxt += "could not authenticate to CP manager - got empty session ID";
                Log.WriteWarning(Autodiscovery, errorTxt);
                throw new AuthenticationException(errorTxt);
            }
            return sessionId;
        }

        private async Task<bool> LogoutCp(CheckPointClient restClientCP, string sessionId)
        {
            RestResponse<CpSessionAuthInfo> sessionResponse = await restClientCP.DeAuthenticateUser(@sessionId);
            if (sessionResponse.StatusCode == HttpStatusCode.OK)
            {
                Log.WriteDebug(Autodiscovery, $"successful CP Manager logout");
                return true;
            }
            else
            {
                Log.WriteWarning(Autodiscovery, $"error while logging out from CP Manager: {sessionResponse.ErrorMessage}");
                return false;
            }
        }

        private async Task<Device[]> GetGateways(CheckPointClient restClientCP, string sessionIdPerDomain)
        {
            List<CpDevice> devList = await restClientCP.GetGateways(@sessionIdPerDomain);
            List<Device> devices = [];

            // add devices to currentManagement
            foreach (CpDevice cpDev in devList)
            {
                if (cpDev.CpDevType != CheckpointHost)   // leave out the management host
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
            return [.. devices];
        }

        protected async Task<string> GetMgmUid(CheckPointClient restClientCP, string sessionIdPerDomain, string mgmHostname)
        {
            List<CpDevice> devList = await restClientCP.GetGateways(sessionIdPerDomain);

            string mgmIp = await IpOperations.DnsLookUp(mgmHostname);

            if (string.IsNullOrEmpty(mgmIp))
            {
                Log.WriteWarning(Autodiscovery, $"Could not resolve management host {mgmHostname} - using hostname instead");
                mgmIp = mgmHostname;
            }

            // Try to find a matching device by IP
            foreach (CpDevice cpDev in devList)
            {
                if (cpDev.CpDevType == CheckpointHost && cpDev.ManagementIp == mgmIp)
                {
                    return cpDev.Uid;
                }
            }

            // Fallback: return UID of the first checkpoint-host device
            var fallbackDevice = devList.FirstOrDefault(d => d.CpDevType == CheckpointHost);
            if (fallbackDevice != null)
            {
                Log.WriteWarning(Autodiscovery, $"No exact IP match for {mgmHostname}, falling back to first found checkpoint-host: {fallbackDevice.Name}");
                return fallbackDevice.Uid;
            }

            Log.WriteDebug(Autodiscovery, $"Did not find any checkpoint-host devices - could not set UID");
            return "";
        }

        protected async Task<List<CpDevice>> GetManagers(CheckPointClient restClientCP, string sessionId)
        {
            List<CpDevice> devList = await restClientCP.GetAllCpDevices(sessionId);
            List<CpDevice> mgmList = [.. devList.Where(d => d.CpDevType == CheckpointHost)];
            return mgmList;
        }

        protected static async Task<string> GetGlobalDomainUid(CheckPointClient restClientCP, string sessionIdPerDomain)
        {
            return await restClientCP.GetGlobalDomainUid(sessionIdPerDomain);
        }
    }
}
