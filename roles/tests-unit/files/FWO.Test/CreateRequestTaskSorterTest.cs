using FWO.Data.Workflow;
using FWO.Middleware.Server.Services;
using NUnit.Framework;

namespace FWO.Test;

[TestFixture]
internal class CreateRequestTaskSorterTest
{
    [Test]
    public void OrderForSave_KeepsOriginalOrderWhenSortingIsDisabled()
    {
        List<WfReqTask> tasks =
        [
            BuildTask("access", WfTaskType.access.ToString()),
            BuildTask("group-delete", WfTaskType.group_delete.ToString()),
            BuildTask("group-create", WfTaskType.group_create.ToString())
        ];

        List<WfReqTask> orderedTasks = CreateRequestTaskSorter.OrderForSave(tasks, false);

        Assert.Multiple(() =>
        {
            Assert.That(orderedTasks.Select(task => task.Title), Is.EqualTo(tasks.Select(task => task.Title)));
            Assert.That(orderedTasks.Select(task => task.TaskNumber), Is.EqualTo(tasks.Select(task => task.TaskNumber)));
        });
    }

    [Test]
    public void OrderForSave_KeepsMixedGroupModifyTaskWhenSortingIsDisabled()
    {
        WfReqTask groupModifyMixed = BuildTask("group-modify", WfTaskType.group_modify.ToString(),
            new WfReqElement { RequestAction = RequestAction.create.ToString() },
            new WfReqElement { RequestAction = RequestAction.delete.ToString() });
        WfReqTask access = BuildTask("access", WfTaskType.access.ToString());

        List<WfReqTask> orderedTasks = CreateRequestTaskSorter.OrderForSave([groupModifyMixed, access], false);

        Assert.Multiple(() =>
        {
            Assert.That(orderedTasks, Has.Count.EqualTo(2));
            Assert.That(orderedTasks[0].TaskType, Is.EqualTo(WfTaskType.group_modify.ToString()));
            Assert.That(orderedTasks[0].Elements, Has.Count.EqualTo(2));
            Assert.That(orderedTasks[0].Elements[0].RequestAction, Is.EqualTo(RequestAction.create.ToString()));
            Assert.That(orderedTasks[0].Elements[1].RequestAction, Is.EqualTo(RequestAction.delete.ToString()));
            Assert.That(orderedTasks[1].TaskType, Is.EqualTo(WfTaskType.access.ToString()));
            Assert.That(orderedTasks.Select(task => task.TaskNumber), Is.EqualTo([99, 99]));
        });
    }

    [Test]
    public void OrderForSave_SortsAndSplitsGroupModifyTasks()
    {
        WfReqTask groupCreate = BuildTask("group-create", WfTaskType.group_create.ToString(), new WfReqElement
        {
            RequestAction = RequestAction.create.ToString()
        });
        WfReqTask groupModifyMixed = BuildTask("group-modify", WfTaskType.group_modify.ToString(),
            new WfReqElement { RequestAction = RequestAction.create.ToString() },
            new WfReqElement { RequestAction = RequestAction.delete.ToString() });
        WfReqTask access = BuildTask("access", WfTaskType.access.ToString());
        WfReqTask groupDelete = BuildTask("group-delete", WfTaskType.group_delete.ToString(),
            new WfReqElement { RequestAction = RequestAction.delete.ToString() });

        List<WfReqTask> orderedTasks = CreateRequestTaskSorter.OrderForSave([access, groupDelete, groupModifyMixed, groupCreate], true);

        Assert.Multiple(() =>
        {
            Assert.That(orderedTasks, Has.Count.EqualTo(5));
            Assert.That(orderedTasks[0].Title, Is.EqualTo("group-create"));
            Assert.That(orderedTasks[0].TaskType, Is.EqualTo(WfTaskType.group_create.ToString()));
            Assert.That(orderedTasks[1].Title, Is.EqualTo("group-modify"));
            Assert.That(orderedTasks[1].TaskType, Is.EqualTo(WfTaskType.group_modify.ToString()));
            Assert.That(orderedTasks[1].Elements, Has.Count.EqualTo(1));
            Assert.That(orderedTasks[1].Elements[0].RequestAction, Is.EqualTo(RequestAction.create.ToString()));
            Assert.That(orderedTasks[2].Title, Is.EqualTo("access"));
            Assert.That(orderedTasks[2].TaskType, Is.EqualTo(WfTaskType.access.ToString()));
            Assert.That(orderedTasks[3].Title, Is.EqualTo("group-modify"));
            Assert.That(orderedTasks[3].TaskType, Is.EqualTo(WfTaskType.group_modify.ToString()));
            Assert.That(orderedTasks[3].Elements, Has.Count.EqualTo(1));
            Assert.That(orderedTasks[3].Elements[0].RequestAction, Is.EqualTo(RequestAction.delete.ToString()));
            Assert.That(orderedTasks[4].Title, Is.EqualTo("group-delete"));
            Assert.That(orderedTasks[4].TaskType, Is.EqualTo(WfTaskType.group_delete.ToString()));
            Assert.That(orderedTasks.Select(task => task.TaskNumber), Is.EqualTo([1, 2, 3, 4, 5]));
        });
    }

