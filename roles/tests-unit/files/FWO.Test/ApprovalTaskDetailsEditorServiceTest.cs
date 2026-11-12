using Bunit;
using AngleSharp.Dom;
using FWO.Api.Client;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Flow;
using FWO.Data.DeviceSpecificSettings;
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

namespace FWO.Test;

[TestFixture]
internal sealed class ApprovalTaskDetailsEditorServiceTest
{
    [Test]
    public static void ApprovalTaskDetailsEditorService_AllowsOnlyOwnerChangesForPlannerInReadOnlyMode()
    {
        WfReqTask task = new() { Id = 11, StateId = 2, TaskType = WfTaskType.access.ToString() };
        WfHandler handler = UiRequestWorkflowTest.CreateWorkflowHandler(WorkflowPhases.planning, task.TaskType, new WfTicket { Tasks = [task] });
        ApprovalTaskDetailsEditorService service = new();
        service.Configure(handler, new SimulatedUserConfig(), DefaultInit.DoNothing);
        service.ReadOnlyDisplay = true;
        service.PlannerOwnerChangesAllowed = true;
        ApprovalTaskViewContext viewContext = service.CreateTaskViewContext();

        List<WfReqTask> tasks = [task];
        Assert.Multiple(() =>
        {
            Assert.That(service.CanEdit(tasks, WorkflowEditableFieldKeys.Owner), Is.True);
            Assert.That(service.CanEdit(tasks, WorkflowEditableFieldKeys.Title), Is.False);
            Assert.That(service.CanEdit(tasks, WorkflowEditableFieldKeys.Source), Is.False);
            Assert.That(viewContext.CanEditTimeObject(tasks), Is.False);
        });
    }

    [Test]
    public static void ApprovalTaskDetailsEditorService_DoesNotAllowPlannerOwnerChangesOutsidePlanning()
    {
        WfReqTask task = new() { Id = 11, StateId = 2, TaskType = WfTaskType.access.ToString() };
        WfHandler handler = UiRequestWorkflowTest.CreateWorkflowHandler(WorkflowPhases.approval, task.TaskType, new WfTicket { Tasks = [task] });
        ApprovalTaskDetailsEditorService service = new();
        service.Configure(handler, new SimulatedUserConfig(), DefaultInit.DoNothing);
        service.ReadOnlyDisplay = true;
        service.PlannerOwnerChangesAllowed = true;

        Assert.That(service.CanEdit(new List<WfReqTask> { task }, WorkflowEditableFieldKeys.Owner), Is.False);
    }

    [Test]
    public static void ApprovalTaskDetailsEditorService_RejectsEmptyAndNonActionableTasks()
    {
        WfReqTask task = new() { Id = 11, StateId = 10, TaskType = WfTaskType.access.ToString() };
        WfHandler handler = UiRequestWorkflowTest.CreateWorkflowHandler(WorkflowPhases.planning, task.TaskType, new WfTicket { Tasks = [task] });
        ApprovalTaskDetailsEditorService service = new();
        service.Configure(handler, new SimulatedUserConfig(), DefaultInit.DoNothing);
        List<WfReqTask> noTasks = [];
        List<WfReqTask> nonActionableTasks = [task];

        Assert.Multiple(() =>
        {
            Assert.That(service.CanEdit(noTasks, WorkflowEditableFieldKeys.Title), Is.False);
            Assert.That(service.CanEdit(nonActionableTasks, WorkflowEditableFieldKeys.Title), Is.False);
        });
    }

}
