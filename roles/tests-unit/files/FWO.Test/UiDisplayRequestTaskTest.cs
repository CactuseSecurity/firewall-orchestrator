using Bunit;
using FWO.Api.Client;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Workflow;
using FWO.Middleware.Client;
using FWO.Services;
using FWO.Services.Workflow;
using FWO.Ui.Pages.Request;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Reflection;
using static FWO.Test.UiRequestCoverageTest;
using static FWO.Test.UiRequestWorkflowTest;

namespace FWO.Test
{
    [TestFixture]
    internal class UiDisplayRequestTaskTest
    {
        private static readonly int[] kRemovedOwnerIds = [1];
        private static readonly int[] kNewOwnerIds = [2];

        /// <summary>
        /// Verifies that approver-specific field permissions control editing and saving.
        /// </summary>
        [Test]
        public void CanEditAndSaveFields_UsesApproverConfig()
        {
            DisplayRequestTask component = new();
            SimulatedUserConfig userConfig = new();
            ApproverAllowedChangesConfig config = new();
            config.SetTaskField(WfTaskType.access, WorkflowEditableFieldKeys.Services, true);
            config.SetTaskField(WfTaskType.access, WorkflowEditableFieldKeys.Reason, true);
            userConfig.ReqAllowedChangesByApprover = config.ToConfigValue();
            SetPrivateField(component, "userConfig", userConfig);
            SetPrivateField(component, nameof(DisplayRequestTask.WfHandler), new WfHandler
            {
                ActReqTask = new WfReqTask { Title = "Valid", TaskType = WfTaskType.access.ToString() },
                ApproveReqTaskMode = true
            });
            SetPrivateField(component, nameof(DisplayRequestTask.Phase), WorkflowPhases.approval);
            SetPrivateField(component, "actTaskType", WfTaskType.access);

            Assert.Multiple(() =>
            {
                Assert.That(InvokePrivate<bool>(component, "CanEditReqTaskField", WorkflowEditableFieldKeys.Services), Is.True);
                Assert.That(InvokePrivate<bool>(component, "CanSaveReqTaskChanges"), Is.True);
            });
        }

        [Test]
        public void AccessTaskVisibilityConfigControlsDescriptiveFields()
        {
            DisplayRequestTask component = new();
            TicketFieldVisibilityConfig visibility = new();
            visibility.AccessTask.ShowManagement = false;
            visibility.AccessTask.ShowOwner = false;
            visibility.AccessTask.ShowValidFrom = false;
            visibility.AccessTask.ShowValidTo = false;
            visibility.AccessTask.ShowReason = false;
            visibility.AccessTask.ShowFreeText = true;
            visibility.AccessTask.ShowComments = false;
            SimulatedUserConfig userConfig = new() { ReqTicketFieldVisibility = visibility.ToConfigValue() };
            SetPrivateField(component, "userConfig", userConfig);
            SetPrivateField(component, "actTaskType", WfTaskType.access);

            Assert.Multiple(() =>
            {
                Assert.That(InvokePrivate<bool>(component, "ShowTaskField", WorkflowEditableFieldKeys.Management), Is.False);
                Assert.That(InvokePrivate<bool>(component, "ShowTaskField", WorkflowEditableFieldKeys.Owner), Is.False);
                Assert.That(InvokePrivate<bool>(component, "ShowTaskField", WorkflowEditableFieldKeys.ValidFrom), Is.False);
                Assert.That(InvokePrivate<bool>(component, "ShowTaskField", WorkflowEditableFieldKeys.ValidTo), Is.False);
                Assert.That(TicketFieldVisibilityConfig.Parse(userConfig.ReqTicketFieldVisibility).AccessTask.ShowFreeText, Is.True);
            });
        }

        [Test]
        public async Task AccessTaskFreeTextVisibility_DoesNotHideVisibleEndDate()
        {
            TicketFieldVisibilityConfig visibility = new();
            visibility.AccessTask.ShowFreeText = true;
            visibility.AccessTask.ShowValidFrom = false;
            visibility.AccessTask.ShowValidTo = true;

            WfReqTask task = new()
            {
                Id = 1,
                Title = "Access task",
                TaskType = WfTaskType.access.ToString(),
                TargetEndDate = DateTime.Today.AddDays(1)
            };
            WfHandler handler = new()
            {
                DisplayReqTaskMode = true,
                ActReqTask = task,
                ActTicket = new WfTicket { Tasks = [task] }
            };

            await using BunitContext context = new();
            context.Services.AddSingleton<UserConfig>(new RequestWorkflowUserConfig
            {
                ReqTicketFieldVisibility = visibility.ToConfigValue()
            });
            IRenderedComponent<DisplayRequestTask> component = RenderDisplayRequestTask(
                context, handler, new WfStateDict(), Roles.Requester);

            Assert.Multiple(() =>
            {
                Assert.That(component.Markup, Does.Not.Contain("valid_from"));
                Assert.That(component.Markup, Does.Contain("valid_to"));
            });
        }

        [Test]
        public async Task DisplayRequestTask_ActionAndSaveMethods_DoNothingWhileBusy()
        {
            DisplayRequestTask component = new();
            SetPrivateField(component, "WorkInProgress", true);
            SetPrivateField(component, nameof(DisplayRequestTask.WfHandler), new WfHandler
            {
                ActReqTask = new WfReqTask { Title = "Task", TaskType = WfTaskType.generic.ToString() }
            });

            await InvokePrivateTask(component, "PerformAction", new WfStateAction());
            await InvokePrivateTask(component, "ConfApproveTask", new WfApproval());
            await InvokePrivateTask(component, "SaveReqTask");

            Assert.That(GetPrivateField<bool>(component, "WorkInProgress"), Is.True);
        }

        [Test]
        public async Task DisplayRequestTask_PerformAction_ExecutesNormalReloadAndResetPath()
        {
            WfReqTask task = new() { Id = 0, TaskNumber = 1, Title = "Task", TaskType = WfTaskType.generic.ToString() };
            WfHandler handler = new()
            {
                DisplayReqTaskMode = true,
                EditReqTaskMode = true,
                ActReqTask = task,
                ActTicket = new WfTicket { Tasks = [task] }
            };

            await using BunitContext context = new();
            IRenderedComponent<DisplayRequestTask> component = RenderDisplayRequestTask(context, handler, new WfStateDict(), Roles.Requester);

            await component.InvokeAsync(() => InvokePrivateTask(component.Instance, "PerformAction", new WfStateAction()));

            Assert.That(GetMember<bool>(component.Instance, "WorkInProgress"), Is.False);
        }

        [Test]
        public async Task DisplayRequestTask_SaveReqTask_ChangesExistingTaskThroughNormalPath()
        {
            WfReqTask task = new() { Id = 0, TaskNumber = 1, Title = "Task", TaskType = WfTaskType.generic.ToString() };
            WfHandler handler = new()
            {
                DisplayReqTaskMode = true,
                EditReqTaskMode = true,
                ActReqTask = task,
                ActTicket = new WfTicket { Tasks = [task] }
            };

            await using BunitContext context = new();
            IRenderedComponent<DisplayRequestTask> component = RenderDisplayRequestTask(context, handler, new WfStateDict(), Roles.Requester);

            await component.InvokeAsync(() => InvokePrivateTask(component.Instance, "SaveReqTask"));

            Assert.Multiple(() =>
            {
                Assert.That(GetMember<bool>(component.Instance, "WorkInProgress"), Is.False);
                Assert.That(handler.ActTicket.Tasks, Has.Count.EqualTo(1));
                Assert.That(handler.ActTicket.Tasks[0], Is.SameAs(task));
            });
        }

