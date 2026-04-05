using FWO.Data.Workflow;
using FWO.Services.Workflow;
using NUnit.Framework;
using System.Reflection;

namespace FWO.Test
{
    [TestFixture]
    internal class WfHandlerStateChangesTest
    {
        private static void SetMatrix(WfHandler handler, string taskType, StateMatrix matrix)
        {
            FieldInfo? field = typeof(WfHandler).GetField("stateMatrixDict", BindingFlags.NonPublic | BindingFlags.Instance);
            StateMatrixDict dict = (StateMatrixDict)(field?.GetValue(handler) ?? new StateMatrixDict());
            dict.Matrices[taskType] = matrix;
        }

        [Test]
        public async Task PromoteTicket_SetsStateAndCompletionAndResetsFlags()
        {
            WfHandler handler = new();
            handler.MasterStateMatrix = new StateMatrix
            {
                MinTicketCompleted = 2,
                LowestEndState = 10,
                PhaseActive = new() { { WorkflowPhases.planning, false } }
            };
            handler.ActTicket = new WfTicket { Id = 1, StateId = 0 };
            handler.TicketList.Add(handler.ActTicket);
            handler.DisplayTicketMode = true;
            handler.EditTicketMode = true;
            handler.AddTicketMode = true;
            handler.DisplayPromoteTicketMode = true;
            handler.DisplaySaveTicketMode = true;

            bool ok = await handler.PromoteTicket(new WfStatefulObject { StateId = 2 });

            Assert.That(ok, Is.True);
            Assert.That(handler.ActTicket.StateId, Is.EqualTo(2));
            Assert.That(handler.ActTicket.CompletionDate, Is.Not.Null);
            Assert.That(handler.DisplayTicketMode, Is.False);
            Assert.That(handler.EditTicketMode, Is.False);
            Assert.That(handler.AddTicketMode, Is.False);
            Assert.That(handler.DisplayPromoteTicketMode, Is.False);
            Assert.That(handler.DisplaySaveTicketMode, Is.False);
        }

        [Test]
        public async Task PromoteReqTask_SetsStartAndHandlerAndClearsFlag()
        {
            WfHandler handler = new();
            handler.MasterStateMatrix = new StateMatrix
            {
                LowestInputState = 0,
                LowestStartedState = 2,
                LowestEndState = 5,
                PhaseActive = new() { { WorkflowPhases.planning, false } }
            };
            handler.ActStateMatrix = new StateMatrix
            {
                LowestInputState = 0,
                LowestStartedState = 2,
                LowestEndState = 5
            };
            handler.ActReqTask = new WfReqTask { Id = 7, TicketId = 10, StateId = 0 };
            handler.ActTicket = new WfTicket { Id = 10, Tasks = { handler.ActReqTask } };
            handler.DisplayPromoteReqTaskMode = true;

            await handler.PromoteReqTask(new WfStatefulObject { StateId = 2 });

            Assert.That(handler.ActReqTask.StateId, Is.EqualTo(2));
            Assert.That(handler.ActReqTask.Start, Is.Not.Null);
            Assert.That(handler.ActReqTask.CurrentHandler, Is.EqualTo(handler.userConfig.User));
            Assert.That(handler.DisplayPromoteReqTaskMode, Is.False);
        }

        [Test]
        public async Task PromoteImplTask_SetsStopAndUpdatesReqAndTicket()
        {
            WfHandler handler = new() { Phase = WorkflowPhases.implementation };
            handler.MasterStateMatrix = new StateMatrix
            {
                LowestInputState = 0,
                LowestStartedState = 2,
                LowestEndState = 5
            };
            handler.ActStateMatrix = new StateMatrix
            {
                LowestInputState = 0,
                LowestStartedState = 2,
                LowestEndState = 5
            };
            WfImplTask implTask = new() { Id = 3, ReqTaskId = 7, StateId = 0 };
            WfReqTask reqTask = new()
            {
                Id = 7,
                TicketId = 1,
                ImplementationTasks = { implTask }
            };
            handler.ActImplTask = implTask;
            handler.ActReqTask = reqTask;
            handler.ActTicket = new WfTicket { Id = 1, Tasks = { reqTask } };
            handler.DisplayPromoteImplTaskMode = true;

            await handler.PromoteImplTask(new WfStatefulObject { StateId = 5 });

            Assert.That(handler.ActImplTask.StateId, Is.EqualTo(5));
            Assert.That(handler.ActImplTask.Stop, Is.Not.Null);
            Assert.That(handler.ActReqTask.StateId, Is.EqualTo(5));
            Assert.That(handler.ActReqTask.Stop, Is.Not.Null);
            Assert.That(handler.ActTicket.StateId, Is.EqualTo(5));
            Assert.That(handler.DisplayPromoteImplTaskMode, Is.False);
        }

