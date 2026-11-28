using FWO.Data;
using FWO.Data.Workflow;
using FWO.Services.Workflow;
using FWO.Ui.Services;
using NUnit.Framework;

namespace FWO.Test;

[TestFixture]
internal sealed class PlanningTaskListRowBuilderTest
{
    private static readonly List<RuleAction> kRuleActions = [new RuleAction { Id = 1, Name = "accept" }];
    private static readonly List<Tracking> kTrackings = [new Tracking { Id = 2, Name = "log" }];
    private static readonly List<IpProtocol> kProtocols = [new IpProtocol { Id = 6, Name = "tcp" }];
    private static readonly List<NetworkZone> kNoZones = [];
    private static readonly Func<WfImplTask, WfReqTask, bool> kEditable = (_, _) => true;
    private static readonly Func<WfImplTask, WfReqTask, bool> kNotEditable = (_, _) => false;
    private static readonly Func<WfImplTask, WfReqTask, string> kNoTimeRestriction = (_, _) => "";
    private static readonly IReadOnlyList<string> kRuleContentColumns =
    [
        ApprovalTaskListRowBuilder.kSource, ApprovalTaskListRowBuilder.kDestination, ApprovalTaskListRowBuilder.kService,
        PlanningTaskListRowBuilder.kDevice, PlanningTaskListRowBuilder.kRulePosition, ApprovalTaskListRowBuilder.kRuleAction
    ];

    private readonly List<string> editCalls = [];

    [SetUp]
    public void SetUp()
    {
        editCalls.Clear();
    }

    [Test]
    public void VisibleColumns_AllTasksShowLevelAndRequestColumns()
    {
        AccessTaskFieldVisibilityConfig visibility = new() { ShowValidFrom = true };

        List<string> keys = Keys(PlanningTaskListRowBuilder.VisibleColumns(TaskListTable.Access, PlanningTaskListMode.AllTasks, visibility));

        Assert.Multiple(() =>
        {
            Assert.That(keys[0], Is.EqualTo(ApprovalTaskListRowBuilder.kId));
            Assert.That(keys[1], Is.EqualTo(PlanningTaskListRowBuilder.kLevel));
            Assert.That(keys, Does.Not.Contain(PlanningTaskListRowBuilder.kRequestTask));
            Assert.That(keys, Does.Not.Contain(ApprovalTaskListRowBuilder.kCompliance));
            Assert.That(keys, Does.Contain(ApprovalTaskListRowBuilder.kSourceZone));
            Assert.That(keys, Does.Contain(ApprovalTaskListRowBuilder.kOwner));
            Assert.That(keys, Does.Contain(ApprovalTaskListRowBuilder.kReason));
            Assert.That(keys, Does.Contain(ApprovalTaskListRowBuilder.kTimeRestriction));
            Assert.That(keys.IndexOf(ApprovalTaskListRowBuilder.kManagement), Is.LessThan(keys.IndexOf(PlanningTaskListRowBuilder.kDevice)));
            Assert.That(keys.IndexOf(PlanningTaskListRowBuilder.kDevice), Is.LessThan(keys.IndexOf(PlanningTaskListRowBuilder.kRulePosition)));
            Assert.That(keys[^1], Is.EqualTo(ApprovalTaskListRowBuilder.kAssigned));
        });
    }

    [Test]
    public void VisibleColumns_ImplementationTasksReplaceRequestOnlyColumnsByRequestTask()
    {
        AccessTaskFieldVisibilityConfig visibility = new() { ShowTitle = false, ShowHandler = false };

        List<string> access = Keys(PlanningTaskListRowBuilder.VisibleColumns(TaskListTable.Access, PlanningTaskListMode.ImplementationTasks, visibility));
        List<string> address = Keys(PlanningTaskListRowBuilder.VisibleColumns(TaskListTable.Address, PlanningTaskListMode.ImplementationTasks, visibility));

        Assert.Multiple(() =>
        {
            Assert.That(access[1], Is.EqualTo(PlanningTaskListRowBuilder.kRequestTask));
            Assert.That(access, Does.Not.Contain(PlanningTaskListRowBuilder.kLevel));
            Assert.That(access, Does.Not.Contain(ApprovalTaskListRowBuilder.kSourceZone));
            Assert.That(access, Does.Not.Contain(ApprovalTaskListRowBuilder.kOwner));
            Assert.That(access, Does.Not.Contain(ApprovalTaskListRowBuilder.kReason));
            Assert.That(access, Does.Not.Contain(ApprovalTaskListRowBuilder.kTitle));
            Assert.That(access, Does.Not.Contain(ApprovalTaskListRowBuilder.kHandler));
            Assert.That(access, Does.Contain(PlanningTaskListRowBuilder.kDevice));
            Assert.That(address, Does.Not.Contain(ApprovalTaskListRowBuilder.kZone));
            Assert.That(address, Does.Contain(ApprovalTaskListRowBuilder.kContent));
            Assert.That(address, Does.Contain(ApprovalTaskListRowBuilder.kManagement));
            Assert.That(address, Does.Not.Contain(PlanningTaskListRowBuilder.kDevice));
        });
    }

