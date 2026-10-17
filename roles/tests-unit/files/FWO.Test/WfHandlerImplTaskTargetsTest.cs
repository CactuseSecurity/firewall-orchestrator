using FWO.Data;
using FWO.Data.Middleware;
using FWO.Data.DeviceSpecificSettings;
using FWO.Data.Workflow;
using FWO.Middleware.Client;
using FWO.Services;
using FWO.Services.Workflow;
using NUnit.Framework;
using RestSharp;
using System.Net;
using System.Reflection;
using System.Security.Claims;

namespace FWO.Test
{
    [TestFixture]
    internal class WfHandlerImplTaskTargetsTest
    {
        private const int kSuperManagementId = 20;
        private const int kFirstSubManagementId = 21;
        private const int kSecondSubManagementId = 22;
        private const int kOtherManagementId = 40;
        private const int kMaxImplTaskTargetPages = 100; // mirrors the page bound of WfHandler
        private const int kOpenStateId = 4;
        private const int kCompletedStateId = 10;
        private const long kGlobalNodeId = 1;
        private const string kFirstSubName = "domain-a";
        private const string kSecondSubName = "domain-b";

        private static readonly List<int?> kBothSubManagements = [kFirstSubManagementId, kSecondSubManagementId];
        private static readonly List<int?> kSuperManagementOnly = [kSuperManagementId];
        private static readonly List<int?> kNoManagement = [null];
        private static readonly List<int?> kExistingAndSecondSubManagement = [null, kSecondSubManagementId];
        private static readonly List<int?> kAllGateways = [1, 2, 3];
        private static readonly List<int?> kRemainingGateways = [2, 3];
        private static readonly List<int> kOffsetsOfTwoSinglePages = [0, 1];

        private static InMemoryDeviceSpecificSettingsApiConnection CreateApi(ProvisioningObjectCreationMode globalMode)
        {
            DeviceType mdsType = new() { Id = 13, Name = "Check Point MDS", Version = "R8x" };
            DeviceType domainType = new() { Id = 9, Name = "Check Point", Version = "R8x" };
            InMemoryDeviceSpecificSettingsApiConnection api = new();
            api.Managements.Add(new Management { Id = kSuperManagementId, Name = "mds", DeviceType = mdsType, IsSupermanager = true });
            api.Managements.Add(new Management { Id = kFirstSubManagementId, Name = kFirstSubName, DeviceType = domainType, SuperManagerId = kSuperManagementId });
            api.Managements.Add(new Management { Id = kSecondSubManagementId, Name = kSecondSubName, DeviceType = domainType, SuperManagerId = kSuperManagementId });
            api.AddNode(kGlobalNodeId, DeviceSpecificScopeType.Global, DeviceSpecificScopePath.GlobalObjectKey, displayName: "Global");
            api.SetValue(kGlobalNodeId, DeviceSpecificSettingKeys.AddressObjectCreation, globalMode);
            return api;
        }

        private static WfHandler CreateMiddlewareHandler(InMemoryDeviceSpecificSettingsApiConnection api, AutoCreateImplTaskOptions option = AutoCreateImplTaskOptions.never)
        {
            return new WfHandler(new SimulatedUserConfig { ReqAutoCreateImplTasks = option }, api, WorkflowPhases.planning, null);
        }

        /// <summary>
        /// Builds a handler as the ui does: its own api connection has no access to managements or device specific
        /// settings, the target managements come from the middleware.
        /// </summary>
        private static WfHandler CreateUiHandler(MiddlewareClient middlewareClient)
        {
            return new WfHandler((_, _, _, _) => { }, new SimulatedUserConfig(), new ClaimsPrincipal(), new NoAccessApiConnection(),
                middlewareClient, WorkflowPhases.planning);
        }

        private static List<Device> CreateDevices()
        {
            Management firstSub = new() { Id = kFirstSubManagementId, SuperManagerId = kSuperManagementId };
            Management secondSub = new() { Id = kSecondSubManagementId, SuperManagerId = kSuperManagementId };
            Management other = new() { Id = kOtherManagementId };
            return
            [
                new Device { Id = 1, Name = "gw-a", Management = firstSub },
                new Device { Id = 2, Name = "gw-b", Management = secondSub },
                new Device { Id = 3, Name = "gw-other", Management = other }
            ];
        }

