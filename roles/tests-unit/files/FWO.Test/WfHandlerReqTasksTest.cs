using FWO.Data;
using FWO.Data.Workflow;
using FWO.Services.Workflow;
using NUnit.Framework;
using System.Reflection;

namespace FWO.Test
{
    [TestFixture]
    internal class WfHandlerReqTasksTest
    {
        private static void SetMatrix(WfHandler handler, string taskType, StateMatrix? matrix = null)
        {
            FieldInfo? field = typeof(WfHandler).GetField("stateMatrixDict", BindingFlags.NonPublic | BindingFlags.Instance);
            StateMatrixDict dict = (StateMatrixDict)(field?.GetValue(handler) ?? new StateMatrixDict());
            dict.Matrices[taskType] = matrix ?? new StateMatrix();
        }

        [Test]
        public void SetReqTaskMode_SetsFlags()
        {
            WfHandler handler = new();

            handler.SetReqTaskMode(ObjAction.add);

            Assert.That(handler.DisplayReqTaskMode, Is.True);
            Assert.That(handler.EditReqTaskMode, Is.True);
            Assert.That(handler.AddReqTaskMode, Is.True);
            Assert.That(handler.PlanReqTaskMode, Is.False);
        }

        [Test]
        public void SetReqTaskPopUpOpt_SetsFlags()
        {
            WfHandler handler = new();

            handler.SetReqTaskPopUpOpt(ObjAction.displayAssign);
            Assert.That(handler.DisplayAssignReqTaskMode, Is.True);

            handler.SetReqTaskPopUpOpt(ObjAction.displayApprovals);
            Assert.That(handler.DisplayApprovalReqMode, Is.True);

            handler.SetReqTaskPopUpOpt(ObjAction.displayPromote);
            Assert.That(handler.DisplayPromoteReqTaskMode, Is.True);

            handler.SetReqTaskPopUpOpt(ObjAction.displayApprove);
            Assert.That(handler.DisplayApproveMode, Is.True);

            handler.SetReqTaskPopUpOpt(ObjAction.displayPathAnalysis);
            Assert.That(handler.DisplayPathAnalysisMode, Is.True);
        }

        [Test]
        public void ResetReqTaskActions_ClearsFlags()
        {
            WfHandler handler = new();
            handler.DisplayReqTaskMode = true;
            handler.EditReqTaskMode = true;
            handler.AddReqTaskMode = true;
            handler.PlanReqTaskMode = true;
            handler.DisplayAssignReqTaskMode = true;
            handler.DisplayApprovalReqMode = true;
            handler.DisplayApproveMode = true;
            handler.DisplayPromoteReqTaskMode = true;
            handler.DisplayDeleteReqTaskMode = true;
            handler.DisplayReqTaskCommentMode = true;
            handler.DisplayPathAnalysisMode = true;

            handler.ResetReqTaskActions();

            Assert.That(handler.DisplayReqTaskMode, Is.False);
            Assert.That(handler.EditReqTaskMode, Is.False);
            Assert.That(handler.AddReqTaskMode, Is.False);
            Assert.That(handler.PlanReqTaskMode, Is.False);
            Assert.That(handler.DisplayAssignReqTaskMode, Is.False);
            Assert.That(handler.DisplayApprovalReqMode, Is.False);
            Assert.That(handler.DisplayApproveMode, Is.False);
            Assert.That(handler.DisplayPromoteReqTaskMode, Is.False);
            Assert.That(handler.DisplayDeleteReqTaskMode, Is.False);
            Assert.That(handler.DisplayReqTaskCommentMode, Is.False);
            Assert.That(handler.DisplayPathAnalysisMode, Is.False);
        }

        [Test]
        public void SelectReqTask_SetsEnvironmentAndMode()
        {
            WfHandler handler = new();
            string taskType = WfTaskType.access.ToString();
            StateMatrix matrix = new() { LowestInputState = 1 };
            SetMatrix(handler, taskType, matrix);
            WfReqTask reqTask = new() { Id = 11, TicketId = 7, TaskNumber = 3, TaskType = taskType };
            WfTicket ticket = new() { Id = 7, Tasks = { reqTask } };
            handler.TicketList.Add(ticket);

            handler.SelectReqTask(reqTask, ObjAction.approve);

            Assert.Multiple(() =>
            {
                Assert.That(handler.ActReqTask.Id, Is.EqualTo(11));
                Assert.That(handler.ActReqTask, Is.Not.SameAs(reqTask));
                Assert.That(handler.ActTicket, Is.SameAs(ticket));
                Assert.That(handler.ActStateMatrix, Is.SameAs(matrix));
                Assert.That(handler.DisplayReqTaskMode, Is.True);
                Assert.That(handler.ApproveReqTaskMode, Is.True);
                Assert.That(handler.EditReqTaskMode, Is.False);
            });
        }

