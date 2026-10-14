using AngleSharp.Dom;
using Bunit;
using FWO.Api.Client;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Workflow;
using FWO.Middleware.Client;
using FWO.Services;
using FWO.Services.Workflow;
using FWO.Ui.Pages.Request;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Reflection;
using System.Security.Claims;
using static FWO.Test.UiRequestWorkflowTest;

namespace FWO.Test
{
    [TestFixture]
    internal class UiDisplayReqTaskTableTest
    {
        [TestCase(PhaseVisibilityMode.AnyTask, 2)]
        [TestCase(PhaseVisibilityMode.TicketState, 0)]
        public void RendersMixedStateActionsAccordingToMasterVisibilityMode(PhaseVisibilityMode masterVisibilityMode, int expectedReadOnlyActionCount)
        {
            using BunitContext context = new();
            SimulatedUserConfig userConfig = CreateUserConfig(Roles.Requester);
            context.Services.AddAuthorizationCore();
            context.Services.AddLocalization();
            context.Services.AddSingleton<UserConfig>(userConfig);
            context.Services.AddSingleton<ApiConnection>(new ThrowingApiConnection());
            context.Services.AddSingleton(new MiddlewareClient("http://localhost/"));
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton<AuthenticationStateProvider>(new TestAuthStateProvider(Roles.Requester));

            WfHandler handler = CreateHandler(new ThrowingApiConnection(), userConfig);
            handler.InitDone = true;
            handler.MasterStateMatrix.VisibilityMode = masterVisibilityMode;
            SetMatrix(handler, WfTaskType.access.ToString(), CreateMatrix());
            handler.ActTicket = new WfTicket
            {
                Tasks =
                [
                    new WfReqTask { Id = 1, Title = "Outside phase", TaskType = WfTaskType.access.ToString(), StateId = 0 },
                    new WfReqTask { Id = 2, Title = "Inside phase", TaskType = WfTaskType.access.ToString(), StateId = 2 }
                ]
            };

            IRenderedComponent<DisplayReqTaskTable> rendered = context.Render<DisplayReqTaskTable>(parameters => parameters
                .Add(parameter => parameter.Phase, WorkflowPhases.request)
                .Add(parameter => parameter.States, new WfStateDict())
                .Add(parameter => parameter.WfHandler, handler));

            IElement outsideRow = rendered.FindAll("tr").Single(row => row.TextContent.Contains("Outside phase"));
            Assert.Multiple(() =>
            {
                Assert.That(outsideRow.QuerySelectorAll("button.btn-primary"), Has.Count.EqualTo(expectedReadOnlyActionCount));
                Assert.That(outsideRow.QuerySelectorAll("button.btn-warning, button.btn-danger"), Is.Empty);
            });
        }

        private static SimulatedUserConfig CreateUserConfig(params string[] roles)
        {
            SimulatedUserConfig userConfig = new();
            userConfig.User.Roles = [.. roles];
            userConfig.User.DbId = 77;
            userConfig.User.Dn = "cn=current";
            return userConfig;
        }

        private static WfHandler CreateHandler(ApiConnection apiConnection, SimulatedUserConfig userConfig)
        {
            return new WfHandler(DefaultInit.DoNothing, userConfig, CreatePrincipal(Roles.Requester), apiConnection, new MiddlewareClient("http://localhost/"), WorkflowPhases.request, null);
        }

        private static ClaimsPrincipal CreatePrincipal(params string[] roles)
        {
            return new ClaimsPrincipal(new ClaimsIdentity(roles.Select(role => new Claim(ClaimTypes.Role, role)), "Test", ClaimTypes.Name, ClaimTypes.Role));
        }

        private static StateMatrix CreateMatrix() => new()
        {
            LowestInputState = 1,
            LowestStartedState = 2,
            LowestEndState = 10,
            PhaseActive =
            {
                [WorkflowPhases.request] = true,
                [WorkflowPhases.approval] = true,
                [WorkflowPhases.planning] = true,
                [WorkflowPhases.implementation] = true,
                [WorkflowPhases.review] = true
            }
        };

        private static void SetMatrix(WfHandler handler, string taskType, StateMatrix matrix)
        {
            FieldInfo field = typeof(WfHandler).GetField("stateMatrixDict", BindingFlags.NonPublic | BindingFlags.Instance)!;
            ((StateMatrixDict)field.GetValue(handler)!).Matrices[taskType] = matrix;
        }

