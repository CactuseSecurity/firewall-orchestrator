using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Workflow;
using FWO.Services.Workflow;
using FWO.Ui.Pages.Request;
using NUnit.Framework;
using System.Reflection;

namespace FWO.Test;

[TestFixture]
internal class UiRequestTaskMetadataEditorTest
{
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
        Assert.That(component.CurrentSelectedDevices.Select(device => device.Id), Is.EqualTo(new[] { WfReqTaskBase.kAllDevicesId }));

        component.SetDevice(new Device { Id = WfReqTaskBase.kAllDevicesId });
        Assert.That(component.DisplayDevices(), Is.EqualTo(userConfig.GetText("all")));

        component.SetDevices([new Device { Id = WfReqTaskBase.kAllDevicesId }, gatewayOne]);
        Assert.That(component.CurrentSelectedDevices.Select(device => device.Id), Is.EqualTo(new[] { 1 }));

        component.SetDevices([gatewayOne, gatewayTwo]);
        Assert.That(component.CurrentSelectedDevices.Select(device => device.Id), Is.EqualTo(new[] { 1, 2 }));
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
            Assert.That(component.CurrentSelectedDevices.Select(device => device.Id), Is.EqualTo(new[] { 2 }));
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
        DirectoryInfo? directory = new(NUnit.Framework.TestContext.CurrentContext.TestDirectory);
        while (directory != null)
        {
            string candidate = Path.Combine(directory.FullName, relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }
            directory = directory.Parent;
        }
        throw new FileNotFoundException($"Could not locate repository file '{relativePath}'.");
    }
}
