using FWO.Api.Client;
using FWO.Basics;
using FWO.Data;
using FWO.Data.DeviceSpecificSettings;
using FWO.Data.Workflow;
using FWO.Services;
using FWO.Services.Workflow;
using NUnit.Framework;
using System.Reflection;

namespace FWO.Test
{
    [TestFixture]
    internal class WfHandlerImplTasksTest
    {
        /// <summary>
        /// Plans every task type automatically, so the tests check the targets independently of the defaults.
        /// </summary>
        private static readonly GlobalDeviceSpecificSettings kAllAutoPlanned = new() { AutoPlanImplementationForRules = true };

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
        public void CanActOnImplTaskInCurrentPhase_OnlyAllowsStatesWithinPhaseBounds()
        {
            WfHandler handler = new();
            string taskType = WfTaskType.access.ToString();
            SetMatrix(handler, taskType, new StateMatrix { LowestInputState = 10, LowestEndState = 20 });

            Assert.Multiple(() =>
            {
                Assert.That(handler.CanActOnImplTaskInCurrentPhase(new WfImplTask { TaskType = taskType, StateId = 9 }), Is.False);
                Assert.That(handler.CanActOnImplTaskInCurrentPhase(new WfImplTask { TaskType = taskType, StateId = 10 }), Is.True);
                Assert.That(handler.CanActOnImplTaskInCurrentPhase(new WfImplTask { TaskType = taskType, StateId = 19 }), Is.True);
                Assert.That(handler.CanActOnImplTaskInCurrentPhase(new WfImplTask { TaskType = taskType, StateId = 20 }), Is.False);
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
        public async Task AddImplTask_ActivatesRulePositioningThroughCreationSeam()
        {
            WfReqTask requestTask = CreatePositioningRequestTask(11, [21]);
            WfHandler handler = new()
            {
                userConfig = new SimulatedUserConfig(),
                ActReqTask = requestTask,
                ActImplTask = new WfImplTask(requestTask) { DeviceId = 21, StateId = 4 },
                ActTicket = new WfTicket { Id = 7, Tasks = { requestTask } }
            };
            RecordingRulePositioningWorkflowCaller caller = RecordingRulePositioningWorkflowCaller.WithProductionApplicability();
            SetDbAccess(handler, new ImplTaskCreationApiConnection(handler.ActTicket), caller);

            await handler.AddImplTask();

            Assert.Multiple(() =>
            {
                Assert.That(caller.CalculationCount, Is.EqualTo(1));
                Assert.That(caller.LastRequestTask, Is.SameAs(requestTask));
                Assert.That(requestTask.ImplementationTasks, Has.Count.EqualTo(1));
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
                userConfig = new SimulatedUserConfig { ReqDeviceInput = ReqDeviceInputOptions.enterInReqTask },
                Devices = [new Device { Id = 3, Name = "FW-1" }, new Device { Id = 5, Name = "FW-2" }],
                AutoPlanSettingsOverride = kAllAutoPlanned
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
                userConfig = new SimulatedUserConfig { ReqDeviceInput = ReqDeviceInputOptions.oneTaskForAllDevices },
                Devices = [new Device { Id = 3, Name = "FW-1" }, new Device { Id = 5, Name = "FW-2" }],
                AutoPlanSettingsOverride = kAllAutoPlanned
            };
            WfReqTask reqTask = new() { TaskType = WfTaskType.access.ToString(), StateId = 4, Title = "Access" };
            reqTask.SetDeviceList([WfReqTaskBase.kAllDevicesId]);

            await InvokeAutoCreateImplTasks(handler, reqTask);

            Assert.That(reqTask.ImplementationTasks, Has.Count.EqualTo(1));
            Assert.That(reqTask.ImplementationTasks[0].DeviceId, Is.Null);
        }

        [Test]
        public async Task AutoCreateImplTasks_NoEnteredDeviceAndNoAlgorithm_CreatesFallbackTaskWithoutDevice()
        {
            WfHandler handler = new()
            {
                userConfig = new SimulatedUserConfig { ReqDeviceInput = ReqDeviceInputOptions.oneTaskForAllDevices },
                Devices = [new Device { Id = 3, Name = "FW-1" }, new Device { Id = 5, Name = "FW-2" }],
                AutoPlanSettingsOverride = kAllAutoPlanned
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
                userConfig = new SimulatedUserConfig { ReqDeviceInput = ReqDeviceInputOptions.oneTaskForAllDevices },
                Devices = [new Device { Id = 3, Name = "FW-1" }, new Device { Id = 5, Name = "FW-2" }],
                AutoPlanSettingsOverride = kAllAutoPlanned
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
                userConfig = new SimulatedUserConfig(),
                Devices = [new Device { Id = 3, Name = "FW-1" }, new Device { Id = 5, Name = "FW-2" }]
            };
            WfReqTask reqTask = new() { TaskType = WfTaskType.group_create.ToString(), StateId = 4, Title = "Group" };

            await InvokeAutoCreateImplTasks(handler, reqTask);

            Assert.That(reqTask.ImplementationTasks, Has.Count.EqualTo(1));
            Assert.That(reqTask.ImplementationTasks[0].DeviceId, Is.Null);
            Assert.That(reqTask.ImplementationTasks[0].TaskType, Is.EqualTo(WfTaskType.group_create.ToString()));
        }

        [Test]
        public async Task AutoCreateAccessImplTasks_ActivatesOncePerNewGatewayAndNotOnRerun()
        {
            WfReqTask requestTask = CreatePositioningRequestTask(11, [21, 22]);
            WfHandler handler = new()
            {
                userConfig = new SimulatedUserConfig { ReqAutoCreateImplTasks = AutoCreateImplTaskOptions.forEachDevice },
                Devices = [new Device { Id = 21 }, new Device { Id = 22 }],
                ActTicket = new WfTicket { Id = 7, Tasks = { requestTask } }
            };
            RecordingRulePositioningWorkflowCaller caller = RecordingRulePositioningWorkflowCaller.WithProductionApplicability();
            ImplTaskCreationApiConnection apiConnection = new(handler.ActTicket);
            SetDbAccess(handler, apiConnection, caller);

            await InvokeAutoCreateImplTasks(handler, requestTask);
            await InvokeAutoCreateImplTasks(handler, requestTask);

            Assert.Multiple(() =>
            {
                Assert.That(requestTask.ImplementationTasks.Select(task => task.DeviceId), Is.EqualTo(new int?[] { 21, 22 }));
                Assert.That(apiConnection.InsertCount, Is.EqualTo(2));
                Assert.That(caller.CalculationCount, Is.EqualTo(2));
                Assert.That(caller.Calls.Select(call => call.ImplementationTask.DeviceId), Is.EqualTo(new int?[] { 21, 22 }));
            });
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task AutoCreateAccessImplTasks_CombinedOrAllDeviceTaskDoesNotActivateRulePositioning(bool allDevices)
        {
            WfReqTask requestTask = CreatePositioningRequestTask(
                11,
                allDevices ? [WfReqTaskBase.kAllDevicesId] : []);
            WfHandler handler = new()
            {
                userConfig = new SimulatedUserConfig { ReqAutoCreateImplTasks = AutoCreateImplTaskOptions.oneTaskForAllDevices },
                ActTicket = new WfTicket { Id = 7, Tasks = { requestTask } }
            };
            RecordingRulePositioningWorkflowCaller caller = RecordingRulePositioningWorkflowCaller.WithProductionApplicability();
            ImplTaskCreationApiConnection apiConnection = new(handler.ActTicket);
            SetDbAccess(handler, apiConnection, caller);

            await InvokeAutoCreateImplTasks(handler, requestTask);

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.InsertCount, Is.EqualTo(1));
                Assert.That(requestTask.ImplementationTasks.Single().DeviceId, Is.Null);
                Assert.That(caller.CalculationCount, Is.Zero);
            });
        }

        [Test]
        public async Task BundledAccessTasks_ActivateOncePerPersistedTaskWithMergedGatewaySelection()
        {
            WfReqTask firstTask = CreateBundledAccessTask(11, "bundle-11-12", "src-1");
            WfReqTask secondTask = CreateBundledAccessTask(12, "bundle-11-12", "src-2");
            firstTask.ManagementId = 11;
            secondTask.ManagementId = 11;
            firstTask.SetDeviceList([21]);
            secondTask.SetDeviceList([22]);
            WfHandler handler = CreateBundlingHandler(considerBundling: true, firstTask, secondTask);
            handler.userConfig.ReqAutoCreateImplTasks = AutoCreateImplTaskOptions.enterInReqTask;
            handler.Devices = [new Device { Id = 21 }, new Device { Id = 22 }];
            RecordingRulePositioningWorkflowCaller caller = RecordingRulePositioningWorkflowCaller.WithProductionApplicability();
            ImplTaskCreationApiConnection apiConnection = new(handler.ActTicket);
            SetDbAccess(handler, apiConnection, caller);

            List<WfReqTask> effectiveTasks = await InvokeRequestTasksForInitialImplCreation(handler, [firstTask, secondTask]);
            await InvokeAutoCreateImplTasks(handler, effectiveTasks.Single());

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.InsertCount, Is.EqualTo(2));
                Assert.That(caller.CalculationCount, Is.EqualTo(2));
                Assert.That(caller.Calls.All(call => call.RequestTask.Id == firstTask.Id), Is.True);
                Assert.That(caller.Calls.All(call => call.RequestTask.GetDeviceList().SequenceEqual(new[] { 21, 22 })), Is.True);
            });
        }