    [Test]
    public void VisibleColumns_ObjectTablesFollowObjectVisibility()
    {
        ObjectTaskFieldVisibilityConfig objectVisibility = new() { ShowHandler = false, ShowReason = false };

        List<string> service = Keys(PlanningTaskListRowBuilder.VisibleColumns(TaskListTable.Service, PlanningTaskListMode.AllTasks,
            new AccessTaskFieldVisibilityConfig(), objectVisibility));
        List<string> other = Keys(PlanningTaskListRowBuilder.VisibleColumns(TaskListTable.Other, PlanningTaskListMode.AllTasks,
            new AccessTaskFieldVisibilityConfig { ShowFreeText = true }));

        Assert.Multiple(() =>
        {
            Assert.That(service, Does.Not.Contain(ApprovalTaskListRowBuilder.kHandler));
            Assert.That(service, Does.Not.Contain(ApprovalTaskListRowBuilder.kReason));
            Assert.That(service, Does.Not.Contain(ApprovalTaskListRowBuilder.kZone));
            Assert.That(service, Does.Contain(ApprovalTaskListRowBuilder.kAssigned));
            Assert.That(other, Does.Contain(ApprovalTaskListRowBuilder.kHandler));
            Assert.That(other, Does.Not.Contain(ApprovalTaskListRowBuilder.kContent));
            Assert.That(other, Does.Contain(ApprovalTaskListRowBuilder.kFreeText));
            Assert.That(other, Does.Not.Contain(PlanningTaskListRowBuilder.kDevice));
        });
    }

    [Test]
    public void VisibleColumns_OtherTableShowsTheRuleColumnsOfItsImplementationTasks()
    {
        WfReqTask ruleDelete = RequestTaskWithImplTask(6, WfTaskType.rule_delete);
        WfReqTask ruleModify = RequestTaskWithImplTask(7, WfTaskType.rule_modify);
        WfReqTask generic = RequestTaskWithImplTask(9, WfTaskType.generic);
        AccessTaskFieldVisibilityConfig visibility = new() { ShowTracking = false };

        PlanningImplColumns deleteOnly = PlanningTaskListRowBuilder.ImplementationColumnsFor(TaskListTable.Other, Singles(ruleDelete, generic), kEditable, kNoTimeRestriction);
        PlanningImplColumns withModify = PlanningTaskListRowBuilder.ImplementationColumnsFor(TaskListTable.Other, Singles(generic, ruleModify, ruleDelete), kEditable, kNoTimeRestriction);
        List<string> deleteColumns = Keys(PlanningTaskListRowBuilder.VisibleColumns(TaskListTable.Other, PlanningTaskListMode.ImplementationTasks,
            visibility, null, deleteOnly));
        List<string> modifyColumns = Keys(PlanningTaskListRowBuilder.VisibleColumns(TaskListTable.Other, PlanningTaskListMode.ImplementationTasks,
            visibility, null, withModify));
        List<string> modifyColumnsWithoutValidity = Keys(PlanningTaskListRowBuilder.VisibleColumns(TaskListTable.Other, PlanningTaskListMode.ImplementationTasks,
            new AccessTaskFieldVisibilityConfig { ShowValidFrom = false, ShowValidTo = false }, null, withModify));

        Assert.Multiple(() =>
        {
            Assert.That(PlanningTaskListRowBuilder.ImplementationColumnsFor(TaskListTable.Other, Singles(generic), kEditable, kNoTimeRestriction).RuleColumns, Is.EqualTo(PlanningRuleColumns.None));
            // the rule tasks are in the other table, not in the access table
            Assert.That(PlanningTaskListRowBuilder.ImplementationColumnsFor(TaskListTable.Access, Singles(ruleModify), kEditable, kNoTimeRestriction), Is.EqualTo(PlanningImplColumns.None));
            Assert.That(deleteOnly.RuleColumns, Is.EqualTo(PlanningRuleColumns.Device));
            Assert.That(withModify.RuleColumns, Is.EqualTo(PlanningRuleColumns.All));
            Assert.That(deleteColumns, Does.Contain(PlanningTaskListRowBuilder.kDevice));
            Assert.That(deleteColumns, Does.Not.Contain(PlanningTaskListRowBuilder.kRulePosition));
            Assert.That(deleteColumns, Does.Not.Contain(ApprovalTaskListRowBuilder.kSource));
            Assert.That(deleteColumns, Does.Not.Contain(ApprovalTaskListRowBuilder.kTimeRestriction));
            // like the access table, the validity of rule tasks follows the configured visibility of valid from and to
            Assert.That(modifyColumns, Does.Contain(ApprovalTaskListRowBuilder.kTimeRestriction));
            Assert.That(modifyColumnsWithoutValidity, Does.Not.Contain(ApprovalTaskListRowBuilder.kTimeRestriction));
            Assert.That(modifyColumns, Is.SupersetOf(kRuleContentColumns));
            Assert.That(modifyColumns, Does.Not.Contain(ApprovalTaskListRowBuilder.kTrack));
            Assert.That(modifyColumns.IndexOf(ApprovalTaskListRowBuilder.kManagement), Is.LessThan(modifyColumns.IndexOf(PlanningTaskListRowBuilder.kDevice)));
            Assert.That(modifyColumns.IndexOf(PlanningTaskListRowBuilder.kDevice), Is.LessThan(modifyColumns.IndexOf(PlanningTaskListRowBuilder.kRulePosition)));
        });
    }

