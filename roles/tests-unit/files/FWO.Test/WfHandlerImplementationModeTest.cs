using FWO.Api.Client.Queries;
using FWO.Data;
using FWO.Data.DeviceSpecificSettings;
using FWO.Data.Workflow;
using FWO.Services.Workflow;
using NUnit.Framework;
using System.Reflection;
using System.Text.Json;

namespace FWO.Test
{
    /// <summary>
    /// Tests how the action "Create implementation tasks" applies the implementation modes: only Automatic targets get
    /// implementation tasks, request tasks handed to an external ticket system get none, gateways of such a management
    /// are left to it or reported, and the scope of the action.
    /// </summary>
    [TestFixture]
    internal class WfHandlerImplementationModeTest
    {
        private const long kGlobalNodeId = 1;
        private const long kDeviceTypeNodeId = 2;
        private const long kManagementNodeId = 3;
        private const long kGatewayNodeId = 4;
        private const long kExternalManagementNodeId = 5;
        private const long kSuperManagementNodeId = 6;
        private const long kSubManagementNodeId = 7;
        private const long kSuperDeviceTypeNodeId = 8;
        private const int kDeviceTypeId = 9;
        private const int kSuperDeviceTypeId = 13;
        private const int kManagementId = 20;
        private const int kExternalManagementId = 30;
        private const int kSuperManagementId = 40;
        private const int kSubManagementId = 41;
        private const int kOtherSubManagementId = 42;
        private const int kFirstGatewayId = 1;
        private const int kSecondGatewayId = 2;
        private const int kExternalGatewayId = 3;
        private const int kExternalTicketSystemId = 2;
        private const int kOpenStateId = 4;
        private const int kApprovedStateId = 5;
        private const int kCompletedStateId = 10;
        private const string kBundleId = "bundle-1";

        private static readonly List<int> kNoGateway = [];
        private static readonly List<int> kFirstGateway = [kFirstGatewayId];
        private static readonly List<int> kAllGateways = [kFirstGatewayId, kSecondGatewayId, kExternalGatewayId];
        private static readonly List<int?> kFirstGatewayTask = [kFirstGatewayId];
        private static readonly List<int?> kInternalGatewayTasks = [kFirstGatewayId, kSecondGatewayId];
        private static readonly List<int> kInternalGateways = [kFirstGatewayId, kSecondGatewayId];
        private static readonly List<int?> kSecondGatewayTask = [kSecondGatewayId];
        private static readonly List<int?> kSuperManagementTask = [kSuperManagementId];
        private static readonly List<int?> kAutomaticManagementTasks = [kManagementId, kSuperManagementId];

        private static InMemoryDeviceSpecificSettingsApiConnection CreateApi()
        {
            InMemoryDeviceSpecificSettingsApiConnection api = new();
            foreach (Management management in CreateManagements())
            {
                api.Managements.Add(management);
            }
            api.AddNode(kGlobalNodeId, DeviceSpecificScopeType.Global, DeviceSpecificScopePath.GlobalObjectKey, displayName: "Global");
            api.AddNode(kDeviceTypeNodeId, DeviceSpecificScopeType.DeviceType, kDeviceTypeId.ToString(), kGlobalNodeId);
            api.AddNode(kSuperDeviceTypeNodeId, DeviceSpecificScopeType.DeviceType, kSuperDeviceTypeId.ToString(), kGlobalNodeId);
            api.AddNode(kManagementNodeId, DeviceSpecificScopeType.Management, kManagementId.ToString(), kDeviceTypeNodeId);
            api.AddNode(kGatewayNodeId, DeviceSpecificScopeType.Gateway, kFirstGatewayId.ToString(), kManagementNodeId);
            api.AddNode(kExternalManagementNodeId, DeviceSpecificScopeType.Management, kExternalManagementId.ToString(), kDeviceTypeNodeId);
            api.AddNode(kSuperManagementNodeId, DeviceSpecificScopeType.Management, kSuperManagementId.ToString(), kSuperDeviceTypeNodeId);
            api.AddNode(kSubManagementNodeId, DeviceSpecificScopeType.Management, kSubManagementId.ToString(), kDeviceTypeNodeId);
            api.SetValue(kGlobalNodeId, DeviceSpecificSettingKeys.AutoPlanImplementationForRules, true);
            return api;
        }

