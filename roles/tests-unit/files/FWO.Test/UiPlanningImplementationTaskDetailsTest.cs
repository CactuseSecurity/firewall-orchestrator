using Bunit;
using AngleSharp.Dom;
using FWO.Api.Client;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Flow;
using FWO.Data.DeviceSpecificSettings;
using FWO.Data.RulePositioning;
using FWO.Data.RulePositioning.CheckPoint;
using FWO.Data.Workflow;
using FWO.Services;
using FWO.Services.Workflow;
using FWO.Ui.Pages.Request;
using FWO.Ui.Services;
using FWO.Ui.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using NUnit.Framework;
using System.Text.Json.Nodes;

namespace FWO.Test;

[TestFixture]
internal sealed class UiPlanningImplementationTaskDetailsTest
{
    private static BunitContext CreateContext()
    {
        BunitContext context = new();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddSingleton<UserConfig>(new SimulatedUserConfig());
        context.Services.AddSingleton<DomEventService>();
        context.Services.AddSingleton<ApiConnection>(new SimulatedApiConnection());
        return context;
    }

    [Test]
    public static void PlanningImplementationTaskDetails_ResolvesRuleActionAndTrackingIds()
    {
        PlanningImplementationTaskDetails component = new();
        UiRequestWorkflowTest.SetMember(component, "ruleActions", new List<RuleAction> { new() { Id = 4, Name = "allow" } });
        UiRequestWorkflowTest.SetMember(component, "trackings", new List<Tracking> { new() { Id = 9, Name = "log" } });
        WfImplTask task = new() { RuleAction = 4, Tracking = 9 };

        object[] taskArguments = [task];
        string action = (string)UiRequestWorkflowTest.GetPrivateMethod(typeof(PlanningImplementationTaskDetails), "RuleActionName")
            .Invoke(component, taskArguments)!;
        string tracking = (string)UiRequestWorkflowTest.GetPrivateMethod(typeof(PlanningImplementationTaskDetails), "TrackingName")
            .Invoke(component, taskArguments)!;

        Assert.Multiple(() =>
        {
            Assert.That(action, Is.EqualTo("allow"));
            Assert.That(tracking, Is.EqualTo("log"));
        });
    }

    [Test]
    public static void RulePositionDisplayHelper_FormatsKnownRulePositions()
    {
        WfImplTask belowTask = new()
        {
            RulePosition = new CheckPointRulePosition(
                "layer-1",
                CheckPointPositionAnchorKind.Below,
                "rule-42",
                ProvisioningPositioningAlgorithm.DefaultEndOfRulebase)
        };
        WfImplTask topTask = new()
        {
            RulePosition = new CheckPointRulePosition(
                "layer-1",
                CheckPointPositionAnchorKind.Top,
                null,
                ProvisioningPositioningAlgorithm.DefaultEndOfRulebase)
        };

        string below = RulePositionDisplayHelper.Format(belowTask.RulePosition, new PositioningUserConfig());
        string top = RulePositionDisplayHelper.Format(topTask.RulePosition, new PositioningUserConfig());

        Assert.Multiple(() =>
        {
            Assert.That(below, Is.EqualTo("Behind rule rule-42"));
            Assert.That(top, Is.EqualTo("At beginning of layer layer-1"));
        });
    }

    [Test]
    public static void RulePositionDisplayHelper_FormatsFallbackPositions()
    {
        PositioningUserConfig userConfig = new();
        string empty = RulePositionDisplayHelper.Format(null, userConfig);
        string unknown = RulePositionDisplayHelper.Format(
            new UnknownRulePosition(
                "vendor_position",
                1,
                ProvisioningPositioningAlgorithm.DefaultEndOfRulebase,
                new JsonObject { ["vendor_value"] = "value" }),
            userConfig);
        string bottom = RulePositionDisplayHelper.Format(
            new CheckPointRulePosition(
                "layer-1",
                CheckPointPositionAnchorKind.Bottom,
                null,
                ProvisioningPositioningAlgorithm.DefaultEndOfRulebase),
            userConfig);
        string above = RulePositionDisplayHelper.Format(
            new CheckPointRulePosition(
                "layer-1",
                CheckPointPositionAnchorKind.Above,
                "rule-42",
                ProvisioningPositioningAlgorithm.DefaultEndOfRulebase),
            userConfig);

        Assert.Multiple(() =>
        {
            Assert.That(empty, Is.Empty);
            Assert.That(unknown, Does.Contain("vendor_value"));
            Assert.That(bottom, Does.Contain("layer-1"));
            Assert.That(above, Does.Contain("rule-42"));
        });
    }

