using FWO.Data;
using FWO.Data.Workflow;
using FWO.Services.Workflow;
using FWO.Ui.Services;
using NUnit.Framework;

namespace FWO.Test;

[TestFixture]
internal sealed class PlanningImplTaskDisplayLogicTest
{
    private static readonly List<string> kMemberIps = ["1.1.1.1", "1.1.1.2", "1.1.1.3", "1.1.1.4", "1.1.1.5", "1.1.1.6"];

    private static PlanningImplTaskDisplayLogic CreateLogic(WfHandler? handler = null, List<RuleAction>? ruleActions = null,
        List<Tracking>? trackings = null)
    {
        return new PlanningImplTaskDisplayLogic(handler ?? new WfHandler(), new SimulatedUserConfig(), ruleActions ?? [],
            trackings ?? [], [new IpProtocol { Id = 6, Name = "tcp" }]);
    }

    private static WfImplElement Source(string name, string? ip = null, string action = "create") =>
        new() { Field = ElemFieldType.source.ToString(), Name = name, IpString = ip, ImplAction = action };

    [Test]
    public void ElementsForDisplay_ShowsOnlyRequestedElementOfObjectTask()
    {
        WfImplTask task = new()
        {
            TaskType = WfTaskType.object_create.ToString(),
            ImplElements =
            [
                Source("existing", action: RequestAction.unchanged.ToString()),
                Source("created"),
                Source("second-created")
            ]
        };
        WfImplTask unmarkedTask = new()
        {
            TaskType = WfTaskType.object_create.ToString(),
            ImplElements = [Source("first", action: RequestAction.unchanged.ToString()), Source("second", action: RequestAction.unchanged.ToString())]
        };
        WfImplTask accessTask = new() { TaskType = WfTaskType.access.ToString(), ImplElements = [Source("a"), Source("b")] };

        Assert.Multiple(() =>
        {
            Assert.That(PlanningImplTaskDisplayLogic.ElementsForDisplay(task, ElemFieldType.source).Select(element => element.Name),
                Is.EqualTo(new List<string> { "created" }));
            Assert.That(PlanningImplTaskDisplayLogic.ElementsForDisplay(unmarkedTask, ElemFieldType.source).Select(element => element.Name),
                Is.EqualTo(new List<string> { "first" }));
            Assert.That(PlanningImplTaskDisplayLogic.ElementsForDisplay(accessTask, ElemFieldType.source).Count(), Is.EqualTo(2));
        });
    }

    [Test]
    public void TaskTypePredicates_ClassifyImplementationTasks()
    {
        WfImplTask groupTask = new() { TaskType = WfTaskType.group_create.ToString() };
        WfImplTask timeTask = new()
        {
            TaskType = WfTaskType.object_create.ToString(),
            ImplElements = [new WfImplElement { Field = ElemFieldType.time.ToString(), Name = "maintenance" }]
        };

        Assert.Multiple(() =>
        {
            Assert.That(PlanningImplTaskDisplayLogic.IsRuleImplementationTask(new WfImplTask { TaskType = WfTaskType.rule_delete.ToString() }), Is.True);
            Assert.That(PlanningImplTaskDisplayLogic.IsRuleTask(new WfImplTask { TaskType = WfTaskType.rule_modify.ToString() }), Is.True);
            Assert.That(PlanningImplTaskDisplayLogic.IsRuleTask(new WfImplTask { TaskType = WfTaskType.rule_delete.ToString() }), Is.False);
            Assert.That(PlanningImplTaskDisplayLogic.IsGroupTask(groupTask), Is.True);
            Assert.That(PlanningImplTaskDisplayLogic.HasFreeText(new WfImplTask { TaskType = WfTaskType.generic.ToString() }), Is.True);
            Assert.That(PlanningImplTaskDisplayLogic.HasFreeText(new WfImplTask { TaskType = WfTaskType.access.ToString() }), Is.False);
            Assert.That(PlanningImplTaskDisplayLogic.CanEditFreeText(new WfImplTask { TaskType = WfTaskType.access.ToString() }), Is.True);
            Assert.That(PlanningImplTaskDisplayLogic.CanEditFreeText(new WfImplTask { TaskType = WfTaskType.object_create.ToString() }), Is.False);
            // an empty free text is offered for adding one where the planner may, as in the list view
            Assert.That(PlanningImplTaskDisplayLogic.ShowsFreeText(new WfImplTask { TaskType = WfTaskType.access.ToString() }, true), Is.True);
            Assert.That(PlanningImplTaskDisplayLogic.ShowsFreeText(new WfImplTask { TaskType = WfTaskType.access.ToString() }, false), Is.False);
            Assert.That(PlanningImplTaskDisplayLogic.ShowsFreeText(new WfImplTask { TaskType = WfTaskType.object_create.ToString() }, true), Is.False);
            Assert.That(PlanningImplTaskDisplayLogic.ShowsFreeText(new WfImplTask { TaskType = WfTaskType.object_create.ToString(), FreeText = "kept" }, false), Is.True);
            Assert.That(PlanningImplTaskDisplayLogic.IsTimeObjectTask(timeTask), Is.True);
            Assert.That(PlanningImplTaskDisplayLogic.HasEditableName(timeTask), Is.True);
            Assert.That(PlanningImplTaskDisplayLogic.HasEditableName(groupTask), Is.True);
            Assert.That(PlanningImplTaskDisplayLogic.HasEditableName(new WfImplTask { TaskType = WfTaskType.access.ToString() }), Is.False);
        });
    }

