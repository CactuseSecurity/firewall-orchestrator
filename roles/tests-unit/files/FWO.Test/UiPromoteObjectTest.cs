using AngleSharp.Dom;
using Bunit;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Flow;
using FWO.Data.Workflow;
using FWO.Services;
using FWO.Services.EventMediator;
using FWO.Services.Workflow;
using FWO.Ui.Pages.Request;
using FWO.Ui.Shared;
using FWO.Ui.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using static FWO.Test.UiRequestWorkflowTest;

namespace FWO.Test
{
    [TestFixture]
    internal class UiPromoteObjectTest
    {
        private sealed class CommentAuthStateProvider : AuthenticationStateProvider
        {
            private readonly ClaimsPrincipal principal;

            public CommentAuthStateProvider(params string[] roles)
            {
                List<Claim> claims = new();
                foreach (string role in roles)
                {
                    claims.Add(new Claim(ClaimTypes.Role, role));
                }
                principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
            }

            public override Task<AuthenticationState> GetAuthenticationStateAsync()
            {
                return Task.FromResult(new AuthenticationState(principal));
            }
        }

        private static IRenderedComponent<CommentObject> RenderCommentObject(BunitContext context,
            Func<string, Task> save, Func<Task> resetParent, params string[] roles)
        {
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton<AuthenticationStateProvider>(new CommentAuthStateProvider(roles));
            SimulatedUserConfig userConfig = new();
            userConfig.User.Roles.AddRange(roles);
            context.Services.AddSingleton<UserConfig>(userConfig);
            context.Services.AddSingleton(new DomEventService());

            IRenderedComponent<CascadingAuthenticationState> wrapper = context.Render<CascadingAuthenticationState>(parameters => parameters
                .AddChildContent<CommentObject>(child => child
                    .Add(p => p.Display, true)
                    .Add(p => p.ObjectName, "Task")
                    .Add(p => p.Save, save)
                    .Add(p => p.ResetParent, resetParent)));

            return wrapper.FindComponent<CommentObject>();
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

        [Test]
        public async Task PromoteObject_WithComment_ShowsCommentForSingleEarlyTransition()
        {
            await using BunitContext context = new();
            WfStateDict states = new();
            StateMatrix stateMatrix = new()
            {
                LowestStartedState = 5,
                Matrix = new() { [0] = [5] }
            };
            WfStatefulObject statefulObject = new() { StateId = 0 };
            IRenderedComponent<PromoteObject> component = RenderPromoteObject(context, states, stateMatrix,
                statefulObject, true, Roles.Requester);
            Assert.That(component.Markup, Does.Contain("id=\"optComment\""));
        }

        [Test]
        public async Task PromoteObject_SingleTransitionWithoutComment_SavesAndCloses()
        {
            WfStatefulObject statefulObject = new() { StateId = 0 };
            PromoteObject component = new();
            int saveCalls = 0;
            int closeCalls = 0;
            SetMember(component, nameof(PromoteObject.Promote), true);
            SetMember(component, nameof(PromoteObject.StatefulObject), statefulObject);
            SimulatedUserConfig userConfig = new();
            userConfig.User.Roles.Add(Roles.Admin);
            SetMember(component, "userConfig", userConfig);
            SetMember(component, nameof(PromoteObject.StateMatrix),
                new StateMatrix { Matrix = new() { [0] = new List<int> { 5 } } });
            SetMember(component, nameof(PromoteObject.Save), (Func<WfStatefulObject, Task>)(_ =>
            {
                saveCalls++;
                return Task.CompletedTask;
            }));
            SetMember(component, nameof(PromoteObject.CloseParent), (Func<Task>)(() =>
            {
                closeCalls++;
                return Task.CompletedTask;
            }));

            await InvokePrivateTask(component, "OnParametersSetAsync");

            Assert.Multiple(() =>
            {
                Assert.That(statefulObject.StateId, Is.EqualTo(5));
                Assert.That(saveCalls, Is.EqualTo(1));
                Assert.That(closeCalls, Is.EqualTo(1));
                Assert.That(GetMember<bool>(component, "Display"), Is.False);
            });
        }

        [Test]
        public async Task PromoteObject_MultipleTransitions_ShowsDialogWithoutSaving()
        {
            WfStatefulObject statefulObject = new() { StateId = 0 };
            int saveCalls = 0;
            PromoteObject component = new();
            SetMember(component, nameof(PromoteObject.Promote), true);
            SetMember(component, nameof(PromoteObject.StatefulObject), statefulObject);
            SimulatedUserConfig userConfig = new();
            userConfig.User.Roles.Add(Roles.Admin);
            SetMember(component, "userConfig", userConfig);
            SetMember(component, nameof(PromoteObject.StateMatrix),
                new StateMatrix { Matrix = new() { [0] = new List<int> { 5, 6 } } });
            SetMember(component, nameof(PromoteObject.Save), (Func<WfStatefulObject, Task>)(_ =>
            {
                saveCalls++;
                return Task.CompletedTask;
            }));

            await InvokePrivateTask(component, "OnParametersSetAsync");

            Assert.Multiple(() =>
            {
                Assert.That(GetMember<bool>(component, "Display"), Is.True);
                Assert.That(GetMember<List<int>>(component, "possibleStates"), Is.EqualTo(new List<int> { 5, 6 }));
                Assert.That(saveCalls, Is.Zero);
            });
        }

        [Test]
        public async Task PromoteObject_Perform_SetsOptionalCommentAndCloses()
        {
            WfStatefulObject statefulObject = new() { StateId = 0 };
            int saveCalls = 0;
            int closeCalls = 0;
            PromoteObject component = new();
            SetMember(component, nameof(PromoteObject.StatefulObject), statefulObject);
            SetMember(component, nameof(PromoteObject.Save), (Func<WfStatefulObject, Task>)(_ =>
            {
                saveCalls++;
                return Task.CompletedTask;
            }));
            SetMember(component, nameof(PromoteObject.CloseParent), (Func<Task>)(() =>
            {
                closeCalls++;
                return Task.CompletedTask;
            }));
            SetMember(component, "optComm", "approval comment");
            SetMember(component, "Display", true);

            await InvokePrivateTask(component, "Perform");

            Assert.Multiple(() =>
            {
                Assert.That(statefulObject.OptComment(), Is.EqualTo("approval comment"));
                Assert.That(saveCalls, Is.EqualTo(1));
                Assert.That(closeCalls, Is.EqualTo(1));
                Assert.That(GetMember<bool>(component, "workInProgress"), Is.False);
                Assert.That(GetMember<bool>(component, "Display"), Is.False);
            });
        }

        [Test]
        public void PromoteObject_Cancel_CallsParentAndClearsState()
        {
            int cancelCalls = 0;
            PromoteObject component = new();
            SetMember(component, nameof(PromoteObject.Promote), true);
            SetMember(component, nameof(PromoteObject.CancelParent), (Func<bool>)(() =>
            {
                cancelCalls++;
                return true;
            }));
            SetMember(component, "Display", true);
            SetMember(component, "workInProgress", true);

            GetPrivateMethod(typeof(PromoteObject), "Cancel").Invoke(component, Array.Empty<object>());

            Assert.Multiple(() =>
            {
                Assert.That(cancelCalls, Is.EqualTo(1));
                Assert.That(component.Promote, Is.False);
                Assert.That(GetMember<bool>(component, "Display"), Is.False);
                Assert.That(GetMember<bool>(component, "workInProgress"), Is.False);
            });
        }

        [Test]
        public async Task CommentObject_SaveAndClose_InvokesCallbacksAndClearsDisplay()
        {
            await using BunitContext context = new();
            string? savedComment = null;
            int resetCalls = 0;
            IRenderedComponent<CommentObject> component = RenderCommentObject(
                context,
                comment =>
                {
                    savedComment = comment;
                    return Task.CompletedTask;
                },
                () =>
                {
                    resetCalls++;
                    return Task.CompletedTask;
                },
                Roles.Admin);

            component.Find("textarea").Change("ticket comment");
            component.Find("button.btn-primary").Click();

            Assert.Multiple(() =>
            {
                Assert.That(savedComment, Is.EqualTo("ticket comment"));
                Assert.That(resetCalls, Is.EqualTo(1));
                Assert.That(component.Instance.Display, Is.False);
            });
        }

        [Test]
        public async Task CommentObject_Cancel_ResetsAndHidesDialog()
        {
            await using BunitContext context = new();
            int resetCalls = 0;
            IRenderedComponent<CommentObject> component = RenderCommentObject(
                context,
                _ => Task.CompletedTask,
                () =>
                {
                    resetCalls++;
                    return Task.CompletedTask;
                },
                Roles.Requester);

            component.Find("button.btn-secondary").Click();

            Assert.Multiple(() =>
            {
                Assert.That(resetCalls, Is.EqualTo(1));
                Assert.That(component.Instance.Display, Is.False);
            });
        }

        [Test]
        public void CommentObject_ClosedDialog_DoesNotResetExistingText()
        {
            CommentObject component = new();
            SetMember(component, nameof(CommentObject.Display), false);
            SetMember(component, "commentText", "retained");

            GetPrivateMethod(typeof(CommentObject), "OnParametersSet").Invoke(component, Array.Empty<object>());

            Assert.That(GetMember<string>(component, "commentText"), Is.EqualTo("retained"));
        }


    }
}