    [Test]
    public static void PlanningImplementationTaskDetails_UsesImplementationDeviceInTitle()
    {
        PlanningImplementationTaskDetails component = new();
        SimulatedUserConfig userConfig = new();
        WfHandler handler = new()
        {
            Devices = [new Device { Id = 4, Name = "Firewall B" }]
        };
        UiRequestWorkflowTest.SetMember(component, "userConfig", userConfig);
        UiRequestWorkflowTest.SetMember(component, nameof(PlanningImplementationTaskDetails.WfHandler), handler);
        WfImplTask task = new() { TaskType = WfTaskType.access.ToString(), DeviceId = 4 };

        object[] taskArguments = [task];
        string title = (string)UiRequestWorkflowTest.GetPrivateMethod(typeof(PlanningImplementationTaskDetails), "ImplementationTaskTitle")
            .Invoke(component, taskArguments)!;

        Assert.That(title, Is.EqualTo("Firewall B"));
    }

    [Test]
    public static void PlanningImplementationTaskDetails_MutationGateRequiresActionableRequestAndImplementationStates()
    {
        string taskType = WfTaskType.access.ToString();
        WfReqTask requestTask = new() { Id = 11, TaskType = taskType, StateId = 1 };
        WfHandler handler = UiRequestWorkflowTest.CreateWorkflowHandler(
            WorkflowPhases.planning,
            taskType,
            new WfTicket { Id = 7, Tasks = [requestTask] });
        PlanningImplementationTaskDetails component = new();
        UiRequestWorkflowTest.SetMember(component, nameof(PlanningImplementationTaskDetails.WfHandler), handler);
        UiRequestWorkflowTest.SetMember(component, nameof(PlanningImplementationTaskDetails.RequestTask), requestTask);
        WfImplTask implementationTask = new() { TaskType = taskType, StateId = 1 };

        Assert.That(UiRequestWorkflowTest.InvokePrivateBool(component, "CanEditImplementationTask", implementationTask), Is.True);

        requestTask.StateId = 10;
        Assert.That(UiRequestWorkflowTest.InvokePrivateBool(component, "CanEditImplementationTask", implementationTask), Is.False);

        requestTask.StateId = 1;
        implementationTask.StateId = -1;
        Assert.That(UiRequestWorkflowTest.InvokePrivateBool(component, "CanEditImplementationTask", implementationTask), Is.False);

        implementationTask.StateId = 1;
        handler.ReadOnlyMode = true;
        Assert.That(UiRequestWorkflowTest.InvokePrivateBool(component, "CanEditImplementationTask", implementationTask), Is.False);
    }