    [Test]
    public void Names_ResolveGroupObjectAndRuleUid()
    {
        WfReqTask requestTask = new() { TaskType = WfTaskType.group_create.ToString() };
        requestTask.SetAddInfo(AdditionalInfoKeys.GrpName, "web-group");
        WfImplTask groupTask = new()
        {
            TaskType = WfTaskType.group_create.ToString(),
            ImplElements = [new WfImplElement { Field = ElemFieldType.source.ToString(), GroupName = "fallback-group" }]
        };
        WfImplTask emptyGroupTask = new() { TaskType = WfTaskType.group_create.ToString() };
        WfImplTask ruleDeleteTask = new()
        {
            TaskType = WfTaskType.rule_delete.ToString(),
            ImplElements = [new WfImplElement { Field = ElemFieldType.rule.ToString(), RuleUid = "rule-1" }]
        };
        WfImplTask timeTask = new()
        {
            TaskType = WfTaskType.object_create.ToString(),
            ImplElements = [new WfImplElement { Field = ElemFieldType.time.ToString(), Name = "maintenance" }]
        };

        Assert.Multiple(() =>
        {
            Assert.That(PlanningImplTaskDisplayLogic.GroupName(groupTask, requestTask), Is.EqualTo("fallback-group"));
            Assert.That(PlanningImplTaskDisplayLogic.GroupName(emptyGroupTask, requestTask), Is.EqualTo("web-group"));
            Assert.That(PlanningImplTaskDisplayLogic.EditableName(groupTask, requestTask), Is.EqualTo("fallback-group"));
            Assert.That(PlanningImplTaskDisplayLogic.RuleUid(ruleDeleteTask), Is.EqualTo("rule-1"));
            Assert.That(PlanningImplTaskDisplayLogic.ObjectName(timeTask), Is.EqualTo("maintenance"));
            Assert.That(PlanningImplTaskDisplayLogic.EditableName(timeTask, requestTask), Is.EqualTo("maintenance"));
        });
    }

    [Test]
    public void RuleActionAndTracking_ResolveNamesOrFallBackToIds()
    {
        PlanningImplTaskDisplayLogic logic = CreateLogic(ruleActions: [new RuleAction { Id = 4, Name = "allow" }],
            trackings: [new Tracking { Id = 9, Name = "log" }]);

        Assert.Multiple(() =>
        {
            Assert.That(logic.RuleActionName(new WfImplTask { RuleAction = 4, Tracking = 9 }), Is.EqualTo("allow"));
            Assert.That(logic.TrackingName(new WfImplTask { RuleAction = 4, Tracking = 9 }), Is.EqualTo("log"));
            Assert.That(logic.RuleActionName(new WfImplTask { RuleAction = 5 }), Is.EqualTo("5"));
            Assert.That(logic.TrackingName(new WfImplTask()), Is.Empty);
        });
    }

