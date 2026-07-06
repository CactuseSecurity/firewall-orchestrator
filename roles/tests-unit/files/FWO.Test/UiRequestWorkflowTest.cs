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
    internal class UiRequestWorkflowTest
    {
        private static void SetMember(object instance, string memberName, object? value)
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

        private static T GetMember<T>(object instance, string memberName)
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

        private static MethodInfo GetPrivateMethod(Type type, string methodName)
        {
            return type.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
                ?? throw new MissingMethodException(type.FullName, methodName);
        }

        private static async Task InvokePrivateTask(object instance, string methodName, params object[] args)
        {
            Task task = (Task)GetPrivateMethod(instance.GetType(), methodName).Invoke(instance, args)!;
            await task;
        }

        private static async Task<Task> StartPrivateTask<TComponent>(IRenderedComponent<TComponent> component, string methodName, params object[] args)
            where TComponent : IComponent
        {
            Task? runningTask = null;
            await component.InvokeAsync(() =>
            {
                runningTask = (Task)GetPrivateMethod(typeof(TComponent), methodName).Invoke(component.Instance, args)!;
            });
            return runningTask!;
        }

        private static bool InvokePrivateBool(object instance, string methodName, params object[] args)
        {
            return (bool)GetPrivateMethod(instance.GetType(), methodName).Invoke(instance, args)!;
        }

        private static bool HasNetworkFlowReference(NwObjectElement element)
        {
            return element.FlowNetworkObjectId.HasValue || element.FlowNetworkGroupId.HasValue;
        }

        private static bool HasServiceFlowReference(NwServiceElement element)
        {
            return element.FlowServiceObjectId.HasValue || element.FlowServiceGroupId.HasValue;
        }

        private static void SetMatrix(WfHandler handler, string taskType, StateMatrix matrix)
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

        private static WfHandler CreateWorkflowHandler(WorkflowPhases phase, string taskType, WfTicket ticket)
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

        private static DisplayReqTaskTable CreateReqTaskTable(WfHandler handler, WorkflowPhases phase)
        {
            DisplayReqTaskTable component = new();
            SetMember(component, nameof(DisplayReqTaskTable.WfHandler), handler);
            SetMember(component, nameof(DisplayReqTaskTable.Phase), phase);
            return component;
        }

        private static DisplayTicketTable CreateTicketTable(WfHandler handler, WorkflowPhases phase)
        {
            DisplayTicketTable component = new();
            SetMember(component, nameof(DisplayTicketTable.WfHandler), handler);
            SetMember(component, nameof(DisplayTicketTable.Phase), phase);
            return component;
        }

        private static DisplayTicket CreateDisplayTicket(
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

        private static IRenderedComponent<DisplayTicket> RenderDisplayTicket(
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

        private static IRenderedComponent<DisplayReqTaskTable> RenderDisplayReqTaskTable(
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

        private static WfReqTask CreateAccessTask(long id, string sourceIp, string destinationIp, int servicePort)
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

        private static IRenderedComponent<DisplayRequestTask> RenderDisplayRequestTask(
            BunitContext context,
            WfHandler handler,
            WfStateDict states,
            params string[] roles)
        {
            return RenderDisplayRequestTask(context, handler, states, null, roles);
        }

        private static IRenderedComponent<DisplayRequestTask> RenderDisplayRequestTask(
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

        private static IRenderedComponent<DisplayImplementationTask> RenderDisplayImplementationTask(
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

        private static IRenderedComponent<PromoteObject> RenderPromoteObject(
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
        public void DisplayTicketTable_ApprovalPhase_AllowsEditInPhaseRange()
        {
            WfHandler handler = new()
            {
                ReadOnlyMode = false,
                MasterStateMatrix = new StateMatrix
                {
                    LowestInputState = 49,
                    LowestEndState = 99
                }
            };
            DisplayTicketTable component = CreateTicketTable(handler, WorkflowPhases.approval);
            MethodInfo? method = typeof(DisplayTicketTable).GetMethod("CanEditTicketInPhase", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(method, Is.Not.Null);

            bool canEdit = (bool)method!.Invoke(component, [new WfTicket { StateId = 60 }])!;

            Assert.That(canEdit, Is.True);
        }

        [Test]
        public void DisplayTicketTable_ApprovalPhase_UsesDetailsOutsidePhaseRange()
        {
            WfHandler handler = new()
            {
                ReadOnlyMode = false,
                MasterStateMatrix = new StateMatrix
                {
                    LowestInputState = 49,
                    LowestEndState = 99
                }
            };
            DisplayTicketTable component = CreateTicketTable(handler, WorkflowPhases.approval);
            MethodInfo? method = typeof(DisplayTicketTable).GetMethod("CanEditTicketInPhase", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(method, Is.Not.Null);

            bool canEdit = (bool)method!.Invoke(component, [new WfTicket { StateId = 99 }])!;

            Assert.That(canEdit, Is.False);
        }

        [Test]
        public void DisplayTicketTable_ReadOnlyMode_UsesDetailsInPhaseRange()
        {
            WfHandler handler = new()
            {
                ReadOnlyMode = true,
                MasterStateMatrix = new StateMatrix
                {
                    LowestInputState = 49,
                    LowestEndState = 99
                }
            };
            DisplayTicketTable component = CreateTicketTable(handler, WorkflowPhases.approval);
            MethodInfo? method = typeof(DisplayTicketTable).GetMethod("CanEditTicketInPhase", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(method, Is.Not.Null);

            bool canEdit = (bool)method!.Invoke(component, [new WfTicket { StateId = 60 }])!;

            Assert.That(canEdit, Is.False);
        }

        [Test]
        public async Task DisplayTicket_InitSaveTicketCopiesSelectedPriorityAndOpensSavePopup()
        {
            WfHandler handler = new()
            {
                ActTicket = new WfTicket
                {
                    Id = 10,
                    Title = "Ticket",
                    Priority = 1,
                    Tasks = [new WfReqTask { Id = 1, Title = "Task" }]
                }
            };
            DisplayTicket component = CreateDisplayTicket(handler, WorkflowPhases.request);
            SetMember(component, "selectedPriority", new WfPriority { NumPrio = 2, Name = "High" });

            await InvokePrivateTask(component, "InitSaveTicket");

            Assert.Multiple(() =>
            {
                Assert.That(handler.ActTicket.Priority, Is.EqualTo(2));
                Assert.That(handler.DisplaySaveTicketMode, Is.True);
            });
        }

        [Test]
        public async Task DisplayTicket_CancelEditForNewTicketWithTasksShowsConfirmPopup()
        {
            int resetCalls = 0;
            WfHandler handler = new()
            {
                EditTicketMode = true,
                ActTicket = new WfTicket
                {
                    Id = 0,
                    Title = "New ticket",
                    Tasks = [new WfReqTask { Id = 1, Title = "Task" }]
                }
            };
            DisplayTicket component = CreateDisplayTicket(handler, WorkflowPhases.request, resetParent: () =>
            {
                resetCalls++;
                return Task.CompletedTask;
            });

            await InvokePrivateTask(component, "CancelEdit");

            Assert.Multiple(() =>
            {
                Assert.That(GetMember<bool>(component, "ConfirmCancelMode"), Is.True);
                Assert.That(resetCalls, Is.EqualTo(0));
            });
        }

        [Test]
        public async Task DisplayTicket_CancelEditForExistingTicketClosesWithoutConfirmPopup()
        {
            int resetCalls = 0;
            WfHandler handler = new()
            {
                EditTicketMode = true,
                ActTicket = new WfTicket
                {
                    Id = 10,
                    Title = "Existing ticket",
                    Tasks = [new WfReqTask { Id = 1, Title = "Task" }]
                }
            };
            DisplayTicket component = CreateDisplayTicket(handler, WorkflowPhases.request, resetParent: () =>
            {
                resetCalls++;
                return Task.CompletedTask;
            });

            await InvokePrivateTask(component, "CancelEdit");

            Assert.Multiple(() =>
            {
                Assert.That(GetMember<bool>(component, "ConfirmCancelMode"), Is.False);
                Assert.That(resetCalls, Is.EqualTo(1));
            });
        }

        [Test]
        public void DisplayTicket_CanSaveTicketChangesUsesApproverAllowedFields()
        {
            ApproverAllowedChangesConfig allowedChanges = new();
            allowedChanges.SetTicketField(WorkflowEditableFieldKeys.Title, true);
            RequestWorkflowUserConfig userConfig = new()
            {
                ReqAllowedChangesByApprover = allowedChanges.ToConfigValue()
            };
            WfHandler handler = new()
            {
                ActTicket = new WfTicket { Id = 10, Title = "Ticket" }
            };
            DisplayTicket component = CreateDisplayTicket(handler, WorkflowPhases.approval, userConfig: userConfig);

            bool canSave = InvokePrivateBool(component, "CanSaveTicketChanges");
            handler.ReadOnlyMode = true;
            bool canSaveReadOnly = InvokePrivateBool(component, "CanSaveTicketChanges");

            Assert.Multiple(() =>
            {
                Assert.That(canSave, Is.True);
                Assert.That(canSaveReadOnly, Is.False);
            });
        }

        [Test]
        public void DisplayTicket_CheckPromoteTicketUsesAllowedMasterTransitions()
        {
            WfHandler handler = new()
            {
                ActTicket = new WfTicket { Id = 10, Title = "Ticket", StateId = 2 },
                MasterStateMatrix = new StateMatrix
                {
                    LowestStartedState = 1,
                    LowestEndState = 5,
                    Matrix = { [2] = [3] }
                }
            };
            DisplayTicket component = CreateDisplayTicket(handler, WorkflowPhases.request);

            bool canPromote = InvokePrivateBool(component, "CheckPromoteTicket");
            handler.MasterStateMatrix.Matrix[2] = [2];
            bool canPromoteSameStateOnly = InvokePrivateBool(component, "CheckPromoteTicket");

            Assert.Multiple(() =>
            {
                Assert.That(canPromote, Is.True);
                Assert.That(canPromoteSameStateOnly, Is.False);
            });
        }

        [Test]
        public void DisplayTicket_CheckPromoteTicketDerivesStateFromTasks()
        {
            WfHandler handler = new()
            {
                ActTicket = new WfTicket
                {
                    Id = 10,
                    Title = "Ticket",
                    StateId = 3,
                    Tasks =
                    [
                        new WfReqTask { Id = 1, StateId = 4 },
                        new WfReqTask { Id = 2, StateId = 4 }
                    ]
                },
                MasterStateMatrix = new StateMatrix
                {
                    LowestInputState = 1,
                    LowestStartedState = 3,
                    LowestEndState = 5
                }
            };
            DisplayTicket component = CreateDisplayTicket(handler, WorkflowPhases.request);

            bool canPromote = InvokePrivateBool(component, "CheckPromoteTicket");
            handler.ActTicket.StateId = 4;
            bool canPromoteWhenDerivedStateMatches = InvokePrivateBool(component, "CheckPromoteTicket");

            Assert.Multiple(() =>
            {
                Assert.That(canPromote, Is.True);
                Assert.That(canPromoteWhenDerivedStateMatches, Is.False);
            });
        }

        [Test]
        public async Task DisplayTicket_StartRequestPhase_DelegatesTaskAndBlocksReentry()
        {
            TaskCompletionSource<object?> started = new(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<object?> release = new(TaskCreationOptions.RunContinuationsAsynchronously);
            WfReqTask reqTask = new() { Id = 1, Title = "Request task" };
            WfReqTask? observedReqTask = null;
            int callCount = 0;
            WfHandler handler = new()
            {
                ActTicket = new WfTicket
                {
                    Id = 10,
                    Title = "Ticket",
                    Tasks = [reqTask]
                }
            };
            using BunitContext context = new();
            IRenderedComponent<DisplayTicket> component = RenderDisplayTicket(
                context,
                handler,
                WorkflowPhases.request,
                new WfStateDict(),
                async task =>
                {
                    callCount++;
                    observedReqTask = task;
                    started.SetResult(null);
                    await release.Task;
                });

            Task runningTask = await StartPrivateTask(component, "StartRequestPhase", reqTask);
            await started.Task.WaitAsync(TimeSpan.FromSeconds(1));

            Task blockedTask = await StartPrivateTask(component, "StartRequestPhase", reqTask);
            await blockedTask;

            Assert.Multiple(() =>
            {
                Assert.That(GetMember<bool>(component.Instance, "WorkInProgress"), Is.True);
                Assert.That(callCount, Is.EqualTo(1));
            });

            release.SetResult(null);
            await runningTask;

            Assert.Multiple(() =>
            {
                Assert.That(GetMember<bool>(component.Instance, "WorkInProgress"), Is.False);
                Assert.That(callCount, Is.EqualTo(1));
                Assert.That(observedReqTask, Is.SameAs(reqTask));
            });
        }

        [Test]
        public async Task DisplayTicket_StartImplementationPhase_DelegatesTaskAndBlocksReentry()
        {
            TaskCompletionSource<object?> started = new(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<object?> release = new(TaskCreationOptions.RunContinuationsAsynchronously);
            WfImplTask implTask = new() { Id = 21, Title = "Implementation task" };
            WfImplTask? observedImplTask = null;
            int callCount = 0;
            WfHandler handler = new()
            {
                ActTicket = new WfTicket
                {
                    Id = 10,
                    Title = "Ticket",
                    Tasks = [new WfReqTask { Id = 1, Title = "Request task", ImplementationTasks = { implTask } }]
                }
            };
            using BunitContext context = new();
            IRenderedComponent<DisplayTicket> component = RenderDisplayTicket(
                context,
                handler,
                WorkflowPhases.implementation,
                new WfStateDict(),
                startImplPhase: async task =>
                {
                    callCount++;
                    observedImplTask = task;
                    started.SetResult(null);
                    await release.Task;
                });

            Task runningTask = await StartPrivateTask(component, "StartImplementationPhase", implTask);
            await started.Task.WaitAsync(TimeSpan.FromSeconds(1));

            Task blockedTask = await StartPrivateTask(component, "StartImplementationPhase", implTask);
            await blockedTask;

            Assert.Multiple(() =>
            {
                Assert.That(GetMember<bool>(component.Instance, "WorkInProgress"), Is.True);
                Assert.That(callCount, Is.EqualTo(1));
            });

            release.SetResult(null);
            await runningTask;

            Assert.Multiple(() =>
            {
                Assert.That(GetMember<bool>(component.Instance, "WorkInProgress"), Is.False);
                Assert.That(callCount, Is.EqualTo(1));
                Assert.That(observedImplTask, Is.SameAs(implTask));
            });
        }

        [Test]
        public async Task DisplayRequestTask_StartImplementationPhase_DelegatesTaskAndBlocksReentry()
        {
            TaskCompletionSource<object?> started = new(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<object?> release = new(TaskCreationOptions.RunContinuationsAsynchronously);
            WfReqTask reqTask = CreateAccessTask(1, "10.0.0.1", "10.0.0.2", 443);
            WfImplTask implTask = new() { Id = 31, Title = "Implementation task", ReqTaskId = reqTask.Id };
            reqTask.ImplementationTasks.Add(implTask);
            WfImplTask? observedImplTask = null;
            int callCount = 0;
            WfHandler handler = new()
            {
                ActReqTask = reqTask,
                ActTicket = new WfTicket
                {
                    Id = 10,
                    Title = "Ticket",
                    Tasks = [reqTask]
                }
            };
            using BunitContext context = new();
            IRenderedComponent<DisplayRequestTask> component = RenderDisplayRequestTask(
                context,
                handler,
                new WfStateDict(),
                async task =>
                {
                    callCount++;
                    observedImplTask = task;
                    started.SetResult(null);
                    await release.Task;
                },
                Roles.Requester);

            Task runningTask = await StartPrivateTask(component, "StartImplementationPhase", implTask);
            await started.Task.WaitAsync(TimeSpan.FromSeconds(1));

            Task blockedTask = await StartPrivateTask(component, "StartImplementationPhase", implTask);
            await blockedTask;

            Assert.Multiple(() =>
            {
                Assert.That(GetMember<bool>(component.Instance, "WorkInProgress"), Is.True);
                Assert.That(callCount, Is.EqualTo(1));
            });

            release.SetResult(null);
            await runningTask;

            Assert.Multiple(() =>
            {
                Assert.That(GetMember<bool>(component.Instance, "WorkInProgress"), Is.False);
                Assert.That(callCount, Is.EqualTo(1));
                Assert.That(observedImplTask, Is.SameAs(implTask));
            });
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

        [Test]
        public async Task DisplayRequestTask_NewTask_AddsTaskTypeDropdownComparedToExistingTask()
        {
            WfHandler existingHandler = new()
            {
                DisplayReqTaskMode = true,
                EditReqTaskMode = true,
                AddReqTaskMode = false,
                ActReqTask = new WfReqTask
                {
                    Id = 12,
                    Title = "Task",
                    TaskType = WfTaskType.generic.ToString(),
                    StateId = 0,
                    FreeText = "text"
                }
            };
            existingHandler.ActTicket.Tasks.Add(existingHandler.ActReqTask);
            WfStateDict states = new() { Name = { [0] = "Draft" } };
            int existingDropdownCount;
            await using (BunitContext existingContext = new())
            {
                IRenderedComponent<DisplayRequestTask> existingComponent = RenderDisplayRequestTask(existingContext, existingHandler, states, Roles.Requester);
                existingDropdownCount = existingComponent.FindAll("input[id^='dropdown-input-']").Count;
            }

            WfHandler newHandler = new()
            {
                DisplayReqTaskMode = true,
                EditReqTaskMode = true,
                AddReqTaskMode = true,
                ActReqTask = new WfReqTask
                {
                    Id = 0,
                    Title = "Task",
                    TaskType = WfTaskType.generic.ToString(),
                    StateId = 0
                }
            };
            newHandler.ActTicket.Tasks.Add(newHandler.ActReqTask);
            int newDropdownCount;
            await using (BunitContext newContext = new())
            {
                IRenderedComponent<DisplayRequestTask> newComponent = RenderDisplayRequestTask(newContext, newHandler, states, Roles.Requester);
                newDropdownCount = newComponent.FindAll("input[id^='dropdown-input-']").Count;
            }

            Assert.That(newDropdownCount, Is.GreaterThan(existingDropdownCount));
        }

        [Test]
        public async Task DisplayRequestTask_NewInterface_RequestingOwnerDropdownUsesOwnOwners()
        {
            RequestWorkflowUserConfig userConfig = new()
            {
                ReqAvailableTaskTypes = "[\"new_interface\"]"
            };
            userConfig.User.Ownerships = [1, 3];
            WfHandler handler = new()
            {
                DisplayReqTaskMode = true,
                EditReqTaskMode = true,
                AddReqTaskMode = true,
                AllOwners =
                [
                    new FwoOwner { Id = 0, Name = "All" },
                    new FwoOwner { Id = 1, Name = "Own A" },
                    new FwoOwner { Id = 2, Name = "Foreign" },
                    new FwoOwner { Id = 3, Name = "Own B" }
                ],
                ActReqTask = new WfReqTask
                {
                    Id = 0,
                    Title = "Task",
                    TaskType = WfTaskType.new_interface.ToString(),
                    StateId = 0
                }
            };
            handler.ActTicket.Tasks.Add(handler.ActReqTask);
            WfStateDict states = new() { Name = { [0] = "Draft" } };
            await using BunitContext context = new();
            context.Services.AddSingleton<UserConfig>(userConfig);

            IRenderedComponent<DisplayRequestTask> component = RenderDisplayRequestTask(context, handler, states, Roles.Requester);

            List<int> ownerIds = GetMember<IEnumerable<FwoOwner>>(component.Instance, "NewInterfaceOwnerOptions")
                .Select(owner => owner.Id)
                .ToList();
            List<int> requestingOwnerIds = GetMember<IEnumerable<FwoOwner>>(component.Instance, "RequestingOwnerOptions")
                .Select(owner => owner.Id)
                .ToList();
            Assert.Multiple(() =>
            {
                Assert.That(ownerIds, Is.EqualTo(new List<int> { 1, 2, 3 }));
                Assert.That(requestingOwnerIds, Is.EqualTo(new List<int> { 1, 3 }));
            });
        }

        [Test]
        public async Task DisplayRequestTask_NewInterface_AdminRequestingOwnerDropdownUsesAllOwners()
        {
            RequestWorkflowUserConfig userConfig = new()
            {
                ReqAvailableTaskTypes = "[\"new_interface\"]"
            };
            userConfig.User.Roles = [Roles.Admin];
            userConfig.User.Ownerships = [0];
            WfHandler handler = new()
            {
                DisplayReqTaskMode = true,
                EditReqTaskMode = true,
                AddReqTaskMode = true,
                AllOwners =
                [
                    new FwoOwner { Id = 0, Name = "All" },
                    new FwoOwner { Id = 1, Name = "App A" },
                    new FwoOwner { Id = 2, Name = "App B" },
                    new FwoOwner { Id = 3, Name = "App C" }
                ],
                ActReqTask = new WfReqTask
                {
                    Id = 0,
                    Title = "Task",
                    TaskType = WfTaskType.new_interface.ToString(),
                    StateId = 0
                }
            };
            handler.ActTicket.Tasks.Add(handler.ActReqTask);
            WfStateDict states = new() { Name = { [0] = "Draft" } };
            await using BunitContext context = new();
            context.Services.AddSingleton<UserConfig>(userConfig);

            IRenderedComponent<DisplayRequestTask> component = RenderDisplayRequestTask(context, handler, states, Roles.Admin);

            List<int> ownerIds = GetMember<IEnumerable<FwoOwner>>(component.Instance, "NewInterfaceOwnerOptions")
                .Select(owner => owner.Id)
                .ToList();
            List<int> requestingOwnerIds = GetMember<IEnumerable<FwoOwner>>(component.Instance, "RequestingOwnerOptions")
                .Select(owner => owner.Id)
                .ToList();
            Assert.Multiple(() =>
            {
                Assert.That(ownerIds, Is.EqualTo(new List<int> { 1, 2, 3 }));
                Assert.That(requestingOwnerIds, Is.EqualTo(new List<int> { 1, 2, 3 }));
            });
        }

        [Test]
        public async Task DisplayAccessElements_ReadOnlyObjectEntriesPreferGroupName()
        {
            await using BunitContext context = new();
            context.Services.AddSingleton<ApiConnection>(new RequestWorkflowApiConn());
            context.Services.AddSingleton<UserConfig>(new RequestWorkflowUserConfig());
            List<NwObjectElement> sources =
            [
                new() { NetworkId = 42, GroupName = "AR-Displayed", Name = "HiddenSourceName" }
            ];
            List<NwObjectElement> destinations =
            [
                new() { NetworkId = 43, GroupName = "AR-Destination", Name = "HiddenDestinationName" }
            ];
            List<NwServiceElement> services =
            [
                new() { ServiceId = 44, GroupName = "SG-Displayed", Name = "HiddenServiceName" }
            ];

            IRenderedComponent<DisplayAccessElements> component = context.Render<DisplayAccessElements>(parameters => parameters
                .Add(p => p.Sources, sources)
                .Add(p => p.Destinations, destinations)
                .Add(p => p.Services, services)
                .Add(p => p.IpProtos, new List<IpProtocol>())
                .Add(p => p.EditMode, false));

            Assert.Multiple(() =>
            {
                Assert.That(component.Markup, Does.Contain("AR-Displayed"));
                Assert.That(component.Markup, Does.Contain("AR-Destination"));
                Assert.That(component.Markup, Does.Contain("SG-Displayed"));
                Assert.That(component.Markup, Does.Not.Contain("HiddenSourceName"));
                Assert.That(component.Markup, Does.Not.Contain("HiddenDestinationName"));
                Assert.That(component.Markup, Does.Not.Contain("HiddenServiceName"));
            });
        }

        [Test]
        public async Task DisplayAccessElements_LoadsFlowObjectsForSearch_WhenFlowDbEnabled()
        {
            await using BunitContext context = new();
            RequestWorkflowApiConn apiConn = new()
            {
                FlowNwObjects =
                [
                    new FlowNwObject { Id = 101, Name = "Flow Source", ShowInRequestModule = true },
                    new FlowNwObject { Id = 102, Name = "Hidden Source", ShowInRequestModule = false }
                ],
                FlowSvcObjects =
                [
                    new FlowSvcObject { Id = 201, Name = "Flow Service", ProtoId = 6, ShowInRequestModule = true },
                    new FlowSvcObject { Id = 202, Name = "Removed Service", ProtoId = 6, ShowInRequestModule = true, RemovedDate = DateTime.UtcNow }
                ]
            };
            context.Services.AddSingleton<ApiConnection>(apiConn);
            context.Services.AddSingleton<UserConfig>(new RequestWorkflowUserConfig { ReqUseFlowDb = true });
            context.Services.AddSingleton<DomEventService>();

            IRenderedComponent<DisplayAccessElements> component = context.Render<DisplayAccessElements>(parameters => parameters
                .Add(p => p.Sources, new List<NwObjectElement>())
                .Add(p => p.Destinations, new List<NwObjectElement>())
                .Add(p => p.Services, new List<NwServiceElement>())
                .Add(p => p.IpProtos, new List<IpProtocol>())
                .Add(p => p.EditMode, true));

            List<NetworkObject> loadedObjects = GetMember<List<NetworkObject>>(component.Instance, "nwObjects");
            List<NetworkService> loadedServices = GetMember<List<NetworkService>>(component.Instance, "nwServices");
            IReadOnlyList<IRenderedComponent<Dropdown<NetworkObject>>> networkDropdowns = component.FindComponents<Dropdown<NetworkObject>>();
            IReadOnlyList<IRenderedComponent<Dropdown<NetworkService>>> serviceDropdowns = component.FindComponents<Dropdown<NetworkService>>();

            Assert.Multiple(() =>
            {
                Assert.That(apiConn.Queries, Does.Contain(FlowQueries.getFlowRequestNwObjectCatalog));
                Assert.That(apiConn.Queries, Does.Contain(FlowQueries.getFlowRequestSvcObjectCatalog));
                Assert.That(loadedObjects.Select(obj => obj.Name), Is.EqualTo(new[] { "Flow Source" }));
                Assert.That(loadedObjects.Single().FlowNetworkObjectId, Is.EqualTo(101));
                Assert.That(loadedServices.Select(svc => svc.Name), Is.EqualTo(new[] { "Flow Service" }));
                Assert.That(loadedServices.Single().FlowServiceObjectId, Is.EqualTo(201));
                Assert.That(networkDropdowns, Has.Count.EqualTo(2));
                Assert.That(networkDropdowns.All(dropdown => dropdown.Instance.Nullable), Is.True);
                Assert.That(serviceDropdowns.Single().Instance.Nullable, Is.True);
            });
        }

        [Test]
        public async Task DisplayAccessElements_UsesPortAndProtocolNameForFlowServiceFallbackName()
        {
            await using BunitContext context = new();
            RequestWorkflowApiConn apiConn = new()
            {
                FlowSvcObjects =
                [
                    new FlowSvcObject { Id = 201, Name = "", PortStart = 443, PortEnd = 8443, ProtoId = 6, ShowInRequestModule = true }
                ]
            };
            context.Services.AddSingleton<ApiConnection>(apiConn);
            context.Services.AddSingleton<UserConfig>(new RequestWorkflowUserConfig { ReqUseFlowDb = true });
            context.Services.AddSingleton<DomEventService>();

            IRenderedComponent<DisplayAccessElements> component = context.Render<DisplayAccessElements>(parameters => parameters
                .Add(p => p.Sources, new List<NwObjectElement>())
                .Add(p => p.Destinations, new List<NwObjectElement>())
                .Add(p => p.Services, new List<NwServiceElement>())
                .Add(p => p.IpProtos, new List<IpProtocol> { new() { Id = 6, Name = "tcp" } })
                .Add(p => p.EditMode, true));

            List<NetworkService> loadedServices = GetMember<List<NetworkService>>(component.Instance, "nwServices");

            Assert.That(loadedServices.Single().Name, Is.EqualTo("443-8443/tcp"));
        }

        [Test]
        public async Task DisplayAccessElements_SelectedFlowObjectsAreAddedWithFlowIds()
        {
            await using BunitContext context = new();
            context.Services.AddSingleton<ApiConnection>(new RequestWorkflowApiConn
            {
                FlowNwObjects = [new FlowNwObject { Id = 101, Name = "Flow Source", IpStart = "10.0.0.1/32", ShowInRequestModule = true }],
                FlowSvcObjects = [new FlowSvcObject { Id = 201, Name = "Flow Service", PortStart = 443, ProtoId = 6, ShowInRequestModule = true }]
            });
            context.Services.AddSingleton<UserConfig>(new RequestWorkflowUserConfig { ReqUseFlowDb = true });
            context.Services.AddSingleton<DomEventService>();
            List<NwObjectElement> sources = [];
            List<NwObjectElement> sourcesToAdd = [];
            List<NwServiceElement> services = [];
            List<NwServiceElement> servicesToAdd = [];
            IRenderedComponent<DisplayAccessElements> component = context.Render<DisplayAccessElements>(parameters => parameters
                .Add(p => p.Sources, sources)
                .Add(p => p.SourcesToAdd, sourcesToAdd)
                .Add(p => p.Destinations, new List<NwObjectElement>())
                .Add(p => p.Services, services)
                .Add(p => p.ServicesToAdd, servicesToAdd)
                .Add(p => p.IpProtos, new List<IpProtocol>())
                .Add(p => p.EditMode, true));

            NetworkObject flowObject = GetMember<List<NetworkObject>>(component.Instance, "nwObjects").Single();
            NetworkService flowService = GetMember<List<NetworkService>>(component.Instance, "nwServices").Single();
            await component.InvokeAsync(() => SetMember(component.Instance, "newSourceNetwork", flowObject));
            await component.InvokeAsync(() => SetMember(component.Instance, "newService", flowService));
            IReadOnlyList<IRenderedComponent<IpSelector>> ipSelectors = component.FindComponents<IpSelector>();
            IRenderedComponent<ServiceSelector> serviceSelector = component.FindComponent<ServiceSelector>();

            Assert.Multiple(() =>
            {
                Assert.That(sources, Is.Empty);
                Assert.That(sourcesToAdd.Single().NetworkId, Is.Null);
                Assert.That(sourcesToAdd.Single().FlowNetworkObjectId, Is.EqualTo(101));
                Assert.That(sourcesToAdd.Single().IpString, Is.EqualTo("10.0.0.1/32"));
                Assert.That(services, Is.Empty);
                Assert.That(servicesToAdd.Single().ServiceId, Is.Null);
                Assert.That(servicesToAdd.Single().FlowServiceObjectId, Is.EqualTo(201));
                Assert.That(servicesToAdd.Single().Port, Is.EqualTo(443));
                Assert.That(servicesToAdd.Single().ProtoId, Is.EqualTo(6));
                Assert.That(ipSelectors, Has.Count.EqualTo(2));
                Assert.That(ipSelectors.SelectMany(selector => selector.Instance.IpAddresses).Any(HasNetworkFlowReference), Is.False);
                Assert.That(serviceSelector.Instance.Services.Any(HasServiceFlowReference), Is.False);
                Assert.That(component.Markup, Does.Contain("Flow Source"));
                Assert.That(component.Markup, Does.Contain("Flow Service"));
            });
        }

        [Test]
        public async Task DisplayAccessElements_CatalogOnlySelectionsAreDisplayedInEditList()
        {
            await using BunitContext context = new();
            context.Services.AddSingleton<ApiConnection>(new RequestWorkflowApiConn
            {
                FlowNwObjects = [new FlowNwObject { Id = 101, Name = "Flow Source", IpStart = "10.0.0.1/32", ShowInRequestModule = true }],
                FlowSvcObjects = [new FlowSvcObject { Id = 201, Name = "Flow Service", PortStart = 443, ProtoId = 6, ShowInRequestModule = true }]
            });
            context.Services.AddSingleton<UserConfig>(new RequestWorkflowUserConfig
            {
                ReqUseFlowDb = true,
                ReqFlowIntegration = new FlowIntegrationConfig
                {
                    SelectObjects = FlowIntegrationObjectSelectionOptions.FromFlowDb,
                    SelectServices = FlowIntegrationObjectSelectionOptions.FromFlowDb,
                    SelectTimeObjects = FlowIntegrationObjectSelectionOptions.Both,
                    TimeObjectPrecision = FlowIntegrationTimePrecisionOptions.Seconds
                }.ToConfigValue()
            });
            context.Services.AddSingleton<DomEventService>();
            List<NwObjectElement> sourcesToAdd = [];
            List<NwServiceElement> servicesToAdd = [];

            IRenderedComponent<DisplayAccessElements> component = context.Render<DisplayAccessElements>(parameters => parameters
                .Add(p => p.Sources, new List<NwObjectElement>())
                .Add(p => p.SourcesToAdd, sourcesToAdd)
                .Add(p => p.Destinations, new List<NwObjectElement>())
                .Add(p => p.Services, new List<NwServiceElement>())
                .Add(p => p.ServicesToAdd, servicesToAdd)
                .Add(p => p.IpProtos, new List<IpProtocol>())
                .Add(p => p.EditMode, true));

            NetworkObject flowObject = GetMember<List<NetworkObject>>(component.Instance, "nwObjects").Single();
            NetworkService flowService = GetMember<List<NetworkService>>(component.Instance, "nwServices").Single();
            await component.InvokeAsync(() => SetMember(component.Instance, "newSourceNetwork", flowObject));
            await component.InvokeAsync(() => SetMember(component.Instance, "newService", flowService));

            Assert.Multiple(() =>
            {
                Assert.That(component.FindComponents<IpSelector>(), Is.Empty);
                Assert.That(component.FindComponents<ServiceSelector>(), Is.Empty);
                Assert.That(sourcesToAdd.Single().Name, Is.EqualTo("Flow Source"));
                Assert.That(servicesToAdd.Single().Name, Is.EqualTo("Flow Service"));
                Assert.That(component.Markup, Does.Contain("Flow Source"));
                Assert.That(component.Markup, Does.Contain("Flow Service"));
            });
        }

        [Test]
        public async Task DisplayAccessElements_ServiceCatalogStaysInServiceColumn_WhenObjectCatalogDisabled()
        {
            await using BunitContext context = new();
            context.Services.AddSingleton<ApiConnection>(new RequestWorkflowApiConn
            {
                FlowSvcObjects = [new FlowSvcObject { Id = 201, Name = "Flow Service", PortStart = 443, ProtoId = 6, ShowInRequestModule = true }]
            });
            context.Services.AddSingleton<UserConfig>(new RequestWorkflowUserConfig
            {
                ReqUseFlowDb = true,
                ReqFlowIntegration = new FlowIntegrationConfig
                {
                    SelectObjects = FlowIntegrationObjectSelectionOptions.Manually,
                    SelectServices = FlowIntegrationObjectSelectionOptions.FromFlowDb,
                    SelectTimeObjects = FlowIntegrationObjectSelectionOptions.Both,
                    TimeObjectPrecision = FlowIntegrationTimePrecisionOptions.Seconds
                }.ToConfigValue()
            });
            context.Services.AddSingleton<DomEventService>();

            IRenderedComponent<DisplayAccessElements> component = context.Render<DisplayAccessElements>(parameters => parameters
                .Add(p => p.Sources, new List<NwObjectElement>())
                .Add(p => p.Destinations, new List<NwObjectElement>())
                .Add(p => p.Services, new List<NwServiceElement>())
                .Add(p => p.IpProtos, new List<IpProtocol>())
                .Add(p => p.EditMode, true));

            IReadOnlyList<IElement> catalogColumns = component.FindAll(".bg-secondary > .form-group.row.col-sm-12 > .col-sm-4");

            Assert.Multiple(() =>
            {
                Assert.That(component.FindComponents<Dropdown<NetworkObject>>(), Is.Empty);
                Assert.That(component.FindComponents<Dropdown<NetworkService>>(), Has.Count.EqualTo(1));
                Assert.That(catalogColumns, Has.Count.EqualTo(3));
                Assert.That(catalogColumns[0].TextContent, Does.Not.Contain("service_catalog"));
                Assert.That(catalogColumns[1].TextContent, Does.Not.Contain("service_catalog"));
                Assert.That(catalogColumns[2].TextContent, Does.Contain("service_catalog"));
            });
        }

        [Test]
        public async Task IpSelector_DisplaysObjectReferenceNamesInMixedList()
        {
            await using BunitContext context = new();
            context.Services.AddSingleton<UserConfig>(new RequestWorkflowUserConfig());
            List<NwObjectElement> ipAddresses =
            [
                new NwObjectElement("10.0.0.1", 1),
                new() { Name = "Flow Source", FlowNetworkObjectId = 101, TaskId = 1 },
                new() { Name = "Classic Source", NetworkId = 201, TaskId = 1 }
            ];

            IRenderedComponent<IpSelector> component = context.Render<IpSelector>(parameters => parameters
                .Add(p => p.IpAddresses, ipAddresses)
                .Add(p => p.WithLabel, false));

            Assert.Multiple(() =>
            {
                Assert.That(component.Markup, Does.Contain("10.0.0.1"));
                Assert.That(component.Markup, Does.Contain("Flow Source"));
                Assert.That(component.Markup, Does.Contain("Classic Source"));
            });
        }

        [Test]
        public async Task ServiceSelector_DisplaysServiceReferenceNamesInMixedList()
        {
            await using BunitContext context = new();
            context.Services.AddSingleton<UserConfig>(new RequestWorkflowUserConfig());
            context.Services.AddSingleton<DomEventService>();
            List<NwServiceElement> services =
            [
                new() { Port = 443, ProtoId = 6, TaskId = 1 },
                new() { Name = "Flow Service", FlowServiceObjectId = 201, TaskId = 1 },
                new() { Name = "Classic Service", ServiceId = 301, TaskId = 1 }
            ];
            List<IpProtocol> ipProtos = [new() { Id = 6, Name = "tcp" }];

            IRenderedComponent<ServiceSelector> component = context.Render<ServiceSelector>(parameters => parameters
                .Add(p => p.Services, services)
                .Add(p => p.IpProtos, ipProtos)
                .Add(p => p.WithLabel, false));

            Assert.Multiple(() =>
            {
                Assert.That(component.Markup, Does.Contain("443"));
                Assert.That(component.Markup, Does.Contain("Flow Service"));
                Assert.That(component.Markup, Does.Contain("Classic Service"));
            });
        }

        [Test]
        public async Task ServiceSelector_RequiresPortForProtocolsWithPorts()
        {
            await using BunitContext context = new();
            context.Services.AddSingleton<UserConfig>(new RequestWorkflowUserConfig());
            context.Services.AddSingleton<DomEventService>();
            List<NwServiceElement> servicesToAdd = [];
            List<IpProtocol> ipProtos =
            [
                new() { Id = 6, Name = "tcp" },
                new() { Id = 50, Name = "esp" }
            ];

            IRenderedComponent<ServiceSelector> component = context.Render<ServiceSelector>(parameters => parameters
                .Add(p => p.Services, new List<NwServiceElement>())
                .Add(p => p.ServicesToAdd, servicesToAdd)
                .Add(p => p.IpProtos, ipProtos)
                .Add(p => p.WithLabel, false));

            await component.InvokeAsync(() => SetMember(component.Instance, "actPort", null));
            await component.InvokeAsync(() => SetMember(component.Instance, "actPortEnd", null));
            component.Find("button.btn-success").Click();

            Assert.That(servicesToAdd, Is.Empty);
        }

        [Test]
        public async Task ServiceSelector_AllowsAddingProtocolWithoutPortsAndAfterRemoval()
        {
            await using BunitContext context = new();
            context.Services.AddSingleton<UserConfig>(new RequestWorkflowUserConfig());
            context.Services.AddSingleton<DomEventService>();
            List<NwServiceElement> servicesToAdd = [];
            List<IpProtocol> ipProtos =
            [
                new() { Id = 6, Name = "tcp" },
                new() { Id = 50, Name = "esp" }
            ];

            IRenderedComponent<ServiceSelector> component = context.Render<ServiceSelector>(parameters => parameters
                .Add(p => p.Services, new List<NwServiceElement>())
                .Add(p => p.ServicesToAdd, servicesToAdd)
                .Add(p => p.IpProtos, ipProtos)
                .Add(p => p.WithLabel, false));

            await component.InvokeAsync(() => SetMember(component.Instance, "actPort", 443));
            component.Find("button.btn-success").Click();

            IRenderedComponent<Dropdown<int>> protoDropdown = component.FindComponent<Dropdown<int>>();
            await (await StartPrivateTask(protoDropdown, "SelectElement", 50));

            Assert.That(component.FindComponents<PortRangeInput>(), Is.Empty);

            component.Find("button.btn-success").Click();
            Assert.Multiple(() =>
            {
                Assert.That(servicesToAdd, Has.Count.EqualTo(2));
                Assert.That(servicesToAdd[0].ProtoId, Is.EqualTo(6));
                Assert.That(servicesToAdd[0].Port, Is.EqualTo(443));
                Assert.That(servicesToAdd[1].ProtoId, Is.EqualTo(50));
                Assert.That(servicesToAdd[1].Port, Is.EqualTo(0));
                Assert.That(servicesToAdd[1].PortEnd, Is.Null);
            });

            servicesToAdd.RemoveAll(service => service.ProtoId == 50);
            await (await StartPrivateTask(protoDropdown, "SelectElement", 50));
            component.Find("button.btn-success").Click();

            Assert.That(servicesToAdd.Count(service => service.ProtoId == 50), Is.EqualTo(1));
        }

        [Test]
        public async Task DisplayRequestTask_AccessTaskWithAllDevicesDisplaysAll()
        {
            WfReqTask task = CreateAccessTask(12, "10.0.0.1", "10.0.1.1", 80);
            task.SetDeviceList([WfReqTaskBase.kAllDevicesId]);
            WfHandler handler = new()
            {
                DisplayReqTaskMode = true,
                ReadOnlyMode = true,
                ActReqTask = task,
                ActTicket = new WfTicket { Id = 100, Tasks = [task] },
                Devices = [new Device { Id = 1, Name = "FW-1" }]
            };
            handler.ActStateMatrix.PhaseActive[WorkflowPhases.planning] = false;
            WfStateDict states = new() { Name = { [0] = "Draft" } };

            await using BunitContext context = new();
            context.Services.AddSingleton<UserConfig>(new RequestWorkflowUserConfig
            {
                ReqAutoCreateImplTasks = AutoCreateImplTaskOptions.enterInReqTask
            });
            IRenderedComponent<DisplayRequestTask> component = RenderDisplayRequestTask(context, handler, states, Roles.Requester);

            component.WaitForAssertion(() =>
            {
                IEnumerable<Device> selectedDevices = GetMember<IEnumerable<Device>>(component.Instance, "selectedDevices");
                Assert.That(selectedDevices.Single().Id, Is.EqualTo(WfReqTaskBase.kAllDevicesId));
            });
        }

        [Test]
        public void DisplayRequestTask_FlowServiceReferenceSkipsManualPortValidation()
        {
            WfReqElement flowServiceElement = new()
            {
                Field = ElemFieldType.service.ToString(),
                FlowServiceObjectId = 201,
                Port = 0
            };
            WfHandler handler = new()
            {
                ActReqTask = new WfReqTask
                {
                    TaskType = WfTaskType.access.ToString(),
                    Elements = [flowServiceElement]
                },
                ActStateMatrix = new StateMatrix
                {
                    PhaseActive = { [WorkflowPhases.planning] = true }
                }
            };
            DisplayRequestTask component = new();
            SetMember(component, nameof(DisplayRequestTask.WfHandler), handler);
            SetMember(component, "userConfig", new RequestWorkflowUserConfig());
            SetMember(component, "actSources", new List<NwObjectElement> { new("10.0.0.1", 1) });
            SetMember(component, "actDestinations", new List<NwObjectElement> { new("10.0.1.1", 1) });
            SetMember(component, "actServices", new List<NwServiceElement> { new() { FlowServiceObjectId = 201, Name = "Flow Service" } });

            bool isValid = InvokePrivateBool(component, "RejectInvalidAccessTask");

            Assert.That(isValid, Is.True);
        }

        [Test]
        public void DisplayRequestTask_AcceptsPortlessProtocolWithoutPort()
        {
            WfReqElement serviceElement = new()
            {
                Field = ElemFieldType.service.ToString(),
                ProtoId = 50,
                Port = 0
            };
            DisplayRequestTask component = new();
            SetMember(component, nameof(DisplayRequestTask.WfHandler), new WfHandler
            {
                ActReqTask = new WfReqTask
                {
                    TaskType = WfTaskType.access.ToString(),
                    Elements = [serviceElement]
                },
                ActStateMatrix = new StateMatrix
                {
                    PhaseActive = { [WorkflowPhases.planning] = true }
                }
            });
            SetMember(component, "userConfig", new RequestWorkflowUserConfig());
            SetMember(component, "actSources", new List<NwObjectElement> { new("10.0.0.1", 1) });
            SetMember(component, "actDestinations", new List<NwObjectElement> { new("10.0.1.1", 1) });
            SetMember(component, "actServices", new List<NwServiceElement> { new() { ProtoId = 50, Port = 0 } });
            SetMember(component, "selectedDevices", new List<Device> { new() { Id = 1, Name = "FW-1" } });
            SetMember(component, "ipProtos", new List<IpProtocol> { new() { Id = 50, Name = "esp" } });

            bool isValid = InvokePrivateBool(component, "RejectInvalidAccessTask");

            Assert.That(isValid, Is.True);
        }

        [Test]
        public void DisplayRequestTask_RejectsTcpWithoutPort()
        {
            WfReqElement serviceElement = new()
            {
                Field = ElemFieldType.service.ToString(),
                ProtoId = 6,
                Port = 0
            };
            DisplayRequestTask component = new();
            SetMember(component, nameof(DisplayRequestTask.WfHandler), new WfHandler
            {
                ActReqTask = new WfReqTask
                {
                    TaskType = WfTaskType.access.ToString(),
                    Elements = [serviceElement]
                },
                ActStateMatrix = new StateMatrix
                {
                    PhaseActive = { [WorkflowPhases.planning] = true }
                }
            });
            SetMember(component, "userConfig", new RequestWorkflowUserConfig());
            SetMember(component, "actSources", new List<NwObjectElement> { new("10.0.0.1", 1) });
            SetMember(component, "actDestinations", new List<NwObjectElement> { new("10.0.1.1", 1) });
            SetMember(component, "actServices", new List<NwServiceElement> { new() { ProtoId = 6, Port = 0 } });
            SetMember(component, "selectedDevices", new List<Device> { new() { Id = 1, Name = "FW-1" } });
            SetMember(component, "ipProtos", new List<IpProtocol> { new() { Id = 6, Name = "tcp" } });

            bool isValid = InvokePrivateBool(component, "RejectInvalidAccessTask");

            Assert.That(isValid, Is.False);
        }

        [Test]
        public async Task DisplayImplementationTask_AccessTaskWithAllDevicesDisplaysAll()
        {
            WfReqTask reqTask = CreateAccessTask(12, "10.0.0.1", "10.0.1.1", 80);
            reqTask.SetDeviceList([WfReqTaskBase.kAllDevicesId]);
            WfImplTask implTask = new(reqTask)
            {
                Id = 22,
                DeviceId = null,
                Title = "Implement all",
                TaskType = WfTaskType.access.ToString()
            };
            WfHandler handler = new()
            {
                DisplayImplTaskMode = true,
                EditImplTaskMode = false,
                ActReqTask = reqTask,
                ActImplTask = implTask,
                Devices = [new Device { Id = 1, Name = "FW-1" }]
            };
            WfStateDict states = new() { Name = { [0] = "Draft" } };

            await using BunitContext context = new();
            IRenderedComponent<DisplayImplementationTask> component = RenderDisplayImplementationTask(context, handler, states, Roles.Implementer);

            Assert.That(component.Markup, Does.Contain("all").IgnoreCase);
        }

        [Test]
        public void DisplayImplementationTask_AcceptsPortlessProtocolWithoutPort()
        {
            WfImplElement serviceElement = new()
            {
                Field = ElemFieldType.service.ToString(),
                ProtoId = 50,
                Port = 0
            };
            DisplayImplementationTask component = new();
            SetMember(component, nameof(DisplayImplementationTask.WfHandler), new WfHandler
            {
                ActImplTask = new WfImplTask
                {
                    TaskType = WfTaskType.access.ToString(),
                    ImplElements = [serviceElement]
                }
            });
            SetMember(component, "userConfig", new RequestWorkflowUserConfig());
            SetMember(component, "ipProtos", new List<IpProtocol> { new() { Id = 50, Name = "esp" } });

            bool isValid = InvokePrivateBool(component, "CheckImplTaskValues");

            Assert.That(isValid, Is.True);
        }

        [Test]
        public void DisplayImplementationTask_RejectsTcpWithoutPort()
        {
            WfImplElement serviceElement = new()
            {
                Field = ElemFieldType.service.ToString(),
                ProtoId = 6,
                Port = 0
            };
            DisplayImplementationTask component = new();
            SetMember(component, nameof(DisplayImplementationTask.WfHandler), new WfHandler
            {
                ActImplTask = new WfImplTask
                {
                    TaskType = WfTaskType.access.ToString(),
                    ImplElements = [serviceElement]
                }
            });
            SetMember(component, "userConfig", new RequestWorkflowUserConfig());
            SetMember(component, "ipProtos", new List<IpProtocol> { new() { Id = 6, Name = "tcp" } });

            bool isValid = InvokePrivateBool(component, "CheckImplTaskValues");

            Assert.That(isValid, Is.False);
        }

        [Test]
        public async Task DisplayImplementationTask_ReadOnlyResolvedFlowSnapshotDisplaysNames()
        {
            WfReqTask reqTask = CreateAccessTask(12, "10.0.0.1", "10.0.1.1", 80);
            WfImplTask implTask = new()
            {
                Id = 22,
                Title = "Implement flow objects",
                TaskType = WfTaskType.access.ToString(),
                StateId = 1,
                ImplElements =
                [
                    new WfImplElement { Id = 1, ImplTaskId = 22, Field = ElemFieldType.source.ToString(), IpString = "10.0.0.1/32", Name = "Flow Source" },
                    new WfImplElement { Id = 2, ImplTaskId = 22, Field = ElemFieldType.destination.ToString(), IpString = "10.0.1.1/32", Name = "Flow Destination" },
                    new WfImplElement { Id = 3, ImplTaskId = 22, Field = ElemFieldType.service.ToString(), Port = 443, ProtoId = 6, Name = "Flow Service" }
                ]
            };
            WfHandler handler = new()
            {
                DisplayImplTaskMode = true,
                EditImplTaskMode = false,
                ActReqTask = reqTask,
                ActImplTask = implTask,
                Devices = []
            };
            WfStateDict states = new() { Name = { [1] = "Open" } };

            await using BunitContext context = new();
            IRenderedComponent<DisplayImplementationTask> component = RenderDisplayImplementationTask(context, handler, states, Roles.Implementer);

            Assert.Multiple(() =>
            {
                Assert.That(component.Markup, Does.Contain("Flow Source"));
                Assert.That(component.Markup, Does.Contain("Flow Destination"));
                Assert.That(component.Markup, Does.Contain("Flow Service"));
            });
        }

        [Test]
        public async Task DisplayImplementationTask_GenericTaskDisplaysFreeText()
        {
            WfImplTask implTask = new()
            {
                Id = 23,
                Title = "Generic impl",
                TaskType = WfTaskType.generic.ToString(),
                FreeText = "Implementation instructions",
                StateId = 1
            };
            WfHandler handler = new()
            {
                DisplayImplTaskMode = true,
                EditImplTaskMode = false,
                ActReqTask = new WfReqTask(),
                ActImplTask = implTask,
                Devices = []
            };
            WfStateDict states = new() { Name = { [1] = "Open" } };

            await using BunitContext context = new();
            IRenderedComponent<DisplayImplementationTask> component = RenderDisplayImplementationTask(context, handler, states, Roles.Implementer);

            Assert.That(component.Markup, Does.Contain("Implementation instructions"));
        }

        [Test]
        public async Task DisplayImplementationTask_GroupCreateReadOnlyDisplaysGroupElements()
        {
            WfImplTask implTask = new()
            {
                Id = 21,
                Title = "Create groups",
                TaskType = WfTaskType.group_create.ToString(),
                StateId = 1,
                ImplElements =
                [
                    new WfImplElement
                    {
                        Id = 1,
                        ImplTaskId = 21,
                        Field = ElemFieldType.source.ToString(),
                        NetworkId = 42,
                        GroupName = "AR-ImplGroup",
                        Name = "HiddenObjectName"
                    },
                    new WfImplElement
                    {
                        Id = 2,
                        ImplTaskId = 21,
                        Field = ElemFieldType.service.ToString(),
                        ServiceId = 44,
                        GroupName = "SG-ImplGroup",
                        Name = "HiddenServiceName"
                    }
                ]
            };
            WfHandler handler = new()
            {
                DisplayImplTaskMode = true,
                EditImplTaskMode = false,
                ActImplTask = implTask,
                ActReqTask = new WfReqTask(),
                Devices = []
            };
            WfStateDict states = new() { Name = { [1] = "Open" } };

            await using BunitContext context = new();
            IRenderedComponent<DisplayImplementationTask> component = RenderDisplayImplementationTask(context, handler, states, Roles.Implementer);

            Assert.Multiple(() =>
            {
                Assert.That(component.Markup, Does.Contain("AR-ImplGroup"));
                Assert.That(component.Markup, Does.Contain("SG-ImplGroup"));
                Assert.That(component.Markup, Does.Not.Contain("HiddenObjectName"));
                Assert.That(component.Markup, Does.Not.Contain("HiddenServiceName"));
            });
        }

        [Test]
        public async Task DisplayTaskTargetDates_EditableDatesUpdateParentValues()
        {
            DateTime? targetBeginDate = new DateTime(2026, 7, 1, 8, 15, 30);
            DateTime? targetEndDate = new DateTime(2026, 7, 31, 17, 45, 15);

            await using BunitContext context = new();
            context.Services.AddSingleton<ApiConnection>(new RequestWorkflowApiConn());
            context.Services.AddSingleton<UserConfig>(new RequestWorkflowUserConfig());

            IRenderedComponent<DisplayTaskTargetDates> component = context.Render<DisplayTaskTargetDates>(parameters => parameters
                .Add(p => p.TargetBeginDate, targetBeginDate)
                .Add(p => p.TargetBeginDateChanged, EventCallback.Factory.Create<DateTime?>(this, value => targetBeginDate = value))
                .Add(p => p.TargetEndDate, targetEndDate)
                .Add(p => p.TargetEndDateChanged, EventCallback.Factory.Create<DateTime?>(this, value => targetEndDate = value))
                .Add(p => p.CanEditTargetBeginDate, true)
                .Add(p => p.CanEditTargetEndDate, true));

            IReadOnlyList<IElement> dateInputs = component.FindAll("input[type=date]");
            IReadOnlyList<IElement> timeInputs = component.FindAll("input[type=time]");
            await dateInputs[0].InputAsync(new ChangeEventArgs { Value = "2026-08-15" });
            await timeInputs[0].InputAsync(new ChangeEventArgs { Value = "12:34:56" });
            await dateInputs[1].InputAsync(new ChangeEventArgs { Value = "2026-08-31" });
            await timeInputs[1].InputAsync(new ChangeEventArgs { Value = "23:59:58" });

            Assert.Multiple(() =>
            {
                Assert.That(targetBeginDate, Is.EqualTo(new DateTime(2026, 8, 15, 12, 34, 56)));
                Assert.That(targetEndDate, Is.EqualTo(new DateTime(2026, 8, 31, 23, 59, 58)));
            });
        }

        [Test]
        public async Task DisplayTaskTargetDates_IncompleteDateTimeMarksInputInvalid()
        {
            DateTime? targetBeginDate = new DateTime(2026, 7, 1, 8, 15, 30);
            bool targetDatesValid = true;

            await using BunitContext context = new();
            context.Services.AddSingleton<ApiConnection>(new RequestWorkflowApiConn());
            context.Services.AddSingleton<UserConfig>(new RequestWorkflowUserConfig());

            IRenderedComponent<DisplayTaskTargetDates> component = context.Render<DisplayTaskTargetDates>(parameters => parameters
                .Add(p => p.TargetBeginDate, targetBeginDate)
                .Add(p => p.TargetBeginDateChanged, EventCallback.Factory.Create<DateTime?>(this, value => targetBeginDate = value))
                .Add(p => p.CanEditTargetBeginDate, true)
                .Add(p => p.CanEditTargetEndDate, true)
                .Add(p => p.TargetDatesValidChanged, EventCallback.Factory.Create<bool>(this, value => targetDatesValid = value)));

            await component.FindAll("input[type=time]")[0].InputAsync(new ChangeEventArgs { Value = "" });

            Assert.Multiple(() =>
            {
                Assert.That(targetDatesValid, Is.False);
                Assert.That(targetBeginDate, Is.EqualTo(new DateTime(2026, 7, 1, 8, 15, 30)));
            });
        }

        [Test]
        public async Task DisplayTaskTargetDates_DateSelectionDefaultsMissingTimes()
        {
            DateTime? targetBeginDate = null;
            DateTime? targetEndDate = null;

            await using BunitContext context = new();
            context.Services.AddSingleton<ApiConnection>(new RequestWorkflowApiConn());
            context.Services.AddSingleton<UserConfig>(new RequestWorkflowUserConfig());

            IRenderedComponent<DisplayTaskTargetDates> component = context.Render<DisplayTaskTargetDates>(parameters => parameters
                .Add(p => p.TargetBeginDate, targetBeginDate)
                .Add(p => p.TargetBeginDateChanged, EventCallback.Factory.Create<DateTime?>(this, value => targetBeginDate = value))
                .Add(p => p.TargetEndDate, targetEndDate)
                .Add(p => p.TargetEndDateChanged, EventCallback.Factory.Create<DateTime?>(this, value => targetEndDate = value))
                .Add(p => p.CanEditTargetBeginDate, true)
                .Add(p => p.CanEditTargetEndDate, true));

            IReadOnlyList<IElement> dateInputs = component.FindAll("input[type=date]");
            await dateInputs[0].InputAsync(new ChangeEventArgs { Value = "2026-08-15" });
            await dateInputs[1].InputAsync(new ChangeEventArgs { Value = "2026-08-31" });

            Assert.Multiple(() =>
            {
                Assert.That(targetBeginDate, Is.EqualTo(new DateTime(2026, 8, 15, 0, 0, 0)));
                Assert.That(targetEndDate, Is.EqualTo(new DateTime(2026, 8, 31, 23, 59, 59)));
            });
        }

        [Test]
        public async Task DisplayTaskTargetDates_TimeWithoutSecondsIsAcceptedAsZeroSeconds()
        {
            DateTime? targetBeginDate = new DateTime(2026, 7, 1, 8, 15, 30);
            bool targetDatesValid = true;

            await using BunitContext context = new();
            context.Services.AddSingleton<ApiConnection>(new RequestWorkflowApiConn());
            context.Services.AddSingleton<UserConfig>(new RequestWorkflowUserConfig());

            IRenderedComponent<DisplayTaskTargetDates> component = context.Render<DisplayTaskTargetDates>(parameters => parameters
                .Add(p => p.TargetBeginDate, targetBeginDate)
                .Add(p => p.TargetBeginDateChanged, EventCallback.Factory.Create<DateTime?>(this, value => targetBeginDate = value))
                .Add(p => p.CanEditTargetBeginDate, true)
                .Add(p => p.CanEditTargetEndDate, true)
                .Add(p => p.TargetDatesValidChanged, EventCallback.Factory.Create<bool>(this, value => targetDatesValid = value)));

            await component.FindAll("input[type=time]")[0].InputAsync(new ChangeEventArgs { Value = "12:34" });

            Assert.Multiple(() =>
            {
                Assert.That(targetDatesValid, Is.True);
                Assert.That(targetBeginDate, Is.EqualTo(new DateTime(2026, 7, 1, 12, 34, 0)));
            });
        }

        [Test]
        public async Task DisplayTaskTargetDates_HoursPrecisionUsesHourNumberInputs()
        {
            DateTime? targetBeginDate = new DateTime(2026, 7, 1, 8, 15, 30);
            DateTime? targetEndDate = new DateTime(2026, 7, 31, 17, 45, 15);

            await using BunitContext context = new();
            context.Services.AddSingleton<ApiConnection>(new RequestWorkflowApiConn());
            context.Services.AddSingleton<UserConfig>(new RequestWorkflowUserConfig
            {
                ReqFlowIntegration = new FlowIntegrationConfig
                {
                    SelectObjects = FlowIntegrationObjectSelectionOptions.Both,
                    SelectServices = FlowIntegrationObjectSelectionOptions.Both,
                    SelectTimeObjects = FlowIntegrationObjectSelectionOptions.Both,
                    TimeObjectPrecision = FlowIntegrationTimePrecisionOptions.Hours
                }.ToConfigValue()
            });

            IRenderedComponent<DisplayTaskTargetDates> component = context.Render<DisplayTaskTargetDates>(parameters => parameters
                .Add(p => p.TargetBeginDate, targetBeginDate)
                .Add(p => p.TargetBeginDateChanged, EventCallback.Factory.Create<DateTime?>(this, value => targetBeginDate = value))
                .Add(p => p.TargetEndDate, targetEndDate)
                .Add(p => p.TargetEndDateChanged, EventCallback.Factory.Create<DateTime?>(this, value => targetEndDate = value))
                .Add(p => p.CanEditTargetBeginDate, true)
                .Add(p => p.CanEditTargetEndDate, true));

            IReadOnlyList<IElement> hourInputs = component.FindAll("input[type=number]");
            await hourInputs[0].InputAsync(new ChangeEventArgs { Value = "13" });
            await hourInputs[1].InputAsync(new ChangeEventArgs { Value = "1" });

            Assert.Multiple(() =>
            {
                Assert.That(component.FindAll("input[type=time]"), Is.Empty);
                Assert.That(hourInputs, Has.Count.EqualTo(2));
                Assert.That(hourInputs[0].GetAttribute("min"), Is.EqualTo("0"));
                Assert.That(hourInputs[0].GetAttribute("max"), Is.EqualTo("23"));
                Assert.That(targetBeginDate, Is.EqualTo(new DateTime(2026, 7, 1, 13, 0, 0)));
                Assert.That(targetEndDate, Is.EqualTo(new DateTime(2026, 7, 31, 1, 0, 0)));
            });
        }

        [TestCase(FlowIntegrationTimePrecisionOptions.Date, "2026-07-01", "2026-07-31", "08:15", "17:45")]
        [TestCase(FlowIntegrationTimePrecisionOptions.Hours, "2026-07-01 8 h", "2026-07-31 17 h", "08:15", "17:45")]
        [TestCase(FlowIntegrationTimePrecisionOptions.Minutes, "2026-07-01 08:15", "2026-07-31 17:45", "08:15:30", "17:45:15")]
        [TestCase(FlowIntegrationTimePrecisionOptions.Seconds, "2026-07-01 08:15:30", "2026-07-31 17:45:15", null, null)]
        public async Task DisplayTaskTargetDates_ReadOnlyDatesDisplayLabelsUseConfiguredPrecision(string precision, string expectedBegin, string expectedEnd, string? hiddenBeginPart, string? hiddenEndPart)
        {
            DateTime targetBeginDate = new(2026, 7, 1, 8, 15, 30);
            DateTime targetEndDate = new(2026, 7, 31, 17, 45, 15);

            await using BunitContext context = new();
            context.Services.AddSingleton<ApiConnection>(new RequestWorkflowApiConn());
            context.Services.AddSingleton<UserConfig>(new RequestWorkflowUserConfig
            {
                ReqFlowIntegration = new FlowIntegrationConfig
                {
                    SelectObjects = FlowIntegrationObjectSelectionOptions.Both,
                    SelectServices = FlowIntegrationObjectSelectionOptions.Both,
                    SelectTimeObjects = FlowIntegrationObjectSelectionOptions.Both,
                    TimeObjectPrecision = precision
                }.ToConfigValue()
            });

            IRenderedComponent<DisplayTaskTargetDates> component = context.Render<DisplayTaskTargetDates>(parameters => parameters
                .Add(p => p.TargetBeginDate, targetBeginDate)
                .Add(p => p.TargetEndDate, targetEndDate)
                .Add(p => p.CanEditTargetBeginDate, false)
                .Add(p => p.CanEditTargetEndDate, false));

            Assert.Multiple(() =>
            {
                Assert.That(component.FindAll("input[type=date]"), Is.Empty);
                Assert.That(component.FindAll("input[type=time]"), Is.Empty);
                Assert.That(component.Markup, Does.Contain(expectedBegin));
                Assert.That(component.Markup, Does.Contain(expectedEnd));
                if (hiddenBeginPart != null)
                {
                    Assert.That(component.Markup, Does.Not.Contain(hiddenBeginPart));
                }
                if (hiddenEndPart != null)
                {
                    Assert.That(component.Markup, Does.Not.Contain(hiddenEndPart));
                }
            });
        }

        [Test]
        public async Task DisplayTaskTargetDates_FlowTimeObjectDropdownDisplaysNameWithoutRange()
        {
            await using BunitContext context = new();
            RequestWorkflowApiConn apiConn = new()
            {
                FlowTimeObjects =
                [
                    new FlowTimeObject
                    {
                        Id = 301,
                        Name = "Business Hours",
                        StartTime = new DateTime(2026, 8, 1, 8, 0, 0),
                        EndTime = new DateTime(2026, 8, 1, 17, 0, 0),
                        ShowInRequestModule = true
                    }
                ]
            };
            context.Services.AddSingleton<ApiConnection>(apiConn);
            context.Services.AddSingleton<UserConfig>(new RequestWorkflowUserConfig
            {
                ReqUseFlowDb = true,
                ReqFlowIntegration = new FlowIntegrationConfig
                {
                    SelectObjects = FlowIntegrationObjectSelectionOptions.Both,
                    SelectServices = FlowIntegrationObjectSelectionOptions.Both,
                    SelectTimeObjects = FlowIntegrationObjectSelectionOptions.FromFlowDb,
                    TimeObjectPrecision = FlowIntegrationTimePrecisionOptions.Seconds
                }.ToConfigValue()
            });
            context.Services.AddSingleton<DomEventService>();

            IRenderedComponent<DisplayTaskTargetDates> component = context.Render<DisplayTaskTargetDates>(parameters => parameters
                .Add(p => p.CanEditTargetBeginDate, true)
                .Add(p => p.CanEditTargetEndDate, true));

            IRenderedComponent<Dropdown<FlowTimeObject>> dropdown = component.FindComponent<Dropdown<FlowTimeObject>>();
            string displayText = dropdown.Instance.ElementToString(apiConn.FlowTimeObjects.Single());

            Assert.Multiple(() =>
            {
                Assert.That(apiConn.Queries, Does.Contain(FlowQueries.getFlowRequestTimeObjectCatalog));
                Assert.That(displayText, Is.EqualTo("Business Hours"));
                Assert.That(displayText, Does.Not.Contain("2026-08-01"));
                Assert.That(displayText, Does.Not.Contain("08:00"));
                Assert.That(displayText, Does.Not.Contain("17:00"));
            });
        }

        [Test]
        public void DisplayRequestTask_TargetBeginAfterTargetEndIsRejected()
        {
            List<string> messages = [];
            DisplayRequestTask component = new();
            SetMember(component, nameof(DisplayRequestTask.WfHandler), new WfHandler
            {
                ActReqTask = new WfReqTask
                {
                    TargetBeginDate = new DateTime(2026, 8, 31, 23, 59, 58),
                    TargetEndDate = new DateTime(2026, 8, 15, 12, 34, 56)
                }
            });
            SetMember(component, "userConfig", new RequestWorkflowUserConfig());
            SetMember(component, "DisplayMessageInUi", (Action<Exception?, string, string, bool>)((_, _, message, _) => messages.Add(message)));

            bool valid = InvokePrivateBool(component, "RejectInvalidTargetDates");

            Assert.Multiple(() =>
            {
                Assert.That(valid, Is.False);
                Assert.That(messages, Does.Contain("E5119"));
            });
        }

        [Test]
        public void DisplayImplementationTask_TargetBeginAfterTargetEndIsRejected()
        {
            List<string> messages = [];
            DisplayImplementationTask component = new();
            SetMember(component, nameof(DisplayImplementationTask.WfHandler), new WfHandler
            {
                ActImplTask = new WfImplTask
                {
                    TargetBeginDate = new DateTime(2026, 8, 31, 23, 59, 58),
                    TargetEndDate = new DateTime(2026, 8, 15, 12, 34, 56)
                }
            });
            SetMember(component, "userConfig", new RequestWorkflowUserConfig());
            SetMember(component, "DisplayMessageInUi", (Action<Exception?, string, string, bool>)((_, _, message, _) => messages.Add(message)));

            bool valid = InvokePrivateBool(component, "RejectInvalidTargetDates");

            Assert.Multiple(() =>
            {
                Assert.That(valid, Is.False);
                Assert.That(messages, Does.Contain("E5119"));
            });
        }

        [Test]
        public async Task DisplayRequestTask_ReinitializesElementsWhenActiveTaskChanges()
        {
            WfReqTask firstTask = CreateAccessTask(12, "10.0.0.1", "10.0.1.1", 80);
            WfReqTask secondTask = CreateAccessTask(13, "10.0.0.2", "10.0.1.2", 443);
            WfHandler handler = new()
            {
                DisplayReqTaskMode = true,
                ReadOnlyMode = true,
                ActReqTask = firstTask,
                ActTicket = new WfTicket { Id = 100, Tasks = [firstTask, secondTask] },
                Devices = []
            };
            SetMatrix(handler, WfTaskType.access.ToString(), new StateMatrix());
            WfStateDict states = new() { Name = { [0] = "Draft" } };

            await using BunitContext context = new();
            IRenderedComponent<DisplayRequestTask> component = RenderDisplayRequestTask(context, handler, states, Roles.Requester);

            Assert.That(GetMember<List<NwObjectElement>>(component.Instance, "actSources").Single().IpString, Is.EqualTo("10.0.0.1/32"));
            Assert.That(GetMember<List<NwObjectElement>>(component.Instance, "actDestinations").Single().IpString, Is.EqualTo("10.0.1.1/32"));
            Assert.That(GetMember<List<NwServiceElement>>(component.Instance, "actServices").Single().Port, Is.EqualTo(80));

            handler.ActReqTask = secondTask;
            await component.InvokeAsync(() => component.Instance.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(DisplayRequestTask.Phase)] = WorkflowPhases.request,
                [nameof(DisplayRequestTask.States)] = states,
                [nameof(DisplayRequestTask.WfHandler)] = handler,
                [nameof(DisplayRequestTask.ResetParent)] = (Func<Task>)DefaultInit.DoNothing,
                [nameof(DisplayRequestTask.StartImplPhase)] = (Func<WfImplTask, Task>)DefaultInit.DoNothing
            })));

            Assert.That(GetMember<List<NwObjectElement>>(component.Instance, "actSources").Single().IpString, Is.EqualTo("10.0.0.2/32"));
            Assert.That(GetMember<List<NwObjectElement>>(component.Instance, "actDestinations").Single().IpString, Is.EqualTo("10.0.1.2/32"));
            Assert.That(GetMember<List<NwServiceElement>>(component.Instance, "actServices").Single().Port, Is.EqualTo(443));
        }

        [Test]
        public async Task PromoteObject_MissingStateName_FallsBackToStateId()
        {
            await using BunitContext context = new();
            WfStateDict states = new();
            StateMatrix stateMatrix = new()
            {
                Matrix = new()
                {
                    [0] = [5, 6]
                }
            };
            WfStatefulObject statefulObject = new()
            {
                StateId = 0
            };

            IRenderedComponent<PromoteObject> component = RenderPromoteObject(context, states, stateMatrix, statefulObject, Roles.Requester);

            Assert.That(component.Markup, Does.Contain("promote_to"));
            Assert.That(component.Markup, Does.Contain("dropdown-input-"));
        }

        private sealed class RequestWorkflowApiConn : SimulatedApiConnection
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

        private sealed class RequestWorkflowUserConfig : SimulatedUserConfig
        {
            public RequestWorkflowUserConfig()
            {
                ReqAvailableTaskTypes = "[\"generic\",\"access\",\"rule_modify\",\"rule_delete\",\"new_interface\",\"group_create\"]";
                ReqAllowedChangesByApprover = "{}";
                DummyTranslate["flow_bundle_id"] = "Bundle ID";
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
