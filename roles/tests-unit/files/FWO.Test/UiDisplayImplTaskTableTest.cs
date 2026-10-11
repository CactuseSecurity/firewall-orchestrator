using Bunit;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Workflow;
using FWO.Middleware.Client;
using FWO.Services.Workflow;
using FWO.Ui.Pages.Request;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Reflection;
using static FWO.Test.UiRequestCoverageTest;

namespace FWO.Test
{
    [TestFixture]
    internal class UiDisplayImplTaskTableTest
    {
        /// <summary>
        /// Verifies that the implementation table resolves the owner through its request task.
        /// </summary>
        [Test]
        public void GetOwnerName_UsesLinkedRequestTaskOwner()
        {
            DisplayImplTaskTable component = CreateComponent();
            SetPrivateField(component, "reqTasksById", new Dictionary<long, WfReqTask>
            {
                { 12, new WfReqTask { Id = 12, Owners = new List<FwoOwnerDataHelper>
                    { new FwoOwnerDataHelper { Owner = new FwoOwner { Id = 7, Name = "Application A" } } } } }
            });

            WfImplTask implementationTask = new() { ReqTaskId = 12 };

            Assert.That(InvokePrivate<string>(component, "GetOwnerName", implementationTask), Is.EqualTo("Application A"));
        }

        /// <summary>
        /// Verifies that an implementation task without a resolvable request task has no owner display value.
        /// </summary>
        [Test]
        public void GetOwnerName_ReturnsEmpty_WhenRequestTaskCannotBeResolved()
        {
            DisplayImplTaskTable component = CreateComponent();
            SetPrivateField(component, "reqTasksById", new Dictionary<long, WfReqTask>());

            Assert.That(InvokePrivate<string>(component, "GetOwnerName", new WfImplTask { ReqTaskId = 99 }), Is.Empty);
        }

        /// <summary>
        /// Verifies that a concrete device is displayed by its configured name.
        /// </summary>
        [Test]
        public void GetDeviceName_UsesConfiguredDeviceName()
        {
            DisplayImplTaskTable component = CreateComponent();
            component.WfHandler.Devices = new List<Device> { new Device { Id = 4, Name = "Gateway A" } };

            Assert.That(InvokePrivate<string>(component, "GetDeviceName", new WfImplTask { DeviceId = 4 }), Is.EqualTo("Gateway A"));
        }

        /// <summary>
        /// Verifies that an access implementation task targeting all devices uses the localized all label.
        /// </summary>
        [Test]
        public void GetDeviceName_UsesAllLabel_ForAllDeviceAccessTask()
        {
            DisplayImplTaskTable component = CreateComponent();
            SimulatedUserConfig userConfig = new();
            SetPrivateField(component, "userConfig", userConfig);
            WfReqTask requestTask = new() { Id = 12, TaskType = WfTaskType.access.ToString() };
            requestTask.SetDeviceList(new List<int> { WfReqTaskBase.kAllDevicesId });
            SetPrivateField(component, "reqTasksById", new Dictionary<long, WfReqTask> { { 12, requestTask } });

            WfImplTask implementationTask = new() { ReqTaskId = 12, TaskType = WfTaskType.access.ToString() };

            Assert.That(InvokePrivate<string>(component, "GetDeviceName", implementationTask), Is.EqualTo(userConfig.GetText("all")));
        }

        /// <summary>
        /// Verifies that unresolved implementation-task references produce safe display values.
        /// </summary>
        [Test]
        public void ResolveAndDisplayHelpers_HandleMissingReferences()
        {
            DisplayImplTaskTable component = CreateComponent();
            SetPrivateField(component, "userConfig", new SimulatedUserConfig());

            WfImplTask implementationTask = new()
            {
                TaskType = WfTaskType.access.ToString(),
                TicketId = 99,
                ReqTaskId = 77,
                DeviceId = 123
            };

            Assert.Multiple(() =>
            {
                Assert.That(InvokePrivateObject(component, "ResolveTicket", implementationTask), Is.Null);
                Assert.That(InvokePrivate<string>(component, "GetOwnerName", implementationTask), Is.Empty);
                Assert.That(InvokePrivate<string>(component, "GetDeviceName", implementationTask), Is.Empty);
                Assert.That(InvokePrivate<bool>(component, "IsAllDevicesImplTask", implementationTask), Is.False);
            });
        }

