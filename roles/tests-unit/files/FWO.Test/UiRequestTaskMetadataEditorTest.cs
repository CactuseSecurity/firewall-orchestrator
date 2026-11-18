using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Flow;
using FWO.Data.Workflow;
using FWO.Api.Client;
using FWO.Services.Workflow;
using FWO.Ui.Pages.Request;
using FWO.Ui.Services;
using FWO.Ui.Shared;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Collections.Generic;
using System.Reflection;

namespace FWO.Test;

[TestFixture]
internal class UiRequestTaskMetadataEditorTest
{
    private static readonly int[] kSingleGatewayId = [1];
    private static readonly int[] kTwoGatewayIds = [1, 2];
    private static readonly int[] kSelectedGatewayId = [2];
    private static readonly int[] kAllDeviceId = [WfReqTaskBase.kAllDevicesId];

    [Test]
    public void NewInterfaceOwnerLayout_ContainsBothReadOnlyFieldsInOneRow()
    {
        string branch = ReadNewInterfaceBranch();

        Assert.Multiple(() =>
        {
            Assert.That(CountOccurrences(branch, "<div class=\"col-sm-6\">"), Is.EqualTo(3));
            Assert.That(branch, Does.Contain("@(userConfig.GetText(\"owner\"))*:"));
            Assert.That(branch, Does.Contain("@(userConfig.GetText(\"requesting_owner\"))"));
            Assert.That(branch, Does.Contain("@Owner?.Display()"));
            Assert.That(branch, Does.Contain("@WfHandler.GetRequestingOwner()"));
        });
    }

    [Test]
    public void NewInterfaceOwnerLayout_UsesIndependentEditabilityChecks()
    {
        string branch = ReadNewInterfaceBranch();

        Assert.Multiple(() =>
        {
            Assert.That(branch, Does.Contain("CanEditField(WorkflowEditableFieldKeys.Owner)"));
            Assert.That(branch, Does.Contain("CanEditField(WorkflowEditableFieldKeys.RequestingOwner)"));
            Assert.That(branch, Does.Not.Contain("CanEditField(WorkflowEditableFieldKeys.Owner) || CanEditField(WorkflowEditableFieldKeys.RequestingOwner)"));
        });
    }

    [Test]
    public void AccessOwnerLayout_UsesOwnerFieldEditability()
    {
        string source = File.ReadAllText(LocateRepositoryFile(Path.Combine(
            "roles", "ui", "files", "FWO.UI", "Pages", "Request", "RequestTaskMetadataEditor.razor")));

        Assert.Multiple(() =>
        {
            Assert.That(source, Does.Contain("@if (TaskType == WfTaskType.access)"));
            Assert.That(source, Does.Contain("Elements=\"NewInterfaceOwnerOptions\" Nullable=\"true\""));
            Assert.That(source, Does.Contain("CanEditField(WorkflowEditableFieldKeys.Owner)"));
            Assert.That(source, Does.Contain("WfObjectTaskHelper.IsManagementObjectTask(TaskType.ToString())"));
        });
    }

    [Test]
    public void DateOnlyValidityIsEditableOnlyWithoutTimeObjectReference()
    {
        WfReqTask accessTask = new() { Id = 20, TaskType = WfTaskType.access.ToString() };
        WfTicket ticket = new() { Tasks = [accessTask] };
        RequestTaskMetadataEditor component = CreateComponent(
            new WfHandler { ActReqTask = accessTask, ActTicket = ticket },
            new SimulatedUserConfig());
        SetMember(component, nameof(RequestTaskMetadataEditor.CanEditField), (Func<string, bool>)(_ => true));

        bool dateOnlyEditable = (bool)InvokePrivate(component, "CanEditDateOnly", WorkflowEditableFieldKeys.ValidTo)!;
        accessTask.Elements.Add(new WfReqElement
        {
            Field = ElemFieldType.time.ToString(),
            FlowTimeObjectId = 42
        });
        bool referencedObjectEditable = (bool)InvokePrivate(component, "CanEditDateOnly", WorkflowEditableFieldKeys.ValidTo)!;

        Assert.Multiple(() =>
        {
            Assert.That(dateOnlyEditable, Is.True);
            Assert.That(referencedObjectEditable, Is.False);
        });
    }