    [Test]
    public void OrderForSave_SplitsMixedGroupModifyTaskIntoAddAndRemoveTasks()
    {
        WfReqTask groupModifyMixed = BuildTask("group-modify", WfTaskType.group_modify.ToString(),
            new WfReqElement { RequestAction = RequestAction.create.ToString() },
            new WfReqElement { RequestAction = RequestAction.delete.ToString() });

        List<WfReqTask> orderedTasks = CreateRequestTaskSorter.OrderForSave([groupModifyMixed], true);

        Assert.Multiple(() =>
        {
            Assert.That(orderedTasks, Has.Count.EqualTo(2));
            Assert.That(orderedTasks[0].Title, Is.EqualTo("group-modify"));
            Assert.That(orderedTasks[0].TaskType, Is.EqualTo(WfTaskType.group_modify.ToString()));
            Assert.That(orderedTasks[0].Elements, Has.Count.EqualTo(1));
            Assert.That(orderedTasks[0].Elements[0].RequestAction, Is.EqualTo(RequestAction.create.ToString()));
            Assert.That(orderedTasks[1].Title, Is.EqualTo("group-modify"));
            Assert.That(orderedTasks[1].TaskType, Is.EqualTo(WfTaskType.group_modify.ToString()));
            Assert.That(orderedTasks[1].Elements, Has.Count.EqualTo(1));
            Assert.That(orderedTasks[1].Elements[0].RequestAction, Is.EqualTo(RequestAction.delete.ToString()));
            Assert.That(orderedTasks.Select(task => task.TaskNumber), Is.EqualTo([1, 2]));
        });
    }

    [Test]
    public void OrderForSave_SplitsMixedGroupModifyTaskAndKeepsRetainedMembers()
    {
        WfReqTask groupModifyMixed = BuildTask("group-modify", WfTaskType.group_modify.ToString(),
            new WfReqElement { RequestAction = RequestAction.create.ToString(), Name = "added" },
            new WfReqElement { RequestAction = RequestAction.unchanged.ToString(), Name = "current-unchanged" },
            new WfReqElement { RequestAction = RequestAction.modify.ToString(), Name = "current-modify" },
            new WfReqElement { RequestAction = RequestAction.delete.ToString(), Name = "removed" });
        groupModifyMixed.Id = 42;
        groupModifyMixed.TicketId = 84;
        groupModifyMixed.FlowAccessId = 126;
        groupModifyMixed.Approvals = [new WfApproval
        {
            Id = 7,
            Comments = [new WfCommentDataHelper(new WfComment { CommentText = "approval-comment" })]
        }];
        groupModifyMixed.RemovedElements = [new WfReqElement { RequestAction = RequestAction.delete.ToString(), Name = "removed-original" }];

        List<WfReqTask> orderedTasks = CreateRequestTaskSorter.OrderForSave([groupModifyMixed], true);

        Assert.Multiple(() =>
        {
            Assert.That(orderedTasks, Has.Count.EqualTo(2));
            Assert.That(orderedTasks[0].Id, Is.EqualTo(0));
            Assert.That(orderedTasks[0].TicketId, Is.EqualTo(0));
            Assert.That(orderedTasks[0].FlowAccessId, Is.Null);
            Assert.That(orderedTasks[0].Elements.Select(element => element.RequestAction), Is.EqualTo([RequestAction.create.ToString(), RequestAction.unchanged.ToString(), RequestAction.modify.ToString()]));
            Assert.That(orderedTasks[1].Elements.Select(element => element.RequestAction), Is.EqualTo([RequestAction.unchanged.ToString(), RequestAction.modify.ToString(), RequestAction.delete.ToString()]));
            Assert.That(object.ReferenceEquals(orderedTasks[0].Elements[1], orderedTasks[1].Elements[0]), Is.False);
            Assert.That(object.ReferenceEquals(orderedTasks[0].Approvals[0], orderedTasks[1].Approvals[0]), Is.False);
            Assert.That(object.ReferenceEquals(orderedTasks[0].Approvals[0].Comments[0], orderedTasks[1].Approvals[0].Comments[0]), Is.False);
            Assert.That(object.ReferenceEquals(orderedTasks[0].RemovedElements[0], orderedTasks[1].RemovedElements[0]), Is.False);
        });
    }