        [Test]
        public async Task DelegatedCreation_ResolvesMergedBundleContextFromLoadedTicket()
        {
            WfReqTask firstTask = CreatePositioningRequestTask(11, [21]);
            WfReqTask secondTask = CreatePositioningRequestTask(12, [22]);
            firstTask.SetAddInfo(AdditionalInfoKeys.FlowBundleId, "bundle-11-12");
            secondTask.SetAddInfo(AdditionalInfoKeys.FlowBundleId, "bundle-11-12");
            WfHandler handler = new()
            {
                userConfig = new SimulatedUserConfig { ReqConsiderBundling = true },
                ActTicket = new WfTicket { Id = 7, Tasks = { firstTask, secondTask } }
            };
            RecordingRulePositioningWorkflowCaller caller = RecordingRulePositioningWorkflowCaller.WithProductionApplicability();
            ActionHandler actionHandler = new(new ImplTaskCreationApiConnection(handler.ActTicket), handler, null, true, null, null, caller);
            await actionHandler.Init([new WfState { Id = 4 }]);
            WfImplTask implementationTask = new(firstTask) { Id = 401, DeviceId = 22 };
            implementationTask.MarkCreatedStateChanged(4);

            await actionHandler.DoStateChangeActions(implementationTask, WfObjectScopes.ImplementationTask);

            Assert.Multiple(() =>
            {
                Assert.That(caller.CalculationCount, Is.EqualTo(1));
                Assert.That(caller.LastRequestTask!.Id, Is.EqualTo(firstTask.Id));
                Assert.That(caller.LastRequestTask.GetDeviceList(), Is.EqualTo(new[] { 21, 22 }));
            });
        }