    [Test]
    public static async Task PlanningImplementationTaskDetails_HidesDatesAlreadyWhileTheListsAreLoading()
    {
        WfReqTask requestTask = new() { Id = 11, TaskType = WfTaskType.access.ToString() };
        WfImplTask implementationTask = new(requestTask, false)
        {
            Id = 21,
            TaskNumber = 1,
            TargetBeginDate = new DateTime(2027, 1, 1),
            TargetEndDate = new DateTime(2027, 2, 1),
            ImplElements = [new WfImplElement { Field = ElemFieldType.time.ToString(), Name = "maintenance" }]
        };
        SimulatedUserConfig userConfig = new()
        {
            ReqTicketFieldVisibility = new TicketFieldVisibilityConfig
            {
                AccessTask = new AccessTaskFieldVisibilityConfig { ShowValidFrom = false, ShowValidTo = false }
            }.ToConfigValue()
        };
        PendingQueriesApiConnection apiConnection = new();

        await using BunitContext context = new();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddSingleton<UserConfig>(userConfig);
        context.Services.AddSingleton<DomEventService>();
        context.Services.AddSingleton<ApiConnection>(apiConnection);
        IRenderedComponent<PlanningImplementationTaskDetails> cut = context.Render<PlanningImplementationTaskDetails>(parameters => parameters
            .Add(parameter => parameter.ImplementationTasks, [implementationTask])
            .Add(parameter => parameter.RequestTask, requestTask)
            .Add(parameter => parameter.WfHandler, new WfHandler())
            .Add(parameter => parameter.CreationMode, true));

        // the first render happens while the rule actions are still being loaded
        Assert.Multiple(() =>
        {
            Assert.That(apiConnection.Loading, Is.True);
            Assert.That(cut.Markup, Does.Contain("maintenance"));
            Assert.That(cut.Markup, Does.Not.Contain("2027"));
        });
    }

    /// <summary>An api connection whose queries do not finish, like a slow connection while the component renders first.</summary>
    private sealed class PendingQueriesApiConnection : SimulatedApiConnection
    {
        private readonly TaskCompletionSource never = new();

        public bool Loading { get; private set; }

        public override async Task<T> SendQueryAsync<T>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
        {
            Loading = true;
            await never.Task;
            throw new InvalidOperationException("never finishes");
        }
    }

    [Test]
    public static async Task PlanningImplementationTaskDetails_RendersGenericTaskAndPlannerHeaderStyle()
    {
        WfImplTask task = new()
        {
            Id = 21,
            TaskType = WfTaskType.generic.ToString(),
            FreeText = "implementation details"
        };

        await using BunitContext context = CreateContext();
        IRenderedComponent<PlanningImplementationTaskDetails> cut = context.Render<PlanningImplementationTaskDetails>(parameters => parameters
            .Add(parameter => parameter.ImplementationTasks, [task])
            .Add(parameter => parameter.RequestTask, new WfReqTask { TaskType = WfTaskType.generic.ToString() })
            .Add(parameter => parameter.WfHandler, new WfHandler()));

        Assert.Multiple(() =>
        {
            Assert.That(cut.Markup, Does.Contain("implementation details"));
            Assert.That(cut.Markup, Does.Contain("bg-secondary-subtle"));
            Assert.That(cut.Markup, Does.Contain("free_text"));
        });
    }

    [Test]
    public static async Task PlanningImplementationTaskDetails_RendersRuleGroupObjectAndTimeBranches()
    {
        List<WfImplTask> tasks =
        [
            new WfImplTask
            {
                Id = 1,
                TaskType = WfTaskType.access.ToString(),
                RuleAction = 7,
                Tracking = 8,
                ImplElements =
                [
                    new WfImplElement { Field = ElemFieldType.source.ToString(), Name = "source", IpString = "10.0.0.1" },
                    new WfImplElement { Field = ElemFieldType.destination.ToString(), Name = "destination", IpString = "10.0.0.2" },
                    new WfImplElement { Field = ElemFieldType.service.ToString(), Name = "https", Port = 443, ProtoId = 6 }
                ]
            },
            new WfImplTask
            {
                Id = 2,
                TaskType = WfTaskType.rule_delete.ToString(),
                ImplElements = [new WfImplElement { Field = ElemFieldType.rule.ToString(), RuleUid = "rule-2" }]
            },
            new WfImplTask
            {
                Id = 3,
                TaskType = WfTaskType.group_create.ToString(),
                ImplElements = [new WfImplElement { Field = ElemFieldType.source.ToString(), GroupName = "web-group", Name = "member" }]
            },
            new WfImplTask
            {
                Id = 4,
                TaskType = WfTaskType.object_create.ToString(),
                ImplElements = [new WfImplElement { Field = ElemFieldType.source.ToString(), Name = "network-object", IpString = "10.0.0.3" }]
            },
            new WfImplTask
            {
                Id = 5,
                TaskType = WfTaskType.object_create.ToString(),
                ImplElements = [new WfImplElement { Field = ElemFieldType.time.ToString(), Name = "maintenance" }]
            }
        ];

        await using BunitContext context = CreateContext();
        IRenderedComponent<PlanningImplementationTaskDetails> cut = context.Render<PlanningImplementationTaskDetails>(parameters => parameters
            .Add(parameter => parameter.ImplementationTasks, tasks)
            .Add(parameter => parameter.RequestTask, new WfReqTask { TaskType = WfTaskType.access.ToString() })
            .Add(parameter => parameter.WfHandler, new WfHandler { ActTicket = new WfTicket() }));

        Assert.Multiple(() =>
        {
            Assert.That(cut.Markup, Does.Contain("source"));
            Assert.That(cut.Markup, Does.Contain("destination"));
            Assert.That(cut.Markup, Does.Contain("https"));
            Assert.That(cut.Markup, Does.Contain("rule-2"));
            Assert.That(cut.Markup, Does.Contain("web-group"));
            Assert.That(cut.Markup, Does.Contain("network-object"));
            Assert.That(cut.Markup, Does.Contain("maintenance"));
        });
    }

