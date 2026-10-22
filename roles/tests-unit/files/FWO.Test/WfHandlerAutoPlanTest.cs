using FWO.Data;
using FWO.Data.DeviceSpecificSettings;
using FWO.Data.Middleware;
using FWO.Data.Workflow;
using FWO.Middleware.Client;
using FWO.Services.Workflow;
using NUnit.Framework;
using RestSharp;
using System.Net;
using System.Reflection;
using System.Security.Claims;

namespace FWO.Test
{
    /// <summary>
    /// Tests the automatic creation of implementation tasks along the auto plan device specific settings: the precedence
    /// of entered devices over the path analysis algorithm, the fallback task and the resolution of the settings.
    /// </summary>
    [TestFixture]
    internal class WfHandlerAutoPlanTest
    {
        private const long kGlobalNodeId = 1;
        private const long kDeviceTypeNodeId = 2;
        private const long kManagementNodeId = 3;
        private const long kGatewayNodeId = 4;
        private const int kDeviceTypeId = 9;
        private const int kManagementId = 20;
        private const int kFirstGatewayId = 1;
        private const int kSecondGatewayId = 2;
        private const int kUnknownGatewayId = 99;
        private const int kUnknownManagementId = 98;
        private const int kOpenStateId = 4;
        private const int kCompletedStateId = 10;

        private static readonly List<int> kFirstGateway = [kFirstGatewayId];
        private static readonly List<int> kSecondGateway = [kSecondGatewayId];
        private static readonly List<int> kBothGateways = [kFirstGatewayId, kSecondGatewayId];
        private static readonly List<int> kNoGateway = [];
        private static readonly List<int> kAllDevicesMarker = [WfReqTaskBase.kAllDevicesId];
        private static readonly List<int?> kFirstGatewayTask = [kFirstGatewayId];
        private static readonly List<int?> kBothGatewayTasks = [kFirstGatewayId, kSecondGatewayId];
        private static readonly List<int?> kTaskWithoutGateway = [null];
        private static readonly List<int> kRequestedGateways = [kFirstGatewayId, kSecondGatewayId, kUnknownGatewayId];
        private static readonly List<int> kRequestedManagements = [kManagementId, kUnknownManagementId];
        private static readonly List<int> kNoIds = [];
        private static readonly List<string> kExpectedScopeTypes =
            [AutoPlanScopeTypes.kGlobal, AutoPlanScopeTypes.kManagement, AutoPlanScopeTypes.kGateway, AutoPlanScopeTypes.kGateway];

        private static InMemoryDeviceSpecificSettingsApiConnection CreateApi()
        {
            DeviceType deviceType = new() { Id = kDeviceTypeId, Name = "Check Point", Version = "R8x" };
            InMemoryDeviceSpecificSettingsApiConnection api = new();
            api.Managements.Add(new Management
            {
                Id = kManagementId,
                Name = "mgm",
                DeviceType = deviceType,
                Devices = [new Device { Id = kFirstGatewayId, Name = "gw-1" }, new Device { Id = kSecondGatewayId, Name = "gw-2" }]
            });
            api.AddNode(kGlobalNodeId, DeviceSpecificScopeType.Global, DeviceSpecificScopePath.GlobalObjectKey, displayName: "Global");
            api.AddNode(kDeviceTypeNodeId, DeviceSpecificScopeType.DeviceType, kDeviceTypeId.ToString(), kGlobalNodeId);
            api.AddNode(kManagementNodeId, DeviceSpecificScopeType.Management, kManagementId.ToString(), kDeviceTypeNodeId);
            api.AddNode(kGatewayNodeId, DeviceSpecificScopeType.Gateway, kFirstGatewayId.ToString(), kManagementNodeId);
            return api;
        }

        private static WfHandler CreateHandler(InMemoryDeviceSpecificSettingsApiConnection api,
            ReqDeviceInputOptions deviceInput = ReqDeviceInputOptions.enterInReqTask)
        {
            return new WfHandler(new SimulatedUserConfig { ReqDeviceInput = deviceInput }, api, WorkflowPhases.planning, null)
            {
                Devices = CreateDevices()
            };
        }

