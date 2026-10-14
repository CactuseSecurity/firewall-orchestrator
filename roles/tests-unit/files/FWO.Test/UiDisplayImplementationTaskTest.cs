using Bunit;
using FWO.Basics;
using FWO.Data;
using FWO.Data.Workflow;
using FWO.Services.Workflow;
using FWO.Ui.Pages.Request;
using FWO.Ui.Services;
using NUnit.Framework;
using System.Reflection;
using static FWO.Test.UiRequestCoverageTest;
using static FWO.Test.UiRequestWorkflowTest;

namespace FWO.Test
{
    [TestFixture]
    internal class UiDisplayImplementationTaskTest
    {
        [Test]
        public void DisplayObjectAndServiceElements_UseFlowIdsWhenNoNamesAreAvailable()
        {
            DisplayImplementationTask component = new();
            SetMember(component, "userConfig", CreateUserConfig());

            string objectDisplay = (string)GetPrivateMethod(typeof(DisplayImplementationTask), "DisplayObjectElement").Invoke(component, [new NwObjectElement { FlowNetworkObjectId = 41 }])!;
            string serviceDisplay = (string)GetPrivateMethod(typeof(DisplayImplementationTask), "DisplayServiceElement").Invoke(component, [new NwServiceElement { FlowServiceGroupId = 42 }])!;

            Assert.Multiple(() =>
            {
                Assert.That(objectDisplay, Is.EqualTo("41"));
                Assert.That(serviceDisplay, Is.EqualTo("42"));
            });
        }

        [Test]
        public void DisplayDevice_ReturnsAllWhenRequestUsesAllDevices()
        {
            DisplayImplementationTask component = new();
            SimulatedUserConfig userConfig = CreateUserConfig();
            WfReqTask reqTask = new() { Id = 20, TaskType = WfTaskType.access.ToString() };
            reqTask.SetDeviceList([WfReqTaskBase.kAllDevicesId]);
            SetMember(component, "userConfig", userConfig);
            SetMember(component, nameof(DisplayImplementationTask.WfHandler), new WfHandler
            {
                ActReqTask = reqTask,
                ActImplTask = new WfImplTask { ReqTaskId = 20, TaskType = WfTaskType.access.ToString(), DeviceId = null }
            });

            string displayDevice = (string)GetPrivateMethod(typeof(DisplayImplementationTask), "DisplayDevice").Invoke(component, [])!;

            Assert.That(displayDevice, Is.EqualTo(userConfig.GetText("all")));
        }

        [Test]
        public void RequestAssignOwner_SetsConfirmationState()
        {
            DisplayImplementationTask component = new();
            SetMember(component, "userConfig", CreateUserConfig());

            GetPrivateMethod(typeof(DisplayImplementationTask), "RequestAssignOwner").Invoke(component, []);

            Assert.Multiple(() =>
            {
                Assert.That(GetMember<bool>(component, "assignOwnerMode"), Is.True);
                Assert.That(GetMember<string>(component, "message"), Is.EqualTo("U8004"));
            });
        }