        /// <summary>
        /// Verifies that implementation actions include the input state and exclude the end state.
        /// </summary>
        [Test]
        public void CanActOnImplTaskInPhase_UsesInclusiveInputAndExclusiveEndBounds()
        {
            DisplayImplTaskTable component = CreateComponent();
            WfHandler handler = new();
            string taskType = WfTaskType.access.ToString();
            SetMatrix(handler, taskType, CreateMatrix(10, 12, 20));
            SetPrivateField(component, nameof(DisplayImplTaskTable.WfHandler), handler);

            Assert.Multiple(() =>
            {
                Assert.That(InvokePrivate<bool>(component, "CanActOnImplTaskInPhase", new WfImplTask { TaskType = taskType, StateId = 9 }), Is.False);
                Assert.That(InvokePrivate<bool>(component, "CanActOnImplTaskInPhase", new WfImplTask { TaskType = taskType, StateId = 10 }), Is.True);
                Assert.That(InvokePrivate<bool>(component, "CanActOnImplTaskInPhase", new WfImplTask { TaskType = taskType, StateId = 19 }), Is.True);
                Assert.That(InvokePrivate<bool>(component, "CanActOnImplTaskInPhase", new WfImplTask { TaskType = taskType, StateId = 20 }), Is.False);
            });
        }

        /// <summary>
        /// Verifies that owner-based implementation editability follows the linked ticket flag.
        /// </summary>
        [Test]
        public void IsEditable_UsesTicketFlagForOwnerBasedRequests()
        {
            DisplayImplTaskTable component = CreateComponent();
            SimulatedUserConfig userConfig = new() { ReqOwnerBased = true };
            WfReqTask requestTask = new() { Id = 20 };
            WfTicket ticket = new() { Id = 10, Editable = false, Tasks = new List<WfReqTask> { requestTask } };
            SetPrivateField(component, "userConfig", userConfig);
            SetPrivateField(component, nameof(DisplayImplTaskTable.WfHandler), new WfHandler());
            SetPrivateField(component, "ticketsById", new Dictionary<long, WfTicket> { { ticket.Id, ticket } });
            WfImplTask implementationTask = new() { TicketId = 10, ReqTaskId = 20 };

            Assert.That(InvokePrivate<bool>(component, "IsEditable", implementationTask), Is.False);

            userConfig.ReqOwnerBased = false;
            Assert.That(InvokePrivate<bool>(component, "IsEditable", implementationTask), Is.True);
        }

        /// <summary>
        /// Verifies that a new implementation task gets the default device when none are available.
        /// </summary>
        [Test]
        public void AddImplTask_UsesDefaultDeviceWhenNoDevicesExist()
        {
            DisplayImplTaskTable component = CreateComponent();
            WfHandler handler = new()
            {
                ActReqTask = new WfReqTask { Id = 20, Title = "Request", TaskType = WfTaskType.access.ToString() },
                Devices = []
            };
            SetMatrix(handler, WfTaskType.access.ToString(), CreateMatrix(0, 0, 10));
            SetPrivateField(component, nameof(DisplayImplTaskTable.WfHandler), handler);

            InvokePrivate(component, "AddImplTask");

            Assert.Multiple(() =>
            {
                Assert.That(handler.DisplayImplTaskMode, Is.True);
                Assert.That(handler.ActImplTask.DeviceId, Is.EqualTo(0));
                Assert.That(handler.ActImplTask.TaskNumber, Is.EqualTo(1));
                Assert.That(handler.ActImplTask.Title, Does.Not.EndWith(": "));
            });
        }

