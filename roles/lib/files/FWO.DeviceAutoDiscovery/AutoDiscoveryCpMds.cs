using FWO.Api.Client;
using FWO.Data;
using FWO.Logging;
using MailKit.Security;
using RestSharp;
using FWO.Api.Client.Queries;
using FWO.Basics;
using System.Net;

namespace FWO.DeviceAutoDiscovery
{
    public class AutoDiscoveryCpMds : AutoDiscoveryBase
    {
        private readonly string Autodiscovery = GlobalConst.kAutodiscovery;

        public AutoDiscoveryCpMds(Management mgm, ApiConnection apiConn) : base(mgm, apiConn) { }


        override public async Task<List<Management>> Run()
        {
            List<Management> discoveredDevices = [];
            // if (superManagement == null)
            //     return null!;
            // }

            Log.WriteAudit(Autodiscovery, $"starting discovery for {SuperManagement.Name} (id={SuperManagement.Id})");

            if (SuperManagement.DeviceType.Name == "Check Point")
            {
                Log.WriteDebug(Autodiscovery, $"discovering CP domains & gateways");
                if (!await DiscoverySession(discoveredDevices))
                {
                    return [];
                }
            }
            return await GetDeltas(discoveredDevices);
        }

        private async Task<bool> DiscoverySession(List<Management> discoveredDevices)
        {
            CheckPointClient restClientCP = new(SuperManagement);
            string domainString = SuperManagement.ConfigPath ?? "";
            if (SuperManagement.DomainUid != null && SuperManagement.DomainUid != "")
            {
                domainString = SuperManagement.DomainUid;
            }
            RestResponse<CpSessionAuthInfo> sessionResponse = await restClientCP.AuthenticateUser(SuperManagement.ImportCredential.ImportUser, SuperManagement.ImportCredential.Secret, domainString);
            if (sessionResponse.StatusCode == HttpStatusCode.OK && sessionResponse.IsSuccessful && sessionResponse.Data?.SessionId != null && sessionResponse.Data?.SessionId != "")
            {
                if (sessionResponse?.Data?.SessionId == null || sessionResponse.Data.SessionId == "")
                {
                    Log.WriteWarning(Autodiscovery, $"Did not receive a correct session ID when trying to login to manager {SuperManagement.Name} (id={SuperManagement.Id})");
                    return false;
                }
                string sessionId = sessionResponse.Data.SessionId;
                Log.WriteDebug(Autodiscovery, $"successful CP Manager login, got SessionID: {sessionId}");
                // need to use @ verbatim identifier for special chars in sessionId

                await CollectDevices(sessionId, restClientCP, discoveredDevices);

                sessionResponse = await restClientCP.DeAuthenticateUser(@sessionId);
                if (sessionResponse.StatusCode == HttpStatusCode.OK)
                {
                    Log.WriteDebug(Autodiscovery, $"successful CP Manager logout");
                }
                else
                {
                    Log.WriteWarning(Autodiscovery, $"error while logging out from CP Manager: {sessionResponse.ErrorMessage}");
                }
            }
            else
            {
                string errorTxt = $"error while logging in to CP Manager: {sessionResponse.ErrorMessage} ";
                if (sessionResponse?.Data?.SessionId == "")
                {
                    errorTxt += "could not authenticate to CP manager - got empty session ID";
                }
                Log.WriteWarning(Autodiscovery, errorTxt);
                throw new AuthenticationException(errorTxt);
            }
            return true;
        }