        [Test]
        public void OnParametersSetAsync_InitializesCachedFields()
        {
            DisplayImplementationTask component = new();
            SimulatedUserConfig userConfig = CreateUserConfig(Roles.Implementer);
            WfReqTask reqTask = new() { Id = 20, TaskType = WfTaskType.access.ToString() };
            reqTask.Owners = [new FwoOwnerDataHelper { Owner = new FwoOwner { Id = 55, Name = "Owner A" } }];
            reqTask.SetAddInfo(AdditionalInfoKeys.GrpName, "group-a");
            WfImplTask implTask = new()
            {
                Id = 99, TicketId = 10, ReqTaskId = 20, TaskType = WfTaskType.access.ToString(), DeviceId = 66,
                RuleAction = 3, Tracking = 4, Comments = [new WfCommentDataHelper(new WfComment { CommentText = "first comment" })]
            };
            WfTicket ticket = new() { Id = 10, Tasks = [reqTask] };
            WfHandler handler = new()
            {
                DisplayImplTaskMode = true, DisplayImplTaskCommentMode = true, ActReqTask = reqTask,
                ActImplTask = implTask, ActTicket = ticket, TicketList = [ticket], Devices = [new Device { Id = 66, Name = "gw-66" }]
            };
            SetMatrix(handler, WfTaskType.access.ToString(), CreateMatrix());
            SetMember(component, "userConfig", userConfig);
            SetMember(component, nameof(DisplayImplementationTask.WfHandler), handler);
            SetMember(component, nameof(DisplayImplementationTask.StateMatrix), CreateMatrix());
            SetMember(component, nameof(DisplayImplementationTask.States), new WfStateDict { Name = { [1] = "Open" } });
            SetMember(component, nameof(DisplayImplementationTask.Phase), WorkflowPhases.implementation);
            SetMember(component, nameof(DisplayImplementationTask.IncludePopups), true);
            SetMember(component, "firstParamSet", true);
            SetMember(component, "ruleActions", new List<RuleAction> { new() { Id = 3, Name = "Allow" } });
            SetMember(component, "trackings", new List<Tracking> { new() { Id = 4, Name = "Track" } });

            InvokePrivateTask(typeof(DisplayImplementationTask), component, "OnParametersSetAsync").GetAwaiter().GetResult();

            Assert.Multiple(() =>
            {
                Assert.That(GetMember<Device?>(component, "actDevice")?.Id, Is.EqualTo(66));
                Assert.That(GetMember<RuleAction?>(component, "actRuleAction")?.Id, Is.EqualTo(3));
                Assert.That(GetMember<Tracking?>(component, "actTracking")?.Id, Is.EqualTo(4));
                Assert.That(GetMember<FwoOwner?>(component, "actOwner")?.Id, Is.EqualTo(55));
                Assert.That(GetMember<FwoOwner?>(component, "oldOwner")?.Id, Is.EqualTo(55));
                Assert.That(GetMember<bool>(component, "newOwnerAssigned"), Is.False);
                Assert.That(GetMember<string?>(component, "actGrpName"), Is.EqualTo("group-a"));
                Assert.That(GetMember<string>(component, "allComments"), Does.Contain("first comment"));
                Assert.That(handler.DisplayImplTaskCommentMode, Is.False);
            });
        }

        [Test]
        public void InitPromoteAndCancelPromote_TogglePopupMode()
        {
            DisplayImplementationTask component = new();
            WfHandler handler = new() { DisplayPromoteImplTaskMode = false };
            SetMember(component, nameof(DisplayImplementationTask.WfHandler), handler);
            SetMember(component, "userConfig", CreateUserConfig());

            GetPrivateMethod(typeof(DisplayImplementationTask), "InitPromoteImplTask").Invoke(component, []);
            bool cancelResult = (bool)GetPrivateMethod(typeof(DisplayImplementationTask), "CancelPromote").Invoke(component, [])!;

            Assert.Multiple(() =>
            {
                Assert.That(handler.DisplayPromoteImplTaskMode, Is.False);
                Assert.That(cancelResult, Is.True);
            });
        }

        [Test]
        public void CheckImplTaskValues_RejectsInvalidServicePorts()
        {
            List<string> messages = [];
            DisplayImplementationTask component = new();
            SetMember(component, "userConfig", CreateUserConfig());
            SetMember(component, nameof(DisplayImplementationTask.WfHandler), new WfHandler
            {
                ActImplTask = new WfImplTask
                {
                    Id = 99, TaskType = WfTaskType.access.ToString(),
                    ImplElements = [new WfImplElement { Id = 1, ImplTaskId = 99, Field = ElemFieldType.service.ToString(), Port = 0, ProtoId = 6, ServiceId = null }]
                }
            });
            SetMember(component, "DisplayMessageInUi", (Action<Exception?, string, string, bool>)((_, _, message, _) => messages.Add(message)));

            bool valid = InvokePrivateBool(component, "CheckImplTaskValues");

            Assert.Multiple(() =>
            {
                Assert.That(valid, Is.False);
                Assert.That(messages, Does.Contain("Invalid port"));
            });
        }

