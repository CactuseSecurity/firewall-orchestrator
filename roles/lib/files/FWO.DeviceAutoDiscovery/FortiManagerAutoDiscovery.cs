using FWO.Api.Data;
using FWO.ApiClient;
using FWO.Logging;
using RestSharp;
using RestSharp.Authenticators;
using RestSharp.Serializers.SystemTextJson;
using System.Text.Json;
using FWO.Rest.Client;
using System.Net;

namespace FWO.DeviceAutoDiscovery
{

    public static class AutoDiscovery
    {
        public static async void Run(Management manager)
        {
            Log.WriteAudit("Autodiscovery", $"starting discovery for {manager.Name} (id={manager.Id})");
            if (manager.DeviceType.Name == "FortiManager")
            {
                List<Adom> customAdoms = new List<Adom>();
                List<string> predefinedAdoms = // TODO: move this to config file
                    new List<string> {"FortiAnalyzer", "FortiAuthenticator", "FortiCache", "FortiCarrier", "FortiClient",
                        "FortiDDoS", "FortiDeceptor", "FortiFirewall", "FortiMail", "FortiManager", "FortiProxy",
                        "FortiSandbox", "FortiWeb", "Syslog", "Unmanaged_Devices", "others", "rootp"};
                Log.WriteDebug("Autodiscovery", $"discovering FortiManager adoms, vdoms, devices");
                FortiManagerClient restClientFM = new FortiManagerClient(manager);

                IRestResponse<SessionAuthInfo> sessionResponse = await restClientFM.AuthenticateUser(manager.ImportUser, manager.PrivateKey);
                string sessionId = "";
                if (sessionResponse.StatusCode == HttpStatusCode.OK)
                    sessionId = sessionResponse.Data.SessionId;
                Log.WriteDebug("Autodiscovery", $"generated well-formed SessionID: {sessionId}");
                if (sessionResponse.StatusCode == HttpStatusCode.OK && sessionResponse.IsSuccessful)
                {
                    Log.WriteDebug("Autodiscovery", $"successful FortiManager login");
                    // need to use @ verbatim identifier for special chars in sessionId
                    IRestResponse<FmApiTopLevelHelper> adomResponse = await restClientFM.GetAdoms(@sessionId);
                    if (adomResponse.StatusCode == HttpStatusCode.OK && adomResponse.IsSuccessful)
                    {
                        List<Adom> adomList = adomResponse.Data.Result[0].AdomList;
                        foreach (Adom adom in adomList)
                            if (!predefinedAdoms.Contains(adom.Name))
                            {
                                Log.WriteDebug("Autodiscovery", $"found adom {adom.Name}");
                                customAdoms.Add(adom);
                            }
                    }
                    else
                        Log.WriteWarning("AutoDiscovery", $"error while getting ADOM list: {adomResponse.ErrorMessage}");

                    IRestResponse<FmApiTopLevelHelperDev> deviceResponse = await restClientFM.GetDevices(@sessionId);
                    if (deviceResponse.StatusCode == HttpStatusCode.OK && deviceResponse.IsSuccessful)
                    {
                        List<FortiGate> fortigateList = deviceResponse.Data.Result[0].DeviceList;
                        foreach (FortiGate fg in fortigateList)
                            Log.WriteDebug("Autodiscovery", $"found FortiGate {fg.Name} belonging to management VDOM {fg.MgtVdom}");

                        customAdoms.Add(new Adom {Name = "global"}); // adding global adom
                        foreach (Adom adom in customAdoms)
                        {
                            IRestResponse<FmApiTopLevelHelperPac> packageResponse = await restClientFM.GetPackages(@sessionId, adom.Name);
                            if (packageResponse.StatusCode == HttpStatusCode.OK && packageResponse.IsSuccessful)
                            {
                                List<Package> packageList = packageResponse.Data.Result[0].PackageList;
                                foreach (Package pac in packageList)
                                {
                                    Log.WriteDebug("Autodiscovery", $"found Package {pac.Name} in ADOM {adom.Name}");
                                    adom.Packages.Add(pac);
                                }
                            }
                        }
                    }
                    else
                        Log.WriteWarning("AutoDiscovery", $"error while getting device/fortigate list: {deviceResponse.ErrorMessage}");

                    sessionResponse = await restClientFM.DeAuthenticateUser(sessionId);
                    if (sessionResponse.StatusCode == HttpStatusCode.OK)
                        Log.WriteDebug("Autodiscovery", $"successful FortiManager logout");
                    else
                        Log.WriteWarning("Autodiscovery", $"error while logging out from FortiManager: {sessionResponse.ErrorMessage}");
                }
                else
                    Log.WriteWarning("AutoDiscovery", $"error while logging in to FortiManager: {sessionResponse.ErrorMessage}");
            }
            else if (manager.DeviceType.Name == "Check Point")
                Log.WriteWarning("Autodiscovery", $"Auto discovery for Check Point MDS not implemented yet");
        }
    }
}