    [Test]
    public void SetDeviceAndSetDevices_HandleAllAndConcreteSelections()
    {
        Device gatewayOne = new() { Id = 1, Name = "gw-1" };
        Device gatewayTwo = new() { Id = 2, Name = "gw-2" };
        SimulatedUserConfig userConfig = new();
        RequestTaskMetadataEditor component = CreateComponent(new WfHandler { Devices = [gatewayOne, gatewayTwo] }, userConfig);
        SetMember(component, "SelectedDevices", new List<Device> { gatewayOne });

        component.SetDevices([new Device { Id = WfReqTaskBase.kAllDevicesId }, gatewayTwo]);
        Assert.That(component.CurrentSelectedDevices.Select(device => device.Id), Is.EqualTo(kAllDeviceId));

        component.SetDevice(new Device { Id = WfReqTaskBase.kAllDevicesId });
        Assert.That(component.DisplayDevices(), Is.EqualTo(userConfig.GetText("all")));

        component.SetDevices([new Device { Id = WfReqTaskBase.kAllDevicesId }, gatewayOne]);
        Assert.That(component.CurrentSelectedDevices.Select(device => device.Id), Is.EqualTo(kSingleGatewayId));

        component.SetDevices([gatewayOne, gatewayTwo]);
        Assert.That(component.CurrentSelectedDevices.Select(device => device.Id), Is.EqualTo(kTwoGatewayIds));
        Assert.That(component.DisplayDevices(), Is.EqualTo("gw-1, gw-2"));
    }

    [Test]
    public void RefreshSelectableDevices_ProvidesSharedAllDevicesOption()
    {
        Device gateway = new() { Id = 1, Name = "gw-1" };
        SimulatedUserConfig userConfig = new();
        RequestTaskMetadataEditor component = CreateComponent(new WfHandler { Devices = [gateway] }, userConfig);

        InvokePrivate(component, "RefreshSelectableDevices");

        Assert.Multiple(() =>
        {
            Assert.That(component.SelectableDevices.Select(device => device.Id), Is.EqualTo(new[] { WfReqTaskBase.kAllDevicesId, 1 }));
            Assert.That(component.SelectableDevices.First().Name, Is.EqualTo(userConfig.GetText("all")));
        });
    }

    [Test]
    public void PrivateFieldChangeHandlers_UpdateCurrentValuesAndInvokeCallbacks()
    {
        RequestTaskMetadataEditor component = CreateComponent(new WfHandler(), new SimulatedUserConfig());
        WfTaskType changedTaskType = WfTaskType.group_create;
        Management changedManagement = new() { Id = 9, Name = "changed-management" };
        bool taskTypeCallbackCalled = false;
        bool managementCallbackCalled = false;
        SetMember(component, nameof(RequestTaskMetadataEditor.TaskTypeChanged), EventCallback.Factory.Create<WfTaskType>(
            new object(), (WfTaskType value) => { taskTypeCallbackCalled = value == changedTaskType; }));
        SetMember(component, nameof(RequestTaskMetadataEditor.ManagementChanged), EventCallback.Factory.Create<Management?>(
            new object(), (Management? value) => { managementCallbackCalled = value == changedManagement; }));

        InvokePrivateTask(component, "OnTaskTypeChanged", changedTaskType).GetAwaiter().GetResult();
        InvokePrivateTask(component, "OnManagementChanged", changedManagement).GetAwaiter().GetResult();
        InvokePrivateTask(component, "OnRuleDeviceChanged", new Device { Id = 4, Name = "changed-gateway" }).GetAwaiter().GetResult();
        InvokePrivateTask(component, "OnOwnerChanged", new FwoOwner { Id = 5, Name = "changed-owner" }).GetAwaiter().GetResult();
        InvokePrivateTask(component, "OnRequestingOwnerChanged", new FwoOwner { Id = 6, Name = "requesting-owner" }).GetAwaiter().GetResult();

        Assert.Multiple(() =>
        {
            Assert.That(component.CurrentTaskType, Is.EqualTo(changedTaskType));
            Assert.That(component.CurrentManagement, Is.EqualTo(changedManagement));
            Assert.That(component.CurrentRuleDevice?.Id, Is.EqualTo(4));
            Assert.That(component.CurrentOwner?.Id, Is.EqualTo(5));
            Assert.That(component.CurrentRequestingOwner?.Id, Is.EqualTo(6));
            Assert.That(taskTypeCallbackCalled, Is.True);
            Assert.That(managementCallbackCalled, Is.True);
        });
    }

