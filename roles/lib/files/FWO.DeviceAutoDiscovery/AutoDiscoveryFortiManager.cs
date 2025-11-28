using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data;
using FWO.Logging;
using MailKit.Security;
using RestSharp;
using System.Net;


namespace FWO.DeviceAutoDiscovery
{
    public class AutoDiscoveryFortiManager : AutoDiscoveryBase
    {
        const string AdomSeparator = "_";
        const string VdomSeparator = "_";

        private readonly List<string> PredefinedAdoms = // TODO: move this to config file
            ["FortiAnalyzer", "FortiAuthenticator", "FortiCache", "FortiCarrier", "FortiFirewallCarrier", "FortiClient",
                "FortiDDoS", "FortiDeceptor", "FortiFirewall", "FortiMail", "FortiManager", "FortiNAC", "FortiProxy",
                "FortiSandbox", "FortiWeb", "Syslog", "Unmanaged_Devices", "others", "rootp"];

        private readonly string Autodiscovery = "Autodiscovery";

        public AutoDiscoveryFortiManager(Management superManagement, ApiConnection apiConn) : base(superManagement, apiConn) { }

        public override async Task<List<Management>> Run()
        {
            List<Management> discoveredDevices = [];
            Log.WriteAudit(Autodiscovery, $"starting discovery for {SuperManagement.Name} (id={SuperManagement.Id})");
            // #if DEBUG
            //      discoveredDevices = fillTestDevices();
            // #endif
            if (SuperManagement.DeviceType.Name == "FortiManager")
            {
                SuperManagement.IsSupermanager = true; // just to be sure
                Log.WriteDebug(Autodiscovery, $"discovering FortiManager adoms, vdoms, devices");
                FortiManagerClient restClientFM = new(SuperManagement);
                RestResponse<SessionAuthInfo> sessionResponse = await restClientFM.AuthenticateUser(SuperManagement.ImportCredential.ImportUser, SuperManagement.ImportCredential.Secret);
                if (sessionResponse.StatusCode == HttpStatusCode.OK && sessionResponse.IsSuccessful && !string.IsNullOrEmpty(sessionResponse.Data?.SessionId))
                {
                    return await DiscoverySession(discoveredDevices);
                }
                else
                {
                    string errorTxt = $"error while logging in to Forti Manager: {sessionResponse.ErrorMessage} ";
                    if (sessionResponse?.Data?.SessionId == "")
                        errorTxt += "could not authenticate to Forti manager - got empty session ID";
                    Log.WriteWarning(Autodiscovery, errorTxt);
                    throw new AuthenticationException(errorTxt);
                }
            }
            return discoveredDevices;
        }

        private async Task<List<Management>> DiscoverySession(List<Management> discoveredDevices)
        {
            Log.WriteDebug(Autodiscovery, $"discovering FortiManager adoms, vdoms, devices");
            FortiManagerClient restClientFM = new(SuperManagement);

            RestResponse<SessionAuthInfo> sessionResponse = await restClientFM.AuthenticateUser(SuperManagement.ImportCredential.ImportUser, SuperManagement.ImportCredential.Secret);
            if (sessionResponse.StatusCode == HttpStatusCode.OK && sessionResponse.IsSuccessful && !string.IsNullOrEmpty(sessionResponse.Data?.SessionId))
            {
                string sessionId = sessionResponse.Data.SessionId;
                Log.WriteDebug(Autodiscovery, $"successful FortiManager login, got SessionID: {sessionId}");
                // need to use @ verbatim identifier for special chars in sessionId

                discoveredDevices = await CollectDevices(sessionId, restClientFM);
                await UpdateMgmtUid(sessionId, restClientFM);
                sessionResponse = await restClientFM.DeAuthenticateUser(sessionId);
                if (sessionResponse.StatusCode == HttpStatusCode.OK)
                {
                    Log.WriteDebug(Autodiscovery, $"successful FortiManager logout");
                }
                else
                {
                    Log.WriteWarning(Autodiscovery, $"error while logging out from FortiManager: {sessionResponse.ErrorMessage}");
                }
            }
            return await GetDeltas(discoveredDevices);
        }

