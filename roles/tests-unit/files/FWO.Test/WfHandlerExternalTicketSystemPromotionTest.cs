using FWO.Api.Client.Queries;
using FWO.Data;
using FWO.Data.DeviceSpecificSettings;
using FWO.Data.Workflow;
using FWO.Services.Workflow;
using NUnit.Framework;
using System.Reflection;

namespace FWO.Test
{
    /// <summary>
    /// Tests how the action "Create implementation tasks" promotes the request tasks handed to an external ticket system to
    /// its target state, so that they do not hold the ticket in the planning.
    /// </summary>
    [TestFixture]
    internal class WfHandlerExternalTicketSystemPromotionTest
    {
        private const long kGlobalNodeId = 1;
        private const long kDeviceTypeNodeId = 2;
        private const long kAutomaticManagementNodeId = 3;
        private const long kExternalManagementNodeId = 4;
        private const int kDeviceTypeId = 9;
        private const int kAutomaticManagementId = 20;
        private const int kExternalManagementId = 30;
        private const long kTicketId = 5;
        private const int kInApprovalStateId = 60;
        private const int kApprovedStateId = 99;
        private const int kInPlanningStateId = 110;
        private const int kPlannedStateId = 149;
        private const int kPlanVerifiedStateId = 199;
        private const int kToImplementStateId = 200;
        private const int kInImplementationStateId = 210;
        private const int kImplementedStateId = 249;
        private const int kRejectedStateId = 610;
        private const int kCompletedStateId = 299;

        private static readonly List<int> kNoGateway = [];
        private static readonly List<(long Id, int StateId)> kFirstTaskToImplement = [(7, kToImplementStateId)];
        private static readonly List<long> kOnlyScopeTask = [7];
        private static readonly List<long> kExternalTasksInOrder = [7, 8];

        private static InMemoryDeviceSpecificSettingsApiConnection CreateApi(bool withSystem = true)
        {
            InMemoryDeviceSpecificSettingsApiConnection api = new();
            DeviceType deviceType = new() { Id = kDeviceTypeId, Name = "Check Point", Version = "R8x" };
            api.Managements.Add(new Management { Id = kAutomaticManagementId, Name = "cp1", DeviceType = deviceType });
            api.Managements.Add(new Management { Id = kExternalManagementId, Name = "cp2", DeviceType = deviceType });
            api.AddNode(kGlobalNodeId, DeviceSpecificScopeType.Global, DeviceSpecificScopePath.GlobalObjectKey, displayName: "Global");
            api.AddNode(kDeviceTypeNodeId, DeviceSpecificScopeType.DeviceType, kDeviceTypeId.ToString(), kGlobalNodeId);
            api.AddNode(kAutomaticManagementNodeId, DeviceSpecificScopeType.Management, kAutomaticManagementId.ToString(), kDeviceTypeNodeId);
            api.AddNode(kExternalManagementNodeId, DeviceSpecificScopeType.Management, kExternalManagementId.ToString(), kDeviceTypeNodeId);
            api.SetValue(kExternalManagementNodeId, DeviceSpecificSettingKeys.ImplementationModeForRules, ProvisioningRuleImplementationMode.RerouteToExternalTicketSystem);
            api.SetValue(kExternalManagementNodeId, DeviceSpecificSettingKeys.ImplementationMode, ProvisioningImplementationMode.RerouteToExternalTicketSystem);
            if (withSystem)
            {
                api.SetValue(kExternalManagementNodeId, DeviceSpecificSettingKeys.RerouteToExternalTicketSystem, ExternalTicketSystemKind.TufinSecureChange);
            }
            return api;
        }

        private static WfHandler CreateHandler(InMemoryDeviceSpecificSettingsApiConnection api, List<(long Id, int StateId)> promotions)
        {
            WfHandler handler = new(new SimulatedUserConfig(), api, WorkflowPhases.planning, null)
            {
                PathAnalyzerOverride = new FixedPathAnalyzer(kNoGateway),
                ReqTaskPromotionOverride = (reqTask, stateId) =>
                {
                    promotions.Add((reqTask.Id, stateId));
                    return Task.CompletedTask;
                }
            };
            SetMatrix(handler, WfTaskType.access.ToString());
            SetMatrix(handler, WfTaskType.group_create.ToString());
            return handler;
        }