        [Test]
        public async Task DisplayRequestTask_ConfApproveTask_ForwardsApprovalInNormalPath()
        {
            WfApproval approval = new() { Id = 7, StateId = 0 };
            WfReqTask task = new()
            {
                Id = 0,
                TaskType = WfTaskType.generic.ToString(),
                Approvals = [approval]
            };
            WfHandler handler = new()
            {
                ActReqTask = task,
                ActApproval = approval,
                ActTicket = new WfTicket { Tasks = [task] },
                ActStateMatrix = new StateMatrix { LowestEndState = 99 }
            };
            DisplayRequestTask component = new();
            SetPrivateField(component, nameof(DisplayRequestTask.WfHandler), handler);
            SetPrivateField(component, "userConfig", CreateUserConfig());

            WfApproval target = new() { Id = 7, StateId = 5 };
            await InvokePrivateTask(component, "ConfApproveTask", target);

            Assert.That(approval.StateId, Is.EqualTo(5));
        }

        [Test]
        public void DisplayRequestTask_StateHelpersAndMetadataCallbacks_UpdateState()
        {
            DisplayRequestTask component = new();
            WfHandler handler = new()
            {
                ActReqTask = new WfReqTask { TaskType = WfTaskType.access.ToString() },
                ActStateMatrix = new StateMatrix { PhaseActive = { [WorkflowPhases.approval] = true } }
            };
            SetPrivateField(component, nameof(DisplayRequestTask.WfHandler), handler);
            SetPrivateField(component, "userConfig", CreateUserConfig());
            SetMatrix(handler, WfTaskType.access.ToString(), new StateMatrix
            {
                PhaseActive = { [WorkflowPhases.approval] = true }
            });
            SetPrivateField(component, nameof(DisplayRequestTask.Phase), WorkflowPhases.approval);
            SetPrivateField(component, "availableTaskTypes", new List<WfTaskType> { WfTaskType.master, WfTaskType.generic, WfTaskType.access });

            InvokePrivate(component, "OnMetadataTaskTypeChanged", WfTaskType.generic);
            Management management = new() { Id = 4, Name = "management" };
            InvokePrivate(component, "OnMetadataManagementChanged", management);
            InvokePrivate(component, "SetTargetDatesValid", false);

            Assert.Multiple(() =>
            {
                Assert.That(InvokePrivate<WfTaskType>(component, "GetDefaultTaskType"), Is.EqualTo(WfTaskType.access));
                Assert.That(GetPrivateField<Management?>(component, "actManagement"), Is.EqualTo(management));
                Assert.That(InvokePrivate<bool>(component, "RejectInvalidTargetDates"), Is.False);
                Assert.That(InvokePrivate<bool>(component, "CanShowApprovalsButton"), Is.True);
                Assert.That(InvokePrivate<bool>(component, "IsRequestTaskReadOnly"), Is.True);
            });

            handler.ApproveReqTaskMode = true;
            Assert.That(InvokePrivate<bool>(component, "IsRequestTaskReadOnly"), Is.False);

            handler.DisplayPromoteReqTaskMode = true;
            handler.DisplayApproveMode = true;
            Assert.That(InvokePrivate<bool>(component, "Cancel"), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(handler.DisplayPromoteReqTaskMode, Is.False);
                Assert.That(handler.DisplayApproveMode, Is.False);
            });
        }

        [Test]
        public void DisplayRequestTask_ConfiguredActionVisibility_RespectsModesAndReadOnlyActions()
        {
            DisplayRequestTask component = new();
            WfHandler handler = new() { ActReqTask = new WfReqTask() };
            SetPrivateField(component, nameof(DisplayRequestTask.WfHandler), handler);

            WfStateAction normalAction = new() { ActionType = StateActionTypes.DoNothing.ToString() };
            WfStateAction readOnlyAction = new() { ActionType = StateActionTypes.DisplayConnection.ToString() };

            Assert.Multiple(() =>
            {
                Assert.That(InvokePrivate<bool>(component, "CanShowConfiguredActionButton", normalAction), Is.False);
                Assert.That(InvokePrivate<bool>(component, "CanShowConfiguredActionButton", readOnlyAction), Is.True);
            });

            handler.EditReqTaskMode = true;
            Assert.That(InvokePrivate<bool>(component, "CanShowConfiguredActionButton", normalAction), Is.True);
        }

        [Test]
        public async Task DisplayRequestTask_ReadOnlyPhaseView_OffersOnlyReadOnlyActionsAndBlocksWrites()
        {
            DisplayRequestTask component = new();
            WfReqTask task = new() { Title = "Unchanged" };
            WfHandler handler = new() { ActReqTask = task, ReadOnlyMode = true, PlanReqTaskMode = true, EditReqTaskMode = true };
            SetPrivateField(component, nameof(DisplayRequestTask.WfHandler), handler);

            WfStateAction normalAction = new() { ActionType = StateActionTypes.DoNothing.ToString() };
            WfStateAction readOnlyAction = new() { ActionType = StateActionTypes.DisplayConnection.ToString() };
            InvokePrivate(component, "InitAddComment");
            await InvokePrivateTask(component, "SaveReqTask");

            Assert.Multiple(() =>
            {
                Assert.That(InvokePrivate<bool>(component, "CanShowConfiguredActionButton", normalAction), Is.False);
                Assert.That(InvokePrivate<bool>(component, "CanShowConfiguredActionButton", readOnlyAction), Is.True);
                Assert.That(handler.DisplayReqTaskCommentMode, Is.False);
                Assert.That(task.Title, Is.EqualTo("Unchanged"));
                Assert.That(GetPrivateField<bool>(component, "WorkInProgress"), Is.False);
            });
        }

