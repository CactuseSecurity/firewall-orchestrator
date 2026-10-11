using AngleSharp.Dom;
using Bunit;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Flow;
using FWO.Data.Workflow;
using FWO.Middleware.Client;
using FWO.Services;
using FWO.Services.EventMediator;
using FWO.Services.EventMediator.Interfaces;
using FWO.Services.Workflow;
using FWO.Ui.Pages.Request;
using FWO.Ui.Shared;
using FWO.Ui.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Claims;
using System.Threading.Tasks;

namespace FWO.Test
{
    internal partial class UiRequestWorkflowTest
    {
        [TestCase(typeof(RequestPlannings))]
        [TestCase(typeof(RequestImplementations))]
        [TestCase(typeof(RequestApprovals))]
        [TestCase(typeof(RequestReviews))]
        [TestCase(typeof(RequestTickets))]
        [TestCase(typeof(RequestTicketsOverview))]
        public async Task RequestPages_InitializationFailure_DisplaysError(Type pageType)
        {
            UiRequestCoverageTest.RequestCoverageUserConfig userConfig = UiRequestCoverageTest.CreateUserConfig(Roles.Admin);
            object component = Activator.CreateInstance(pageType)!;
            List<string> messages = [];

            SetMember(component, "userConfig", userConfig);
            SetMember(component, "apiConnection", new UiRequestCoverageTest.ThrowingApiConnection());
            SetMember(component, "middlewareClient", new MiddlewareClient("http://localhost/"));
            SetMember(component, "DisplayMessageInUi", (Action<Exception?, string, string, bool>)((_, _, message, _) => messages.Add(message)));

            await UiRequestCoverageTest.InvokePrivateTask(pageType, component, "OnInitializedAsync");

            Assert.That(messages, Has.Count.EqualTo(1));
        }

        [TestCase(typeof(RequestPlannings))]
        [TestCase(typeof(RequestImplementations))]
        [TestCase(typeof(RequestApprovals))]
        [TestCase(typeof(RequestReviews))]
        [TestCase(typeof(RequestTickets))]
        public void RequestPages_HandleInvalidTicketId_DoesNothing(Type pageType)
        {
            object component = Activator.CreateInstance(pageType)!;
            SetMember(component, nameof(RequestPlannings.TicketId), "invalid");

            Assert.DoesNotThrowAsync(() => UiRequestCoverageTest.InvokePrivateTask(pageType, component, "HandleTicketId"));
        }

        [TestCase(typeof(RequestPlannings), "StartPlanTask", typeof(WfReqTask))]
        [TestCase(typeof(RequestImplementations), "StartImplementTask", typeof(WfImplTask))]
        [TestCase(typeof(RequestApprovals), "StartApproveTask", typeof(WfReqTask))]
        [TestCase(typeof(RequestReviews), "StartReviewTask", typeof(WfImplTask))]
        public void RequestPages_BusyStartAction_DoesNothing(Type pageType, string methodName, Type taskType)
        {
            object component = Activator.CreateInstance(pageType)!;
            SetMember(component, "WorkInProgress", true);
            object task = Activator.CreateInstance(taskType)!;

            Assert.DoesNotThrowAsync(() => UiRequestCoverageTest.InvokePrivateTask(pageType, component, methodName, task));
        }

        [TestCase(typeof(RequestPlannings))]
        [TestCase(typeof(RequestImplementations))]
        [TestCase(typeof(RequestApprovals))]
        [TestCase(typeof(RequestReviews))]
        [TestCase(typeof(RequestTickets))]
        public async Task RequestPages_HandleValidTicketIdWithoutDatabase_DoesNothing(Type pageType)
        {
            object component = Activator.CreateInstance(pageType)!;
            SetMember(component, nameof(RequestPlannings.TicketId), "7");
            SetMember(component, "wfHandler", new WfHandler());

            await UiRequestCoverageTest.InvokePrivateTask(pageType, component, "HandleTicketId");

            Assert.That(GetMember<string>(component, nameof(RequestPlannings.TicketId)), Is.EqualTo("7"));
        }