        [Test]
        public void ResolveRulePositioningRequestTask_UsesOnlySafeParentFallbacks()
        {
            WfReqTask activeTicketTask = CreatePositioningRequestTask(11, [21]);
            WfReqTask listedTask = CreatePositioningRequestTask(12, [22]);
            WfReqTask activeRequestTask = CreatePositioningRequestTask(13, [23]);
            activeRequestTask.SetAddInfo(AdditionalInfoKeys.FlowBundleId, "active-only-bundle");
            WfHandler handler = new()
            {
                userConfig = new SimulatedUserConfig { ReqConsiderBundling = true },
                ActTicket = new WfTicket { Id = 7, Tasks = { activeTicketTask } },
                TicketList = [new WfTicket { Id = 8, Tasks = { listedTask } }],
                ActReqTask = activeRequestTask
            };

            WfReqTask? fromActiveTicket = handler.ResolveRulePositioningRequestTask(new WfImplTask { ReqTaskId = 11 });
            WfReqTask? fromTicketList = handler.ResolveRulePositioningRequestTask(new WfImplTask { ReqTaskId = 12 });
            WfReqTask? fromActiveRequest = handler.ResolveRulePositioningRequestTask(new WfImplTask { ReqTaskId = 13 });
            WfReqTask? missing = handler.ResolveRulePositioningRequestTask(new WfImplTask { ReqTaskId = 99 });
            handler.userConfig.ReqConsiderBundling = false;
            WfReqTask? withoutBundling = handler.ResolveRulePositioningRequestTask(new WfImplTask { ReqTaskId = 11 });

            Assert.Multiple(() =>
            {
                Assert.That(fromActiveTicket, Is.SameAs(activeTicketTask));
                Assert.That(fromTicketList, Is.SameAs(listedTask));
                Assert.That(fromActiveRequest, Is.SameAs(activeRequestTask));
                Assert.That(missing, Is.Null);
                Assert.That(withoutBundling, Is.SameAs(activeTicketTask));
            });
        }