        [Test]
        public void DisplayRequestTask_ObjectReferencesAndServicePortsAreValidated()
        {
            Assert.Multiple(() =>
            {
                Assert.That(InvokePrivate<bool>(new DisplayRequestTask(), "HasObjectReference", new WfReqElement { NetworkId = 1 }), Is.True);
                Assert.That(InvokePrivate<bool>(new DisplayRequestTask(), "HasObjectReference", new WfReqElement { ServiceId = 1 }), Is.True);
                Assert.That(InvokePrivate<bool>(new DisplayRequestTask(), "HasObjectReference", new WfReqElement { FlowNetworkObjectId = 1 }), Is.True);
                Assert.That(InvokePrivate<bool>(new DisplayRequestTask(), "HasObjectReference", new WfReqElement { FlowNetworkGroupId = 1 }), Is.True);
                Assert.That(InvokePrivate<bool>(new DisplayRequestTask(), "HasObjectReference", new WfReqElement { FlowServiceObjectId = 1 }), Is.True);
                Assert.That(InvokePrivate<bool>(new DisplayRequestTask(), "HasObjectReference", new WfReqElement { FlowServiceGroupId = 1 }), Is.True);
                Assert.That(InvokePrivate<bool>(new DisplayRequestTask(), "HasObjectReference", new WfReqElement()), Is.False);
            });

            DisplayRequestTask component = new();
            Assert.Multiple(() =>
            {
                Assert.That(InvokePrivate<bool>(component, "IsInvalidServicePort", new WfReqElement { Field = ElemFieldType.source.ToString(), Port = 0, ProtoId = 6 }), Is.False);
                Assert.That(InvokePrivate<bool>(component, "IsInvalidServicePort", new WfReqElement { Field = ElemFieldType.service.ToString(), Port = 443, ProtoId = 6 }), Is.False);
                Assert.That(InvokePrivate<bool>(component, "IsInvalidServicePort", new WfReqElement { Field = ElemFieldType.service.ToString(), Port = 0, ProtoId = 6 }), Is.True);
                Assert.That(InvokePrivate<bool>(component, "IsInvalidServicePort", new WfReqElement { Field = ElemFieldType.service.ToString(), Port = 65536, ProtoId = 6 }), Is.True);
            });
        }

        [Test]
        public void DisplayRequestTask_OwnerAndTaskTypeChangesArePreparedForSave()
        {
            FwoOwner oldOwner = new() { Id = 1, Name = "old" };
            FwoOwner newOwner = new() { Id = 2, Name = "new" };
            WfReqTask task = new() { TaskType = WfTaskType.new_interface.ToString() };
            DisplayRequestTask component = new();
            SetPrivateField(component, nameof(DisplayRequestTask.WfHandler), new WfHandler { ActReqTask = task });
            SetPrivateField(component, "oldOwner", oldOwner);
            SetPrivateField(component, "actOwner", newOwner);
            SetPrivateField(component, "actTaskType", WfTaskType.new_interface);
            SetPrivateField(component, "actRequestingOwner", new FwoOwner { Id = 3, Name = "requesting" });

            InvokePrivate(component, "ApplyOwnerChanges");
            InvokePrivate(component, "ApplyTaskTypeSpecificValues");

            Assert.Multiple(() =>
            {
                Assert.That(task.RemovedOwners.Select(owner => owner.Id), Is.EqualTo(kRemovedOwnerIds));
                Assert.That(task.NewOwners.Select(owner => owner.Id), Is.EqualTo(kNewOwnerIds));
                Assert.That(task.GetAddInfoIntValue(AdditionalInfoKeys.ReqOwner), Is.EqualTo(3));
            });
        }

        [Test]
        public void GetBundledTaskCount_OnlyCountsTasksWithSameStateWhenDisplayGroupingIsEnabled()
        {
            WfReqTask activeTask = new() { Id = 1, StateId = 2 };
            activeTask.SetAddInfo(AdditionalInfoKeys.FlowBundleId, "bundle-1-2");
            WfReqTask sameStateTask = new() { Id = 2, StateId = 2 };
            sameStateTask.SetAddInfo(AdditionalInfoKeys.FlowBundleId, "bundle-1-2");
            WfReqTask differentStateTask = new() { Id = 3, StateId = 3 };
            differentStateTask.SetAddInfo(AdditionalInfoKeys.FlowBundleId, "bundle-1-2");
            SimulatedUserConfig userConfig = new()
            {
                ReqConsiderBundling = true,
                ReqDisplayBundledTasksAsOne = true
            };
            DisplayRequestTask component = new();
            SetPrivateField(component, "userConfig", userConfig);
            SetPrivateField(component, nameof(DisplayRequestTask.WfHandler), new WfHandler
            {
                ActReqTask = activeTask,
                ActTicket = new WfTicket { Tasks = [activeTask, sameStateTask, differentStateTask] }
            });
            SetPrivateField(component, nameof(DisplayRequestTask.Phase), WorkflowPhases.approval);

            Assert.That(InvokePrivate<int>(component, "GetBundledTaskCount"), Is.EqualTo(2));
        }

        [Test]
        public void GetBundledDisplayTask_MergesDistinctElementsWithoutChangingActiveTask()
        {
            WfReqTask activeTask = CreateBundledAccessTask(1, "10.0.0.1/32", "10.0.1.1/32");
            WfReqTask secondTask = CreateBundledAccessTask(2, "10.0.0.1/32", "10.0.1.2/32");
            SimulatedUserConfig userConfig = new()
            {
                ReqConsiderBundling = true,
                ReqDisplayBundledTasksAsOne = true
            };
            DisplayRequestTask component = new();
            SetPrivateField(component, "userConfig", userConfig);
            SetPrivateField(component, nameof(DisplayRequestTask.WfHandler), new WfHandler
            {
                ActReqTask = activeTask,
                ActTicket = new WfTicket { Tasks = [activeTask, secondTask] }
            });
            SetPrivateField(component, nameof(DisplayRequestTask.Phase), WorkflowPhases.approval);

            WfReqTask mergedTask = InvokePrivate<WfReqTask>(component, "GetBundledDisplayTask");

            Assert.Multiple(() =>
            {
                Assert.That(mergedTask.GetNwObjectElements(ElemFieldType.source), Has.Count.EqualTo(1));
                Assert.That(mergedTask.GetNwObjectElements(ElemFieldType.destination)
                    .Select(destination => destination.IpString), Is.EquivalentTo(["10.0.1.1/32", "10.0.1.2/32"]));
                Assert.That(mergedTask.GetServiceElements(), Has.Count.EqualTo(1));
                Assert.That(activeTask.Elements, Has.Count.EqualTo(3));
            });
        }

        private static WfReqTask CreateBundledAccessTask(long id, string source, string destination)
        {
            WfReqTask task = new()
            {
                Id = id,
                StateId = 2,
                Elements =
                [
                    new WfReqElement { Field = ElemFieldType.source.ToString(), IpString = source, Name = "source" },
                    new WfReqElement { Field = ElemFieldType.destination.ToString(), IpString = destination, Name = destination },
                    new WfReqElement { Field = ElemFieldType.service.ToString(), Port = 443, ProtoId = 6, Name = "https" }
                ]
            };
            task.SetAddInfo(AdditionalInfoKeys.FlowBundleId, "bundle-1-2");
            return task;
        }

        private static void SetPrivateField<T>(object component, string fieldName, T value)
        {
            PropertyInfo? property = component.GetType().GetProperty(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (property != null)
            {
                property.SetValue(component, value);
                return;
            }

            FieldInfo? field = component.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(component, value);
                return;
            }

            throw new MissingMemberException(component.GetType().FullName, fieldName);
        }