    [Test]
    public static async Task PlanningImplementationTaskDetails_SavesEditableFieldsInCreationMode()
    {
        WfReqTask requestTask = new() { Id = 11, TaskType = WfTaskType.generic.ToString() };
        WfHandler handler = UiRequestWorkflowTest.CreateWorkflowHandler(
            WorkflowPhases.planning,
            requestTask.TaskType,
            new WfTicket { Id = 7, Tasks = [requestTask] });
        handler.SelectReqTask(requestTask, ObjAction.display);
        WfImplTask implementationTask = new(requestTask, false) { Id = 21, TaskNumber = 1 };
        handler.SelectImplTask(implementationTask, ObjAction.add);
        Management management = new() { Id = 4, Name = "Management A" };
        Device device = new() { Id = 8, Name = "Firewall A", Management = management };
        handler.Managements.Add(management);
        handler.Devices.Add(device);
        List<WfImplTask> implementationTasks = [implementationTask];

        await using BunitContext context = CreateContext();
        IRenderedComponent<PlanningImplementationTaskDetails> cut = context.Render<PlanningImplementationTaskDetails>(parameters => parameters
            .Add(parameter => parameter.ImplementationTasks, implementationTasks)
            .Add(parameter => parameter.RequestTask, requestTask)
            .Add(parameter => parameter.WfHandler, handler)
            .Add(parameter => parameter.CreationMode, true));

        PlanningImplTaskEditor editor = Editor(cut);
        await cut.InvokeAsync(() => editor.OpenFreeText(implementationTask, requestTask));
        await editor.SaveFreeText("planner text");
        await editor.SaveManagement(management);
        await editor.SaveDevice(device);

        WfImplTask savedTask = implementationTasks.Single();
        Assert.Multiple(() =>
        {
            Assert.That(savedTask.FreeText, Is.EqualTo("planner text"));
            Assert.That(savedTask.ManagementId, Is.EqualTo(4));
            Assert.That(savedTask.DeviceId, Is.EqualTo(8));
        });
    }