        [Test]
        public void SetChangedOwner_AddsOldAndNewOwners()
        {
            DisplayImplementationTask component = new();
            WfHandler handler = new() { ActReqTask = new WfReqTask() };
            SetMember(component, nameof(DisplayImplementationTask.WfHandler), handler);
            SetMember(component, "actOwner", new FwoOwner { Id = 2, Name = "New" });
            SetMember(component, "oldOwner", new FwoOwner { Id = 1, Name = "Old" });

            GetPrivateMethod(typeof(DisplayImplementationTask), "SetChangedOwner").Invoke(component, []);

            Assert.Multiple(() =>
            {
                Assert.That(handler.ActReqTask.RemovedOwners, Has.Count.EqualTo(1));
                Assert.That(handler.ActReqTask.RemovedOwners[0].Id, Is.EqualTo(1));
                Assert.That(handler.ActReqTask.NewOwners, Has.Count.EqualTo(1));
                Assert.That(handler.ActReqTask.NewOwners[0].Id, Is.EqualTo(2));
            });
        }

        [Test]
        public void InitAddCommentAndReadonlyActionChecks_UseExpectedFlags()
        {
            DisplayImplementationTask component = new();
            WfHandler handler = new() { DisplayImplTaskCommentMode = false };
            SetMember(component, nameof(DisplayImplementationTask.WfHandler), handler);
            SetMember(component, "userConfig", CreateUserConfig());

            GetPrivateMethod(typeof(DisplayImplementationTask), "InitAddComment").Invoke(component, []);
            bool readOnlyAction = InvokePrivateBool(component, "CanShowConfiguredActionButton", new WfStateAction { ActionType = StateActionTypes.DisplayConnection.ToString() });
            bool hiddenAction = InvokePrivateBool(component, "CanShowConfiguredActionButton", new WfStateAction { ActionType = StateActionTypes.DoNothing.ToString() });

            Assert.Multiple(() =>
            {
                Assert.That(handler.DisplayImplTaskCommentMode, Is.True);
                Assert.That(readOnlyAction, Is.True);
                Assert.That(hiddenAction, Is.False);
            });
        }