        /// <summary>Matrix with approval, active planning and the implementation starting at its end, in the default numbering.</summary>
        private static void SetMatrix(WfHandler handler, string taskType)
        {
            FieldInfo field = typeof(WfHandler).GetField("stateMatrixDict", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingFieldException(nameof(WfHandler), "stateMatrixDict");
            ((StateMatrixDict)field.GetValue(handler)!).Matrices[taskType] = new StateMatrix
            {
                MinTicketCompleted = kCompletedStateId,
                ApprovalLowestEndState = kApprovedStateId,
                PlanningLowestEndState = kPlannedStateId,
                MinImplTasksNeeded = kPlannedStateId,
                ImplementationLowestEndState = kImplementedStateId
            };
        }

        private static WfReqTask CreateAccessTask(long id, int managementId, int stateId, int taskNumber = 1)
        {
            return new() { Id = id, TicketId = kTicketId, TaskNumber = taskNumber, TaskType = WfTaskType.access.ToString(), StateId = stateId, Title = "Access", ManagementId = managementId };
        }

        private static WfReqTask CreateGroupTask(long id, int managementId, int stateId)
        {
            return new() { Id = id, TicketId = kTicketId, TaskType = WfTaskType.group_create.ToString(), StateId = stateId, Title = "Group", ManagementId = managementId };
        }

        private static WfTicket CreateTicket(params WfReqTask[] reqTasks)
        {
            return new() { Id = kTicketId, Tasks = [.. reqTasks] };
        }

        [Test]
        public async Task ApprovedTaskOfExternalTicketSystem_IsPromotedToTheTargetState()
        {
            List<(long Id, int StateId)> promotions = [];
            WfHandler handler = CreateHandler(CreateApi(), promotions);
            WfReqTask externalTask = CreateAccessTask(7, kExternalManagementId, kApprovedStateId);

            int promoted = await handler.PromoteRequestTasksOfExternalTicketSystem(CreateTicket(externalTask), null, kToImplementStateId);

            Assert.Multiple(() =>
            {
                Assert.That(promoted, Is.EqualTo(1));
                Assert.That(promotions, Is.EqualTo(kFirstTaskToImplement));
            });
        }

        [Test]
        public async Task ObjectTaskOfExternalTicketSystem_IsPromoted()
        {
            List<(long Id, int StateId)> promotions = [];
            WfHandler handler = CreateHandler(CreateApi(), promotions);

            int promoted = await handler.PromoteRequestTasksOfExternalTicketSystem(
                CreateTicket(CreateGroupTask(7, kExternalManagementId, kApprovedStateId)), null, kToImplementStateId);

            Assert.That(promoted, Is.EqualTo(1));
        }

        [Test]
        public async Task TaskOfAutomaticManagement_IsNotPromoted()
        {
            List<(long Id, int StateId)> promotions = [];
            WfHandler handler = CreateHandler(CreateApi(), promotions);

            int promoted = await handler.PromoteRequestTasksOfExternalTicketSystem(
                CreateTicket(CreateAccessTask(7, kAutomaticManagementId, kApprovedStateId)), null, kToImplementStateId);

            Assert.That(promoted, Is.Zero);
        }

        [Test]
        public async Task TaskOfExternalModeWithoutSystem_IsNotPromotedAndRaisesNoAlert()
        {
            List<(long Id, int StateId)> promotions = [];
            InMemoryDeviceSpecificSettingsApiConnection api = CreateApi(withSystem: false);
            WfHandler handler = CreateHandler(api, promotions);

            int promoted = await handler.PromoteRequestTasksOfExternalTicketSystem(
                CreateTicket(CreateAccessTask(7, kExternalManagementId, kApprovedStateId)), null, kToImplementStateId);

            Assert.Multiple(() =>
            {
                Assert.That(promoted, Is.Zero);
                Assert.That(api.Calls.Any(call => call.Query == MonitorQueries.addAlert), Is.False);
            });
        }

        [TestCase(kInApprovalStateId)]
        [TestCase(kToImplementStateId)]
        [TestCase(kInImplementationStateId)]
        [TestCase(kRejectedStateId)]
        public async Task TaskNotApprovedOrAlreadyAtOrBeyondTheTarget_IsNotPromoted(int stateId)
        {
            List<(long Id, int StateId)> promotions = [];
            WfHandler handler = CreateHandler(CreateApi(), promotions);

            int promoted = await handler.PromoteRequestTasksOfExternalTicketSystem(
                CreateTicket(CreateAccessTask(7, kExternalManagementId, stateId)), null, kToImplementStateId);

            Assert.That(promoted, Is.Zero);
        }

        [Test]
        public async Task TaskInPlanning_IsPromoted()
        {
            List<(long Id, int StateId)> promotions = [];
            WfHandler handler = CreateHandler(CreateApi(), promotions);

            int promoted = await handler.PromoteRequestTasksOfExternalTicketSystem(
                CreateTicket(CreateAccessTask(7, kExternalManagementId, kInPlanningStateId)), null, kPlanVerifiedStateId);

            Assert.That(promoted, Is.EqualTo(1));
        }

        [Test]
        public async Task WithoutTarget_PromotesToTheLowestInputStateOfTheImplementation()
        {
            List<(long Id, int StateId)> promotions = [];
            WfHandler handler = CreateHandler(CreateApi(), promotions);

            int promoted = await handler.PromoteRequestTasksOfExternalTicketSystem(
                CreateTicket(CreateAccessTask(7, kExternalManagementId, kApprovedStateId)), null, null);

            Assert.Multiple(() =>
            {
                Assert.That(promoted, Is.EqualTo(1));
                Assert.That(promotions.Single(), Is.EqualTo((7L, kPlannedStateId)));
            });
        }

        [Test]
        public async Task WithoutTargetAndAlreadyAtTheLowestInputStateOfTheImplementation_PromotesNothing()
        {
            List<(long Id, int StateId)> promotions = [];
            WfHandler handler = CreateHandler(CreateApi(), promotions);

            int promoted = await handler.PromoteRequestTasksOfExternalTicketSystem(
                CreateTicket(CreateAccessTask(7, kExternalManagementId, kPlannedStateId)), null, null);

            Assert.That(promoted, Is.Zero);
        }

        [Test]
        public async Task TargetBelowTheLowestInputStateOfTheImplementation_PromotesNothing()
        {
            List<(long Id, int StateId)> promotions = [];
            WfHandler handler = CreateHandler(CreateApi(), promotions);

            int promoted = await handler.PromoteRequestTasksOfExternalTicketSystem(
                CreateTicket(CreateAccessTask(7, kExternalManagementId, kApprovedStateId)), null, kInPlanningStateId);

            Assert.That(promoted, Is.Zero);
        }

        [Test]
        public async Task RequestTaskScope_PromotesOnlyThatTask()
        {
            List<(long Id, int StateId)> promotions = [];
            WfHandler handler = CreateHandler(CreateApi(), promotions);
            WfReqTask scopeTask = CreateAccessTask(7, kExternalManagementId, kApprovedStateId, 1);
            WfReqTask otherTask = CreateAccessTask(8, kExternalManagementId, kApprovedStateId, 2);

            await handler.PromoteRequestTasksOfExternalTicketSystem(CreateTicket(scopeTask, otherTask), scopeTask, kToImplementStateId);

            Assert.That(promotions.Select(promotion => promotion.Id), Is.EqualTo(kOnlyScopeTask));
        }

        [Test]
        public async Task TicketScope_PromotesInTaskNumberOrder()
        {
            List<(long Id, int StateId)> promotions = [];
            WfHandler handler = CreateHandler(CreateApi(), promotions);
            WfReqTask secondTask = CreateAccessTask(8, kExternalManagementId, kApprovedStateId, 2);
            WfReqTask firstTask = CreateAccessTask(7, kExternalManagementId, kApprovedStateId, 1);
            WfReqTask automaticTask = CreateAccessTask(9, kAutomaticManagementId, kApprovedStateId, 3);

            await handler.PromoteRequestTasksOfExternalTicketSystem(CreateTicket(secondTask, firstTask, automaticTask), null, kToImplementStateId);

            Assert.That(promotions.Select(promotion => promotion.Id), Is.EqualTo(kExternalTasksInOrder));
        }

        [TestCase("", kPlannedStateId)]
        [TestCase("{\"external_ticket_system_target_state_id\":199}", kPlanVerifiedStateId)]
        public async Task Action_CreatesTasksAndPromotesToItsTargetState(string externalParams, int expectedStateId)
        {
            List<(long Id, int StateId)> promotions = [];
            InMemoryDeviceSpecificSettingsApiConnection api = CreateApi();
            WfHandler handler = CreateHandler(api, promotions);
            WfReqTask externalTask = CreateAccessTask(7, kExternalManagementId, kApprovedStateId);
            WfTicket ticket = CreateTicket(externalTask);
            handler.SetTicketEnv(ticket);
            ActionHandler actionHandler = new(api, handler);

            await actionHandler.CreateImplTasks(new WfStateAction { Name = "prepare", ExternalParams = externalParams }, ticket, WfObjectScopes.Ticket);

            Assert.Multiple(() =>
            {
                Assert.That(promotions.Single(), Is.EqualTo((7L, expectedStateId)));
                Assert.That(handler.ActTicket.Tasks.Single().ImplementationTasks, Is.Empty);
            });
        }
    }
}
