using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Workflow;
using FWO.Api.Client;
using FWO.Services.Workflow;
using FWO.Ui.Pages.Request;
using FWO.Ui.Services;
using FWO.Ui.Shared;
using Bunit;
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
    public void NeedsManualDeviceSelection_ReflectsPlanningPhaseAndConfiguration()
    {
        RequestTaskMetadataEditor inactivePlanning = CreateComponent(new WfHandler
        {
            ActStateMatrix = new StateMatrix { PhaseActive = { [WorkflowPhases.planning] = false } }
        }, new SimulatedUserConfig { ReqAutoCreateImplTasks = AutoCreateImplTaskOptions.enterInReqTask });
        RequestTaskMetadataEditor activePlanning = CreateComponent(new WfHandler
        {
            ActStateMatrix = new StateMatrix { PhaseActive = { [WorkflowPhases.planning] = true } }
        }, new SimulatedUserConfig { ReqAutoCreateImplTasks = AutoCreateImplTaskOptions.oneTaskForAllDevices });

        Assert.Multiple(() =>
        {
            Assert.That(inactivePlanning.NeedsManualDeviceSelection, Is.True);
            Assert.That(activePlanning.NeedsManualDeviceSelection, Is.False);
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
            managements: [new Management { Id = 7, Name = "Management" }]);

        component.InitializeFromTask();

        Assert.Multiple(() =>
        {
            Assert.That(component.CurrentTaskType, Is.EqualTo(WfTaskType.new_interface));
            Assert.That(component.CurrentManagement?.Id, Is.EqualTo(7));
            Assert.That(component.CurrentOwner?.Id, Is.EqualTo(21));
            Assert.That(component.CurrentGroupName, Is.EqualTo("group-name"));
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
            ReqAutoCreateImplTasks = AutoCreateImplTaskOptions.enterInReqTask
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

    private static RequestTaskMetadataEditor CreateComponent(WfHandler handler, SimulatedUserConfig? userConfig = null,
        IEnumerable<Management>? managements = null)
    {
        RequestTaskMetadataEditor component = new();
        SetMember(component, nameof(RequestTaskMetadataEditor.WfHandler), handler);
        SetMember(component, "userConfig", userConfig ?? new SimulatedUserConfig());
        SetMember(component, nameof(RequestTaskMetadataEditor.Managements), managements ?? []);
        return component;
    }

    private static string ReadNewInterfaceBranch()
    {
        string path = LocateRepositoryFile(Path.Combine(
            "roles", "ui", "files", "FWO.UI", "Pages", "Request", "RequestTaskMetadataEditor.razor"));
        string source = File.ReadAllText(path);
        const string startMarker = "else if (TaskType == WfTaskType.new_interface)";
        const string endMarker = "else if (TaskType == WfTaskType.group_create)";
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