        [Test]
        public async Task SyncImplTaskStatesAfterApproval_CreatesNoImplTasks()
        {
            WfReqTask reqTask = new()
            {
                Id = 11,
                TicketId = 7,
                TaskType = WfTaskType.access.ToString(),
                StateId = 4,
                Title = "Access"
            };
            WfHandler handler = new()
            {
                Phase = WorkflowPhases.request,
                userConfig = new SimulatedUserConfig
                {
                    ReqDeviceInput = ReqDeviceInputOptions.oneTaskForAllDevices
                },
                MasterStateMatrix = new StateMatrix
                {
                    LowestEndState = 3,
                    MinTicketCompleted = 99
                },
                ActTicket = new WfTicket
                {
                    Id = 7,
                    StateId = 4,
                    Tasks = { reqTask }
                }
            };
            SetMatrix(handler, WfTaskType.access.ToString(), new StateMatrix
            {
                LowestInputState = 0,
                LowestStartedState = 2,
                LowestEndState = 10,
                MinImplTasksNeeded = 3,
                MinTicketCompleted = 99
            });

            await InvokeSyncImplTaskStatesAfterApproval(handler);

            Assert.That(reqTask.ImplementationTasks, Is.Empty, "only the action creates implementation tasks");
        }

        [Test]
        public async Task CreateImplTasksAction_ConsiderBundlingFalse_CreatesPerRequestTask()
        {
            WfReqTask firstTask = CreateBundledAccessTask(11, "bundle-11-12", "src-1");
            WfReqTask secondTask = CreateBundledAccessTask(12, "bundle-11-12", "src-2");
            WfHandler handler = CreateBundlingHandler(considerBundling: false, firstTask, secondTask);

            await handler.CreateMissingImplTasksForTicket(handler.ActTicket);

            Assert.That(firstTask.ImplementationTasks, Has.Count.EqualTo(1));
            Assert.That(secondTask.ImplementationTasks, Has.Count.EqualTo(1));
            Assert.That(firstTask.ImplementationTasks[0].ImplElements.Select(element => element.Name), Is.EqualTo(new[] { "src-1" }));
            Assert.That(secondTask.ImplementationTasks[0].ImplElements.Select(element => element.Name), Is.EqualTo(new[] { "src-2" }));
        }

        [Test]
        public async Task CreateImplTasksAction_ConsiderBundlingTrue_MergesBundledRequestTasks()
        {
            WfReqTask firstTask = CreateBundledAccessTask(11, "bundle-11-12", "src-1");
            WfReqTask secondTask = CreateBundledAccessTask(12, "bundle-11-12", "src-2");
            WfReqTask unbundledTask = CreateBundledAccessTask(13, "", "src-3");
            WfHandler handler = CreateBundlingHandler(considerBundling: true, firstTask, secondTask, unbundledTask);

            await handler.CreateMissingImplTasksForTicket(handler.ActTicket);

            Assert.That(firstTask.ImplementationTasks, Has.Count.EqualTo(1));
            Assert.That(secondTask.ImplementationTasks, Is.Empty);
            Assert.That(unbundledTask.ImplementationTasks, Has.Count.EqualTo(1));
            Assert.That(firstTask.ImplementationTasks[0].ReqTaskId, Is.EqualTo(11));
            Assert.That(firstTask.ImplementationTasks[0].ImplElements.Select(element => element.Name), Is.EqualTo(new[] { "src-1", "src-2" }));
            Assert.That(unbundledTask.ImplementationTasks[0].ImplElements.Select(element => element.Name), Is.EqualTo(new[] { "src-3" }));
        }

        [Test]
        public async Task CreateImplTasksAction_ConsiderBundlingTrue_SkipsBundleWhenAnyBundledTaskAlreadyHasImplTask()
        {
            WfReqTask firstTask = CreateBundledAccessTask(11, "bundle-11-12", "src-1");
            WfReqTask secondTask = CreateBundledAccessTask(12, "bundle-11-12", "src-2");
            secondTask.ImplementationTasks.Add(new WfImplTask { Id = 21, ReqTaskId = secondTask.Id });
            WfHandler handler = CreateBundlingHandler(considerBundling: true, firstTask, secondTask);

            await handler.CreateMissingImplTasksForTicket(handler.ActTicket);

            Assert.Multiple(() =>
            {
                Assert.That(firstTask.ImplementationTasks, Is.Empty);
                Assert.That(secondTask.ImplementationTasks, Has.Count.EqualTo(1));
            });
        }