    [Test]
    public void NetworkDisplayRows_TruncatesMembersOfReferencedGroup()
    {
        WfReqTask groupRequestTask = new()
        {
            Id = 30,
            TaskType = WfTaskType.group_create.ToString(),
            Elements = [.. kMemberIps.Select(ip => new WfReqElement { Field = ElemFieldType.source.ToString(), IpString = ip })]
        };
        groupRequestTask.SetAddInfo(AdditionalInfoKeys.GrpName, "big-group");
        WfHandler handler = new() { ActTicket = new WfTicket { Tasks = [groupRequestTask] } };
        WfImplTask accessTask = new()
        {
            TaskType = WfTaskType.access.ToString(),
            ImplElements =
            [
                new WfImplElement { Field = ElemFieldType.source.ToString(), GroupName = "big-group" },
                Source("host", "10.0.0.1/32")
            ]
        };

        List<PlanningImplElementRow> rows = CreateLogic(handler).NetworkDisplayRows(accessTask, ElemFieldType.source);

        Assert.Multiple(() =>
        {
            Assert.That(rows, Has.Count.EqualTo(2));
            Assert.That(rows[0].Name, Is.EqualTo("big-group"));
            Assert.That(rows[0].Content, Does.StartWith("1.1.1.1"));
            Assert.That(rows[0].Content, Does.EndWith("(+1)"));
            Assert.That(rows[1].Name, Is.EqualTo("host"));
            Assert.That(rows[1].Content, Does.Contain("10.0.0.1"));
        });
    }

    [Test]
    public void ServiceRowsAndContent_DisplayServiceDefinition()
    {
        WfImplTask serviceObjectTask = new()
        {
            TaskType = WfTaskType.object_create.ToString(),
            ImplElements = [new WfImplElement { Field = ElemFieldType.service.ToString(), Name = "https", Port = 443, ProtoId = 6, ImplAction = "create" }]
        };
        WfImplTask serviceGroupTask = new()
        {
            TaskType = WfTaskType.group_modify.ToString(),
            ImplElements = [new WfImplElement { Field = ElemFieldType.service.ToString(), Name = "ssh", Port = 22, ProtoId = 6, ImplAction = "delete" }]
        };
        PlanningImplTaskDisplayLogic logic = CreateLogic();

        List<PlanningImplElementRow> serviceRows = logic.ServiceDisplayRows(serviceObjectTask);
        List<PlanningImplElementRow> members = logic.GroupMemberRows(serviceGroupTask);

        Assert.Multiple(() =>
        {
            Assert.That(serviceRows.Single().Name, Is.EqualTo("https"));
            Assert.That(serviceRows.Single().Content, Does.Contain("443"));
            Assert.That(logic.ObjectContent(serviceObjectTask), Does.Contain("443"));
            Assert.That(logic.ObjectContent(new WfImplTask { TaskType = WfTaskType.object_create.ToString() }), Is.Empty);
            Assert.That(members.Single().Name, Is.EqualTo("ssh"));
            Assert.That(members.Single().Content, Does.Contain("22"));
            Assert.That(members.Single().Action, Is.EqualTo("delete"));
        });
    }

    [Test]
    public void ObjectContent_ShowsIpOfNetworkObject()
    {
        WfImplTask networkObjectTask = new()
        {
            TaskType = WfTaskType.object_create.ToString(),
            ImplElements = [Source("host", "10.0.0.1/32")]
        };

        Assert.That(CreateLogic().ObjectContent(networkObjectTask), Does.Contain("10.0.0.1"));
    }

