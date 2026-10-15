using Bunit;
using FWO.Data;
using FWO.Data.Workflow;
using FWO.Services.Workflow;
using FWO.Ui.Pages.Request;
using NUnit.Framework;
using System.Reflection;
using static FWO.Test.UiRequestWorkflowTest;

namespace FWO.Test
{
    [TestFixture]
    internal class UiDisplayTicketTest
    {
        private static UiRequestCoverageTest.RequestCoverageUserConfig CreateUserConfig(params string[] roles)
        {
            return UiRequestCoverageTest.CreateUserConfig(roles);
        }

        private static void SetMember(object instance, string memberName, object? value)
        {
            UiRequestCoverageTest.SetMember(instance, memberName, value);
        }

        private static T GetMember<T>(object instance, string memberName)
        {
            return UiRequestCoverageTest.GetMember<T>(instance, memberName);
        }

        private static MethodInfo GetPrivateMethod(Type type, string methodName)
        {
            return UiRequestCoverageTest.GetPrivateMethod(type, methodName);
        }

        private static bool InvokePrivateBool(object instance, string methodName, params object[] args)
        {
            return UiRequestCoverageTest.InvokePrivateBool(instance, methodName, args);
        }

        private static Task InvokePrivateTask(object instance, string methodName, params object[] args)
        {
            return UiRequestWorkflowTest.InvokePrivateTask(instance, methodName, args);
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
        public void DisplayTicket_ReadsCombinedTicketFieldVisibilityConfig()
        {
            var userConfig = CreateUserConfig();
            userConfig.ReqTicketFieldVisibility = new TicketFieldVisibilityConfig
            {
                ShowRequester = false,
                ShowPriority = true,
                ShowDeadline = false,
                ShowReason = true
            }.ToConfigValue();
            WfHandler handler = new()
            {
                ActTicket = new WfTicket { Id = 10, Title = "Ticket" }
            };
            DisplayTicket component = CreateDisplayTicket(handler, WorkflowPhases.request, userConfig: userConfig);

            TicketFieldVisibilityConfig visibility = GetMember<TicketFieldVisibilityConfig>(component, "TicketFieldVisibility");

            Assert.Multiple(() =>
            {
                Assert.That(visibility.ShowRequester, Is.False);
                Assert.That(visibility.ShowPriority, Is.True);
                Assert.That(visibility.ShowDeadline, Is.False);
                Assert.That(visibility.ShowReason, Is.True);
                Assert.That(visibility.ShowComments, Is.False);
            });
        }

        [Test]
        public void DisplayTicket_UsesExternalRequestorNameAndId()
        {
            WfHandler handler = new()
            {
                ActTicket = new WfTicket
                {
                    Id = 10,
                    Requester = new UiUser { Name = "Authenticated User" },
                    AdditionalInfo = "{\"requestorName\":\"Alice Example\",\"requestorId\":\"alice\"}"
                }
            };
            DisplayTicket component = CreateDisplayTicket(handler, WorkflowPhases.request);

            string requesterDisplay = (string)GetPrivateMethod(typeof(DisplayTicket), "BuildTicketRequesterDisplay")
                .Invoke(component, null)!;

            Assert.That(requesterDisplay, Is.EqualTo("Alice Example (alice)"));
        }

        [Test]
        public async Task DisplayTicket_ConfAddComment_RerendersUpdatedComments()
        {
            await using BunitContext context = new();
            WfHandler handler = new()
            {
                DisplayTicketMode = true,
                ActTicket = new WfTicket { Id = 10, Title = "Ticket" }
            };
            IRenderedComponent<DisplayTicket> component = RenderDisplayTicket(context, handler, WorkflowPhases.approval, new WfStateDict());

            await InvokePrivateTask(component.Instance, "ConfAddComment", "new ticket comment");

            Assert.That(GetMember<string>(component.Instance, "allComments"), Does.Contain("new ticket comment"));
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
        public void DisplayTicket_InitPromoteTicket_OpensPromotePopup()
        {
            WfHandler handler = new();
            DisplayTicket component = CreateDisplayTicket(handler, WorkflowPhases.request);

            GetPrivateMethod(typeof(DisplayTicket), "InitPromoteTicket").Invoke(component, null);

            Assert.Multiple(() =>
            {
                Assert.That(handler.DisplayPromoteTicketMode, Is.True);
                Assert.That(handler.DisplaySaveTicketMode, Is.False);
            });
        }

        [Test]
        public async Task DisplayTicket_PromoteTicketForPhase_RequestPhasePromotesTicketAndTasks()
        {
            WfHandler handler = new()
            {
                ActTicket = new WfTicket { Id = 10, StateId = 1 }
            };
            DisplayTicket component = CreateDisplayTicket(handler, WorkflowPhases.request);

            await InvokePrivateTask(component, "PromoteTicketForPhase", new WfStatefulObject { StateId = 4 });

            Assert.That(handler.ActTicket.StateId, Is.EqualTo(4));
        }

        [Test]
        public async Task DisplayTicket_PromoteTicketForPhase_LaterPhasePromotesTasksAndTicket()
        {
            WfHandler handler = new()
            {
                ActTicket = new WfTicket { Id = 10, StateId = 1 }
            };
            DisplayTicket component = CreateDisplayTicket(handler, WorkflowPhases.approval);

            await InvokePrivateTask(component, "PromoteTicketForPhase", new WfStatefulObject { StateId = 4 });

            Assert.That(handler.ActTicket.StateId, Is.EqualTo(4));
        }

        [Test]
        public async Task DisplayTicket_PerformAction_ResetsParentAndClearsBusyState()
        {
            int resetCalls = 0;
            WfHandler handler = new()
            {
                ActTicket = new WfTicket { Id = 10 }
            };
            using BunitContext context = new();
            IRenderedComponent<DisplayTicket> renderedComponent = RenderDisplayTicket(
                context, handler, WorkflowPhases.request, new WfStateDict());
            SetMember(renderedComponent.Instance, nameof(DisplayTicket.ResetParent), () =>
            {
                resetCalls++;
                return Task.CompletedTask;
            });

            await InvokePrivateTask(renderedComponent.Instance, "PerformAction", new WfStateAction());

            Assert.Multiple(() =>
            {
                Assert.That(resetCalls, Is.EqualTo(1));
                Assert.That(GetMember<bool>(renderedComponent.Instance, "WorkInProgress"), Is.False);
            });
        }

        [Test]
        public async Task DisplayTicket_PerformAction_WhenBusyDoesNothing()
        {
            int resetCalls = 0;
            WfHandler handler = new() { ActTicket = new WfTicket { Id = 10 } };
            DisplayTicket component = CreateDisplayTicket(handler, WorkflowPhases.request, resetParent: () =>
            {
                resetCalls++;
                return Task.CompletedTask;
            });
            SetMember(component, "WorkInProgress", true);

            await InvokePrivateTask(component, "PerformAction", new WfStateAction());

            Assert.Multiple(() =>
            {
                Assert.That(resetCalls, Is.EqualTo(0));
                Assert.That(GetMember<bool>(component, "WorkInProgress"), Is.True);
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
        public void DisplayTicket_CheckTicketValues_RejectsEmptyTitle()
        {
            List<string> messages = [];
            DisplayTicket component = new();
            UiRequestCoverageTest.RequestCoverageUserConfig userConfig = CreateUserConfig();
            SetMember(component, "userConfig", userConfig);
            SetMember(component, nameof(DisplayTicket.WfHandler), new WfHandler
            {
                ActTicket = new WfTicket { Title = "" }
            });
            SetMember(component, "DisplayMessageInUi", (Action<Exception?, string, string, bool>)((_, _, message, _) => messages.Add(message)));

            bool valid = InvokePrivateBool(component, "CheckTicketValues");

            Assert.Multiple(() =>
            {
                Assert.That(valid, Is.False);
                Assert.That(messages, Does.Contain(userConfig.GetText("E5102")));
            });
        }

        [Test]
        public void DisplayTicket_Cancel_ResetsPromoteAndSaveModes()
        {
            DisplayTicket component = new();
            WfHandler handler = new()
            {
                DisplaySaveTicketMode = true,
                DisplayPromoteTicketMode = true
            };
            SetMember(component, nameof(DisplayTicket.WfHandler), handler);

            bool cancelResult = InvokePrivateBool(component, "Cancel");

            Assert.Multiple(() =>
            {
                Assert.That(cancelResult, Is.True);
                Assert.That(handler.DisplaySaveTicketMode, Is.False);
                Assert.That(handler.DisplayPromoteTicketMode, Is.False);
            });
        }
    }
}