        [Test]
        public void SetReqTaskEnv_ById_FindsTaskInTickets()
        {
            WfHandler handler = new();
            string taskType = WfTaskType.access.ToString();
            SetMatrix(handler, taskType);
            WfReqTask reqTask = new() { Id = 11, TicketId = 7, TaskType = taskType };
            handler.TicketList.Add(new WfTicket { Id = 7, Tasks = { reqTask } });

            bool found = handler.SetReqTaskEnv(11);

            Assert.Multiple(() =>
            {
                Assert.That(found, Is.True);
                Assert.That(handler.ActReqTask.Id, Is.EqualTo(11));
                Assert.That(handler.ActTicket.Id, Is.EqualTo(7));
            });
        }

        [Test]
        public void SetReqTaskEnv_ById_ReturnsFalseWhenTaskMissing()
        {
            WfHandler handler = new();
            handler.TicketList.Add(new WfTicket { Id = 7 });

            bool found = handler.SetReqTaskEnv(11);

            Assert.That(found, Is.False);
        }

        [Test]
        public async Task AddReqTask_ExistingTicketAssignsTicketAndAddsTask()
        {
            WfHandler handler = new();
            handler.ActTicket = new WfTicket { Id = 7 };
            handler.ActReqTask = new WfReqTask { TaskNumber = 1, TaskType = WfTaskType.access.ToString() };

            await handler.AddReqTask();

            Assert.Multiple(() =>
            {
                Assert.That(handler.ActReqTask.TicketId, Is.EqualTo(7));
                Assert.That(handler.ActTicket.Tasks, Has.Count.EqualTo(1));
                Assert.That(handler.ActTicket.Tasks[0], Is.SameAs(handler.ActReqTask));
            });
        }

        [Test]
        public async Task ChangeReqTask_ReplacesTaskByTaskNumber()
        {
            WfHandler handler = new();
            WfReqTask oldTask = new() { Id = 11, TaskNumber = 2, Title = "Old" };
            handler.ActTicket = new WfTicket { Tasks = { oldTask } };
            handler.ActReqTask = new WfReqTask { Id = 11, TaskNumber = 2, Title = "New" };

            await handler.ChangeReqTask();

            Assert.Multiple(() =>
            {
                Assert.That(handler.ActTicket.Tasks, Has.Count.EqualTo(1));
                Assert.That(handler.ActTicket.Tasks[0].Title, Is.EqualTo("New"));
                Assert.That(handler.ActTicket.Tasks[0], Is.SameAs(handler.ActReqTask));
            });
        }

        [Test]
        public async Task ConfDeleteReqTask_RemovesTaskAndClearsFlag()
        {
            WfHandler handler = new();
            WfReqTask reqTask = new() { Id = 11 };
            handler.ActTicket = new WfTicket { Tasks = { reqTask, new WfReqTask { Id = 12 } } };
            handler.ActReqTask = reqTask;
            handler.DisplayDeleteReqTaskMode = true;

            await handler.ConfDeleteReqTask();

            Assert.Multiple(() =>
            {
                Assert.That(handler.ActTicket.Tasks.Select(task => task.Id), Is.EqualTo(new long[] { 12 }));
                Assert.That(handler.DisplayDeleteReqTaskMode, Is.False);
            });
        }

        [Test]
        public async Task ConfAddCommentToReqTask_AddsCommentAndClearsFlag()
        {
            WfHandler handler = new();
            handler.ActReqTask = new WfReqTask();
            handler.DisplayReqTaskCommentMode = true;

            await handler.ConfAddCommentToReqTask("comment");

            Assert.That(handler.ActReqTask.Comments, Has.Count.EqualTo(1));
            Assert.That(handler.ActReqTask.Comments[0].Comment.CommentText, Is.EqualTo("comment"));
            Assert.That(handler.DisplayReqTaskCommentMode, Is.False);
        }

        [Test]
        public void GetRequestingOwner_ReturnsOwnerDisplay()
        {
            WfHandler handler = new();
            handler.AllOwners.Add(new FwoOwner { Id = 7, Name = "Owner" });
            handler.ActReqTask = new WfReqTask();
            handler.ActReqTask.SetAddInfo(AdditionalInfoKeys.ReqOwner, "7");

            string name = handler.GetRequestingOwner();

            Assert.That(name, Does.Contain("Owner"));
        }

        [Test]
        public async Task SetAddInfoInReqTask_SetsAdditionalInfo()
        {
            WfHandler handler = new();
            WfReqTask task = new();

            await handler.SetAddInfoInReqTask(task, "Key", "Value");

            Assert.That(task.GetAddInfoValue("Key"), Is.EqualTo("Value"));
        }
    }
}