        private async Task UpdateMgmtUid(string sessionId, FortiManagerClient restClientFM)
        {
            // when passing sessionId, we always need to use @ verbatim identifier for special chars in sessionId
            if (string.IsNullOrEmpty(SuperManagement.Uid))  // pre v9 managements might not have a UID
            {
                // update manager UID in existing management; typically triggered in daily scheduler
                // this update happens only once when AutoDiscovery v9.0 is run for the first time
                SuperManagement.Uid = GetFortiManagerUid(restClientFM, sessionId, SuperManagement.Name);
                var vars = new { id = SuperManagement.Id, uid = SuperManagement.Uid };
                _ = (await apiConnection.SendQueryAsync<ReturnId>(DeviceQueries.updateManagementUid, vars)).UpdatedId;
                // TODO: also add UIDs in gateways?
            }
        }

        private async Task<List<Management>> CollectDevices(string sessionId, FortiManagerClient restClientFM)
        {
            List<Adom> customAdoms = await GetAdoms(sessionId, restClientFM);
            await BuildAdomDeviceVdomStructure(sessionId, customAdoms, restClientFM);
            return ConvertAdomsToManagements(customAdoms);
        }

        public async Task BuildAdomDeviceVdomStructure(string sessionId, List<Adom> customAdoms, FortiManagerClient restClientFM)
        {

            foreach (Adom adom in customAdoms)
            {
                List<FortiGate> additionalVdomDevices = [];
                RestResponse<FmApiTopLevelHelperDev> deviceResponse = await restClientFM.GetDevicesPerAdom(sessionId, adom.Name);
                if (deviceResponse != null && deviceResponse.StatusCode == HttpStatusCode.OK && deviceResponse.IsSuccessful)
                {
                    adom.DeviceList = deviceResponse.Data?.Result[0].FortiGates ?? [];
                }
            }
            // now get vdoms per device
            foreach (Adom adom in customAdoms)
            {
                List<FortiGate> additionalVdomDevices = [];
                if (adom.DeviceList != null)
                {
                    foreach (FortiGate fg in adom.DeviceList)
                    {
                        BuildAdomDeviceVdomStructurePerPhysicalDevice(fg, additionalVdomDevices);
                    }
                }
                if (additionalVdomDevices.Count > 0)
                {
                    // replace physical devices with vdoms
                    adom.DeviceList = additionalVdomDevices;
                }
            }
        }
        public void BuildAdomDeviceVdomStructurePerPhysicalDevice(FortiGate fg, List<FortiGate> additionalVdomDevices)
        {
            Log.WriteDebug(Autodiscovery, $"found device {fg.Name} belonging to management VDOM {fg.MgtVdom}");
            if (fg.VdomList != null)
            {
                // add vdom as device
                foreach (Vdom vdom in fg.VdomList)
                {
                    Log.WriteDebug(Autodiscovery, $"found vdom {vdom.Name} belonging to device {fg.Name}");
                    // add vdom as device
                    additionalVdomDevices.Add(new FortiGate
                    {
                        Name = $"{fg.Name}{AdomSeparator}{vdom.Name}",
                        Hostname = fg.Hostname,
                        MgtVdom = vdom.Name,
                        Uid = $"{fg.Name}{AdomSeparator}{vdom.Name}",
                        VdomList = []
                    });
                }
            }
        }

        private void LogDeviceResponse(RestResponse<FmApiTopLevelHelperDev> deviceResponse)
        {
            if (deviceResponse.Data != null && deviceResponse.Data.Result.Count > 0)
            {
                List<FortiGate> fortigateList = deviceResponse.Data.Result[0].FortiGates;
                foreach (FortiGate fg in fortigateList)
                {
                    Log.WriteDebug(Autodiscovery, $"found device {fg.Name} belonging to management VDOM {fg.MgtVdom}");
                    foreach (Vdom vdom in fg.VdomList)
                    {
                        Log.WriteDebug(Autodiscovery, $"found vdom {vdom.Name} belonging to device {fg.Name}");
                    }
                }
            }
        }

