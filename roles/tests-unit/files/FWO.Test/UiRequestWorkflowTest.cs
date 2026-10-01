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
    [TestFixture]
    internal partial class UiRequestWorkflowTest
    {
        internal static void SetMember(object instance, string memberName, object? value)
        {
            Type type = instance.GetType();
            PropertyInfo? property = type.GetProperty(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (property != null)
            {
                property.SetValue(instance, value);
                return;
            }

            FieldInfo? field = type.GetField(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(instance, value);
                return;
            }

            throw new MissingFieldException(type.FullName, memberName);
        }

        internal static T GetMember<T>(object instance, string memberName)
        {
            Type type = instance.GetType();
            PropertyInfo? property = type.GetProperty(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (property != null)
            {
                return (T)property.GetValue(instance)!;
            }

            FieldInfo? field = type.GetField(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
            {
                return (T)field.GetValue(instance)!;
            }

            throw new MissingFieldException(type.FullName, memberName);
        }

        internal static MethodInfo GetPrivateMethod(Type type, string methodName)
        {
            return type.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
                ?? throw new MissingMethodException(type.FullName, methodName);
        }

        internal static async Task InvokePrivateTask(object instance, string methodName, params object[] args)
        {
            Task task = (Task)GetPrivateMethod(instance.GetType(), methodName).Invoke(instance, args)!;
            await task;
        }

        internal static async Task<Task> StartPrivateTask<TComponent>(IRenderedComponent<TComponent> component, string methodName, params object[] args)
            where TComponent : IComponent
        {
            Task? runningTask = null;
            await component.InvokeAsync(() =>
            {
                runningTask = (Task)GetPrivateMethod(typeof(TComponent), methodName).Invoke(component.Instance, args)!;
            });
            return runningTask!;
        }

        internal static bool InvokePrivateBool(object instance, string methodName, params object[] args)
        {
            return (bool)GetPrivateMethod(instance.GetType(), methodName).Invoke(instance, args)!;
        }

        internal static bool HasNetworkFlowReference(NwObjectElement element)
        {
            return element.FlowNetworkObjectId.HasValue || element.FlowNetworkGroupId.HasValue;
        }

        internal static bool HasServiceFlowReference(NwServiceElement element)
        {
            return element.FlowServiceObjectId.HasValue || element.FlowServiceGroupId.HasValue;
        }

        internal static void SetMatrix(WfHandler handler, string taskType, StateMatrix matrix)
        {
            FieldInfo? field = typeof(WfHandler).GetField("stateMatrixDict", BindingFlags.NonPublic | BindingFlags.Instance);
            StateMatrixDict dict = (StateMatrixDict)(field?.GetValue(handler) ?? new StateMatrixDict());
            dict.Matrices[taskType] = matrix;
        }

        private static StateMatrix CreateWorkflowMatrix(bool planningActive = true)
        {
            return new()
            {
                LowestInputState = 0,
                LowestStartedState = 2,
                LowestEndState = 10,
                PhaseActive =
                {
                    [WorkflowPhases.request] = true,
                    [WorkflowPhases.approval] = true,
                    [WorkflowPhases.planning] = planningActive,
                    [WorkflowPhases.verification] = false,
                    [WorkflowPhases.implementation] = true,
                    [WorkflowPhases.review] = false,
                    [WorkflowPhases.recertification] = false
                }
            };
        }

        internal static WfHandler CreateWorkflowHandler(WorkflowPhases phase, string taskType, WfTicket ticket)
        {
            WfHandler handler = new()
            {
                Phase = phase,
                ActTicket = ticket,
                MasterStateMatrix = CreateWorkflowMatrix()
            };
            handler.userConfig.User.Dn = "cn=current";
            handler.userConfig.User.DbId = 10;
            handler.TicketList.Add(ticket);
            SetMatrix(handler, taskType, CreateWorkflowMatrix(planningActive: phase == WorkflowPhases.planning));
            return handler;
        }

        internal static DisplayReqTaskTable CreateReqTaskTable(WfHandler handler, WorkflowPhases phase)
        {
            DisplayReqTaskTable component = new();
            SetMember(component, nameof(DisplayReqTaskTable.WfHandler), handler);
            SetMember(component, nameof(DisplayReqTaskTable.Phase), phase);
            return component;
        }

        internal static DisplayTicketTable CreateTicketTable(WfHandler handler, WorkflowPhases phase)
        {
            DisplayTicketTable component = new();
            SetMember(component, nameof(DisplayTicketTable.WfHandler), handler);
            SetMember(component, nameof(DisplayTicketTable.Phase), phase);
            return component;
        }

        internal static DisplayTicket CreateDisplayTicket(
            WfHandler handler,
            WorkflowPhases phase,
            WfStateDict? states = null,
            UserConfig? userConfig = null,
            Func<Task>? resetParent = null)
        {
            DisplayTicket component = new();
            SetMember(component, nameof(DisplayTicket.WfHandler), handler);
            SetMember(component, nameof(DisplayTicket.Phase), phase);
            SetMember(component, nameof(DisplayTicket.States), states ?? new WfStateDict());
            SetMember(component, nameof(DisplayTicket.ResetParent), resetParent ?? DefaultInit.DoNothing);
            SetMember(component, "userConfig", userConfig ?? new RequestWorkflowUserConfig());
            return component;
        }

        internal static IRenderedComponent<DisplayTicket> RenderDisplayTicket(
            BunitContext context,
            WfHandler handler,
            WorkflowPhases phase,
            WfStateDict states,
            Func<WfReqTask, Task>? startPhase = null,
            Func<WfImplTask, Task>? startImplPhase = null)
        {
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddLocalization();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton<AuthenticationStateProvider>(new RequestWorkflowAuthStateProvider(Roles.Requester));
            context.Services.TryAddSingleton<ApiConnection>(new RequestWorkflowApiConn());
            context.Services.AddSingleton(new MiddlewareClient("http://localhost/"));
            context.Services.TryAddSingleton<UserConfig>(new RequestWorkflowUserConfig());
            context.Services.TryAddSingleton<DomEventService>();
            context.Services.TryAddSingleton<IEventMediator>(new EventMediator());

            IRenderedComponent<CascadingAuthenticationState> wrapper = context.Render<CascadingAuthenticationState>(parameters => parameters
                .AddChildContent<DisplayTicket>(child => child
                    .Add(p => p.Phase, phase)
                    .Add(p => p.States, states)
                    .Add(p => p.WfHandler, handler)
                    .Add(p => p.ResetParent, DefaultInit.DoNothing)
                    .Add(p => p.StartPhase, startPhase ?? (Func<WfReqTask, Task>)DefaultInit.DoNothing)
                    .Add(p => p.StartImplPhase, startImplPhase ?? (Func<WfImplTask, Task>)DefaultInit.DoNothing)));

            return wrapper.FindComponent<DisplayTicket>();
        }

        internal static IRenderedComponent<DisplayReqTaskTable> RenderDisplayReqTaskTable(
            BunitContext context,
            WfHandler handler,
            WorkflowPhases phase,
            WfStateDict states,
            params string[] roles)
        {
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddLocalization();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton<AuthenticationStateProvider>(new RequestWorkflowAuthStateProvider(roles.Length > 0 ? roles : [Roles.Requester]));
            context.Services.TryAddSingleton<ApiConnection>(new RequestWorkflowApiConn());
            context.Services.AddSingleton(new MiddlewareClient("http://localhost/"));
            context.Services.TryAddSingleton<UserConfig>(new RequestWorkflowUserConfig());
            context.Services.TryAddSingleton<DomEventService>();
            context.Services.TryAddSingleton<IEventMediator>(new EventMediator());

            IRenderedComponent<CascadingAuthenticationState> wrapper = context.Render<CascadingAuthenticationState>(parameters => parameters
                .AddChildContent<DisplayReqTaskTable>(child => child
                    .Add(p => p.Phase, phase)
                    .Add(p => p.States, states)
                    .Add(p => p.WfHandler, handler)
                    .Add(p => p.ResetParent, DefaultInit.DoNothing)
                    .Add(p => p.StartPhase, (Func<WfReqTask, Task>)DefaultInit.DoNothing)
                    .Add(p => p.StartImplPhase, (Func<WfImplTask, Task>)DefaultInit.DoNothing)));

            return wrapper.FindComponent<DisplayReqTaskTable>();
        }

        internal static IRenderedComponent<DisplayTicketTable> RenderDisplayTicketTable(
            BunitContext context,
            WfHandler handler,
            WorkflowPhases phase,
            WfStateDict states,
            params string[] roles)
        {
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddLocalization();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton<AuthenticationStateProvider>(new RequestWorkflowAuthStateProvider(roles.Length > 0 ? roles : [Roles.Requester]));
            context.Services.TryAddSingleton<ApiConnection>(new RequestWorkflowApiConn());
            context.Services.AddSingleton(new MiddlewareClient("http://localhost/"));
            context.Services.TryAddSingleton<UserConfig>(new RequestWorkflowUserConfig());
            context.Services.TryAddSingleton<DomEventService>();
            context.Services.TryAddSingleton<IEventMediator>(new EventMediator());

            IRenderedComponent<CascadingAuthenticationState> wrapper = context.Render<CascadingAuthenticationState>(parameters => parameters
                .AddChildContent<DisplayTicketTable>(child => child
                    .Add(p => p.Phase, phase)
                    .Add(p => p.States, states)
                    .Add(p => p.WfHandler, handler)
                    .Add(p => p.ResetParent, DefaultInit.DoNothing)
                    .Add(p => p.StartPhase, (Func<WfReqTask, Task>)DefaultInit.DoNothing)
                    .Add(p => p.StartImplPhase, (Func<WfImplTask, Task>)DefaultInit.DoNothing)));

            return wrapper.FindComponent<DisplayTicketTable>();
        }

        internal static WfReqTask CreateAccessTask(long id, string sourceIp, string destinationIp, int servicePort)
        {
            return new()
            {
                Id = id,
                TicketId = 100,
                Title = $"Task {id}",
                TaskType = WfTaskType.access.ToString(),
                RuleAction = 1,
                Tracking = 1,
                Elements =
                [
                    new WfReqElement { Id = id * 10 + 1, TaskId = id, Field = ElemFieldType.source.ToString(), IpString = sourceIp },
                    new WfReqElement { Id = id * 10 + 2, TaskId = id, Field = ElemFieldType.destination.ToString(), IpString = destinationIp },
                    new WfReqElement { Id = id * 10 + 3, TaskId = id, Field = ElemFieldType.service.ToString(), Port = servicePort, ProtoId = 6 }
                ]
            };
        }

        internal static IRenderedComponent<DisplayRequestTask> RenderDisplayRequestTask(
            BunitContext context,
            WfHandler handler,
            WfStateDict states,
            params string[] roles)
        {
            return RenderDisplayRequestTask(context, handler, states, null, roles);
        }

        internal static IRenderedComponent<DisplayRequestTask> RenderDisplayRequestTask(
            BunitContext context,
            WfHandler handler,
            WfStateDict states,
            Func<WfImplTask, Task>? startImplPhase,
            params string[] roles)
        {
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton<AuthenticationStateProvider>(new RequestWorkflowAuthStateProvider(roles));
            context.Services.TryAddSingleton<ApiConnection>(new RequestWorkflowApiConn());
            context.Services.AddSingleton(new MiddlewareClient("http://localhost/"));
            context.Services.TryAddSingleton<UserConfig>(new RequestWorkflowUserConfig());
            context.Services.TryAddSingleton<DomEventService>();
            context.Services.TryAddSingleton<IEventMediator>(new EventMediator());

            IRenderedComponent<CascadingAuthenticationState> wrapper = context.Render<CascadingAuthenticationState>(parameters => parameters
                .AddChildContent<DisplayRequestTask>(child => child
                    .Add(p => p.Phase, WorkflowPhases.request)
                    .Add(p => p.States, states)
                    .Add(p => p.WfHandler, handler)
                    .Add(p => p.ResetParent, DefaultInit.DoNothing)
                    .Add(p => p.StartImplPhase, startImplPhase ?? (Func<WfImplTask, Task>)DefaultInit.DoNothing)));

            return wrapper.FindComponent<DisplayRequestTask>();
        }

        private static IRenderedComponent<TComponent> RenderWorkflowPage<TComponent>(BunitContext context, params string[] roles)
            where TComponent : IComponent
        {
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton<AuthenticationStateProvider>(new RequestWorkflowAuthStateProvider(roles));
            context.Services.TryAddSingleton<ApiConnection>(new RequestWorkflowApiConn());
            context.Services.AddSingleton(new MiddlewareClient("http://localhost/"));
            context.Services.TryAddSingleton<UserConfig>(new RequestWorkflowUserConfig());
            context.Services.TryAddSingleton<DomEventService>();
            context.Services.TryAddSingleton<IEventMediator>(new EventMediator());

            IRenderedComponent<CascadingAuthenticationState> wrapper = context.Render<CascadingAuthenticationState>(parameters => parameters
                .AddChildContent<TComponent>());

            return wrapper.FindComponent<TComponent>();
        }

        internal static IRenderedComponent<DisplayImplementationTask> RenderDisplayImplementationTask(
            BunitContext context,
            WfHandler handler,
            WfStateDict states,
            params string[] roles)
        {
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton<AuthenticationStateProvider>(new RequestWorkflowAuthStateProvider(roles));
            context.Services.TryAddSingleton<ApiConnection>(new RequestWorkflowApiConn());
            context.Services.TryAddSingleton<UserConfig>(new RequestWorkflowUserConfig());
            context.Services.TryAddSingleton<DomEventService>();
            context.Services.TryAddSingleton<IEventMediator>(new EventMediator());

            IRenderedComponent<CascadingAuthenticationState> wrapper = context.Render<CascadingAuthenticationState>(parameters => parameters
                .AddChildContent<DisplayImplementationTask>(child => child
                    .Add(p => p.Phase, WorkflowPhases.implementation)
                    .Add(p => p.States, states)
                    .Add(p => p.WfHandler, handler)
                    .Add(p => p.ResetParent, DefaultInit.DoNothing)
                    .Add(p => p.StateMatrix, new StateMatrix())
                    .Add(p => p.IncludePopups, false)));

            return wrapper.FindComponent<DisplayImplementationTask>();
        }

        internal static IRenderedComponent<PromoteObject> RenderPromoteObject(
            BunitContext context,
            WfStateDict states,
            StateMatrix stateMatrix,
            WfStatefulObject statefulObject,
            params string[] roles)
        {
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton<AuthenticationStateProvider>(new RequestWorkflowAuthStateProvider(roles));
            context.Services.AddSingleton(new MiddlewareClient("http://localhost/"));
            context.Services.TryAddSingleton<UserConfig>(new RequestWorkflowUserConfig());
            context.Services.TryAddSingleton<DomEventService>();
            context.Services.TryAddSingleton<IEventMediator>(new EventMediator());

            IRenderedComponent<CascadingAuthenticationState> wrapper = context.Render<CascadingAuthenticationState>(parameters => parameters
                .AddChildContent<PromoteObject>(child => child
                    .Add(p => p.Promote, true)
                    .Add(p => p.States, states)
                    .Add(p => p.StateMatrix, stateMatrix)
                    .Add(p => p.StatefulObject, statefulObject)
                    .Add(p => p.ObjectName, "Task")
                    .Add(p => p.CloseParent, DefaultInit.DoNothing)
                    .Add(p => p.CancelParent, DefaultInit.DoNothingSync)
                    .Add(p => p.Save, (Func<WfStatefulObject, Task>)DefaultInit.DoNothing)));

            return wrapper.FindComponent<PromoteObject>();
        }

        [Test]
        public async Task RequestPlannings_StartPlanTask_StampsStartAndOpensPlanMode()
        {
            string taskType = WfTaskType.access.ToString();
            WfReqTask reqTask = new()
            {
                Id = 11,
                TicketId = 7,
                TaskType = taskType,
                StateId = 0
            };
            WfTicket ticket = new() { Id = 7, Tasks = { reqTask } };
            WfHandler handler = CreateWorkflowHandler(WorkflowPhases.planning, taskType, ticket);
            DateTime beforeStart = DateTime.Now;

            await using BunitContext context = new();
            IRenderedComponent<RequestPlannings> component = RenderWorkflowPage<RequestPlannings>(context, Roles.Planner);
            SetMember(component.Instance, "wfHandler", handler);

            await (await StartPrivateTask(component, "StartPlanTask", reqTask));

            Assert.Multiple(() =>
            {
                Assert.That(reqTask.Start, Is.Not.Null);
                Assert.That(reqTask.Start!.Value, Is.GreaterThanOrEqualTo(beforeStart));
                Assert.That(handler.ActReqTask.Start, Is.EqualTo(reqTask.Start));
                Assert.That(handler.ActReqTask.CurrentHandler, Is.SameAs(handler.userConfig.User));
                Assert.That(handler.PlanReqTaskMode, Is.True);
            });
        }

        [Test]
        public async Task RequestApprovals_StartApproveTask_OpensApproveModeWithoutStampingStart()
        {
            string taskType = WfTaskType.access.ToString();
            WfReqTask reqTask = new()
            {
                Id = 11,
                TicketId = 7,
                TaskType = taskType,
                StateId = 0
            };
            WfTicket ticket = new() { Id = 7, Tasks = { reqTask } };
            WfHandler handler = CreateWorkflowHandler(WorkflowPhases.approval, taskType, ticket);

            await using BunitContext context = new();
            IRenderedComponent<RequestApprovals> component = RenderWorkflowPage<RequestApprovals>(context, Roles.Approver);
            SetMember(component.Instance, "wfHandler", handler);

            await (await StartPrivateTask(component, "StartApproveTask", reqTask));

            Assert.Multiple(() =>
            {
                Assert.That(reqTask.Start, Is.Null);
                Assert.That(handler.ActReqTask.Start, Is.Null);
                Assert.That(handler.ActReqTask.CurrentHandler, Is.SameAs(handler.userConfig.User));
                Assert.That(handler.ApproveReqTaskMode, Is.True);
            });
        }

        [Test]
        public async Task RequestImplementations_StartImplementTask_StampsStartClearsStopAndOpensImplementMode()
        {
            string taskType = WfTaskType.access.ToString();
            WfReqTask reqTask = new()
            {
                Id = 11,
                TicketId = 7,
                TaskType = taskType,
                StateId = 0
            };
            WfImplTask implTask = new()
            {
                Id = 21,
                TicketId = 7,
                ReqTaskId = 11,
                TaskType = taskType,
                StateId = 0,
                Stop = new DateTime(2026, 1, 1)
            };
            reqTask.ImplementationTasks.Add(implTask);
            WfTicket ticket = new() { Id = 7, Tasks = { reqTask } };
            WfHandler handler = CreateWorkflowHandler(WorkflowPhases.implementation, taskType, ticket);
            DateTime beforeStart = DateTime.Now;

            await using BunitContext context = new();
            IRenderedComponent<RequestImplementations> component = RenderWorkflowPage<RequestImplementations>(context, Roles.Implementer);
            SetMember(component.Instance, "wfHandler", handler);

            await (await StartPrivateTask(component, "StartImplementTask", implTask));

            Assert.Multiple(() =>
            {
                Assert.That(implTask.Start, Is.Not.Null);
                Assert.That(implTask.Start!.Value, Is.GreaterThanOrEqualTo(beforeStart));
                Assert.That(implTask.Stop, Is.Null);
                Assert.That(handler.ActImplTask.Start, Is.EqualTo(implTask.Start));
                Assert.That(handler.ActImplTask.Stop, Is.Null);
                Assert.That(handler.ActImplTask.CurrentHandler, Is.SameAs(handler.userConfig.User));
                Assert.That(handler.ImplementImplTaskMode, Is.True);
            });
        }

        [Test]
        public async Task RequestReviews_StartReviewTask_OpensReviewModeWithoutChangingImplementationTimes()
        {
            string taskType = WfTaskType.access.ToString();
            DateTime existingStart = new(2026, 1, 1);
            DateTime existingStop = new(2026, 1, 2);
            WfReqTask reqTask = new()
            {
                Id = 11,
                TicketId = 7,
                TaskType = taskType,
                StateId = 0
            };
            WfImplTask implTask = new()
            {
                Id = 21,
                TicketId = 7,
                ReqTaskId = 11,
                TaskType = taskType,
                StateId = 0,
                Start = existingStart,
                Stop = existingStop
            };
            reqTask.ImplementationTasks.Add(implTask);
            WfTicket ticket = new() { Id = 7, Tasks = { reqTask } };
            WfHandler handler = CreateWorkflowHandler(WorkflowPhases.review, taskType, ticket);

            await using BunitContext context = new();
            IRenderedComponent<RequestReviews> component = RenderWorkflowPage<RequestReviews>(context, Roles.Reviewer);
            SetMember(component.Instance, "wfHandler", handler);

            await (await StartPrivateTask(component, "StartReviewTask", implTask));

            Assert.Multiple(() =>
            {
                Assert.That(implTask.Start, Is.EqualTo(existingStart));
                Assert.That(implTask.Stop, Is.EqualTo(existingStop));
                Assert.That(handler.ActImplTask.Start, Is.EqualTo(existingStart));
                Assert.That(handler.ActImplTask.Stop, Is.EqualTo(existingStop));
                Assert.That(handler.ActImplTask.CurrentHandler, Is.SameAs(handler.userConfig.User));
                Assert.That(handler.ReviewImplTaskMode, Is.True);
            });
        }

        [Test]
        public async Task RequestImplementations_SelectDeviceShowsImplementationTable()
        {
            string taskType = WfTaskType.access.ToString();
            WfImplTask implTask = new()
            {
                Id = 21,
                TicketId = 7,
                ReqTaskId = 11,
                TaskType = taskType,
                DeviceId = 1
            };
            WfReqTask reqTask = new()
            {
                Id = 11,
                TicketId = 7,
                TaskType = taskType,
                ImplementationTasks = [implTask]
            };
            WfTicket ticket = new() { Id = 7, Tasks = { reqTask } };
            WfHandler handler = CreateWorkflowHandler(WorkflowPhases.implementation, taskType, ticket);
            handler.userConfig.ReqOwnerBased = false;

            await using BunitContext context = new();
            IRenderedComponent<RequestImplementations> component = RenderWorkflowPage<RequestImplementations>(context, Roles.Implementer);
            SetMember(component.Instance, "wfHandler", handler);

            await component.InvokeAsync(async () => await InvokePrivateTask(component.Instance, "SelectDevice", new Device { Id = 1, Name = "gw-1" }));

            Assert.Multiple(() =>
            {
                Assert.That(GetMember<bool>(component.Instance, "DisplayTickets"), Is.False);
                Assert.That(handler.AllVisibleImplTasks, Has.Count.EqualTo(1));
                Assert.That(component.Markup, Does.Not.Contain("create_ticket"));
            });
        }

        [Test]
        public async Task RequestReviews_SelectDeviceShowsImplementationTable()
        {
            string taskType = WfTaskType.access.ToString();
            WfImplTask implTask = new()
            {
                Id = 21,
                TicketId = 7,
                ReqTaskId = 11,
                TaskType = taskType,
                DeviceId = 1
            };
            WfReqTask reqTask = new()
            {
                Id = 11,
                TicketId = 7,
                TaskType = taskType,
                ImplementationTasks = [implTask]
            };
            WfTicket ticket = new() { Id = 7, Tasks = { reqTask } };
            WfHandler handler = CreateWorkflowHandler(WorkflowPhases.review, taskType, ticket);
            handler.userConfig.ReqOwnerBased = false;

            await using BunitContext context = new();
            IRenderedComponent<RequestReviews> component = RenderWorkflowPage<RequestReviews>(context, Roles.Reviewer);
            SetMember(component.Instance, "wfHandler", handler);

            await component.InvokeAsync(async () => await InvokePrivateTask(component.Instance, "SelectDevice", new Device { Id = 1, Name = "gw-1" }));

            Assert.Multiple(() =>
            {
                Assert.That(GetMember<bool>(component.Instance, "DisplayTickets"), Is.False);
                Assert.That(handler.AllVisibleImplTasks, Has.Count.EqualTo(1));
            });
        }

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

        internal sealed class RequestWorkflowApiConn : SimulatedApiConnection
        {
            public List<string> Queries { get; } = [];
            public List<FlowNwObject> FlowNwObjects { get; set; } = [];
            public List<FlowSvcObject> FlowSvcObjects { get; set; } = [];
            public List<FlowTimeObject> FlowTimeObjects { get; set; } = [];

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, FWO.Api.Client.QueryChunkingOptions? chunkingOptions = null)
            {
                Queries.Add(query);
                if (query == StmQueries.getRuleActions)
                {
                    return Task.FromResult((QueryResponseType)(object)new List<RuleAction>());
                }
                if (query == StmQueries.getTracking)
                {
                    return Task.FromResult((QueryResponseType)(object)new List<Tracking>());
                }
                if (query == StmQueries.getIpProtocols)
                {
                    return Task.FromResult((QueryResponseType)(object)new List<IpProtocol>());
                }
                if (query == DeviceQueries.getManagementNames)
                {
                    return Task.FromResult((QueryResponseType)(object)new List<Management>());
                }
                if (query == FlowQueries.getFlowRequestNwObjectCatalog)
                {
                    return Task.FromResult((QueryResponseType)(object)FlowNwObjects);
                }
                if (query == FlowQueries.getFlowRequestSvcObjectCatalog)
                {
                    return Task.FromResult((QueryResponseType)(object)FlowSvcObjects);
                }
                if (query == FlowQueries.getFlowRequestTimeObjectCatalog)
                {
                    return Task.FromResult((QueryResponseType)(object)FlowTimeObjects);
                }

                throw new NotImplementedException($"Unexpected query: {query}");
            }
        }

        internal sealed class RequestWorkflowUserConfig : SimulatedUserConfig
        {
            public RequestWorkflowUserConfig()
            {
                ReqAvailableTaskTypes = "[\"generic\",\"access\",\"rule_modify\",\"rule_delete\",\"new_interface\",\"group_create\"]";
                ReqAllowedChangesByApprover = "{}";
            }

            public override string GetText(string key)
            {
                return DummyTranslate.TryGetValue(key, out string? value) ? value : key;
            }
        }

        private sealed class RequestWorkflowAuthStateProvider : AuthenticationStateProvider
        {
            private readonly ClaimsPrincipal principal;

            public RequestWorkflowAuthStateProvider(params string[] roles)
            {
                List<Claim> claims = [];
                foreach (string role in roles)
                {
                    claims.Add(new Claim(ClaimTypes.Role, role));
                }
                ClaimsIdentity identity = new(claims, "Test");
                principal = new ClaimsPrincipal(identity);
            }

            public override Task<AuthenticationState> GetAuthenticationStateAsync()
            {
                return Task.FromResult(new AuthenticationState(principal));
            }
        }
    }
}