    [Test]
    public void VisibleColumns_OtherTableShowsTheValidityOfTimeObjectTasks()
    {
        WfReqTask timeObject = new()
        {
            Id = 4,
            TaskType = WfTaskType.object_create.ToString(),
            Elements = [new WfReqElement { Field = ElemFieldType.time.ToString(), Name = "t-2027" }],
            ImplementationTasks =
            [
                new WfImplTask
                {
                    Id = 41,
                    ReqTaskId = 4,
                    TaskType = WfTaskType.object_create.ToString(),
                    ImplElements =
                    [
                        new WfImplElement { Field = ElemFieldType.time.ToString(), Name = "t-2027", StartTime = new DateTime(2027, 1, 1), EndTime = new DateTime(2027, 12, 31) }
                    ]
                }
            ]
        };
        WfReqTask generic = RequestTaskWithImplTask(9, WfTaskType.generic);
        WfReqTask ruleModify = RequestTaskWithImplTask(7, WfTaskType.rule_modify);
        // the validity of access tasks is hidden, but object tasks show theirs like in the card view
        AccessTaskFieldVisibilityConfig noValidity = new() { ShowValidFrom = false, ShowValidTo = false };
        PlanningImplTaskDisplayLogic implLogic = new(new WfHandler(), new SimulatedUserConfig(), kRuleActions, kTrackings, kProtocols);
        string TimeRestriction(WfImplTask implementationTask, WfReqTask requestTask) => implLogic.TimeRestriction(implementationTask, requestTask, noValidity);
        PlanningImplColumns withTimeObject = PlanningTaskListRowBuilder.ImplementationColumnsFor(TaskListTable.Other, Singles(timeObject, generic),
            kEditable, TimeRestriction);
        PlanningImplColumns genericOnly = PlanningTaskListRowBuilder.ImplementationColumnsFor(TaskListTable.Other, Singles(generic),
            kEditable, TimeRestriction);
        // with hidden dates, the time restriction of a rule task is the name of its time element
        PlanningImplColumns ruleTaskWithTimeName = PlanningTaskListRowBuilder.ImplementationColumnsFor(TaskListTable.Other, Singles(ruleModify),
            kEditable, (_, _) => "maintenance");
        List<string> accessWithTimeName = Keys(PlanningTaskListRowBuilder.VisibleColumns(TaskListTable.Access, PlanningTaskListMode.ImplementationTasks,
            noValidity, null, new PlanningImplColumns(PlanningRuleColumns.None, false, true)));
        List<string> accessWithoutTimeRestriction = Keys(PlanningTaskListRowBuilder.VisibleColumns(TaskListTable.Access, PlanningTaskListMode.ImplementationTasks,
            noValidity, null, PlanningImplColumns.None));
        PlanningTaskListRow timeObjectRow = CreateBuilder([timeObject], false, noValidity)
            .Build(Singles(timeObject), TaskListTable.Other, PlanningTaskListMode.ImplementationTasks).Single();

        bool HasTimeRestriction(PlanningImplColumns implColumns) => Keys(PlanningTaskListRowBuilder.VisibleColumns(TaskListTable.Other,
            PlanningTaskListMode.ImplementationTasks, noValidity, null, implColumns)).Contains(ApprovalTaskListRowBuilder.kTimeRestriction);

        Assert.Multiple(() =>
        {
            Assert.That(ApprovalTaskListRowBuilder.TableOf(timeObject), Is.EqualTo(TaskListTable.Other));
            Assert.That(withTimeObject.TimeRestriction, Is.True);
            Assert.That(HasTimeRestriction(withTimeObject), Is.True);
            // the hidden validity dates are left out, as in the request-task rows
            Assert.That(timeObjectRow.Cell(ApprovalTaskListRowBuilder.kTimeRestriction).Text, Is.EqualTo("t-2027"));
            Assert.That(HasTimeRestriction(genericOnly), Is.False);
            // the name of the time element of rule tasks is shown as in the card view, also in the access table
            Assert.That(ruleTaskWithTimeName.TimeRestriction, Is.True);
            Assert.That(HasTimeRestriction(ruleTaskWithTimeName), Is.True);
            Assert.That(accessWithTimeName, Does.Contain(ApprovalTaskListRowBuilder.kTimeRestriction));
            Assert.That(accessWithoutTimeRestriction, Does.Not.Contain(ApprovalTaskListRowBuilder.kTimeRestriction));
        });
    }