    [Test]
    public void OrderForSave_SplitsMixedGroupModifyTaskAndKeepsPriorityWithOtherTaskTypes()
    {
        WfReqTask groupDelete = BuildTask("group-delete", WfTaskType.group_delete.ToString(),
            new WfReqElement { RequestAction = RequestAction.delete.ToString() });
        WfReqTask groupModifyMixed = BuildTask("group-modify", WfTaskType.group_modify.ToString(),
            new WfReqElement { RequestAction = RequestAction.create.ToString() },
            new WfReqElement { RequestAction = RequestAction.delete.ToString() });
        WfReqTask access = BuildTask("access", WfTaskType.access.ToString());
        WfReqTask groupCreate = BuildTask("group-create", WfTaskType.group_create.ToString(),
            new WfReqElement { RequestAction = RequestAction.create.ToString() });

        List<WfReqTask> orderedTasks = CreateRequestTaskSorter.OrderForSave([groupDelete, groupModifyMixed, access, groupCreate], true);

        Assert.Multiple(() =>
        {
            Assert.That(orderedTasks, Has.Count.EqualTo(5));
            Assert.That(orderedTasks[0].TaskType, Is.EqualTo(WfTaskType.group_create.ToString()));
            Assert.That(orderedTasks[1].TaskType, Is.EqualTo(WfTaskType.group_modify.ToString()));
            Assert.That(orderedTasks[1].Elements[0].RequestAction, Is.EqualTo(RequestAction.create.ToString()));
            Assert.That(orderedTasks[2].TaskType, Is.EqualTo(WfTaskType.access.ToString()));
            Assert.That(orderedTasks[3].TaskType, Is.EqualTo(WfTaskType.group_modify.ToString()));
            Assert.That(orderedTasks[3].Elements[0].RequestAction, Is.EqualTo(RequestAction.delete.ToString()));
            Assert.That(orderedTasks[4].TaskType, Is.EqualTo(WfTaskType.group_delete.ToString()));
        });
    }

    [Test]
    public void OrderForSave_UsesAddPriorityForGroupModifyTasksWithOnlyAddElements()
    {
        WfReqTask groupModifyAddOnly = BuildTask("group-modify", WfTaskType.group_modify.ToString(),
            new WfReqElement { RequestAction = RequestAction.create.ToString() });
        WfReqTask access = BuildTask("access", WfTaskType.access.ToString());

        List<WfReqTask> orderedTasks = CreateRequestTaskSorter.OrderForSave([access, groupModifyAddOnly], true);

        Assert.Multiple(() =>
        {
            Assert.That(orderedTasks, Has.Count.EqualTo(2));
            Assert.That(orderedTasks[0].TaskType, Is.EqualTo(WfTaskType.group_modify.ToString()));
            Assert.That(orderedTasks[0].Elements, Has.Count.EqualTo(1));
            Assert.That(orderedTasks[0].Elements[0].RequestAction, Is.EqualTo(RequestAction.create.ToString()));
            Assert.That(orderedTasks[1].TaskType, Is.EqualTo(WfTaskType.access.ToString()));
            Assert.That(orderedTasks.Select(task => task.TaskNumber), Is.EqualTo([1, 2]));
        });
    }

    [Test]
    public void OrderForSave_UsesRemovePriorityForGroupModifyTasksWithOnlyRemoveElements()
    {
        WfReqTask groupModifyRemoveOnly = BuildTask("group-modify", WfTaskType.group_modify.ToString(),
            new WfReqElement { RequestAction = RequestAction.delete.ToString() });
        WfReqTask access = BuildTask("access", WfTaskType.access.ToString());

        List<WfReqTask> orderedTasks = CreateRequestTaskSorter.OrderForSave([access, groupModifyRemoveOnly], true);

        Assert.Multiple(() =>
        {
            Assert.That(orderedTasks, Has.Count.EqualTo(2));
            Assert.That(orderedTasks[0].TaskType, Is.EqualTo(WfTaskType.access.ToString()));
            Assert.That(orderedTasks[1].TaskType, Is.EqualTo(WfTaskType.group_modify.ToString()));
            Assert.That(orderedTasks[1].Elements, Has.Count.EqualTo(1));
            Assert.That(orderedTasks[1].Elements[0].RequestAction, Is.EqualTo(RequestAction.delete.ToString()));
            Assert.That(orderedTasks.Select(task => task.TaskNumber), Is.EqualTo([1, 2]));
        });
    }