        private static T GetPrivateField<T>(object component, string fieldName)
        {
            PropertyInfo? property = component.GetType().GetProperty(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            object? value = property?.GetValue(component);
            if (property == null)
            {
                FieldInfo? field = component.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                value = field?.GetValue(component);
            }

            return (T)(value ?? throw new AssertionException($"Member '{fieldName}' was null or missing."));
        }

        private static void InvokePrivate(object component, string methodName, params object?[] parameters)
        {
            GetPrivateMethod(component, methodName).Invoke(component, parameters);
        }

        private static T InvokePrivate<T>(object component, string methodName, params object?[] parameters)
        {
            return (T)(GetPrivateMethod(component, methodName).Invoke(component, parameters)
                ?? throw new AssertionException($"Method '{methodName}' returned null."));
        }

        private static MethodInfo GetPrivateMethod(object component, string methodName)
        {
            return component.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                ?? throw new MissingMethodException(component.GetType().FullName, methodName);
        }

        private static async Task InvokePrivateTask(object component, string methodName, params object[] parameters)
        {
            await (Task)GetPrivateMethod(component, methodName).Invoke(component, parameters)!;
        }

        private static async Task<T> InvokePrivateTaskResult<T>(object component, string methodName, params object[] parameters)
        {
            return await (Task<T>)GetPrivateMethod(component, methodName).Invoke(component, parameters)!;
        }

        private static void SetMember(object instance, string memberName, object? value)
        {
            UiRequestCoverageTest.SetMember(instance, memberName, value);
        }

        private static T GetMember<T>(object instance, string memberName)
        {
            return UiRequestCoverageTest.GetMember<T>(instance, memberName);
        }

        private static UiRequestCoverageTest.RequestCoverageUserConfig CreateUserConfig(params string[] roles)
        {
            return UiRequestCoverageTest.CreateUserConfig(roles);
        }

        private static void SetMatrix(WfHandler handler, string taskType, StateMatrix matrix)
        {
            UiRequestCoverageTest.SetMatrix(handler, taskType, matrix);
        }

        [Test]
        public async Task DisplayRequestTask_RuleDeleteWithoutGatewayOrRules_IsRejected()
        {
            List<string> messages = [];
            DisplayRequestTask component = new();
            SimulatedUserConfig userConfig = new();
            SetPrivateField(component, "userConfig", userConfig);
            SetPrivateField(component, nameof(DisplayRequestTask.WfHandler), new WfHandler
            {
                ActReqTask = new WfReqTask { Title = "Delete rule", TaskType = WfTaskType.rule_delete.ToString() }
            });
            SetPrivateField(component, "actTaskType", WfTaskType.rule_delete);
            SetPrivateField(component, "DisplayMessageInUi", (Action<Exception?, string, string, bool>)((_, _, message, _) => messages.Add(message)));

            bool valid = await InvokePrivateTaskResult<bool>(component, "CheckTaskValues");

            Assert.Multiple(() =>
            {
                Assert.That(valid, Is.False);
                Assert.That(messages, Has.Count.EqualTo(1));
                Assert.That(messages[0], Is.EqualTo(userConfig.GetText("E5102")));
            });
        }

        [Test]
        public async Task DisplayRequestTask_PrepareReqTaskForSave_SetsSpecialTaskValues()
        {
            WfReqTask ruleDeleteTask = new() { Title = "Delete", TaskType = WfTaskType.rule_delete.ToString() };
            DisplayRequestTask ruleDeleteComponent = new();
            SetPrivateField(ruleDeleteComponent, "userConfig", new SimulatedUserConfig());
            SetPrivateField(ruleDeleteComponent, nameof(DisplayRequestTask.WfHandler), new WfHandler { ActReqTask = ruleDeleteTask });
            SetPrivateField(ruleDeleteComponent, "actTaskType", WfTaskType.rule_delete);

            bool ruleDeleteSaved = await InvokePrivateTaskResult<bool>(ruleDeleteComponent, "PrepareReqTaskForSave");

            WfReqTask groupTask = new() { Title = "Create group", TaskType = WfTaskType.group_create.ToString() };
            DisplayRequestTask groupComponent = new();
            SetPrivateField(groupComponent, "userConfig", new SimulatedUserConfig());
            SetPrivateField(groupComponent, nameof(DisplayRequestTask.WfHandler), new WfHandler { ActReqTask = groupTask });
            SetPrivateField(groupComponent, "actTaskType", WfTaskType.group_create);
            SetPrivateField(groupComponent, "actGrpName", "network-group");

            bool groupSaved = await InvokePrivateTaskResult<bool>(groupComponent, "PrepareReqTaskForSave");

            Assert.Multiple(() =>
            {
                Assert.That(ruleDeleteSaved, Is.False);
                Assert.That(ruleDeleteTask.RequestAction, Is.EqualTo(RequestAction.delete.ToString()));
                Assert.That(groupSaved, Is.False);
                Assert.That(groupTask.GetAddInfoValue(AdditionalInfoKeys.GrpName), Is.EqualTo("network-group"));
            });
        }

        [Test]
        public async Task DisplayRequestTask_SaveWithInvalidTargetDates_DoesNotContinue()
        {
            List<string> messages = [];
            DisplayRequestTask component = new();
            SimulatedUserConfig userConfig = new();
            SetPrivateField(component, "userConfig", userConfig);
            SetPrivateField(component, nameof(DisplayRequestTask.WfHandler), new WfHandler
            {
                ActReqTask = new WfReqTask { Title = "Invalid dates", TaskType = WfTaskType.generic.ToString() }
            });
            SetPrivateField(component, "targetDatesValid", false);
            SetPrivateField(component, "DisplayMessageInUi", (Action<Exception?, string, string, bool>)((_, _, message, _) => messages.Add(message)));

            await InvokePrivateTask(component, "SaveReqTask");

            Assert.Multiple(() =>
            {
                Assert.That(messages, Has.Count.EqualTo(1));
                Assert.That(messages[0], Is.EqualTo(userConfig.GetText("E5119")));
                Assert.That(GetPrivateField<bool>(component, "WorkInProgress"), Is.False);
            });
        }

        [Test]
        public async Task DisplayRequestTask_SaveWhileBusy_DoesNothing()
        {
            DisplayRequestTask component = new();
            WfReqTask task = new() { Title = "Unchanged" };
            SetPrivateField(component, nameof(DisplayRequestTask.WfHandler), new WfHandler { ActReqTask = task });
            SetPrivateField(component, "WorkInProgress", true);

            await InvokePrivateTask(component, "SaveReqTask");

            Assert.Multiple(() =>
            {
                Assert.That(task.Title, Is.EqualTo("Unchanged"));
                Assert.That(GetPrivateField<bool>(component, "WorkInProgress"), Is.True);
            });
        }

        [Test]
        public async Task DisplayRequestTask_Comments_InitializeAndOpenCommentPopup()
        {
            WfReqTask task = new()
            {
                Title = "Commented task",
                Comments = [new WfCommentDataHelper(new WfComment { CommentText = "task comment" })]
            };
            WfHandler handler = new() { ActReqTask = task };
            DisplayRequestTask component = new();
            SetPrivateField(component, "userConfig", new SimulatedUserConfig());
            SetPrivateField(component, nameof(DisplayRequestTask.WfHandler), handler);

            await InvokePrivateTask(component, "InitComments");
            InvokePrivate(component, "InitAddComment");

            Assert.Multiple(() =>
            {
                Assert.That(GetPrivateField<string>(component, "allComments"), Does.Contain("task comment"));
                Assert.That(handler.DisplayReqTaskCommentMode, Is.True);
            });
        }

        [Test]
        public async Task DisplayRequestTask_Approvals_ShowAndResetPopup()
        {
            await using BunitContext context = new();
            WfHandler handler = new()
            {
                ActReqTask = new WfReqTask { TaskType = WfTaskType.access.ToString() },
                DisplayApprovalReqMode = false
            };
            context.Services.AddSingleton<UserConfig>(new SimulatedUserConfig());
            context.Services.AddSingleton<ApiConnection>(new UiRequestCoverageTest.ThrowingApiConnection());
            context.Services.AddSingleton(new MiddlewareClient("http://localhost/"));
            IRenderedComponent<DisplayRequestTask> component = context.Render<DisplayRequestTask>(parameters => parameters
                .Add(parameter => parameter.WfHandler, handler)
            );

            InvokePrivate(component.Instance, "ShowApprovals");
            Assert.That(handler.DisplayApprovalReqMode, Is.True);

            await component.InvokeAsync(async () => await InvokePrivateTask(component.Instance, "ResetApprovalsPopup"));

            Assert.That(handler.DisplayApprovalReqMode, Is.False);
        }

        [Test]
        public void DisplayRequestTask_CanShowApprovalsButton_UsesPhaseMatrix()
        {
            WfHandler handler = new()
            {
                ActReqTask = new WfReqTask { TaskType = WfTaskType.access.ToString() }
            };
            StateMatrixDict matrices = new();
            matrices.Matrices[WfTaskType.access.ToString()] = new StateMatrix { PhaseActive = { [WorkflowPhases.approval] = true } };
            SetPrivateField(handler, "stateMatrixDict", matrices);
            DisplayRequestTask component = new();
            SetPrivateField(component, nameof(DisplayRequestTask.WfHandler), handler);

            Assert.That(InvokePrivate<bool>(component, "CanShowApprovalsButton"), Is.True);
        }

        [Test]
        public async Task DisplayRequestTask_PrepareReqTaskForSave_StoresEditedValues()
        {
            DisplayRequestTask component = new();
            UiRequestCoverageTest.RequestCoverageUserConfig userConfig = CreateUserConfig(Roles.Admin);
            WfReqTask reqTask = new()
            {
                Id = 7,
                Title = "Interface change",
                Reason = "Need access",
                TaskType = WfTaskType.access.ToString()
            };
            WfHandler handler = new()
            {
                ActReqTask = reqTask,
                AllOwners =
                [
                new FwoOwner { Id = 11, Name = "Allowed owner" },
                new FwoOwner { Id = 12, Name = "Other owner" }
                ]
            };

            SetMember(component, "userConfig", userConfig);
            SetMember(component, nameof(DisplayRequestTask.WfHandler), handler);
            SetMember(component, "actTaskType", WfTaskType.new_interface);
            SetMember(component, "actOwner", new FwoOwner { Id = 21, Name = "Current owner" });
            SetMember(component, "oldOwner", new FwoOwner { Id = 22, Name = "Previous owner" });
            SetMember(component, "actManagement", new Management { Id = 44, Name = "Mgmt" });
            SetMember(component, "managements", new List<Management> { new() { Id = 44, Name = "Mgmt" } });
            SetMember(component, "actRuleAction", new RuleAction { Id = 3, Name = "Allow" });
            SetMember(component, "actTracking", new Tracking { Id = 4, Name = "Track" });
            SetMember(component, "actRequestingOwner", new FwoOwner { Id = 11, Name = "Allowed owner" });
            SetMember(component, "selectedDevices", new List<Device>
            {
                new() { Id = 101, Name = "gw-101" },
                new() { Id = 102, Name = "gw-102" }
            });

            bool saved = await InvokePrivateTaskResult<bool>(component, "PrepareReqTaskForSave");

            Assert.Multiple(() =>
            {
                Assert.That(saved, Is.True);
                Assert.That(reqTask.TaskType, Is.EqualTo(WfTaskType.new_interface.ToString()));
                Assert.That(reqTask.RuleAction, Is.EqualTo(3));
                Assert.That(reqTask.Tracking, Is.EqualTo(4));
                Assert.That(reqTask.ManagementId, Is.EqualTo(44));
                Assert.That(reqTask.GetAddInfoIntValue(AdditionalInfoKeys.ReqOwner), Is.EqualTo(11));
                Assert.That(reqTask.GetDeviceList(), Is.EqualTo(kDeviceListIds));
                Assert.That(reqTask.Owners, Has.Count.EqualTo(1));
                Assert.That(reqTask.Owners[0].Owner.Id, Is.EqualTo(21));
                Assert.That(reqTask.RemovedOwners, Has.Count.EqualTo(1));
                Assert.That(reqTask.RemovedOwners[0].Id, Is.EqualTo(22));
            });
        }

        [Test]
        public async Task DisplayRequestTask_CheckTaskValues_RejectsMissingTitleAndInvalidTaskShapes()
        {
            List<string> messages = [];
            string expectedMessage = CreateUserConfig().GetText("E5102");

            DisplayRequestTask missingTitleComponent = new();
            SetMember(missingTitleComponent, "userConfig", CreateUserConfig());
            SetMember(missingTitleComponent, nameof(DisplayRequestTask.WfHandler), new WfHandler
            {
                ActReqTask = new WfReqTask { Title = "", TaskType = WfTaskType.access.ToString() }
            });
            SetMember(missingTitleComponent, "actTaskType", WfTaskType.access);
            SetMember(missingTitleComponent, "DisplayMessageInUi", (Action<Exception?, string, string, bool>)((_, _, message, _) => messages.Add(message)));

            bool missingTitleValid = await InvokePrivateTaskResult<bool>(missingTitleComponent, "CheckTaskValues");

            DisplayRequestTask accessComponent = new();
            SetMember(accessComponent, "userConfig", CreateUserConfig());
            SetMember(accessComponent, nameof(DisplayRequestTask.WfHandler), new WfHandler
            {
                ActReqTask = new WfReqTask { Title = "Valid", TaskType = WfTaskType.access.ToString() }
            });
            SetMember(accessComponent, "actTaskType", WfTaskType.access);
            SetMember(accessComponent, "DisplayMessageInUi", (Action<Exception?, string, string, bool>)((_, _, message, _) => messages.Add(message)));

            bool accessValid = await InvokePrivateTaskResult<bool>(accessComponent, "CheckTaskValues");

            DisplayRequestTask groupComponent = new();
            SetMember(groupComponent, "userConfig", CreateUserConfig());
            SetMember(groupComponent, nameof(DisplayRequestTask.WfHandler), new WfHandler
            {
                ActReqTask = new WfReqTask { Title = "Valid", TaskType = WfTaskType.group_create.ToString() }
            });
            SetMember(groupComponent, "actTaskType", WfTaskType.group_create);
            SetMember(groupComponent, "actGrpName", "");
            SetMember(groupComponent, "DisplayMessageInUi", (Action<Exception?, string, string, bool>)((_, _, message, _) => messages.Add(message)));

            bool groupValid = await InvokePrivateTaskResult<bool>(groupComponent, "CheckTaskValues");

            Assert.Multiple(() =>
            {
                Assert.That(missingTitleValid, Is.False);
                Assert.That(accessValid, Is.False);
                Assert.That(groupValid, Is.False);
                Assert.That(messages, Has.Count.EqualTo(3));
                Assert.That(messages, Is.All.EqualTo(expectedMessage));
            });
        }

        [Test]
        public async Task DisplayRequestTask_StartImplementationPhase_DelegatesTaskAndBlocksReentry()
        {
            TaskCompletionSource<object?> started = new(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<object?> release = new(TaskCreationOptions.RunContinuationsAsynchronously);
            WfReqTask reqTask = CreateAccessTask(1, "10.0.0.1", "10.0.0.2", 443);
            WfImplTask implTask = new() { Id = 31, Title = "Implementation task", ReqTaskId = reqTask.Id };
            reqTask.ImplementationTasks.Add(implTask);
            WfImplTask? observedImplTask = null;
            int callCount = 0;
            WfHandler handler = new()
            {
                ActReqTask = reqTask,
                ActTicket = new WfTicket
                {
                    Id = 10,
                    Title = "Ticket",
                    Tasks = [reqTask]
                }
            };
            using BunitContext context = new();
            IRenderedComponent<DisplayRequestTask> component = RenderDisplayRequestTask(
            context,
            handler,
            new WfStateDict(),
            async task =>
            {
                callCount++;
                observedImplTask = task;
                started.SetResult(null);
                await release.Task;
            },
            Roles.Requester);

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
        public async Task DisplayRequestTask_NewTask_AddsTaskTypeDropdownComparedToExistingTask()
        {
            WfHandler existingHandler = new()
            {
                DisplayReqTaskMode = true,
                EditReqTaskMode = true,
                AddReqTaskMode = false,
                ActReqTask = new WfReqTask
                {
                    Id = 12,
                    Title = "Task",
                    TaskType = WfTaskType.generic.ToString(),
                    StateId = 0,
                    FreeText = "text"
                }
            };
            existingHandler.ActTicket.Tasks.Add(existingHandler.ActReqTask);
            WfStateDict states = new() { Name = { [0] = "Draft" } };
            int existingDropdownCount;
            await using (BunitContext existingContext = new())
            {
                IRenderedComponent<DisplayRequestTask> existingComponent = RenderDisplayRequestTask(existingContext, existingHandler, states, Roles.Requester);
                existingDropdownCount = existingComponent.FindAll("input[id^='dropdown-input-']").Count;
            }

            WfHandler newHandler = new()
            {
                DisplayReqTaskMode = true,
                EditReqTaskMode = true,
                AddReqTaskMode = true,
                ActReqTask = new WfReqTask
                {
                    Id = 0,
                    Title = "Task",
                    TaskType = WfTaskType.generic.ToString(),
                    StateId = 0
                }
            };
            newHandler.ActTicket.Tasks.Add(newHandler.ActReqTask);
            int newDropdownCount;
            await using (BunitContext newContext = new())
            {
                IRenderedComponent<DisplayRequestTask> newComponent = RenderDisplayRequestTask(newContext, newHandler, states, Roles.Requester);
                newDropdownCount = newComponent.FindAll("input[id^='dropdown-input-']").Count;
            }

            Assert.That(newDropdownCount, Is.GreaterThan(existingDropdownCount));
        }

        [Test]
        public async Task DisplayRequestTask_NewTask_UsesOneTaskTypeForMetadataAndElements()
        {
            RequestWorkflowUserConfig userConfig = new()
            {
                ReqAvailableTaskTypes = "[1,2,3,4,5]"
            };
            WfReqTask existingTask = new()
            {
                Id = 12,
                Title = "Existing",
                TaskType = WfTaskType.access.ToString(),
                StateId = 0
            };
            WfHandler handler = new()
            {
                DisplayReqTaskMode = true,
                EditReqTaskMode = true,
                ActReqTask = existingTask,
                ActTicket = new WfTicket { Id = 100, Tasks = [existingTask] }
            };
            WfStateDict states = new() { Name = { [0] = "Draft" } };

            await using BunitContext context = new();
            context.Services.AddSingleton<UserConfig>(userConfig);
            IRenderedComponent<DisplayRequestTask> component = RenderDisplayRequestTask(context, handler, states, Roles.Requester);

            WfReqTask newTask = new() { Id = 13, Title = "New", TaskType = WfTaskType.access.ToString(), StateId = 0 };
            handler.AddReqTaskMode = true;
            handler.ActReqTask = newTask;
            handler.ActTicket.Tasks.Add(newTask);
            await component.InvokeAsync(() => component.Instance.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(DisplayRequestTask.Phase)] = WorkflowPhases.request,
                [nameof(DisplayRequestTask.States)] = states,
                [nameof(DisplayRequestTask.WfHandler)] = handler,
                [nameof(DisplayRequestTask.ResetParent)] = (Func<Task>)DefaultInit.DoNothing,
                [nameof(DisplayRequestTask.StartImplPhase)] = (Func<WfImplTask, Task>)DefaultInit.DoNothing
            })));