    [Test]
    public void VisibleColumns_FreeTextColumnOnlyIfRequestOrImplementationTasksNeedIt()
    {
        AccessTaskFieldVisibilityConfig shown = new() { ShowFreeText = true };
        AccessTaskFieldVisibilityConfig hidden = new() { ShowFreeText = false };
        WfReqTask accessWithImplTask = AccessRequestTask(7, [AccessImplTask(71, 7)]);
        // management object tasks keep their free text, so it can only be shown, not added
        WfReqTask group = RequestTaskWithImplTask(5, WfTaskType.group_create);
        TaskListTable groupTable = ApprovalTaskListRowBuilder.TableOf(group);
        PlanningImplColumns noImplTasks = PlanningTaskListRowBuilder.ImplementationColumnsFor(TaskListTable.Access, Singles(AccessRequestTask(8, [])), kEditable, kNoTimeRestriction);
        PlanningImplColumns editableFreeText = PlanningTaskListRowBuilder.ImplementationColumnsFor(TaskListTable.Access, Singles(accessWithImplTask), kEditable, kNoTimeRestriction);
        PlanningImplColumns groupWithoutFreeText = PlanningTaskListRowBuilder.ImplementationColumnsFor(groupTable, Singles(group), kEditable, kNoTimeRestriction);
        group.ImplementationTasks[0].FreeText = "kept text";
        PlanningImplColumns groupWithFreeText = PlanningTaskListRowBuilder.ImplementationColumnsFor(groupTable, Singles(group), kEditable, kNoTimeRestriction);
        // e.g. a finished ticket: an empty free text can not be added, a filled one is still shown
        PlanningImplColumns notEditableWithoutFreeText = PlanningTaskListRowBuilder.ImplementationColumnsFor(TaskListTable.Access, Singles(accessWithImplTask), kNotEditable, kNoTimeRestriction);
        WfReqTask accessWithFreeText = AccessRequestTask(9, [AccessImplTask(91, 9)]);
        accessWithFreeText.ImplementationTasks[0].FreeText = "planner note";
        PlanningImplColumns notEditableWithFreeText = PlanningTaskListRowBuilder.ImplementationColumnsFor(TaskListTable.Access, Singles(accessWithFreeText), kNotEditable, kNoTimeRestriction);

        bool HasFreeText(TaskListTable table, PlanningTaskListMode mode, AccessTaskFieldVisibilityConfig visibility, PlanningImplColumns implColumns) =>
            Keys(PlanningTaskListRowBuilder.VisibleColumns(table, mode, visibility, null, implColumns)).Contains(ApprovalTaskListRowBuilder.kFreeText);

        Assert.Multiple(() =>
        {
            Assert.That(noImplTasks.FreeText, Is.False);
            Assert.That(editableFreeText.FreeText, Is.True);
            Assert.That(groupWithoutFreeText.FreeText, Is.False);
            Assert.That(groupWithFreeText.FreeText, Is.True);
            Assert.That(notEditableWithoutFreeText.FreeText, Is.False);
            Assert.That(notEditableWithFreeText.FreeText, Is.True);
            Assert.That(HasFreeText(TaskListTable.Access, PlanningTaskListMode.ImplementationTasks, hidden, notEditableWithoutFreeText), Is.False);
            Assert.That(HasFreeText(TaskListTable.Access, PlanningTaskListMode.AllTasks, shown, noImplTasks), Is.True);
            // like the approval task list view
            Assert.That(HasFreeText(TaskListTable.Access, PlanningTaskListMode.AllTasks, hidden, noImplTasks), Is.False);
            Assert.That(HasFreeText(groupTable, PlanningTaskListMode.AllTasks, shown, groupWithoutFreeText), Is.False);
            // the request tasks and their free text are not shown
            Assert.That(HasFreeText(TaskListTable.Access, PlanningTaskListMode.ImplementationTasks, shown, noImplTasks), Is.False);
            Assert.That(HasFreeText(TaskListTable.Access, PlanningTaskListMode.AllTasks, hidden, editableFreeText), Is.True);
            Assert.That(HasFreeText(groupTable, PlanningTaskListMode.ImplementationTasks, hidden, groupWithFreeText), Is.True);
        });
    }