    [Test]
    public static async Task PlanningImplementationTaskDetails_RejectsCrossManagementDevice()
    {
        WfReqTask requestTask = new() { Id = 11, TaskType = WfTaskType.generic.ToString() };
        WfHandler handler = UiRequestWorkflowTest.CreateWorkflowHandler(
            WorkflowPhases.planning,
            requestTask.TaskType,
            new WfTicket { Id = 7, Tasks = [requestTask] });
        Management managementA = new() { Id = 4, Name = "Management A" };
        Management managementB = new() { Id = 5, Name = "Management B" };
        Device deviceA = new() { Id = 8, Name = "Firewall A", Management = managementA };
        Device deviceB = new() { Id = 9, Name = "Firewall B", Management = managementB };
        handler.Managements.Add(managementA);
        handler.Managements.Add(managementB);
        handler.Devices.Add(deviceA);
        handler.Devices.Add(deviceB);
        handler.SelectReqTask(requestTask, ObjAction.display);
        WfImplTask implementationTask = new(requestTask, false)
        {
            Id = 21,
            TaskNumber = 1,
            ManagementId = managementA.Id,
            DeviceId = deviceA.Id
        };
        handler.SelectImplTask(implementationTask, ObjAction.edit);

        await using BunitContext context = CreateContext();
        IRenderedComponent<PlanningImplementationTaskDetails> cut = context.Render<PlanningImplementationTaskDetails>(parameters => parameters
            .Add(parameter => parameter.ImplementationTasks, [implementationTask])
            .Add(parameter => parameter.RequestTask, requestTask)
            .Add(parameter => parameter.WfHandler, handler)
            .Add(parameter => parameter.CreationMode, true));

        PlanningImplTaskEditor editor = Editor(cut);
        await cut.InvokeAsync(() => editor.OpenDevice(implementationTask, requestTask));
        await editor.SaveDevice(deviceB);
        List<Device> availableDevices = editor.AvailableDevices();

        Assert.Multiple(() =>
        {
            Assert.That(implementationTask.ManagementId, Is.EqualTo(managementA.Id));
            Assert.That(implementationTask.DeviceId, Is.EqualTo(deviceA.Id));
            Assert.That(availableDevices, Has.Count.EqualTo(1));
            Assert.That(availableDevices[0], Is.SameAs(deviceA));
            Assert.That(PlanningImplTaskDisplayLogic.IsDeviceCompatible(implementationTask, requestTask, deviceB), Is.False);
        });
    }

    [Test]
    public static async Task PlanningImplementationTaskDetails_ClearsDeviceWhenManagementChanges()
    {
        WfReqTask requestTask = new() { Id = 11, TaskType = WfTaskType.generic.ToString() };
        WfHandler handler = UiRequestWorkflowTest.CreateWorkflowHandler(
            WorkflowPhases.planning,
            requestTask.TaskType,
            new WfTicket { Id = 7, Tasks = [requestTask] });
        Management managementA = new() { Id = 4, Name = "Management A" };
        Management managementB = new() { Id = 5, Name = "Management B" };
        Device deviceA = new() { Id = 8, Name = "Firewall A", Management = managementA };
        handler.Managements.Add(managementA);
        handler.Managements.Add(managementB);
        handler.Devices.Add(deviceA);
        handler.SelectReqTask(requestTask, ObjAction.display);
        WfImplTask implementationTask = new(requestTask, false)
        {
            Id = 21,
            TaskNumber = 1,
            ManagementId = managementA.Id,
            DeviceId = deviceA.Id
        };
        handler.SelectImplTask(implementationTask, ObjAction.edit);
        List<WfImplTask> implementationTasks = [implementationTask];

        await using BunitContext context = CreateContext();
        IRenderedComponent<PlanningImplementationTaskDetails> cut = context.Render<PlanningImplementationTaskDetails>(parameters => parameters
            .Add(parameter => parameter.ImplementationTasks, implementationTasks)
            .Add(parameter => parameter.RequestTask, requestTask)
            .Add(parameter => parameter.WfHandler, handler)
            .Add(parameter => parameter.CreationMode, true));

        PlanningImplTaskEditor editor = Editor(cut);
        await cut.InvokeAsync(() => editor.OpenManagement(implementationTask, requestTask));
        await editor.SaveManagement(managementB);
        WfImplTask savedTask = implementationTasks.Single();

        Assert.Multiple(() =>
        {
            Assert.That(savedTask.ManagementId, Is.EqualTo(managementB.Id));
            Assert.That(savedTask.DeviceId, Is.Null);
        });
    }

