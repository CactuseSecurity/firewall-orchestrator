using FWO.Data;
using FWO.Data.Workflow;
using FWO.Services.Workflow;
using NUnit.Framework;
using System.Reflection;

namespace FWO.Test
{
    [TestFixture]
    internal class WfHandlerImplTasksTest
    {
        private static void SetMatrix(WfHandler handler, string taskType, StateMatrix? matrix = null)
        {
            FieldInfo? field = typeof(WfHandler).GetField("stateMatrixDict", BindingFlags.NonPublic | BindingFlags.Instance);
            StateMatrixDict dict = (StateMatrixDict)(field?.GetValue(handler) ?? new StateMatrixDict());
            dict.Matrices[taskType] = matrix ?? new StateMatrix();
        }

        [Test]
        public void SetImplTaskOpt_SetsFlags()
        {
            WfHandler handler = new();

            handler.SetImplTaskOpt(ObjAction.add);

            Assert.That(handler.DisplayImplTaskMode, Is.True);
            Assert.That(handler.EditImplTaskMode, Is.True);
            Assert.That(handler.AddImplTaskMode, Is.True);
            Assert.That(handler.ImplementImplTaskMode, Is.False);
            Assert.That(handler.ReviewImplTaskMode, Is.False);
        }

        [Test]
        public void SetImplTaskPopUpOpt_SetsFlags()
        {
            WfHandler handler = new();

            handler.SetImplTaskPopUpOpt(ObjAction.displayAssign);
            Assert.That(handler.DisplayAssignImplTaskMode, Is.True);

            handler.SetImplTaskPopUpOpt(ObjAction.displayPromote);
            Assert.That(handler.DisplayPromoteImplTaskMode, Is.True);

            handler.SetImplTaskPopUpOpt(ObjAction.displayDelete);
            Assert.That(handler.DisplayDeleteImplTaskMode, Is.True);

            handler.SetImplTaskPopUpOpt(ObjAction.displayCleanup);
            Assert.That(handler.DisplayCleanupMode, Is.True);

            handler.SetImplTaskPopUpOpt(ObjAction.displayApprovals);
            Assert.That(handler.DisplayApprovalImplMode, Is.True);

            handler.SetImplTaskPopUpOpt(ObjAction.displayComment);
            Assert.That(handler.DisplayImplTaskCommentMode, Is.True);
        }

        [Test]
        public void ResetImplTaskActions_ClearsFlags()
        {
            WfHandler handler = new()
            {
                DisplayImplTaskMode = true,
                EditImplTaskMode = true,
                AddImplTaskMode = true,
                ImplementImplTaskMode = true,
                ReviewImplTaskMode = true,
                DisplayPromoteImplTaskMode = true,
                DisplayDeleteImplTaskMode = true,
                DisplayCleanupMode = true,
                DisplayAssignImplTaskMode = true,
                DisplayImplTaskCommentMode = true,
                DisplayApprovalImplMode = true
            };

            handler.ResetImplTaskActions();

            Assert.That(handler.DisplayImplTaskMode, Is.False);
            Assert.That(handler.EditImplTaskMode, Is.False);
            Assert.That(handler.AddImplTaskMode, Is.False);
            Assert.That(handler.ImplementImplTaskMode, Is.False);
            Assert.That(handler.ReviewImplTaskMode, Is.False);
            Assert.That(handler.DisplayPromoteImplTaskMode, Is.False);
            Assert.That(handler.DisplayDeleteImplTaskMode, Is.False);
            Assert.That(handler.DisplayCleanupMode, Is.False);
            Assert.That(handler.DisplayAssignImplTaskMode, Is.False);
            Assert.That(handler.DisplayImplTaskCommentMode, Is.False);
            Assert.That(handler.DisplayApprovalImplMode, Is.False);
        }