    [Test]
    public void Build_AllTasksFollowEachRequestTaskByItsImplementationTasks()
    {
        WfReqTask first = AccessRequestTask(7, [AccessImplTask(72, 7), AccessImplTask(71, 7)]);
        WfReqTask second = AccessRequestTask(8, [AccessImplTask(81, 8)]);
        WfReqTask generic = new() { Id = 9, TaskType = WfTaskType.generic.ToString(), ImplementationTasks = [new WfImplTask { Id = 91, ReqTaskId = 9 }] };

        List<PlanningTaskListRow> rows = CreateBuilder([first, second, generic], false)
            .Build(Singles(second, generic, first), TaskListTable.Access, PlanningTaskListMode.AllTasks);

        Assert.Multiple(() =>
        {
            Assert.That(rows.Select(row => row.Id), Is.EqualTo(new List<long> { 7, 71, 72, 8, 81 }));
            Assert.That(rows.Select(row => row.IsImplementationTask), Is.EqualTo(new List<bool> { false, true, true, false, true }));
            Assert.That(rows[0].Cell(PlanningTaskListRowBuilder.kLevel).Text, Is.EqualTo("RequestTask"));
            Assert.That(rows[1].Cell(PlanningTaskListRowBuilder.kLevel).Text, Is.EqualTo("ImplementationTask"));
            Assert.That(rows[1].RequestTask, Is.SameAs(first));
            Assert.That(rows[0].Cell(ApprovalTaskListRowBuilder.kSource).Text, Does.Contain("client-a"));
        });
    }