    [Test]
    public void NeedsManualDeviceSelection_FollowsDeviceInputIndependentlyOfPlanningPhase()
    {
        RequestTaskMetadataEditor inactivePlanning = CreateComponent(new WfHandler
        {
            ActStateMatrix = new StateMatrix { PhaseActive = { [WorkflowPhases.planning] = false } }
        }, new SimulatedUserConfig { ReqDeviceInput = ReqDeviceInputOptions.enterInReqTask });
        RequestTaskMetadataEditor activePlanning = CreateComponent(new WfHandler
        {
            ActStateMatrix = new StateMatrix { PhaseActive = { [WorkflowPhases.planning] = true } }
        }, new SimulatedUserConfig { ReqDeviceInput = ReqDeviceInputOptions.oneTaskForAllDevices });

        Assert.Multiple(() =>
        {
            Assert.That(inactivePlanning.NeedsManualDeviceSelection, Is.True);
            Assert.That(activePlanning.NeedsManualDeviceSelection, Is.True);
            Assert.That(CreateComponent(new WfHandler(), new SimulatedUserConfig { ReqDeviceInput = ReqDeviceInputOptions.never }).NeedsManualDeviceSelection, Is.False);
        });
    }

    [Test]
    public void InitializeFromTask_LoadsMetadataAndDeviceSelection()
    {
        WfReqTask task = new()
        {
            Id = 12,
            TaskType = WfTaskType.new_interface.ToString(),
            ManagementId = 7,
            Elements = [],
            Owners = [new FwoOwnerDataHelper { Owner = new FwoOwner { Id = 21, Name = "Owner" } }]
        };
        task.SetAddInfo(AdditionalInfoKeys.GrpName, "group-name");
        task.SetDeviceList([2]);
        WfHandler handler = new()
        {
            ActReqTask = task,
            Devices = [new Device { Id = 1, Name = "gw-1" }, new Device { Id = 2, Name = "gw-2" }],
            AllOwners = [new FwoOwner { Id = 21, Name = "Owner" }]
        };
        RequestTaskMetadataEditor component = CreateComponent(handler,
            managements: [new Management { Id = 7, Name = "Management" }], taskType: WfTaskType.new_interface);

        component.InitializeFromTask();

        Assert.Multiple(() =>
        {
            Assert.That(component.CurrentTaskType, Is.EqualTo(WfTaskType.new_interface));
            Assert.That(component.CurrentManagement?.Id, Is.EqualTo(7));
            Assert.That(component.CurrentOwner?.Id, Is.EqualTo(21));
            Assert.That(component.CurrentSelectedDevices.Select(device => device.Id), Is.EqualTo(kSelectedGatewayId));
        });
    }

    [Test]
    public async Task RenderedGatewayDropdown_DoesNotOfferSelectedAllDeviceAgain()
    {
        WfReqTask task = new() { Id = 13, TaskType = WfTaskType.access.ToString() };
        task.SetDeviceList(kAllDeviceId.ToList());
        WfHandler handler = new()
        {
            ActReqTask = task,
            Devices = [new Device { Id = 1, Name = "gw-1" }],
            ActStateMatrix = new StateMatrix { PhaseActive = { [WorkflowPhases.planning] = false } }
        };
        SimulatedUserConfig userConfig = new()
        {
            ReqDeviceInput = ReqDeviceInputOptions.enterInReqTask
        };

        await using BunitContext context = new();
        context.Services.AddSingleton<ApiConnection>(new UiRequestWorkflowTest.RequestWorkflowApiConn());
        context.Services.AddSingleton<UserConfig>(userConfig);
        context.Services.AddSingleton<DomEventService>();

        IRenderedComponent<RequestTaskMetadataEditor> component = context.Render<RequestTaskMetadataEditor>(parameters => parameters
            .Add(parameter => parameter.WfHandler, handler)
            .Add(parameter => parameter.CanEditField, _ => true));
        IRenderedComponent<Dropdown<Device>> dropdown = component.FindComponent<Dropdown<Device>>();
        await component.InvokeAsync(() => dropdown.Find("input").Focus());

        Assert.Multiple(() =>
        {
            Assert.That(dropdown.FindAll("button[id^='dropdown-menu-selected-']").Count(button => button.TextContent.Contains(userConfig.GetText("all"))), Is.EqualTo(1));
            Assert.That(dropdown.FindAll("button[id^='dropdown-menu-element-']").Any(button => button.TextContent.Contains(userConfig.GetText("all"))), Is.False);
        });
    }

    [Test]
    public async Task OnFlowTimeObjectIdChanged_AttachesFlowObject()
    {
        WfReqTask task = new() { Id = 20, TaskType = WfTaskType.access.ToString() };
        WfHandler handler = new() { ActReqTask = task, ActTicket = new WfTicket { Tasks = [task] } };
        RequestTaskMetadataEditor component = CreateComponent(handler, new SimulatedUserConfig());

        await InvokePrivateTask(component, "OnFlowTimeObjectIdChanged", 42L);

        WfReqElement? timeElement = TimeObjectElementHelper.Find(task.Elements);
        Assert.Multiple(() =>
        {
            Assert.That(timeElement?.FlowTimeObjectId, Is.EqualTo(42));
            Assert.That(timeElement?.ObjectTaskId, Is.Null);
        });
    }