        private static List<Device> CreateDevices()
        {
            return [new Device { Id = kFirstGatewayId, Name = "gw-1" }, new Device { Id = kSecondGatewayId, Name = "gw-2" }];
        }

        private static WfReqTask CreateAccessTask(List<int> enteredDeviceIds)
        {
            WfReqTask reqTask = new() { Id = 8, TaskType = WfTaskType.access.ToString(), StateId = kOpenStateId, Title = "Access", ManagementId = kManagementId };
            reqTask.SetDeviceList(enteredDeviceIds);
            reqTask.Elements.Add(new WfReqElement { Field = ElemFieldType.source.ToString(), IpString = "10.0.0.1" });
            reqTask.Elements.Add(new WfReqElement { Field = ElemFieldType.destination.ToString(), IpString = "10.0.1.1" });
            return reqTask;
        }

        private static WfReqTask CreateRuleDeleteTask()
        {
            WfReqTask reqTask = new() { Id = 9, TaskType = WfTaskType.rule_delete.ToString(), StateId = kOpenStateId, Title = "Delete" };
            reqTask.Elements.Add(new WfReqElement { Field = ElemFieldType.rule.ToString(), DeviceId = kFirstGatewayId, RuleUid = "uid-1" });
            return reqTask;
        }

        private static async Task InvokeAutoCreateImplTasks(WfHandler handler, WfReqTask reqTask)
        {
            MethodInfo method = typeof(WfHandler).GetMethod("AutoCreateImplTasks", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMethodException(nameof(WfHandler), "AutoCreateImplTasks");
            object[] arguments = [reqTask];
            await (Task)method.Invoke(handler, arguments)!;
        }

        private static void SetMatrix(WfHandler handler, string taskType, StateMatrix matrix)
        {
            FieldInfo field = typeof(WfHandler).GetField("stateMatrixDict", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingFieldException(nameof(WfHandler), "stateMatrixDict");
            ((StateMatrixDict)field.GetValue(handler)!).Matrices[taskType] = matrix;
        }

        private static List<int?> DeviceIds(WfReqTask reqTask)
        {
            return [.. reqTask.ImplementationTasks.Select(implTask => implTask.DeviceId)];
        }

        [Test]
        public async Task AccessTask_RulesPlannedManuallyByDefault_CreatesNoTask()
        {
            WfHandler handler = CreateHandler(CreateApi());
            WfReqTask reqTask = CreateAccessTask(kBothGateways);

            await InvokeAutoCreateImplTasks(handler, reqTask);

            Assert.That(reqTask.ImplementationTasks, Is.Empty);
        }

        [Test]
        public async Task AccessTask_GatewayOverride_OnlyThatGatewayGetsTask()
        {
            InMemoryDeviceSpecificSettingsApiConnection api = CreateApi();
            api.SetValue(kGatewayNodeId, DeviceSpecificSettingKeys.AutoPlanImplementationForRules, true);
            WfHandler handler = CreateHandler(api);
            WfReqTask reqTask = CreateAccessTask(kBothGateways);

            await InvokeAutoCreateImplTasks(handler, reqTask);

            Assert.That(DeviceIds(reqTask), Is.EqualTo(kFirstGatewayTask));
        }

        [Test]
        public async Task AccessTask_ManagementWithoutAutoPlan_OverridesRulesSettingOfItsGateways()
        {
            InMemoryDeviceSpecificSettingsApiConnection api = CreateApi();
            api.SetValue(kGlobalNodeId, DeviceSpecificSettingKeys.AutoPlanImplementationForRules, true);
            api.SetValue(kManagementNodeId, DeviceSpecificSettingKeys.AutoPlanImplementation, false);
            WfHandler handler = CreateHandler(api);
            WfReqTask reqTask = CreateAccessTask(kBothGateways);

            await InvokeAutoCreateImplTasks(handler, reqTask);

            Assert.That(reqTask.ImplementationTasks, Is.Empty, "rules need both settings");
        }

        [Test]
        public async Task AccessTask_EnteredDevicesTakePrecedenceOverAlgorithm()
        {
            InMemoryDeviceSpecificSettingsApiConnection api = CreateApi();
            api.SetValue(kGlobalNodeId, DeviceSpecificSettingKeys.AutoPlanImplementationForRules, true);
            WfHandler handler = CreateHandler(api);
            FixedPathAnalyzer analyzer = new(kSecondGateway);
            handler.PathAnalyzerOverride = analyzer;
            WfReqTask reqTask = CreateAccessTask(kFirstGateway);

            await InvokeAutoCreateImplTasks(handler, reqTask);

            Assert.Multiple(() =>
            {
                Assert.That(DeviceIds(reqTask), Is.EqualTo(kFirstGatewayTask));
                Assert.That(analyzer.Requests, Is.Empty);
            });
        }

        [Test]
        public async Task AccessTask_WithoutEnteredDevices_UsesAlgorithmWithAddressesOfRequest()
        {
            InMemoryDeviceSpecificSettingsApiConnection api = CreateApi();
            api.SetValue(kGlobalNodeId, DeviceSpecificSettingKeys.AutoPlanImplementationForRules, true);
            WfHandler handler = CreateHandler(api, ReqDeviceInputOptions.never);
            FixedPathAnalyzer analyzer = new(kBothGateways);
            handler.PathAnalyzerOverride = analyzer;
            WfReqTask reqTask = CreateAccessTask(kNoGateway);

            await InvokeAutoCreateImplTasks(handler, reqTask);

            Assert.Multiple(() =>
            {
                Assert.That(DeviceIds(reqTask), Is.EqualTo(kBothGatewayTasks));
                Assert.That(analyzer.Requests.Single().Sources.Single().ToString(), Is.EqualTo("10.0.0.1"));
                Assert.That(analyzer.Requests.Single().Destinations.Single().ToString(), Is.EqualTo("10.0.1.1"));
            });
        }

        [Test]
        public async Task AccessTask_FailingAlgorithm_CreatesFallbackTaskWithoutGateway()
        {
            InMemoryDeviceSpecificSettingsApiConnection api = CreateApi();
            api.SetValue(kGlobalNodeId, DeviceSpecificSettingKeys.AutoPlanImplementationForRules, true);
            WfHandler handler = CreateHandler(api);
            handler.PathAnalyzerOverride = new FixedPathAnalyzer(kBothGateways, fail: true);
            WfReqTask reqTask = CreateAccessTask(kNoGateway);

            await InvokeAutoCreateImplTasks(handler, reqTask);

            Assert.That(DeviceIds(reqTask), Is.EqualTo(kTaskWithoutGateway));
        }

        [Test]
        public async Task AccessTask_NoTargetWhileRulesPlannedManually_CreatesNoFallback()
        {
            WfHandler handler = CreateHandler(CreateApi());
            handler.PathAnalyzerOverride = new FixedPathAnalyzer(kNoGateway);
            WfReqTask reqTask = CreateAccessTask(kNoGateway);

            await InvokeAutoCreateImplTasks(handler, reqTask);

            Assert.That(reqTask.ImplementationTasks, Is.Empty);
        }

        [Test]
        public async Task AccessTask_GatewaysPlannedManuallyUnderAutomaticGlobal_CreatesNoFallback()
        {
            InMemoryDeviceSpecificSettingsApiConnection api = CreateApi();
            api.SetValue(kGlobalNodeId, DeviceSpecificSettingKeys.AutoPlanImplementationForRules, true);
            api.SetValue(kManagementNodeId, DeviceSpecificSettingKeys.AutoPlanImplementationForRules, false);
            WfHandler handler = CreateHandler(api);
            WfReqTask reqTask = CreateAccessTask(kBothGateways);

            await InvokeAutoCreateImplTasks(handler, reqTask);

            Assert.That(reqTask.ImplementationTasks, Is.Empty);
        }

        [Test]
        public async Task AccessTask_AllDevicesWithOneTaskOption_CreatesOneTaskWithoutGateway()
        {
            InMemoryDeviceSpecificSettingsApiConnection api = CreateApi();
            api.SetValue(kGlobalNodeId, DeviceSpecificSettingKeys.AutoPlanImplementationForRules, true);
            WfHandler handler = CreateHandler(api, ReqDeviceInputOptions.oneTaskForAllDevices);
            WfReqTask reqTask = CreateAccessTask(kAllDevicesMarker);

            await InvokeAutoCreateImplTasks(handler, reqTask);

            Assert.That(DeviceIds(reqTask), Is.EqualTo(kTaskWithoutGateway));
        }

        [Test]
        public async Task AccessTask_AllDevicesWithEnterOption_CreatesTaskPerGateway()
        {
            InMemoryDeviceSpecificSettingsApiConnection api = CreateApi();
            api.SetValue(kGlobalNodeId, DeviceSpecificSettingKeys.AutoPlanImplementationForRules, true);
            WfHandler handler = CreateHandler(api);
            WfReqTask reqTask = CreateAccessTask(kAllDevicesMarker);

            await InvokeAutoCreateImplTasks(handler, reqTask);

            Assert.That(DeviceIds(reqTask), Is.EqualTo(kBothGatewayTasks));
        }

        [Test]
        public async Task RuleDeleteTask_FollowsSettingsOfRuleGateway()
        {
            WfHandler manualHandler = CreateHandler(CreateApi());
            WfReqTask manualTask = CreateRuleDeleteTask();
            InMemoryDeviceSpecificSettingsApiConnection automaticApi = CreateApi();
            automaticApi.SetValue(kGatewayNodeId, DeviceSpecificSettingKeys.AutoPlanImplementationForRules, true);
            WfHandler automaticHandler = CreateHandler(automaticApi);
            WfReqTask automaticTask = CreateRuleDeleteTask();

            await InvokeAutoCreateImplTasks(manualHandler, manualTask);
            await InvokeAutoCreateImplTasks(automaticHandler, automaticTask);

            Assert.Multiple(() =>
            {
                Assert.That(manualTask.ImplementationTasks, Is.Empty);
                Assert.That(DeviceIds(automaticTask), Is.EqualTo(kFirstGatewayTask));
            });
        }

        [Test]
        public async Task GroupTask_ManagementWithoutAutoPlan_CreatesNoTask()
        {
            InMemoryDeviceSpecificSettingsApiConnection api = CreateApi();
            api.SetValue(kManagementNodeId, DeviceSpecificSettingKeys.AutoPlanImplementation, false);
            WfHandler handler = CreateHandler(api);
            WfReqTask reqTask = new() { Id = 7, TaskType = WfTaskType.group_create.ToString(), StateId = kOpenStateId, Title = "Group", ManagementId = kManagementId };

            await InvokeAutoCreateImplTasks(handler, reqTask);

            Assert.That(reqTask.ImplementationTasks, Is.Empty);
        }

        [Test]
        public async Task GroupTask_AutoPlannedByDefault_CreatesTaskOnManagement()
        {
            WfHandler handler = CreateHandler(CreateApi());
            WfReqTask reqTask = new() { Id = 7, TaskType = WfTaskType.group_create.ToString(), StateId = kOpenStateId, Title = "Group", ManagementId = kManagementId };

            await InvokeAutoCreateImplTasks(handler, reqTask);

            Assert.That(reqTask.ImplementationTasks.Single().ManagementId, Is.EqualTo(kManagementId));
        }

        [Test]
        public async Task GenericTask_FollowsGlobalAutoPlan()
        {
            InMemoryDeviceSpecificSettingsApiConnection manualApi = CreateApi();
            manualApi.SetValue(kGlobalNodeId, DeviceSpecificSettingKeys.AutoPlanImplementation, false);
            WfReqTask manualTask = new() { Id = 6, TaskType = WfTaskType.generic.ToString(), StateId = kOpenStateId, Title = "Generic" };
            WfReqTask automaticTask = new() { Id = 6, TaskType = WfTaskType.generic.ToString(), StateId = kOpenStateId, Title = "Generic" };

            await InvokeAutoCreateImplTasks(CreateHandler(manualApi), manualTask);
            await InvokeAutoCreateImplTasks(CreateHandler(CreateApi()), automaticTask);

            Assert.Multiple(() =>
            {
                Assert.That(manualTask.ImplementationTasks, Is.Empty);
                Assert.That(automaticTask.ImplementationTasks, Has.Count.EqualTo(1));
            });
        }

        [Test]
        public async Task CreateMissingImplTasksForTicket_FollowsAutoPlanSettings()
        {
            WfHandler manualHandler = CreateHandler(CreateApi());
            SetMatrix(manualHandler, WfTaskType.access.ToString(), new StateMatrix { MinTicketCompleted = kCompletedStateId });
            WfTicket manualTicket = new() { Id = 5, Tasks = [CreateAccessTask(kBothGateways)] };
            InMemoryDeviceSpecificSettingsApiConnection automaticApi = CreateApi();
            automaticApi.SetValue(kGlobalNodeId, DeviceSpecificSettingKeys.AutoPlanImplementationForRules, true);
            WfHandler automaticHandler = CreateHandler(automaticApi);
            SetMatrix(automaticHandler, WfTaskType.access.ToString(), new StateMatrix { MinTicketCompleted = kCompletedStateId });
            WfTicket automaticTicket = new() { Id = 6, Tasks = [CreateAccessTask(kBothGateways)] };

            int manualCount = await manualHandler.CreateMissingImplTasksForTicket(manualTicket);
            int automaticCount = await automaticHandler.CreateMissingImplTasksForTicket(automaticTicket);

            Assert.Multiple(() =>
            {
                Assert.That(manualCount, Is.EqualTo(0), "rules are planned manually by default");
                Assert.That(automaticCount, Is.EqualTo(kBothGateways.Count));
            });
        }

        [Test]
        public async Task UiHandler_TakesSettingsFromMiddleware()
        {
            AutoPlanMiddlewareClient client = new(CreateApi());
            WfHandler handler = CreateUiHandler(client);
            WfReqTask reqTask = CreateAccessTask(kBothGateways);

            await InvokeAutoCreateImplTasks(handler, reqTask);

            Assert.Multiple(() =>
            {
                Assert.That(reqTask.ImplementationTasks, Is.Empty, "rules are planned manually by default");
                Assert.That(client.Calls.Single().DeviceIds, Is.EqualTo(kBothGateways));
            });
        }

        [Test]
        public async Task UiHandler_FailingMiddleware_CreatesTasksAutomatically()
        {
            WfHandler handler = CreateUiHandler(new AutoPlanMiddlewareClient(CreateApi()) { Fail = true });
            WfReqTask reqTask = CreateAccessTask(kBothGateways);

            await InvokeAutoCreateImplTasks(handler, reqTask);

            Assert.That(DeviceIds(reqTask), Is.EqualTo(kBothGatewayTasks));
        }

        [Test]
        public async Task Resolver_ReturnsInheritedSettingsOfExistingLevelsOnly()
        {
            InMemoryDeviceSpecificSettingsApiConnection api = CreateApi();
            api.SetValue(kManagementNodeId, DeviceSpecificSettingKeys.AutoPlanImplementation, false);
            api.SetValue(kGatewayNodeId, DeviceSpecificSettingKeys.AutoPlanImplementationForRules, true);

            List<AutoPlanScopeSettings> settings = await new ImplTaskAutoPlanResolver(api).Resolve(kRequestedGateways, kRequestedManagements);

            Assert.Multiple(() =>
            {
                Assert.That(settings.Select(setting => setting.ScopeType), Is.EqualTo(kExpectedScopeTypes));
                Assert.That(settings[0].AutoPlanImplementation, Is.True);
                Assert.That(settings[1].AutoPlanImplementation, Is.False);
                Assert.That(settings[2].Id, Is.EqualTo(kFirstGatewayId));
                Assert.That(settings[2].AutoPlanImplementation, Is.False, "inherited from the management");
                Assert.That(settings[2].AutoPlanImplementationForRules, Is.True);
                Assert.That(settings[3].AutoPlanImplementationForRules, Is.False);
            });
        }

        [Test]
        public async Task Resolver_WithoutIds_ReturnsGlobalSettingsOnly()
        {
            List<AutoPlanScopeSettings> settings = await new ImplTaskAutoPlanResolver(CreateApi()).Resolve(kNoIds, kNoIds);

            Assert.That(settings.Single().ScopeType, Is.EqualTo(AutoPlanScopeTypes.kGlobal));
        }

        [TestCase(true, true, true, true)]
        [TestCase(true, false, false, true)]
        [TestCase(false, true, false, false)]
        [TestCase(false, false, false, false)]
        public void IsAutomatic_CombinesBothSettingsForRules(bool autoPlan, bool forRules, bool expectedForRules, bool expectedForOthers)
        {
            AutoPlanScopeSettings settings = new() { AutoPlanImplementation = autoPlan, AutoPlanImplementationForRules = forRules };

            Assert.Multiple(() =>
            {
                Assert.That(settings.IsAutomatic(true), Is.EqualTo(expectedForRules));
                Assert.That(settings.IsAutomatic(false), Is.EqualTo(expectedForOthers));
            });
        }

        [TestCase(nameof(WfTaskType.access), true)]
        [TestCase(nameof(WfTaskType.rule_modify), true)]
        [TestCase(nameof(WfTaskType.rule_delete), true)]
        [TestCase(nameof(WfTaskType.group_create), false)]
        [TestCase(nameof(WfTaskType.generic), false)]
        public void IsRuleTaskType_RecognizesRuleTasks(string taskType, bool expected)
        {
            Assert.That(WfHandler.IsRuleTaskType(taskType), Is.EqualTo(expected));
        }

        [TestCase(true, 50, 50)]
        [TestCase(false, 50, 99)]
        [TestCase(true, 0, 99)]
        public void MinImplTasksCreatable_StartsWithPlanningPhaseIfActive(bool planningActive, int planningInputState, int expected)
        {
            StateMatrix matrix = new() { MinImplTasksNeeded = 99, MinPlanningInputState = planningInputState };
            matrix.PhaseActive[WorkflowPhases.planning] = planningActive;

            Assert.That(matrix.MinImplTasksCreatable, Is.EqualTo(expected));
        }

        private static WfHandler CreateUiHandler(MiddlewareClient middlewareClient)
        {
            return new WfHandler((_, _, _, _) => { }, new SimulatedUserConfig { ReqDeviceInput = ReqDeviceInputOptions.enterInReqTask },
                new ClaimsPrincipal(), new SimulatedApiConnection(), middlewareClient, WorkflowPhases.planning)
            {
                Devices = CreateDevices()
            };
        }

        /// <summary>
        /// Plays the middleware endpoint getAutoPlanSettings with the middleware's view of the settings.
        /// </summary>
        private sealed class AutoPlanMiddlewareClient(InMemoryDeviceSpecificSettingsApiConnection middlewareApi) : MiddlewareClient("http://localhost/")
        {
            public List<AutoPlanSettingsParameters> Calls { get; } = [];
            public bool Fail { get; init; }

            public override async Task<RestResponse<AutoPlanSettingsResult>> GetAutoPlanSettings(AutoPlanSettingsParameters parameters)
            {
                Calls.Add(parameters);
                if (Fail)
                {
                    return new RestResponse<AutoPlanSettingsResult>(new RestRequest())
                    {
                        StatusCode = HttpStatusCode.InternalServerError,
                        ResponseStatus = ResponseStatus.Completed,
                        IsSuccessStatusCode = false
                    };
                }
                List<AutoPlanScopeSettings> settings = await new ImplTaskAutoPlanResolver(middlewareApi).Resolve(parameters.DeviceIds, parameters.ManagementIds);
                return new RestResponse<AutoPlanSettingsResult>(new RestRequest())
                {
                    StatusCode = HttpStatusCode.OK,
                    ResponseStatus = ResponseStatus.Completed,
                    IsSuccessStatusCode = true,
                    Data = new AutoPlanSettingsResult { Settings = settings }
                };
            }
        }
    }
}