        [Test]
        public void OnParametersSetAsync_ResolvesAllDevicesAndCachedLookups()
        {
            DisplayImplTaskTable component = CreateComponent();
            SimulatedUserConfig userConfig = new();
            WfReqTask requestTask = new() { Id = 20, TaskType = WfTaskType.access.ToString() };
            requestTask.SetDeviceList([WfReqTaskBase.kAllDevicesId]);
            requestTask.Owners = [new FwoOwnerDataHelper { Owner = new FwoOwner { Id = 77, Name = "Owner A" } }];
            WfTicket ticket = new() { Id = 10, Tasks = [requestTask] };
            WfImplTask implementationTask = new() { Id = 99, TicketId = 10, ReqTaskId = 20, TaskType = WfTaskType.access.ToString() };
            WfHandler handler = new() { ActReqTask = requestTask, ActImplTask = implementationTask, ActTicket = ticket, TicketList = [ticket], Devices = [] };
            SetMatrix(handler, WfTaskType.access.ToString(), CreateMatrix(0, 0, 10));
            SetPrivateField(component, nameof(DisplayImplTaskTable.WfHandler), handler);
            SetPrivateField(component, "States", new WfStateDict());
            SetPrivateField(component, nameof(DisplayImplTaskTable.ImplTaskView), true);
            SetPrivateField(component, "userConfig", userConfig);

            InvokePrivateTask(component, "OnParametersSetAsync").GetAwaiter().GetResult();

            Assert.Multiple(() =>
            {
                Assert.That(InvokePrivateObject(component, "ResolveTicket", implementationTask), Is.EqualTo(ticket));
                Assert.That(InvokePrivate<string>(component, "GetOwnerName", implementationTask), Is.EqualTo("Owner A"));
                Assert.That(InvokePrivate<string>(component, "GetDeviceName", implementationTask), Is.EqualTo(userConfig.GetText("all")));
                Assert.That(InvokePrivate<bool>(component, "IsAllDevicesImplTask", implementationTask), Is.True);
            });
        }

        [Test]
        public void RowActions_SetTheExpectedHandlerModes()
        {
            DisplayImplTaskTable component = CreateComponent();
            WfImplTask implementationTask = new() { Id = 99, TicketId = 10, ReqTaskId = 20, TaskType = WfTaskType.access.ToString() };
            WfReqTask requestTask = new() { Id = 20, TaskType = WfTaskType.access.ToString(), ImplementationTasks = [implementationTask] };
            WfTicket ticket = new() { Id = 10, Tasks = [requestTask] };
            WfHandler handler = new() { ActReqTask = requestTask, ActImplTask = implementationTask, ActTicket = ticket, TicketList = [ticket], Devices = [] };
            SetMatrix(handler, WfTaskType.access.ToString(), CreateMatrix(0, 0, 10));
            SetPrivateField(component, nameof(DisplayImplTaskTable.WfHandler), handler);
            object[] args = [implementationTask];

            foreach ((string method, string mode) in new[]
            {
                ("ShowImplTask", nameof(handler.DisplayImplTaskMode)),
                ("EditImplTask", nameof(handler.EditImplTaskMode)),
                ("DeleteImplTask", nameof(handler.DisplayDeleteImplTaskMode)),
                ("ShowApprovals", nameof(handler.DisplayApprovalImplMode)),
                ("AssignImplTask", nameof(handler.DisplayAssignImplTaskMode)),
                ("CleanupImplTasks", nameof(handler.DisplayCleanupMode))
            })
            {
                handler.ResetImplTaskActions();
                GetPrivateMethod(method).Invoke(component, method == "CleanupImplTasks" ? [] : args);
                Assert.That(GetHandlerMode(handler, mode), Is.True, method);
            }
        }

        [Test]
        public void ContinueImplPhase_ReassignsTheCurrentHandler()
        {
            DisplayImplTaskTable component = CreateComponent();
            SimulatedUserConfig userConfig = new();
            WfImplTask implementationTask = new() { Id = 99, TicketId = 10, ReqTaskId = 20, TaskType = WfTaskType.access.ToString(), CurrentHandler = new UiUser { DbId = 10, Name = "Other" } };
            WfReqTask requestTask = new() { Id = 20, TaskType = WfTaskType.access.ToString(), ImplementationTasks = [implementationTask] };
            WfTicket ticket = new() { Id = 10, Tasks = [requestTask] };
            WfHandler handler = new() { ActReqTask = requestTask, ActImplTask = implementationTask, ActTicket = ticket, TicketList = [ticket], Devices = [] };
            SetPrivateField(component, nameof(DisplayImplTaskTable.WfHandler), handler);
            SetPrivateField(component, "userConfig", userConfig);
            SetMatrix(handler, WfTaskType.access.ToString(), CreateMatrix(0, 0, 10));

            InvokePrivateTask(component, "ContinueImplPhase", implementationTask).GetAwaiter().GetResult();

            Assert.That(handler.ActImplTask.CurrentHandler?.DbId, Is.EqualTo(userConfig.User.DbId));
        }

