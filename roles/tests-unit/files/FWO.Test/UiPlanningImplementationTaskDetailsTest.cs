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
using System.Reflection;
using System.Text.Json.Nodes;

namespace FWO.Test;

[TestFixture]
internal sealed class UiPlanningImplementationTaskDetailsTest
{
    private static readonly object[] kNoArguments = [];

    private static readonly string[] kGroupContents = ["one", "two", "three", "four", "five", "six"];

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
    public static void PlanningImplementationTaskDetails_SelectsOnlyRequestedObjectElement()
    {
        List<WfImplElement> elements =
        [
            new WfImplElement { Field = ElemFieldType.source.ToString(), Name = "existing", ImplAction = RequestAction.unchanged.ToString() },
            new WfImplElement { Field = ElemFieldType.source.ToString(), Name = "created", ImplAction = RequestAction.create.ToString() },
            new WfImplElement { Field = ElemFieldType.source.ToString(), Name = "second-created", ImplAction = RequestAction.create.ToString() }
        ];
        WfImplTask task = new() { TaskType = WfTaskType.object_create.ToString(), ImplElements = elements };

        object[] elementArguments = [task, ElemFieldType.source];
        IEnumerable<WfImplElement> result = (IEnumerable<WfImplElement>)UiRequestWorkflowTest.GetPrivateMethod(
            typeof(PlanningImplementationTaskDetails), "ElementsForDisplay").Invoke(null, elementArguments)!;

        Assert.That(result.Select(element => element.Name), Is.EqualTo(new List<string> { "created" }));
    }

    [Test]
    public static void PlanningImplementationTaskDetails_TruncatesGroupContent()
    {
        object[] groupArguments = [kGroupContents];
        string result = (string)UiRequestWorkflowTest.GetPrivateMethod(typeof(PlanningImplementationTaskDetails), "TruncatedGroupContent")
            .Invoke(null, groupArguments)!;

        Assert.Multiple(() =>
        {
            Assert.That(result, Does.StartWith("one, two, three, four, five"));
            Assert.That(result, Does.Contain("(+1)"));
        });
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
    public static void PlanningImplementationTaskDetails_CoversTaskTypeAndDisplayHelpers()
    {
        PlanningImplementationTaskDetails component = new();
        UiRequestWorkflowTest.SetMember(component, "userConfig", new SimulatedUserConfig());
        WfReqTask requestTask = new() { TaskType = WfTaskType.group_create.ToString() };
        requestTask.SetAddInfo(AdditionalInfoKeys.GrpName, "web-group");
        UiRequestWorkflowTest.SetMember(component, nameof(PlanningImplementationTaskDetails.RequestTask), requestTask);

        WfImplTask groupTask = new()
        {
            TaskType = WfTaskType.group_create.ToString(),
            FreeText = "group text",
            ImplElements = [new WfImplElement { Field = ElemFieldType.source.ToString(), GroupName = "fallback-group" }]
        };
        WfImplTask ruleDeleteTask = new()
        {
            TaskType = WfTaskType.rule_delete.ToString(),
            ImplElements = [new WfImplElement { Field = ElemFieldType.rule.ToString(), RuleUid = "rule-1" }]
        };
        WfImplTask genericTask = new() { TaskType = WfTaskType.generic.ToString(), FreeText = "text" };
        WfImplTask timeTask = new()
        {
            TaskType = WfTaskType.object_create.ToString(),
            TargetBeginDate = new DateTime(2027, 12, 31),
            TargetEndDate = new DateTime(2028, 1, 1),
            ImplElements = [new WfImplElement { Field = ElemFieldType.time.ToString(), Name = "maintenance" }]
        };
        object[] groupArguments = [groupTask];
        object[] ruleDeleteArguments = [ruleDeleteTask];
        object[] timeArguments = [timeTask];

        Assert.Multiple(() =>
        {
            Assert.That(UiRequestWorkflowTest.InvokePrivateBool(component, "IsRuleImplementationTask", ruleDeleteTask), Is.True);
            Assert.That(UiRequestWorkflowTest.InvokePrivateBool(component, "IsRuleTask", new WfImplTask { TaskType = WfTaskType.rule_modify.ToString() }), Is.True);
            Assert.That(InvokeImplementationTaskBool(component, "IsGroupTask", groupTask), Is.True);
            Assert.That(UiRequestWorkflowTest.InvokePrivateBool(component, "HasFreeText", genericTask), Is.True);
            Assert.That(UiRequestWorkflowTest.InvokePrivateBool(component, "CanEditFreeText", new WfImplTask { TaskType = WfTaskType.access.ToString() }), Is.True);
            Assert.That(UiRequestWorkflowTest.InvokePrivateBool(component, "CanEditFreeText", new WfImplTask { TaskType = WfTaskType.object_create.ToString() }), Is.False);
            Assert.That(UiRequestWorkflowTest.InvokePrivateBool(component, "IsTimeObjectTask", timeTask), Is.True);
            Assert.That(UiRequestWorkflowTest.GetPrivateMethod(typeof(PlanningImplementationTaskDetails), "GroupName")
                .Invoke(component, groupArguments), Is.EqualTo("fallback-group"));
            Assert.That(UiRequestWorkflowTest.GetPrivateMethod(typeof(PlanningImplementationTaskDetails), "RuleUid")
                .Invoke(null, ruleDeleteArguments), Is.EqualTo("rule-1"));
            Assert.That(UiRequestWorkflowTest.GetPrivateMethod(typeof(PlanningImplementationTaskDetails), "ObjectName")
                .Invoke(component, timeArguments), Is.EqualTo("maintenance"));
        });
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
            .Add(parameter => parameter.ImplementationTasks, new List<WfImplTask> { task })
            .Add(parameter => parameter.RequestTask, new WfReqTask { TaskType = WfTaskType.generic.ToString() })
            .Add(parameter => parameter.WfHandler, new WfHandler()));

        Assert.Multiple(() =>
        {
            Assert.That(cut.Markup, Does.Contain("implementation details"));
            Assert.That(cut.Markup, Does.Contain("bg-secondary-subtle"));
            Assert.That(cut.Markup, Does.Contain("free_text"));
        });
    }