        [Test]
        public void UpdateElements_ReconcilesListsAndRules()
        {
            DisplayImplementationTask component = new();
            WfImplElement sourceElem = new() { Id = 1, ImplTaskId = 42, Field = ElemFieldType.source.ToString(), Name = "old source" };
            WfImplElement destinationElem = new() { Id = 2, ImplTaskId = 42, Field = ElemFieldType.destination.ToString(), Name = "old destination" };
            WfImplElement serviceElem = new() { Id = 3, ImplTaskId = 42, Field = ElemFieldType.service.ToString(), Port = 443, ProtoId = 6, Name = "old service" };
            WfImplElement ruleElem = new() { Id = 4, ImplTaskId = 42, Field = ElemFieldType.rule.ToString(), RuleUid = "rule-old", Name = "old rule" };
            WfHandler handler = new() { ActImplTask = new WfImplTask { Id = 42, ImplElements = [sourceElem, destinationElem, serviceElem, ruleElem] } };
            SetMember(component, nameof(DisplayImplementationTask.WfHandler), handler);
            SetMember(component, "actSources", new List<NwObjectElement> { new() { ElemId = 5, TaskId = 42, Name = "new source" } });
            SetMember(component, "actDestinations", new List<NwObjectElement> { new() { ElemId = 6, TaskId = 42, Name = "new destination" } });
            SetMember(component, "actServices", new List<NwServiceElement> { new() { ElemId = 7, TaskId = 42, Port = 80, ProtoId = 6, Name = "new service" } });
            SetMember(component, "actRules", new List<NwRuleElement> { new() { ElemId = 8, TaskId = 42, RuleUid = "rule-new", Name = "new rule" } });
            SetMember(component, "sourcesToDelete", new List<NwObjectElement> { new() { ElemId = 1, TaskId = 42, Name = "old source" } });
            SetMember(component, "destinationsToDelete", new List<NwObjectElement> { new() { ElemId = 2, TaskId = 42, Name = "old destination" } });
            SetMember(component, "servicesToDelete", new List<NwServiceElement> { new() { ElemId = 3, TaskId = 42, Port = 443, ProtoId = 6, Name = "old service" } });
            SetMember(component, "sourcesToAdd", new List<NwObjectElement> { new() { ElemId = 9, TaskId = 42, Name = "added source" } });
            SetMember(component, "destinationsToAdd", new List<NwObjectElement> { new() { ElemId = 10, TaskId = 42, Name = "added destination" } });
            SetMember(component, "servicesToAdd", new List<NwServiceElement> { new() { ElemId = 11, TaskId = 42, Port = 22, ProtoId = 6, Name = "added service" } });

            GetPrivateMethod(typeof(DisplayImplementationTask), "UpdateElements").Invoke(component, []);

            Assert.Multiple(() =>
            {
                Assert.That(handler.ActImplTask.ImplElements.Any(element => element.Id is 1 or 2 or 3 or 4), Is.False);
                Assert.That(handler.ActImplTask.ImplElements.Any(element => element.Id == 8 && element.Field == ElemFieldType.rule.ToString()), Is.True);
                Assert.That(handler.ActImplTask.ImplElements.Any(element => element.Id == 9 && element.Field == ElemFieldType.source.ToString()), Is.True);
                Assert.That(handler.ActImplTask.ImplElements.Any(element => element.Id == 10 && element.Field == ElemFieldType.destination.ToString()), Is.True);
                Assert.That(handler.ActImplTask.ImplElements.Any(element => element.Id == 11 && element.Field == ElemFieldType.service.ToString()), Is.True);
                Assert.That(GetMember<List<NwObjectElement>>(component, "sourcesToAdd"), Is.Empty);
                Assert.That(GetMember<List<NwObjectElement>>(component, "sourcesToDelete"), Is.Empty);
                Assert.That(GetMember<List<NwObjectElement>>(component, "destinationsToAdd"), Is.Empty);
                Assert.That(GetMember<List<NwObjectElement>>(component, "destinationsToDelete"), Is.Empty);
                Assert.That(GetMember<List<NwServiceElement>>(component, "servicesToAdd"), Is.Empty);
                Assert.That(GetMember<List<NwServiceElement>>(component, "servicesToDelete"), Is.Empty);
            });
        }

        [Test]
        public async Task DisplayImplementationTask_PerformAction_ExecutesNormalPath()
        {
            WfReqTask reqTask = new() { Id = 20, TaskType = WfTaskType.generic.ToString() };
            WfImplTask implTask = new()
            {
                Id = 30,
                TaskNumber = 1,
                ReqTaskId = reqTask.Id,
                TaskType = WfTaskType.generic.ToString(),
                Title = "Implementation task"
            };
            reqTask.ImplementationTasks = [implTask];
            WfHandler handler = new()
            {
                DisplayImplTaskMode = true,
                ActReqTask = reqTask,
                ActImplTask = implTask,
                ActTicket = new WfTicket { Tasks = [reqTask] }
            };

            await using BunitContext context = new();
            IRenderedComponent<DisplayImplementationTask> component = RenderDisplayImplementationTask(context, handler,
                new WfStateDict(), Roles.Implementer);

            await component.InvokeAsync(() => InvokePrivateTask(typeof(DisplayImplementationTask), component.Instance,
                "PerformAction", new WfStateAction()));

            Assert.That(GetMember<bool>(component.Instance, "WorkInProgress"), Is.False);
        }

