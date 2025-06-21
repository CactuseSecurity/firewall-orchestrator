using FWO.Api.Client;
using FWO.Data;
using FWO.Logging;
// using FWO.Rest.Client;
using FWO.Api.Client.Queries;
using MailKit.Security;
using RestSharp;
using System.Net;
using FWO.Basics;

namespace FWO.DeviceAutoDiscovery
{
    public class AutoDiscoveryFortiManager : AutoDiscoveryBase
    {
        public AutoDiscoveryFortiManager(Management superManagement, ApiConnection apiConn) : base(superManagement, apiConn) { }

        public override async Task<List<Management>> Run()
        {
            List<Management> discoveredDevices = [];
            Log.WriteAudit(GlobalConst.kAutodiscovery, $"starting discovery for {SuperManagement.Name} (id={SuperManagement.Id})");
            // #if DEBUG
            //            discoveredDevices = fillTestDevices();
            // #endif
            if (SuperManagement.DeviceType.Name == "FortiManager")
            {
                await DiscoverySession(discoveredDevices);
            }
            return await GetDeltas(discoveredDevices);
        }

        private async Task DiscoverySession(List<Management> discoveredDevices)
        {
            Log.WriteDebug(GlobalConst.kAutodiscovery, $"discovering FortiManager adoms, vdoms, devices");
            FortiManagerClient restClientFM = new(SuperManagement);

            RestResponse<SessionAuthInfo> sessionResponse = await restClientFM.AuthenticateUser(SuperManagement.ImportCredential.ImportUser, SuperManagement.ImportCredential.Secret);
            if (sessionResponse.StatusCode == HttpStatusCode.OK && sessionResponse.IsSuccessful && !string.IsNullOrEmpty(sessionResponse?.Data?.SessionId))
            {
                string sessionId = sessionResponse.Data.SessionId;
                Log.WriteDebug(GlobalConst.kAutodiscovery, $"successful FortiManager login, got SessionID: {sessionId}");
                // need to use @ verbatim identifier for special chars in sessionId

                await CollectDevices(sessionId, restClientFM, discoveredDevices);

                sessionResponse = await restClientFM.DeAuthenticateUser(sessionId);
                if (sessionResponse.StatusCode == HttpStatusCode.OK)
                {
                    string sessionId = sessionResponse.Data.SessionId;
                    Log.WriteDebug(GlobalConst.kAutodiscovery, $"successful FortiManager login, got SessionID: {sessionId}");


                    // when passing sessionId, we always need to use @ verbatim identifier for special chars in sessionId
                    if (string.IsNullOrEmpty(SuperManagement.Uid))  // pre v9 managements might not have a UID
                    {
                        // update manager UID in existing management; typically triggered in daily scheduler
                        // this update happens only once when AutoDiscovery v9.0 is run for the first time
                        SuperManagement.Uid = await GetFortiManagerUid(restClientFM, sessionId, SuperManagement.Name);
                        var vars = new { id = SuperManagement.Id, uid = SuperManagement.Uid };
                        _ = (await apiConnection.SendQueryAsync<ReturnId>(DeviceQueries.updateManagementUid, vars)).UpdatedId;
                        // TODO: also add UIDs in gateways?
                    }


                    RestResponse<FmApiTopLevelHelper> adomResponse = await restClientFM.GetAdoms(sessionId);
                    if (adomResponse.StatusCode == HttpStatusCode.OK && adomResponse.IsSuccessful)
                    {
                        List<Adom>? adomList = adomResponse?.Data?.Result[0]?.AdomList;
                        if (adomList?.Count > 0)

                        {
                            Log.WriteDebug(GlobalConst.kAutodiscovery, $"found a total of {adomList.Count} adoms");
                            foreach (Adom adom in adomList)
                            {
                                Log.WriteDebug(GlobalConst.kAutodiscovery, $"found adom {adom.Name}");
                                if (!predefinedAdoms.Contains(adom.Name))
                                {
                                    Log.WriteDebug(GlobalConst.kAutodiscovery, $"found non-predefined adom {adom.Name}");
                                    customAdoms.Add(adom);
                                }
                            }
                            customAdoms.Add(new Adom { Name = "global" }); // adding global adom
                        }
                        else
                            Log.WriteWarning(GlobalConst.kAutodiscovery, $"found no adoms at all!");
                    }
                    else
                        Log.WriteWarning("AutoDiscovery", $"error while getting ADOM list: {adomResponse.ErrorMessage}");

                    RestResponse<FmApiTopLevelHelperDev> deviceResponse = await restClientFM.GetDevices(@sessionId);
                    if (deviceResponse.StatusCode == HttpStatusCode.OK && deviceResponse.IsSuccessful)
                    {
                        if (deviceResponse.Data != null && deviceResponse.Data.Result.Count > 0)
                        {
                            List<FortiGate> fortigateList = deviceResponse.Data.Result[0].DeviceList;
                            foreach (FortiGate fg in fortigateList)
                            {
                                Log.WriteDebug(GlobalConst.kAutodiscovery, $"found device {fg.Name} belonging to management VDOM {fg.MgtVdom}");
                                foreach (Vdom vdom in fg.VdomList)
                                {
                                    Log.WriteDebug(GlobalConst.kAutodiscovery, $"found vdom {vdom.Name} belonging to device {fg.Name}");
                                }
                            }
                        }
                        foreach (Adom adom in customAdoms)
                        {
                            Management currentManagement = CreateManagement(superManagement, adom.Name, adom.Uid);
                            RestResponse<FmApiTopLevelHelperAssign> assignResponse = await restClientFM.GetPackageAssignmentsPerAdom(@sessionId, adom.Name);
                            if (assignResponse.StatusCode == HttpStatusCode.OK && assignResponse.IsSuccessful)
                            {
                                if (assignResponse.Data != null && assignResponse.Data.Result.Count > 0)
                                {
                                    List<Assignment> assignmentList = assignResponse.Data.Result[0].AssignmentList;
                                    foreach (Assignment assign in assignmentList)
                                    {
                                        Device devFound = new();
                                        Log.WriteDebug(GlobalConst.kAutodiscovery, $"found assignment1 in ADOM {adom.Name}: package {assign.PackageName} assigned to device {assign.RulebaseName}, vdom: {assign.VdomName} ");
                                        if (assign.RulebaseName != null)
                                        {
                                            Log.WriteDebug(GlobalConst.kAutodiscovery, $"found assignment2 (device<>null) in ADOM {adom.Name}: package {assign.PackageName} assigned to device {assign.RulebaseName}, vdom: {assign.VdomName} ");
                                            if (assign.RulebaseName != "")
                                            {
                                                Log.WriteDebug(GlobalConst.kAutodiscovery, $"found assignment3 (non-device-empty-string) in ADOM {adom.Name}: package {assign.PackageName} assigned to device {assign.RulebaseName}, vdom: {assign.VdomName} ");
                                                string devName = assign.RulebaseName;
                                                if (assign.VdomName != null && assign.VdomName != "")
                                                    devName += "_" + assign.VdomName;
                                                devFound = new Device
                                                {
                                                    Name = devName,
                                                    // LocalRulebase = assign.PackageName,
                                                    // Package = assign.PackageName,
                                                    DeviceType = new DeviceType { Id = 10 } // fortiGate
                                                };
                                                // handle global vs. local based on VdomName?
                                                Log.WriteDebug(GlobalConst.kAutodiscovery, $"assignment devFound Name = {devFound.Name}");
                                                Log.WriteDebug(GlobalConst.kAutodiscovery, $"assignment currentManagement before Append contains {currentManagement.Devices.Length} devices");
                                                currentManagement.Devices = currentManagement.Devices.Append(devFound).ToArray();
                                                Log.WriteDebug(GlobalConst.kAutodiscovery, $"assignment currentManagement after Append contains {currentManagement.Devices.Length} devices");
                                            }
                                        }
                                        adom.Assignments.Add(assign);
                                    }
                                }
                            }
                            discoveredDevices.Add(currentManagement); // add discovered adom including devices
                        }
                    }
                    else
                        Log.WriteWarning(GlobalConst.kAutodiscovery, $"error while getting device/fortigate list: {deviceResponse.ErrorMessage}");

                    sessionResponse = await restClientFM.DeAuthenticateUser(sessionId);
                    if (sessionResponse.StatusCode == HttpStatusCode.OK)
                        Log.WriteDebug(GlobalConst.kAutodiscovery, $"successful FortiManager logout");
                    else
                        Log.WriteWarning(GlobalConst.kAutodiscovery, $"error while logging out from FortiManager: {sessionResponse.ErrorMessage}");
                }
                else
                {
                    Log.WriteWarning(GlobalConst.kAutodiscovery, $"error while logging out from FortiManager: {sessionResponse.ErrorMessage}");
                }
            }
            else
            {
                string errorTxt = $"error while logging in to FortiManager: {sessionResponse.ErrorMessage} ";
                if (sessionResponse.Data?.SessionId == "")
                {
                    errorTxt += "could not authenticate to FortiManager - got empty session ID";
                }
                Log.WriteWarning(GlobalConst.kAutodiscovery, errorTxt);
                throw new AuthenticationException(errorTxt);
            }
        }