        private async Task<List<Adom>> GetAdoms(string sessionId, FortiManagerClient restClientFM)
        {
            List<Adom> customAdoms = [];
            RestResponse<FmApiTopLevelHelper> adomResponse = await restClientFM.GetAdoms(sessionId);
            if (adomResponse.StatusCode == HttpStatusCode.OK && adomResponse.IsSuccessful)
            {
                List<Adom>? adomList = adomResponse.Data?.Result[0]?.AdomList;
                if (adomList?.Count > 0)
                {
                    Log.WriteDebug(Autodiscovery, $"found a total of {adomList.Count} adoms");
                    foreach (Adom adom in adomList)
                    {
                        Log.WriteDebug(Autodiscovery, $"found adom {adom.Name}");
                        if (!PredefinedAdoms.Contains(adom.Name))
                        {
                            Log.WriteDebug(Autodiscovery, $"found non-predefined adom {adom.Name}");
                            customAdoms.Add(adom);
                        }
                    }
                    customAdoms.Add(new Adom { Name = "global" }); // adding global adom
                }
                else
                {
                    Log.WriteWarning(Autodiscovery, $"found no adoms at all!");
                }
            }
            else
            {
                Log.WriteWarning(Autodiscovery, $"error while getting ADOM list: {adomResponse.ErrorMessage}");
            }
            return customAdoms;
        }

        override protected Management CreateManagement(Management superManagement, string domainName, string domainUid)
        {
            // create object from discovered adom
            Management currentManagement = new()
            {
                Name = $"{superManagement.Name}{AdomSeparator}{domainName}",
                Uid = domainUid,
                ImporterHostname = superManagement.ImporterHostname,
                Hostname = superManagement.Hostname,
                ImportCredential = superManagement.ImportCredential,
                Port = superManagement.Port,
                ImportDisabled = false,
                ForceInitialImport = true,
                HideInUi = false,
                ConfigPath = domainName,
                DebugLevel = superManagement.DebugLevel,
                SuperManagerId = superManagement.Id,
                DeviceType = new DeviceType { Id = 11 },
                Devices = []
            };
            return currentManagement;
        }

        protected static string GetFortiManagerUid(FortiManagerClient restClient, string sessionIdPerDomain, string mgmName)
        {
            return mgmName; // fortiManager does not have a real UID
        }

        private List<Management> ConvertAdomsToManagements(List<Adom> customAdoms)
        {
            List<Management> discoveredDevices = [];
            foreach (Adom adom in customAdoms)
            {
                Management currentManagement = CreateManagement(SuperManagement, adom.Name, adom.Uid);
                if (adom.DeviceList != null)
                {
                    foreach (FortiGate fg in adom.DeviceList)
                    {
                        string devName = fg.Name;
                        Device devFound = new()
                        {
                            Name = devName,
                            DeviceType = new DeviceType { Id = 10 } // fortiGate
                        };
                        currentManagement.Devices = currentManagement.Devices.Append(devFound).ToArray();
                        Log.WriteDebug(Autodiscovery, $"adom device found Name = {devFound.Name}");
                    }
                }
                discoveredDevices.Add(currentManagement);
            }
            return discoveredDevices;
        }
        
        // #if DEBUG
        //         List<Management> fillTestDevices()
        //         {
        //             List<Management> testDevices = new List<Management>();
        //             Management currentManagement = new Management
        //             {
        //                 Name = $"{superManagement.Name}{AdomSeparator}{TestAdom}",
        //                 ImporterHostname = superManagement.ImporterHostname,
        //                 Hostname = superManagement.Hostname,
        //                 ImportUser = superManagement.ImportUser,
        //                 Secret = superManagement.Secret,
        //                 Port = superManagement.Port,
        //                 ImportDisabled = false,
        //                 ForceInitialImport = true,
        //                 HideInUi = false,
        //                 ConfigPath = "TestAdom",
        //                 DebugLevel = superManagement.DebugLevel,
        //                 SuperManagerId = superManagement.Id,
        //                 DeviceType = new DeviceType { Id = 11 },
        //                 Devices = new Device[] { }
        //             };
        //             Device dev1 = new Device
        //             {
        //                 Name = "TestGateway1",
        //                 LocalRulebase = "Package1",
        //                 Package = "Package1",
        //                 DeviceType = new DeviceType { Id = 10 } // fortiGate
        //             };
        //             currentManagement.Devices = currentManagement.Devices.Append(dev1).ToArray();
        //             Device dev2 = new Device
        //             {
        //                 Name = "TestGateway2",
        //                 LocalRulebase = "Package2",
        //                 Package = "Package2",
        //                 DeviceType = new DeviceType { Id = 10 } // fortiGate
        //             };
        //             currentManagement.Devices = currentManagement.Devices.Append(dev2).ToArray();
        //             testDevices.Add(currentManagement);
        //             return testDevices;
        //         }
        // #endif
    }
}