        private static ObjectTaskKind ToKind(ImplTaskTargetParameters parameters)
        {
            if (parameters.IsTimeObject)
            {
                return ObjectTaskKind.Time;
            }
            return parameters.IsServiceObject ? ObjectTaskKind.Service : ObjectTaskKind.Network;
        }

        private static WfReqTask CreateGroupTask(int? managementId)
        {
            return new WfReqTask { Id = 7, TaskType = WfTaskType.group_create.ToString(), StateId = kOpenStateId, Title = "New group", ManagementId = managementId };
        }

        private static WfReqTask CreateTimeObjectTask(int? managementId)
        {
            WfReqTask reqTask = new() { Id = 9, TaskType = WfTaskType.object_create.ToString(), StateId = kOpenStateId, Title = "New time", ManagementId = managementId };
            reqTask.Elements.Add(new WfReqElement { Field = ElemFieldType.time.ToString(), Name = "maintenance" });
            return reqTask;
        }

        private static WfReqTask CreateAccessTask(int? managementId)
        {
            return new WfReqTask { Id = 8, TaskType = WfTaskType.access.ToString(), StateId = kOpenStateId, Title = "Access", ManagementId = managementId };
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

        private static List<int?> ManagementIds(WfReqTask reqTask)
        {
            return [.. reqTask.ImplementationTasks.Select(implTask => implTask.ManagementId)];
        }

        private static List<int?> DeviceIds(WfReqTask reqTask)
        {
            return [.. reqTask.ImplementationTasks.Select(implTask => implTask.DeviceId)];
        }

        [Test]
        public async Task ObjectTask_SubmanagerSettingCreatesOneTaskPerSubManagement()
        {
            WfHandler handler = CreateMiddlewareHandler(CreateApi(ProvisioningObjectCreationMode.Submanager));
            WfReqTask reqTask = CreateGroupTask(kSuperManagementId);

            await InvokeAutoCreateImplTasks(handler, reqTask);

            Assert.Multiple(() =>
            {
                Assert.That(ManagementIds(reqTask), Is.EqualTo(kBothSubManagements));
                Assert.That(reqTask.ImplementationTasks[0].Title, Is.EqualTo("New group: " + kFirstSubName));
                Assert.That(reqTask.ImplementationTasks[1].Title, Is.EqualTo("New group: " + kSecondSubName));
            });
        }

        [Test]
        public async Task ObjectTask_SupermanagerSettingCreatesSingleTaskWithoutTitleSuffix()
        {
            WfHandler handler = CreateMiddlewareHandler(CreateApi(ProvisioningObjectCreationMode.Supermanager));
            WfReqTask reqTask = CreateGroupTask(kSuperManagementId);

            await InvokeAutoCreateImplTasks(handler, reqTask);

            Assert.Multiple(() =>
            {
                Assert.That(ManagementIds(reqTask), Is.EqualTo(kSuperManagementOnly));
                Assert.That(reqTask.ImplementationTasks[0].Title, Is.EqualTo("New group"));
            });
        }

        [Test]
        public async Task ObjectTask_WithoutRequestManagementKeepsSingleTaskWithoutManagement()
        {
            WfHandler handler = CreateMiddlewareHandler(CreateApi(ProvisioningObjectCreationMode.Submanager));
            WfReqTask reqTask = CreateGroupTask(null);

            await InvokeAutoCreateImplTasks(handler, reqTask);

            Assert.That(ManagementIds(reqTask), Is.EqualTo(kNoManagement));
        }

        [Test]
        public async Task ObjectTask_RerunSkipsManagementsWithExistingTasks()
        {
            WfHandler handler = CreateMiddlewareHandler(CreateApi(ProvisioningObjectCreationMode.Submanager));
            WfReqTask reqTask = CreateGroupTask(kFirstSubManagementId);
            reqTask.ImplementationTasks.Add(new WfImplTask { Id = 100, TaskNumber = 1, ManagementId = null });

            await InvokeAutoCreateImplTasks(handler, reqTask);

            Assert.That(ManagementIds(reqTask), Is.EqualTo(kExistingAndSecondSubManagement));
        }

        [Test]
        public async Task ObjectTask_WithoutMiddlewareFallsBackToRequestManagement()
        {
            WfHandler handler = new() { userConfig = new SimulatedUserConfig() };
            WfReqTask reqTask = CreateGroupTask(kSuperManagementId);

            await InvokeAutoCreateImplTasks(handler, reqTask);

            Assert.That(ManagementIds(reqTask), Is.EqualTo(kSuperManagementOnly));
        }

        [Test]
        public async Task ObjectTask_InUiResolvesTargetsThroughMiddleware()
        {
            ImplTaskTargetMiddlewareClient middlewareClient = new(CreateApi(ProvisioningObjectCreationMode.Submanager));
            WfHandler handler = CreateUiHandler(middlewareClient);
            WfReqTask reqTask = CreateGroupTask(kSuperManagementId);

            await InvokeAutoCreateImplTasks(handler, reqTask);

            Assert.Multiple(() =>
            {
                Assert.That(ManagementIds(reqTask), Is.EqualTo(kBothSubManagements));
                Assert.That(reqTask.ImplementationTasks[0].Title, Is.EqualTo("New group: " + kFirstSubName));
                Assert.That(middlewareClient.Calls.Single().ManagementId, Is.EqualTo(kSuperManagementId));
                Assert.That(middlewareClient.Calls.Single().IsServiceObject, Is.False);
            });
        }

        [Test]
        public async Task ObjectTask_InUiReadsEveryPageOfTheMiddleware()
        {
            ImplTaskTargetMiddlewareClient middlewareClient = new(CreateApi(ProvisioningObjectCreationMode.Submanager)) { PageSize = 1 };
            WfHandler handler = CreateUiHandler(middlewareClient);
            WfReqTask reqTask = CreateGroupTask(kSuperManagementId);

            await InvokeAutoCreateImplTasks(handler, reqTask);

            Assert.Multiple(() =>
            {
                Assert.That(ManagementIds(reqTask), Is.EqualTo(kBothSubManagements));
                Assert.That(middlewareClient.Calls.Select(call => call.Options.Offset), Is.EqualTo(kOffsetsOfTwoSinglePages));
            });
        }

        [Test]
        public async Task ObjectTask_InUiStopsReadingPagesWhenTheMiddlewareNeverEnds()
        {
            ImplTaskTargetMiddlewareClient middlewareClient = new(CreateApi(ProvisioningObjectCreationMode.Submanager)) { AlwaysHasMore = true };
            WfHandler handler = CreateUiHandler(middlewareClient);
            WfReqTask reqTask = CreateGroupTask(kSuperManagementId);

            await InvokeAutoCreateImplTasks(handler, reqTask);

            Assert.Multiple(() =>
            {
                Assert.That(ManagementIds(reqTask), Is.EqualTo(kSuperManagementOnly));
                Assert.That(middlewareClient.Calls, Has.Count.EqualTo(kMaxImplTaskTargetPages));
            });
        }

        [Test]
        public async Task ObjectTask_ServiceObjectsUseServiceSetting()
        {
            ImplTaskTargetMiddlewareClient middlewareClient = new(CreateApi(ProvisioningObjectCreationMode.Submanager));
            WfHandler handler = CreateUiHandler(middlewareClient);
            WfReqTask reqTask = CreateGroupTask(kSuperManagementId);
            reqTask.Elements.Add(new WfReqElement { Field = ElemFieldType.service.ToString() });

            await InvokeAutoCreateImplTasks(handler, reqTask);

            Assert.Multiple(() =>
            {
                Assert.That(ManagementIds(reqTask), Is.EqualTo(kSuperManagementOnly));
                Assert.That(middlewareClient.Calls.Single().IsServiceObject, Is.True);
            });
        }

        [Test]
        public async Task ObjectTask_TimeObjectsUseTimeSetting()
        {
            InMemoryDeviceSpecificSettingsApiConnection api = CreateApi(ProvisioningObjectCreationMode.Supermanager);
            api.SetValue(kGlobalNodeId, DeviceSpecificSettingKeys.TimeObjectCreation, ProvisioningObjectCreationMode.Submanager);
            ImplTaskTargetMiddlewareClient middlewareClient = new(api);
            WfHandler handler = CreateUiHandler(middlewareClient);
            WfReqTask reqTask = new() { Id = 9, TaskType = WfTaskType.object_create.ToString(), StateId = kOpenStateId, Title = "New time", ManagementId = kSuperManagementId };
            reqTask.Elements.Add(new WfReqElement { Field = ElemFieldType.time.ToString(), Name = "maintenance" });

            await InvokeAutoCreateImplTasks(handler, reqTask);

            Assert.Multiple(() =>
            {
                Assert.That(ManagementIds(reqTask), Is.EqualTo(kBothSubManagements));
                Assert.That(middlewareClient.Calls.Single().IsTimeObject, Is.True);
                Assert.That(middlewareClient.Calls.Single().IsServiceObject, Is.False);
            });
        }

        [Test]
        public async Task ObjectTask_IgnoredTimeObjectsGetNoImplTask()
        {
            InMemoryDeviceSpecificSettingsApiConnection api = CreateApi(ProvisioningObjectCreationMode.Submanager);
            api.SetValue(kGlobalNodeId, DeviceSpecificSettingKeys.TimeObjectCreation, ProvisioningObjectCreationMode.Ignore);
            WfHandler handler = CreateUiHandler(new ImplTaskTargetMiddlewareClient(api));
            WfReqTask reqTask = CreateTimeObjectTask(kSuperManagementId);

            await InvokeAutoCreateImplTasks(handler, reqTask);

            Assert.That(reqTask.ImplementationTasks, Is.Empty);
        }

        [Test]
        public async Task HasObjectTargets_IsFalseOnlyForIgnoredTimeObjects()
        {
            InMemoryDeviceSpecificSettingsApiConnection api = CreateApi(ProvisioningObjectCreationMode.Submanager);
            api.SetValue(kGlobalNodeId, DeviceSpecificSettingKeys.TimeObjectCreation, ProvisioningObjectCreationMode.Ignore);
            WfHandler handler = CreateUiHandler(new ImplTaskTargetMiddlewareClient(api));

            bool? timeObject = await handler.HasObjectTargets(CreateTimeObjectTask(kSuperManagementId));
            bool? group = await handler.HasObjectTargets(CreateGroupTask(kSuperManagementId));
            bool? allManagements = await handler.HasObjectTargets(CreateTimeObjectTask(null));

            Assert.Multiple(() =>
            {
                Assert.That(timeObject, Is.False);
                Assert.That(group, Is.True);
                Assert.That(allManagements, Is.True);
            });
        }

        [Test]
        public async Task HasObjectTargets_IsTrueWhenTimeObjectsAreCreated()
        {
            InMemoryDeviceSpecificSettingsApiConnection api = CreateApi(ProvisioningObjectCreationMode.Supermanager);
            WfHandler handler = CreateUiHandler(new ImplTaskTargetMiddlewareClient(api));

            Assert.That(await handler.HasObjectTargets(CreateTimeObjectTask(kSuperManagementId)), Is.True);
        }

        [Test]
        public async Task HasObjectTargets_IsUnknownWhenTheTargetsCannotBeResolved()
        {
            ImplTaskTargetMiddlewareClient failingClient = new(CreateApi(ProvisioningObjectCreationMode.Submanager)) { Fail = true };
            WfHandler uiHandler = CreateUiHandler(failingClient);
            WfHandler handlerWithoutBackend = new();

            bool? failed = await uiHandler.HasObjectTargets(CreateTimeObjectTask(kSuperManagementId));
            bool? withoutBackend = await handlerWithoutBackend.HasObjectTargets(CreateTimeObjectTask(kSuperManagementId));

            Assert.Multiple(() =>
            {
                Assert.That(failed, Is.Null);
                Assert.That(withoutBackend, Is.Null);
            });
        }

        [Test]
        public async Task ObjectTask_FallsBackToRequestManagementWhenMiddlewareFails()
        {
            ImplTaskTargetMiddlewareClient middlewareClient = new(CreateApi(ProvisioningObjectCreationMode.Submanager)) { Fail = true };
            WfHandler handler = CreateUiHandler(middlewareClient);
            WfReqTask reqTask = CreateGroupTask(kSuperManagementId);

            await InvokeAutoCreateImplTasks(handler, reqTask);

            Assert.That(ManagementIds(reqTask), Is.EqualTo(kSuperManagementOnly));
        }

        [Test]
        public async Task ObjectTask_FallsBackToRequestManagementWhenResolutionFails()
        {
            InMemoryDeviceSpecificSettingsApiConnection api = CreateApi(ProvisioningObjectCreationMode.Submanager);
            api.FailingQuery = FWO.Api.Client.Queries.DeviceQueries.getManagementHierarchy;
            WfHandler handler = CreateMiddlewareHandler(api);
            WfReqTask reqTask = CreateGroupTask(kSuperManagementId);

            await InvokeAutoCreateImplTasks(handler, reqTask);

            Assert.That(ManagementIds(reqTask), Is.EqualTo(kSuperManagementOnly));
        }

        [Test]
        public async Task CreateMissingImplTasksForTicket_ResolvesTargetsOncePerRunAndObjectKind()
        {
            ImplTaskTargetMiddlewareClient middlewareClient = new(CreateApi(ProvisioningObjectCreationMode.Submanager));
            WfHandler handler = CreateUiHandler(middlewareClient);
            SetMatrix(handler, WfTaskType.group_create.ToString(), new StateMatrix { MinTicketCompleted = kCompletedStateId });
            WfReqTask firstGroupTask = CreateGroupTask(kSuperManagementId);
            WfReqTask secondGroupTask = CreateGroupTask(kSuperManagementId);
            secondGroupTask.Id = 12;
            WfReqTask serviceGroupTask = CreateGroupTask(kSuperManagementId);
            serviceGroupTask.Id = 13;
            serviceGroupTask.Elements.Add(new WfReqElement { Field = ElemFieldType.service.ToString() });
            WfTicket ticket = new() { Id = 5, Tasks = [firstGroupTask, secondGroupTask, serviceGroupTask] };

            int createdCount = await handler.CreateMissingImplTasksForTicket(ticket);
            int callsAfterFirstRun = middlewareClient.Calls.Count;
            await handler.CreateMissingImplTasksForTicket(ticket);

            Assert.Multiple(() =>
            {
                Assert.That(createdCount, Is.EqualTo(kBothSubManagements.Count * 2 + kSuperManagementOnly.Count));
                Assert.That(callsAfterFirstRun, Is.EqualTo(2), "one resolution for network objects, one for services");
                Assert.That(middlewareClient.Calls, Has.Count.EqualTo(4), "a new run resolves again");
            });
        }

        [Test]
        public async Task AccessTask_RerunSkipsGatewaysWithExistingTasks()
        {
            WfHandler handler = CreateMiddlewareHandler(CreateApi(ProvisioningObjectCreationMode.Supermanager), AutoCreateImplTaskOptions.forEachDevice);
            handler.Devices = CreateDevices();
            WfReqTask reqTask = CreateAccessTask(kSuperManagementId);
            reqTask.ImplementationTasks.Add(new WfImplTask { Id = 100, TaskNumber = 1, DeviceId = 1 });

            await InvokeAutoCreateImplTasks(handler, reqTask);

            Assert.That(DeviceIds(reqTask).Skip(1), Is.EqualTo(kRemainingGateways));
        }

        [Test]
        public async Task CreateMissingImplTasksForTicket_CreatesTasksOnlyForOpenRequestTasks()
        {
            WfHandler handler = CreateMiddlewareHandler(CreateApi(ProvisioningObjectCreationMode.Submanager));
            SetMatrix(handler, WfTaskType.group_create.ToString(), new StateMatrix { MinTicketCompleted = kCompletedStateId });
            WfReqTask openTask = CreateGroupTask(kSuperManagementId);
            WfReqTask completedTask = CreateGroupTask(kSuperManagementId);
            completedTask.Id = 9;
            completedTask.StateId = kCompletedStateId;
            WfTicket ticket = new() { Id = 5, Tasks = [openTask, completedTask] };

            int firstRunCount = await handler.CreateMissingImplTasksForTicket(ticket);
            int secondRunCount = await handler.CreateMissingImplTasksForTicket(ticket);

            Assert.Multiple(() =>
            {
                Assert.That(firstRunCount, Is.EqualTo(kBothSubManagements.Count));
                Assert.That(secondRunCount, Is.Zero);
                Assert.That(handler.ActTicket.Tasks.Single(task => task.Id == completedTask.Id).ImplementationTasks, Is.Empty);
            });
        }

        [Test]
        public async Task CreateImplTasksAction_CreatesTasksForTheTicket()
        {
            InMemoryDeviceSpecificSettingsApiConnection api = CreateApi(ProvisioningObjectCreationMode.Submanager);
            WfHandler handler = CreateMiddlewareHandler(api);
            SetMatrix(handler, WfTaskType.group_create.ToString(), new StateMatrix { MinTicketCompleted = kCompletedStateId });
            WfTicket ticket = new() { Id = 5, Tasks = [CreateGroupTask(kSuperManagementId)] };
            ActionHandler actionHandler = new(api, handler);

            await actionHandler.CreateImplTasks(new WfStateAction { Name = "create" }, ticket, WfObjectScopes.Ticket);

            Assert.That(handler.ActTicket.Tasks.Single().ImplementationTasks, Has.Count.EqualTo(kBothSubManagements.Count));
        }

        [Test]
        public async Task CreateMissingImplTasksForTicket_DoesNotDuplicateGenericTasksOnRerun()
        {
            WfHandler handler = CreateMiddlewareHandler(CreateApi(ProvisioningObjectCreationMode.Supermanager));
            SetMatrix(handler, WfTaskType.rule_delete.ToString(), new StateMatrix { MinTicketCompleted = kCompletedStateId });
            WfReqTask ruleDeleteTask = new() { Id = 11, TaskType = WfTaskType.rule_delete.ToString(), StateId = kOpenStateId, Title = "Delete rule" };
            WfTicket ticket = new() { Id = 5, Tasks = [ruleDeleteTask] };

            int firstRunCount = await handler.CreateMissingImplTasksForTicket(ticket);
            int secondRunCount = await handler.CreateMissingImplTasksForTicket(ticket);

            Assert.Multiple(() =>
            {
                Assert.That(firstRunCount, Is.EqualTo(1));
                Assert.That(secondRunCount, Is.Zero);
                Assert.That(handler.ActTicket.Tasks.Single().ImplementationTasks, Has.Count.EqualTo(1));
            });
        }

        [Test]
        public async Task CreateImplTasksAction_SkipsWhenNoTicketCanBeResolved()
        {
            InMemoryDeviceSpecificSettingsApiConnection api = CreateApi(ProvisioningObjectCreationMode.Submanager);
            WfHandler handler = CreateMiddlewareHandler(api);
            ActionHandler actionHandler = new(api, handler);
            int callsBefore = api.Calls.Count;

            await actionHandler.CreateImplTasks(new WfStateAction { Name = "create" }, new WfReqTask(), WfObjectScopes.RequestTask);

            Assert.That(api.Calls, Has.Count.EqualTo(callsBefore));
        }

        [Test]
        public async Task CreateImplTasksAction_IsDispatchedByPerformAction()
        {
            InMemoryDeviceSpecificSettingsApiConnection api = CreateApi(ProvisioningObjectCreationMode.Supermanager);
            WfHandler handler = CreateMiddlewareHandler(api);
            SetMatrix(handler, WfTaskType.group_create.ToString(), new StateMatrix { MinTicketCompleted = kCompletedStateId });
            WfTicket ticket = new() { Id = 5, Tasks = [CreateGroupTask(kSuperManagementId)] };
            ActionHandler actionHandler = new(api, handler);
            WfStateAction action = new() { Name = "create", ActionType = nameof(StateActionTypes.CreateImplTasks) };

            await actionHandler.PerformAction(action, ticket, WfObjectScopes.Ticket);

            Assert.That(ManagementIds(handler.ActTicket.Tasks.Single()), Is.EqualTo(kSuperManagementOnly));
        }

        [Test]
        public async Task AccessTask_AfterPathAnalysisCreatesTaskForEveryFoundGateway()
        {
            PathAnalysisApiConnection api = new(CreateDevices());
            WfHandler handler = new(new SimulatedUserConfig { ReqAutoCreateImplTasks = AutoCreateImplTaskOptions.afterPathAnalysis },
                api, WorkflowPhases.planning, null)
            {
                Devices = CreateDevices()
            };
            WfReqTask reqTask = CreateAccessTask(kSuperManagementId);
            reqTask.Elements.Add(new WfReqElement { Field = ElemFieldType.source.ToString(), Cidr = new Cidr("10.0.0.1/32") });
            reqTask.Elements.Add(new WfReqElement { Field = ElemFieldType.destination.ToString(), Cidr = new Cidr("10.0.1.1/32") });

            await InvokeAutoCreateImplTasks(handler, reqTask);

            Assert.Multiple(() =>
            {
                Assert.That(DeviceIds(reqTask), Is.EqualTo(kAllGateways));
                Assert.That(reqTask.ImplementationTasks[0].Title, Is.EqualTo("Access: gw-a"));
            });
        }

        /// <summary>
        /// Plays the middleware endpoint getImplTaskTargets: resolves with the middleware's view of the
        /// managements and records every call.
        /// </summary>
        private sealed class ImplTaskTargetMiddlewareClient(InMemoryDeviceSpecificSettingsApiConnection middlewareApi) : MiddlewareClient("http://localhost/")
        {
            public List<ImplTaskTargetParameters> Calls { get; } = [];
            public bool Fail { get; init; }
            public int PageSize { get; init; } = int.MaxValue;
            public bool AlwaysHasMore { get; init; }

            public override async Task<RestResponse<ImplTaskTargetResult>> GetImplTaskTargets(ImplTaskTargetParameters parameters)
            {
                Calls.Add(parameters);
                if (Fail)
                {
                    return new RestResponse<ImplTaskTargetResult>(new RestRequest())
                    {
                        StatusCode = HttpStatusCode.InternalServerError,
                        ResponseStatus = ResponseStatus.Completed,
                        IsSuccessStatusCode = false
                    };
                }
                List<Management> targets = await new ImplTaskManagementResolver(middlewareApi)
                    .ResolveObjectTargets(parameters.ManagementId, ToKind(parameters));
                List<Management> page = [.. targets.Skip(parameters.Options.Offset).Take(PageSize)];
                bool hasMore = AlwaysHasMore || parameters.Options.Offset + page.Count < targets.Count;
                List<HeaderParameter> headers = [new HeaderParameter(ImplTaskTargetPaging.kHasMoreHeader, hasMore ? "true" : "false")];
                return new RestResponse<ImplTaskTargetResult>(new RestRequest())
                {
                    StatusCode = HttpStatusCode.OK,
                    ResponseStatus = ResponseStatus.Completed,
                    IsSuccessStatusCode = true,
                    Headers = headers,
                    Data = new ImplTaskTargetResult { Targets = AlwaysHasMore ? targets : page }
                };
            }
        }

        /// <summary>
        /// Api connection of a ui user who may neither read managements nor device specific settings.
        /// </summary>
        private sealed class NoAccessApiConnection : SimulatedApiConnection
        {
            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null,
                FWO.Api.Client.QueryChunkingOptions? chunkingOptions = null)
            {
                throw new InvalidOperationException("The ui user has no access to " + query);
            }
        }

        private sealed class PathAnalysisApiConnection(List<Device> foundDevices) : SimulatedApiConnection
        {
            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null,
                FWO.Api.Client.QueryChunkingOptions? chunkingOptions = null)
            {
                if (query == FWO.Api.Client.Queries.NetworkAnalysisQueries.pathAnalysis)
                {
                    return Task.FromResult((QueryResponseType)(object)foundDevices);
                }
                throw new NotImplementedException();
            }
        }
    }
}