    [Test]
    public void OrderForSave_UsesConfiguredPrioritiesWhenSortingIsEnabled()
    {
        CreateRequestTaskSortConfig sortConfig = new()
        {
            GroupCreatePriority = 10,
            GroupModifyAddPriority = 20,
            AccessPriority = 0,
            RuleModifyPriority = 30,
            RuleDeletePriority = 40,
            GroupModifyRemovePriority = 50,
            GroupDeletePriority = 60,
            AllowTaskSplit = true
        };

        WfReqTask groupCreate = BuildTask("group-create", WfTaskType.group_create.ToString(),
            new WfReqElement { RequestAction = RequestAction.create.ToString() });
        WfReqTask groupModifyAddOnly = BuildTask("group-modify-add", WfTaskType.group_modify.ToString(),
            new WfReqElement { RequestAction = RequestAction.create.ToString() });
        WfReqTask access = BuildTask("access", WfTaskType.access.ToString());
        WfReqTask ruleModify = BuildTask("rule-modify", WfTaskType.rule_modify.ToString());
        WfReqTask ruleDelete = BuildTask("rule-delete", WfTaskType.rule_delete.ToString());
        WfReqTask groupModifyRemoveOnly = BuildTask("group-modify-remove", WfTaskType.group_modify.ToString(),
            new WfReqElement { RequestAction = RequestAction.delete.ToString() });
        WfReqTask groupDelete = BuildTask("group-delete", WfTaskType.group_delete.ToString(),
            new WfReqElement { RequestAction = RequestAction.delete.ToString() });

        List<WfReqTask> orderedTasks = CreateRequestTaskSorter.OrderForSave([groupDelete, groupModifyRemoveOnly, ruleDelete, access, ruleModify, groupModifyAddOnly, groupCreate], true, sortConfig);

        Assert.Multiple(() =>
        {
            Assert.That(orderedTasks.Select(task => task.Title), Is.EqualTo(["access", "group-create", "group-modify-add", "rule-modify", "rule-delete", "group-modify-remove", "group-delete"]));
            Assert.That(orderedTasks.Select(task => task.TaskNumber), Is.EqualTo([1, 2, 3, 4, 5, 6, 7]));
        });
    }

    [Test]
    public void OrderForSave_PlacesUnknownTaskTypesLastEvenWithLargeConfiguredPriorities()
    {
        CreateRequestTaskSortConfig sortConfig = new()
        {
            GroupCreatePriority = 200,
            GroupModifyAddPriority = 300,
            AccessPriority = 100,
            RuleModifyPriority = 400,
            RuleDeletePriority = 500,
            GroupModifyRemovePriority = 600,
            GroupDeletePriority = 700
        };

        WfReqTask access = BuildTask("access", WfTaskType.access.ToString());
        WfReqTask generic = BuildTask("generic", WfTaskType.generic.ToString());
        WfReqTask groupCreate = BuildTask("group-create", WfTaskType.group_create.ToString());

        List<WfReqTask> orderedTasks = CreateRequestTaskSorter.OrderForSave([generic, groupCreate, access], true, sortConfig);

        Assert.Multiple(() =>
        {
            Assert.That(orderedTasks.Select(task => task.Title), Is.EqualTo(["access", "group-create", "generic"]));
            Assert.That(orderedTasks[2].TaskType, Is.EqualTo(WfTaskType.generic.ToString()));
        });
    }

    [Test]
    public void OrderForSave_DoesNotSplitMixedGroupModifyTaskWhenSplitIsDisabled()
    {
        CreateRequestTaskSortConfig sortConfig = new()
        {
            AllowTaskSplit = false
        };

        WfReqTask groupModifyMixed = BuildTask("group-modify", WfTaskType.group_modify.ToString(),
            new WfReqElement { RequestAction = RequestAction.create.ToString() },
            new WfReqElement { RequestAction = RequestAction.delete.ToString() });
        WfReqTask access = BuildTask("access", WfTaskType.access.ToString());

        List<WfReqTask> orderedTasks = CreateRequestTaskSorter.OrderForSave([access, groupModifyMixed], true, sortConfig);

        Assert.Multiple(() =>
        {
            Assert.That(orderedTasks, Has.Count.EqualTo(2));
            Assert.That(orderedTasks[0].Title, Is.EqualTo("group-modify"));
            Assert.That(orderedTasks[0].TaskType, Is.EqualTo(WfTaskType.group_modify.ToString()));
            Assert.That(orderedTasks[0].Elements, Has.Count.EqualTo(2));
            Assert.That(orderedTasks[0].Elements[0].RequestAction, Is.EqualTo(RequestAction.create.ToString()));
            Assert.That(orderedTasks[0].Elements[1].RequestAction, Is.EqualTo(RequestAction.delete.ToString()));
            Assert.That(orderedTasks[1].Title, Is.EqualTo("access"));
            Assert.That(orderedTasks.Select(task => task.TaskNumber), Is.EqualTo([1, 2]));
        });
    }

    private static WfReqTask BuildTask(string title, string taskType, params WfReqElement[] elements)
    {
        return new WfReqTask
        {
            Title = title,
            TaskType = taskType,
            TaskNumber = 99,
            Elements = [.. elements]
        };
    }
}