    [Test]
    public async Task OnFlowTimeObjectIdChanged_AttachesTicketTimeObjectTask()
    {
        WfReqTask objectTask = new()
        {
            Id = 77,
            TaskType = WfTaskType.object_create.ToString(),
            Elements = [new WfReqElement { Field = ElemFieldType.time.ToString(), TimeObjId = -17, Name = "ticket-time" }]
        };
        WfReqTask accessTask = new() { Id = 20, TaskType = WfTaskType.access.ToString() };
        WfTicket ticket = new() { Tasks = [accessTask, objectTask] };
        WfHandler handler = new() { ActReqTask = accessTask, ActTicket = ticket };
        RequestTaskMetadataEditor component = CreateComponent(handler, new SimulatedUserConfig());

        await InvokePrivateTask(component, "OnFlowTimeObjectIdChanged", -17L);

        Assert.That(TimeObjectElementHelper.Find(accessTask.Elements)?.ObjectTaskId, Is.EqualTo(77));
    }

    [Test]
    public async Task OnFlowTimeObjectIdChanged_ClearsElementDates()
    {
        WfReqElement oldElement = new()
        {
            Id = 15,
            Field = ElemFieldType.time.ToString(),
            FlowTimeObjectId = 42,
            StartTime = DateTime.UtcNow,
            EndTime = DateTime.UtcNow.AddHours(1)
        };
        WfReqTask task = new()
        {
            Id = 20,
            TaskType = WfTaskType.access.ToString(),
            TargetBeginDate = oldElement.StartTime,
            TargetEndDate = oldElement.EndTime,
            Elements = [oldElement]
        };
        WfHandler handler = new() { ActReqTask = task, ActTicket = new WfTicket { Tasks = [task] } };
        RequestTaskMetadataEditor component = CreateComponent(handler, new SimulatedUserConfig());

        await InvokePrivateTask(component, "OnFlowTimeObjectIdChanged", (long?)null);

        Assert.Multiple(() =>
        {
            Assert.That(TimeObjectElementHelper.Find(task.Elements), Is.Null);
            Assert.That(task.RemovedElements, Does.Contain(oldElement));
            Assert.That(task.TargetBeginDate, Is.Null);
            Assert.That(task.TargetEndDate, Is.Null);
        });
    }

    [Test]
    public async Task TargetDateAndTimeObjectCallbacks_UpdateTaskElementAndValidity()
    {
        WfReqElement timeElement = new() { Field = ElemFieldType.time.ToString() };
        WfReqTask task = new() { Id = 20, TaskType = WfTaskType.access.ToString(), Elements = [timeElement] };
        WfHandler handler = new() { ActReqTask = task, ActTicket = new WfTicket { Tasks = [task] } };
        RequestTaskMetadataEditor component = CreateComponent(handler, new SimulatedUserConfig());
        bool valid = false;
        SetMember(component, nameof(RequestTaskMetadataEditor.TargetDatesValidChanged),
            EventCallback.Factory.Create<bool>(new object(), value => valid = value));
        DateTime begin = new(2026, 8, 1, 8, 0, 0);
        DateTime end = new(2026, 8, 1, 17, 0, 0);
        FlowTimeObject selected = new() { StartTime = begin, EndTime = end, Name = "selected" };

        await InvokePrivateTask(component, "OnTargetBeginDateChanged", begin);
        await InvokePrivateTask(component, "OnTargetEndDateChanged", end);
        await InvokePrivateTask(component, "OnFlowTimeObjectSelected", selected);
        await InvokePrivateTask(component, "OnFlowTimeObjectNameChanged", "renamed");

        Assert.Multiple(() =>
        {
            Assert.That(task.TargetBeginDate, Is.EqualTo(begin));
            Assert.That(task.TargetEndDate, Is.EqualTo(end));
            Assert.That(timeElement.StartTime, Is.EqualTo(begin));
            Assert.That(timeElement.EndTime, Is.EqualTo(end));
            Assert.That(timeElement.Name, Is.EqualTo("renamed"));
            Assert.That(valid, Is.True);
        });
    }