        private async Task CollectDevices(string sessionId, FortiManagerClient restClientFM, List<Management> discoveredDevices)
        {
            List<Adom> customAdoms = [];
            await GetAdoms(sessionId, customAdoms, restClientFM);

            RestResponse<FmApiTopLevelHelperDev> deviceResponse = await restClientFM.GetDevices(@sessionId);
            if (deviceResponse.StatusCode == HttpStatusCode.OK && deviceResponse.IsSuccessful)
            {
                LogDeviceResponse(deviceResponse);
                foreach (Adom adom in customAdoms)
                {
                    // add discovered adom including devices
                    discoveredDevices.Add(await CreateManagementFromAdom(adom, sessionId, restClientFM));
                }
            }
            else
            {
                Log.WriteWarning(GlobalConst.kAutodiscovery, $"error while getting device/fortigate list: {deviceResponse.ErrorMessage}");
            }
        }

        private async Task GetAdoms(string sessionId, List<Adom> customAdoms, FortiManagerClient restClientFM)
        {
            RestResponse<FmApiTopLevelHelper> adomResponse = await restClientFM.GetAdoms(sessionId);
            if (adomResponse.StatusCode == HttpStatusCode.OK && adomResponse.IsSuccessful)
            {
                List<Adom>? adomList = adomResponse?.Data?.Result[0]?.AdomList;
                if (adomList?.Count > 0)
                {
                    Log.WriteDebug(GlobalConst.kAutodiscovery, $"found a total of {adomList.Count} adoms");
                    foreach (Adom adom in adomList)
                    {
                        Log.WriteDebug(GlobalConst.kAutodiscovery, $"found adom {adom.Name}");
                        if (!PredefinedAdoms.Contains(adom.Name))
                        {
                            Log.WriteDebug(GlobalConst.kAutodiscovery, $"found non-predefined adom {adom.Name}");
                            customAdoms.Add(adom);
                        }
                    }
                    customAdoms.Add(new Adom { Name = "global" }); // adding global adom
                }
                else
                {
                    Log.WriteWarning(GlobalConst.kAutodiscovery, $"found no adoms at all!");
                }
            }
            else
            {
                Log.WriteWarning(GlobalConst.kAutodiscovery, $"error while getting ADOM list: {adomResponse.ErrorMessage}");
            }
        }

