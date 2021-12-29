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
                // string sessionId = "+"; // workaround - make sure sessionId contains no "+" - special chars cause failure
                // if (sessionResponse.StatusCode == HttpStatusCode.OK)
                //     sessionId = sessionResponse.Data.SessionId;
                // while (sessionId.Contains("+") || sessionId=="") {
                //     sessionResponse = await restClientFM.AuthenticateUser(manager.ImportUser, manager.PrivateKey);
                //     if (sessionResponse.StatusCode == HttpStatusCode.OK)
                //         sessionId = sessionResponse.Data.SessionId;
                //     else 
                //     {
                //         Log.WriteWarning("Autodiscovery", $"problems during FortiManager login");
                //         break;
                //     }
                //     Log.WriteDebug("Autodiscovery", $"generated SessionID: {sessionId}");
                //     Thread.Sleep(5000);
                // }
                Log.WriteDebug("Autodiscovery", $"generated well-formed SessionID: {sessionId}");
                if (sessionResponse.StatusCode == HttpStatusCode.OK)
                {
                    Log.WriteDebug("Autodiscovery", $"successful FortiManager login");
                    // need to use @ verbatim identifier for special chars in sessionId
                    IRestResponse<FmApiTopLevelHelper> adomResponse = await restClientFM.GetAdoms(@sessionId);
                    if (adomResponse.StatusCode == HttpStatusCode.OK)
                    {
                        List<Adom> adomList = adomResponse.Data.Result[0].AdomList;
                        foreach (Adom adom in adomList)
                        {
                            if (!predefinedAdoms.Contains(adom.Name))
                            {
                                Log.WriteDebug("Autodiscovery", $"found adom {adom.Name}");
                                customAdoms.Add(adom);
                            }
                        }
                    }
                    else
                        Log.WriteWarning("AutoDiscovery", $"error while getting ADOM list: {adomResponse.ErrorMessage}");

                    IRestResponse<FmApiTopLevelHelper> deviceResponse = await restClientFM.GetDevices(@sessionId);
                    if (deviceResponse.StatusCode == HttpStatusCode.OK)
                    {
                        // List<FortiGate> fortigateList = deviceResponse.Data.Result[0].DeviceList;
                        // foreach (FortiGate fg in fortigateList)
                        // {
                        //     Log.WriteDebug("Autodiscovery", $"found FortiGate {fg}");
                        // }

                    /*
                    def getDeviceDetails(sid, fm_api_url, raw_config, mgm_details, debug_level):
                        # for each adom get devices
                        for adom in raw_config["adoms"]:
                            q_get_devices_per_adom = {"params": [{"fields": ["name", "desc", "hostname", "vdom",
                                                                                "ip", "mgmt_id", "mgt_vdom", "os_type", "os_ver", "platform_str", "dev_status"]}]}
                            devs = getter.fortinet_api_call(
                                sid, fm_api_url, "/dvmdb/adom/" + adom["name"] + "/device", payload=q_get_devices_per_adom, debug=debug_level)
                            adom.update({"devices": devs})

                        # for each adom get packages
                        for adom in raw_config["adoms"]:
                            pkg_names = []
                            packages = getter.fortinet_api_call(
                                sid, fm_api_url, "/pm/pkg/adom/" + adom["name"], debug=debug_level)
                            for pkg in packages:
                                pkg_names.append(pkg['name'])
                            adom.update({"packages": packages})
                            adom.update({"package_names": pkg_names})
                        
                        global_pkg_names = []
                        global_packages = getter.fortinet_api_call(sid, fm_api_url, "/pm/pkg/global", debug=debug_level)
                        for pkg in global_packages:
                            global_pkg_names.append(pkg['name'])
                        raw_config.update({"global_packages": global_packages})
                        raw_config.update({"global_package_names": global_pkg_names})

                        devices = []
                        device_names = []
                        for device in mgm_details['devices']:
                            device_names.append(device['name'])
                            # vdoms = getter.fortinet_api_call(sid, fm_api_url, "/dvmdb/device/" + device['name'] + "/vdom", debug=debug_level)

                            devices.append(
                                {
                                    'id': device['id'],
                                    'name': device['name'],
                                    'global_rulebase': device['global_rulebase_name'],
                                    'local_rulebase': device['local_rulebase_name'],
                                    'package': device['package_name']
                                }
                            )
                        raw_config.update({"devices": devices})
                        raw_config.update({"device_names": device_names})
                    */

                    }
                    else
                        Log.WriteWarning("AutoDiscovery", $"error while getting device/fortigate list: {adomResponse.ErrorMessage}");

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
