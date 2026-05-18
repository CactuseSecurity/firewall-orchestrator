using Bunit;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Workflow;
using FWO.Services;
using FWO.Services.EventMediator;
using FWO.Services.EventMediator.Interfaces;
using FWO.Services.Workflow;
using FWO.Ui.Pages.Request;
using FWO.Ui.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
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

        private static void SetMatrix(WfHandler handler, string taskType, StateMatrix matrix)
        {
            FieldInfo? field = typeof(WfHandler).GetField("stateMatrixDict", BindingFlags.NonPublic | BindingFlags.Instance);
            StateMatrixDict dict = (StateMatrixDict)(field?.GetValue(handler) ?? new StateMatrixDict());
            dict.Matrices[taskType] = matrix;
        }

        private static DisplayReqTaskTable CreateReqTaskTable(WfHandler handler, WorkflowPhases phase)
        {
            DisplayReqTaskTable component = new();
            SetMember(component, nameof(DisplayReqTaskTable.WfHandler), handler);
            SetMember(component, nameof(DisplayReqTaskTable.Phase), phase);
            return component;
        }

        private static IRenderedComponent<DisplayRequestTask> RenderDisplayRequestTask(
            Bunit.TestContext context,
            WfHandler handler,
            WfStateDict states,
            params string[] roles)
        {
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton<AuthenticationStateProvider>(new RequestWorkflowAuthStateProvider(roles));
            context.Services.AddSingleton<ApiConnection>(new RequestWorkflowApiConn());
            context.Services.AddSingleton<UserConfig>(new RequestWorkflowUserConfig());
            context.Services.AddSingleton<DomEventService>();
            context.Services.AddSingleton<IEventMediator>(new EventMediator());

            IRenderedComponent<CascadingAuthenticationState> wrapper = context.Render<CascadingAuthenticationState>(parameters => parameters
                .AddChildContent<DisplayRequestTask>(child => child
                    .Add(p => p.Phase, WorkflowPhases.request)
                    .Add(p => p.States, states)
                    .Add(p => p.WfHandler, handler)
                    .Add(p => p.ResetParent, DefaultInit.DoNothing)
                    .Add(p => p.StartImplPhase, (Func<WfImplTask, Task>)DefaultInit.DoNothing)));

            return wrapper.FindComponent<DisplayRequestTask>();
        }

        private static IRenderedComponent<PromoteObject> RenderPromoteObject(
            Bunit.TestContext context,
            WfStateDict states,
            StateMatrix stateMatrix,
            WfStatefulObject statefulObject,
            params string[] roles)
        {
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton<AuthenticationStateProvider>(new RequestWorkflowAuthStateProvider(roles));
            context.Services.AddSingleton<UserConfig>(new RequestWorkflowUserConfig());
            context.Services.AddSingleton<DomEventService>();
            context.Services.AddSingleton<IEventMediator>(new EventMediator());

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
        public void DisplayReqTaskTable_ApprovalPhase_StopsEditAtLowestStartedState()
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
            await using (Bunit.TestContext existingContext = new())
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
            await using (Bunit.TestContext newContext = new())
            {
                IRenderedComponent<DisplayRequestTask> newComponent = RenderDisplayRequestTask(newContext, newHandler, states, Roles.Requester);
                newDropdownCount = newComponent.FindAll("input[id^='dropdown-input-']").Count;
            }

            Assert.That(newDropdownCount, Is.GreaterThan(existingDropdownCount));
        }

        [Test]
        public async Task PromoteObject_MissingStateName_FallsBackToStateId()
        {
            await using Bunit.TestContext context = new();
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
            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null)
            {
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

                throw new NotImplementedException($"Unexpected query: {query}");
            }
        }

        private sealed class RequestWorkflowUserConfig : SimulatedUserConfig
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