        private void LogDeviceResponse(RestResponse<FmApiTopLevelHelperDev> deviceResponse)
        {
            if (deviceResponse.Data != null && deviceResponse.Data.Result.Count > 0)
            {
                List<FortiGate> fortigateList = deviceResponse.Data.Result[0].DeviceList;
                foreach (FortiGate fg in fortigateList)
                {
                    Log.WriteDebug(GlobalConst.kAutodiscovery, $"found device {fg.Name} belonging to management VDOM {fg.MgtVdom}");
                    foreach (Vdom vdom in fg.VdomList)
                    {
                        Log.WriteDebug(GlobalConst.kAutodiscovery, $"found vdom {vdom.Name} belonging to device {fg.Name}");
                    }
                }
            }
        }

        private async Task<Management> CreateManagementFromAdom(Adom adom, string sessionId, FortiManagerClient restClientFM)
        {
            // create object from discovered adom
            Management currentManagement = new()
            {
                Name = SuperManagement.Name + "__" + adom.Name,
                ImporterHostname = SuperManagement.ImporterHostname,
                Hostname = SuperManagement.Hostname,
                ImportCredential = SuperManagement.ImportCredential,
                Port = SuperManagement.Port,
                ImportDisabled = false,
                ForceInitialImport = true,
                HideInUi = false,
                ConfigPath = adom.Name,
                DebugLevel = SuperManagement.DebugLevel,
                SuperManagerId = SuperManagement.Id,
                DeviceType = new DeviceType { Id = 11 },
                Devices = []
            };

            RestResponse<FmApiTopLevelHelperAssign> assignResponse = await restClientFM.GetPackageAssignmentsPerAdom(@sessionId, adom.Name);
            if (assignResponse.StatusCode == HttpStatusCode.OK && assignResponse.IsSuccessful && assignResponse.Data != null && assignResponse.Data.Result.Count > 0)
            {
                List<Assignment> assignmentList = assignResponse.Data.Result[0].AssignmentList;
                foreach (Assignment assign in assignmentList)
                {
                    // assign.PackageName = assign.PackageName.Replace("/", "\\/");    // replace / in package name with \/
                    Log.WriteDebug(GlobalConst.kAutodiscovery, $"found assignment1 in ADOM {adom.Name}: package {assign.PackageName} assigned to device {assign.DeviceName}, vdom: {assign.VdomName} ");
                    if (assign.DeviceName != null)
                    {
                        Log.WriteDebug(GlobalConst.kAutodiscovery, $"found assignment2 (device<>null) in ADOM {adom.Name}: package {assign.PackageName} assigned to device {assign.DeviceName}, vdom: {assign.VdomName} ");
                        if (assign.DeviceName != "")
                        {
                            Log.WriteDebug(GlobalConst.kAutodiscovery, $"found assignment3 (non-device-empty-string) in ADOM {adom.Name}: package {assign.PackageName} assigned to device {assign.DeviceName}, vdom: {assign.VdomName} ");
                            Log.WriteDebug(GlobalConst.kAutodiscovery, $"assignment currentManagement before Append contains {currentManagement.Devices.Length} devices");
                            currentManagement.Devices = [.. currentManagement.Devices, CreateDeviceFromAssignment(assign)];
                            Log.WriteDebug(GlobalConst.kAutodiscovery, $"assignment currentManagement after Append contains {currentManagement.Devices.Length} devices");
                        }
                    }
                    adom.Assignments.Add(assign);
                }
            }
            return currentManagement;
        }