        [Test]
        public void SelectImplTask_SetsEnvironmentAndMode()
        {
            WfHandler handler = new();
            string taskType = WfTaskType.access.ToString();
            StateMatrix matrix = new() { LowestInputState = 1 };
            SetMatrix(handler, taskType, matrix);
            WfImplTask implTask = new() { Id = 21, TicketId = 7, ReqTaskId = 11, TaskNumber = 3, TaskType = taskType };
            WfReqTask reqTask = new() { Id = 11, TicketId = 7, TaskType = taskType, ImplementationTasks = { implTask } };
            WfTicket ticket = new() { Id = 7, Tasks = { reqTask } };
            handler.TicketList.Add(ticket);

            handler.SelectImplTask(implTask, ObjAction.implement);

            Assert.Multiple(() =>
            {
                Assert.That(handler.ActImplTask.Id, Is.EqualTo(21));
                Assert.That(handler.ActImplTask, Is.Not.SameAs(implTask));
                Assert.That(handler.ActTicket, Is.SameAs(ticket));
                Assert.That(handler.ActReqTask, Is.SameAs(reqTask));
                Assert.That(handler.ActStateMatrix, Is.SameAs(matrix));
                Assert.That(handler.DisplayImplTaskMode, Is.True);
                Assert.That(handler.ImplementImplTaskMode, Is.True);
                Assert.That(handler.EditImplTaskMode, Is.False);
            });
        }

        [Test]
        public void SelectImplTaskPopUp_SetsEnvironmentAndPopupMode()
        {
            WfHandler handler = new();
            string taskType = WfTaskType.access.ToString();
            SetMatrix(handler, taskType);
            WfImplTask implTask = new() { Id = 21, TicketId = 7, ReqTaskId = 11, TaskType = taskType };
            WfReqTask reqTask = new() { Id = 11, TicketId = 7, TaskType = taskType, ImplementationTasks = { implTask } };
            handler.TicketList.Add(new WfTicket { Id = 7, Tasks = { reqTask } });

            handler.SelectImplTaskPopUp(implTask, ObjAction.displayAssign);

            Assert.Multiple(() =>
            {
                Assert.That(handler.ActImplTask.Id, Is.EqualTo(21));
                Assert.That(handler.ActReqTask.Id, Is.EqualTo(11));
                Assert.That(handler.DisplayAssignImplTaskMode, Is.True);
            });
        }

        [Test]
        public async Task AddImplTask_AddsActiveImplementationTask()
        {
            WfHandler handler = new();
            handler.ActReqTask = new WfReqTask();
            handler.ActImplTask = new WfImplTask { TaskNumber = 1, Title = "Impl" };

            await handler.AddImplTask();

            Assert.Multiple(() =>
            {
                Assert.That(handler.ActReqTask.ImplementationTasks, Has.Count.EqualTo(1));
                Assert.That(handler.ActReqTask.ImplementationTasks[0], Is.SameAs(handler.ActImplTask));
            });
        }

        [Test]
        public async Task ChangeImplTask_ReplacesTaskByTaskNumber()
        {
            WfHandler handler = new();
            WfImplTask oldTask = new() { Id = 21, TaskNumber = 2, Title = "Old" };
            handler.ActReqTask = new WfReqTask { ImplementationTasks = { oldTask } };
            handler.ActImplTask = new WfImplTask { Id = 21, TaskNumber = 2, Title = "New" };

            await handler.ChangeImplTask();

            Assert.Multiple(() =>
            {
                Assert.That(handler.ActReqTask.ImplementationTasks, Has.Count.EqualTo(1));
                Assert.That(handler.ActReqTask.ImplementationTasks[0].Title, Is.EqualTo("New"));
                Assert.That(handler.ActReqTask.ImplementationTasks[0], Is.SameAs(handler.ActImplTask));
            });
        }