    [Test]
    public void Build_ImplementationTasksOnlyShowImplementationRowsWithRequestTask()
    {
        WfReqTask requestTask = AccessRequestTask(7, [AccessImplTask(71, 7)]);

        List<PlanningTaskListRow> rows = CreateBuilder([requestTask], false)
            .Build(Singles(requestTask), TaskListTable.Access, PlanningTaskListMode.ImplementationTasks);

        PlanningTaskListRow row = rows.Single();
        Assert.Multiple(() =>
        {
            Assert.That(row.ImplementationTask!.Id, Is.EqualTo(71));
            Assert.That(row.Cell(PlanningTaskListRowBuilder.kRequestTask).Text, Is.EqualTo("7"));
            Assert.That(row.Cell(ApprovalTaskListRowBuilder.kState).Text, Is.EqualTo("Planning"));
            Assert.That(row.Cell(ApprovalTaskListRowBuilder.kHandler).Text, Is.EqualTo("bob"));
            Assert.That(row.Cell(ApprovalTaskListRowBuilder.kManagement).Text, Is.EqualTo("Management A"));
            Assert.That(row.Cell(PlanningTaskListRowBuilder.kDevice).Text, Is.EqualTo("Firewall A"));
            Assert.That(row.Cell(ApprovalTaskListRowBuilder.kRuleAction).Text, Is.EqualTo("accept"));
            Assert.That(row.Cell(ApprovalTaskListRowBuilder.kTrack).Text, Is.EqualTo("log"));
            Assert.That(row.Cell(ApprovalTaskListRowBuilder.kSource).Values, Is.EqualTo(new List<string> { "impl-client (10.0.0.5)" }));
            Assert.That(row.Cell(ApprovalTaskListRowBuilder.kService).Text, Does.Contain("443"));
            Assert.That(row.Cell(ApprovalTaskListRowBuilder.kSourceZone).Values, Is.Empty);
            Assert.That(row.Cell(ApprovalTaskListRowBuilder.kManagement).CanEdit, Is.False);
        });
    }

    [Test]
    public async Task Build_EditableImplementationCellsOpenTheirEditors()
    {
        WfImplTask implTask = AccessImplTask(71, 7);
        implTask.FreeText = "text";
        WfReqTask requestTask = AccessRequestTask(7, [implTask]);
        PlanningTaskListRow row = CreateBuilder([requestTask], true)
            .Build(Singles(requestTask), TaskListTable.Access, PlanningTaskListMode.ImplementationTasks).Single();

        await row.Cell(ApprovalTaskListRowBuilder.kManagement).Edit!();
        await row.Cell(PlanningTaskListRowBuilder.kDevice).Edit!();
        await row.Cell(ApprovalTaskListRowBuilder.kFreeText).Edit!();

        Assert.Multiple(() =>
        {
            Assert.That(editCalls, Is.EqualTo(new List<string> { "management:71", "device:71", "free_text:71" }));
            Assert.That(row.Cell(ApprovalTaskListRowBuilder.kObject).CanEdit, Is.False);
        });
    }

    [Test]
    public async Task Build_GroupAndObjectImplementationTasksShowNameAndContent()
    {
        WfImplTask groupImpl = new()
        {
            Id = 41,
            TaskType = WfTaskType.group_modify.ToString(),
            ImplElements =
            [
                new WfImplElement { Field = ElemFieldType.source.ToString(), Name = "host-a", GroupName = "grp", IpString = "10.0.0.1", ImplAction = "create" },
                new WfImplElement { Field = ElemFieldType.source.ToString(), Name = "host-b", GroupName = "grp", IpString = "10.0.0.2", ImplAction = "delete" }
            ]
        };
        WfReqTask groupRequest = new() { Id = 4, TaskType = WfTaskType.group_modify.ToString(), ImplementationTasks = [groupImpl] };
        WfImplTask objectImpl = new()
        {
            Id = 51,
            TaskType = WfTaskType.object_create.ToString(),
            ImplElements = [new WfImplElement { Field = ElemFieldType.source.ToString(), Name = "new-host", IpString = "10.0.0.9", ImplAction = "create" }]
        };
        WfReqTask objectRequest = new()
        {
            Id = 5,
            TaskType = WfTaskType.object_create.ToString(),
            Elements = [new WfReqElement { Field = ElemFieldType.source.ToString(), Name = "new-host", IpString = "10.0.0.9", RequestAction = "create" }],
            ImplementationTasks = [objectImpl]
        };
        PlanningTaskListRowBuilder builder = CreateBuilder([groupRequest, objectRequest], true);

        List<PlanningTaskListRow> rows = builder.Build(Singles(groupRequest, objectRequest), TaskListTable.Address, PlanningTaskListMode.ImplementationTasks);
        PlanningTaskListRow objectRow = rows.Single(row => row.Id == 51);
        PlanningTaskListRow groupRow = rows.Single(row => row.Id == 41);
        await groupRow.Cell(ApprovalTaskListRowBuilder.kObject).Edit!();

        Assert.Multiple(() =>
        {
            Assert.That(objectRow.Cell(ApprovalTaskListRowBuilder.kObject).Text, Is.EqualTo("new-host"));
            Assert.That(objectRow.Cell(ApprovalTaskListRowBuilder.kContent).Text, Does.Contain("10.0.0.9"));
            Assert.That(groupRow.Cell(ApprovalTaskListRowBuilder.kObject).Text, Is.EqualTo("grp"));
            Assert.That(groupRow.Cell(ApprovalTaskListRowBuilder.kContent).Values, Has.Count.EqualTo(2));
            Assert.That(groupRow.Cell(ApprovalTaskListRowBuilder.kContent).Badge(0)!.CssClass, Is.EqualTo("text-bg-success"));
            Assert.That(groupRow.Cell(ApprovalTaskListRowBuilder.kContent).Badge(1)!.CssClass, Is.EqualTo("text-bg-danger"));
            Assert.That(editCalls, Is.EqualTo(new List<string> { "name:41" }));
        });
    }

