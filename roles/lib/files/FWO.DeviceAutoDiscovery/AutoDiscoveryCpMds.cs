using FWO.Api.Data;
using FWO.Api.Client;
using FWO.Logging;
using FWO.Rest.Client;
using System.Net;
using RestSharp;

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
                List<Management> discoveredDevices = new List<Management>();
                string ManagementType = "";
                Log.WriteAudit("Autodiscovery", $"starting discovery for {superManagement.Name} (id={superManagement.Id})");

                if (superManagement.DeviceType.Name == "Check Point")
                {
                    Log.WriteDebug("Autodiscovery", $"discovering CP domains & gateways");
                    CheckPointClient restClientCP = new CheckPointClient(superManagement);

                    RestResponse<CpSessionAuthInfo> sessionResponse = await restClientCP.AuthenticateUser(superManagement.ImportUser, superManagement.Secret, superManagement.ConfigPath);
                    if (sessionResponse.StatusCode == HttpStatusCode.OK && sessionResponse.IsSuccessful && sessionResponse.Data?.SessionId != null && sessionResponse.Data?.SessionId != "")
                    {
                        // if (sessionResponse==null || sessionResponse.Data==null || sessionResponse.Data.SessionId==null || sessionResponse.Data.SessionId=="")
                        if (sessionResponse?.Data?.SessionId == null || sessionResponse.Data.SessionId == "")
                        {
                            Log.WriteWarning("Autodiscovery", $"Did not receive a correct session ID when trying to login to manager {superManagement.Name} (id={superManagement.Id})");
                            return new List<Management>() { };
                        }
                        string sessionId = sessionResponse.Data.SessionId;
                        Log.WriteDebug("Autodiscovery", $"successful CP Manager login, got SessionID: {sessionId}");
                        // need to use @ verbatim identifier for special chars in sessionId
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

                                Management currentManagement = new Management
                                {
                                    Name = superManagement.Name + "__" + domain.Name,
                                    ImporterHostname = superManagement.ImporterHostname,
                                    Hostname = superManagement.Hostname,
                                    ImportUser = superManagement.ImportUser,
                                    Secret = superManagement.Secret,
                                    Port = superManagement.Port,
                                    ImportDisabled = false,
                                    ForceInitialImport = true,
                                    HideInUi = false,
                                    ConfigPath = domain.Name,
                                    DebugLevel = superManagement.DebugLevel,
                                    SuperManagerId = superManagement.Id,
                                    DeviceType = new DeviceType { Id = 9 },
                                    Devices = new Device[] { }
                                };
                                // if super manager is just a simple management
                                if (domain.Name == "") // set some settings identical to "superManager", so that no new manager is created
                                {
                                    currentManagement.Name = superManagement.Name;
                                    currentManagement.ConfigPath = "";
                                    currentManagement.SuperManagerId = null;
                                }

                                // session id pins this session to a specific domain (if domain is given during login) 
                                RestResponse<CpSessionAuthInfo> sessionResponsePerDomain =
                                    await restClientCP.AuthenticateUser(superManagement.ImportUser, superManagement.Secret, domain.Name);

                                if (sessionResponsePerDomain.StatusCode == HttpStatusCode.OK &&
                                    sessionResponsePerDomain.IsSuccessful &&
                                    sessionResponsePerDomain.Data?.SessionId != null &&
                                    sessionResponsePerDomain.Data?.SessionId != "")
                                {
                                    string sessionIdPerDomain = sessionResponsePerDomain.Data!.SessionId;
                                    Log.WriteDebug("Autodiscovery", $"successful CP manager login, domain: {domain.Name}, got SessionID: {sessionIdPerDomain}");

                                    // now fetching per gateway information (including package and layer names)
                                    List<CpDevice> devList = await restClientCP.GetGateways(@sessionIdPerDomain, ManagementType);

                                    // add devices to currentManagement
                                    foreach (CpDevice cpDev in devList)
                                    {
                                        if (cpDev.Package.CpAccessLayers.Count < 1)
                                        {
                                            // Log.WriteWarning("AutoDiscovery", $"did not find any layers");
                                            continue;
                                        }

                                        if (cpDev.CpDevType != "checkpoint-host")   // leave out the management host?!
                                        {
                                            Device dev = new Device
                                            {
                                                Name = cpDev.Name,
                                                LocalRulebase = cpDev.LocalLayerName,
                                                GlobalRulebase = cpDev.GlobalLayerName,
                                                Package = cpDev.Package.Name,
                                                DeviceType = new DeviceType { Id = 9 } // CheckPoint GW
                                            };
                                            currentManagement.Devices = currentManagement.Devices.Append(dev).ToArray();
                                        }
                                        sessionResponsePerDomain = await restClientCP.DeAuthenticateUser(@sessionIdPerDomain);
                                    }
                                }
                                else
                                {
                                    Log.WriteWarning("Autodiscovery", 
                                        $"CP manager: could not login to manager {currentManagement.Name}, domain: {domain.Name}, got SessionID: {sessionResponsePerDomain.Data?.SessionId}");
                                }
                                discoveredDevices.Add(currentManagement);
                            }
                            Log.WriteDebug("Autodiscovery", $"found a total of {domainList.Count} domains");
                        }
                        else
                            Log.WriteWarning("AutoDiscovery", $"error while getting domain list: {domainResponse.ErrorMessage}");

                        sessionResponse = await restClientCP.DeAuthenticateUser(@sessionId);
                        if (sessionResponse.StatusCode == HttpStatusCode.OK)
                            Log.WriteDebug("Autodiscovery", $"successful CP Manager logout");
                        else
                            Log.WriteWarning("Autodiscovery", $"error while logging out from CP Manager: {sessionResponse.ErrorMessage}");
                    }
                    else
                    {
                        string errorTxt = $"error while logging in to CP Manager: {sessionResponse.ErrorMessage} ";
                        if (sessionResponse?.Data?.SessionId == "")
                            errorTxt += "could not authenticate to CP manager - got empty session ID";
                        Log.WriteWarning("AutoDiscovery", errorTxt);
                        throw new Exception(errorTxt);
                    }
                }
                return await GetDeltas(discoveredDevices);
            }
        }
    }
}
