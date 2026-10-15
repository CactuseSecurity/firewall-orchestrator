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
            return RenderPromoteObject(context, states, stateMatrix, statefulObject, false, roles);
        }

        internal static IRenderedComponent<PromoteObject> RenderPromoteObject(
            BunitContext context,
            WfStateDict states,
            StateMatrix stateMatrix,
            WfStatefulObject statefulObject,
            bool withComment,
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
                    .Add(p => p.WithComment, withComment)
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

        internal sealed class RequestWorkflowApiConn : SimulatedApiConnection
        {
            public List<string> Queries { get; } = [];
            public List<FlowNwObject> FlowNwObjects { get; set; } = [];
            public List<FlowNwGroup> FlowNwGroups { get; set; } = [];
            public List<FlowSvcObject> FlowSvcObjects { get; set; } = [];
            public List<FlowSvcGroup> FlowSvcGroups { get; set; } = [];
            public List<FlowTimeObject> FlowTimeObjects { get; set; } = [];
            public List<Management> Managements { get; set; } = [];

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
                    return Task.FromResult((QueryResponseType)(object)Managements);
                }
                if (query == FlowQueries.getFlowRequestNwObjectCatalog)
                {
                    return Task.FromResult((QueryResponseType)(object)FlowNwObjects);
                }
                if (query == FlowQueries.getFlowAddressGroups)
                {
                    return Task.FromResult((QueryResponseType)(object)FlowNwGroups);
                }
                if (query == FlowQueries.getFlowRequestSvcObjectCatalog)
                {
                    return Task.FromResult((QueryResponseType)(object)FlowSvcObjects);
                }
                if (query == FlowQueries.getFlowServiceGroups)
                {
                    return Task.FromResult((QueryResponseType)(object)FlowSvcGroups);
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