        [Test]
        public async Task AutoPromote_TicketScope_UsesProvidedState()
        {
            WfHandler handler = new();
            handler.MasterStateMatrix = new StateMatrix
            {
                MinTicketCompleted = 99,
                LowestEndState = 10,
                PhaseActive = new() { { WorkflowPhases.planning, false } }
            };
            WfTicket ticket = new() { Id = 1, StateId = 0 };
            handler.TicketList.Add(ticket);

            await handler.AutoPromote(ticket, WfObjectScopes.Ticket, 3);

            Assert.That(handler.ActTicket.StateId, Is.EqualTo(3));
            Assert.That(handler.DisplayTicketMode, Is.False);
            Assert.That(handler.EditTicketMode, Is.False);
        }

        [Test]
        public async Task AutoPromote_RequestTaskScope_UpdatesStateAndHandler()
        {
            WfHandler handler = new();
            StateMatrix matrix = new()
            {
                LowestInputState = 0,
                LowestStartedState = 2,
                LowestEndState = 5
            };
            SetMatrix(handler, WfTaskType.access.ToString(), matrix);

            WfReqTask reqTask = new()
            {
                Id = 7,
                TicketId = 1,
                TaskType = WfTaskType.access.ToString(),
                StateId = 0,
                CurrentHandler = new FWO.Data.UiUser { DbId = 99 }
            };
            handler.TicketList.Add(new WfTicket { Id = 1, Tasks = { reqTask } });

            await handler.AutoPromote(reqTask, WfObjectScopes.RequestTask, 3);

            Assert.That(handler.ActReqTask.StateId, Is.EqualTo(3));
            Assert.That(handler.ActReqTask.CurrentHandler?.DbId, Is.EqualTo(99));
            Assert.That(handler.ActTicket.Tasks[0].StateId, Is.EqualTo(3));
        }

        [Test]
        public async Task AutoPromote_ImplTaskScope_UpdatesStateAndHandler()
        {
            WfHandler handler = new();
            StateMatrix matrix = new()
            {
                LowestInputState = 0,
                LowestStartedState = 2,
                LowestEndState = 5
            };
            SetMatrix(handler, WfTaskType.access.ToString(), matrix);

            WfImplTask implTask = new()
            {
                Id = 3,
                TicketId = 1,
                ReqTaskId = 7,
                TaskType = WfTaskType.access.ToString(),
                StateId = 0,
                CurrentHandler = new FWO.Data.UiUser { DbId = 42 }
            };
            WfReqTask reqTask = new()
            {
                Id = 7,
                TicketId = 1,
                TaskType = WfTaskType.access.ToString(),
                ImplementationTasks = { implTask }
            };
            handler.TicketList.Add(new WfTicket { Id = 1, Tasks = { reqTask } });

            await handler.AutoPromote(implTask, WfObjectScopes.ImplementationTask, 4);

            Assert.That(handler.ActImplTask.StateId, Is.EqualTo(4));
            Assert.That(handler.ActImplTask.CurrentHandler?.DbId, Is.EqualTo(42));
            Assert.That(handler.ActReqTask.ImplementationTasks[0].StateId, Is.EqualTo(4));
        }