        private sealed class ThrowingApiConnection : SimulatedApiConnection
        {
            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                throw new NotImplementedException($"Unexpected query: {query}");
            }

            public override GraphQlApiSubscription<SubscriptionResponseType> GetSubscription<SubscriptionResponseType>(Action<Exception> exceptionHandler, GraphQlApiSubscription<SubscriptionResponseType>.SubscriptionUpdate subscriptionUpdateHandler, string subscription, object? variables = null, string? operationName = null)
            {
                return null!;
            }
        }

        private sealed class TestAuthStateProvider(params string[] roles) : AuthenticationStateProvider
        {
            private readonly ClaimsPrincipal principal = CreatePrincipal(roles);

            public override Task<AuthenticationState> GetAuthenticationStateAsync()
            {
                return Task.FromResult(new AuthenticationState(principal));
            }
        }
        [Test]
        public void DisplayReqTaskTable_RequestPhase_AllowsEditBelowLowestEndState()
        {
            WfHandler handler = new()
            {
                EditTicketMode = true
            };
            WfReqTask reqTask = new()
            {
                TaskType = WfTaskType.access.ToString(),
                StateId = 1
            };
            SetMatrix(handler, reqTask.TaskType, new StateMatrix
            {
                LowestStartedState = 1,
                LowestEndState = 5
            });

            DisplayReqTaskTable component = CreateReqTaskTable(handler, WorkflowPhases.request);
            MethodInfo? method = typeof(DisplayReqTaskTable).GetMethod("CanEditReqTaskInPhase", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(method, Is.Not.Null);

            bool canEdit = (bool)method!.Invoke(component, [reqTask])!;

            Assert.That(canEdit, Is.True);
        }

        [Test]
        public void DisplayReqTaskTable_RequestPhase_BlocksLockedRequestTaskEdit()
        {
            WfHandler handler = new()
            {
                EditTicketMode = true
            };
            WfReqTask reqTask = new()
            {
                TaskType = WfTaskType.access.ToString(),
                StateId = 1,
                Locked = true
            };
            SetMatrix(handler, reqTask.TaskType, new StateMatrix
            {
                LowestStartedState = 1,
                LowestEndState = 5
            });

            DisplayReqTaskTable component = CreateReqTaskTable(handler, WorkflowPhases.request);
            MethodInfo? method = typeof(DisplayReqTaskTable).GetMethod("CanEditReqTaskInPhase", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(method, Is.Not.Null);

            bool canEdit = (bool)method!.Invoke(component, [reqTask])!;

            Assert.That(canEdit, Is.False);
        }

        [Test]
        public async Task DisplayReqTaskTable_ShowsBundleColumnWhenAnyTaskHasBundleId()
        {
            await using BunitContext context = new();
            WfReqTask bundledTask = new()
            {
                Id = 1,
                Title = "Bundled task",
                TaskType = WfTaskType.access.ToString(),
                StateId = 1
            };
            bundledTask.SetAddInfo(AdditionalInfoKeys.FlowBundleId, "bundle-1-2");
            WfReqTask unbundledTask = new()
            {
                Id = 2,
                Title = "Unbundled task",
                TaskType = WfTaskType.access.ToString(),
                StateId = 1
            };
            WfTicket ticket = new()
            {
                Id = 1,
                Tasks = [bundledTask, unbundledTask]
            };
            WfHandler handler = CreateWorkflowHandler(WorkflowPhases.request, WfTaskType.access.ToString(), ticket);
            handler.InitDone = true;

            IRenderedComponent<DisplayReqTaskTable> component = RenderDisplayReqTaskTable(context, handler, WorkflowPhases.request, new WfStateDict());

            Assert.Multiple(() =>
            {
                Assert.That(component.Markup, Does.Contain("Bundle ID"));
                Assert.That(component.Find("#req-task-bundle-id-1").TextContent, Is.EqualTo("bundle-1-2"));
                Assert.That(component.Find("#req-task-bundle-id-2").TextContent, Is.Empty);
            });
        }

        [Test]
        public async Task DisplayReqTaskTable_HidesBundleColumnWhenNoTaskHasBundleId()
        {
            await using BunitContext context = new();
            WfTicket ticket = new()
            {
                Id = 1,
                Tasks =
                [
                new WfReqTask
                {
                    Id = 1,
                    Title = "Request task",
                    TaskType = WfTaskType.access.ToString(),
                    StateId = 1
                }
                ]
            };
            WfHandler handler = CreateWorkflowHandler(WorkflowPhases.request, WfTaskType.access.ToString(), ticket);
            handler.InitDone = true;

            IRenderedComponent<DisplayReqTaskTable> component = RenderDisplayReqTaskTable(context, handler, WorkflowPhases.request, new WfStateDict());

            Assert.Multiple(() =>
            {
                Assert.That(component.Markup, Does.Not.Contain("Bundle ID"));
                Assert.That(component.FindAll("#req-task-bundle-id-1"), Is.Empty);
            });
        }

        [Test]
        public async Task DisplayReqTaskTable_GroupsBundledTasksOnlyWhenStatesMatch()
        {
            await using BunitContext context = new();
            WfReqTask first = new()
            {
                Id = 1,
                Title = "First bundled task",
                TaskType = WfTaskType.access.ToString(),
                StateId = 1
            };
            first.SetAddInfo(AdditionalInfoKeys.FlowBundleId, "bundle-1-2");
            WfReqTask sameState = new()
            {
                Id = 2,
                Title = "Second bundled task",
                TaskType = WfTaskType.access.ToString(),
                StateId = 1
            };
            sameState.SetAddInfo(AdditionalInfoKeys.FlowBundleId, "bundle-1-2");
            WfReqTask differentState = new()
            {
                Id = 3,
                Title = "Different state task",
                TaskType = WfTaskType.access.ToString(),
                StateId = 2
            };
            differentState.SetAddInfo(AdditionalInfoKeys.FlowBundleId, "bundle-1-2");
            WfTicket ticket = new() { Id = 1, Tasks = [first, sameState, differentState] };
            WfHandler handler = CreateWorkflowHandler(WorkflowPhases.approval, WfTaskType.access.ToString(), ticket);
            handler.InitDone = true;
            handler.userConfig.ReqConsiderBundling = true;
            handler.userConfig.ReqDisplayBundledTasksAsOne = true;

            IRenderedComponent<DisplayReqTaskTable> component = RenderDisplayReqTaskTable(context, handler, WorkflowPhases.approval, new WfStateDict());

            Assert.Multiple(() =>
            {
                Assert.That(component.Markup, Does.Contain("First bundled task"));
                Assert.That(component.Markup, Does.Not.Contain("Second bundled task"));
                Assert.That(component.Markup, Does.Contain("Different state task"));
            });
        }

        [Test]
        public async Task DisplayReqTaskTable_HidesTableBeforeHandlerInitialization()
        {
            await using BunitContext context = new();
            WfTicket ticket = new()
            {
                Id = 1,
                Tasks =
                [
                new WfReqTask
                {
                    Id = 1,
                    Title = "Hidden task",
                    TaskType = WfTaskType.access.ToString(),
                    StateId = 1
                }
                ]
            };
            WfHandler handler = CreateWorkflowHandler(WorkflowPhases.request, WfTaskType.access.ToString(), ticket);
            handler.InitDone = false;

            IRenderedComponent<DisplayReqTaskTable> component = RenderDisplayReqTaskTable(context, handler, WorkflowPhases.request, new WfStateDict());

            Assert.Multiple(() =>
            {
                Assert.That(component.FindAll("table"), Is.Empty);
                Assert.That(component.Markup, Does.Not.Contain("Hidden task"));
            });
        }

        [Test]
        public void DisplayReqTaskTable_DoesNotGroupSeparateUnsavedTasks()
        {
            WfReqTask first = new() { Id = 0, Title = "First new task" };
            WfReqTask second = new() { Id = 0, Title = "Second new task" };
            WfTicket ticket = new() { Id = 0, Tasks = [first, second] };
            WfHandler handler = CreateWorkflowHandler(WorkflowPhases.request, WfTaskType.access.ToString(), ticket);
            handler.InitDone = true;
            handler.userConfig.ReqConsiderBundling = true;
            handler.userConfig.ReqDisplayBundledTasksAsOne = true;
            DisplayReqTaskTable component = CreateReqTaskTable(handler, WorkflowPhases.request);
            MethodInfo method = typeof(DisplayReqTaskTable).GetMethod("GetDisplayedTasks", BindingFlags.NonPublic | BindingFlags.Instance)!;

            IEnumerable<WfReqTask> displayedTasks = (IEnumerable<WfReqTask>)method.Invoke(component, [])!;

            Assert.That(displayedTasks, Has.Count.EqualTo(2));
        }

        [Test]
        public async Task DisplayReqTaskTable_RequestEditModeShowsStructuralActions()
        {
            await using BunitContext context = new();
            WfTicket ticket = new()
            {
                Id = 1,
                Locked = false,
                Tasks =
                [
                new WfReqTask
                {
                    Id = 1,
                    Title = "Editable task",
                    TaskType = WfTaskType.access.ToString(),
                    StateId = 1
                }
                ]
            };
            WfHandler handler = CreateWorkflowHandler(WorkflowPhases.request, WfTaskType.access.ToString(), ticket);
            handler.InitDone = true;
            handler.EditTicketMode = true;

            IRenderedComponent<DisplayReqTaskTable> component = RenderDisplayReqTaskTable(context, handler, WorkflowPhases.request, new WfStateDict());

            Assert.Multiple(() =>
            {
                Assert.That(component.FindAll("button.btn-success.m-2"), Has.Count.EqualTo(1));
                Assert.That(component.FindAll("table tbody button.btn-primary"), Has.Count.EqualTo(2));
                Assert.That(component.FindAll("table tbody button.btn-warning"), Has.Count.EqualTo(1));
                Assert.That(component.FindAll("table tbody button.btn-danger"), Has.Count.EqualTo(1));
            });
        }

        [Test]
        public async Task DisplayReqTaskTable_ApprovalPhaseShowsApprovalDeadlineInsteadOfAssignmentColumns()
        {
            await using BunitContext context = new();
            WfReqTask reqTask = new()
            {
                Id = 1,
                Title = "Approval task",
                TaskType = WfTaskType.access.ToString(),
                StateId = 3,
                AssignedGroup = "cn=assigned",
                Start = new DateTime(2026, 1, 1),
                Stop = new DateTime(2026, 1, 2)
            };
            reqTask.Approvals.Add(new WfApproval
            {
                InitialApproval = true,
                Deadline = new DateTime(2026, 2, 3)
            });
            WfTicket ticket = new()
            {
                Id = 1,
                Tasks = [reqTask]
            };
            WfHandler handler = CreateWorkflowHandler(WorkflowPhases.approval, WfTaskType.access.ToString(), ticket);
            handler.InitDone = true;

            IRenderedComponent<DisplayReqTaskTable> component = RenderDisplayReqTaskTable(context, handler, WorkflowPhases.approval, new WfStateDict());

            Assert.Multiple(() =>
            {
                Assert.That(component.Markup, Does.Contain("approval_deadline"));
                Assert.That(component.Markup, Does.Not.Contain(">assigned<"));
                Assert.That(component.Markup, Does.Not.Contain(">start<"));
                Assert.That(component.Markup, Does.Not.Contain(">stop<"));
            });
        }

        [Test]
        public async Task DisplayReqTaskTable_PlanningPhaseShowsStartContinueAndAssignActions()
        {
            await using BunitContext context = new();
            WfTicket ticket = new()
            {
                Id = 1,
                Tasks =
                [
                new WfReqTask
                {
                    Id = 1,
                    Title = "Startable task",
                    TaskType = WfTaskType.access.ToString(),
                    StateId = 0
                },
                new WfReqTask
                {
                    Id = 2,
                    Title = "Continuable task",
                    TaskType = WfTaskType.access.ToString(),
                    StateId = 3
                }
                ]
            };
            WfHandler handler = CreateWorkflowHandler(WorkflowPhases.planning, WfTaskType.access.ToString(), ticket);
            handler.InitDone = true;

            IRenderedComponent<DisplayReqTaskTable> component = RenderDisplayReqTaskTable(context, handler, WorkflowPhases.planning, new WfStateDict(), Roles.Planner);

            Assert.Multiple(() =>
            {
                Assert.That(component.Markup, Does.Contain("start_planning"));
                Assert.That(component.Markup, Does.Contain("continue_planning"));
                Assert.That(component.FindAll("table tbody button.btn-warning"), Has.Count.EqualTo(3));
            });
        }

        [Test]
        public async Task DisplayReqTaskTable_ReadOnlyModeSuppressesPhaseActions()
        {
            await using BunitContext context = new();
            WfTicket ticket = new()
            {
                Id = 1,
                Tasks =
                [
                new WfReqTask
                {
                    Id = 1,
                    Title = "Read-only task",
                    TaskType = WfTaskType.access.ToString(),
                    StateId = 0
                }
                ]
            };
            WfHandler handler = CreateWorkflowHandler(WorkflowPhases.planning, WfTaskType.access.ToString(), ticket);
            handler.InitDone = true;
            handler.ReadOnlyMode = true;

            IRenderedComponent<DisplayReqTaskTable> component = RenderDisplayReqTaskTable(context, handler, WorkflowPhases.planning, new WfStateDict(), Roles.Planner);

            Assert.Multiple(() =>
            {
                Assert.That(component.FindAll("table tbody button.btn-primary"), Is.Not.Empty);
                Assert.That(component.FindAll("table tbody button.btn-warning"), Is.Empty);
                Assert.That(component.Markup, Does.Not.Contain("start_planning"));
                Assert.That(component.Markup, Does.Not.Contain("continue_planning"));
            });
        }

        [Test]
        public void DisplayReqTaskTable_AddReqTaskInitializesDefaultAccessTask()
        {
            WfHandler handler = CreateWorkflowHandler(WorkflowPhases.request, WfTaskType.access.ToString(), new WfTicket { Id = 1 });
            DisplayReqTaskTable component = CreateReqTaskTable(handler, WorkflowPhases.request);

            GetPrivateMethod(typeof(DisplayReqTaskTable), "AddReqTask").Invoke(component, []);

            Assert.Multiple(() =>
            {
                Assert.That(handler.DisplayReqTaskMode, Is.True);
                Assert.That(handler.EditReqTaskMode, Is.True);
                Assert.That(handler.AddReqTaskMode, Is.True);
                Assert.That(handler.ActReqTask.TaskType, Is.EqualTo(WfTaskType.access.ToString()));
                Assert.That(handler.ActReqTask.RuleAction, Is.EqualTo(1));
                Assert.That(handler.ActReqTask.Tracking, Is.EqualTo(1));
                Assert.That(handler.ActReqTask.ManagementId, Is.EqualTo(-1));
            });
        }

        [Test]
        public void DisplayReqTaskTable_DeleteReqTaskOpensDeletePopupForTask()
        {
            WfReqTask reqTask = new()
            {
                Id = 7,
                TicketId = 1,
                Title = "Delete task",
                TaskType = WfTaskType.access.ToString()
            };
            WfTicket ticket = new()
            {
                Id = 1,
                Tasks = [reqTask]
            };
            WfHandler handler = CreateWorkflowHandler(WorkflowPhases.request, WfTaskType.access.ToString(), ticket);
            DisplayReqTaskTable component = CreateReqTaskTable(handler, WorkflowPhases.request);

            GetPrivateMethod(typeof(DisplayReqTaskTable), "DeleteReqTask").Invoke(component, [reqTask]);

            Assert.Multiple(() =>
            {
                Assert.That(handler.DisplayDeleteReqTaskMode, Is.True);
                Assert.That(handler.ActReqTask.Id, Is.EqualTo(7));
                Assert.That(handler.ActReqTask.Title, Is.EqualTo("Delete task"));
            });
        }

        [Test]
        public async Task DisplayReqTaskTable_ResetCallsParentAndClearsRequestTaskActions()
        {
            await using BunitContext context = new();
            int resetCalls = 0;
            WfHandler handler = CreateWorkflowHandler(WorkflowPhases.request, WfTaskType.access.ToString(), new WfTicket { Id = 1 });
            handler.DisplayReqTaskMode = true;
            handler.EditReqTaskMode = true;
            handler.DisplayDeleteReqTaskMode = true;
            IRenderedComponent<DisplayReqTaskTable> component = RenderDisplayReqTaskTable(context, handler, WorkflowPhases.request, new WfStateDict());
            SetMember(component.Instance, nameof(DisplayReqTaskTable.ResetParent), () =>
            {
                resetCalls++;
                return Task.CompletedTask;
            });

            await component.InvokeAsync(async () => await InvokePrivateTask(component.Instance, "Reset"));

            Assert.Multiple(() =>
            {
                Assert.That(resetCalls, Is.EqualTo(1));
                Assert.That(handler.DisplayReqTaskMode, Is.False);
                Assert.That(handler.EditReqTaskMode, Is.False);
                Assert.That(handler.DisplayDeleteReqTaskMode, Is.False);
            });
        }

        [Test]
        public async Task DisplayReqTaskTable_OpenActionsDelegateToHandler()
        {
            WfReqTask reqTask = new()
            {
                Id = 7,
                TicketId = 1,
                Title = "Task",
                TaskType = WfTaskType.access.ToString(),
                StateId = 2
            };
            WfTicket ticket = new()
            {
                Id = 1,
                Tasks = [reqTask]
            };
            WfHandler handler = CreateWorkflowHandler(WorkflowPhases.request, WfTaskType.access.ToString(), ticket);
            DisplayReqTaskTable component = CreateReqTaskTable(handler, WorkflowPhases.request);

            await InvokePrivateTask(component, "ShowReqTask", reqTask);
            await InvokePrivateTask(component, "EditReqTask", reqTask);
            await InvokePrivateTask(component, "ShowApprovals", reqTask);

            WfHandler assignHandler = CreateWorkflowHandler(WorkflowPhases.request, WfTaskType.access.ToString(), ticket);
            DisplayReqTaskTable assignComponent = CreateReqTaskTable(assignHandler, WorkflowPhases.request);
            await InvokePrivateTask(assignComponent, "AssignTask", reqTask);

            Assert.Multiple(() =>
            {
                Assert.That(handler.ActReqTask.Id, Is.EqualTo(7));
                Assert.That(handler.DisplayReqTaskMode, Is.True);
                Assert.That(handler.EditReqTaskMode, Is.True);
                Assert.That(assignHandler.DisplayAssignReqTaskMode, Is.True);
            });
        }

        [Test]
        public async Task DisplayReqTaskTable_ContinuePhase_ReassignsCurrentHandler()
        {
            WfReqTask reqTask = new()
            {
                Id = 7,
                TicketId = 1,
                Title = "Task",
                TaskType = WfTaskType.access.ToString(),
                StateId = 3,
                CurrentHandler = new UiUser { DbId = 99, Name = "Other" }
            };
            WfTicket ticket = new()
            {
                Id = 1,
                Tasks = [reqTask]
            };
            WfHandler handler = CreateWorkflowHandler(WorkflowPhases.planning, WfTaskType.access.ToString(), ticket);
            DisplayReqTaskTable component = CreateReqTaskTable(handler, WorkflowPhases.planning);

            await InvokePrivateTask(component, "ContinuePhase", reqTask);

            Assert.Multiple(() =>
            {
                Assert.That(handler.ActReqTask.CurrentHandler?.DbId, Is.EqualTo(10));
                Assert.That(handler.DisplayReqTaskMode, Is.True);
            });
        }

        [Test]
        public void DisplayReqTaskTable_IsEditable_RespectsOwnerBasedConfiguration()
        {
            WfTicket ticket = new()
            {
                Id = 1,
                Editable = false
            };
            WfHandler ownerBasedHandler = CreateWorkflowHandler(WorkflowPhases.request, WfTaskType.access.ToString(), ticket);
            ownerBasedHandler.userConfig.ReqOwnerBased = true;
            DisplayReqTaskTable ownerBasedComponent = CreateReqTaskTable(ownerBasedHandler, WorkflowPhases.request);
            SetMember(ownerBasedComponent, "userConfig", ownerBasedHandler.userConfig);
            DisplayReqTaskTable freeComponent = CreateReqTaskTable(CreateWorkflowHandler(WorkflowPhases.request, WfTaskType.access.ToString(), ticket), WorkflowPhases.request);
            SetMember(freeComponent, "userConfig", new RequestWorkflowUserConfig());
            MethodInfo? method = typeof(DisplayReqTaskTable).GetMethod("IsEditable", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(method, Is.Not.Null);

            bool ownerBasedEditable = (bool)method!.Invoke(ownerBasedComponent, [new WfReqTask()])!;
            bool freeEditable = (bool)method!.Invoke(freeComponent, [new WfReqTask()])!;

            Assert.Multiple(() =>
            {
                Assert.That(ownerBasedEditable, Is.False);
                Assert.That(freeEditable, Is.True);
            });
        }

        [Test]
        public void DisplayReqTaskTable_StructureGateAndBundleDetection_WorkAsExpected()
        {
            WfReqTask bundledTask = new()
            {
                Id = 1,
                TaskType = WfTaskType.access.ToString()
            };
            bundledTask.SetAddInfo(AdditionalInfoKeys.FlowBundleId, "bundle-1");
            WfReqTask unbundledTask = new()
            {
                Id = 2,
                TaskType = WfTaskType.access.ToString()
            };
            WfTicket ticket = new()
            {
                Id = 1,
                Locked = false,
                Tasks = [bundledTask, unbundledTask]
            };
            WfHandler handler = CreateWorkflowHandler(WorkflowPhases.request, WfTaskType.access.ToString(), ticket);
            handler.EditTicketMode = true;
            DisplayReqTaskTable component = CreateReqTaskTable(handler, WorkflowPhases.request);
            WfReqTask editableTask = new()
            {
                Id = 3,
                TaskType = WfTaskType.access.ToString(),
                StateId = 1
            };
            SetMatrix(handler, editableTask.TaskType, new StateMatrix
            {
                LowestStartedState = 1,
                LowestEndState = 5
            });

            bool canChange = InvokePrivateBool(component, "CanChangeReqTaskStructure");
            bool hasBundles = InvokePrivateBool(component, "HasBundleIds");
            bool canEdit = (bool)typeof(DisplayReqTaskTable).GetMethod("CanEditReqTaskInPhase", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(component, [editableTask])!;

            Assert.Multiple(() =>
            {
                Assert.That(canChange, Is.True);
                Assert.That(hasBundles, Is.True);
                Assert.That(canEdit, Is.True);
            });
        }

        [Test]
        public void DisplayReqTaskTable_ApprovalPhase_BlocksStructuralTaskChanges()
        {
            WfHandler handler = new()
            {
                EditTicketMode = true
            };
            WfReqTask reqTask = new()
            {
                TaskType = WfTaskType.access.ToString(),
                StateId = 1
            };
            SetMatrix(handler, reqTask.TaskType, new StateMatrix
            {
                LowestStartedState = 1,
                LowestEndState = 5
            });

            DisplayReqTaskTable component = CreateReqTaskTable(handler, WorkflowPhases.approval);
            MethodInfo? method = typeof(DisplayReqTaskTable).GetMethod("CanEditReqTaskInPhase", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(method, Is.Not.Null);

            bool canEdit = (bool)method!.Invoke(component, [reqTask])!;

            Assert.That(canEdit, Is.False);
        }

        [Test]
        public void DisplayReqTaskTable_RequestPhase_AllowsStructuralTaskChangesInTicketEditMode()
        {
            WfHandler handler = new()
            {
                EditTicketMode = true
            };
            DisplayReqTaskTable component = CreateReqTaskTable(handler, WorkflowPhases.request);
            MethodInfo? method = typeof(DisplayReqTaskTable).GetMethod("CanChangeReqTaskStructure", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(method, Is.Not.Null);

            bool canChange = (bool)method!.Invoke(component, [])!;

            Assert.That(canChange, Is.True);
        }

        [Test]
        public void DisplayReqTaskTable_RequestPhase_BlocksStructuralTaskChangesForLockedTicket()
        {
            WfHandler handler = new()
            {
                EditTicketMode = true,
                ActTicket = new WfTicket { Locked = true }
            };
            DisplayReqTaskTable component = CreateReqTaskTable(handler, WorkflowPhases.request);
            MethodInfo? method = typeof(DisplayReqTaskTable).GetMethod("CanChangeReqTaskStructure", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(method, Is.Not.Null);

            bool canChange = (bool)method!.Invoke(component, [])!;

            Assert.That(canChange, Is.False);
        }

        [Test]
        public void DisplayReqTaskTable_ApprovalPhase_BlocksStructuralTaskChangesInTicketEditMode()
        {
            WfHandler handler = new()
            {
                EditTicketMode = true
            };
            DisplayReqTaskTable component = CreateReqTaskTable(handler, WorkflowPhases.approval);
            MethodInfo? method = typeof(DisplayReqTaskTable).GetMethod("CanChangeReqTaskStructure", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(method, Is.Not.Null);

            bool canChange = (bool)method!.Invoke(component, [])!;

            Assert.That(canChange, Is.False);
        }

    }
}
