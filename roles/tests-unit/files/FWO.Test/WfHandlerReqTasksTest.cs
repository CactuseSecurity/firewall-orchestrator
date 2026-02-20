using FWO.Data;
using FWO.Data.Workflow;
using FWO.Services.Workflow;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class WfHandlerReqTasksTest
    {
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
