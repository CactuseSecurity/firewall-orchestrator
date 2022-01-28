using System.Net;
using RestSharp;
using FWO.Api.Data;
using FWO.Logging;
using FWO.Rest.Client;

namespace FWO.DeviceAutoDiscovery
{
    public class AutoDiscoveryFortiManager : AutoDiscoveryBase
    {
        public AutoDiscoveryFortiManager(Management superManagement) : base(superManagement) { }
        public override async Task<List<Management>> Run()
        {
            bool fullAuditAutodiscovery = false;
            List<Management> discoveredDevices = new List<Management>();
            Log.WriteAudit("Autodiscovery", $"starting discovery for {superManagement.Name} (id={superManagement.Id})");
            if (superManagement.DeviceType.Name == "FortiManager")
            {
                List<Adom> customAdoms = new List<Adom>();
                List<string> predefinedAdoms = // TODO: move this to config file
                    new List<string> {"FortiAnalyzer", "FortiAuthenticator", "FortiCache", "FortiCarrier", "FortiClient",
                        "FortiDDoS", "FortiDeceptor", "FortiFirewall", "FortiMail", "FortiManager", "FortiProxy",
                        "FortiSandbox", "FortiWeb", "Syslog", "Unmanaged_Devices", "others", "rootp"};
                if (fullAuditAutodiscovery)
                    Log.WriteAudit("Autodiscovery", $"discovering FortiManager adoms, vdoms, devices");
                else
                    Log.WriteDebug("Autodiscovery", $"discovering FortiManager adoms, vdoms, devices");
                FortiManagerClient restClientFM = new FortiManagerClient(superManagement);

                IRestResponse<SessionAuthInfo> sessionResponse = await restClientFM.AuthenticateUser(superManagement.ImportUser, superManagement.PrivateKey);
                if (sessionResponse.StatusCode == HttpStatusCode.OK && sessionResponse.IsSuccessful)
                {
                    string sessionId = sessionResponse.Data.SessionId;
                    if (fullAuditAutodiscovery)
                        Log.WriteAudit("Autodiscovery", $"successful FortiManager login, got SessionID: {sessionId}");
                    else
                        Log.WriteDebug("Autodiscovery", $"successful FortiManager login, got SessionID: {sessionId}");
                    // need to use @ verbatim identifier for special chars in sessionId
                    IRestResponse<FmApiTopLevelHelper> adomResponse = await restClientFM.GetAdoms(@sessionId);
                    if (adomResponse.StatusCode == HttpStatusCode.OK && adomResponse.IsSuccessful)
                    {
                        List<Adom> adomList = adomResponse.Data.Result[0].AdomList;
                        foreach (Adom adom in adomList)
                            if (!predefinedAdoms.Contains(adom.Name))
                            {
                                if (fullAuditAutodiscovery)
                                    Log.WriteAudit("Autodiscovery", $"found adom {adom.Name}");
                                else
                                    Log.WriteDebug("Autodiscovery", $"found adom {adom.Name}");
                                customAdoms.Add(adom);
                            }
                        customAdoms.Add(new Adom { Name = "global" }); // adding global adom
                    }
                    else
                        Log.WriteWarning("AutoDiscovery", $"error while getting ADOM list: {adomResponse.ErrorMessage}");

                    IRestResponse<FmApiTopLevelHelperDev> deviceResponse = await restClientFM.GetDevices(@sessionId);
                    if (deviceResponse.StatusCode == HttpStatusCode.OK && deviceResponse.IsSuccessful)
                    {
                        List<FortiGate> fortigateList = deviceResponse.Data.Result[0].DeviceList;
                        foreach (FortiGate fg in fortigateList)
                        {
                            if (fullAuditAutodiscovery)
                                Log.WriteAudit("Autodiscovery", $"found device {fg.Name} belonging to management VDOM {fg.MgtVdom}");
                            else
                                Log.WriteDebug("Autodiscovery", $"found device {fg.Name} belonging to management VDOM {fg.MgtVdom}");
                            foreach (Vdom vdom in fg.VdomList)
                            {
                                if (fullAuditAutodiscovery)
                                    Log.WriteAudit("Autodiscovery", $"found vdom {vdom.Name} belonging to device {fg.Name}");
                                else
                                    Log.WriteDebug("Autodiscovery", $"found vdom {vdom.Name} belonging to device {fg.Name}");
                            }
                        }
                        foreach (Adom adom in customAdoms)
                        {
                            // create object from discovered adom
                            Management currentManagement = new Management
                            {
                                Name = superManagement.Name + "__" + adom.Name,
                                ImporterHostname = superManagement.ImporterHostname,
                                Hostname = superManagement.Hostname,
                                ImportUser = superManagement.ImportUser,
                                PrivateKey = superManagement.PrivateKey,
                                Password = superManagement.Password,
                                Port = superManagement.Port,
                                ImportDisabled = false,
                                ForceInitialImport = true,
                                HideInUi = false,
                                ConfigPath = adom.Name,
                                DebugLevel = superManagement.DebugLevel,
                                SuperManagerId = superManagement.Id,
                                DeviceType = new DeviceType { Id = 11 },
                                Devices = new Device[] { }
                            };

                            IRestResponse<FmApiTopLevelHelperAssign> assignResponse = await restClientFM.GetPackageAssignmentsPerAdom(@sessionId, adom.Name);
                            if (assignResponse.StatusCode == HttpStatusCode.OK && assignResponse.IsSuccessful)
                            {
                                List<Assignment> assignmentList = assignResponse.Data.Result[0].AssignmentList;
                                foreach (Assignment assign in assignmentList)
                                {
                                    Device devFound = new Device();
                                    // assign.PackageName = assign.PackageName.Replace("/", "\\/");    // replace / in package name with \/
                                    if (fullAuditAutodiscovery)
                                        Log.WriteAudit("Autodiscovery", $"found assignment1 in ADOM {adom.Name}: package {assign.PackageName} assigned to device {assign.DeviceName}, vdom: {assign.VdomName} ");
                                    else
                                        Log.WriteDebug("Autodiscovery", $"found assignment1 in ADOM {adom.Name}: package {assign.PackageName} assigned to device {assign.DeviceName}, vdom: {assign.VdomName} ");
                                    if (assign.DeviceName != null)
                                    {
                                        if (fullAuditAutodiscovery)
                                            Log.WriteAudit("Autodiscovery", $"found assignment2 (device<>null) in ADOM {adom.Name}: package {assign.PackageName} assigned to device {assign.DeviceName}, vdom: {assign.VdomName} ");
                                        else
                                            Log.WriteDebug("Autodiscovery", $"found assignment2 (device<>null) in ADOM {adom.Name}: package {assign.PackageName} assigned to device {assign.DeviceName}, vdom: {assign.VdomName} ");
                                        if (assign.DeviceName != "")
                                        {
                                            if (fullAuditAutodiscovery)
                                                Log.WriteAudit("Autodiscovery", $"found assignment3 (non-device-empty-string) in ADOM {adom.Name}: package {assign.PackageName} assigned to device {assign.DeviceName}, vdom: {assign.VdomName} ");
                                            else
                                                Log.WriteDebug("Autodiscovery", $"found assignment3 (non-device-empty-string) in ADOM {adom.Name}: package {assign.PackageName} assigned to device {assign.DeviceName}, vdom: {assign.VdomName} ");
                                            string devName = assign.DeviceName;
                                            if (assign.VdomName != null && assign.VdomName != "")
                                                devName += "_" + assign.VdomName;
                                            devFound = new Device
                                            {
                                                Name = devName,
                                                LocalRulebase = assign.PackageName,
                                                Package = assign.PackageName,
                                                DeviceType = new DeviceType { Id = 10 } // fortiGate
                                            };
                                            // handle global vs. local based on VdomName?
                                            if (fullAuditAutodiscovery)
                                            {
                                                Log.WriteAudit("Autodiscovery", $"assignment devFound Name = {devFound.Name}");
                                                Log.WriteAudit("Autodiscovery", $"assignment currentManagement before Append contains {currentManagement.Devices.Length} devices");
                                            }
                                            else
                                            {
                                                Log.WriteDebug("Autodiscovery", $"assignment devFound Name = {devFound.Name}");
                                                Log.WriteDebug("Autodiscovery", $"assignment currentManagement before Append contains {currentManagement.Devices.Length} devices");
                                            }
                                            currentManagement.Devices = currentManagement.Devices.Append(devFound).ToArray();
                                            if (fullAuditAutodiscovery)
                                                Log.WriteAudit("Autodiscovery", $"assignment currentManagement after Append contains {currentManagement.Devices.Length} devices");
                                            else
                                                Log.WriteDebug("Autodiscovery", $"assignment currentManagement after Append contains {currentManagement.Devices.Length} devices");
                                        }
                                    }
                                    adom.Assignments.Add(assign);
                                }
                            }
                            discoveredDevices.Add(currentManagement); // add discovered adom including devices
                        }
                    }
                    else
                        Log.WriteWarning("AutoDiscovery", $"error while getting device/fortigate list: {deviceResponse.ErrorMessage}");

                    sessionResponse = await restClientFM.DeAuthenticateUser(sessionId);
                    if (sessionResponse.StatusCode == HttpStatusCode.OK)
                        if (fullAuditAutodiscovery)
                            Log.WriteAudit("Autodiscovery", $"successful FortiManager logout");
                        else
                            Log.WriteDebug("Autodiscovery", $"successful FortiManager logout");
                    else
                        Log.WriteWarning("Autodiscovery", $"error while logging out from FortiManager: {sessionResponse.ErrorMessage}");
                }
                else
                    Log.WriteWarning("AutoDiscovery", $"error while logging in to FortiManager: {sessionResponse.ErrorMessage}");
            }
            return discoveredDevices;
        }
    }
}