        [Test]
        public void CanAutoCreateInitialImplTasks_ReturnsFalseWhenBundledSiblingAlreadyHasImplTask()
        {
            WfReqTask firstTask = CreateBundledAccessTask(11, "bundle-11-12", "src-1");
            WfReqTask secondTask = CreateBundledAccessTask(12, "bundle-11-12", "src-2");
            secondTask.ImplementationTasks.Add(new WfImplTask { Id = 21, ReqTaskId = secondTask.Id });
            WfHandler handler = CreateBundlingHandler(considerBundling: true, firstTask, secondTask);

            bool canAutoCreate = handler.CanAutoCreateInitialImplTasks(handler.ActTicket, firstTask);

            Assert.That(canAutoCreate, Is.False);
        }

        [Test]
        public void CanAutoCreateInitialImplTasks_ReturnsFalseBeforeImplementationInputState()
        {
            WfReqTask requestTask = new()
            {
                Id = 11,
                TicketId = 7,
                TaskType = WfTaskType.group_create.ToString(),
                StateId = 49
            };
            WfHandler handler = CreateImplementationCreationHandler(requestTask, implementationInputState: 99);

            bool canAutoCreate = handler.CanAutoCreateInitialImplTasks(requestTask);

            requestTask.StateId = 99;
            bool canAutoCreateAtInput = handler.CanAutoCreateInitialImplTasks(requestTask);

            Assert.Multiple(() =>
            {
                Assert.That(canAutoCreate, Is.False);
                Assert.That(canAutoCreateAtInput, Is.True);
            });
        }

        [Test]
        public async Task AutoCreateInitialImplTasksForMonitoring_ReturnsFalseWhenBundledSiblingAlreadyHasImplTask()
        {
            WfReqTask firstTask = CreateBundledAccessTask(11, "bundle-11-12", "src-1");
            WfReqTask secondTask = CreateBundledAccessTask(12, "bundle-11-12", "src-2");
            secondTask.ImplementationTasks.Add(new WfImplTask { Id = 21, ReqTaskId = secondTask.Id });
            WfHandler handler = CreateBundlingHandler(considerBundling: true, firstTask, secondTask);

            bool created = await handler.AutoCreateInitialImplTasksForMonitoring(handler.ActTicket, firstTask);

            Assert.Multiple(() =>
            {
                Assert.That(created, Is.False);
                Assert.That(firstTask.ImplementationTasks, Is.Empty);
                Assert.That(secondTask.ImplementationTasks, Has.Count.EqualTo(1));
            });
        }

        [Test]
        public async Task CreateImplTasksAction_ConsiderBundlingTrue_DeduplicatesCommonElements()
        {
            WfReqTask firstTask = CreateBundledAccessTask(11, "bundle-11-12", "shared-source");
            firstTask.Elements[0].IpString = "10.0.0.1";
            firstTask.Elements.Add(CreateBundleElement(11, ElemFieldType.destination, "dst-1", "10.0.1.1"));
            WfReqTask secondTask = CreateBundledAccessTask(12, "bundle-11-12", "shared-source");
            secondTask.Elements[0].IpString = "10.0.0.1";
            secondTask.Elements.Add(CreateBundleElement(12, ElemFieldType.destination, "dst-2", "10.0.1.2"));
            WfHandler handler = CreateBundlingHandler(considerBundling: true, firstTask, secondTask);

            await handler.CreateMissingImplTasksForTicket(handler.ActTicket);

            Assert.That(firstTask.ImplementationTasks, Has.Count.EqualTo(1));
            Assert.That(firstTask.ImplementationTasks[0].ImplElements.Select(element => element.Name),
                Is.EqualTo(new[] { "shared-source", "dst-1", "dst-2" }));
        }

        [Test]
        public async Task UpdateRequestTasksFromTicket_CreatesNoImplTasks()
        {
            WfReqTask firstTask = CreateBundledAccessTask(11, "bundle-11-12", "src-1");
            WfReqTask secondTask = CreateBundledAccessTask(12, "bundle-11-12", "src-2");
            WfHandler handler = CreateBundlingHandler(considerBundling: true, firstTask, secondTask);

            await InvokeUpdateRequestTasksFromTicket(handler);

            Assert.Multiple(() =>
            {
                Assert.That(firstTask.ImplementationTasks, Is.Empty, "only the action creates implementation tasks");
                Assert.That(secondTask.ImplementationTasks, Is.Empty);
            });
        }