        private async Task CollectDevices(string sessionId, CheckPointClient restClientCP, List<Management> discoveredDevices)
        {
            RestResponse<CpDomainHelper> domainResponse = await restClientCP.GetDomains(@sessionId);
            if (domainResponse.StatusCode == HttpStatusCode.OK && domainResponse.IsSuccessful && domainResponse.Data?.DomainList != null)
            {
                List<Domain> domainList = domainResponse.Data.DomainList;
                string ManagementType;
                if (domainList.Count == 0)
                {
                    Log.WriteDebug(Autodiscovery, $"found no domains - assuming this is a standard management, adding dummy domain with empty name");
                    domainList.Add(new Domain { Name = "" });
                    ManagementType = "stand-alone";
                }
                else
                {
                    ManagementType = "MDS";
                }

                foreach (Domain domain in domainList)
                {
                    Log.WriteDebug(Autodiscovery, $"found domain '{domain.Name}'");
                    discoveredDevices.Add(await CreateManagementFromDomain(domain, ManagementType, restClientCP));
                }
                Log.WriteDebug(Autodiscovery, $"found a total of {domainList.Count} domains");
            }
            else
            {
                Log.WriteWarning(Autodiscovery, $"error while getting domain list: {domainResponse.ErrorMessage}");
            }
        }

        private async Task<Management> CreateManagementFromDomain(Domain domain, string ManagementType, CheckPointClient restClientCP)
        {
            Management currentManagement = new()
            {
                Name = SuperManagement.Name + "__" + domain.Name,
                ImporterHostname = SuperManagement.ImporterHostname,
                Hostname = SuperManagement.Hostname,
                ImportCredential = SuperManagement.ImportCredential,
                Port = SuperManagement.Port,
                ImportDisabled = false,
                ForceInitialImport = true,
                HideInUi = false,
                ConfigPath = domain.Name,
                DomainUid = domain.Uid,
                DebugLevel = SuperManagement.DebugLevel,
                SuperManagerId = SuperManagement.Id,
                DeviceType = new DeviceType { Id = 9 },
                Devices = []
            };
            // if super manager is just a simple management
            if (domain.Name == "") // set some settings identical to "superManager", so that no new manager is created
            {
                currentManagement.Name = SuperManagement.Name;
                currentManagement.ConfigPath = "";
                currentManagement.SuperManagerId = null;
                currentManagement.DomainUid = "";
            }

            // session id pins this session to a specific domain (if domain is given during login)
            RestResponse<CpSessionAuthInfo> sessionResponsePerDomain =
                await restClientCP.AuthenticateUser(currentManagement.ImportCredential.ImportUser, currentManagement.ImportCredential.Secret, currentManagement.DomainUid);

            if (sessionResponsePerDomain.StatusCode == HttpStatusCode.OK &&
                sessionResponsePerDomain.IsSuccessful &&
                sessionResponsePerDomain.Data?.SessionId != null &&
                sessionResponsePerDomain.Data?.SessionId != "")
            {
                string sessionIdPerDomain = sessionResponsePerDomain.Data!.SessionId;
                Log.WriteDebug(Autodiscovery, $"successful CP manager login, domain: {domain.Name}/{domain.Uid}, got SessionID: {sessionIdPerDomain}");

                // now fetching per gateway information (including package and layer names)
                List<CpDevice> devList = await restClientCP.GetGateways(@sessionIdPerDomain, ManagementType);

                // add devices to currentManagement
                foreach (CpDevice cpDev in devList)
                {
                    if (cpDev.Package.CpAccessLayers.Count < 1)
                    {
                        continue;
                    }

                    if (cpDev.CpDevType != "checkpoint-host")   // leave out the management host?!
                    {
                        Device dev = new()
                        {
                            Name = cpDev.Name,
                            LocalRulebase = cpDev.LocalLayerName,
                            GlobalRulebase = cpDev.GlobalLayerName,
                            Package = cpDev.Package.Name,
                            DeviceType = new DeviceType { Id = 9 } // CheckPoint GW
                        };
                        currentManagement.Devices = currentManagement.Devices.Append(dev).ToArray();
                    }

                    List<Domain> domainList = await restClientCP.GetDomains(@sessionId);
                    discoveredDevices = await DiscoverDomainDevices(domainList, restClientCP, ManagementType);
                    await LogoutCp(restClientCP, @sessionId);
                }
                await restClientCP.DeAuthenticateUser(@sessionIdPerDomain);
            }
            else
            {
                Log.WriteWarning(Autodiscovery,
                    $"CP manager: could not login to manager {currentManagement.Name}, domain: {domain.Name}, got SessionID: {sessionResponsePerDomain.Data?.SessionId}");
            }
            return currentManagement;
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

        private async Task<string> UpdateMgmUids(Management mgm, CheckPointClient restClientCP, string sessionId)
        {
            string mgmType = "";
            if (mgm.DeviceType.Id == 13)    // MDS
            {
                mgm.Uid = await restClientCP.GetMdsUid(mgm);
                mgmType = "MDS";
            }
            else    // single management
            {
                mgm.Uid = await GetMgmUid(restClientCP, sessionId, superManagement.Hostname);
                mgmType = "Management";
            }

            var vars = new { id = mgm.Id, uid = mgm.Uid, domainUid = mgm.DomainUid };
            _ = (await apiConnection.SendQueryAsync<ReturnId>(DeviceQueries.updateManagementUids, vars)).UpdatedId;
 
            return mgmType;
        }

        private async Task<(string, CheckPointClient)> LoginCp(Management mgm)
        {
            CheckPointClient restClientCP = new(mgm, baseUrl: $"https://{mgm.Hostname}:{mgm.Port}/web_api");
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
                    Log.WriteWarning(GlobalConst.kAutodiscovery, $"Did not receive a correct session ID when trying to login to manager {superManagement.Name} (id={superManagement.Id})");
                }
                else
                {
                    sessionId = sessionResponse.Data.SessionId;
                    Log.WriteDebug(GlobalConst.kAutodiscovery, $"successful CP Manager login, got SessionID: {sessionId}");
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
                Log.WriteDebug(GlobalConst.kAutodiscovery, $"successful CP Manager logout");
                return true;
            }
            else
            {
                Log.WriteWarning(GlobalConst.kAutodiscovery, $"error while logging out from CP Manager: {sessionResponse.ErrorMessage}");
                return false;
            }
        }