    [Test]
    public void ToServiceElement_CopiesServiceFields()
    {
        WfImplElement serviceElement = new() { Id = 3, Field = ElemFieldType.service.ToString(), Name = "https", Port = 443, ProtoId = 6 };

        NwServiceElement converted = PlanningImplTaskDisplayLogic.ToServiceElement(serviceElement);

        Assert.Multiple(() =>
        {
            Assert.That(converted.ElemId, Is.EqualTo(3));
            Assert.That(converted.Name, Is.EqualTo("https"));
            Assert.That(converted.Port, Is.EqualTo(443));
            Assert.That(converted.HasProtocol, Is.True);
        });
    }

    [Test]
    public void ManagementAndDevices_FallBackToDeviceAndRequestTask()
    {
        Management management = new() { Id = 4, Name = "Management A" };
        Device device = new() { Id = 8, Name = "Firewall A", Management = management };
        Device otherDevice = new() { Id = 9, Name = "Firewall B", Management = new Management { Id = 5 } };
        WfReqTask requestTask = new() { ManagementId = 3 };
        PlanningImplTaskDisplayLogic logic = CreateLogic(new WfHandler { Devices = [device, otherDevice] });

        Assert.Multiple(() =>
        {
            Assert.That(logic.EffectiveManagementId(new WfImplTask { ManagementId = 9 }, requestTask), Is.EqualTo(9));
            Assert.That(logic.EffectiveManagementId(new WfImplTask { DeviceId = 8 }, requestTask), Is.EqualTo(4));
            Assert.That(logic.EffectiveManagementId(new WfImplTask(), requestTask), Is.EqualTo(3));
            Assert.That(logic.AvailableDevices(new WfImplTask { ManagementId = 4 }, requestTask), Is.EqualTo(new List<Device> { device }));
            Assert.That(logic.AvailableDevices(new WfImplTask(), new WfReqTask()), Has.Count.EqualTo(2));
            Assert.That(logic.FindDevice(9), Is.SameAs(otherDevice));
            Assert.That(logic.FindDevice(null), Is.Null);
            Assert.That(logic.DeviceName(new WfImplTask { DeviceId = 8 }, requestTask), Is.EqualTo("Firewall A"));
            Assert.That(logic.DeviceName(new WfImplTask { TaskType = WfTaskType.generic.ToString() }, requestTask), Is.Empty);
        });
    }

    [Test]
    public void ApplyManagementAndDevice_KeepDeviceAndManagementConsistent()
    {
        Management managementA = new() { Id = 4 };
        Management managementB = new() { Id = 5 };
        Device deviceA = new() { Id = 8, Management = managementA };
        Device deviceB = new() { Id = 9, Management = managementB };
        WfReqTask requestTask = new();
        PlanningImplTaskDisplayLogic logic = CreateLogic(new WfHandler { Devices = [deviceA, deviceB] });
        WfImplTask task = new() { ManagementId = managementA.Id, DeviceId = deviceA.Id };
        WfImplTask taskWithoutManagement = new();

        bool rejected = PlanningImplTaskDisplayLogic.ApplyDevice(task, requestTask, deviceB);
        bool accepted = PlanningImplTaskDisplayLogic.ApplyDevice(taskWithoutManagement, requestTask, deviceB);
        logic.ApplyManagement(task, requestTask, managementB);

        Assert.Multiple(() =>
        {
            Assert.That(rejected, Is.False);
            Assert.That(accepted, Is.True);
            Assert.That(taskWithoutManagement.DeviceId, Is.EqualTo(deviceB.Id));
            Assert.That(taskWithoutManagement.ManagementId, Is.EqualTo(managementB.Id));
            Assert.That(task.ManagementId, Is.EqualTo(managementB.Id));
            Assert.That(task.DeviceId, Is.Null);
        });
    }

    [Test]
    public void ApplyName_RenamesGroupMembersOrObject()
    {
        WfImplTask groupTask = new()
        {
            TaskType = WfTaskType.group_create.ToString(),
            ImplElements =
            [
                new WfImplElement { Field = ElemFieldType.source.ToString(), GroupName = "old" },
                new WfImplElement { Field = ElemFieldType.service.ToString(), GroupName = "old" }
            ]
        };
        WfImplTask objectTask = new() { TaskType = WfTaskType.object_create.ToString(), ImplElements = [Source("old-host")] };

        PlanningImplTaskDisplayLogic.ApplyName(groupTask, "new");
        PlanningImplTaskDisplayLogic.ApplyName(objectTask, "new-host");

        Assert.Multiple(() =>
        {
            Assert.That(groupTask.ImplElements.All(element => element.GroupName == "new"), Is.True);
            Assert.That(objectTask.ImplElements.Single().Name, Is.EqualTo("new-host"));
        });
    }