    [TestCase(false, true, TestName = "PlanningImplementationTaskDetails_OffersAddingFreeTextToChangeableRuleTasks")]
    [TestCase(true, false, TestName = "PlanningImplementationTaskDetails_HidesEmptyFreeTextOfReadOnlyRuleTasks")]
    public static async Task PlanningImplementationTaskDetails_EmptyFreeTextOfRuleTasks(bool readOnly, bool shown)
    {
        WfReqTask requestTask = new() { Id = 11, TaskType = WfTaskType.access.ToString() };
        WfHandler handler = UiRequestWorkflowTest.CreateWorkflowHandler(
            WorkflowPhases.planning,
            requestTask.TaskType,
            new WfTicket { Id = 7, Tasks = [requestTask] });
        handler.ReadOnlyMode = readOnly;
        WfImplTask implementationTask = new(requestTask, false) { Id = 21, TaskNumber = 1 };

        await using BunitContext context = CreateContext();
        IRenderedComponent<PlanningImplementationTaskDetails> cut = context.Render<PlanningImplementationTaskDetails>(parameters => parameters
            .Add(parameter => parameter.ImplementationTasks, [implementationTask])
            .Add(parameter => parameter.RequestTask, requestTask)
            .Add(parameter => parameter.WfHandler, handler)
            .Add(parameter => parameter.CreationMode, true));

        // like the free text column of the list view
        Assert.That(cut.Markup.Contains("free_text"), Is.EqualTo(shown));
    }

    [Test]
    public static async Task PlanningImplementationTaskDetails_OpensManagementFreeTextAndDeviceEditors()
    {
        WfReqTask requestTask = new() { Id = 11, TaskType = WfTaskType.generic.ToString() };
        WfHandler handler = UiRequestWorkflowTest.CreateWorkflowHandler(
            WorkflowPhases.planning,
            requestTask.TaskType,
            new WfTicket { Id = 7, Tasks = [requestTask] });
        Management management = new() { Id = 4, Name = "Management A" };
        Device device = new() { Id = 8, Name = "Firewall A" };
        handler.Managements.Add(management);
        handler.Devices.Add(device);
        handler.SelectReqTask(requestTask, ObjAction.display);
        WfImplTask implementationTask = new(requestTask, false)
        {
            Id = 21,
            TaskNumber = 1,
            ManagementId = management.Id,
            DeviceId = device.Id,
            FreeText = "existing text"
        };
        handler.SelectImplTask(implementationTask, ObjAction.edit);

        await using BunitContext context = CreateContext();
        IRenderedComponent<PlanningImplementationTaskDetails> cut = context.Render<PlanningImplementationTaskDetails>(parameters => parameters
            .Add(parameter => parameter.ImplementationTasks, [implementationTask])
            .Add(parameter => parameter.RequestTask, requestTask)
            .Add(parameter => parameter.WfHandler, handler)
            .Add(parameter => parameter.CreationMode, true));

        await cut.InvokeAsync(() => UiRequestWorkflowTest.InvokePrivateTask(cut.Instance, "OpenManagementEditor", implementationTask));
        await cut.InvokeAsync(() => UiRequestWorkflowTest.InvokePrivateTask(cut.Instance, "OpenFreeTextEditor", implementationTask));
        await cut.InvokeAsync(() => UiRequestWorkflowTest.InvokePrivateTask(cut.Instance, "OpenDeviceEditor", implementationTask));

        PlanningImplTaskEditor editor = Editor(cut);
        Assert.Multiple(() =>
        {
            Assert.That(editor.DisplayManagementEditor, Is.True);
            Assert.That(editor.SelectedManagement, Is.SameAs(management));
            Assert.That(editor.DisplayFreeTextEditor, Is.True);
            Assert.That(editor.FreeText, Is.EqualTo("existing text"));
            Assert.That(editor.DisplayDeviceEditor, Is.True);
            Assert.That(editor.SelectedDevice, Is.SameAs(device));
            Assert.That(cut.FindComponent<ManagementEditor>().Instance.Display, Is.True);
            Assert.That(cut.FindComponent<DeviceEditor>().Instance.Display, Is.True);
        });
    }