        private static DisplayImplTaskTable CreateComponent()
        {
            DisplayImplTaskTable component = new();
            PropertyInfo property = typeof(DisplayImplTaskTable).GetProperty(nameof(DisplayImplTaskTable.WfHandler), BindingFlags.Public | BindingFlags.Instance)
                ?? throw new MissingMemberException(typeof(DisplayImplTaskTable).FullName, nameof(DisplayImplTaskTable.WfHandler));
            property.SetValue(component, new WfHandler());
            return component;
        }

        private static StateMatrix CreateMatrix(int lowestInputState = 1, int lowestStartedState = 2, int lowestEndState = 10)
        {
            return new StateMatrix
            {
                LowestInputState = lowestInputState,
                LowestStartedState = lowestStartedState,
                LowestEndState = lowestEndState,
                PhaseActive =
                {
                    [WorkflowPhases.request] = true,
                    [WorkflowPhases.approval] = true,
                    [WorkflowPhases.planning] = true,
                    [WorkflowPhases.implementation] = true,
                    [WorkflowPhases.review] = true
                }
            };
        }

        private static void SetMatrix(WfHandler handler, string taskType, StateMatrix matrix)
        {
            FieldInfo field = typeof(WfHandler).GetField("stateMatrixDict", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingFieldException(typeof(WfHandler).FullName, "stateMatrixDict");
            StateMatrixDict dictionary = (StateMatrixDict)field.GetValue(handler)!;
            dictionary.Matrices[taskType] = matrix;
        }

        private static void InvokePrivate(DisplayImplTaskTable component, string methodName, params object?[] parameters)
        {
            GetPrivateMethod(methodName).Invoke(component, parameters);
        }

        private static void SetPrivateField<T>(DisplayImplTaskTable component, string fieldName, T value)
        {
            Type? componentType = typeof(DisplayImplTaskTable);
            PropertyInfo? property = null;
            FieldInfo? field = null;
            while (componentType != null && property == null && field == null)
            {
                property = componentType.GetProperty(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                field = componentType.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                componentType = componentType.BaseType;
            }

            if (property != null)
            {
                property.SetValue(component, value);
                return;
            }

            if (field != null)
            {
                field.SetValue(component, value);
                return;
            }

            throw new MissingMemberException(typeof(DisplayImplTaskTable).FullName, fieldName);
        }

        private static T InvokePrivate<T>(DisplayImplTaskTable component, string methodName, params object?[] parameters)
        {
            return (T)(GetPrivateMethod(methodName).Invoke(component, parameters)
                ?? throw new AssertionException($"Method '{methodName}' returned null."));
        }

        private static MethodInfo GetPrivateMethod(string methodName)
        {
            return typeof(DisplayImplTaskTable).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMethodException(typeof(DisplayImplTaskTable).FullName, methodName);
        }

        private static MethodInfo GetPrivateMethod(Type type, string methodName)
        {
            return type.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
                ?? throw new MissingMethodException(type.FullName, methodName);
        }

        private static object? InvokePrivateObject(DisplayImplTaskTable component, string methodName, params object?[] parameters)
        {
            MethodInfo method = typeof(DisplayImplTaskTable).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMethodException(typeof(DisplayImplTaskTable).FullName, methodName);
            return method.Invoke(component, parameters);
        }

        private static Task InvokePrivateTask(DisplayImplTaskTable component, string methodName, params object?[] parameters)
        {
            return (Task)(GetPrivateMethod(methodName).Invoke(component, parameters)
                ?? throw new AssertionException($"Method '{methodName}' returned null."));
        }

        private static async Task InvokePrivateTask(Type type, object instance, string methodName, params object[] parameters)
        {
            await (Task)GetPrivateMethod(type, methodName).Invoke(instance, parameters)!;
        }

        private static bool GetHandlerMode(WfHandler handler, string modeName)
        {
            FieldInfo field = typeof(WfHandler).GetField(modeName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingFieldException(typeof(WfHandler).FullName, modeName);
            return (bool)(field.GetValue(handler) ?? throw new AssertionException($"Handler field '{modeName}' was null."));
        }
        [Test]
        public async Task DisplayImplTaskTable_ResolvesTicketsDevicesAndPopupActions()
        {
            DisplayImplTaskTable component = new();
            UiRequestCoverageTest.RequestCoverageUserConfig userConfig = CreateUserConfig(Roles.Implementer);
            WfTicket ticket = new()
            {
                Id = 10,
                Editable = true,
                Tasks =
                [
                new WfReqTask
                {
                    Id = 20,
                    Owners =
                    [
                    new FwoOwnerDataHelper
                    {
                        Owner = new FwoOwner { Id = 77, Name = "Owner A" }
                    }
                    ]
                }
                ]
            };
            WfHandler handler = new()
            {
                ActReqTask = ticket.Tasks[0],
                ActTicket = ticket,
                TicketList = [ticket],
                Devices = [new Device { Id = 55, Name = "gw-55" }]
            };
            handler.ActReqTask.ImplementationTasks = [new WfImplTask { Id = 99, TaskType = WfTaskType.access.ToString(), TicketId = 10, ReqTaskId = 20, DeviceId = 55, TaskNumber = 1 }];
            handler.ActReqTask.Elements =
            [
            new WfReqElement { Field = ElemFieldType.source.ToString(), Cidr = new Cidr("10.0.0.1/32") },
            new WfReqElement { Field = ElemFieldType.destination.ToString(), Cidr = new Cidr("10.0.1.1/32") }
            ];
            SetMatrix(handler, WfTaskType.access.ToString(), CreateMatrix());
            SetMember(component, nameof(DisplayImplTaskTable.WfHandler), handler);
            SetMember(component, nameof(DisplayImplTaskTable.States), new WfStateDict { Name = { [1] = "Draft" } });
            SetMember(component, nameof(DisplayImplTaskTable.ImplTaskView), true);
            SetMember(component, nameof(DisplayImplTaskTable.Phase), WorkflowPhases.implementation);
            SetMember(component, "userConfig", userConfig);
            UiRequestCoverageTest.PathAnalysisApiConnection apiConnection = new();
            SetMember(component, "apiConnection", apiConnection);

            await InvokePrivateTask(typeof(DisplayImplTaskTable), component, "OnParametersSetAsync");

            WfImplTask implTask = handler.ActReqTask.ImplementationTasks[0];
            object[] implTaskArguments = [implTask];
            Assert.Multiple(() =>
            {
                Assert.That(GetPrivateMethod(typeof(DisplayImplTaskTable), "ResolveTicket").Invoke(component, implTaskArguments), Is.EqualTo(ticket));
                Assert.That(GetPrivateMethod(typeof(DisplayImplTaskTable), "GetOwnerName").Invoke(component, implTaskArguments), Is.EqualTo("Owner A"));
                Assert.That(GetPrivateMethod(typeof(DisplayImplTaskTable), "GetDeviceName").Invoke(component, implTaskArguments), Is.EqualTo("gw-55"));
                Assert.That(GetPrivateMethod(typeof(DisplayImplTaskTable), "IsEditable").Invoke(component, implTaskArguments), Is.True);
            });

            GetPrivateMethod(typeof(DisplayImplTaskTable), "ShowImplTask").Invoke(component, implTaskArguments);
            Assert.That(handler.DisplayImplTaskMode, Is.True);

            GetPrivateMethod(typeof(DisplayImplTaskTable), "EditImplTask").Invoke(component, implTaskArguments);
            Assert.That(handler.EditImplTaskMode, Is.True);

            GetPrivateMethod(typeof(DisplayImplTaskTable), "DeleteImplTask").Invoke(component, implTaskArguments);
            Assert.That(handler.DisplayDeleteImplTaskMode, Is.True);

            GetPrivateMethod(typeof(DisplayImplTaskTable), "ShowApprovals").Invoke(component, implTaskArguments);
            Assert.That(handler.DisplayApprovalImplMode, Is.True);

            GetPrivateMethod(typeof(DisplayImplTaskTable), "AssignImplTask").Invoke(component, implTaskArguments);
            Assert.That(handler.DisplayAssignImplTaskMode, Is.True);

            GetPrivateMethod(typeof(DisplayImplTaskTable), "CleanupImplTasks").Invoke(component, []);
            Assert.That(handler.DisplayCleanupMode, Is.True);

            GetPrivateMethod(typeof(DisplayImplTaskTable), "AddImplTask").Invoke(component, []);
            Assert.That(handler.DisplayImplTaskMode, Is.True);
            Assert.That(handler.ActImplTask.DeviceId, Is.EqualTo(55));
            Assert.That(handler.ActImplTask.TaskNumber, Is.EqualTo(2));

            await InvokePrivateTask(component, "CheckImplTasks");
            Assert.That(GetMember<bool>(component, "DisplayInfo"), Is.True);
            Assert.That(apiConnection.Queries, Does.Contain(NetworkAnalysisQueries.pathAnalysis));

            await InvokePrivateTask(typeof(DisplayImplTaskTable), component, "ContinueImplPhase", implTask);
        }

        [TestCase(PhaseVisibilityMode.TicketState, 0)]
        public void DisplayImplTaskTable_RendersMixedStateActionsAccordingToMasterVisibilityMode(PhaseVisibilityMode masterVisibilityMode, int expectedReadOnlyActionCount)
        {
            using BunitContext context = new();
            UiRequestCoverageTest.RequestCoverageUserConfig userConfig = CreateUserConfig(Roles.Implementer);
            context.Services.AddAuthorizationCore();
            context.Services.AddLocalization();
            context.Services.AddSingleton<UserConfig>(userConfig);
            context.Services.AddSingleton<ApiConnection>(new ThrowingApiConnection());
            context.Services.AddSingleton(new MiddlewareClient("http://localhost/"));
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton<AuthenticationStateProvider>(new UiRequestCoverageTest.TestAuthStateProvider(Roles.Implementer));

            WfHandler handler = CreateHandler(new ThrowingApiConnection(), userConfig, WorkflowPhases.implementation);
            handler.InitDone = true;
            handler.MasterStateMatrix.VisibilityMode = masterVisibilityMode;
            StateMatrix matrix = CreateMatrix();
            matrix.PhaseActive[WorkflowPhases.planning] = false;
            SetMatrix(handler, WfTaskType.access.ToString(), matrix);
            WfReqTask reqTask = new() { Id = 20, Title = "Outside implementation", TaskType = WfTaskType.access.ToString() };
            WfTicket ticket = new() { Id = 10, Title = "Mixed ticket", StateId = 2, Tasks = [reqTask] };
            handler.ActReqTask = reqTask;
            handler.ActTicket = ticket;
            handler.TicketList = [ticket];

            List<WfImplTask> implementationTasks =
            [
            new() { Id = 1, TaskType = WfTaskType.access.ToString(), StateId = 0, TicketId = 10, ReqTaskId = 20 },
            new() { Id = 2, TaskType = WfTaskType.access.ToString(), StateId = 2, TicketId = 10, ReqTaskId = 20 }
            ];

            IRenderedComponent<DisplayImplTaskTable> rendered = context.Render<DisplayImplTaskTable>(parameters => parameters
            .Add(parameter => parameter.Phase, WorkflowPhases.implementation)
            .Add(parameter => parameter.States, new WfStateDict())
            .Add(parameter => parameter.WfHandler, handler)
            .Add(parameter => parameter.AllImplTasks, implementationTasks)
            .Add(parameter => parameter.ImplTaskView, true));

            var mixedStateRows = rendered.FindAll("tr")
            .Where(row => row.TextContent.Contains("Outside implementation"))
            .ToList();
            Assert.That(mixedStateRows, Has.Count.EqualTo(2));
            var outsideRow = mixedStateRows[0];
            Assert.Multiple(() =>
            {
                Assert.That(outsideRow.QuerySelectorAll("button.btn-primary"), Has.Count.EqualTo(expectedReadOnlyActionCount));
                Assert.That(outsideRow.QuerySelectorAll("button.btn-warning, button.btn-danger"), Is.Empty);
            });
        }

        [Test]
        public void DisplayImplTaskTable_OnParametersSetAsync_ResolvesAllDevicesAndCachedLookups()
        {
            DisplayImplTaskTable component = new();
            UiRequestCoverageTest.RequestCoverageUserConfig userConfig = CreateUserConfig(Roles.Implementer);
            WfReqTask reqTask = new()
            {
                Id = 20,
                TaskType = WfTaskType.access.ToString()
            };
            reqTask.SetDeviceList([WfReqTaskBase.kAllDevicesId]);
            reqTask.Owners = [new FwoOwnerDataHelper { Owner = new FwoOwner { Id = 77, Name = "Owner A" } }];
            WfTicket ticket = new()
            {
                Id = 10,
                Tasks = [reqTask]
            };
            WfImplTask implTask = new()
            {
                Id = 99,
                TicketId = 10,
                ReqTaskId = 20,
                TaskType = WfTaskType.access.ToString(),
                DeviceId = null
            };
            WfHandler handler = new()
            {
                ActReqTask = reqTask,
                ActImplTask = implTask,
                ActTicket = ticket,
                TicketList = [ticket],
                Devices = []
            };
            SetMatrix(handler, WfTaskType.access.ToString(), CreateMatrix());
            SetMember(component, nameof(DisplayImplTaskTable.WfHandler), handler);
            SetMember(component, nameof(DisplayImplTaskTable.States), new WfStateDict { Name = { [1] = "Draft" } });
            SetMember(component, nameof(DisplayImplTaskTable.ImplTaskView), true);
            SetMember(component, "userConfig", userConfig);

            InvokePrivateTask(typeof(DisplayImplTaskTable), component, "OnParametersSetAsync").GetAwaiter().GetResult();
            object[] implTaskArguments = [implTask];

            Assert.Multiple(() =>
            {
                Assert.That(GetPrivateMethod(typeof(DisplayImplTaskTable), "ResolveTicket").Invoke(component, implTaskArguments), Is.EqualTo(ticket));
                Assert.That(GetPrivateMethod(typeof(DisplayImplTaskTable), "GetOwnerName").Invoke(component, implTaskArguments), Is.EqualTo("Owner A"));
                Assert.That(GetPrivateMethod(typeof(DisplayImplTaskTable), "GetDeviceName").Invoke(component, implTaskArguments), Is.EqualTo(userConfig.GetText("all")));
                Assert.That(GetPrivateMethod(typeof(DisplayImplTaskTable), "IsAllDevicesImplTask").Invoke(component, implTaskArguments), Is.True);
            });
        }

        [Test]
        public async Task DisplayImplTaskTable_AssignAndAssignBack_RefreshTheVisibleTask()
        {
            DisplayImplTaskTable component = new();
            UiRequestCoverageTest.RequestCoverageUserConfig userConfig = CreateUserConfig(Roles.Implementer);
            WfImplTask storedTask = new()
            {
                Id = 99,
                TicketId = 10,
                ReqTaskId = 20,
                TaskType = WfTaskType.access.ToString(),
                AssignedGroup = "cn=old"
            };
            WfTicket ticket = new()
            {
                Id = 10,
                Tasks =
                [
                new WfReqTask
                {
                    Id = 20,
                    TaskType = WfTaskType.access.ToString(),
                    ImplementationTasks = [storedTask]
                }
                ]
            };
            WfHandler handler = new()
            {
                ActReqTask = ticket.Tasks[0],
                ActImplTask = new WfImplTask(storedTask)
                {
                    AssignedGroup = "cn=new",
                    CurrentHandler = new UiUser { Dn = "cn=old", Name = "Old" }
                },
                ActTicket = ticket,
                TicketList = [ticket],
                Devices = []
            };
            SetMatrix(handler, WfTaskType.access.ToString(), CreateMatrix());
            SetMember(component, nameof(DisplayImplTaskTable.WfHandler), handler);
            SetMember(component, nameof(DisplayImplTaskTable.States), new WfStateDict());
            SetMember(component, nameof(DisplayImplTaskTable.ImplTaskView), true);
            SetMember(component, "userConfig", userConfig);
            SetMember(component, "AllImplTasks", new List<WfImplTask> { storedTask });

            await InvokePrivateTask(component, "Assign", new WfStatefulObject { AssignedGroup = "cn=new" });
            Assert.That(GetMember<List<WfImplTask>>(component, "AllImplTasks")[0].AssignedGroup, Is.EqualTo("cn=new"));
            Assert.That(handler.DisplayAssignImplTaskMode, Is.False);

            await InvokePrivateTask(component, "AssignBack");
            Assert.That(GetMember<List<WfImplTask>>(component, "AllImplTasks")[0].AssignedGroup, Is.EqualTo("cn=old"));
            Assert.That(handler.DisplayAssignImplTaskMode, Is.False);
        }

        [Test]
        public async Task DisplayImplTaskTable_ContinueImplPhase_ReassignsTheCurrentHandler()
        {
            DisplayImplTaskTable component = new();
            UiRequestCoverageTest.RequestCoverageUserConfig userConfig = CreateUserConfig(Roles.Implementer);
            WfImplTask implTask = new()
            {
                Id = 99,
                TicketId = 10,
                ReqTaskId = 20,
                TaskType = WfTaskType.access.ToString(),
                CurrentHandler = new UiUser { DbId = 10, Name = "Other" }
            };
            WfTicket ticket = new()
            {
                Id = 10,
                Tasks = [new WfReqTask { Id = 20, TaskType = WfTaskType.access.ToString(), ImplementationTasks = [implTask] }]
            };
            WfHandler handler = new()
            {
                ActReqTask = ticket.Tasks[0],
                ActImplTask = implTask,
                ActTicket = ticket,
                TicketList = [ticket],
                Devices = []
            };
            SetMember(handler, "userConfig", userConfig);
            SetMatrix(handler, WfTaskType.access.ToString(), CreateMatrix());
            SetMember(component, nameof(DisplayImplTaskTable.WfHandler), handler);
            SetMember(component, nameof(DisplayImplTaskTable.States), new WfStateDict());
            SetMember(component, nameof(DisplayImplTaskTable.ImplTaskView), true);
            SetMember(component, "userConfig", userConfig);

            await InvokePrivateTask(component, "ContinueImplPhase", implTask);

            Assert.That(handler.ActImplTask.CurrentHandler?.DbId, Is.EqualTo(userConfig.User.DbId));
        }

        [Test]
        public void DisplayImplTaskTable_RowActions_SetTheExpectedHandlerModes()
        {
            DisplayImplTaskTable component = new();
            UiRequestCoverageTest.RequestCoverageUserConfig userConfig = CreateUserConfig(Roles.Implementer);
            WfImplTask implTask = new()
            {
                Id = 99,
                TicketId = 10,
                ReqTaskId = 20,
                TaskType = WfTaskType.access.ToString()
            };
            WfReqTask reqTask = new()
            {
                Id = 20,
                TaskType = WfTaskType.access.ToString(),
                ImplementationTasks = [implTask]
            };
            WfTicket ticket = new()
            {
                Id = 10,
                Tasks = [reqTask]
            };
            WfHandler handler = new()
            {
                ActReqTask = reqTask,
                ActImplTask = implTask,
                ActTicket = ticket,
                TicketList = [ticket],
                Devices = []
            };
            SetMatrix(handler, WfTaskType.access.ToString(), CreateMatrix());
            SetMember(component, nameof(DisplayImplTaskTable.WfHandler), handler);
            SetMember(component, nameof(DisplayImplTaskTable.States), new WfStateDict());
            SetMember(component, nameof(DisplayImplTaskTable.ImplTaskView), true);
            SetMember(component, "userConfig", userConfig);
            object[] implTaskArguments = [implTask];

            GetPrivateMethod(typeof(DisplayImplTaskTable), "ShowImplTask").Invoke(component, implTaskArguments);
            Assert.That(handler.DisplayImplTaskMode, Is.True);

            handler.ResetImplTaskActions();
            GetPrivateMethod(typeof(DisplayImplTaskTable), "EditImplTask").Invoke(component, implTaskArguments);
            Assert.That(handler.DisplayImplTaskMode, Is.True);
            Assert.That(handler.EditImplTaskMode, Is.True);

            handler.ResetImplTaskActions();
            GetPrivateMethod(typeof(DisplayImplTaskTable), "DeleteImplTask").Invoke(component, implTaskArguments);
            Assert.That(handler.DisplayDeleteImplTaskMode, Is.True);

            handler.ResetImplTaskActions();
            GetPrivateMethod(typeof(DisplayImplTaskTable), "ShowApprovals").Invoke(component, implTaskArguments);
            Assert.That(handler.DisplayApprovalImplMode, Is.True);

            handler.ResetImplTaskActions();
            GetPrivateMethod(typeof(DisplayImplTaskTable), "AssignImplTask").Invoke(component, implTaskArguments);
            Assert.That(handler.DisplayAssignImplTaskMode, Is.True);

            handler.ResetImplTaskActions();
            GetPrivateMethod(typeof(DisplayImplTaskTable), "CleanupImplTasks").Invoke(component, []);
            Assert.That(handler.DisplayCleanupMode, Is.True);
        }
    }
}