        [TestCase(typeof(RequestPlannings), WorkflowPhases.planning, "/request/implementations/7")]
        [TestCase(typeof(RequestImplementations), WorkflowPhases.implementation, "/request/reviews/7")]
        [TestCase(typeof(RequestApprovals), WorkflowPhases.approval, "/request/plannings/7")]
        [TestCase(typeof(RequestReviews), WorkflowPhases.review, "/request/recertifications/7")]
        [TestCase(typeof(RequestTickets), WorkflowPhases.request, "/request/approvals/7")]
        public async Task RequestPages_HandleTicketId_NavigatesToNextPhase(Type pageType, WorkflowPhases phase, string expectedUri)
        {
            RequestPageTicketApiConn apiConnection = new()
            {
                Ticket = new WfTicket { Id = 7, StateId = 12 }
            };
            WfHandler handler = CreateTicketRoutingHandler(apiConnection, phase);
            RequestPageNavigationManager navigationManager = new();
            object component = Activator.CreateInstance(pageType)!;

            SetMember(component, nameof(RequestPlannings.TicketId), "7");
            SetMember(component, "wfHandler", handler);
            SetMember(component, "NavigationManager", navigationManager);

            Func<GlobalStateMatrix> originalFactory = GlobalStateMatrix.Factory;
            GlobalStateMatrix.Factory = () => new RequestPageGlobalStateMatrix();
            try
            {
                await UiRequestCoverageTest.InvokePrivateTask(pageType, component, "HandleTicketId");
            }
            finally
            {
                GlobalStateMatrix.Factory = originalFactory;
            }

            Assert.That(navigationManager.LastUri, Is.EqualTo(expectedUri));
        }

        [Test]
        public async Task RequestImplementations_Select_UsesOwnerSelectionWhenConfigured()
        {
            UiRequestCoverageTest.RequestCoverageUserConfig userConfig = UiRequestCoverageTest.CreateUserConfig(Roles.Implementer);
            userConfig.ReqOwnerBased = true;
            WfHandler handler = CreateWorkflowHandler(WorkflowPhases.implementation, WfTaskType.access.ToString(), new WfTicket());
            RequestImplementations component = new();
            SetMember(component, "userConfig", userConfig);
            SetMember(component, "wfHandler", handler);
            SetMember(component, "selectedOwnerOpt", new FwoOwner { Id = -1 });

            await UiRequestCoverageTest.InvokePrivateTask(typeof(RequestImplementations), component, "Select");

            Assert.That(GetMember<FwoOwner>(component, "selectedOwnerOpt").Id, Is.EqualTo(-1));
        }

        [Test]
        public async Task RequestImplementations_Select_UsesDeviceSelectionWhenConfigured()
        {
            UiRequestCoverageTest.RequestCoverageUserConfig userConfig = UiRequestCoverageTest.CreateUserConfig(Roles.Implementer);
            userConfig.ReqOwnerBased = false;
            WfHandler handler = CreateWorkflowHandler(WorkflowPhases.implementation, WfTaskType.access.ToString(), new WfTicket());
            RequestImplementations component = new();
            SetMember(component, "userConfig", userConfig);
            SetMember(component, "wfHandler", handler);
            SetMember(component, "selectedDeviceOpt", new Device { Id = 1, Name = "gw-1" });

            await UiRequestCoverageTest.InvokePrivateTask(typeof(RequestImplementations), component, "Select");

            Assert.That(GetMember<Device>(component, "selectedDeviceOpt").Id, Is.EqualTo(1));
        }