        private static void SetExternalTicketSystem(InMemoryDeviceSpecificSettingsApiConnection api, long nodeId, bool withSystem = true)
        {
            api.SetValue(nodeId, DeviceSpecificSettingKeys.ImplementationModeOverrideForRules, ProvisioningRuleImplementationMode.RerouteToExternalTicketSystem);
            api.SetValue(nodeId, DeviceSpecificSettingKeys.ImplementationMode, ProvisioningImplementationMode.RerouteToExternalTicketSystem);
            if (withSystem)
            {
                api.SetValue(nodeId, DeviceSpecificSettingKeys.RerouteToExternalTicketSystem, (int?)kExternalTicketSystemId);
            }
        }

        private static DeviceType CreateDeviceType()
        {
            return new DeviceType { Id = kDeviceTypeId, Name = "Check Point", Version = "R8x" };
        }

        private static List<Management> CreateManagements()
        {
            DeviceType superType = new() { Id = kSuperDeviceTypeId, Name = "Check Point MDS", Version = "R8x" };
            return
            [
                new Management { Id = kManagementId, Name = "cp1", DeviceType = CreateDeviceType() },
                new Management { Id = kExternalManagementId, Name = "cp2", DeviceType = CreateDeviceType() },
                new Management { Id = kSuperManagementId, Name = "mds", DeviceType = superType, IsSupermanager = true },
                new Management { Id = kSubManagementId, Name = "domain-a", DeviceType = CreateDeviceType(), SuperManagerId = kSuperManagementId },
                new Management { Id = kOtherSubManagementId, Name = "domain-b", DeviceType = CreateDeviceType(), SuperManagerId = kSuperManagementId }
            ];
        }

        private static List<Device> CreateDevices()
        {
            Management management = new() { Id = kManagementId, Name = "cp1", DeviceType = CreateDeviceType() };
            Management externalManagement = new() { Id = kExternalManagementId, Name = "cp2", DeviceType = CreateDeviceType() };
            return
            [
                new Device { Id = kFirstGatewayId, Name = "gw-1", Management = management },
                new Device { Id = kSecondGatewayId, Name = "gw-2", Management = management },
                new Device { Id = kExternalGatewayId, Name = "gw-tufin", Management = externalManagement }
            ];
        }

        private static WfHandler CreateHandler(InMemoryDeviceSpecificSettingsApiConnection api, bool considerBundling = false)
        {
            WfHandler handler = new(new SimulatedUserConfig { ReqConsiderBundling = considerBundling }, api, WorkflowPhases.planning, null)
            {
                Devices = CreateDevices(),
                PathAnalyzerOverride = new FixedPathAnalyzer(kAllGateways)
            };
            SetMatrix(handler, WfTaskType.access.ToString());
            SetMatrix(handler, WfTaskType.group_create.ToString());
            return handler;
        }

        private static WfReqTask CreateAccessTask(long id, int? managementId, List<int> enteredDeviceIds)
        {
            WfReqTask reqTask = new() { Id = id, TicketId = 5, TaskType = WfTaskType.access.ToString(), StateId = kOpenStateId, Title = "Access", ManagementId = managementId };
            reqTask.SetDeviceList(enteredDeviceIds);
            reqTask.Elements.Add(new WfReqElement { Field = ElemFieldType.source.ToString(), IpString = "10.0.0.1" });
            reqTask.Elements.Add(new WfReqElement { Field = ElemFieldType.destination.ToString(), IpString = "10.0.1.1" });
            return reqTask;
        }

        private static WfReqTask CreateGroupTask(long id, int? managementId, string bundleId = "")
        {
            WfReqTask reqTask = new() { Id = id, TicketId = 5, TaskType = WfTaskType.group_create.ToString(), StateId = kOpenStateId, Title = "Group", ManagementId = managementId };
            if (bundleId.Length > 0)
            {
                Dictionary<string, string> addInfo = new() { { AdditionalInfoKeys.FlowBundleId, bundleId } };
                reqTask.AdditionalInfo = JsonSerializer.Serialize(addInfo);
            }
            return reqTask;
        }