    [Test]
    public void Build_BundleShowsOneRequestRowWithImplementationTasksOfAllItsTasks()
    {
        WfImplTask bundleImpl = AccessImplTask(71, 7);
        WfImplTask laterImpl = AccessImplTask(75, 8);
        WfReqTask first = AccessRequestTask(7, [bundleImpl]);
        WfReqTask second = AccessRequestTask(8, [laterImpl]);
        List<List<WfReqTask>> bundle = [[first, second]];
        PlanningTaskListRowBuilder builder = CreateBuilder([first, second], false);

        List<PlanningTaskListRow> allRows = builder.Build(bundle, TaskListTable.Access, PlanningTaskListMode.AllTasks);
        List<PlanningTaskListRow> implRows = builder.Build(bundle, TaskListTable.Access, PlanningTaskListMode.ImplementationTasks);

        Assert.Multiple(() =>
        {
            Assert.That(allRows.Select(row => row.Id), Is.EqualTo(new List<long> { 7, 71, 75 }));
            Assert.That(allRows[0].Cell(ApprovalTaskListRowBuilder.kId).Text, Is.EqualTo("7, 8"));
            Assert.That(allRows[0].TaskGroup, Is.EqualTo(new List<WfReqTask> { first, second }));
            Assert.That(allRows[1].RequestTask, Is.SameAs(first));
            Assert.That(allRows[2].RequestTask, Is.SameAs(second));
            Assert.That(allRows[1].TaskGroup, Is.EqualTo(new List<WfReqTask> { first, second }));
            Assert.That(implRows.Select(row => row.Cell(PlanningTaskListRowBuilder.kRequestTask).Text), Is.EqualTo(new List<string> { "7, 8", "7, 8" }));
        });
    }

    [Test]
    public void Build_RequestTaskFreeTextFollowsTheApprovalListVisibility()
    {
        WfImplTask implTask = AccessImplTask(71, 7);
        implTask.FreeText = "impl text";
        WfReqTask requestTask = AccessRequestTask(7, [implTask]);
        requestTask.FreeText = "request text";

        List<PlanningTaskListRow> shown = CreateBuilder([requestTask], false, new AccessTaskFieldVisibilityConfig { ShowFreeText = true })
            .Build(Singles(requestTask), TaskListTable.Access, PlanningTaskListMode.AllTasks);
        List<PlanningTaskListRow> hidden = CreateBuilder([requestTask], false, new AccessTaskFieldVisibilityConfig { ShowFreeText = false })
            .Build(Singles(requestTask), TaskListTable.Access, PlanningTaskListMode.AllTasks);

        Assert.Multiple(() =>
        {
            Assert.That(shown[0].Cell(ApprovalTaskListRowBuilder.kFreeText).Text, Is.EqualTo("request text"));
            Assert.That(hidden[0].Cell(ApprovalTaskListRowBuilder.kFreeText).Values, Is.Empty);
            // the implementation tasks keep their own free text
            Assert.That(hidden[1].Cell(ApprovalTaskListRowBuilder.kFreeText).Text, Is.EqualTo("impl text"));
            // as in the approval task list view, object tables never show the free text of request tasks
            Assert.That(ApprovalTaskListRowBuilder.ShowsRequestFreeText(TaskListTable.Address, new AccessTaskFieldVisibilityConfig { ShowFreeText = true }), Is.False);
            Assert.That(ApprovalTaskListRowBuilder.ShowsRequestFreeText(TaskListTable.Other, new AccessTaskFieldVisibilityConfig { ShowFreeText = true }), Is.True);
        });
    }

