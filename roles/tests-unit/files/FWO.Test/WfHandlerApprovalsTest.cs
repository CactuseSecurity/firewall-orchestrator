using FWO.Data;
using FWO.Data.Workflow;
using FWO.Services.Workflow;
using NUnit.Framework;
using System.Text.Json;

namespace FWO.Test
{
    [TestFixture]
    internal class WfHandlerApprovalsTest
    {
        [Test]
        public async Task SetApprovalEnv_SelectsOpenApproval()
        {
            WfHandler handler = new();
            handler.ActStateMatrix = new StateMatrix { LowestEndState = 4 };
            handler.ActReqTask = new WfReqTask
            {
                Approvals =
                [
                    new WfApproval { Id = 1, StateId = 1 },
                    new WfApproval { Id = 2, StateId = 5 }
                ]
            };

            await handler.SetApprovalEnv(createIfMissing: false);

            Assert.That(handler.ActApproval.Id, Is.EqualTo(1));
        }

        [Test]
        public async Task SetApprovalEnv_FallsBackToLastApproval()
        {
            WfHandler handler = new();
            handler.ActStateMatrix = new StateMatrix { LowestEndState = 4 };
            handler.ActReqTask = new WfReqTask
            {
                Approvals =
                [
                    new WfApproval { Id = 1, StateId = 4 },
                    new WfApproval { Id = 2, StateId = 6 }
                ]
            };

            await handler.SetApprovalEnv(createIfMissing: false);

            Assert.That(handler.ActApproval.Id, Is.EqualTo(2));
        }

        [Test]
        public async Task AddApproval_UsesExtParams()
        {
            WfHandler handler = new();
            handler.ActStateMatrix = new StateMatrix { LowestEndState = 9 };
            handler.ActReqTask = new WfReqTask { Id = 10 };
            handler.ActTicket = new WfTicket { TenantId = 3 };
            ApprovalParams approvalParams = new()
            {
                StateId = 7,
                ApproverGroup = "cn=group",
                Deadline = 1
            };
            string extParams = JsonSerializer.Serialize(approvalParams);

            await handler.AddApproval(extParams);

            WfApproval approval = handler.ActReqTask.Approvals[^1];
            Assert.That(approval.StateId, Is.EqualTo(7));
            Assert.That(approval.ApproverGroup, Is.EqualTo("cn=group"));
            Assert.That(approval.Deadline?.Date, Is.EqualTo(DateTime.Now.AddDays(1).Date));
        }

        [Test]
        public async Task AddApproval_DefaultsToLowestEndState_AndMarksInitial()
        {
            WfHandler handler = new();
            handler.ActStateMatrix = new StateMatrix { LowestEndState = 7 };
            handler.ActReqTask = new WfReqTask { Id = 10 };
            handler.ActTicket = new WfTicket { Priority = 2 };
            handler.PrioList =
            [
                new WfPriority { NumPrio = 2, ApprovalDeadline = 2 }
            ];

            await handler.AddApproval();

            WfApproval approval = handler.ActReqTask.Approvals[^1];
            Assert.That(approval.StateId, Is.EqualTo(7));
            Assert.That(approval.InitialApproval, Is.True);
            Assert.That(approval.Deadline?.Date, Is.EqualTo(DateTime.Now.AddDays(2).Date));
        }

        [Test]
        public void SetApprovalPopUpOpt_AndResetApprovalActions_ToggleFlags()
        {
            WfHandler handler = new();

            handler.SetApprovalPopUpOpt(ObjAction.displayAssign);
            Assert.That(handler.DisplayAssignApprovalMode, Is.True);
            Assert.That(handler.DisplayApprovalCommentMode, Is.False);

            handler.SetApprovalPopUpOpt(ObjAction.displayComment);
            Assert.That(handler.DisplayAssignApprovalMode, Is.False);
            Assert.That(handler.DisplayApprovalCommentMode, Is.True);

            handler.ResetApprovalActions();
            Assert.That(handler.DisplayAssignApprovalMode, Is.False);
            Assert.That(handler.DisplayApprovalCommentMode, Is.False);
        }

        [Test]
        public async Task ConfAddCommentToApproval_AddsCommentAndResetsFlag()
        {
            WfHandler handler = new();
            handler.ActApproval = new WfApproval { Id = 5 };
            handler.DisplayApprovalCommentMode = true;

            await handler.ConfAddCommentToApproval("comment");

            Assert.That(handler.ActApproval.Comments, Has.Count.EqualTo(1));
            Assert.That(handler.DisplayApprovalCommentMode, Is.False);
        }

        [Test]
        public async Task AssignApprovalGroup_UpdatesGroupAndClearsDisplay()
        {
            WfHandler handler = new();
            WfStatefulObject statefulObject = new() { AssignedGroup = "cn=group" };
            handler.ActApproval = new WfApproval
            {
                AssignedGroup = "cn=old",
                CurrentHandler = new UiUser { Name = "handler" }
            };
            handler.ActReqTask = new WfReqTask
            {
                Approvals =
                [
                    handler.ActApproval
                ]
            };
            handler.DisplayAssignApprovalMode = true;

            await handler.AssignApprovalGroup(statefulObject);

            Assert.That(handler.ActApproval.AssignedGroup, Is.EqualTo("cn=group"));
            Assert.That(handler.ActApproval.RecentHandler, Is.EqualTo(handler.ActApproval.CurrentHandler));
            Assert.That(handler.DisplayAssignApprovalMode, Is.False);
        }
    }
}