    private static bool InvokeImplementationTaskBool(PlanningImplementationTaskDetails component, string methodName, WfImplTask task)
    {
        MethodInfo method = typeof(PlanningImplementationTaskDetails).GetMethods(
                BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
            .Single(candidate => candidate.Name == methodName
                && candidate.GetParameters().Length == 1
                && candidate.GetParameters()[0].ParameterType == typeof(WfImplTask));
        object[] taskArguments = [task];
        return (bool)method.Invoke(component, taskArguments)!;
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

        await UiRequestWorkflowTest.InvokePrivateTask(cut.Instance, "SaveFreeText", "planner text");
        await UiRequestWorkflowTest.InvokePrivateTask(cut.Instance, "SaveManagement", management);
        await UiRequestWorkflowTest.InvokePrivateTask(cut.Instance, "SaveDevice", device);

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
            .Add(parameter => parameter.ImplementationTasks, new List<WfImplTask> { implementationTask })
            .Add(parameter => parameter.RequestTask, requestTask)
            .Add(parameter => parameter.WfHandler, handler)
            .Add(parameter => parameter.CreationMode, true));

        await UiRequestWorkflowTest.InvokePrivateTask(cut.Instance, "SaveDevice", deviceB);
        object[] availableDeviceArguments = [implementationTask];
        object[] compatibilityArguments = [implementationTask, deviceB];
        List<Device> availableDevices = (List<Device>)UiRequestWorkflowTest.GetPrivateMethod(typeof(PlanningImplementationTaskDetails), "AvailableDevices")
            .Invoke(cut.Instance, availableDeviceArguments)!;

        Assert.Multiple(() =>
        {
            Assert.That(implementationTask.ManagementId, Is.EqualTo(managementA.Id));
            Assert.That(implementationTask.DeviceId, Is.EqualTo(deviceA.Id));
            Assert.That(availableDevices, Has.Count.EqualTo(1));
            Assert.That(availableDevices[0], Is.SameAs(deviceA));
            Assert.That(UiRequestWorkflowTest.GetPrivateMethod(typeof(PlanningImplementationTaskDetails), "IsDeviceCompatible")
                .Invoke(cut.Instance, compatibilityArguments), Is.EqualTo(false));
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

        await UiRequestWorkflowTest.InvokePrivateTask(cut.Instance, "SaveManagement", managementB);
        WfImplTask savedTask = implementationTasks.Single();

        Assert.Multiple(() =>
        {
            Assert.That(savedTask.ManagementId, Is.EqualTo(managementB.Id));
            Assert.That(savedTask.DeviceId, Is.Null);
        });
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
            .Add(parameter => parameter.ImplementationTasks, new List<WfImplTask> { implementationTask })
            .Add(parameter => parameter.RequestTask, requestTask)
            .Add(parameter => parameter.WfHandler, handler)
            .Add(parameter => parameter.CreationMode, true));

        await UiRequestWorkflowTest.InvokePrivateTask(cut.Instance, "OpenManagementEditor", implementationTask);
        await UiRequestWorkflowTest.InvokePrivateTask(cut.Instance, "OpenFreeTextEditor", implementationTask);
        await UiRequestWorkflowTest.InvokePrivateTask(cut.Instance, "OpenDeviceEditor", implementationTask);

        Assert.Multiple(() =>
        {
            Assert.That(UiRequestWorkflowTest.GetMember<bool>(cut.Instance, "displayManagementEditor"), Is.True);
            Assert.That(UiRequestWorkflowTest.GetMember<Management>(cut.Instance, "selectedManagement"), Is.SameAs(management));
            Assert.That(UiRequestWorkflowTest.GetMember<bool>(cut.Instance, "displayTextEditor"), Is.True);
            Assert.That(UiRequestWorkflowTest.GetMember<string>(cut.Instance, "textEditorValue"), Is.EqualTo("existing text"));
            Assert.That(UiRequestWorkflowTest.GetMember<bool>(cut.Instance, "displayDeviceEditor"), Is.True);
            Assert.That(UiRequestWorkflowTest.GetMember<Device>(cut.Instance, "selectedDevice"), Is.SameAs(device));
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

        await UiRequestWorkflowTest.InvokePrivateTask(cut.Instance, "SaveName", "new-group");
        Assert.That(groupTasks.Single().ImplElements.All(element => element.GroupName == "new-group"), Is.True);
        cut.Render();
        Assert.That(cut.Markup, Does.Contain("new-group"));
        Assert.That(cut.Markup, Does.Not.Contain("request-group"));
        await UiRequestWorkflowTest.InvokePrivateTask(cut.Instance, "OpenNameEditor", groupTask);
        Assert.That(UiRequestWorkflowTest.GetMember<string>(cut.Instance, "nameEditorValue"), Is.EqualTo("new-group"));

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
        await UiRequestWorkflowTest.InvokePrivateTask(cut.Instance, "SaveName", "new-time");

        Assert.That(timeTasks.Single().ImplElements.Single().Name, Is.EqualTo("new-time"));
    }

    [Test]
    public static void PlanningImplementationTaskDetails_ResolvesServiceRowsAndManagementFallbacks()
    {
        PlanningImplementationTaskDetails component = new();
        Management management = new() { Id = 4, Name = "Management A" };
        Device device = new() { Id = 8, Name = "Firewall A", Management = management };
        WfReqTask requestTask = new() { ManagementId = 3 };
        WfHandler handler = new() { Devices = [device] };
        UiRequestWorkflowTest.SetMember(component, nameof(PlanningImplementationTaskDetails.WfHandler), handler);
        UiRequestWorkflowTest.SetMember(component, nameof(PlanningImplementationTaskDetails.RequestTask), requestTask);
        UiRequestWorkflowTest.SetMember(component, "ipProtocols", new List<IpProtocol> { new() { Id = 6, Name = "tcp" } });

        WfImplTask serviceTask = new()
        {
            ImplElements = [new WfImplElement { Field = ElemFieldType.service.ToString(), Name = "https", Port = 443, ProtoId = 6 }]
        };
        object[] serviceArguments = [serviceTask];
        List<(string Name, string Content, string Action)> rows = (List<(string Name, string Content, string Action)>)UiRequestWorkflowTest.GetPrivateMethod(typeof(PlanningImplementationTaskDetails), "GroupServiceDisplayRows")
            .Invoke(component, serviceArguments)!;
        object[] explicitManagementArguments = [new WfImplTask { ManagementId = 9 }];
        object[] deviceManagementArguments = [new WfImplTask { DeviceId = 8 }];
        object[] requestManagementArguments = [new WfImplTask()];
        object explicitManagement = UiRequestWorkflowTest.GetPrivateMethod(typeof(PlanningImplementationTaskDetails), "EffectiveManagementId")
            .Invoke(component, explicitManagementArguments)!;
        object deviceManagement = UiRequestWorkflowTest.GetPrivateMethod(typeof(PlanningImplementationTaskDetails), "EffectiveManagementId")
            .Invoke(component, deviceManagementArguments)!;
        object requestManagement = UiRequestWorkflowTest.GetPrivateMethod(typeof(PlanningImplementationTaskDetails), "EffectiveManagementId")
            .Invoke(component, requestManagementArguments)!;

        Assert.Multiple(() =>
        {
            Assert.That(rows.Single().Name, Is.EqualTo("https"));
            Assert.That(rows.Single().Content, Does.Contain("443"));
            Assert.That(explicitManagement, Is.EqualTo(9));
            Assert.That(deviceManagement, Is.EqualTo(4));
            Assert.That(requestManagement, Is.EqualTo(3));
        });
    }

    [Test]
    public static void PlanningImplementationTaskDetails_ConvertsServiceElements()
    {
        WfImplTask task = new()
        {
            ImplElements =
            [
                new WfImplElement { Field = ElemFieldType.source.ToString(), Name = "source" },
                new WfImplElement { Field = ElemFieldType.source.ToString(), Name = "source-2" },
                new WfImplElement { Field = ElemFieldType.service.ToString(), Name = "https", Port = 443, ProtoId = 6 }
            ]
        };

        WfImplElement serviceElement = task.ImplElements.Single(element => element.Field == ElemFieldType.service.ToString());
        object[] serviceElementArguments = [serviceElement];
        NwServiceElement converted = (NwServiceElement)UiRequestWorkflowTest.GetPrivateMethod(typeof(PlanningImplementationTaskDetails), "ToServiceElement")
            .Invoke(null, serviceElementArguments)!;
        Assert.Multiple(() =>
        {
            Assert.That(converted.Name, Is.EqualTo("https"));
            Assert.That(converted.Port, Is.EqualTo(443));
        });
    }

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
