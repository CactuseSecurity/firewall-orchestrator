using FWO.Api.Data;
using FWO.ApiClient;
using FWO.Logging;
using FWO.Rest.Client;
using System.Net;
using RestSharp;

namespace FWO.DeviceAutoDiscovery
{
    public class AutoDiscoveryCpMds : AutoDiscoveryBase
    {
        public AutoDiscoveryCpMds(Management mgm, APIConnection apiConn) : base(mgm, apiConn) { }

        private static bool containsDomainLayer(List<CpAccessLayer> layers)
        {
            foreach (CpAccessLayer layer in layers)
            {
                if (layer.ParentLayer != "")
                    return true;
            }
            return false;
        }

        public override async Task<List<Management>> Run()
        {
            List<Management> discoveredDevices = new List<Management>();
            string ManagementType = "";
            Log.WriteAudit("Autodiscovery", $"starting discovery for {superManagement.Name} (id={superManagement.Id})");

            if (superManagement.DeviceType.Name == "Check Point")
            {
                // List<Domain> customDomains = new List<Domain>();
                Log.WriteDebug("Autodiscovery", $"discovering CP domains & gateways");

                CheckPointClient restClientCP = new CheckPointClient(superManagement);

                RestResponse<CpSessionAuthInfo> sessionResponse = await restClientCP.AuthenticateUser(superManagement.ImportUser, superManagement.Password, superManagement.ConfigPath);
                if (sessionResponse.StatusCode == HttpStatusCode.OK && sessionResponse.IsSuccessful && sessionResponse?.Data?.SessionId != "")
                {
                    // need to serialize here
                    string? sessionId = sessionResponse?.Data?.SessionId;
                    Log.WriteDebug("Autodiscovery", $"successful CP Manager login, got SessionID: {sessionId}");
                    // need to use @ verbatim identifier for special chars in sessionId
                    RestResponse<CpDomainHelper> domainResponse = await restClientCP.GetDomains(@sessionId);
                    if (domainResponse.StatusCode == HttpStatusCode.OK && domainResponse.IsSuccessful)
                    {
                        List<Domain> domainList = domainResponse?.Data?.DomainList;
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
                                PrivateKey = superManagement.PrivateKey,
                                Password = superManagement.Password,
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
                            Log.WriteDebug("Autodiscovery", $"DeviceTypeId of freshly created management={currentManagement.DeviceType.Id}");
                            // if super manager is just a simple management
                            if (domain.Name == "") // set some settings identical to "superManager", so that no new manager is created
                            {
                                currentManagement.Name = superManagement.Name;
                                currentManagement.ConfigPath = "";
                                currentManagement.SuperManagerId = null;
                            }

                            RestResponse<CpSessionAuthInfo> sessionResponsePerDomain =
                                await restClientCP.AuthenticateUser(superManagement.ImportUser, superManagement.Password, domain.Name);

                            if (sessionResponsePerDomain.StatusCode == HttpStatusCode.OK &&
                                sessionResponsePerDomain.IsSuccessful &&
                                sessionResponsePerDomain?.Data?.SessionId != "")
                            {
                                string? sessionIdPerDomain = sessionResponsePerDomain?.Data?.SessionId;
                                Log.WriteDebug("Autodiscovery", $"successful CP manager login, got SessionID: {sessionId}");
                                List<CpDevice> devList = await restClientCP.GetGateways(@sessionIdPerDomain);

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
                                        string globalLayerName = "";
                                        string localLayerName = "";
                                        bool packageContainsDomainLayer = containsDomainLayer(cpDev.Package.CpAccessLayers);
                                        foreach (CpAccessLayer layer in cpDev.Package.CpAccessLayers)
                                        {
                                            if (layer.Type != "access-layer") // only dealing with access layers, ignore the rest
                                                continue;

                                            if (layer.ParentLayer != "")      // this is a domain layer
                                            {
                                                localLayerName = layer.Name;
                                                layer.LayerType = "domain-layer";
                                                Log.WriteDebug("Autodiscovery", $"found domain layer with link to parent layer '{layer.ParentLayer}'");
                                            }
                                            else if (ManagementType == "stand-alone")
                                            {
                                                localLayerName = layer.Name;
                                                layer.LayerType = "local-layer";
                                                Log.WriteDebug("Autodiscovery", $"found stand-alone layer '{layer.Name}'");
                                            }
                                            else if (packageContainsDomainLayer)
                                            {   // this must the be global layer
                                                layer.LayerType = "global-layer";
                                                globalLayerName = layer.Name;
                                                Log.WriteDebug("Autodiscovery", $"found global layer '{layer.Name}'");
                                            }
                                            else
                                            { // in domain context, but no global layer exists
                                                layer.LayerType = "stand-alone-layer";
                                                Log.WriteDebug("Autodiscovery", $"found stand-alone layer in domain context '{layer.Name}'");
                                            }
                                            // TODO: this will contstantly overwrite local layer name if more than one exists, the last one wins!
                                        }
                                        Device dev = new Device
                                        {
                                            Name = cpDev.Name,
                                            LocalRulebase = localLayerName,
                                            GlobalRulebase = globalLayerName,
                                            Package = cpDev.Package.Name,
                                            DeviceType = new DeviceType { Id = 9 } // CheckPoint GW
                                        };
                                        currentManagement.Devices = currentManagement.Devices.Append(dev).ToArray();
                                    }
                                    sessionResponsePerDomain = await restClientCP.DeAuthenticateUser(@sessionIdPerDomain);
                                }
                            }
                            discoveredDevices.Add(currentManagement);
                            Log.WriteDebug("Autodiscovery", $"DeviceTypeId of discovered management={currentManagement.DeviceType.Id}");
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
