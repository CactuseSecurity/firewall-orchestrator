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
using FWO.Ui.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;

namespace FWO.Test
{
    [TestFixture]
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    internal class UiMonitorWorkflowTest
    {
        private const int kPageSize = 10;
        private static readonly List<FancyColumnFilter> kNoFilters = [];
        private static readonly object[] kAccessTaskTypeSelection = [new WfTaskType?[] { WfTaskType.access }];

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

        private static async Task InvokeTaskAllowingUnrenderedStateHasChanged(MonitorWorkflow component, string methodName, params object[] args)
        {
            try
            {
                await (Task)GetPrivateMethod(methodName).Invoke(component, args)!;
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
                    ReqDeviceInput = ReqDeviceInputOptions.oneTaskForAllDevices,
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

        private static FancyItemsRequest PageRequest(int page = 1, int pageSize = kPageSize)
        {
            return new FancyItemsRequest("", null, true, kNoFilters, page, pageSize);
        }

        private static Task<FancyItemsResult<WfTicket>> LoadTicketPage(MonitorWorkflow component, FancyItemsRequest request)
        {
            object[] arguments = [request, CancellationToken.None];
            return (Task<FancyItemsResult<WfTicket>>)GetPrivateMethod("LoadTicketPage").Invoke(component, arguments)!;
        }

        private static string WhereJson(MonitorWorkflowApiConn apiConn)
        {
            object variables = apiConn.Variables.Last(query => query.Query == RequestQueries.getFullTicketsPaged).Variables;
            return JsonSerializer.Serialize(GetVariable<Dictionary<string, object>>(variables, "where"));
        }

        private static List<WfTicket> CreateTickets(int count)
        {
            return [.. Enumerable.Range(1, count).Select(id => new WfTicket { Id = id, Title = $"Ticket {id}", StateId = 1 })];
        }

        [Test]
        public async Task LoadTicketPage_SendsBuilderVariablesAndTrimsExtraRow()
        {
            MonitorWorkflowApiConn apiConn = new() { Tickets = CreateTickets(kPageSize + 1) };
            MonitorWorkflow component = CreateComponent(apiConn);

            FancyItemsResult<WfTicket> result = await LoadTicketPage(component, PageRequest());

            object variables = apiConn.Variables.Single(query => query.Query == RequestQueries.getFullTicketsPaged).Variables;
            Assert.Multiple(() =>
            {
                Assert.That(result.Items, Has.Count.EqualTo(kPageSize));
                Assert.That(result.HasMore, Is.True);
                Assert.That(GetVariable<int>(variables, "limit"), Is.EqualTo(kPageSize + 1));
                Assert.That(GetVariable<int>(variables, "offset"), Is.EqualTo(0));
                Assert.That(WhereJson(apiConn), Does.Contain("\"task_type\":{\"_in\":[\"access\",\"group_create\"]}"));
                Assert.That(WhereJson(apiConn), Does.Contain("\"state_id\":{\"_in\":[1,2,3,4]}"));
                Assert.That(GetPrivateField<List<WfTicket>>(component, "tickets"), Has.Count.EqualTo(kPageSize));
                Assert.That(GetPrivateField<WfHandler>(component, "wfHandler").TicketList, Has.Count.EqualTo(kPageSize));
            });
        }

        [Test]
        public async Task LoadTicketPage_SupersededRequest_DoesNotOverwriteCurrentTickets()
        {
            MonitorWorkflowApiConn apiConn = new() { Tickets = CreateTickets(3) };
            MonitorWorkflow component = CreateComponent(apiConn);
            List<WfTicket> currentTickets = [new WfTicket { Id = 99 }];
            SetPrivateField(component, "tickets", currentTickets);
            GetPrivateField<WfHandler>(component, "wfHandler").TicketList = currentTickets;
            using CancellationTokenSource supersededRequest = new();
            await supersededRequest.CancelAsync();

            object[] arguments = [PageRequest(), supersededRequest.Token];
            await (Task<FancyItemsResult<WfTicket>>)GetPrivateMethod("LoadTicketPage").Invoke(component, arguments)!;

            Assert.Multiple(() =>
            {
                Assert.That(GetPrivateField<List<WfTicket>>(component, "tickets"), Is.SameAs(currentTickets));
                Assert.That(GetPrivateField<WfHandler>(component, "wfHandler").TicketList, Is.SameAs(currentTickets));
            });
        }

        [Test]
        public async Task LoadTicketPage_LastPage_HasNoMore()
        {
            MonitorWorkflowApiConn apiConn = new() { Tickets = CreateTickets(kPageSize + 1) };
            MonitorWorkflow component = CreateComponent(apiConn);

            FancyItemsResult<WfTicket> result = await LoadTicketPage(component, PageRequest(page: 2));

            Assert.Multiple(() =>
            {
                Assert.That(result.Items.Select(ticket => ticket.Id), Is.EqualTo(new long[] { kPageSize + 1 }));
                Assert.That(result.HasMore, Is.False);
            });
        }

        [Test]
        public async Task OnInitializedAsync_LoadsStatesAndTaskTypesButLeavesTicketsToTheTable()
        {
            MonitorWorkflowApiConn apiConn = new() { Tickets = CreateTickets(2) };
            MonitorWorkflowUserConfig userConfig = new()
            {
                ReqAvailableTaskTypes = "[\"master\",\"access\",\"group_create\"]"
            };
            MonitorWorkflow component = CreateComponent(apiConn, userConfig);

            await (Task)typeof(MonitorWorkflow).GetMethod("OnInitializedAsync", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(component, null)!;

            Assert.Multiple(() =>
            {
                Assert.That(GetPrivateField<bool>(component, "InitComplete"), Is.True);
                Assert.That(GetPrivateField<List<WfTaskType>>(component, "availableRealTaskTypes"), Is.EqualTo(new List<WfTaskType> { WfTaskType.access, WfTaskType.group_create }));
                Assert.That(GetPrivateProperty<List<WfTaskType>>(component, "SelectedTaskTypes"), Is.EqualTo(new List<WfTaskType> { WfTaskType.access, WfTaskType.group_create }));
                Assert.That(GetPrivateProperty<Dictionary<int, string>>(component, "StateNames")[3], Is.EqualTo("Implementation"));
                Assert.That(apiConn.Variables.Any(query => query.Query == RequestQueries.getFullTicketsPaged), Is.False);
            });
        }

        [Test]
        public async Task TaskTypesChanged_NextPageUsesSelectedTaskTypes()
        {
            MonitorWorkflowApiConn apiConn = new();
            MonitorWorkflow component = CreateComponent(apiConn);
            SetPrivateField(component, "availableRealTaskTypes", new List<WfTaskType> { WfTaskType.access, WfTaskType.group_create });

            await (Task)GetPrivateMethod("TaskTypesChanged").Invoke(component, kAccessTaskTypeSelection)!;
            await LoadTicketPage(component, PageRequest());

            Assert.Multiple(() =>
            {
                Assert.That(GetPrivateProperty<List<WfTaskType>>(component, "SelectedTaskTypes"), Is.EqualTo(new List<WfTaskType> { WfTaskType.access }));
                Assert.That(WhereJson(apiConn), Does.Contain("\"task_type\":{\"_in\":[\"access\"]}"));
            });
        }

        [Test]
        public async Task TaskTypesChanged_NullSelectionUsesAllAvailableTaskTypes()
        {
            MonitorWorkflowApiConn apiConn = new();
            MonitorWorkflow component = CreateComponent(apiConn);
            SetPrivateField(component, "availableRealTaskTypes", new List<WfTaskType> { WfTaskType.access, WfTaskType.group_create });
            SetPrivateProperty(component, "SelectedTaskTypes", new List<WfTaskType> { WfTaskType.access });

            await (Task)GetPrivateMethod("TaskTypesChanged").Invoke(component, [new WfTaskType?[] { null }])!;

            Assert.Multiple(() =>
            {
                Assert.That(GetPrivateProperty<List<WfTaskType>>(component, "SelectedTaskTypes"), Is.EqualTo(new List<WfTaskType> { WfTaskType.access, WfTaskType.group_create }));
                Assert.That(GetPrivateProperty<IEnumerable<WfTaskType?>>(component, "SelectedTaskTypesForUi"), Is.Empty);
            });
        }

        [Test]
        public async Task StateIdsChanged_UsesSelectedStatesAndNullSelectionMeansAllStates()
        {
            MonitorWorkflowApiConn apiConn = new();
            MonitorWorkflow component = CreateComponent(apiConn);

            await (Task)GetPrivateMethod("StateIdsChanged").Invoke(component, [new int?[] { 2, 4 }])!;
            await LoadTicketPage(component, PageRequest());

            Assert.Multiple(() =>
            {
                Assert.That(GetPrivateProperty<List<int>>(component, "SelectedStateIds"), Is.EqualTo(new List<int> { 2, 4 }));
                Assert.That(GetPrivateProperty<IEnumerable<int?>>(component, "SelectedStateIdsForUi"), Is.EqualTo(new int?[] { 2, 4 }));
                Assert.That(WhereJson(apiConn), Does.Contain("\"state_id\":{\"_in\":[2,4]}"));
            });

            await (Task)GetPrivateMethod("StateIdsChanged").Invoke(component, [new int?[] { null }])!;
            await LoadTicketPage(component, PageRequest());

            Assert.Multiple(() =>
            {
                Assert.That(GetPrivateProperty<List<int>>(component, "SelectedStateIds"), Is.Empty);
                Assert.That(GetPrivateProperty<IEnumerable<int?>>(component, "SelectedStateIdsForUi"), Is.Empty);
                Assert.That(WhereJson(apiConn), Does.Contain("\"state_id\":{\"_in\":[1,2,3,4]}"));
            });
        }

        [Test]
        public void TextHelpers_ReturnAllTextAndStateFallbacks()
        {
            MonitorWorkflow component = CreateComponent(new MonitorWorkflowApiConn());

            string allTaskTypes = (string)GetPrivateMethod("TaskTypeToString").Invoke(component, [null])!;
            string accessTaskType = (string)GetPrivateMethod("TaskTypeToString").Invoke(component, [WfTaskType.access])!;
            string allStates = (string)GetPrivateMethod("StateIdToString").Invoke(component, [null])!;
            string namedState = (string)GetPrivateMethod("StateIdToString").Invoke(component, [3])!;
            string fallbackState = (string)GetPrivateMethod("StateIdToString").Invoke(component, [99])!;

            Assert.Multiple(() =>
            {
                Assert.That(allTaskTypes, Is.EqualTo("All"));
                Assert.That(accessTaskType, Is.EqualTo("Access"));
                Assert.That(allStates, Is.EqualTo("All"));
                Assert.That(namedState, Is.EqualTo("Implementation"));
                Assert.That(fallbackState, Is.EqualTo("99"));
            });
        }

        [Test]
        public async Task InitWorkflowHandler_NoAuthenticationState_DoesNothing()
        {
            MonitorWorkflowApiConn apiConn = new();
            MonitorWorkflow component = CreateComponent(apiConn);
            SetProperty<MonitorWorkflow, Task<AuthenticationState>?>(component, "authenticationStateTask", null);

            await (Task)GetPrivateMethod("InitWorkflowHandler").Invoke(component, null)!;

            Assert.That(apiConn.Variables, Is.Empty);
        }

        [Test]
        public async Task LoadTicketPage_QueryFailure_ShowsMessageAndReturnsEmpty()
        {
            MonitorWorkflowApiConn apiConn = new() { ThrowOnTicketQuery = true };
            MonitorWorkflow component = CreateComponent(apiConn);
            Exception? reportedException = null;
            SetProperty(component, "DisplayMessageInUi", (Action<Exception?, string, string, bool>)((exception, _, _, _) => reportedException = exception));

            FancyItemsResult<WfTicket> result = await LoadTicketPage(component, PageRequest());

            Assert.Multiple(() =>
            {
                Assert.That(reportedException, Is.Not.Null);
                Assert.That(result.Items, Is.Empty);
                Assert.That(result.HasMore, Is.False);
            });
        }

        [Test]
        public async Task DropdownChange_RefreshesFromFirstPage()
        {
            MonitorWorkflowApiConn apiConn = new() { Tickets = CreateTickets(kPageSize + 5) };
            await using BunitContext context = CreatePageContext(apiConn);
            IRenderedComponent<MonitorWorkflow> page = RenderPage(context);
            GoToNextTablePage(page, apiConn);

            await page.InvokeAsync(() => (Task)GetPrivateMethod("TaskTypesChanged").Invoke(page.Instance, kAccessTaskTypeSelection)!);

            page.WaitForAssertion(() => Assert.That(LastTicketQueryOffset(apiConn), Is.EqualTo(0)));
        }

        [Test]
        public async Task StateChange_RefreshKeepsPage()
        {
            WfReqTask reqTask = new() { Id = 2, TicketId = 1, Title = "Req", StateId = 4, TaskType = WfTaskType.group_create.ToString() };
            WfTicket ticket = new() { Id = 1, Tasks = [reqTask] };
            MonitorWorkflowApiConn apiConn = new() { Tickets = CreateTickets(kPageSize + 5) };
            await using BunitContext context = CreatePageContext(apiConn);
            IRenderedComponent<MonitorWorkflow> page = RenderPage(context);
            GoToNextTablePage(page, apiConn);
            int ticketQueries = apiConn.Variables.Count(query => query.Query == RequestQueries.getFullTicketsPaged);
            SetPrivateField(page.Instance, "wfHandler", CreateWorkflowHandler());
            SetPrivateField(page.Instance, "selectedAutoCreateTicket", ticket);
            SetPrivateField(page.Instance, "selectedAutoCreateReqTask", reqTask);

            await page.InvokeAsync(() => (Task)GetPrivateMethod("ConfirmAutoCreateImplementationTasks").Invoke(page.Instance, null)!);

            page.WaitForAssertion(() => Assert.That(apiConn.Variables.Count(query => query.Query == RequestQueries.getFullTicketsPaged), Is.EqualTo(ticketQueries + 1)));
            Assert.That(LastTicketQueryOffset(apiConn), Is.EqualTo(kPageSize));
        }

        private static BunitContext CreatePageContext(MonitorWorkflowApiConn apiConn)
        {
            BunitContext context = CreatePopupContext(Roles.Admin, out _);
            context.Services.AddSingleton<ApiConnection>(apiConn);
            context.Services.AddSingleton(new MiddlewareClient("http://localhost/"));
            return context;
        }

        private static IRenderedComponent<MonitorWorkflow> RenderPage(BunitContext context)
        {
            IRenderedComponent<CascadingAuthenticationState> wrapper = context.Render<CascadingAuthenticationState>(parameters => parameters
                .AddChildContent<MonitorWorkflow>());
            IRenderedComponent<MonitorWorkflow> page = wrapper.FindComponent<MonitorWorkflow>();
            page.WaitForAssertion(() => Assert.That(page.FindAll(".fancy-pagination button"), Is.Not.Empty));
            return page;
        }

        private static void GoToNextTablePage(IRenderedComponent<MonitorWorkflow> page, MonitorWorkflowApiConn apiConn)
        {
            page.FindAll(".fancy-pagination button")[^1].Click();
            page.WaitForAssertion(() => Assert.That(LastTicketQueryOffset(apiConn), Is.EqualTo(kPageSize)));
        }

        private static int LastTicketQueryOffset(MonitorWorkflowApiConn apiConn)
        {
            return GetVariable<int>(apiConn.Variables.Last(query => query.Query == RequestQueries.getFullTicketsPaged).Variables, "offset");
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

            await InvokeTaskAllowingUnrenderedStateHasChanged(component, "AutoCreateImplementationTasks", ticket, reqTask);

            Assert.Multiple(() =>
            {
                Assert.That(GetPrivateProperty<bool>(component, "ConfirmAutoCreateImplTasksDisplay"), Is.True);
                Assert.That(GetPrivateField<WfTicket?>(component, "selectedAutoCreateTicket"), Is.SameAs(ticket));
                Assert.That(GetPrivateField<WfReqTask?>(component, "selectedAutoCreateReqTask"), Is.SameAs(reqTask));
                Assert.That(reqTask.ImplementationTasks, Is.Empty);
                Assert.That(apiConn.Variables, Is.Empty);
            });
        }

        [Test]
        public void CloseStateDialog_ResetsSelectedStateChangeObject()
        {
            MonitorWorkflow component = CreateComponent(new MonitorWorkflowApiConn());
            SetPrivateField(component, "showStateDialog", true);
            SetPrivateField(component, "selectedTicket", new WfTicket());
            SetPrivateField(component, "selectedReqTask", new WfReqTask());
            SetPrivateField(component, "selectedImplTask", new WfImplTask());
            SetPrivateField(component, "selectedApproval", new WfApproval());
            SetPrivateField(component, "selectedScope", WfObjectScopes.Approval);

            InvokeAllowingUnrenderedStateHasChanged(component, "CloseStateDialog");

            Assert.Multiple(() =>
            {
                Assert.That(GetPrivateField<bool>(component, "showStateDialog"), Is.False);
                Assert.That(GetPrivateField<WfTicket?>(component, "selectedTicket"), Is.Null);
                Assert.That(GetPrivateField<WfReqTask?>(component, "selectedReqTask"), Is.Null);
                Assert.That(GetPrivateField<WfImplTask?>(component, "selectedImplTask"), Is.Null);
                Assert.That(GetPrivateField<WfApproval?>(component, "selectedApproval"), Is.Null);
                Assert.That(GetPrivateField<WfObjectScopes>(component, "selectedScope"), Is.EqualTo(WfObjectScopes.None));
            });
        }

        [Test]
        public async Task ConfirmAutoCreateImplementationTasks_NoSelectionDoesNotFetch()
        {
            MonitorWorkflowApiConn apiConn = new();
            MonitorWorkflow component = CreateComponent(apiConn);
            SetPrivateProperty(component, "WorkInProgress", true);

            await (Task)GetPrivateMethod("ConfirmAutoCreateImplementationTasks").Invoke(component, null)!;

            Assert.Multiple(() =>
            {
                Assert.That(apiConn.Variables, Is.Empty);
                Assert.That(GetPrivateProperty<bool>(component, "WorkInProgress"), Is.False);
                Assert.That(GetPrivateField<WfTicket?>(component, "selectedAutoCreateTicket"), Is.Null);
                Assert.That(GetPrivateField<WfReqTask?>(component, "selectedAutoCreateReqTask"), Is.Null);
            });
        }

        [Test]
        public async Task ApplyStateChange_NoSelectedTicketDoesNotReloadTickets()
        {
            MonitorWorkflowApiConn apiConn = new();
            MonitorWorkflow component = CreateComponent(apiConn);
            SetPrivateField<WfTicket?>(component, "selectedTicket", null);

            await (Task)GetPrivateMethod("ApplyStateChange").Invoke(component, null)!;

            Assert.Multiple(() =>
            {
                Assert.That(apiConn.Variables, Is.Empty);
                Assert.That(GetPrivateProperty<bool>(component, "WorkInProgress"), Is.False);
            });
        }

        [Test]
        public async Task ConfirmAutoCreateImplementationTasks_CreatesTasksAndRefreshesTickets()
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
            SetProperty(popup, nameof(MonitorWorkflowStateChangePopup.Display), true);
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
                Assert.That(typeof(MonitorWorkflowStateChangePopup).GetProperty(nameof(MonitorWorkflowStateChangePopup.Display))!.GetValue(popup), Is.False);
            });
        }

        [Test]
        public async Task StateChangePopup_RenderedVisible_ShowsStateModeAndHelp()
        {
            await using BunitContext context = CreatePopupContext(Roles.Admin, out _);

            IRenderedComponent<MonitorWorkflowStateChangePopup> component = RenderStateChangePopup(context);

            Assert.Multiple(() =>
            {
                Assert.That(component.Markup, Does.Contain("Change state: Ticket 42"));
                Assert.That(component.Markup, Does.Contain("Promote to"));
                Assert.That(component.Markup, Does.Contain("Implementation"));
                Assert.That(component.Markup, Does.Contain("State change mode"));
                Assert.That(component.Markup, Does.Contain("Local only"));
                Assert.That(component.Markup, Does.Contain("Mode help"));
            });
        }

        [Test]
        public async Task StateChangePopup_RenderedHidden_DoesNotRenderPopupBody()
        {
            await using BunitContext context = CreatePopupContext(Roles.Admin, out _);

            IRenderedComponent<MonitorWorkflowStateChangePopup> component = RenderStateChangePopup(context, display: false);

            Assert.Multiple(() =>
            {
                Assert.That(component.Markup, Does.Not.Contain("Change state: Ticket 42"));
                Assert.That(component.Markup, Does.Not.Contain("Promote to"));
                Assert.That(component.FindAll("button"), Is.Empty);
            });
        }

        [Test]
        public async Task StateChangePopup_SaveAndCancelButtons_InvokeCallbacks()
        {
            await using BunitContext context = CreatePopupContext(Roles.Admin, out _);
            bool applied = false;
            bool closed = false;
            IRenderedComponent<MonitorWorkflowStateChangePopup> component = RenderStateChangePopup(context,
                applyStateChange: () =>
                {
                    applied = true;
                    return Task.CompletedTask;
                },
                closeStateDialog: () => closed = true);

            component.WaitForAssertion(() => Assert.That(component.Find("button.btn-primary").HasAttribute("disabled"), Is.False));
            component.Find("button.btn-primary").Click();
            component.Find("button.btn-secondary").Click();

            Assert.Multiple(() =>
            {
                Assert.That(applied, Is.True);
                Assert.That(closed, Is.True);
                Assert.That(component.Instance.Display, Is.False);
            });
        }

        [TestCase(Roles.Auditor)]
        public async Task StateChangePopup_NonAdminGetsDisabledSaveButton(string role)
        {
            await using BunitContext context = CreatePopupContext(role, out _);

            IRenderedComponent<MonitorWorkflowStateChangePopup> component = RenderStateChangePopup(context);

            component.WaitForAssertion(() =>
            {
                var saveButtons = component.FindAll("button.btn-primary");
                Assert.That(saveButtons, Has.Count.EqualTo(1));
                Assert.That(saveButtons[0].HasAttribute("disabled"), Is.True);
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

        private static BunitContext CreatePopupContext(string role, out MonitorWorkflowUserConfig userConfig)
        {
            BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddScoped<DomEventService>();
            context.Services.AddSingleton<AuthenticationStateProvider>(new MonitoringTestAuthStateProvider(role));
            userConfig = new MonitorWorkflowUserConfig();
            userConfig.User.Roles = [role];
            userConfig.SetExecutionMode(role);
            context.Services.AddSingleton<UserConfig>(userConfig);
            return context;
        }

        private static IRenderedComponent<MonitorWorkflowStateChangePopup> RenderStateChangePopup(BunitContext context, bool display = true,
            Func<Task>? applyStateChange = null, Action? closeStateDialog = null)
        {
            IRenderedComponent<CascadingAuthenticationState> wrapper = context.Render<CascadingAuthenticationState>(parameters => parameters
                .AddChildContent<MonitorWorkflowStateChangePopup>(child => child
                    .Add(p => p.Display, display)
                    .Add(p => p.ObjectName, "Ticket 42")
                    .Add(p => p.TargetStateId, 3)
                    .Add(p => p.StateChangeMode, MonitoringStateChangeMode.LocalOnly)
                    .Add(p => p.AvailableTargetStateIds, new List<int> { 2, 3 })
                    .Add(p => p.StateNames, new Dictionary<int, string> { [2] = "Approved", [3] = "Implementation" })
                    .Add(p => p.ApplyStateChange, applyStateChange ?? (() => Task.CompletedTask))
                    .Add(p => p.CloseStateDialog, closeStateDialog ?? (() => { }))));
            return wrapper.FindComponent<MonitorWorkflowStateChangePopup>();
        }
    }

    internal sealed class MonitorWorkflowUserConfig : SimulatedUserConfig
    {
        public MonitorWorkflowUserConfig()
        {
            ReqAvailableTaskTypes = "[\"access\",\"group_create\"]";
            ReqDeviceInput = ReqDeviceInputOptions.oneTaskForAllDevices;
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
                "LocalOnly" => "Local only",
                "CascadeParents" => "Cascade parents",
                "TriggerActions" => "Trigger actions",
                "H7031" => "Mode help",
                "access" => "Access",
                "group_create" => "Group Create",
                _ => base.GetText(key)
            };
        }
    }

    internal sealed class MonitorWorkflowApiConn : SimulatedApiConnection
    {
        private readonly string stateMatrix = JsonSerializer.Serialize(new GlobalStateMatrix
        {
            GlobalMatrix = new()
            {
                [WorkflowPhases.request] = CreateMatrix(1, 2, 10, true),
                [WorkflowPhases.approval] = CreateMatrix(10, 11, 20, false),
                [WorkflowPhases.planning] = CreateMatrix(20, 21, 30, false),
                [WorkflowPhases.verification] = CreateMatrix(30, 31, 40, false),
                [WorkflowPhases.implementation] = CreateMatrix(40, 41, 50, false),
                [WorkflowPhases.review] = CreateMatrix(50, 51, 60, false),
                [WorkflowPhases.recertification] = CreateMatrix(60, 61, 70, false)
            }
        });

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
        public bool ThrowOnTicketQuery { get; set; }

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
            if (typeof(QueryResponseType) == typeof(List<WorkflowConfiguration>) && query == RequestQueries.getActiveStateMatrixConfiguration)
            {
                return Task.FromResult((QueryResponseType)(object)StateMatrixConfigurationTestHelper.FromLegacyJson(stateMatrix));
            }
            if (typeof(QueryResponseType) == typeof(List<WfTicket>) && query == RequestQueries.getFullTicketsPaged)
            {
                if (ThrowOnTicketQuery)
                {
                    throw new InvalidOperationException("ticket query failed");
                }
                int offset = variables == null ? 0 : GetVariable<int>(variables, "offset");
                int limit = variables == null ? Tickets.Count : GetVariable<int>(variables, "limit");
                return Task.FromResult((QueryResponseType)(object)Tickets.Skip(offset).Take(limit).ToList());
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

        private static StateMatrix CreateMatrix(int lowestInputState, int lowestStartedState, int lowestEndState, bool active)
        {
            return new StateMatrix
            {
                Matrix = [],
                DerivedStates = [],
                LowestInputState = lowestInputState,
                LowestStartedState = lowestStartedState,
                LowestEndState = lowestEndState,
                Active = active
            };
        }

        private static T GetVariable<T>(object variables, string name)
        {
            return (T)(variables.GetType().GetProperty(name)?.GetValue(variables) ?? throw new MissingMemberException(variables.GetType().FullName, name));
        }
    }
}