        private Device CreateDeviceFromAssignment(Assignment assign)
        {
            string devName = assign.DeviceName;
            if (assign.VdomName != null && assign.VdomName != "")
            {
                devName += "_" + assign.VdomName;
            }
            Device devFound = new()
            {
                Name = devName,
                LocalRulebase = assign.PackageName,
                Package = assign.PackageName,
                DeviceType = new DeviceType { Id = 10 } // fortiGate
            };
            // handle global vs. local based on VdomName?
            Log.WriteDebug(GlobalConst.kAutodiscovery, $"assignment devFound Name = {devFound.Name}");
            return devFound;
        }

        override protected Management CreateManagement(Management superManagement, string domainName, string domainUid)
        {
            // create object from discovered adom
            Management currentManagement = new Management
            {
                Name = superManagement.Name + "__" + domainName,
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

        protected static async Task<string> GetFortiManagerUid(FortiManagerClient restClient, string sessionIdPerDomain, string mgmName)
        {
            return mgmName; // fortiManager does not have a real UID
            // return await restClient.GetFortiManagerDetails(@sessionIdPerDomain);
        }

        // #if DEBUG
        //         List<Management> fillTestDevices()
        //         {
        //             List<Management> testDevices = new List<Management>();
        //             Management currentManagement = new Management
        //             {
        //                 Name = SuperManagement.Name + "__TestAdom",
        //                 ImporterHostname = SuperManagement.ImporterHostname,
        //                 Hostname = SuperManagement.Hostname,
        //                 ImportUser = SuperManagement.ImportUser,
        //                 Secret = SuperManagement.Secret,
        //                 Port = SuperManagement.Port,
        //                 ImportDisabled = false,
        //                 ForceInitialImport = true,
        //                 HideInUi = false,
        //                 ConfigPath = "TestAdom",
        //                 DebugLevel = SuperManagement.DebugLevel,
        //                 SuperManagerId = SuperManagement.Id,
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