    [Test]
    public void SelectedFlowTimeObjectNameAndTicketTimeObjects_UseTaskTimeElements()
    {
        DateTime begin = new(2026, 8, 1, 8, 0, 0);
        DateTime end = new(2026, 8, 1, 17, 0, 0);
        WfReqTask accessTask = new()
        {
            Id = 20,
            TaskType = WfTaskType.access.ToString(),
            TargetBeginDate = begin,
            TargetEndDate = end
        };
        WfReqTask objectTask = new()
        {
            Id = 77,
            TaskType = WfTaskType.object_create.ToString(),
            Elements = [new WfReqElement
            {
                Field = ElemFieldType.time.ToString(),
                TimeObjId = -17,
                Name = "ticket-window",
                StartTime = begin,
                EndTime = end
            }]
        };
        RequestTaskMetadataEditor component = CreateComponent(
            new WfHandler { ActReqTask = accessTask, ActTicket = new WfTicket { Tasks = [accessTask, objectTask] } },
            new SimulatedUserConfig());

        PropertyInfo nameProperty = typeof(RequestTaskMetadataEditor).GetProperty("SelectedFlowTimeObjectName",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        PropertyInfo ticketObjectsProperty = typeof(RequestTaskMetadataEditor).GetProperty("TicketTimeObjects",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        IReadOnlyList<FlowTimeObject> ticketObjects = (IReadOnlyList<FlowTimeObject>)ticketObjectsProperty.GetValue(component)!;

        Assert.Multiple(() =>
        {
            Assert.That(nameProperty.GetValue(component), Is.EqualTo("ticket-window"));
            Assert.That(ticketObjects.Select(timeObject => timeObject.Id), Is.EquivalentTo([-17L]));
            Assert.That(ticketObjects.Single().Name, Is.EqualTo("ticket-window"));
        });
    }

    private static RequestTaskMetadataEditor CreateComponent(WfHandler handler, SimulatedUserConfig? userConfig = null,
        IEnumerable<Management>? managements = null, WfTaskType? taskType = null)
    {
        RequestTaskMetadataEditor component = new();
        SetMember(component, nameof(RequestTaskMetadataEditor.WfHandler), handler);
        if (taskType.HasValue)
        {
            SetMember(component, nameof(RequestTaskMetadataEditor.TaskType), taskType.Value);
        }
        SetMember(component, "userConfig", userConfig ?? new SimulatedUserConfig());
        SetMember(component, nameof(RequestTaskMetadataEditor.Managements), managements ?? []);
        return component;
    }

    private static object? InvokePrivate(object instance, string methodName, params object?[] args)
    {
        MethodInfo method = instance.GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new MissingMethodException(instance.GetType().FullName, methodName);
        return method.Invoke(instance, args);
    }

    private static Task InvokePrivateTask(object instance, string methodName, params object?[] args)
    {
        return (Task)(InvokePrivate(instance, methodName, args)
            ?? throw new InvalidOperationException($"Method '{methodName}' returned null."));
    }

    private static string ReadNewInterfaceBranch()
    {
        string path = LocateRepositoryFile(Path.Combine(
            "roles", "ui", "files", "FWO.UI", "Pages", "Request", "RequestTaskMetadataEditor.razor"));
        string source = File.ReadAllText(path);
        const string startMarker = "else if (TaskType == WfTaskType.new_interface)";
        const string endMarker = "@code";
        int start = source.IndexOf(startMarker, StringComparison.Ordinal);
        int end = source.IndexOf(endMarker, start, StringComparison.Ordinal);
        Assert.That(start, Is.GreaterThanOrEqualTo(0));
        Assert.That(end, Is.GreaterThan(start));
        return source[start..end];
    }

    private static int CountOccurrences(string value, string searchValue)
    {
        int count = 0;
        int offset = 0;
        while ((offset = value.IndexOf(searchValue, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += searchValue.Length;
        }
        return count;
    }

    private static void SetMember(object instance, string memberName, object? value)
    {
        PropertyInfo? property = instance.GetType().GetProperty(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (property != null)
        {
            property.SetValue(instance, value);
            return;
        }
        FieldInfo? field = instance.GetType().GetField(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (field != null)
        {
            field.SetValue(instance, value);
            return;
        }
        throw new MissingMemberException(instance.GetType().FullName, memberName);
    }

    private static string LocateRepositoryFile(string relativePath)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory != null)
        {
            foreach (string candidatePath in GetRepositoryRelativePaths(relativePath))
            {
                string candidate = Path.Combine(directory.FullName, candidatePath);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
            directory = directory.Parent;
        }
        throw new FileNotFoundException($"Could not locate repository file '{relativePath}'.");
    }

    private static IEnumerable<string> GetRepositoryRelativePaths(string relativePath)
    {
        yield return relativePath;
        yield return relativePath.Replace(Path.Combine("roles", "ui"), "ui", StringComparison.OrdinalIgnoreCase);
    }
}