        [Test]
        public async Task RequestReviews_Select_UsesOwnerSelectionWhenConfigured()
        {
            UiRequestCoverageTest.RequestCoverageUserConfig userConfig = UiRequestCoverageTest.CreateUserConfig(Roles.Reviewer);
            userConfig.ReqOwnerBased = true;
            WfHandler handler = CreateWorkflowHandler(WorkflowPhases.review, WfTaskType.access.ToString(), new WfTicket());
            RequestReviews component = new();
            SetMember(component, "userConfig", userConfig);
            SetMember(component, "wfHandler", handler);
            SetMember(component, "selectedOwnerOpt", new FwoOwner { Id = -1 });

            await UiRequestCoverageTest.InvokePrivateTask(typeof(RequestReviews), component, "Select");

            Assert.That(GetMember<FwoOwner>(component, "selectedOwnerOpt").Id, Is.EqualTo(-1));
        }

        [Test]
        public async Task RequestReviews_Select_UsesDeviceSelectionWhenConfigured()
        {
            UiRequestCoverageTest.RequestCoverageUserConfig userConfig = UiRequestCoverageTest.CreateUserConfig(Roles.Reviewer);
            userConfig.ReqOwnerBased = false;
            WfHandler handler = CreateWorkflowHandler(WorkflowPhases.review, WfTaskType.access.ToString(), new WfTicket());
            RequestReviews component = new();
            SetMember(component, "userConfig", userConfig);
            SetMember(component, "wfHandler", handler);
            SetMember(component, "selectedDeviceOpt", new Device { Id = 1, Name = "gw-1" });

            await UiRequestCoverageTest.InvokePrivateTask(typeof(RequestReviews), component, "Select");

            Assert.That(GetMember<Device>(component, "selectedDeviceOpt").Id, Is.EqualTo(1));
        }

        [Test]
        public async Task RequestPlannings_StartFailure_ResetsWorkInProgress()
        {
            await AssertStartActionFailure<RequestPlannings>("StartPlanTask", new WfReqTask(), Roles.Planner);
        }

        [Test]
        public async Task RequestApprovals_StartFailure_ResetsWorkInProgress()
        {
            await AssertStartActionFailure<RequestApprovals>("StartApproveTask", new WfReqTask(), Roles.Approver);
        }

        [Test]
        public async Task RequestImplementations_StartFailure_ResetsWorkInProgress()
        {
            await AssertStartActionFailure<RequestImplementations>("StartImplementTask", new WfImplTask(), Roles.Implementer);
        }

        [Test]
        public async Task RequestReviews_StartFailure_ResetsWorkInProgress()
        {
            await AssertStartActionFailure<RequestReviews>("StartReviewTask", new WfImplTask(), Roles.Reviewer);
        }

        [Test]
        public async Task RequestPlannings_Reset_SetsReadOnlyForNonPlanner()
        {
            UiRequestCoverageTest.RequestCoverageUserConfig userConfig = UiRequestCoverageTest.CreateUserConfig(Roles.Requester);
            WfHandler handler = UiRequestCoverageTest.CreateHandler(new UiRequestCoverageTest.ThrowingApiConnection(), userConfig, WorkflowPhases.planning);
            RequestPlannings component = new();
            SetMember(component, "userConfig", userConfig);
            SetMember(component, "wfHandler", handler);

            await UiRequestCoverageTest.InvokePrivateTask(typeof(RequestPlannings), component, "Reset");

            Assert.That(handler.ReadOnlyMode, Is.True);
        }

        [Test]
        public async Task RequestApprovals_Reset_AllowsApprover()
        {
            UiRequestCoverageTest.RequestCoverageUserConfig userConfig = UiRequestCoverageTest.CreateUserConfig(Roles.Approver);
            WfHandler handler = UiRequestCoverageTest.CreateHandler(new UiRequestCoverageTest.ThrowingApiConnection(), userConfig, WorkflowPhases.approval);
            RequestApprovals component = new();
            SetMember(component, "userConfig", userConfig);
            SetMember(component, "wfHandler", handler);

            await UiRequestCoverageTest.InvokePrivateTask(typeof(RequestApprovals), component, "Reset");

            Assert.That(handler.ReadOnlyMode, Is.False);
        }

