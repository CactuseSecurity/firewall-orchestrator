using Bunit;
using FWO.Basics;
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
    internal class UiDisplayTicketTableTest
    {
        private static UiRequestCoverageTest.RequestCoverageUserConfig CreateUserConfig(params string[] roles)
        {
            return UiRequestCoverageTest.CreateUserConfig(roles);
        }

        private static StateMatrix CreateMatrix(int lowestInputState = 1, int lowestStartedState = 2, int lowestEndState = 10)
        {
            return UiRequestCoverageTest.CreateMatrix(lowestInputState, lowestStartedState, lowestEndState);
        }

        private static Task InvokePrivateTask(Type type, object instance, string methodName, params object[] args)
        {
            return UiRequestCoverageTest.InvokePrivateTask(type, instance, methodName, args);
        }

        private static Task InvokePrivateTask(object instance, string methodName, params object[] args)
        {
            return UiRequestWorkflowTest.InvokePrivateTask(instance, methodName, args);
        }

        [Test]
        public async Task DisplayTicketTable_AddAndEditTicket_UseExpectedTicketModes()
        {
            DisplayTicketTable component = new();
            UiRequestCoverageTest.RequestCoverageUserConfig userConfig = CreateUserConfig(Roles.Requester);
            WfHandler handler = new()
            {
                MasterStateMatrix = CreateMatrix(),
                ActTicket = new WfTicket { Id = 11, Title = "Ticket", StateId = 3 },
                ReadOnlyMode = false
            };
            SetMember(component, nameof(DisplayTicketTable.WfHandler), handler);
            SetMember(component, nameof(DisplayTicketTable.Phase), WorkflowPhases.request);
            SetMember(component, "userConfig", userConfig);

            await InvokePrivateTask(typeof(DisplayTicketTable), component, "AddTicket");
            Assert.Multiple(() =>
            {
                Assert.That(handler.DisplayTicketMode, Is.True);
                Assert.That(handler.EditTicketMode, Is.True);
                Assert.That(handler.AddTicketMode, Is.True);
                Assert.That(handler.ActTicket.Requester, Is.EqualTo(userConfig.User));
            });

            await InvokePrivateTask(typeof(DisplayTicketTable), component, "ShowTicketDetails", new WfTicket { Id = 22, Title = "Details", StateId = 4 });
            Assert.That(handler.DisplayTicketMode, Is.True);

            await InvokePrivateTask(typeof(DisplayTicketTable), component, "EditTicket", new WfTicket { Id = 23, Title = "Edit", StateId = 4 });
            Assert.That(handler.EditTicketMode, Is.True);
        }

        [Test]
        public void DisplayTicketTable_CanEditTicketInPhase_RespectsStateBounds()
        {
            DisplayTicketTable component = new();
            SetMember(component, nameof(DisplayTicketTable.WfHandler), new WfHandler
            {
                MasterStateMatrix = CreateMatrix(lowestInputState: 2, lowestEndState: 7),
                ReadOnlyMode = false
            });

            bool editable = (bool)GetPrivateMethod(typeof(DisplayTicketTable), "CanEditTicketInPhase").Invoke(component, [new WfTicket { StateId = 4 }])!;
            bool notEditableLow = (bool)GetPrivateMethod(typeof(DisplayTicketTable), "CanEditTicketInPhase").Invoke(component, [new WfTicket { StateId = 1 }])!;
            bool notEditableHigh = (bool)GetPrivateMethod(typeof(DisplayTicketTable), "CanEditTicketInPhase").Invoke(component, [new WfTicket { StateId = 7 }])!;

            Assert.Multiple(() =>
            {
                Assert.That(editable, Is.True);
                Assert.That(notEditableLow, Is.False);
                Assert.That(notEditableHigh, Is.False);
            });
        }
        [Test]
        public async Task DisplayTicketTable_OpenAddEditAndDetailsActionsDelegateToHandler()
        {
            WfTicket ticket = new()
            {
                Id = 8,
                Title = "Ticket",
                StateId = 3,
                Requester = new UiUser { DbId = 11, Name = "Requester" }
            };
            WfHandler handler = CreateWorkflowHandler(WorkflowPhases.request, WfTaskType.access.ToString(), ticket);
            DisplayTicketTable component = CreateTicketTable(handler, WorkflowPhases.request);
            SetMember(component, "userConfig", handler.userConfig);

            await InvokePrivateTask(component, "ShowTicketDetails", ticket);
            await InvokePrivateTask(component, "EditTicket", ticket);
            await InvokePrivateTask(component, "AddTicket");

            Assert.Multiple(() =>
            {
                Assert.That(handler.ActTicket.Id, Is.EqualTo(0));
                Assert.That(handler.DisplayTicketMode, Is.True);
                Assert.That(handler.EditTicketMode, Is.True);
                Assert.That(handler.AddTicketMode, Is.True);
                Assert.That(handler.ActTicket.Requester, Is.Not.Null);
                Assert.That(handler.ActTicket.Requester!.DbId, Is.EqualTo(10));
            });
        }

        [Test]
        public async Task DisplayTicketTable_ResetClearsTicketActionsAndCallsParent()
        {
            await using BunitContext context = new();
            int resetCalls = 0;
            WfHandler handler = CreateWorkflowHandler(WorkflowPhases.request, WfTaskType.access.ToString(), new WfTicket { Id = 1 });
            handler.DisplayTicketMode = true;
            handler.EditTicketMode = true;
            handler.AddTicketMode = true;
            IRenderedComponent<DisplayTicketTable> component = RenderDisplayTicketTable(context, handler, WorkflowPhases.request, new WfStateDict());
            SetMember(component.Instance, nameof(DisplayTicketTable.ResetParent), () =>
            {
                resetCalls++;
                return Task.CompletedTask;
            });

            await component.InvokeAsync(async () => await InvokePrivateTask(component.Instance, "Reset"));

            Assert.Multiple(() =>
            {
                Assert.That(resetCalls, Is.EqualTo(1));
                Assert.That(handler.DisplayTicketMode, Is.False);
                Assert.That(handler.EditTicketMode, Is.False);
                Assert.That(handler.AddTicketMode, Is.False);
            });
        }

        [Test]
        public void DisplayTicketTable_CanEditTicketInPhase_UsesReadOnlyAndStateBounds()
        {
            WfHandler handler = new()
            {
                ReadOnlyMode = false,
                MasterStateMatrix = new StateMatrix
                {
                    LowestInputState = 2,
                    LowestEndState = 5
                }
            };
            DisplayTicketTable component = CreateTicketTable(handler, WorkflowPhases.approval);
            MethodInfo? method = typeof(DisplayTicketTable).GetMethod("CanEditTicketInPhase", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(method, Is.Not.Null);

            bool canEdit = (bool)method!.Invoke(component, [new WfTicket { StateId = 3 }])!;
            bool canNotEditLow = (bool)method!.Invoke(component, [new WfTicket { StateId = 1 }])!;
            bool canNotEditHigh = (bool)method!.Invoke(component, [new WfTicket { StateId = 5 }])!;

            Assert.Multiple(() =>
            {
                Assert.That(canEdit, Is.True);
                Assert.That(canNotEditLow, Is.False);
                Assert.That(canNotEditHigh, Is.False);
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
    }
}