    [Test]
    public void Build_RuleDeleteImplementationTaskShowsRuleUid()
    {
        WfImplTask ruleDelete = new()
        {
            Id = 61,
            TaskType = WfTaskType.rule_delete.ToString(),
            ImplElements = [new WfImplElement { Field = ElemFieldType.rule.ToString(), RuleUid = "rule-1" }]
        };
        WfReqTask requestTask = new() { Id = 6, TaskType = WfTaskType.rule_delete.ToString(), ImplementationTasks = [ruleDelete] };

        PlanningTaskListRow row = CreateBuilder([requestTask], false)
            .Build(Singles(requestTask), TaskListTable.Other, PlanningTaskListMode.ImplementationTasks).Single();

        Assert.That(row.Cell(ApprovalTaskListRowBuilder.kObject).Text, Is.EqualTo("rule-1"));
    }

    private static List<string> Keys(List<TaskListColumn> columns) => [.. columns.Select(column => column.Key)];

    private static List<List<WfReqTask>> Singles(params WfReqTask[] tasks) => [.. tasks.Select(task => new List<WfReqTask> { task })];

    private static WfReqTask RequestTaskWithImplTask(long id, WfTaskType taskType) => new()
    {
        Id = id,
        TaskType = taskType.ToString(),
        ImplementationTasks = [new WfImplTask { Id = id * 10 + 1, ReqTaskId = id, TaskType = taskType.ToString() }]
    };

    private static WfReqTask AccessRequestTask(long id, List<WfImplTask> implementationTasks)
    {
        return new WfReqTask
        {
            Id = id,
            StateId = 2,
            Title = $"access {id}",
            TaskType = WfTaskType.access.ToString(),
            Elements = [new WfReqElement { Field = ElemFieldType.source.ToString(), Name = "client-a", IpString = "10.0.0.1" }],
            ImplementationTasks = implementationTasks
        };
    }

    private static WfImplTask AccessImplTask(long id, long requestTaskId)
    {
        return new WfImplTask
        {
            Id = id,
            ReqTaskId = requestTaskId,
            StateId = 3,
            TaskType = WfTaskType.access.ToString(),
            ManagementId = 4,
            DeviceId = 8,
            RuleAction = 1,
            Tracking = 2,
            CurrentHandler = new UiUser { Name = "bob" },
            ImplElements =
            [
                new WfImplElement { Field = ElemFieldType.source.ToString(), Name = "impl-client", IpString = "10.0.0.5" },
                new WfImplElement { Field = ElemFieldType.service.ToString(), Name = "https", Port = 443, ProtoId = 6 }
            ]
        };
    }

    private PlanningTaskListRowBuilder CreateBuilder(List<WfReqTask> tasks, bool canEdit, AccessTaskFieldVisibilityConfig? visibility = null)
    {
        SimulatedUserConfig userConfig = new();
        Management management = new() { Id = 4, Name = "Management A" };
        WfHandler handler = new()
        {
            ActTicket = new WfTicket { Tasks = tasks },
            Managements = [management],
            Devices = [new Device { Id = 8, Name = "Firewall A", Management = management }]
        };
        ApprovalTaskDisplayLogic displayLogic = new(handler, userConfig, kRuleActions, kTrackings, kProtocols, kNoZones,
            ApprovalTaskElementDisplayMode.Resolved);
        ApprovalTaskViewContext context = new(displayLogic, visibility ?? new AccessTaskFieldVisibilityConfig(), kProtocols, new ApprovalTaskViewActions());
        PlanningImplTaskListActions implActions = new()
        {
            CanEdit = (_, _) => canEdit,
            EditManagement = (task, _) => Record($"management:{task.Id}"),
            EditDevice = (task, _) => Record($"device:{task.Id}"),
            EditFreeText = (task, _) => Record($"free_text:{task.Id}"),
            EditName = (task, _) => Record($"name:{task.Id}")
        };
        PlanningImplTaskDisplayLogic implLogic = new(handler, userConfig, kRuleActions, kTrackings, kProtocols);
        return new PlanningTaskListRowBuilder(context, userConfig, new WfStateDict { Name = { [2] = "Approval", [3] = "Planning" } },
            implLogic, implActions);
    }

    private Task Record(string call)
    {
        editCalls.Add(call);
        return Task.CompletedTask;
    }
}