    [Test]
    public void TimeRestriction_ShowsNameAndValidity()
    {
        WfImplTask namedTask = new()
        {
            TargetBeginDate = new DateTime(2027, 1, 1),
            TargetEndDate = new DateTime(2027, 2, 1),
            ImplElements = [new WfImplElement { Field = ElemFieldType.time.ToString(), Name = "maintenance" }]
        };
        WfImplTask endOnlyTask = new() { TargetEndDate = new DateTime(2027, 2, 1) };
        PlanningImplTaskDisplayLogic logic = CreateLogic();

        AccessTaskFieldVisibilityConfig visible = new();
        string named = logic.TimeRestriction(namedTask, new WfReqTask(), visible);
        string endOnly = logic.TimeRestriction(endOnlyTask, new WfReqTask(), visible);

        Assert.Multiple(() =>
        {
            Assert.That(named, Does.StartWith("maintenance ("));
            Assert.That(named, Does.Contain(" - "));
            Assert.That(endOnly, Is.Not.Empty);
            Assert.That(endOnly, Does.Not.Contain(" - "));
            Assert.That(logic.TimeRestriction(new WfImplTask(), new WfReqTask(), visible), Is.Empty);
        });
    }

    [Test]
    public void TimeRestriction_LeavesOutHiddenDates()
    {
        WfImplTask namedTask = new()
        {
            ImplElements = [new WfImplElement { Field = ElemFieldType.time.ToString(), Name = "maintenance", StartTime = new DateTime(2027, 1, 1), EndTime = new DateTime(2027, 12, 31) }]
        };
        WfImplTask unnamedTask = new() { TargetBeginDate = new DateTime(2027, 1, 1), TargetEndDate = new DateTime(2027, 12, 31) };
        PlanningImplTaskDisplayLogic logic = CreateLogic();
        AccessTaskFieldVisibilityConfig endOnly = new() { ShowValidFrom = false, ShowValidTo = true };
        AccessTaskFieldVisibilityConfig beginOnly = new() { ShowValidFrom = true, ShowValidTo = false };
        AccessTaskFieldVisibilityConfig noDates = new() { ShowValidFrom = false, ShowValidTo = false };

        string namedEndOnly = logic.TimeRestriction(namedTask, new WfReqTask(), endOnly);
        string namedBeginOnly = logic.TimeRestriction(namedTask, new WfReqTask(), beginOnly);
        string unnamedEndOnly = logic.TimeRestriction(unnamedTask, new WfReqTask(), endOnly);

        Assert.Multiple(() =>
        {
            Assert.That(namedEndOnly, Does.StartWith("maintenance (").And.Contain("2027").And.Not.Contain("01.01.2027").And.Not.Contain(" - "));
            Assert.That(namedBeginOnly, Does.Contain("01.01.2027").And.Not.Contain("31.12.2027"));
            Assert.That(logic.TimeRestriction(namedTask, new WfReqTask(), noDates), Is.EqualTo("maintenance"));
            // the name built from the dates must not show the hidden begin either
            Assert.That(unnamedEndOnly, Is.Not.Empty.And.Not.Contain("2027-01-01").And.Not.Contain("01.01.2027"));
            Assert.That(logic.TimeRestriction(unnamedTask, new WfReqTask(), noDates), Is.Empty);
            // like the request tasks, no name is built from the dates, which would show them twice
            Assert.That(logic.TimeRestriction(unnamedTask, new WfReqTask(), new AccessTaskFieldVisibilityConfig()),
                Does.StartWith("(01.01.2027").And.Not.Contain("2027-01-01"));
        });
    }
}