        private static WfReqTask CreateRuleDeleteTask(int deviceId)
        {
            WfReqTask reqTask = new() { Id = 9, TicketId = 5, TaskType = WfTaskType.rule_delete.ToString(), StateId = kOpenStateId, Title = "Delete" };
            reqTask.Elements.Add(new WfReqElement { Field = ElemFieldType.rule.ToString(), DeviceId = deviceId, RuleUid = "uid-1" });
            return reqTask;
        }

        private static async Task InvokeAutoCreateImplTasks(WfHandler handler, WfReqTask reqTask)
        {
            MethodInfo method = typeof(WfHandler).GetMethod("AutoCreateImplTasks", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMethodException(nameof(WfHandler), "AutoCreateImplTasks");
            object[] arguments = [reqTask];
            await (Task)method.Invoke(handler, arguments)!;
        }

        private static void SetMatrix(WfHandler handler, string taskType)
        {
            FieldInfo field = typeof(WfHandler).GetField("stateMatrixDict", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingFieldException(nameof(WfHandler), "stateMatrixDict");
            ((StateMatrixDict)field.GetValue(handler)!).Matrices[taskType] = new StateMatrix { MinTicketCompleted = kCompletedStateId };
        }

        private static List<int?> DeviceIds(WfReqTask reqTask)
        {
            return [.. reqTask.ImplementationTasks.Select(implTask => implTask.DeviceId)];
        }

        private static List<int?> ManagementIds(WfReqTask reqTask)
        {
            return [.. reqTask.ImplementationTasks.Select(implTask => implTask.ManagementId)];
        }

        private static bool AlertRaised(InMemoryDeviceSpecificSettingsApiConnection api)
        {
            return api.Calls.Any(call => call.Query == MonitorQueries.getOpenAlerts);
        }

        [Test]
        public async Task AccessTask_GatewayWithManualRuleMode_GetsNoTask()
        {
            InMemoryDeviceSpecificSettingsApiConnection api = CreateApi();
            api.SetValue(kGatewayNodeId, DeviceSpecificSettingKeys.ImplementationModeOverrideForRules, ProvisioningRuleImplementationMode.Manual);
            WfHandler handler = CreateHandler(api);
            WfReqTask reqTask = CreateAccessTask(8, kManagementId, kInternalGateways);

            await InvokeAutoCreateImplTasks(handler, reqTask);

            Assert.That(DeviceIds(reqTask), Is.EqualTo(kSecondGatewayTask));
        }

        [Test]
        public async Task AccessTask_ObjectModeDoesNotApplyToRules()
        {
            InMemoryDeviceSpecificSettingsApiConnection api = CreateApi();
            api.SetValue(kGlobalNodeId, DeviceSpecificSettingKeys.ImplementationMode, ProvisioningImplementationMode.Manual);
            WfHandler handler = CreateHandler(api);
            WfReqTask reqTask = CreateAccessTask(8, kManagementId, kInternalGateways);

            await InvokeAutoCreateImplTasks(handler, reqTask);

            Assert.That(DeviceIds(reqTask), Is.EqualTo(kInternalGatewayTasks));
        }

        [Test]
        public async Task GroupTask_RuleModeDoesNotApplyToObjects()
        {
            InMemoryDeviceSpecificSettingsApiConnection api = CreateApi();
            api.SetValue(kGlobalNodeId, DeviceSpecificSettingKeys.ImplementationModeOverrideForRules, ProvisioningRuleImplementationMode.Manual);
            WfHandler handler = CreateHandler(api);
            WfReqTask reqTask = CreateGroupTask(7, kManagementId);

            await InvokeAutoCreateImplTasks(handler, reqTask);

            Assert.That(ManagementIds(reqTask).Single(), Is.EqualTo(kManagementId));
        }

        [Test]
        public async Task GroupTask_ManualObjectModeOnTarget_GetsNoTask()
        {
            InMemoryDeviceSpecificSettingsApiConnection api = CreateApi();
            api.SetValue(kManagementNodeId, DeviceSpecificSettingKeys.ImplementationMode, ProvisioningImplementationMode.Manual);
            WfHandler handler = CreateHandler(api);
            WfReqTask reqTask = CreateGroupTask(7, kManagementId);

            await InvokeAutoCreateImplTasks(handler, reqTask);

            Assert.That(reqTask.ImplementationTasks, Is.Empty);
        }

        [Test]
        public async Task GroupTask_TargetDecides_SubWithSupermanagerSettingFollowsModeOfSuperManagement()
        {
            InMemoryDeviceSpecificSettingsApiConnection api = CreateApi();
            api.SetValue(kSubManagementNodeId, DeviceSpecificSettingKeys.ImplementationMode, ProvisioningImplementationMode.Manual);
            WfHandler automaticSuperHandler = CreateHandler(api);
            WfReqTask automaticSuperTask = CreateGroupTask(7, kSubManagementId);
            InMemoryDeviceSpecificSettingsApiConnection manualSuperApi = CreateApi();
            manualSuperApi.SetValue(kSuperManagementNodeId, DeviceSpecificSettingKeys.ImplementationMode, ProvisioningImplementationMode.Manual);
            WfHandler manualSuperHandler = CreateHandler(manualSuperApi);
            WfReqTask manualSuperTask = CreateGroupTask(7, kSubManagementId);

            await InvokeAutoCreateImplTasks(automaticSuperHandler, automaticSuperTask);
            await InvokeAutoCreateImplTasks(manualSuperHandler, manualSuperTask);

            Assert.Multiple(() =>
            {
                Assert.That(ManagementIds(automaticSuperTask), Is.EqualTo(kSuperManagementTask), "the mode of the source does not matter");
                Assert.That(manualSuperTask.ImplementationTasks, Is.Empty, "the mode of the target decides");
            });
        }

        [Test]
        public async Task GroupTask_SubmanagerSettingFollowsModeOfEachSubManagement()
        {
            InMemoryDeviceSpecificSettingsApiConnection api = CreateApi();
            api.SetValue(kGlobalNodeId, DeviceSpecificSettingKeys.AddressObjectCreation, ProvisioningObjectCreationMode.Submanager);
            api.SetValue(kSuperManagementNodeId, DeviceSpecificSettingKeys.ImplementationMode, ProvisioningImplementationMode.Manual);
            WfHandler handler = CreateHandler(api);
            WfReqTask reqTask = CreateGroupTask(7, kSuperManagementId);
            api.SetValue(kSubManagementNodeId, DeviceSpecificSettingKeys.ImplementationMode, ProvisioningImplementationMode.Manual);

            await InvokeAutoCreateImplTasks(handler, reqTask);

            Assert.That(ManagementIds(reqTask).Single(), Is.EqualTo(kOtherSubManagementId));
        }

        [Test]
        public async Task GroupTask_WithoutManagementGoesToEveryAutomaticManagement()
        {
            InMemoryDeviceSpecificSettingsApiConnection api = CreateApi();
            SetExternalTicketSystem(api, kExternalManagementNodeId);
            WfHandler handler = CreateHandler(api);
            WfReqTask reqTask = CreateGroupTask(7, null);

            await InvokeAutoCreateImplTasks(handler, reqTask);

            Assert.That(ManagementIds(reqTask), Is.EquivalentTo(kAutomaticManagementTasks));
        }

        [Test]
        public async Task RequestTaskOfExternalTicketSystemManagement_GetsNoTaskAndNoPathAnalysis()
        {
            InMemoryDeviceSpecificSettingsApiConnection api = CreateApi();
            SetExternalTicketSystem(api, kExternalManagementNodeId);
            WfHandler handler = CreateHandler(api);
            FixedPathAnalyzer analyzer = new(kAllGateways);
            handler.PathAnalyzerOverride = analyzer;
            WfReqTask accessTask = CreateAccessTask(8, kExternalManagementId, kNoGateway);
            WfReqTask groupTask = CreateGroupTask(7, kExternalManagementId);

            await InvokeAutoCreateImplTasks(handler, accessTask);
            await InvokeAutoCreateImplTasks(handler, groupTask);

            Assert.Multiple(() =>
            {
                Assert.That(accessTask.ImplementationTasks, Is.Empty);
                Assert.That(groupTask.ImplementationTasks, Is.Empty);
                Assert.That(analyzer.Requests, Is.Empty);
                Assert.That(AlertRaised(api), Is.False);
            });
        }

        [Test]
        public async Task RequestTaskOfExternalModeWithoutSystem_GetsNoTaskAndRaisesAlert()
        {
            InMemoryDeviceSpecificSettingsApiConnection api = CreateApi();
            SetExternalTicketSystem(api, kExternalManagementNodeId, withSystem: false);
            WfHandler handler = CreateHandler(api);
            WfReqTask reqTask = CreateAccessTask(8, kExternalManagementId, kNoGateway);

            await InvokeAutoCreateImplTasks(handler, reqTask);

            Assert.Multiple(() =>
            {
                Assert.That(reqTask.ImplementationTasks, Is.Empty);
                Assert.That(AlertRaised(api), Is.True);
            });
        }

        [Test]
        public async Task GatewayOfExternalManagement_IsLeftToTheRequestTaskOfThatManagement()
        {
            InMemoryDeviceSpecificSettingsApiConnection api = CreateApi();
            SetExternalTicketSystem(api, kExternalManagementNodeId);
            WfHandler handler = CreateHandler(api);
            WfReqTask internalTask = CreateAccessTask(8, kManagementId, kNoGateway);
            WfReqTask externalTask = CreateAccessTask(9, kExternalManagementId, kNoGateway);
            WfTicket ticket = new() { Id = 5, Tasks = [internalTask, externalTask] };

            await handler.CreateMissingImplTasksForTicket(ticket);

            Assert.Multiple(() =>
            {
                Assert.That(DeviceIds(handler.ActTicket.Tasks.Single(task => task.Id == internalTask.Id)), Is.EqualTo(kInternalGatewayTasks));
                Assert.That(handler.ActTicket.Tasks.Single(task => task.Id == externalTask.Id).ImplementationTasks, Is.Empty);
                Assert.That(AlertRaised(api), Is.False);
            });
        }

        [Test]
        public async Task GatewayOfExternalManagementWithoutRequestTask_IsSkippedWithAlert()
        {
            InMemoryDeviceSpecificSettingsApiConnection api = CreateApi();
            SetExternalTicketSystem(api, kExternalManagementNodeId);
            WfHandler handler = CreateHandler(api);
            WfTicket ticket = new() { Id = 5, Tasks = [CreateAccessTask(8, null, kNoGateway)] };

            await handler.CreateMissingImplTasksForTicket(ticket);

            Assert.Multiple(() =>
            {
                Assert.That(DeviceIds(handler.ActTicket.Tasks.Single()), Is.EqualTo(kInternalGatewayTasks));
                Assert.That(AlertRaised(api), Is.True);
            });
        }

        [Test]
        public async Task RuleDeleteTask_GatewayWithManualRuleMode_GetsNoTask()
        {
            InMemoryDeviceSpecificSettingsApiConnection api = CreateApi();
            api.SetValue(kGatewayNodeId, DeviceSpecificSettingKeys.ImplementationModeOverrideForRules, ProvisioningRuleImplementationMode.Manual);
            WfHandler handler = CreateHandler(api);
            WfReqTask manualTask = CreateRuleDeleteTask(kFirstGatewayId);
            WfReqTask automaticTask = CreateRuleDeleteTask(kSecondGatewayId);

            await InvokeAutoCreateImplTasks(handler, manualTask);
            await InvokeAutoCreateImplTasks(handler, automaticTask);

            Assert.Multiple(() =>
            {
                Assert.That(manualTask.ImplementationTasks, Is.Empty);
                Assert.That(automaticTask.ImplementationTasks, Has.Count.EqualTo(1));
            });
        }

        [Test]
        public async Task AccessTask_NoGatewayFoundWithManualGlobalRuleMode_CreatesNoFallback()
        {
            InMemoryDeviceSpecificSettingsApiConnection api = CreateApi();
            api.SetValue(kGlobalNodeId, DeviceSpecificSettingKeys.ImplementationModeOverrideForRules, ProvisioningRuleImplementationMode.Manual);
            api.SetValue(kManagementNodeId, DeviceSpecificSettingKeys.ImplementationModeOverrideForRules, ProvisioningRuleImplementationMode.Automatic);
            WfHandler handler = CreateHandler(api);
            handler.PathAnalyzerOverride = new FixedPathAnalyzer(kNoGateway);
            WfReqTask reqTask = CreateAccessTask(8, kManagementId, kNoGateway);

            await InvokeAutoCreateImplTasks(handler, reqTask);

            Assert.That(reqTask.ImplementationTasks, Is.Empty);
        }

        [Test]
        public async Task RequestTaskScope_CreatesTasksOnlyForThatRequestTask()
        {
            WfHandler handler = CreateHandler(CreateApi());
            WfReqTask approvedTask = CreateAccessTask(8, kManagementId, kFirstGateway);
            approvedTask.StateId = kApprovedStateId;
            WfReqTask otherTask = CreateAccessTask(9, kManagementId, kFirstGateway);
            WfTicket ticket = new() { Id = 5, Tasks = [approvedTask, otherTask] };
            ActionHandler actionHandler = new(CreateApi(), handler);
            handler.SetTicketEnv(ticket);

            await actionHandler.CreateImplTasks(new WfStateAction { Name = "create" }, approvedTask, WfObjectScopes.RequestTask);

            Assert.Multiple(() =>
            {
                Assert.That(DeviceIds(handler.ActTicket.Tasks.Single(task => task.Id == approvedTask.Id)), Is.EqualTo(kFirstGatewayTask));
                Assert.That(handler.ActTicket.Tasks.Single(task => task.Id == otherTask.Id).ImplementationTasks, Is.Empty);
            });
        }

        [Test]
        public async Task RequestTaskScope_BundleWaitsUntilEveryMemberReachedTheState()
        {
            WfHandler handler = CreateHandler(CreateApi(), considerBundling: true);
            WfReqTask firstMember = CreateGroupTask(7, kManagementId, kBundleId);
            WfReqTask secondMember = CreateGroupTask(8, kManagementId, kBundleId);
            WfTicket ticket = new() { Id = 5, Tasks = [firstMember, secondMember] };
            firstMember.StateId = kApprovedStateId;

            int createdWhileWaiting = await handler.CreateMissingImplTasksForTicket(ticket, firstMember);
            secondMember.StateId = kApprovedStateId;
            int createdForBundle = await handler.CreateMissingImplTasksForTicket(handler.ActTicket, secondMember);

            Assert.Multiple(() =>
            {
                Assert.That(createdWhileWaiting, Is.Zero);
                Assert.That(createdForBundle, Is.EqualTo(1), "one merged implementation task for the bundle");
            });
        }

        [TestCase(ProvisioningImplementationMode.Undefined, ProvisioningImplementationMode.Automatic)]
        [TestCase(ProvisioningImplementationMode.Automatic, ProvisioningImplementationMode.Automatic)]
        [TestCase(ProvisioningImplementationMode.Manual, ProvisioningImplementationMode.Manual)]
        [TestCase(ProvisioningImplementationMode.RerouteToExternalTicketSystem, ProvisioningImplementationMode.RerouteToExternalTicketSystem)]
        public void GetImplementationMode_ObjectsUseImplementationMode(ProvisioningImplementationMode objectMode, ProvisioningImplementationMode expected)
        {
            GlobalDeviceSpecificSettings settings = new()
            {
                ImplementationMode = objectMode,
                ImplementationModeOverrideForRules = ProvisioningRuleImplementationMode.Manual
            };

            Assert.That(settings.GetImplementationMode(false), Is.EqualTo(expected));
        }

        [TestCase(ProvisioningRuleImplementationMode.Undefined, ProvisioningImplementationMode.Automatic)]
        [TestCase(ProvisioningRuleImplementationMode.Automatic, ProvisioningImplementationMode.Automatic)]
        [TestCase(ProvisioningRuleImplementationMode.Manual, ProvisioningImplementationMode.Manual)]
        [TestCase(ProvisioningRuleImplementationMode.RerouteToExternalTicketSystem, ProvisioningImplementationMode.RerouteToExternalTicketSystem)]
        public void GetImplementationMode_RulesUseOnlyTheRuleSetting(ProvisioningRuleImplementationMode ruleMode, ProvisioningImplementationMode expected)
        {
            GlobalDeviceSpecificSettings settings = new()
            {
                ImplementationMode = ProvisioningImplementationMode.Manual,
                ImplementationModeOverrideForRules = ruleMode
            };

            Assert.Multiple(() =>
            {
                Assert.That(settings.GetImplementationMode(true), Is.EqualTo(expected));
                Assert.That(settings.UsesExternalTicketSystem(true), Is.EqualTo(expected == ProvisioningImplementationMode.RerouteToExternalTicketSystem));
            });
        }
    }
}