        [Test]
        public async Task RequestTasksForInitialImplCreation_LoadsMissingTaskDetailsBeforeBundling()
        {
            WfReqTask overviewFirstTask = CreateBundledAccessTask(11, "bundle-11-12", "overview-1");
            WfReqTask overviewSecondTask = CreateBundledAccessTask(12, "bundle-11-12", "overview-2");
            overviewFirstTask.Elements.Clear();
            overviewSecondTask.Elements.Clear();
            WfReqTask fullFirstTask = CreateBundledAccessTask(11, "bundle-11-12", "src-1");
            WfReqTask fullSecondTask = CreateBundledAccessTask(12, "bundle-11-12", "src-2");
            WfTicket fullTicket = new() { Id = 7, Tasks = { fullFirstTask, fullSecondTask } };
            WfHandler handler = CreateBundlingHandler(considerBundling: true, overviewFirstTask, overviewSecondTask);
            SetDbAccess(handler, new RequestTaskDetailsApiConn(fullTicket));

            List<WfReqTask> requestTasks = await InvokeRequestTasksForInitialImplCreation(handler, [overviewFirstTask, overviewSecondTask]);

            Assert.That(requestTasks, Has.Count.EqualTo(1));
            Assert.That(requestTasks[0].Elements.Select(element => element.Name), Is.EqualTo(new[] { "src-1", "src-2" }));
        }

        [Test]
        public async Task RequestTasksForInitialImplCreation_SkipsAccessTasksWithoutElements()
        {
            WfReqTask overviewTask = CreateBundledAccessTask(11, "", "overview");
            overviewTask.Elements.Clear();
            WfReqTask fullTask = CreateBundledAccessTask(11, "", "full");
            fullTask.Elements.Clear();
            WfTicket fullTicket = new() { Id = 7, Tasks = { fullTask } };
            WfHandler handler = CreateBundlingHandler(considerBundling: false, overviewTask);
            SetDbAccess(handler, new RequestTaskDetailsApiConn(fullTicket));

            List<WfReqTask> requestTasks = await InvokeRequestTasksForInitialImplCreation(handler, [overviewTask]);

            Assert.That(requestTasks, Is.Empty);
        }

        [Test]
        public async Task RequestTasksForInitialImplCreation_DoesNotReloadNewInterfaceTasksWithoutElements()
        {
            WfReqTask newInterfaceTask = new()
            {
                Id = 11,
                TicketId = 7,
                TaskType = WfTaskType.new_interface.ToString()
            };
            WfHandler handler = CreateBundlingHandler(considerBundling: false, newInterfaceTask);
            SetDbAccess(handler, new RequestTaskDetailsApiConn(new WfTicket { Id = 7 }));

            List<WfReqTask> requestTasks = await InvokeRequestTasksForInitialImplCreation(handler,
                new List<WfReqTask> { newInterfaceTask });

            Assert.That(requestTasks, Has.Count.EqualTo(1));
            Assert.That(requestTasks[0], Is.SameAs(newInterfaceTask));
        }

        [Test]
        public async Task RequestTasksForInitialImplCreation_SkipsTaskFoundDuringDatabaseReload()
        {
            WfReqTask overviewTask = new()
            {
                Id = 11,
                TicketId = 7,
                TaskType = WfTaskType.group_create.ToString(),
                StateId = 99,
                Title = "Group"
            };
            WfReqTask storedTask = new()
            {
                Id = 11,
                TicketId = 7,
                TaskType = WfTaskType.group_create.ToString(),
                StateId = 99,
                Title = "Group",
                ImplementationTasks = { new WfImplTask { Id = 673, ReqTaskId = 11, StateId = 100 } }
            };
            WfTicket storedTicket = new() { Id = 7, Tasks = { storedTask } };
            WfHandler handler = CreateImplementationCreationHandler(overviewTask, implementationInputState: 99);
            SetDbAccess(handler, new RequestTaskDetailsApiConn(storedTicket));
            List<WfReqTask> candidateTasks = new() { overviewTask };

            List<WfReqTask> requestTasks = await InvokeRequestTasksForInitialImplCreation(handler, candidateTasks);

            Assert.That(requestTasks, Is.Empty);
        }