        [Test]
        public async Task RequestImplementations_SelectOwner_FiltersVisibleImplementationTasks()
        {
            UiRequestCoverageTest.RequestCoverageUserConfig userConfig = UiRequestCoverageTest.CreateUserConfig(Roles.Implementer);
            WfReqTask reqTask = CreateAccessTask(11, "10.0.0.1", "10.0.0.2", 443);
            reqTask.ImplementationTasks.Add(new WfImplTask { Id = 21, ReqTaskId = reqTask.Id, DeviceId = 1 });
            WfTicket ticket = new() { Id = 7, Tasks = { reqTask } };
            WfHandler handler = CreateWorkflowHandler(WorkflowPhases.implementation, WfTaskType.access.ToString(), ticket);
            RequestImplementations component = new();
            SetMember(component, "userConfig", userConfig);
            SetMember(component, "wfHandler", handler);

            await UiRequestCoverageTest.InvokePrivateTask(typeof(RequestImplementations), component, "SelectOwner", new FwoOwner { Id = -1 });

            Assert.Multiple(() =>
            {
                Assert.That(GetMember<FwoOwner>(component, "selectedOwnerOpt").Id, Is.EqualTo(-1));
                Assert.That(GetMember<bool>(component, "DisplayTickets"), Is.False);
                Assert.That(handler.AllVisibleImplTasks, Has.Count.EqualTo(1));
            });
        }

        [Test]
        public async Task RequestReviews_SelectOwner_FiltersVisibleImplementationTasks()
        {
            UiRequestCoverageTest.RequestCoverageUserConfig userConfig = UiRequestCoverageTest.CreateUserConfig(Roles.Reviewer);
            WfReqTask reqTask = CreateAccessTask(11, "10.0.0.1", "10.0.0.2", 443);
            reqTask.ImplementationTasks.Add(new WfImplTask { Id = 21, ReqTaskId = reqTask.Id, DeviceId = 1 });
            WfTicket ticket = new() { Id = 7, Tasks = { reqTask } };
            WfHandler handler = CreateWorkflowHandler(WorkflowPhases.review, WfTaskType.access.ToString(), ticket);
            RequestReviews component = new();
            SetMember(component, "userConfig", userConfig);
            SetMember(component, "wfHandler", handler);

            await UiRequestCoverageTest.InvokePrivateTask(typeof(RequestReviews), component, "SelectOwner", new FwoOwner { Id = -1 });

            Assert.Multiple(() =>
            {
                Assert.That(GetMember<FwoOwner>(component, "selectedOwnerOpt").Id, Is.EqualTo(-1));
                Assert.That(GetMember<bool>(component, "DisplayTickets"), Is.False);
                Assert.That(handler.AllVisibleImplTasks, Has.Count.EqualTo(1));
            });
        }

        [TestCase(typeof(RequestPlannings))]
        [TestCase(typeof(RequestImplementations))]
        [TestCase(typeof(RequestApprovals))]
        [TestCase(typeof(RequestReviews))]
        [TestCase(typeof(RequestTickets))]
        [TestCase(typeof(RequestTicketsOverview))]
        public async Task RequestPages_ResetFailure_DisplaysError(Type pageType)
        {
            object component = Activator.CreateInstance(pageType)!;
            List<string> messages = [];

            SetMember(component, "userConfig", UiRequestCoverageTest.CreateUserConfig(Roles.Admin));
            SetMember(component, "wfHandler", (WfHandler)null!);
            SetMember(component, "DisplayMessageInUi", (Action<Exception?, string, string, bool>)((_, _, message, _) => messages.Add(message)));

            await UiRequestCoverageTest.InvokePrivateTask(pageType, component, "Reset");

            Assert.That(messages, Has.Count.EqualTo(1));
        }