        [Test]
        public async Task DisplayImplementationTask_SaveImplTask_ChangesExistingTaskThroughNormalPath()
        {
            WfReqTask reqTask = new() { Id = 20, TaskType = WfTaskType.generic.ToString() };
            WfImplTask implTask = new()
            {
                Id = 30,
                TaskNumber = 1,
                ReqTaskId = reqTask.Id,
                TaskType = WfTaskType.generic.ToString(),
                Title = "Implementation task",
                FreeText = "instructions"
            };
            reqTask.ImplementationTasks = [implTask];
            WfHandler handler = new()
            {
                DisplayImplTaskMode = true,
                EditImplTaskMode = true,
                ActReqTask = reqTask,
                ActImplTask = implTask,
                ActTicket = new WfTicket { Tasks = [reqTask] }
            };

            await using BunitContext context = new();
            IRenderedComponent<DisplayImplementationTask> component = RenderDisplayImplementationTask(context, handler,
                new WfStateDict(), Roles.Implementer);

            await component.InvokeAsync(() => InvokePrivateTask(typeof(DisplayImplementationTask), component.Instance,
                "SaveImplTask"));

            Assert.Multiple(() =>
            {
                Assert.That(GetMember<bool>(component.Instance, "WorkInProgress"), Is.False);
                Assert.That(reqTask.ImplementationTasks, Has.Count.EqualTo(1));
                Assert.That(reqTask.ImplementationTasks[0], Is.SameAs(implTask));
            });
        }

        [Test]
        public async Task DisplayImplementationTask_ConfAddComment_UpdatesCommentsThroughNormalPath()
        {
            WfImplTask implTask = new()
            {
                Id = 30,
                TaskType = WfTaskType.generic.ToString(),
                Title = "Implementation task"
            };
            WfHandler handler = new()
            {
                DisplayImplTaskMode = true,
                ActReqTask = new WfReqTask(),
                ActImplTask = implTask
            };

            await using BunitContext context = new();
            IRenderedComponent<DisplayImplementationTask> component = RenderDisplayImplementationTask(context, handler,
                new WfStateDict(), Roles.Implementer);

            await component.InvokeAsync(() => InvokePrivateTask(typeof(DisplayImplementationTask), component.Instance,
                "ConfAddComment", "Implementation comment"));

            Assert.Multiple(() =>
            {
                Assert.That(implTask.Comments.Any(comment => comment.Comment.CommentText == "Implementation comment"), Is.True);
                Assert.That(handler.DisplayImplTaskCommentMode, Is.False);
            });
        }

        private static SimulatedUserConfig CreateUserConfig(params string[] roles)
        {
            SimulatedUserConfig userConfig = new();
            userConfig.User.Roles = [.. roles];
            userConfig.User.DbId = 77;
            userConfig.User.Dn = "cn=current";
            return userConfig;
        }

