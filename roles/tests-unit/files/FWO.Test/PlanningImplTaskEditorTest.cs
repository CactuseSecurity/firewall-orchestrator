using FWO.Data;
using FWO.Data.Workflow;
using FWO.Services.Workflow;
using FWO.Ui.Services;
using NUnit.Framework;

namespace FWO.Test;

[TestFixture]
internal sealed class PlanningImplTaskEditorTest
{
    private static readonly Management kManagementA = new() { Id = 4, Name = "Management A" };
    private static readonly Management kManagementB = new() { Id = 5, Name = "Management B" };

    private WfHandler handler = new();
    private bool canEdit;
    private int saveCount;
    private int refreshCount;

    [SetUp]
    public void SetUp()
    {
        handler = UiRequestWorkflowTest.CreateWorkflowHandler(WorkflowPhases.planning, WfTaskType.access.ToString(), new WfTicket { Id = 7 });
        handler.Managements.Add(kManagementA);
        handler.Managements.Add(kManagementB);
        handler.Devices.Add(new Device { Id = 8, Name = "Firewall A", Management = kManagementA });
        handler.Devices.Add(new Device { Id = 9, Name = "Firewall B", Management = kManagementB });
        canEdit = true;
        saveCount = 0;
        refreshCount = 0;
    }

    private PlanningImplTaskEditor CreateEditor() => new(new PlanningImplTaskEditorCallbacks
    {
        WfHandler = () => handler,
        DisplayLogic = () => new PlanningImplTaskDisplayLogic(handler, new SimulatedUserConfig(), [], [], []),
        CanEdit = (_, _) => canEdit,
        Save = () =>
        {
            saveCount++;
            return Task.CompletedTask;
        },
        Refresh = () =>
        {
            refreshCount++;
            return Task.CompletedTask;
        }
    });

    private static WfImplTask ImplementationTask() => new()
    {
        Id = 21,
        TaskType = WfTaskType.access.ToString(),
        ManagementId = kManagementA.Id,
        DeviceId = 8,
        FreeText = "text",
        ImplElements = [new WfImplElement { Field = ElemFieldType.source.ToString(), Name = "host", ImplAction = "create" }]
    };

    [Test]
    public async Task Open_PreselectsCurrentValuesAndRendersTheHost()
    {
        PlanningImplTaskEditor editor = CreateEditor();
        WfReqTask requestTask = new() { Id = 11 };
        WfImplTask implementationTask = ImplementationTask();

        await editor.OpenManagement(implementationTask, requestTask);
        await editor.OpenDevice(implementationTask, requestTask);
        await editor.OpenFreeText(implementationTask, requestTask);
        await editor.OpenName(implementationTask, requestTask);

        Assert.Multiple(() =>
        {
            Assert.That(editor.DisplayManagementEditor && editor.DisplayDeviceEditor && editor.DisplayFreeTextEditor && editor.DisplayNameEditor, Is.True);
            Assert.That(editor.SelectedManagement, Is.SameAs(kManagementA));
            Assert.That(editor.SelectedDevice?.Id, Is.EqualTo(8));
            Assert.That(editor.FreeText, Is.EqualTo("text"));
            Assert.That(editor.Name, Is.EqualTo("host"));
            Assert.That(editor.RequestTask, Is.SameAs(requestTask));
            Assert.That(editor.Managements, Has.Count.EqualTo(2));
            Assert.That(refreshCount, Is.EqualTo(4));
        });
    }

    [Test]
    public async Task Open_DoesNothingIfTheTaskMayNotBeChanged()
    {
        canEdit = false;
        PlanningImplTaskEditor editor = CreateEditor();

        await editor.OpenManagement(ImplementationTask(), new WfReqTask());
        await editor.OpenDevice(ImplementationTask(), new WfReqTask());
        await editor.OpenFreeText(ImplementationTask(), new WfReqTask());
        await editor.OpenName(ImplementationTask(), new WfReqTask());

        Assert.Multiple(() =>
        {
            Assert.That(editor.DisplayManagementEditor || editor.DisplayDeviceEditor || editor.DisplayFreeTextEditor || editor.DisplayNameEditor, Is.False);
            Assert.That(refreshCount, Is.Zero);
            Assert.That(handler.ActImplTask.Id, Is.Zero);
        });
    }

    [Test]
    public async Task Save_RequiresAnOpenedEditor()
    {
        PlanningImplTaskEditor editor = CreateEditor();

        await editor.SaveFreeText("changed");

        Assert.That(saveCount, Is.Zero);
    }

    [Test]
    public async Task Save_AppliesValuesToTheSelectedTaskAndSavesIt()
    {
        PlanningImplTaskEditor editor = CreateEditor();
        await editor.OpenFreeText(ImplementationTask(), new WfReqTask { Id = 11 });

        await editor.SaveFreeText("changed");
        await editor.SaveName("renamed");
        await editor.SaveManagement(kManagementB);

        Assert.Multiple(() =>
        {
            Assert.That(handler.ActImplTask.FreeText, Is.EqualTo("changed"));
            Assert.That(handler.ActImplTask.ImplElements.Single().Name, Is.EqualTo("renamed"));
            Assert.That(handler.ActImplTask.ManagementId, Is.EqualTo(kManagementB.Id));
            Assert.That(handler.ActImplTask.DeviceId, Is.Null);
            Assert.That(saveCount, Is.EqualTo(3));
            Assert.That(editor.AvailableDevices().Select(device => device.Id), Is.EqualTo(new List<int> { 9 }));
        });
    }

    [Test]
    public async Task SaveDevice_RejectsDeviceOfAnotherManagement()
    {
        PlanningImplTaskEditor editor = CreateEditor();
        await editor.OpenDevice(ImplementationTask(), new WfReqTask { Id = 11 });

        await editor.SaveDevice(handler.Devices.Single(device => device.Id == 9));

        Assert.Multiple(() =>
        {
            Assert.That(handler.ActImplTask.DeviceId, Is.EqualTo(8));
            Assert.That(saveCount, Is.Zero);
        });
    }

    [Test]
    public async Task Save_IsSkippedIfTheTaskCanNoLongerBeChanged()
    {
        PlanningImplTaskEditor editor = CreateEditor();
        await editor.OpenFreeText(ImplementationTask(), new WfReqTask { Id = 11 });
        canEdit = false;

        await editor.SaveFreeText("changed");

        Assert.That(saveCount, Is.Zero);
    }
}