            RequestTaskMetadataEditor metadataEditor = component.FindComponent<RequestTaskMetadataEditor>().Instance;
            RequestTaskElementEditor elementEditor = component.FindComponent<RequestTaskElementEditor>().Instance;
            Assert.Multiple(() =>
            {
                Assert.That(metadataEditor.CurrentTaskType, Is.EqualTo(WfTaskType.group_create));
                Assert.That(elementEditor.TaskType, Is.EqualTo(WfTaskType.group_create));
            });
        }

        [Test]
        public async Task DisplayRequestTask_NewInterface_RequestingOwnerDropdownUsesOwnOwners()
        {
            RequestWorkflowUserConfig userConfig = new()
            {
                ReqAvailableTaskTypes = "[\"new_interface\"]"
            };
            userConfig.User.Ownerships = [1, 3];
            WfHandler handler = new()
            {
                DisplayReqTaskMode = true,
                EditReqTaskMode = true,
                AddReqTaskMode = true,
                AllOwners =
                [
                new FwoOwner { Id = 0, Name = "All" },
                new FwoOwner { Id = 1, Name = "Own A" },
                new FwoOwner { Id = 2, Name = "Foreign" },
                new FwoOwner { Id = 3, Name = "Own B" }
                ],
                ActReqTask = new WfReqTask
                {
                    Id = 0,
                    Title = "Task",
                    TaskType = WfTaskType.new_interface.ToString(),
                    StateId = 0
                }
            };
            handler.ActTicket.Tasks.Add(handler.ActReqTask);
            WfStateDict states = new() { Name = { [0] = "Draft" } };
            await using BunitContext context = new();
            context.Services.AddSingleton<UserConfig>(userConfig);

            IRenderedComponent<DisplayRequestTask> component = RenderDisplayRequestTask(context, handler, states, Roles.Requester);

            List<int> ownerIds = GetMember<IEnumerable<FwoOwner>>(component.Instance, "NewInterfaceOwnerOptions")
            .Select(owner => owner.Id)
            .ToList();
            List<int> requestingOwnerIds = GetMember<IEnumerable<FwoOwner>>(component.Instance, "RequestingOwnerOptions")
            .Select(owner => owner.Id)
            .ToList();
            Assert.Multiple(() =>
            {
                Assert.That(ownerIds, Is.EqualTo(new List<int> { 1, 2, 3 }));
                Assert.That(requestingOwnerIds, Is.EqualTo(new List<int> { 1, 3 }));
            });
        }

        [Test]
        public async Task DisplayRequestTask_NewInterface_AdminRequestingOwnerDropdownUsesAllOwners()
        {
            RequestWorkflowUserConfig userConfig = new()
            {
                ReqAvailableTaskTypes = "[\"new_interface\"]"
            };
            userConfig.User.Roles = [Roles.Admin];
            userConfig.User.Ownerships = [0];
            WfHandler handler = new()
            {
                DisplayReqTaskMode = true,
                EditReqTaskMode = true,
                AddReqTaskMode = true,
                AllOwners =
                [
                new FwoOwner { Id = 0, Name = "All" },
                new FwoOwner { Id = 1, Name = "App A" },
                new FwoOwner { Id = 2, Name = "App B" },
                new FwoOwner { Id = 3, Name = "App C" }
                ],
                ActReqTask = new WfReqTask
                {
                    Id = 0,
                    Title = "Task",
                    TaskType = WfTaskType.new_interface.ToString(),
                    StateId = 0
                }
            };
            handler.ActTicket.Tasks.Add(handler.ActReqTask);
            WfStateDict states = new() { Name = { [0] = "Draft" } };
            await using BunitContext context = new();
            context.Services.AddSingleton<UserConfig>(userConfig);

            IRenderedComponent<DisplayRequestTask> component = RenderDisplayRequestTask(context, handler, states, Roles.Admin);

            List<int> ownerIds = GetMember<IEnumerable<FwoOwner>>(component.Instance, "NewInterfaceOwnerOptions")
            .Select(owner => owner.Id)
            .ToList();
            List<int> requestingOwnerIds = GetMember<IEnumerable<FwoOwner>>(component.Instance, "RequestingOwnerOptions")
            .Select(owner => owner.Id)
            .ToList();
            Assert.Multiple(() =>
            {
                Assert.That(ownerIds, Is.EqualTo(new List<int> { 1, 2, 3 }));
                Assert.That(requestingOwnerIds, Is.EqualTo(new List<int> { 1, 2, 3 }));
            });
        }

        [Test]
        public async Task DisplayRequestTask_AccessTaskWithAllDevicesDisplaysAll()
        {
            WfReqTask task = CreateAccessTask(12, "10.0.0.1", "10.0.1.1", 80);
            task.SetDeviceList([WfReqTaskBase.kAllDevicesId]);
            WfHandler handler = new()
            {
                DisplayReqTaskMode = true,
                ReadOnlyMode = true,
                ActReqTask = task,
                ActTicket = new WfTicket { Id = 100, Tasks = [task] },
                Devices = [new Device { Id = 1, Name = "FW-1" }]
            };
            handler.ActStateMatrix.PhaseActive[WorkflowPhases.planning] = false;
            WfStateDict states = new() { Name = { [0] = "Draft" } };

            await using BunitContext context = new();
            context.Services.AddSingleton<UserConfig>(new RequestWorkflowUserConfig
            {
                ReqAutoCreateImplTasks = AutoCreateImplTaskOptions.enterInReqTask
            });
            IRenderedComponent<DisplayRequestTask> component = RenderDisplayRequestTask(context, handler, states, Roles.Requester);

            component.WaitForAssertion(() =>
            {
                RequestTaskMetadataEditor metadata = component.FindComponent<RequestTaskMetadataEditor>().Instance;
                Assert.That(metadata.CurrentSelectedDevices.Single().Id, Is.EqualTo(WfReqTaskBase.kAllDevicesId));
            });
        }

        [Test]
        public void DisplayRequestTask_FlowServiceReferenceSkipsManualPortValidation()
        {
            WfReqElement flowServiceElement = new()
            {
                Field = ElemFieldType.service.ToString(),
                FlowServiceObjectId = 201,
                Port = 0
            };
            WfHandler handler = new()
            {
                ActReqTask = new WfReqTask
                {
                    TaskType = WfTaskType.access.ToString(),
                    Elements = [flowServiceElement]
                },
                ActStateMatrix = new StateMatrix
                {
                    PhaseActive = { [WorkflowPhases.planning] = true }
                }
            };
            DisplayRequestTask component = new();
            SetMember(component, nameof(DisplayRequestTask.WfHandler), handler);
            SetMember(component, "userConfig", new RequestWorkflowUserConfig());
            SetRequestTaskElementMember(component, "actSources", new List<NwObjectElement> { new("10.0.0.1", 1) });
            SetRequestTaskElementMember(component, "actDestinations", new List<NwObjectElement> { new("10.0.1.1", 1) });
            SetRequestTaskElementMember(component, "actServices", new List<NwServiceElement> { new() { FlowServiceObjectId = 201, Name = "Flow Service" } });

        bool isValid = UiRequestCoverageTest.InvokePrivateBool(component, "RejectInvalidAccessTask");

            Assert.That(isValid, Is.True);
        }

        [Test]
        public void DisplayRequestTask_AcceptsPortlessProtocolWithoutPort()
        {
            WfReqElement serviceElement = new()
            {
                Field = ElemFieldType.service.ToString(),
                ProtoId = 50,
                Port = 0
            };
            DisplayRequestTask component = new();
            SetMember(component, nameof(DisplayRequestTask.WfHandler), new WfHandler
            {
                ActReqTask = new WfReqTask
                {
                    TaskType = WfTaskType.access.ToString(),
                    Elements = [serviceElement]
                },
                ActStateMatrix = new StateMatrix
                {
                    PhaseActive = { [WorkflowPhases.planning] = true }
                }
            });
            SetMember(component, "userConfig", new RequestWorkflowUserConfig());
            SetRequestTaskElementMember(component, "actSources", new List<NwObjectElement> { new("10.0.0.1", 1) });
            SetRequestTaskElementMember(component, "actDestinations", new List<NwObjectElement> { new("10.0.1.1", 1) });
            SetRequestTaskElementMember(component, "actServices", new List<NwServiceElement> { new() { ProtoId = 50, Port = 0 } });
            SetMember(component, "selectedDevices", new List<Device> { new() { Id = 1, Name = "FW-1" } });

        bool isValid = UiRequestCoverageTest.InvokePrivateBool(component, "RejectInvalidAccessTask");

            Assert.That(isValid, Is.True);
        }

        [Test]
        public void DisplayRequestTask_RejectsTcpWithoutPort()
        {
            WfReqElement serviceElement = new()
            {
                Field = ElemFieldType.service.ToString(),
                ProtoId = 6,
                Port = 0
            };
            DisplayRequestTask component = new();
            SetMember(component, nameof(DisplayRequestTask.WfHandler), new WfHandler
            {
                ActReqTask = new WfReqTask
                {
                    TaskType = WfTaskType.access.ToString(),
                    Elements = [serviceElement]
                },
                ActStateMatrix = new StateMatrix
                {
                    PhaseActive = { [WorkflowPhases.planning] = true }
                }
            });
            SetMember(component, "userConfig", new RequestWorkflowUserConfig());
            SetRequestTaskElementMember(component, "actSources", new List<NwObjectElement> { new("10.0.0.1", 1) });
            SetRequestTaskElementMember(component, "actDestinations", new List<NwObjectElement> { new("10.0.1.1", 1) });
            SetRequestTaskElementMember(component, "actServices", new List<NwServiceElement> { new() { ProtoId = 6, Port = 0 } });
            SetMember(component, "selectedDevices", new List<Device> { new() { Id = 1, Name = "FW-1" } });

        bool isValid = UiRequestCoverageTest.InvokePrivateBool(component, "RejectInvalidAccessTask");

            Assert.That(isValid, Is.False);
        }

        [Test]
        public void DisplayRequestTask_TargetBeginAfterTargetEndIsRejected()
        {
            List<string> messages = [];
            DisplayRequestTask component = new();
            SetMember(component, nameof(DisplayRequestTask.WfHandler), new WfHandler
            {
                ActReqTask = new WfReqTask
                {
                    TargetBeginDate = new DateTime(2026, 8, 31, 23, 59, 58),
                    TargetEndDate = new DateTime(2026, 8, 15, 12, 34, 56)
                }
            });
            SetMember(component, "userConfig", new RequestWorkflowUserConfig());
            SetMember(component, "DisplayMessageInUi", (Action<Exception?, string, string, bool>)((_, _, message, _) => messages.Add(message)));

        bool valid = UiRequestCoverageTest.InvokePrivateBool(component, "RejectInvalidTargetDates");

            Assert.Multiple(() =>
            {
                Assert.That(valid, Is.False);
                Assert.That(messages, Does.Contain("E5119"));
            });
        }

        [Test]
        public async Task DisplayRequestTask_ReinitializesElementsWhenActiveTaskChanges()
        {
            WfReqTask firstTask = CreateAccessTask(12, "10.0.0.1", "10.0.1.1", 80);
            WfReqTask secondTask = CreateAccessTask(13, "10.0.0.2", "10.0.1.2", 443);
            WfHandler handler = new()
            {
                DisplayReqTaskMode = true,
                ReadOnlyMode = true,
                ActReqTask = firstTask,
                ActTicket = new WfTicket { Id = 100, Tasks = [firstTask, secondTask] },
                Devices = []
            };
            SetMatrix(handler, WfTaskType.access.ToString(), new StateMatrix());
            WfStateDict states = new() { Name = { [0] = "Draft" } };

            await using BunitContext context = new();
            IRenderedComponent<DisplayRequestTask> component = RenderDisplayRequestTask(context, handler, states, Roles.Requester);

            RequestTaskElementEditor editor = component.FindComponent<RequestTaskElementEditor>().Instance;
            Assert.That(GetMember<List<NwObjectElement>>(editor, "actSources").Single().IpString, Is.EqualTo("10.0.0.1/32"));
            Assert.That(GetMember<List<NwObjectElement>>(editor, "actDestinations").Single().IpString, Is.EqualTo("10.0.1.1/32"));
            Assert.That(GetMember<List<NwServiceElement>>(editor, "actServices").Single().Port, Is.EqualTo(80));

            handler.ActReqTask = secondTask;
            await component.InvokeAsync(() => component.Instance.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(DisplayRequestTask.Phase)] = WorkflowPhases.request,
                [nameof(DisplayRequestTask.States)] = states,
                [nameof(DisplayRequestTask.WfHandler)] = handler,
                [nameof(DisplayRequestTask.ResetParent)] = (Func<Task>)DefaultInit.DoNothing,
                [nameof(DisplayRequestTask.StartImplPhase)] = (Func<WfImplTask, Task>)DefaultInit.DoNothing
            })));

            editor = component.FindComponent<RequestTaskElementEditor>().Instance;
            Assert.That(GetMember<List<NwObjectElement>>(editor, "actSources").Single().IpString, Is.EqualTo("10.0.0.2/32"));
            Assert.That(GetMember<List<NwObjectElement>>(editor, "actDestinations").Single().IpString, Is.EqualTo("10.0.1.2/32"));
            Assert.That(GetMember<List<NwServiceElement>>(editor, "actServices").Single().Port, Is.EqualTo(443));
        }

        [TestCase("10.1.1.5", true)]
        [TestCase("10.1.300.1", false)]
        public async Task DisplayRequestTask_ObjectCreate_AcceptsAValidObjectOnSave(string ip, bool expectedValid)
        {
            RequestWorkflowUserConfig userConfig = new() { ReqAvailableTaskTypes = "[8]" };
            RequestWorkflowApiConn apiConnection = new() { Managements = [new Management { Id = 3, Name = "mgmt-3" }] };
            WfHandler handler = new()
            {
                DisplayReqTaskMode = true,
                EditReqTaskMode = true,
                AddReqTaskMode = false,
                ActReqTask = new WfReqTask
                {
                    Id = 14,
                    Title = "Object task",
                    TaskType = WfTaskType.object_create.ToString(),
                    ManagementId = 3,
                    StateId = 0
                }
            };
            handler.ActReqTask.Elements.Add(new WfReqElement
            {
                Id = 141,
                TaskId = 14,
                Field = ElemFieldType.source.ToString(),
                RequestAction = RequestAction.create.ToString(),
                Cidr = new Cidr(ip)
            });
            handler.ActTicket.Tasks.Add(handler.ActReqTask);
            WfStateDict states = new() { Name = { [0] = "Draft" } };
            await using BunitContext context = new();
            context.Services.AddSingleton<UserConfig>(userConfig);
            context.Services.AddSingleton<ApiConnection>(apiConnection);

            IRenderedComponent<DisplayRequestTask> component = RenderDisplayRequestTask(context, handler, states, Roles.Requester);
            bool valid = await component.InvokeAsync(() => UiRequestCoverageTest.InvokePrivateTaskResult<bool>(component.Instance, "RejectInvalidObjectTask"));

            Assert.Multiple(() =>
            {
                Assert.That(component.FindComponents<DisplayObjectTaskElement>(), Has.Count.EqualTo(1));
                Assert.That(component.FindComponent<DisplayObjectTaskElement>().Instance.ManagementId, Is.EqualTo(3));
                Assert.That(valid, Is.EqualTo(expectedValid));
                Assert.That(handler.ActReqTask.Elements, Has.Count.EqualTo(1));
                Assert.That(handler.ActReqTask.Elements[0].Id, Is.EqualTo(141));
            });
        }

        private static void SetRequestTaskElementMember<T>(DisplayRequestTask component, string memberName, T value)
        {
            RequestTaskElementEditor? editor = GetMember<RequestTaskElementEditor?>(component, "elementEditor");
            if (editor == null)
            {
                editor = new RequestTaskElementEditor();
                SetMember(editor, nameof(RequestTaskElementEditor.WfHandler),
                    GetMember<WfHandler>(component, nameof(DisplayRequestTask.WfHandler)));
                SetMember(component, "elementEditor", editor);
            }
            SetMember(editor, memberName, value);
        }

    }
}