        private static void SetMember(object instance, string memberName, object? value)
        {
            Type? type = instance.GetType();
            while (type != null)
            {
                PropertyInfo? property = type.GetProperty(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                if (property != null) { property.SetValue(instance, value); return; }
                FieldInfo? field = type.GetField(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                if (field != null) { field.SetValue(instance, value); return; }
                type = type.BaseType;
            }
            throw new MissingMemberException(instance.GetType().FullName, memberName);
        }

        private static T GetMember<T>(object instance, string memberName)
        {
            Type? type = instance.GetType();
            while (type != null)
            {
                PropertyInfo? property = type.GetProperty(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                if (property != null) return (T)property.GetValue(instance)!;
                FieldInfo? field = type.GetField(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                if (field != null) return (T)field.GetValue(instance)!;
                type = type.BaseType;
            }
            throw new MissingMemberException(instance.GetType().FullName, memberName);
        }

        private static MethodInfo GetPrivateMethod(Type type, string methodName) => type.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance) ?? throw new MissingMethodException(type.FullName, methodName);

        private static bool InvokePrivateBool(object instance, string methodName, params object[] args) => (bool)GetPrivateMethod(instance.GetType(), methodName).Invoke(instance, args)!;

        private static async Task InvokePrivateTask(Type type, object instance, string methodName, params object[] args) => await (Task)GetPrivateMethod(type, methodName).Invoke(instance, args)!;

        private static StateMatrix CreateMatrix(int lowestInputState = 1, int lowestStartedState = 2, int lowestEndState = 10) => new()
        {
            LowestInputState = lowestInputState,
            LowestStartedState = lowestStartedState,
            LowestEndState = lowestEndState,
            PhaseActive =
            {
                [WorkflowPhases.request] = true,
                [WorkflowPhases.approval] = true,
                [WorkflowPhases.planning] = true,
                [WorkflowPhases.implementation] = true,
                [WorkflowPhases.review] = true
            }
        };

        private static void SetMatrix(WfHandler handler, string taskType, StateMatrix matrix)
        {
            FieldInfo field = typeof(WfHandler).GetField("stateMatrixDict", BindingFlags.NonPublic | BindingFlags.Instance)!;
            ((StateMatrixDict)field.GetValue(handler)!).Matrices[taskType] = matrix;
        }
        [Test]
        public async Task DisplayImplementationTask_AccessTaskWithAllDevicesDisplaysAll()
        {
            WfReqTask reqTask = CreateAccessTask(12, "10.0.0.1", "10.0.1.1", 80);
            reqTask.SetDeviceList([WfReqTaskBase.kAllDevicesId]);
            WfImplTask implTask = new(reqTask)
            {
                Id = 22,
                DeviceId = null,
                Title = "Implement all",
                TaskType = WfTaskType.access.ToString()
            };
            WfHandler handler = new()
            {
                DisplayImplTaskMode = true,
                EditImplTaskMode = false,
                ActReqTask = reqTask,
                ActImplTask = implTask,
                Devices = [new Device { Id = 1, Name = "FW-1" }]
            };
            WfStateDict states = new() { Name = { [0] = "Draft" } };

            await using BunitContext context = new();
            IRenderedComponent<DisplayImplementationTask> component = RenderDisplayImplementationTask(context, handler, states, Roles.Implementer);

            Assert.That(component.Markup, Does.Contain("all").IgnoreCase);
        }

        [Test]
        public void DisplayImplementationTask_AcceptsPortlessProtocolWithoutPort()
        {
            WfImplElement serviceElement = new()
            {
                Field = ElemFieldType.service.ToString(),
                ProtoId = 50,
                Port = 0
            };
            DisplayImplementationTask component = new();
            SetMember(component, nameof(DisplayImplementationTask.WfHandler), new WfHandler
            {
                ActImplTask = new WfImplTask
                {
                    TaskType = WfTaskType.access.ToString(),
                    ImplElements = [serviceElement]
                }
            });
            SetMember(component, "userConfig", new RequestWorkflowUserConfig());

            bool isValid = InvokePrivateBool(component, "CheckImplTaskValues");

            Assert.That(isValid, Is.True);
        }

        [Test]
        public void DisplayImplementationTask_RejectsTcpWithoutPort()
        {
            WfImplElement serviceElement = new()
            {
                Field = ElemFieldType.service.ToString(),
                ProtoId = 6,
                Port = 0
            };
            DisplayImplementationTask component = new();
            SetMember(component, nameof(DisplayImplementationTask.WfHandler), new WfHandler
            {
                ActImplTask = new WfImplTask
                {
                    TaskType = WfTaskType.access.ToString(),
                    ImplElements = [serviceElement]
                }
            });
            SetMember(component, "userConfig", new RequestWorkflowUserConfig());

            bool isValid = InvokePrivateBool(component, "CheckImplTaskValues");

            Assert.That(isValid, Is.False);
        }

        [Test]
        public async Task DisplayImplementationTask_ReadOnlyResolvedFlowSnapshotDisplaysNames()
        {
            WfReqTask reqTask = CreateAccessTask(12, "10.0.0.1", "10.0.1.1", 80);
            WfImplTask implTask = new()
            {
                Id = 22,
                Title = "Implement flow objects",
                TaskType = WfTaskType.access.ToString(),
                StateId = 1,
                ImplElements =
                [
                new WfImplElement { Id = 1, ImplTaskId = 22, Field = ElemFieldType.source.ToString(), IpString = "10.0.0.1/32", Name = "Flow Source" },
                new WfImplElement { Id = 2, ImplTaskId = 22, Field = ElemFieldType.destination.ToString(), IpString = "10.0.1.1/32", Name = "Flow Destination" },
                new WfImplElement { Id = 3, ImplTaskId = 22, Field = ElemFieldType.service.ToString(), Port = 443, ProtoId = 6, Name = "Flow Service" }
                ]
            };
            WfHandler handler = new()
            {
                DisplayImplTaskMode = true,
                EditImplTaskMode = false,
                ActReqTask = reqTask,
                ActImplTask = implTask,
                Devices = []
            };
            WfStateDict states = new() { Name = { [1] = "Open" } };

            await using BunitContext context = new();
            IRenderedComponent<DisplayImplementationTask> component = RenderDisplayImplementationTask(context, handler, states, Roles.Implementer);

            Assert.Multiple(() =>
            {
                Assert.That(component.Markup, Does.Contain("Flow Source"));
                Assert.That(component.Markup, Does.Contain("Flow Destination"));
                Assert.That(component.Markup, Does.Contain("Flow Service"));
            });
        }

        [Test]
        public async Task DisplayImplementationTask_GenericTaskDisplaysFreeText()
        {
            WfImplTask implTask = new()
            {
                Id = 23,
                Title = "Generic impl",
                TaskType = WfTaskType.generic.ToString(),
                FreeText = "Implementation instructions",
                StateId = 1
            };
            WfHandler handler = new()
            {
                DisplayImplTaskMode = true,
                EditImplTaskMode = false,
                ActReqTask = new WfReqTask(),
                ActImplTask = implTask,
                Devices = []
            };
            WfStateDict states = new() { Name = { [1] = "Open" } };

            await using BunitContext context = new();
            IRenderedComponent<DisplayImplementationTask> component = RenderDisplayImplementationTask(context, handler, states, Roles.Implementer);

            Assert.That(component.Markup, Does.Contain("Implementation instructions"));
        }

        [Test]
        public async Task DisplayImplementationTask_GroupCreateReadOnlyDisplaysGroupElements()
        {
            WfImplTask implTask = new()
            {
                Id = 21,
                Title = "Create groups",
                TaskType = WfTaskType.group_create.ToString(),
                StateId = 1,
                ImplElements =
                [
                new WfImplElement
                {
                    Id = 1,
                    ImplTaskId = 21,
                    Field = ElemFieldType.source.ToString(),
                    NetworkId = 42,
                    GroupName = "AR-ImplGroup",
                    Name = "HiddenObjectName"
                },
                new WfImplElement
                {
                    Id = 2,
                    ImplTaskId = 21,
                    Field = ElemFieldType.service.ToString(),
                    ServiceId = 44,
                    GroupName = "SG-ImplGroup",
                    Name = "HiddenServiceName"
                }
                ]
            };
            WfHandler handler = new()
            {
                DisplayImplTaskMode = true,
                EditImplTaskMode = false,
                ActImplTask = implTask,
                ActReqTask = new WfReqTask(),
                Devices = []
            };
            WfStateDict states = new() { Name = { [1] = "Open" } };

            await using BunitContext context = new();
            IRenderedComponent<DisplayImplementationTask> component = RenderDisplayImplementationTask(context, handler, states, Roles.Implementer);

            Assert.Multiple(() =>
            {
                Assert.That(component.Markup, Does.Contain("AR-ImplGroup"));
                Assert.That(component.Markup, Does.Contain("SG-ImplGroup"));
                Assert.That(component.Markup, Does.Not.Contain("HiddenObjectName"));
                Assert.That(component.Markup, Does.Not.Contain("HiddenServiceName"));
            });
        }

        [Test]
        public void DisplayImplementationTask_TargetBeginAfterTargetEndIsRejected()
        {
            List<string> messages = [];
            DisplayImplementationTask component = new();
            SetMember(component, nameof(DisplayImplementationTask.WfHandler), new WfHandler
            {
                ActImplTask = new WfImplTask
                {
                    TargetBeginDate = new DateTime(2026, 8, 31, 23, 59, 58),
                    TargetEndDate = new DateTime(2026, 8, 15, 12, 34, 56)
                }
            });
            SetMember(component, "userConfig", new RequestWorkflowUserConfig());
            SetMember(component, "DisplayMessageInUi", (Action<Exception?, string, string, bool>)((_, _, message, _) => messages.Add(message)));

            bool valid = InvokePrivateBool(component, "RejectInvalidTargetDates");

            Assert.Multiple(() =>
            {
                Assert.That(valid, Is.False);
                Assert.That(messages, Does.Contain("E5119"));
            });
        }

    }
}
