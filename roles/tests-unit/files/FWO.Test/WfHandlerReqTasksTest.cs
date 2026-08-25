using FWO.Data;
using FWO.Data.Workflow;
using FWO.Services;
using FWO.Services.Workflow;
using NUnit.Framework;
using System.Reflection;
using System.Text.Json;

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

            handler.SetReqTaskPopUpOpt(ObjAction.displayDelete);
            Assert.That(handler.DisplayDeleteReqTaskMode, Is.True);

            handler.SetReqTaskPopUpOpt(ObjAction.displayComment);
            Assert.That(handler.DisplayReqTaskCommentMode, Is.True);
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
        public async Task SelectReqTask_AsyncWithoutReloadUsesProvidedTask()
        {
            WfHandler handler = new();
            string taskType = WfTaskType.access.ToString();
            SetMatrix(handler, taskType);
            WfReqTask reqTask = new() { Id = 11, TicketId = 7, TaskNumber = 3, TaskType = taskType };
            handler.TicketList.Add(new WfTicket { Id = 7, Tasks = { reqTask } });

            await handler.SelectReqTask(reqTask, ObjAction.plan, reload: false);

            Assert.Multiple(() =>
            {
                Assert.That(handler.ActReqTask.Id, Is.EqualTo(11));
                Assert.That(handler.PlanReqTaskMode, Is.True);
                Assert.That(handler.DisplayReqTaskMode, Is.True);
                Assert.That(handler.EditReqTaskMode, Is.False);
            });
        }

        [Test]
        public void SelectReqTaskPopUp_SetsEnvironmentAndPopupMode()
        {
            WfHandler handler = new();
            string taskType = WfTaskType.access.ToString();
            SetMatrix(handler, taskType);
            WfReqTask reqTask = new() { Id = 11, TicketId = 7, TaskType = taskType };
            handler.TicketList.Add(new WfTicket { Id = 7, Tasks = { reqTask } });

            handler.SelectReqTaskPopUp(reqTask, ObjAction.displayDelete);

            Assert.Multiple(() =>
            {
                Assert.That(handler.ActReqTask.Id, Is.EqualTo(11));
                Assert.That(handler.DisplayDeleteReqTaskMode, Is.True);
            });
        }

        [Test]
        public async Task SelectReqTaskPopUp_AsyncWithoutReloadSetsPopupMode()
        {
            WfHandler handler = new();
            string taskType = WfTaskType.access.ToString();
            SetMatrix(handler, taskType);
            WfReqTask reqTask = new() { Id = 11, TicketId = 7, TaskType = taskType };
            handler.TicketList.Add(new WfTicket { Id = 7, Tasks = { reqTask } });

            await handler.SelectReqTaskPopUp(reqTask, ObjAction.displayComment, reload: false);

            Assert.Multiple(() =>
            {
                Assert.That(handler.ActReqTask.Id, Is.EqualTo(11));
                Assert.That(handler.DisplayReqTaskCommentMode, Is.True);
            });
        }

        [Test]
        public async Task ReloadActReqTask_ReturnsFalseWhenTaskOrTicketInvalid()
        {
            WfHandler handler = new();

            bool missingTask = await handler.ReloadActReqTask();
            handler.ActReqTask = new WfReqTask { Id = 11, TicketId = 0 };
            bool missingTicket = await handler.ReloadActReqTask();

            Assert.Multiple(() =>
            {
                Assert.That(missingTask, Is.False);
                Assert.That(missingTicket, Is.False);
            });
        }

        [Test]
        public async Task ReloadActReqTask_RefreshesTicketAndTask()
        {
            string taskType = WfTaskType.access.ToString();
            WfReqTask refreshedTask = new() { Id = 11, TicketId = 7, TaskType = taskType };
            WfTicket refreshedTicket = new() { Id = 7, Tasks = { refreshedTask } };
            WfHandler handler = CreateReloadHandler(refreshedTicket);
            SetMatrix(handler, taskType);
            handler.ActReqTask = new WfReqTask { Id = 11, TicketId = 7, TaskType = taskType };
            handler.TicketList.Add(new WfTicket { Id = 7, Tasks = { new WfReqTask { Id = 11, TicketId = 7, TaskType = taskType } } });

            bool reloaded = await handler.ReloadActReqTask();

            Assert.Multiple(() =>
            {
                Assert.That(reloaded, Is.True);
                Assert.That(handler.ActTicket, Is.SameAs(refreshedTicket));
                Assert.That(handler.ActReqTask, Is.Not.SameAs(refreshedTask));
                Assert.That(handler.ActReqTask.Id, Is.EqualTo(11));
                Assert.That(handler.TicketList[0], Is.SameAs(refreshedTicket));
            });
        }

        [Test]
        public async Task ReloadActReqTask_ReturnsFalseWhenReloadedTaskCannotBeActivated()
        {
            string taskType = WfTaskType.access.ToString();
            WfReqTask refreshedTask = new() { Id = 11, TicketId = 7, TaskType = taskType };
            WfTicket refreshedTicket = new() { Id = 7, Tasks = { refreshedTask } };
            WfHandler handler = CreateReloadHandler(refreshedTicket);
            handler.ActReqTask = new WfReqTask { Id = 11, TicketId = 7, TaskType = taskType };

            bool reloaded = await handler.ReloadActReqTask();

            Assert.That(reloaded, Is.False);
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
        public void SetReqTaskMode_DisplayOnlyLeavesEditAndApprovalModesFalse()
        {
            WfHandler handler = new();
            handler.EditReqTaskMode = true;
            handler.ApproveReqTaskMode = true;

            handler.SetReqTaskMode(ObjAction.display);

            Assert.Multiple(() =>
            {
                Assert.That(handler.DisplayReqTaskMode, Is.True);
                Assert.That(handler.EditReqTaskMode, Is.False);
                Assert.That(handler.ApproveReqTaskMode, Is.False);
                Assert.That(handler.AddReqTaskMode, Is.False);
            });
        }

        [Test]
        public void TrySetReqTaskEnv_ReturnsFalseForUnknownTaskType()
        {
            WfHandler handler = new();
            handler.ActReqTask = new WfReqTask { Id = 99 };
            WfReqTask reqTask = new() { Id = 11, TicketId = 7, TaskType = WfTaskType.access.ToString() };

            bool found = handler.TrySetReqTaskEnv(reqTask);

            Assert.Multiple(() =>
            {
                Assert.That(found, Is.False);
                Assert.That(handler.ActReqTask.Id, Is.EqualTo(99));
            });
        }

        [Test]
        public void TrySetReqTaskEnv_SetsEnvironmentForKnownTaskType()
        {
            WfHandler handler = new();
            string taskType = WfTaskType.access.ToString();
            StateMatrix matrix = new();
            SetMatrix(handler, taskType, matrix);
            WfReqTask reqTask = new() { Id = 11, TicketId = 7, TaskType = taskType };
            WfTicket ticket = new() { Id = 7, Tasks = { reqTask } };
            handler.TicketList.Add(ticket);

            bool found = handler.TrySetReqTaskEnv(reqTask);

            Assert.Multiple(() =>
            {
                Assert.That(found, Is.True);
                Assert.That(handler.ActReqTask.Id, Is.EqualTo(11));
                Assert.That(handler.ActTicket, Is.SameAs(ticket));
                Assert.That(handler.ActStateMatrix, Is.SameAs(matrix));
            });
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
        public async Task AddReqTask_NewTicketAddsTaskWithoutAssigningTicketId()
        {
            WfHandler handler = new();
            handler.ActTicket = new WfTicket();
            handler.ActReqTask = new WfReqTask { TicketId = 0, TaskNumber = 1, TaskType = WfTaskType.access.ToString() };

            await handler.AddReqTask();

            Assert.Multiple(() =>
            {
                Assert.That(handler.ActReqTask.TicketId, Is.EqualTo(0));
                Assert.That(handler.ActTicket.Tasks, Has.Count.EqualTo(1));
                Assert.That(handler.ActTicket.Tasks[0], Is.SameAs(handler.ActReqTask));
            });
        }

        [Test]
        public async Task AddReqTask_LockedTicketDoesNotAddTask()
        {
            WfHandler handler = new();
            handler.ActTicket = new WfTicket { Id = 7, Locked = true };
            handler.ActReqTask = new WfReqTask { TaskNumber = 1, TaskType = WfTaskType.access.ToString() };

            await handler.AddReqTask();

            Assert.That(handler.ActTicket.Tasks, Is.Empty);
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
        public async Task ChangeReqTask_LockedTaskDoesNotReplaceTask()
        {
            WfHandler handler = new();
            WfReqTask oldTask = new() { Id = 11, TaskNumber = 2, Title = "Old" };
            handler.ActTicket = new WfTicket { Tasks = { oldTask } };
            handler.ActReqTask = new WfReqTask { Id = 11, TaskNumber = 2, Title = "New", Locked = true };

            await handler.ChangeReqTask();

            Assert.That(handler.ActTicket.Tasks[0], Is.SameAs(oldTask));
        }

        [Test]
        public async Task ChangeOwner_ReplacesTaskByTaskNumber()
        {
            WfHandler handler = new();
            WfReqTask oldTask = new() { Id = 11, TaskNumber = 2, Owners = [new FwoOwnerDataHelper { Owner = new FwoOwner { Id = 1 } }] };
            handler.ActTicket = new WfTicket { Tasks = { oldTask } };
            handler.ActReqTask = new WfReqTask { Id = 11, TaskNumber = 2, Owners = [new FwoOwnerDataHelper { Owner = new FwoOwner { Id = 2 } }] };

            await handler.ChangeOwner();

            Assert.Multiple(() =>
            {
                Assert.That(handler.ActTicket.Tasks, Has.Count.EqualTo(1));
                Assert.That(handler.ActTicket.Tasks[0], Is.SameAs(handler.ActReqTask));
                Assert.That(handler.ActTicket.Tasks[0].Owners[0].Owner.Id, Is.EqualTo(2));
            });
        }

        [Test]
        public async Task ChangeOwner_LockedTaskDoesNotReplaceTask()
        {
            WfHandler handler = new();
            WfReqTask oldTask = new() { Id = 11, TaskNumber = 2, Owners = [new FwoOwnerDataHelper { Owner = new FwoOwner { Id = 1 } }] };
            handler.ActTicket = new WfTicket { Tasks = { oldTask } };
            handler.ActReqTask = new WfReqTask
            {
                Id = 11,
                TaskNumber = 2,
                Locked = true,
                Owners = [new FwoOwnerDataHelper { Owner = new FwoOwner { Id = 2 } }]
            };

            await handler.ChangeOwner();

            Assert.That(handler.ActTicket.Tasks[0], Is.SameAs(oldTask));
        }

        [Test]
        public async Task ContinuePhase_AssignsCurrentHandlerWhenDifferent()
        {
            WfHandler handler = new();
            handler.userConfig.User.Dn = "cn=current";
            handler.userConfig.User.DbId = 10;
            string taskType = WfTaskType.access.ToString();
            SetMatrix(handler, taskType);
            WfReqTask reqTask = new()
            {
                Id = 11,
                TicketId = 7,
                TaskType = taskType,
                CurrentHandler = new UiUser { Dn = "cn=old", DbId = 9 }
            };
            handler.ActTicket = new WfTicket { Id = 7, Tasks = { reqTask } };
            handler.TicketList.Add(handler.ActTicket);

            await handler.ContinuePhase(reqTask);

            Assert.Multiple(() =>
            {
                Assert.That(handler.ActReqTask.CurrentHandler, Is.SameAs(handler.userConfig.User));
                Assert.That(handler.ActTicket.Tasks[0].CurrentHandler, Is.SameAs(handler.userConfig.User));
            });
        }

        [Test]
        public async Task StartWorkOnReqTask_AutoMovesToSingleStartedState()
        {
            WfHandler handler = new();
            handler.userConfig.User.Dn = "cn=current";
            handler.userConfig.User.DbId = 10;
            string taskType = WfTaskType.access.ToString();
            StateMatrix matrix = new()
            {
                Matrix = new Dictionary<int, List<int>> { [0] = [2] },
                LowestStartedState = 2,
                LowestEndState = 10
            };
            SetMatrix(handler, taskType, matrix);
            handler.MasterStateMatrix = new StateMatrix
            {
                LowestInputState = 1,
                LowestStartedState = 2,
                LowestEndState = 10,
                PhaseActive = new Dictionary<WorkflowPhases, bool> { [WorkflowPhases.planning] = true }
            };
            WfReqTask reqTask = new() { Id = 11, TicketId = 7, TaskType = taskType, StateId = 0 };
            handler.ActTicket = new WfTicket { Id = 7, Tasks = { reqTask } };
            handler.TicketList.Add(handler.ActTicket);

            await handler.StartWorkOnReqTask(reqTask, ObjAction.edit);

            Assert.Multiple(() =>
            {
                Assert.That(handler.ActReqTask.StateId, Is.EqualTo(2));
                Assert.That(handler.ActReqTask.CurrentHandler, Is.SameAs(handler.userConfig.User));
                Assert.That(handler.ActTicket.Tasks[0].StateId, Is.EqualTo(2));
                Assert.That(handler.DisplayReqTaskMode, Is.True);
                Assert.That(handler.EditReqTaskMode, Is.True);
            });
        }

        [Test]
        public async Task AssignReqTaskGroup_UpdatesAssignmentAndRecentHandler()
        {
            WfHandler handler = new();
            handler.userConfig.User.Dn = "cn=fallback";
            handler.userConfig.User.DbId = 10;
            handler.ActReqTask = new WfReqTask
            {
                Id = 11,
                CurrentHandler = new UiUser { Dn = "cn=current", DbId = 9 }
            };
            handler.ActTicket = new WfTicket { Tasks = { handler.ActReqTask } };
            handler.DisplayAssignReqTaskMode = true;

            await handler.AssignReqTaskGroup(new WfStatefulObject { AssignedGroup = "cn=group" });

            Assert.Multiple(() =>
            {
                Assert.That(handler.ActReqTask.AssignedGroup, Is.EqualTo("cn=group"));
                Assert.That(handler.ActReqTask.RecentHandler?.Dn, Is.EqualTo("cn=current"));
                Assert.That(handler.DisplayAssignReqTaskMode, Is.False);
            });
        }

        [Test]
        public async Task AssignReqTaskBack_AssignsRecentHandlerDnAndUsesCurrentAsRecent()
        {
            WfHandler handler = new();
            handler.userConfig.User.Dn = "cn=fallback";
            handler.userConfig.User.DbId = 10;
            handler.ActReqTask = new WfReqTask
            {
                Id = 11,
                CurrentHandler = new UiUser { Dn = "cn=current", DbId = 9 },
                RecentHandler = new UiUser { Dn = "cn=recent", DbId = 8 }
            };
            handler.ActTicket = new WfTicket { Tasks = { handler.ActReqTask } };
            handler.DisplayAssignReqTaskMode = true;

            await handler.AssignReqTaskBack();

            Assert.Multiple(() =>
            {
                Assert.That(handler.ActReqTask.AssignedGroup, Is.EqualTo("cn=recent"));
                Assert.That(handler.ActReqTask.RecentHandler?.Dn, Is.EqualTo("cn=current"));
                Assert.That(handler.DisplayAssignReqTaskMode, Is.False);
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
        public async Task ConfDeleteReqTask_MissingTaskOnlyClearsFlag()
        {
            WfHandler handler = new();
            handler.ActTicket = new WfTicket { Tasks = { new WfReqTask { Id = 12 } } };
            handler.ActReqTask = new WfReqTask { Id = 11 };
            handler.DisplayDeleteReqTaskMode = true;

            await handler.ConfDeleteReqTask();

            Assert.Multiple(() =>
            {
                Assert.That(handler.ActTicket.Tasks.Select(task => task.Id), Is.EqualTo(new long[] { 12 }));
                Assert.That(handler.DisplayDeleteReqTaskMode, Is.False);
            });
        }

        [Test]
        public async Task ConfDeleteReqTask_LockedTicketKeepsTaskAndClearsFlag()
        {
            WfHandler handler = new();
            WfReqTask reqTask = new() { Id = 11 };
            handler.ActTicket = new WfTicket { Locked = true, Tasks = { reqTask } };
            handler.ActReqTask = reqTask;
            handler.DisplayDeleteReqTaskMode = true;

            await handler.ConfDeleteReqTask();

            Assert.Multiple(() =>
            {
                Assert.That(handler.ActTicket.Tasks, Has.Count.EqualTo(1));
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
        public async Task ConfAddCommentToReqTask_StoresCreatorAndScope()
        {
            WfHandler handler = new();
            handler.userConfig.User.Dn = "cn=creator";
            handler.userConfig.User.DbId = 3;
            handler.ActReqTask = new WfReqTask();

            await handler.ConfAddCommentToReqTask("comment");

            WfComment comment = handler.ActReqTask.Comments.Single().Comment;
            Assert.Multiple(() =>
            {
                Assert.That(comment.Scope, Is.EqualTo(WfObjectScopes.RequestTask.ToString()));
                Assert.That(comment.Creator, Is.SameAs(handler.userConfig.User));
                Assert.That(comment.CreationDate, Is.Not.EqualTo(default(DateTime)));
            });
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
        public void GetRequestingOwner_ReturnsEmptyWhenAdditionalInfoMissing()
        {
            WfHandler handler = new();
            handler.AllOwners.Add(new FwoOwner { Id = 7, Name = "Owner" });
            handler.ActReqTask = new WfReqTask();

            string name = handler.GetRequestingOwner();

            Assert.That(name, Is.Empty);
        }

        [Test]
        public async Task SetAddInfoInReqTask_SetsAdditionalInfo()
        {
            WfHandler handler = new();
            WfReqTask task = new();

            await handler.SetAddInfoInReqTask(task, "Key", "Value");

            Assert.That(task.GetAddInfoValue("Key"), Is.EqualTo("Value"));
        }

        [Test]
        public async Task RemoveAddInfoInReqTask_RemovesAdditionalInfo()
        {
            WfHandler handler = new();
            WfReqTask task = new();
            task.SetAddInfo("Key", "Value");

            await handler.RemoveAddInfoInReqTask(task, "Key");

            Assert.That(task.GetAddInfoValue("Key"), Is.Empty);
        }

        [Test]
        public async Task RemoveAddInfoInReqTask_MissingKeyLeavesOtherValues()
        {
            WfHandler handler = new();
            WfReqTask task = new();
            task.SetAddInfo("Other", "Value");

            await handler.RemoveAddInfoInReqTask(task, "Key");

            Assert.That(task.GetAddInfoValue("Other"), Is.EqualTo("Value"));
        }

        [Test]
        public async Task HandlePathAnalysisAction_DefaultShowsPathAnalysisPopup()
        {
            WfHandler handler = new();

            await handler.HandlePathAnalysisAction();

            Assert.That(handler.DisplayPathAnalysisMode, Is.True);
        }

        [Test]
        public async Task HandlePathAnalysisAction_DisplayFoundDevicesShowsPathAnalysisPopup()
        {
            WfHandler handler = new();
            string externalParams = JsonSerializer.Serialize(new PathAnalysisActionParams { Option = PathAnalysisOptions.DisplayFoundDevices });

            await handler.HandlePathAnalysisAction(externalParams);

            Assert.That(handler.DisplayPathAnalysisMode, Is.True);
        }

        [Test]
        public async Task HandlePathAnalysisAction_WriteToDeviceListWithoutApiDoesNotShowPopup()
        {
            WfHandler handler = new();
            string externalParams = JsonSerializer.Serialize(new PathAnalysisActionParams { Option = PathAnalysisOptions.WriteToDeviceList });

            await handler.HandlePathAnalysisAction(externalParams);

            Assert.That(handler.DisplayPathAnalysisMode, Is.False);
        }

        [Test]
        public async Task HandlePathAnalysisAction_InvalidParamsDoesNotThrow()
        {
            WfHandler handler = new();

            await handler.HandlePathAnalysisAction("{");

            Assert.That(handler.DisplayPathAnalysisMode, Is.False);
        }

        private static WfHandler CreateReloadHandler(WfTicket refreshedTicket)
        {
            ReloadApiConnection apiConnection = new(refreshedTicket);
            WfHandler handler = new();
            handler.userConfig.ReqOwnerBased = false;
            WfDbAccess dbAccess = new((_, _, _, _) => { }, handler.userConfig, apiConnection, null!, true);
            SetPrivateField(handler, "dbAcc", dbAccess);
            return handler;
        }

        private static void SetPrivateField<T>(WfHandler handler, string fieldName, T value)
        {
            FieldInfo field = typeof(WfHandler).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException($"Field '{fieldName}' not found.");
            field.SetValue(handler, value);
        }

        private sealed class ReloadApiConnection : SimulatedApiConnection
        {
            private readonly WfTicket refreshedTicket;

            public ReloadApiConnection(WfTicket refreshedTicket)
            {
                this.refreshedTicket = refreshedTicket;
            }

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, FWO.Api.Client.QueryChunkingOptions? chunkingOptions = null)
            {
                if (typeof(QueryResponseType) == typeof(WfTicket))
                {
                    return Task.FromResult((QueryResponseType)(object)refreshedTicket);
                }

                throw new NotSupportedException($"Unexpected query type {typeof(QueryResponseType).Name}.");
            }
        }
    }
}