        [Test]
        public async Task LoadReqTaskDetailsForImplCreation_PreservesDerivedTicketState()
        {
            WfReqTask overviewTask = new()
            {
                Id = 11,
                TicketId = 7,
                TaskType = WfTaskType.group_create.ToString(),
                StateId = 99
            };
            WfReqTask storedTask = new(overviewTask);
            WfTicket storedTicket = new() { Id = 7, StateId = 49, Tasks = { storedTask } };
            WfHandler handler = CreateImplementationCreationHandler(overviewTask, implementationInputState: 99);
            handler.ActTicket.StateId = 100;
            SetDbAccess(handler, new RequestTaskDetailsApiConn(storedTicket));

            await InvokeLoadReqTaskDetailsForImplCreation(handler, overviewTask);

            Assert.That(handler.ActTicket.StateId, Is.EqualTo(100));
        }

        private static async Task InvokeAutoCreateImplTasks(WfHandler handler, WfReqTask reqTask)
        {
            MethodInfo method = typeof(WfHandler).GetMethod("AutoCreateImplTasks", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new MissingMethodException(typeof(WfHandler).FullName, "AutoCreateImplTasks");
            object[] arguments = [reqTask];
            Task task = (Task)(method.Invoke(handler, arguments) ?? throw new InvalidOperationException("AutoCreateImplTasks returned null."));
            await task;
        }

        private static async Task InvokeSyncImplTaskStatesAfterApproval(WfHandler handler)
        {
            MethodInfo method = typeof(WfHandler).GetMethod("SyncImplTaskStatesAfterApproval", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new MissingMethodException(typeof(WfHandler).FullName, "SyncImplTaskStatesAfterApproval");
            object[] arguments = [];
            Task task = (Task)(method.Invoke(handler, arguments) ?? throw new InvalidOperationException("SyncImplTaskStatesAfterApproval returned null."));
            await task;
        }

        private static async Task InvokeUpdateRequestTasksFromTicket(WfHandler handler)
        {
            MethodInfo method = typeof(WfHandler).GetMethod("UpdateRequestTasksFromTicket", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new MissingMethodException(typeof(WfHandler).FullName, "UpdateRequestTasksFromTicket");
            object[] arguments = [true, null!, null!];
            Task task = (Task)(method.Invoke(handler, arguments) ?? throw new InvalidOperationException("UpdateRequestTasksFromTicket returned null."));
            await task;
        }

        private static async Task<List<WfReqTask>> InvokeRequestTasksForInitialImplCreation(WfHandler handler, List<WfReqTask> requestTasks)
        {
            MethodInfo method = typeof(WfHandler).GetMethod("RequestTasksForInitialImplCreation", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new MissingMethodException(typeof(WfHandler).FullName, "RequestTasksForInitialImplCreation");
            Task<List<WfReqTask>> task = (Task<List<WfReqTask>>)(method.Invoke(handler, [requestTasks, false])
                ?? throw new InvalidOperationException("RequestTasksForInitialImplCreation returned null."));
            return await task;
        }

        private static async Task<WfReqTask> InvokeLoadReqTaskDetailsForImplCreation(WfHandler handler, WfReqTask reqTask)
        {
            MethodInfo method = typeof(WfHandler).GetMethod("LoadReqTaskDetailsForImplCreation", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new MissingMethodException(typeof(WfHandler).FullName, "LoadReqTaskDetailsForImplCreation");
            Task<WfReqTask> task = (Task<WfReqTask>)(method.Invoke(handler, [reqTask])
                ?? throw new InvalidOperationException("LoadReqTaskDetailsForImplCreation returned null."));
            return await task;
        }

        private static void SetDbAccess(WfHandler handler, SimulatedApiConnection apiConnection)
        {
            ActionHandler actionHandler = new(apiConnection, handler);
            WfDbAccess dbAccess = new(DefaultInit.DoNothing, handler.userConfig, apiConnection, actionHandler, false, WorkflowPhases.request);
            FieldInfo? field = typeof(WfHandler).GetField("dbAcc", BindingFlags.NonPublic | BindingFlags.Instance);
            field?.SetValue(handler, dbAccess);
        }

        private static void SetDbAccess(
            WfHandler handler,
            SimulatedApiConnection apiConnection,
            RecordingRulePositioningWorkflowCaller caller)
        {
            ActionHandler actionHandler = new(apiConnection, handler, null, true, null, null, caller);
            actionHandler.Init([new WfState { Id = 4 }]).GetAwaiter().GetResult();
            WfDbAccess dbAccess = new(
                DefaultInit.DoNothing,
                handler.userConfig,
                apiConnection,
                actionHandler,
                false,
                WorkflowPhases.request,
                false);
            FieldInfo? field = typeof(WfHandler).GetField("dbAcc", BindingFlags.NonPublic | BindingFlags.Instance);
            field?.SetValue(handler, dbAccess);
        }

        private static WfHandler CreateBundlingHandler(bool considerBundling, params WfReqTask[] requestTasks)
        {
            WfHandler handler = new()
            {
                userConfig = new SimulatedUserConfig
                {
                    ReqDeviceInput = ReqDeviceInputOptions.oneTaskForAllDevices,
                    ReqConsiderBundling = considerBundling
                },
                AutoPlanSettingsOverride = kAllAutoPlanned,
                MasterStateMatrix = new StateMatrix
                {
                    LowestEndState = 3,
                    PhaseActive = new() { { WorkflowPhases.planning, false } }
                },
                ActTicket = new WfTicket { Id = 7, StateId = 3, Tasks = [.. requestTasks] }
            };
            SetMatrix(handler, WfTaskType.access.ToString(), new StateMatrix
            {
                LowestInputState = 0,
                LowestStartedState = 2,
                LowestEndState = 10,
                MinImplTasksNeeded = 3,
                MinTicketCompleted = 99,
                PhaseActive = new() { { WorkflowPhases.planning, false } }
            });
            return handler;
        }

        private static WfHandler CreateImplementationCreationHandler(WfReqTask requestTask,
            int implementationInputState)
        {
            WfHandler handler = new()
            {
                userConfig = new SimulatedUserConfig
                {
                    ReqDeviceInput = ReqDeviceInputOptions.oneTaskForAllDevices
                },
                MasterStateMatrix = new StateMatrix
                {
                    LowestEndState = 49,
                    PhaseActive = new() { { WorkflowPhases.planning, false } }
                },
                ActTicket = new WfTicket { Id = 7, StateId = 99, Tasks = { requestTask } }
            };
            SetMatrix(handler, requestTask.TaskType, new StateMatrix
            {
                LowestInputState = 0,
                LowestEndState = 249,
                MinImplTasksNeeded = implementationInputState,
                MinTicketCompleted = 299,
                PhaseActive = new()
                {
                    { WorkflowPhases.planning, false },
                    { WorkflowPhases.implementation, true }
                }
            });
            return handler;
        }

        private static WfReqTask CreateBundledAccessTask(long id, string bundleId, string elementName)
        {
            WfReqTask reqTask = new()
            {
                Id = id,
                TicketId = 7,
                StateId = 4,
                Title = $"Access {id}",
                TaskType = WfTaskType.access.ToString(),
                Elements =
                [
                    CreateBundleElement(id, ElemFieldType.source, elementName, $"10.0.0.{id}")
                ]
            };
            if (!string.IsNullOrWhiteSpace(bundleId))
            {
                reqTask.SetAddInfo(AdditionalInfoKeys.FlowBundleId, bundleId);
            }
            return reqTask;
        }

        private static WfReqTask CreatePositioningRequestTask(long id, List<int> gatewayIds)
        {
            WfReqTask requestTask = new()
            {
                Id = id,
                TicketId = 7,
                StateId = 4,
                Title = $"Access {id}",
                TaskType = WfTaskType.access.ToString(),
                RequestAction = RequestAction.create.ToString(),
                ManagementId = 11
            };
            requestTask.SetDeviceList(gatewayIds);
            return requestTask;
        }

        private static WfReqElement CreateBundleElement(long taskId, ElemFieldType field, string name, string ip)
        {
            return new WfReqElement
            {
                TaskId = taskId,
                Field = field.ToString(),
                Name = name,
                IpString = ip
            };
        }

        private sealed class RequestTaskDetailsApiConn(WfTicket fullTicket) : SimulatedApiConnection
        {
            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                if (typeof(QueryResponseType) == typeof(WfTicket))
                {
                    return Task.FromResult((QueryResponseType)(object)fullTicket);
                }
                throw new InvalidOperationException($"Unexpected query: {query}");
            }
        }
    }
}