        [Test]
        public async Task ConfAddCommentToImplTask_AddsCommentAndClearsFlag()
        {
            WfHandler handler = new();
            handler.ActImplTask = new WfImplTask();
            handler.DisplayImplTaskCommentMode = true;

            await handler.ConfAddCommentToImplTask("comment");

            Assert.That(handler.ActImplTask.Comments, Has.Count.EqualTo(1));
            Assert.That(handler.ActImplTask.Comments[0].Comment.CommentText, Is.EqualTo("comment"));
            Assert.That(handler.DisplayImplTaskCommentMode, Is.False);
        }

        [Test]
        public async Task ConfDeleteImplTask_RemovesTaskAndClearsFlag()
        {
            WfHandler handler = new();
            WfImplTask implTask = new() { Id = 7 };
            handler.ActReqTask = new WfReqTask();
            handler.ActReqTask.ImplementationTasks.Add(implTask);
            handler.ActImplTask = implTask;
            handler.DisplayDeleteImplTaskMode = true;

            await handler.ConfDeleteImplTask();

            Assert.That(handler.ActReqTask.ImplementationTasks, Is.Empty);
            Assert.That(handler.DisplayDeleteImplTaskMode, Is.False);
        }

        [Test]
        public async Task ConfCleanupImplTasks_ClearsTasksAndClearsFlag()
        {
            WfHandler handler = new();
            handler.ActReqTask = new WfReqTask();
            handler.ActReqTask.ImplementationTasks.Add(new WfImplTask());
            handler.ActReqTask.ImplementationTasks.Add(new WfImplTask());
            handler.DisplayCleanupMode = true;

            await handler.ConfCleanupImplTasks();

            Assert.That(handler.ActReqTask.ImplementationTasks, Is.Empty);
            Assert.That(handler.DisplayCleanupMode, Is.False);
        }

        [Test]
        public void SelectDeviceImplTasks_FiltersByDevice()
        {
            WfHandler handler = new();
            WfImplTask implTask = new() { Id = 3, DeviceId = 7 };
            WfReqTask reqTask = new() { Id = 11, ImplementationTasks = { implTask } };
            WfTicket ticket = new() { Id = 5, Tasks = { reqTask } };
            handler.TicketList.Add(ticket);

            bool keepSelection = handler.SelectDeviceImplTasks(new Device { Id = 7 });

            Assert.That(keepSelection, Is.False);
            Assert.That(handler.AllVisibleImplTasks, Has.Count.EqualTo(1));
            Assert.That(handler.AllVisibleImplTasks[0].TicketId, Is.EqualTo(5));
            Assert.That(handler.AllVisibleImplTasks[0].ReqTaskId, Is.EqualTo(11));
        }

        [Test]
        public void SelectDeviceImplTasks_AllDevicesIncludesEveryImplementationTask()
        {
            WfHandler handler = new();
            WfReqTask reqTask = new()
            {
                Id = 11,
                ImplementationTasks =
                {
                    new WfImplTask { Id = 3, DeviceId = 7 },
                    new WfImplTask { Id = 4, DeviceId = 8 }
                }
            };
            handler.TicketList.Add(new WfTicket { Id = 5, Tasks = { reqTask } });

            bool keepSelection = handler.SelectDeviceImplTasks(new Device { Id = 0 });

            Assert.Multiple(() =>
            {
                Assert.That(keepSelection, Is.False);
                Assert.That(handler.AllVisibleImplTasks.Select(task => task.Id), Is.EqualTo(new long[] { 3, 4 }));
                Assert.That(handler.AllVisibleImplTasks.All(task => task.TicketId == 5), Is.True);
                Assert.That(handler.AllVisibleImplTasks.All(task => task.ReqTaskId == 11), Is.True);
            });
        }

        [Test]
        public void SelectDeviceImplTasks_NoSelectionKeepsSelectionAndClearsVisibleTasks()
        {
            WfHandler handler = new();
            handler.AllVisibleImplTasks.Add(new WfImplTask { Id = 99 });

            bool keepSelection = handler.SelectDeviceImplTasks(new Device { Id = -1 });

            Assert.Multiple(() =>
            {
                Assert.That(keepSelection, Is.True);
                Assert.That(handler.AllVisibleImplTasks, Is.Empty);
            });
        }