        [TestCase(typeof(RequestImplementations), "SelectOwner", typeof(FwoOwner))]
        [TestCase(typeof(RequestImplementations), "SelectDevice", typeof(Device))]
        [TestCase(typeof(RequestReviews), "SelectOwner", typeof(FwoOwner))]
        [TestCase(typeof(RequestReviews), "SelectDevice", typeof(Device))]
        public async Task RequestPages_SelectionFailure_DisplaysError(Type pageType, string methodName, Type optionType)
        {
            object component = Activator.CreateInstance(pageType)!;
            List<string> messages = [];

            SetMember(component, "userConfig", UiRequestCoverageTest.CreateUserConfig());
            SetMember(component, "wfHandler", (WfHandler)null!);
            SetMember(component, "DisplayMessageInUi", (Action<Exception?, string, string, bool>)((_, _, message, _) => messages.Add(message)));

            await UiRequestCoverageTest.InvokePrivateTask(pageType, component, methodName, Activator.CreateInstance(optionType)!);

            Assert.That(messages, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task RequestImplementations_Reset_SetsReadOnlyForNonImplementer()
        {
            UiRequestCoverageTest.RequestCoverageUserConfig userConfig = UiRequestCoverageTest.CreateUserConfig(Roles.Requester);
            WfHandler handler = UiRequestCoverageTest.CreateHandler(new UiRequestCoverageTest.ThrowingApiConnection(), userConfig, WorkflowPhases.implementation);
            RequestImplementations component = new();
            SetMember(component, "userConfig", userConfig);
            SetMember(component, "wfHandler", handler);

            await UiRequestCoverageTest.InvokePrivateTask(typeof(RequestImplementations), component, "Reset");

            Assert.That(handler.ReadOnlyMode, Is.True);
        }

        [Test]
        public async Task RequestReviews_Reset_SetsReadOnlyForNonReviewer()
        {
            UiRequestCoverageTest.RequestCoverageUserConfig userConfig = UiRequestCoverageTest.CreateUserConfig(Roles.Requester);
            WfHandler handler = UiRequestCoverageTest.CreateHandler(new UiRequestCoverageTest.ThrowingApiConnection(), userConfig, WorkflowPhases.review);
            RequestReviews component = new();
            SetMember(component, "userConfig", userConfig);
            SetMember(component, "wfHandler", handler);

            await UiRequestCoverageTest.InvokePrivateTask(typeof(RequestReviews), component, "Reset");

            Assert.That(handler.ReadOnlyMode, Is.True);
        }

        [Test]
        public async Task RequestTickets_Reset_AllowsAdmin()
        {
            UiRequestCoverageTest.RequestCoverageUserConfig userConfig = UiRequestCoverageTest.CreateUserConfig(Roles.Admin);
            WfHandler handler = UiRequestCoverageTest.CreateHandler(new UiRequestCoverageTest.ThrowingApiConnection(), userConfig, WorkflowPhases.request);
            RequestTickets component = new();
            SetMember(component, "userConfig", userConfig);
            SetMember(component, "wfHandler", handler);

            await UiRequestCoverageTest.InvokePrivateTask(typeof(RequestTickets), component, "Reset");

            Assert.That(handler.ReadOnlyMode, Is.False);
        }

        [Test]
        public async Task RequestTicketsOverview_Reset_DoesNotFilterAdminTickets()
        {
            UiRequestCoverageTest.RequestCoverageUserConfig userConfig = UiRequestCoverageTest.CreateUserConfig(Roles.Admin);
            WfTicket firstTicket = new() { Id = 1, Requester = new UiUser { DbId = 10 } };
            WfTicket secondTicket = new() { Id = 2, Requester = new UiUser { DbId = 20 } };
            WfHandler handler = UiRequestCoverageTest.CreateHandler(new UiRequestCoverageTest.ThrowingApiConnection(), userConfig, WorkflowPhases.request);
            handler.TicketList = [firstTicket, secondTicket];
            RequestTicketsOverview component = new();
            SetMember(component, "userConfig", userConfig);
            SetMember(component, "wfHandler", handler);

            await UiRequestCoverageTest.InvokePrivateTask(typeof(RequestTicketsOverview), component, "Reset");

            Assert.Multiple(() =>
            {
                Assert.That(handler.ReadOnlyMode, Is.True);
                Assert.That(handler.TicketList, Has.Count.EqualTo(2));
            });
        }

        private static async Task AssertStartActionFailure<TComponent>(string methodName, object task, string role)
            where TComponent : IComponent
        {
            await using BunitContext context = new();
            IRenderedComponent<TComponent> component = RenderWorkflowPage<TComponent>(context, role);
            List<string> messages = [];

            SetMember(component.Instance, "wfHandler", (WfHandler)null!);
            SetMember(component.Instance, "DisplayMessageInUi", (Action<Exception?, string, string, bool>)((_, _, message, _) => messages.Add(message)));

            await (await StartPrivateTask(component, methodName, task));

            Assert.Multiple(() =>
            {
                Assert.That(messages, Has.Count.EqualTo(1));
                Assert.That(GetMember<bool>(component.Instance, "WorkInProgress"), Is.False);
            });
        }

        private static WfHandler CreateTicketRoutingHandler(RequestPageTicketApiConn apiConnection, WorkflowPhases phase)
        {
            RequestWorkflowUserConfig userConfig = new();
            WfHandler handler = new(DefaultInit.DoNothing, userConfig, new ClaimsPrincipal(), apiConnection,
                new MiddlewareClient("http://localhost/"), phase);
            ActionHandler actionHandler = new(apiConnection, handler);
            WfDbAccess dbAccess = new(DefaultInit.DoNothing, userConfig, apiConnection, actionHandler, false, phase);
            SetMember(handler, "dbAcc", dbAccess);
            handler.MasterStateMatrix = new StateMatrix
            {
                LowestEndState = 10,
                PhaseActive =
                {
                    [WorkflowPhases.request] = true,
                    [WorkflowPhases.approval] = true,
                    [WorkflowPhases.planning] = true,
                    [WorkflowPhases.implementation] = true,
                    [WorkflowPhases.review] = true,
                    [WorkflowPhases.recertification] = true
                },
                IsLastActivePhase = false
            };
            return handler;
        }

        private sealed class RequestPageTicketApiConn : SimulatedApiConnection
        {
            public WfTicket Ticket { get; set; } = new();

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, FWO.Api.Client.QueryChunkingOptions? chunkingOptions = null)
            {
                if (query == RequestQueries.getTicketById)
                {
                    return Task.FromResult((QueryResponseType)(object)Ticket);
                }

                throw new AssertionException($"Unexpected query: {query}");
            }
        }

        private sealed class RequestPageGlobalStateMatrix : GlobalStateMatrix
        {
            public override Task Init(ApiConnection apiConnection, WfTaskType taskType = WfTaskType.master)
            {
                GlobalMatrix = new Dictionary<WorkflowPhases, StateMatrix>
                {
                    [WorkflowPhases.request] = new StateMatrix { LowestEndState = 100 },
                    [WorkflowPhases.approval] = new StateMatrix { LowestEndState = 100 },
                    [WorkflowPhases.planning] = new StateMatrix { LowestEndState = 100 },
                    [WorkflowPhases.implementation] = new StateMatrix { LowestEndState = 100 },
                    [WorkflowPhases.review] = new StateMatrix { LowestEndState = 100 },
                    [WorkflowPhases.recertification] = new StateMatrix { LowestEndState = 100 }
                };
                return Task.CompletedTask;
            }
        }

        private sealed class RequestPageNavigationManager : NavigationManager
        {
            public RequestPageNavigationManager()
            {
                Initialize("http://localhost/", "http://localhost/");
            }

            public string? LastUri { get; private set; }

            protected override void NavigateToCore(string uri, bool forceLoad)
            {
                LastUri = uri;
            }
        }

    }
}