        [Test]
        public async Task AutoPromote_ApprovalScope_UpdatesApprovalAndReqTask()
        {
            WfHandler handler = new();
            StateMatrix matrix = new()
            {
                LowestInputState = 0,
                LowestStartedState = 2,
                LowestEndState = 5
            };
            SetMatrix(handler, WfTaskType.access.ToString(), matrix);
            handler.MasterStateMatrix = new StateMatrix
            {
                LowestEndState = 10,
                MinTicketCompleted = 99,
                PhaseActive = new() { { WorkflowPhases.planning, false } }
            };

            WfApproval approval = new() { Id = 1, TaskId = 7, StateId = 1 };
            WfReqTask reqTask = new()
            {
                Id = 7,
                TicketId = 1,
                TaskType = WfTaskType.access.ToString(),
                Approvals = { approval }
            };
            handler.TicketList.Add(new WfTicket { Id = 1, Tasks = { reqTask } });

            await handler.AutoPromote(approval, WfObjectScopes.Approval, 5);

            Assert.That(handler.ActApproval.StateId, Is.EqualTo(5));
            Assert.That(handler.ActApproval.ApprovalDate, Is.Not.Null);
            Assert.That(handler.ActReqTask.StateId, Is.EqualTo(5));
        }

        [Test]
        public async Task UpdateRequestTasksFromTicket_UpdatesApprovalStatesFromReqTask()
        {
            WfHandler handler = new();
            StateMatrix matrix = new()
            {
                LowestInputState = 0,
                LowestStartedState = 2,
                LowestEndState = 5,
                ApprovalLowestEndState = 5,
                PhaseActive = new() { { WorkflowPhases.planning, false } },
                MinImplTasksNeeded = 99
            };
            SetMatrix(handler, WfTaskType.access.ToString(), matrix);
            handler.ActTicket = new WfTicket { Id = 1, StateId = 4 };
            WfReqTask reqTask = new()
            {
                Id = 7,
                TicketId = 1,
                TaskType = WfTaskType.access.ToString(),
                StateId = 1,
                Approvals =
                {
                    new WfApproval { Id = 1, TaskId = 7, StateId = 1 },
                    new WfApproval { Id = 2, TaskId = 7, StateId = 6 }
                }
            };
            handler.ActTicket.Tasks.Add(reqTask);

            MethodInfo? method = typeof(WfHandler).GetMethod("UpdateRequestTasksFromTicket", BindingFlags.NonPublic | BindingFlags.Instance);
            await (Task)(method?.Invoke(handler, [false]) ?? throw new InvalidOperationException("Method not found."));

            Assert.That(reqTask.StateId, Is.EqualTo(4));
            Assert.That(reqTask.Approvals[0].StateId, Is.EqualTo(4));
            Assert.That(reqTask.Approvals[1].StateId, Is.EqualTo(6));
        }

        [Test]
        public async Task UpdateRequestTasksFromTicket_DoesNotUpdateApprovalsAtLowestEndState()
        {
            WfHandler handler = new();
            StateMatrix matrix = new()
            {
                LowestInputState = 0,
                LowestStartedState = 2,
                LowestEndState = 5,
                ApprovalLowestEndState = 5,
                PhaseActive = new() { { WorkflowPhases.planning, false } },
                MinImplTasksNeeded = 99
            };
            SetMatrix(handler, WfTaskType.access.ToString(), matrix);
            handler.ActStateMatrix = matrix;
            handler.ActTicket = new WfTicket { Id = 1, StateId = 6 };
            WfReqTask reqTask = new()
            {
                Id = 7,
                TicketId = 1,
                TaskType = WfTaskType.access.ToString(),
                StateId = 1,
                Approvals =
                {
                    new WfApproval { Id = 1, TaskId = 7, StateId = 4 },
                    new WfApproval { Id = 2, TaskId = 7, StateId = 5 }
                }
            };
            handler.ActTicket.Tasks.Add(reqTask);

            MethodInfo? method = typeof(WfHandler).GetMethod("UpdateRequestTasksFromTicket", BindingFlags.NonPublic | BindingFlags.Instance);
            await (Task)(method?.Invoke(handler, [false]) ?? throw new InvalidOperationException("Method not found."));

            Assert.That(reqTask.StateId, Is.EqualTo(6));
            Assert.That(reqTask.Approvals[0].StateId, Is.EqualTo(6));
            Assert.That(reqTask.Approvals[1].StateId, Is.EqualTo(5));
        }
    }
}