        [Test]
        public void SelectOwnerImplTasks_FiltersByOwner()
        {
            WfHandler handler = new();
            WfImplTask implTask = new() { Id = 3 };
            FwoOwner owner = new() { Id = 9, Name = "Owner" };
            WfReqTask reqTask = new()
            {
                Id = 11,
                ImplementationTasks = { implTask },
                Owners = { new FwoOwnerDataHelper { Owner = owner } }
            };
            WfTicket ticket = new() { Id = 5, Tasks = { reqTask } };
            handler.TicketList.Add(ticket);

            bool keepSelection = handler.SelectOwnerImplTasks(owner);

            Assert.That(keepSelection, Is.False);
            Assert.That(handler.AllVisibleImplTasks, Has.Count.EqualTo(1));
            Assert.That(handler.AllVisibleImplTasks[0].TicketId, Is.EqualTo(5));
            Assert.That(handler.AllVisibleImplTasks[0].ReqTaskId, Is.EqualTo(11));
        }

        [Test]
        public void SelectOwnerImplTasks_AllOwnersIncludesEveryImplementationTask()
        {
            WfHandler handler = new();
            WfReqTask reqTask = new()
            {
                Id = 11,
                ImplementationTasks =
                {
                    new WfImplTask { Id = 3 },
                    new WfImplTask { Id = 4 }
                }
            };
            handler.TicketList.Add(new WfTicket { Id = 5, Tasks = { reqTask } });

            bool keepSelection = handler.SelectOwnerImplTasks(new FwoOwner { Id = -1 });

            Assert.Multiple(() =>
            {
                Assert.That(keepSelection, Is.False);
                Assert.That(handler.AllVisibleImplTasks.Select(task => task.Id), Is.EqualTo(new long[] { 3, 4 }));
                Assert.That(handler.AllVisibleImplTasks.All(task => task.TicketId == 5), Is.True);
                Assert.That(handler.AllVisibleImplTasks.All(task => task.ReqTaskId == 11), Is.True);
            });
        }

        [Test]
        public void SelectOwnerImplTasks_NoSelectionKeepsSelectionAndClearsVisibleTasks()
        {
            WfHandler handler = new();
            handler.AllVisibleImplTasks.Add(new WfImplTask { Id = 99 });

            bool keepSelection = handler.SelectOwnerImplTasks(new FwoOwner { Id = -3 });

            Assert.Multiple(() =>
            {
                Assert.That(keepSelection, Is.True);
                Assert.That(handler.AllVisibleImplTasks, Is.Empty);
            });
        }

        [Test]
        public async Task AutoCreateImplTasks_EnterInReqTask_ResolvesAllMarkerToEveryDevice()
        {
            WfHandler handler = new()
            {
                userConfig = new SimulatedUserConfig { ReqAutoCreateImplTasks = AutoCreateImplTaskOptions.enterInReqTask },
                Devices = [new Device { Id = 3, Name = "FW-1" }, new Device { Id = 5, Name = "FW-2" }]
            };
            WfReqTask reqTask = new() { TaskType = WfTaskType.access.ToString(), StateId = 4, Title = "Access" };
            reqTask.SetDeviceList([WfReqTaskBase.kAllDevicesId]);

            await InvokeAutoCreateImplTasks(handler, reqTask);

            Assert.That(reqTask.ImplementationTasks.Select(task => task.DeviceId), Is.EqualTo(new int?[] { 3, 5 }));
        }