    [Test]
    public static async Task PlanningImplementationTaskDetails_SavesGroupAndTimeObjectNames()
    {
        WfReqTask requestTask = new() { Id = 11, TaskType = WfTaskType.group_create.ToString() };
        requestTask.SetAddInfo(AdditionalInfoKeys.GrpName, "request-group");
        WfHandler handler = UiRequestWorkflowTest.CreateWorkflowHandler(
            WorkflowPhases.planning,
            requestTask.TaskType,
            new WfTicket { Id = 7, Tasks = [requestTask] });
        handler.SelectReqTask(requestTask, ObjAction.display);
        WfImplTask groupTask = new(requestTask, false)
        {
            TaskNumber = 1,
            TaskType = WfTaskType.group_create.ToString(),
            ImplElements =
            [
                new WfImplElement { Field = ElemFieldType.source.ToString(), GroupName = "old-group" },
                new WfImplElement { Field = ElemFieldType.service.ToString(), GroupName = "old-group" }
            ]
        };
        List<WfImplTask> groupTasks = [groupTask];
        handler.SelectImplTask(groupTask, ObjAction.edit);

        await using BunitContext context = CreateContext();
        IRenderedComponent<PlanningImplementationTaskDetails> cut = context.Render<PlanningImplementationTaskDetails>(parameters => parameters
            .Add(parameter => parameter.ImplementationTasks, groupTasks)
            .Add(parameter => parameter.RequestTask, requestTask)
            .Add(parameter => parameter.WfHandler, handler)
            .Add(parameter => parameter.CreationMode, true));

        PlanningImplTaskEditor editor = Editor(cut);
        await cut.InvokeAsync(() => editor.OpenName(groupTask, requestTask));
        Assert.That(editor.Name, Is.EqualTo("old-group"));
        await editor.SaveName("new-group");
        Assert.That(groupTasks.Single().ImplElements.All(element => element.GroupName == "new-group"), Is.True);
        cut.Render();
        Assert.That(cut.Markup, Does.Contain("new-group"));
        Assert.That(cut.Markup, Does.Not.Contain("request-group"));
        await cut.InvokeAsync(() => UiRequestWorkflowTest.InvokePrivateTask(cut.Instance, "OpenNameEditor", groupTasks.Single()));
        Assert.That(editor.Name, Is.EqualTo("new-group"));

        WfImplTask timeTask = new(requestTask, false)
        {
            TaskNumber = 2,
            TaskType = WfTaskType.object_create.ToString(),
            ImplElements = [new WfImplElement { Field = ElemFieldType.time.ToString(), Name = "old-time" }]
        };
        List<WfImplTask> timeTasks = [timeTask];
        UiRequestWorkflowTest.SetMatrix(handler, timeTask.TaskType, new StateMatrix
        {
            LowestInputState = 0,
            LowestStartedState = 2,
            LowestEndState = 10
        });
        handler.SelectImplTask(timeTask, ObjAction.edit);
        await cut.InvokeAsync(() => cut.Instance.SetParametersAsync(ParameterView.FromDictionary(
            new Dictionary<string, object?>
            {
                [nameof(PlanningImplementationTaskDetails.ImplementationTasks)] = timeTasks,
                [nameof(PlanningImplementationTaskDetails.RequestTask)] = requestTask,
                [nameof(PlanningImplementationTaskDetails.WfHandler)] = handler,
                [nameof(PlanningImplementationTaskDetails.CreationMode)] = true
            })));
        await cut.InvokeAsync(() => editor.OpenName(timeTask, requestTask));
        await editor.SaveName("new-time");

        Assert.That(timeTasks.Single().ImplElements.Single().Name, Is.EqualTo("new-time"));
    }

    private static PlanningImplTaskEditor Editor(IRenderedComponent<PlanningImplementationTaskDetails> cut) =>
        UiRequestWorkflowTest.GetMember<PlanningImplTaskEditor>(cut.Instance, "Editor");

    private sealed class PositioningUserConfig : UserConfig
    {
        public override string GetText(string key)
        {
            return key switch
            {
                "rule_position_behind_rule" => "Behind rule {0}",
                "rule_position_at_start_of_layer" => "At beginning of layer {0}",
                "rule_position_before_rule" => "Before rule {0}",
                "rule_position_at_end_of_layer" => "At end of layer {0}",
                _ => key
            };
        }
    }

}
