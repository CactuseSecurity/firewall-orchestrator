using Bunit;
using BlazorTable;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Report;
using FWO.Data.Workflow;
using FWO.Middleware.Client;
using FWO.Services.Workflow;
using FWO.Ui.Pages.Monitoring;
using FWO.Ui.Pages.Reporting.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Reflection;
using System.Security.Claims;

namespace FWO.Test
{
    [TestFixture]
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    internal class UiMonitorWorkflowTest
    {
        private static MethodInfo GetPrivateMethod(string name)
        {
            return typeof(MonitorWorkflow).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMethodException(typeof(MonitorWorkflow).FullName, name);
        }

        private static MethodInfo GetPrivateMethod<TComponent>(string name)
        {
            return typeof(TComponent).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                ?? throw new MissingMethodException(typeof(TComponent).FullName, name);
        }

        private static void InvokeAllowingUnrenderedStateHasChanged(MonitorWorkflow component, string methodName, params object[] args)
        {
            try
            {
                GetPrivateMethod(methodName).Invoke(component, args);
            }
            catch (TargetInvocationException exception) when (exception.InnerException is InvalidOperationException invalidOperation
                && invalidOperation.Message.Contains("render handle is not yet assigned", StringComparison.OrdinalIgnoreCase))
            {
                // The tested handlers set their selection state before calling StateHasChanged.
            }
        }

        private static T GetPrivateField<T>(MonitorWorkflow component, string fieldName)
        {
            FieldInfo? field = typeof(MonitorWorkflow).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
            {
                throw new MissingFieldException(typeof(MonitorWorkflow).FullName, fieldName);
            }
            return (T)field.GetValue(component)!;
        }

        private static void SetPrivateField<T>(MonitorWorkflow component, string fieldName, T value)
        {
            FieldInfo? field = typeof(MonitorWorkflow).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
            {
                throw new MissingFieldException(typeof(MonitorWorkflow).FullName, fieldName);
            }
            field.SetValue(component, value);
        }

        private static void SetProperty<TComponent, TValue>(TComponent component, string propertyName, TValue value)
        {
            PropertyInfo? property = typeof(TComponent).GetProperty(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (property == null)
            {
                throw new MissingMemberException(typeof(TComponent).FullName, propertyName);
            }
            property.SetValue(component, value);
        }

        private static T GetPrivateProperty<T>(MonitorWorkflow component, string propertyName)
        {
            PropertyInfo? property = typeof(MonitorWorkflow).GetProperty(propertyName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (property == null)
            {
                throw new MissingMemberException(typeof(MonitorWorkflow).FullName, propertyName);
            }
            return (T)property.GetValue(component)!;
        }

        private static void SetPrivateProperty<T>(MonitorWorkflow component, string propertyName, T value)
        {
            PropertyInfo? property = typeof(MonitorWorkflow).GetProperty(propertyName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (property == null)
            {
                throw new MissingMemberException(typeof(MonitorWorkflow).FullName, propertyName);
            }
            property.SetValue(component, value);
        }

        private static void SetInjectedService<TService>(MonitorWorkflow component, TService service)
        {
            PropertyInfo? prop = typeof(MonitorWorkflow).GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .FirstOrDefault(property => property.PropertyType == typeof(TService));
            if (prop == null)
            {
                throw new MissingMemberException(typeof(MonitorWorkflow).FullName, typeof(TService).Name);
            }
            prop.SetValue(component, service);
        }

        private static void SetAuthenticationState(MonitorWorkflow component, params string[] roles)
        {
            PropertyInfo? prop = typeof(MonitorWorkflow).GetProperty("authenticationStateTask", BindingFlags.NonPublic | BindingFlags.Instance);
            if (prop == null)
            {
                throw new MissingMemberException(typeof(MonitorWorkflow).FullName, "authenticationStateTask");
            }

            ClaimsIdentity identity = new(roles.Select(role => new Claim(ClaimTypes.Role, role)), "test", ClaimTypes.Name, ClaimTypes.Role);
            prop.SetValue(component, Task.FromResult(new AuthenticationState(new ClaimsPrincipal(identity))));
        }

        private static MonitorWorkflow CreateComponent(MonitorWorkflowApiConn apiConn, MonitorWorkflowUserConfig? userConfig = null)
        {
            MonitorWorkflow component = new();
            SetInjectedService<ApiConnection>(component, apiConn);
            SetInjectedService(component, new MiddlewareClient("http://localhost/"));
            SetInjectedService<UserConfig>(component, userConfig ?? new MonitorWorkflowUserConfig());
            SetAuthenticationState(component, Roles.Admin);
            SetPrivateField(component, "allStates", apiConn.States);
            SetPrivateProperty(component, "StateNames", apiConn.States.ToDictionary(state => state.Id, state => state.Name));
            SetPrivateProperty(component, "SelectedTaskTypes", new List<WfTaskType> { WfTaskType.access, WfTaskType.group_create });
            SetPrivateProperty(component, "SelectedStateIds", new List<int>());
            SetPrivateField(component, "wfHandler", CreateWorkflowHandler());
            return component;
        }

        private static WfHandler CreateWorkflowHandler()
        {
            WfHandler handler = new()
            {
                userConfig = new SimulatedUserConfig
                {
                    ReqAutoCreateImplTasks = AutoCreateImplTaskOptions.oneTaskForAllDevices,
                    ReqConsiderBundling = true
                }
            };
            SetMatrix(handler, WfTaskType.access.ToString(), new StateMatrix
            {
                MinImplTasksNeeded = 3,
                MinTicketCompleted = 99,
                PhaseActive = new() { { WorkflowPhases.planning, false } }
            });
            SetMatrix(handler, WfTaskType.group_create.ToString(), new StateMatrix
            {
                MinImplTasksNeeded = 3,
                MinTicketCompleted = 99,
                PhaseActive = new() { { WorkflowPhases.planning, false } }
            });
            return handler;
        }

        private static void SetMatrix(WfHandler handler, string taskType, StateMatrix matrix)
        {
            FieldInfo? field = typeof(WfHandler).GetField("stateMatrixDict", BindingFlags.NonPublic | BindingFlags.Instance);
            StateMatrixDict dict = (StateMatrixDict)(field?.GetValue(handler) ?? new StateMatrixDict());
            dict.Matrices[taskType] = matrix;
        }

        private static T GetVariable<T>(object variables, string name)
        {
            PropertyInfo? property = variables.GetType().GetProperty(name);
            if (property == null)
            {
                throw new MissingMemberException(variables.GetType().FullName, name);
            }
            return (T)property.GetValue(variables)!;
        }

        [Test]
        public async Task FetchTicketPage_UsesPagedWorkflowQueryAndStoresOnlyRequestedPage()
        {
            MonitorWorkflowApiConn apiConn = new()
            {
                Tickets =
                [
                    new WfTicket { Id = 30, Title = "Ticket 30" },
                    new WfTicket { Id = 20, Title = "Ticket 20" },
                    new WfTicket { Id = 10, Title = "Ticket 10" }
                ]
            };
            MonitorWorkflow component = CreateComponent(apiConn);
            SetPrivateProperty(component, "PageSize", 2);

            await (Task)GetPrivateMethod("FetchTicketPage").Invoke(component, null)!;

            object variables = apiConn.Variables.Single(query => query.Query == RequestQueries.getFullTicketsPaged).Variables;
            Assert.Multiple(() =>
            {
                Assert.That(GetVariable<int>(variables, "limit"), Is.EqualTo(3));
                Assert.That(GetVariable<int>(variables, "offset"), Is.EqualTo(0));
                Assert.That(GetVariable<List<string>>(variables, "taskTypes"), Is.EqualTo(new List<string> { "access", "group_create" }));
                Assert.That(GetVariable<List<int>>(variables, "stateIds"), Is.EqualTo(new List<int> { 1, 2, 3, 4 }));
                Assert.That(GetPrivateProperty<bool>(component, "HasNextPage"), Is.True);
                Assert.That(GetPrivateField<List<WfTicket>>(component, "tickets").Select(ticket => ticket.Id), Is.EqualTo(new long[] { 30, 20 }));
            });
        }

        [Test]
        public async Task UpdatePageSize_ResetsToFirstPageAndFetchesPageSizePlusOne()
        {
            MonitorWorkflowApiConn apiConn = new();
            MonitorWorkflow component = CreateComponent(apiConn);
            SetPrivateProperty(component, "CurrentPage", 3);

            await (Task)GetPrivateMethod("UpdatePageSize").Invoke(component, [50])!;

            object variables = apiConn.Variables.Single(query => query.Query == RequestQueries.getFullTicketsPaged).Variables;
            Assert.Multiple(() =>
            {
                Assert.That(GetPrivateProperty<int>(component, "PageSize"), Is.EqualTo(50));
                Assert.That(GetPrivateProperty<int>(component, "CurrentPage"), Is.EqualTo(0));
                Assert.That(GetVariable<int>(variables, "limit"), Is.EqualTo(51));
                Assert.That(GetVariable<int>(variables, "offset"), Is.EqualTo(0));
            });
        }

        [Test]
        public async Task TaskTypesChanged_UsesSelectedTaskTypesAndResetsPage()
        {
            MonitorWorkflowApiConn apiConn = new();
            MonitorWorkflow component = CreateComponent(apiConn);
            SetPrivateField(component, "availableRealTaskTypes", new List<WfTaskType> { WfTaskType.access, WfTaskType.group_create });
            SetPrivateProperty(component, "CurrentPage", 2);

            await (Task)GetPrivateMethod("TaskTypesChanged").Invoke(component, [new WfTaskType?[] { WfTaskType.access }])!;

            object variables = apiConn.Variables.Single(query => query.Query == RequestQueries.getFullTicketsPaged).Variables;
            Assert.Multiple(() =>
            {
                Assert.That(GetPrivateProperty<List<WfTaskType>>(component, "SelectedTaskTypes"), Is.EqualTo(new List<WfTaskType> { WfTaskType.access }));
                Assert.That(GetPrivateProperty<int>(component, "CurrentPage"), Is.EqualTo(0));
                Assert.That(GetVariable<List<string>>(variables, "taskTypes"), Is.EqualTo(new List<string> { "access" }));
            });
        }

        [Test]
        public void OpenStateChangeMethods_ShowPopupForEveryWorkflowObjectLevel()
        {
            MonitorWorkflow component = CreateComponent(new MonitorWorkflowApiConn());
            WfTicket ticket = new() { Id = 1, StateId = 2, Title = "Ticket" };
            WfReqTask reqTask = new() { Id = 2, StateId = 3, Title = "Req" };
            WfImplTask implTask = new() { Id = 3, StateId = 4, Title = "Impl" };
            WfApproval approval = new() { Id = 4, StateId = 1 };

            InvokeAllowingUnrenderedStateHasChanged(component, "OpenTicketStateChange", ticket);
            AssertPopup(component, WfObjectScopes.Ticket, "Ticket", 2);

            InvokeAllowingUnrenderedStateHasChanged(component, "OpenReqTaskStateChange", ticket, reqTask);
            AssertPopup(component, WfObjectScopes.RequestTask, "Req", 3);

            InvokeAllowingUnrenderedStateHasChanged(component, "OpenImplTaskStateChange", ticket, reqTask, implTask);
            AssertPopup(component, WfObjectScopes.ImplementationTask, "Impl", 4);

            InvokeAllowingUnrenderedStateHasChanged(component, "OpenApprovalStateChange", ticket, reqTask, approval);
            AssertPopup(component, WfObjectScopes.Approval, "approval", 1);
        }

        [Test]
        public async Task AutoCreateImplementationTasks_OpensConfirmWithoutCreatingImmediately()
        {
            MonitorWorkflowApiConn apiConn = new();
            MonitorWorkflow component = CreateComponent(apiConn);
            WfTicket ticket = new() { Id = 1, Title = "Ticket" };
            WfReqTask reqTask = new() { Id = 2, Title = "Req", StateId = 4, TaskType = WfTaskType.access.ToString() };

            await (Task)GetPrivateMethod("AutoCreateImplementationTasks").Invoke(component, [ticket, reqTask])!;

            Assert.Multiple(() =>
            {
                Assert.That(GetPrivateField<bool>(component, "confirmAutoCreateImplTasks"), Is.True);
                Assert.That(GetPrivateField<WfTicket?>(component, "selectedAutoCreateTicket"), Is.SameAs(ticket));
                Assert.That(GetPrivateField<WfReqTask?>(component, "selectedAutoCreateReqTask"), Is.SameAs(reqTask));
                Assert.That(reqTask.ImplementationTasks, Is.Empty);
                Assert.That(apiConn.Variables, Is.Empty);
            });
        }

        [Test]
        public async Task ConfirmAutoCreateImplementationTasks_CreatesTasksAndRefreshesCurrentPage()
        {
            WfReqTask reqTask = new()
            {
                Id = 2,
                TicketId = 1,
                Title = "Req",
                StateId = 4,
                TaskType = WfTaskType.group_create.ToString()
            };
            WfTicket ticket = new() { Id = 1, Tasks = [reqTask] };
            MonitorWorkflowApiConn apiConn = new() { Tickets = [ticket] };
            MonitorWorkflow component = CreateComponent(apiConn);
            SetPrivateField(component, "selectedAutoCreateTicket", ticket);
            SetPrivateField(component, "selectedAutoCreateReqTask", reqTask);

            await (Task)GetPrivateMethod("ConfirmAutoCreateImplementationTasks").Invoke(component, null)!;

            Assert.Multiple(() =>
            {
                Assert.That(reqTask.ImplementationTasks, Has.Count.EqualTo(1));
                Assert.That(apiConn.Variables.Count(query => query.Query == RequestQueries.getFullTicketsPaged), Is.EqualTo(1));
                Assert.That(GetPrivateField<WfTicket?>(component, "selectedAutoCreateTicket"), Is.Null);
                Assert.That(GetPrivateField<WfReqTask?>(component, "selectedAutoCreateReqTask"), Is.Null);
            });
        }

        [Test]
        public async Task ReportedTickets_RenderedForWorkflowMonitoring_ShowsAutoCreateButton()
        {
            await using BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddBlazorTable();
            context.Services.AddSingleton<UserConfig>(new MonitorWorkflowUserConfig());
            WfReqTask reqTask = new()
            {
                Id = 2,
                TicketId = 1,
                Title = "Req",
                StateId = 3,
                TaskType = WfTaskType.access.ToString()
            };
            reqTask.SetAddInfo(AdditionalInfoKeys.FlowBundleId, "bundle-1-2");
            WfTicket ticket = new()
            {
                Id = 1,
                Title = "Ticket",
                StateId = 2,
                Tasks = [reqTask]
            };
            bool autoCreateClicked = false;

            IRenderedComponent<ReportedTickets> component = context.Render<ReportedTickets>(parameters => parameters
                .Add(p => p.Tickets, new List<WfTicket> { ticket })
                .Add(p => p.StateNames, new Dictionary<int, string> { [2] = "Approved", [3] = "Implementation" })
                .Add(p => p.SelectedReportType, ReportType.TicketReport)
                .Add(p => p.DetailedView, true)
                .Add(p => p.ShowFullTicket, true)
                .Add(p => p.TaskTypes, new List<WfTaskType> { WfTaskType.access })
                .Add(p => p.ShowStateChangeButtons, true)
                .Add(p => p.SortDescendingById, true)
                .Add(p => p.CanAutoCreateImplementationTasks, (Func<WfTicket, WfReqTask, bool>)((_, _) => true))
                .Add(p => p.AutoCreateImplementationTasks, (Func<WfTicket, WfReqTask, Task>)((_, _) =>
                {
                    autoCreateClicked = true;
                    return Task.CompletedTask;
                })));

            component.Find("tbody a").Click();

            Assert.That(component.Markup, Does.Contain("Autocreate implementation tasks"));

            component.Find("button.btn-warning").Click();

            Assert.That(autoCreateClicked, Is.True);
        }

        [Test]
        public void ReportedTickets_GetFlowBundleId_ReturnsStoredBundleId()
        {
            WfReqTask reqTask = new();
            reqTask.SetAddInfo(AdditionalInfoKeys.FlowBundleId, "bundle-1-2");

            MethodInfo method = GetPrivateMethod<ReportedTickets>("GetFlowBundleId");

            Assert.That(method.Invoke(new ReportedTickets(), [reqTask]), Is.EqualTo("bundle-1-2"));
        }

        [Test]
        public async Task StateChangePopup_UsesCallbacksAndResolvesStateNames()
        {
            bool applied = false;
            bool closed = false;
            MonitorWorkflowStateChangePopup popup = new();
            SetProperty(popup, nameof(MonitorWorkflowStateChangePopup.StateNames), new Dictionary<int, string> { [3] = "Implementation" });
            SetProperty(popup, nameof(MonitorWorkflowStateChangePopup.ApplyStateChange), (Func<Task>)(() =>
            {
                applied = true;
                return Task.CompletedTask;
            }));
            SetProperty(popup, nameof(MonitorWorkflowStateChangePopup.CloseStateDialog), (Action)(() => closed = true));

            string namedState = (string)GetPrivateMethod<MonitorWorkflowStateChangePopup>("StateIdToString").Invoke(popup, [3])!;
            string fallbackState = (string)GetPrivateMethod<MonitorWorkflowStateChangePopup>("StateIdToString").Invoke(popup, [99])!;
            await (Task)GetPrivateMethod<MonitorWorkflowStateChangePopup>("Apply").Invoke(popup, null)!;
            GetPrivateMethod<MonitorWorkflowStateChangePopup>("Close").Invoke(popup, null);

            Assert.Multiple(() =>
            {
                Assert.That(namedState, Is.EqualTo("Implementation"));
                Assert.That(fallbackState, Is.EqualTo("99"));
                Assert.That(applied, Is.True);
                Assert.That(closed, Is.True);
            });
        }

        private static void AssertPopup(MonitorWorkflow component, WfObjectScopes scope, string objectName, int targetStateId)
        {
            Assert.Multiple(() =>
            {
                Assert.That(GetPrivateField<bool>(component, "showStateDialog"), Is.True);
                Assert.That(GetPrivateField<WfObjectScopes>(component, "selectedScope"), Is.EqualTo(scope));
                Assert.That(GetPrivateField<string>(component, "selectedObjectName"), Is.EqualTo(objectName));
                Assert.That(GetPrivateField<int>(component, "selectedTargetStateId"), Is.EqualTo(targetStateId));
                Assert.That(GetPrivateField<MonitoringStateChangeMode>(component, "selectedStateChangeMode"), Is.EqualTo(MonitoringStateChangeMode.LocalOnly));
            });
        }
    }

    internal sealed class MonitorWorkflowUserConfig : SimulatedUserConfig
    {
        public MonitorWorkflowUserConfig()
        {
            ReqAvailableTaskTypes = "[\"access\",\"group_create\"]";
            ReqAutoCreateImplTasks = AutoCreateImplTaskOptions.oneTaskForAllDevices;
            ReqConsiderBundling = true;
        }

        public override string GetText(string key)
        {
            return key switch
            {
                "workflow" => "Workflow",
                "tickets" => "Tickets",
                "task_type" => "Task Type",
                "state" => "State",
                "refresh" => "Refresh",
                "all" => "All",
                "no_workflow_tickets" => "No workflow tickets",
                "actions" => "Actions",
                "change_state" => "Change state",
                "auto_create_impltasks" => "Autocreate implementation tasks",
                "impltask_created" => "Implementation task created",
                "found_no_changes" => "No changes found",
                "approval" => "approval",
                "flow_bundle_id" => "Bundle ID",
                "id" => "ID",
                "task_number" => "Task Number",
                "name" => "Name",
                "tasks" => "Tasks",
                "requester" => "Requester",
                "created" => "Created",
                "closed" => "Closed",
                "start_time" => "Start",
                "end_time" => "End",
                "implementation" => "Implementation",
                "save" => "Save",
                "cancel" => "Cancel",
                "confirm" => "Confirm",
                "promote_to" => "Promote to",
                "state_change_mode" => "State change mode",
                "access" => "Access",
                "group_create" => "Group Create",
                _ => base.GetText(key)
            };
        }
    }

    internal sealed class MonitorWorkflowApiConn : SimulatedApiConnection
    {
        public List<(string Query, object Variables)> Variables { get; } = [];
        public List<WfState> States { get; set; } =
        [
            new() { Id = 1, Name = "Requested" },
            new() { Id = 2, Name = "Approved" },
            new() { Id = 3, Name = "Implementation" },
            new() { Id = 4, Name = "Done" }
        ];
        public List<WfTicket> Tickets { get; set; } = [];
        public List<Device> Devices { get; set; } = [];
        public List<FwoOwner> Owners { get; set; } = [];

        public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
        {
            if (variables != null)
            {
                Variables.Add((query, variables));
            }

            if (typeof(QueryResponseType) == typeof(List<WfState>) && query == RequestQueries.getStates)
            {
                return Task.FromResult((QueryResponseType)(object)States);
            }
            if (typeof(QueryResponseType) == typeof(List<WfTicket>) && query == RequestQueries.getFullTicketsPaged)
            {
                int limit = variables == null ? Tickets.Count : GetVariable<int>(variables, "limit");
                return Task.FromResult((QueryResponseType)(object)Tickets.Take(limit).ToList());
            }
            if (typeof(QueryResponseType) == typeof(List<Device>) && query == DeviceQueries.getDeviceDetails)
            {
                return Task.FromResult((QueryResponseType)(object)Devices);
            }
            if (typeof(QueryResponseType) == typeof(List<FwoOwner>) && query == OwnerQueries.getOwners)
            {
                return Task.FromResult((QueryResponseType)(object)Owners);
            }

            throw new NotImplementedException($"Unexpected query: {query}");
        }

        private static T GetVariable<T>(object variables, string name)
        {
            PropertyInfo? property = variables.GetType().GetProperty(name);
            if (property == null)
            {
                throw new MissingMemberException(variables.GetType().FullName, name);
            }
            return (T)property.GetValue(variables)!;
        }
    }
}