        [Test]
        public async Task AutoCreateImplTasks_OneTaskForAllDevices_CreatesSingleImplTaskWithoutConcreteDevice()
        {
            WfHandler handler = new()
            {
                userConfig = new SimulatedUserConfig { ReqAutoCreateImplTasks = AutoCreateImplTaskOptions.oneTaskForAllDevices },
                Devices = [new Device { Id = 3, Name = "FW-1" }, new Device { Id = 5, Name = "FW-2" }]
            };
            WfReqTask reqTask = new() { TaskType = WfTaskType.access.ToString(), StateId = 4, Title = "Access" };
            reqTask.SetDeviceList([WfReqTaskBase.kAllDevicesId]);

            await InvokeAutoCreateImplTasks(handler, reqTask);

            Assert.That(reqTask.ImplementationTasks, Has.Count.EqualTo(1));
            Assert.That(reqTask.ImplementationTasks[0].DeviceId, Is.Null);
        }

        [Test]
        public async Task AutoCreateImplTasks_OneTaskForAllDevices_TreatsEmptySelectionAsCombinedTask()
        {
            WfHandler handler = new()
            {
                userConfig = new SimulatedUserConfig { ReqAutoCreateImplTasks = AutoCreateImplTaskOptions.oneTaskForAllDevices },
                Devices = [new Device { Id = 3, Name = "FW-1" }, new Device { Id = 5, Name = "FW-2" }]
            };
            WfReqTask reqTask = new() { TaskType = WfTaskType.access.ToString(), StateId = 4, Title = "Access" };

            await InvokeAutoCreateImplTasks(handler, reqTask);

            Assert.That(reqTask.ImplementationTasks, Has.Count.EqualTo(1));
            Assert.That(reqTask.ImplementationTasks[0].DeviceId, Is.Null);
        }

        [Test]
        public async Task AutoCreateImplTasks_OneTaskForAllDevices_KeepsPerDeviceCreationForExplicitSelection()
        {
            WfHandler handler = new()
            {
                userConfig = new SimulatedUserConfig { ReqAutoCreateImplTasks = AutoCreateImplTaskOptions.oneTaskForAllDevices },
                Devices = [new Device { Id = 3, Name = "FW-1" }, new Device { Id = 5, Name = "FW-2" }]
            };
            WfReqTask reqTask = new() { TaskType = WfTaskType.access.ToString(), StateId = 4, Title = "Access" };
            reqTask.SetDeviceList([new Device { Id = 3 }, new Device { Id = 5 }]);

            await InvokeAutoCreateImplTasks(handler, reqTask);

            Assert.That(reqTask.ImplementationTasks.Select(task => task.DeviceId), Is.EqualTo(new int?[] { 3, 5 }));
        }

        [Test]
        public async Task AutoCreateImplTasks_NonAccessTask_CreatesSingleGenericImplTask()
        {
            WfHandler handler = new()
            {
                userConfig = new SimulatedUserConfig { ReqAutoCreateImplTasks = AutoCreateImplTaskOptions.forEachDevice },
                Devices = [new Device { Id = 3, Name = "FW-1" }, new Device { Id = 5, Name = "FW-2" }]
            };
            WfReqTask reqTask = new() { TaskType = WfTaskType.group_create.ToString(), StateId = 4, Title = "Group" };

            await InvokeAutoCreateImplTasks(handler, reqTask);

            Assert.That(reqTask.ImplementationTasks, Has.Count.EqualTo(1));
            Assert.That(reqTask.ImplementationTasks[0].DeviceId, Is.Null);
            Assert.That(reqTask.ImplementationTasks[0].TaskType, Is.EqualTo(WfTaskType.group_create.ToString()));
        }

        private static async Task InvokeAutoCreateImplTasks(WfHandler handler, WfReqTask reqTask)
        {
            MethodInfo method = typeof(WfHandler).GetMethod("AutoCreateImplTasks", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new MissingMethodException(typeof(WfHandler).FullName, "AutoCreateImplTasks");
            Task task = (Task)(method.Invoke(handler, [reqTask]) ?? throw new InvalidOperationException("AutoCreateImplTasks returned null."));
            await task;
        }
    }
}