        private async Task<Device[]> GetGateways(CheckPointClient restClientCP, string sessionIdPerDomain, string ManagementType)
        {
            List<CpDevice> devList = await restClientCP.GetGateways(@sessionIdPerDomain);
            List<Device> devices = [];

            // add devices to currentManagement
            foreach (CpDevice cpDev in devList)
            {
                if (cpDev.CpDevType != "checkpoint-host")   // leave out the management host
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

        protected static async Task<string> GetMgmUid(CheckPointClient restClientCP, string sessionIdPerDomain, string mgmHostname)
        {
            List<CpDevice> devList = await restClientCP.GetGateways(sessionIdPerDomain);

            string mgmIp = await IpOperations.DnsLookUp(mgmHostname);

            if (string.IsNullOrEmpty(mgmIp))
            {
                Log.WriteWarning("Autodiscovery", $"Could not resolve management host {mgmHostname} - using hostname instead");
                mgmIp = mgmHostname;
            }

            // Try to find a matching device by IP
            foreach (CpDevice cpDev in devList)
            {
                if (cpDev.CpDevType == "checkpoint-host" && cpDev.ManagementIp == mgmIp)
                {
                    return cpDev.Uid;
                }
            }

            // Fallback: return UID of the first checkpoint-host device
            var fallbackDevice = devList.FirstOrDefault(d => d.CpDevType == "checkpoint-host");
            if (fallbackDevice != null)
            {
                Log.WriteWarning("Autodiscovery", $"No exact IP match for {mgmHostname}, falling back to first found checkpoint-host: {fallbackDevice.Name}");
                return fallbackDevice.Uid;
            }

            Log.WriteDebug("Autodiscovery", $"Did not find any checkpoint-host devices - could not set UID");
            return "";
        }

        protected static async Task<List<CpDevice>> GetManagers(CheckPointClient restClientCP, string sessionId)
        {
            List<CpDevice> devList = await restClientCP.GetAllCpDevices(sessionId);
            List<CpDevice> mgmList = devList.Where(d => d.CpDevType == "checkpoint-host").ToList();
            return mgmList;
        }

        protected static async Task<string> GetGlobalDomainUid(CheckPointClient restClientCP, string sessionIdPerDomain)
        {
            return await restClientCP.GetGlobalDomainUid(sessionIdPerDomain);
        }
    }

}
