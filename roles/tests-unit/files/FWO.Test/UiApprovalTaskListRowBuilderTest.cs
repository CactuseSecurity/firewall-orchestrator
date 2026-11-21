using FWO.Data;
using FWO.Data.Workflow;
using FWO.Services.Workflow;
using FWO.Ui.Services;
using NetTools;
using NUnit.Framework;

namespace FWO.Test;

[TestFixture]
internal class UiApprovalTaskListRowBuilderTest
{
    private static readonly List<RuleAction> kNoRuleActions = [];
    private static readonly List<Tracking> kNoTrackings = [];
    private static readonly List<IpProtocol> kProtocols = [new IpProtocol { Id = 6, Name = "tcp" }];
    private static readonly IPAddressRange[] kInternalRanges = [IPAddressRange.Parse("10.0.0.0/8")];
    private static readonly List<NetworkZone> kZones = [new NetworkZone { Name = "internal", IPRanges = kInternalRanges }];
    private static readonly List<string> kNoFields = [];
    private static readonly List<string> kViolationReasons = ["forbidden service"];
    private static readonly List<string> kTitleAndSource = [WorkflowEditableFieldKeys.Title, WorkflowEditableFieldKeys.Source];
    private static readonly List<string> kSourceOnly = [WorkflowEditableFieldKeys.Source];
    private static readonly List<string> kGroupMembersAndName = [WorkflowEditableFieldKeys.GroupMembers, WorkflowEditableFieldKeys.Name];
    private static readonly List<string> kContentOnly = [WorkflowEditableFieldKeys.Content];

    private readonly List<string> editCalls = [];

    [SetUp]
    public void SetUp()
    {
        editCalls.Clear();
    }

    [Test]
    public void VisibleColumns_AccessTableShowsRuleContentFirstAndTaskInfoBeforeHandler()
    {
        AccessTaskFieldVisibilityConfig visibility = new() { ShowOwner = false, ShowFreeText = true };

        List<string> keys = AccessColumnKeys(visibility);

        Assert.Multiple(() =>
        {
            Assert.That(keys.Take(7), Is.EqualTo(new List<string>
            {
                ApprovalTaskListRowBuilder.kId,
                ApprovalTaskListRowBuilder.kSourceZone,
                ApprovalTaskListRowBuilder.kDestinationZone,
                ApprovalTaskListRowBuilder.kSource,
                ApprovalTaskListRowBuilder.kDestination,
                ApprovalTaskListRowBuilder.kService,
                ApprovalTaskListRowBuilder.kTimeRestriction
            }));
            Assert.That(keys, Does.Not.Contain(ApprovalTaskListRowBuilder.kOwner));
            Assert.That(keys, Does.Contain(ApprovalTaskListRowBuilder.kManagement));
            Assert.That(keys, Does.Contain(ApprovalTaskListRowBuilder.kRuleAction));
            Assert.That(keys.TakeLast(6), Is.EqualTo(new List<string>
            {
                ApprovalTaskListRowBuilder.kFreeText,
                ApprovalTaskListRowBuilder.kTitle,
                ApprovalTaskListRowBuilder.kTaskType,
                ApprovalTaskListRowBuilder.kState,
                ApprovalTaskListRowBuilder.kHandler,
                ApprovalTaskListRowBuilder.kAssigned
            }));
        });
    }

    [Test]
    public void VisibleColumns_TitleHandlerAndAssignedHiddenOnlyInAccessTable()
    {
        AccessTaskFieldVisibilityConfig visibility = new() { ShowTitle = false, ShowHandler = false, ShowAssigned = false };

        List<string> access = AccessColumnKeys(visibility);
        List<string> address = [.. ApprovalTaskListRowBuilder.VisibleColumns(ApprovalTaskListTable.Address, visibility).Select(column => column.Key)];

        Assert.Multiple(() =>
        {
            Assert.That(access, Does.Not.Contain(ApprovalTaskListRowBuilder.kTitle));
            Assert.That(access, Does.Not.Contain(ApprovalTaskListRowBuilder.kHandler));
            Assert.That(access, Does.Not.Contain(ApprovalTaskListRowBuilder.kAssigned));
            Assert.That(access.TakeLast(2), Is.EqualTo(new List<string>
            {
                ApprovalTaskListRowBuilder.kTaskType,
                ApprovalTaskListRowBuilder.kState
            }));
            Assert.That(address, Does.Contain(ApprovalTaskListRowBuilder.kTitle));
            Assert.That(address, Does.Contain(ApprovalTaskListRowBuilder.kHandler));
            Assert.That(address, Does.Contain(ApprovalTaskListRowBuilder.kAssigned));
        });
    }

    [Test]
    public void VisibleColumns_AddressAndServiceTablesShowOnlyObjectRelevantColumns()
    {
        AccessTaskFieldVisibilityConfig visibility = new() { ShowFreeText = true };

        List<string> address = [.. ApprovalTaskListRowBuilder.VisibleColumns(ApprovalTaskListTable.Address, visibility).Select(column => column.Key)];
        List<string> service = [.. ApprovalTaskListRowBuilder.VisibleColumns(ApprovalTaskListTable.Service, visibility).Select(column => column.Key)];

        Assert.Multiple(() =>
        {
            Assert.That(address, Is.EqualTo(new List<string>
            {
                ApprovalTaskListRowBuilder.kId, ApprovalTaskListRowBuilder.kTaskType, ApprovalTaskListRowBuilder.kState,
                ApprovalTaskListRowBuilder.kTitle, ApprovalTaskListRowBuilder.kObject, ApprovalTaskListRowBuilder.kContent,
                ApprovalTaskListRowBuilder.kZone, ApprovalTaskListRowBuilder.kManagement,
                ApprovalTaskListRowBuilder.kReason, ApprovalTaskListRowBuilder.kHandler, ApprovalTaskListRowBuilder.kAssigned
            }));
            Assert.That(service, Does.Not.Contain(ApprovalTaskListRowBuilder.kZone));
            Assert.That(service, Does.Not.Contain(ApprovalTaskListRowBuilder.kRuleAction));
            Assert.That(service, Does.Not.Contain(ApprovalTaskListRowBuilder.kTrack));
            Assert.That(service, Does.Contain(ApprovalTaskListRowBuilder.kContent));
        });
    }

    [Test]
    public void VisibleColumns_HidesTimeRestrictionOnlyWhenBothDatesAreHidden()
    {
        AccessTaskFieldVisibilityConfig onlyEnd = new() { ShowValidFrom = false, ShowValidTo = true };
        AccessTaskFieldVisibilityConfig none = new() { ShowValidFrom = false, ShowValidTo = false };

        Assert.Multiple(() =>
        {
            Assert.That(AccessColumnKeys(onlyEnd), Does.Contain(ApprovalTaskListRowBuilder.kTimeRestriction));
            Assert.That(AccessColumnKeys(none), Does.Not.Contain(ApprovalTaskListRowBuilder.kTimeRestriction));
        });
    }

    [Test]
    public void TableOf_SortsTasksIntoAccessAddressServiceAndOtherTables()
    {
        WfReqTask networkObject = ObjectTask(1, ElemFieldType.source);
        WfReqTask serviceObject = ObjectTask(2, ElemFieldType.service);
        WfReqTask timeObject = ObjectTask(3, ElemFieldType.time);
        WfReqTask networkGroup = new()
        {
            TaskType = WfTaskType.group_create.ToString(),
            Elements = [new WfReqElement { Field = ElemFieldType.source.ToString() }]
        };
        WfReqTask serviceGroup = new()
        {
            TaskType = WfTaskType.group_modify.ToString(),
            Elements = [new WfReqElement { Field = ElemFieldType.service.ToString() }]
        };

        Assert.Multiple(() =>
        {
            Assert.That(ApprovalTaskListRowBuilder.TableOf(CreateAccessTask()), Is.EqualTo(ApprovalTaskListTable.Access));
            Assert.That(ApprovalTaskListRowBuilder.TableOf(networkObject), Is.EqualTo(ApprovalTaskListTable.Address));
            Assert.That(ApprovalTaskListRowBuilder.TableOf(networkGroup), Is.EqualTo(ApprovalTaskListTable.Address));
            Assert.That(ApprovalTaskListRowBuilder.TableOf(serviceObject), Is.EqualTo(ApprovalTaskListTable.Service));
            Assert.That(ApprovalTaskListRowBuilder.TableOf(serviceGroup), Is.EqualTo(ApprovalTaskListTable.Service));
            Assert.That(ApprovalTaskListRowBuilder.TableOf(timeObject), Is.EqualTo(ApprovalTaskListTable.Other));
            Assert.That(ApprovalTaskListRowBuilder.TableOf(new WfReqTask { TaskType = WfTaskType.generic.ToString() }),
                Is.EqualTo(ApprovalTaskListTable.Other));
        });
    }

    [Test]
    public void Build_CreatesOneRowPerTaskOfTheTableWithElementValues()
    {
        WfReqTask task = CreateAccessTask();
        WfReqTask secondTask = new() { Id = 8, Title = "second", TaskType = WfTaskType.generic.ToString(), StateId = 2 };
        ApprovalTaskListRowBuilder builder = CreateBuilder(task, kNoFields);

        List<ApprovalTaskListRow> rows = builder.Build(Groups(task, secondTask), ApprovalTaskListTable.Access);
        List<ApprovalTaskListRow> otherRows = builder.Build(Groups(task, secondTask), ApprovalTaskListTable.Other);
        ApprovalTaskListRow row = rows[0];

        Assert.Multiple(() =>
        {
            Assert.That(rows, Has.Count.EqualTo(1));
            Assert.That(otherRows.Single().Task, Is.SameAs(secondTask));
            Assert.That(row.Cell(ApprovalTaskListRowBuilder.kId).Text, Is.EqualTo("7"));
            Assert.That(row.Cell(ApprovalTaskListRowBuilder.kTitle).Text, Is.EqualTo("web access"));
            Assert.That(row.Cell(ApprovalTaskListRowBuilder.kSourceZone).Text, Is.EqualTo("internal"));
            Assert.That(row.Cell(ApprovalTaskListRowBuilder.kDestinationZone).Values, Is.Empty);
            Assert.That(row.Cell(ApprovalTaskListRowBuilder.kSource).Values, Has.Count.EqualTo(2));
            Assert.That(row.Cell(ApprovalTaskListRowBuilder.kSource).Values[0], Does.StartWith("client-a (").And.Contain("10.0.0.1"));
            Assert.That(row.Cell(ApprovalTaskListRowBuilder.kDestination).Text, Does.StartWith("web-server (").And.Contain("192.168.1.10"));
            Assert.That(row.Cell(ApprovalTaskListRowBuilder.kService).Text, Does.Contain("https"));
            Assert.That(row.Cell(ApprovalTaskListRowBuilder.kState).Text, Is.EqualTo("Approval"));
            Assert.That(row.Cell(ApprovalTaskListRowBuilder.kHandler).Text, Is.EqualTo("alice"));
            Assert.That(row.Cell(ApprovalTaskListRowBuilder.kReason).Text, Is.EqualTo("business need"));
            Assert.That(row.Cell("unknown"), Is.SameAs(ApprovalTaskListCell.Empty));
        });
    }

    [Test]
    public void Build_OffersEditorsOnlyForAllowedFields()
    {
        WfReqTask task = CreateAccessTask();
        ApprovalTaskListRowBuilder builder = CreateBuilder(task, kTitleAndSource);

        ApprovalTaskListRow row = builder.Build(Groups(task), ApprovalTaskListTable.Access)[0];

        Assert.Multiple(() =>
        {
            Assert.That(row.Cell(ApprovalTaskListRowBuilder.kTitle).CanEdit, Is.True);
            Assert.That(row.Cell(ApprovalTaskListRowBuilder.kSource).CanEdit, Is.True);
            Assert.That(row.Cell(ApprovalTaskListRowBuilder.kSourceZone).CanEdit, Is.True);
            Assert.That(row.Cell(ApprovalTaskListRowBuilder.kDestination).CanEdit, Is.False);
            Assert.That(row.Cell(ApprovalTaskListRowBuilder.kService).CanEdit, Is.False);
            Assert.That(row.Cell(ApprovalTaskListRowBuilder.kReason).CanEdit, Is.False);
            Assert.That(row.Cell(ApprovalTaskListRowBuilder.kId).CanEdit, Is.False);
            Assert.That(row.Cell(ApprovalTaskListRowBuilder.kState).CanEdit, Is.False);
        });
    }

    [Test]
    public async Task Build_EditCallbacksOpenTheRespectiveEditors()
    {
        WfReqTask task = CreateAccessTask();
        List<string> allowed =
        [
            WorkflowEditableFieldKeys.Title, WorkflowEditableFieldKeys.Destination, WorkflowEditableFieldKeys.Services,
            WorkflowEditableFieldKeys.Owner, WorkflowEditableFieldKeys.Management, WorkflowEditableFieldKeys.RuleAction,
            WorkflowEditableFieldKeys.Track, WorkflowEditableFieldKeys.Reason, WorkflowEditableFieldKeys.FreeText
        ];
        ApprovalTaskListRowBuilder builder = CreateBuilder(task, allowed, canEditTimeObject: true);
        ApprovalTaskListRow row = builder.Build(Groups(task), ApprovalTaskListTable.Access)[0];

        foreach (string key in new List<string>
        {
            ApprovalTaskListRowBuilder.kTitle, ApprovalTaskListRowBuilder.kDestination, ApprovalTaskListRowBuilder.kService,
            ApprovalTaskListRowBuilder.kOwner, ApprovalTaskListRowBuilder.kManagement, ApprovalTaskListRowBuilder.kRuleAction,
            ApprovalTaskListRowBuilder.kTrack, ApprovalTaskListRowBuilder.kTimeRestriction, ApprovalTaskListRowBuilder.kReason,
            ApprovalTaskListRowBuilder.kFreeText
        })
        {
            await row.Cell(key).Edit!();
        }

        Assert.That(editCalls, Is.EqualTo(new List<string>
        {
            $"text:{WorkflowEditableFieldKeys.Title}:web access",
            $"field:{WorkflowEditableFieldKeys.Destination}",
            $"field:{WorkflowEditableFieldKeys.Services}",
            "owner", "management", "rule_action", "track", "dates:valid_to",
            $"text:{WorkflowEditableFieldKeys.Reason}:business need",
            $"text:{WorkflowEditableFieldKeys.FreeText}:"
        }));
    }

    [Test]
    public async Task Build_AddressGroupShowsMembersAndOpensGroupEditors()
    {
        WfReqTask task = new()
        {
            Id = 20,
            TaskType = WfTaskType.group_modify.ToString(),
            Elements =
            [
                new WfReqElement
                {
                    Field = ElemFieldType.source.ToString(),
                    Name = "member",
                    GroupName = "grp-web",
                    IpString = "10.1.1.1",
                    RequestAction = RequestAction.create.ToString()
                },
                new WfReqElement
                {
                    Field = ElemFieldType.source.ToString(),
                    Name = "old-member",
                    GroupName = "grp-web",
                    IpString = "172.16.1.1",
                    RequestAction = RequestAction.delete.ToString()
                },
                new WfReqElement
                {
                    Field = ElemFieldType.source.ToString(),
                    Name = "kept-member",
                    GroupName = "grp-web",
                    IpString = "10.1.1.5",
                    RequestAction = RequestAction.unchanged.ToString()
                }
            ]
        };
        ApprovalTaskListRowBuilder builder = CreateBuilder(task, kGroupMembersAndName);

        ApprovalTaskListRow row = builder.Build(Groups(task), ApprovalTaskListTable.Address)[0];
        await row.Cell(ApprovalTaskListRowBuilder.kContent).Edit!();
        await row.Cell(ApprovalTaskListRowBuilder.kObject).Edit!();

        Assert.Multiple(() =>
        {
            Assert.That(row.Cell(ApprovalTaskListRowBuilder.kObject).Text, Is.EqualTo("grp-web"));
            Assert.That(row.Cell(ApprovalTaskListRowBuilder.kContent).Values, Has.Count.EqualTo(3));
            Assert.That(row.Cell(ApprovalTaskListRowBuilder.kContent).Values[0], Does.StartWith("member (").And.Contain("10.1.1.1"));
            Assert.That(row.Cell(ApprovalTaskListRowBuilder.kContent).Values[1], Does.StartWith("old-member ("));
            Assert.That(row.Cell(ApprovalTaskListRowBuilder.kContent).Badge(0), Is.EqualTo(new ApprovalTaskListBadge("add_member", "text-bg-success")));
            Assert.That(row.Cell(ApprovalTaskListRowBuilder.kContent).Badge(1), Is.EqualTo(new ApprovalTaskListBadge("remove_member", "text-bg-danger")));
            Assert.That(row.Cell(ApprovalTaskListRowBuilder.kContent).Badge(2), Is.EqualTo(new ApprovalTaskListBadge("unchanged", "text-bg-secondary")));
            Assert.That(row.Cell(ApprovalTaskListRowBuilder.kContent).Text, Does.Contain("(remove_member)"));
            Assert.That(row.Cell(ApprovalTaskListRowBuilder.kZone).Text, Is.EqualTo("internal"));
            Assert.That(row.Cell(ApprovalTaskListRowBuilder.kRuleAction).Values, Is.Empty);
            Assert.That(row.Cell(ApprovalTaskListRowBuilder.kTrack).Values, Is.Empty);
            Assert.That(editCalls, Is.EqualTo(new List<string> { "group_members", $"text:{WorkflowEditableFieldKeys.Name}:grp-web" }));
        });
    }

    [Test]
    public void Build_CreatedGroupShowsMembersWithoutChangeBadges()
    {
        WfReqTask task = new()
        {
            Id = 25,
            TaskType = WfTaskType.group_create.ToString(),
            Elements =
            [
                new WfReqElement
                {
                    Field = ElemFieldType.source.ToString(),
                    Name = "member",
                    GroupName = "grp-new",
                    IpString = "10.1.1.1",
                    RequestAction = RequestAction.create.ToString()
                }
            ]
        };
        ApprovalTaskListRowBuilder builder = CreateBuilder(task, kNoFields);

        ApprovalTaskListCell content = builder.Build(Groups(task), ApprovalTaskListTable.Address)[0].Cell(ApprovalTaskListRowBuilder.kContent);

        Assert.Multiple(() =>
        {
            Assert.That(content.Values, Has.Count.EqualTo(1));
            Assert.That(content.Badges, Is.Null);
            Assert.That(content.Text, Does.Not.Contain("add_member"));
        });
    }

    [Test]
    public async Task Build_ModifiedNetworkObjectShowsChangeInContent()
    {
        WfReqTask task = new()
        {
            Id = 21,
            TaskType = WfTaskType.object_modify.ToString(),
            Elements =
            [
                new WfReqElement
                {
                    Field = ElemFieldType.source.ToString(),
                    Name = "host",
                    IpString = "10.1.1.2",
                    RequestAction = RequestAction.unchanged.ToString()
                },
                new WfReqElement
                {
                    Field = ElemFieldType.source.ToString(),
                    Name = "host",
                    IpString = "10.1.1.3",
                    RequestAction = RequestAction.modify.ToString()
                }
            ]
        };
        ApprovalTaskListRowBuilder builder = CreateBuilder(task, kContentOnly);

        ApprovalTaskListRow row = builder.Build(Groups(task), ApprovalTaskListTable.Address)[0];
        await row.Cell(ApprovalTaskListRowBuilder.kContent).Edit!();

        Assert.Multiple(() =>
        {
            Assert.That(row.Cell(ApprovalTaskListRowBuilder.kObject).Text, Is.EqualTo("host"));
            Assert.That(row.Cell(ApprovalTaskListRowBuilder.kContent).Text, Does.Match("10\\.1\\.1\\.2.* \u2192 10\\.1\\.1\\.3"));
            Assert.That(row.Cell(ApprovalTaskListRowBuilder.kContent).Badges, Is.Null);
            Assert.That(row.Cell(ApprovalTaskListRowBuilder.kZone).Text, Is.EqualTo("internal"));
            Assert.That(row.Cell(ApprovalTaskListRowBuilder.kRuleAction).Values, Is.Empty);
            Assert.That(editCalls, Is.EqualTo(new List<string> { "object_task" }));
        });
    }

    [Test]
    public void Build_ServiceObjectShowsServiceContent()
    {
        WfReqTask task = new()
        {
            Id = 23,
            TaskType = WfTaskType.object_create.ToString(),
            Elements =
            [
                new WfReqElement
                {
                    Field = ElemFieldType.service.ToString(),
                    Name = "web",
                    Port = 8080,
                    ProtoId = 6,
                    RequestAction = RequestAction.create.ToString()
                }
            ]
        };
        ApprovalTaskListRowBuilder builder = CreateBuilder(task, kNoFields);

        ApprovalTaskListRow row = builder.Build(Groups(task), ApprovalTaskListTable.Service)[0];

        Assert.Multiple(() =>
        {
            Assert.That(row.Cell(ApprovalTaskListRowBuilder.kObject).Text, Is.EqualTo("web"));
            Assert.That(row.Cell(ApprovalTaskListRowBuilder.kContent).Text, Does.Contain("8080"));
            Assert.That(row.Cell(ApprovalTaskListRowBuilder.kContent).CanEdit, Is.False);
            Assert.That(row.Cell(ApprovalTaskListRowBuilder.kContent).Text, Does.Not.Contain("\u2192"));
        });
    }

    [Test]
    public async Task Build_TimeObjectTaskInOtherTableOpensObjectTaskEditor()
    {
        WfReqTask task = ObjectTask(24, ElemFieldType.time);
        task.Elements[0].Name = "office-hours";
        ApprovalTaskListRowBuilder builder = CreateBuilder(task, kContentOnly);

        ApprovalTaskListRow row = builder.Build(Groups(task), ApprovalTaskListTable.Other)[0];
        await row.Cell(ApprovalTaskListRowBuilder.kObject).Edit!();

        Assert.Multiple(() =>
        {
            Assert.That(row.Cell(ApprovalTaskListRowBuilder.kObject).Text, Is.EqualTo("office-hours"));
            Assert.That(editCalls, Is.EqualTo(new List<string> { "object_task" }));
        });
    }

    [Test]
    public void Build_GenericTaskElementsAreReadOnly()
    {
        WfReqTask task = new() { Id = 22, TaskType = WfTaskType.generic.ToString() };
        ApprovalTaskListRowBuilder builder = CreateBuilder(task, kSourceOnly);

        ApprovalTaskListRow row = builder.Build(Groups(task), ApprovalTaskListTable.Other)[0];

        Assert.Multiple(() =>
        {
            Assert.That(row.Cell(ApprovalTaskListRowBuilder.kObject).CanEdit, Is.False);
            Assert.That(row.Cell(ApprovalTaskListRowBuilder.kObject).Values, Is.Empty);
            Assert.That(row.Cell(ApprovalTaskListRowBuilder.kTaskType).Text, Is.EqualTo("generic"));
        });
    }

    [Test]
    public void Build_MergesBundledTasksIntoOneRow()
    {
        WfReqTask task = CreateAccessTask();
        WfReqTask bundledTask = new()
        {
            Id = 9,
            StateId = 2,
            Title = "web access",
            TaskType = WfTaskType.access.ToString(),
            Reason = "second reason",
            Elements = [new WfReqElement { Field = ElemFieldType.source.ToString(), Name = "client-c", IpString = "10.0.0.3" }]
        };
        ApprovalTaskListRowBuilder builder = CreateBuilder(task, kNoFields);
        List<List<WfReqTask>> groups = [[task, bundledTask]];

        List<ApprovalTaskListRow> rows = builder.Build(groups, ApprovalTaskListTable.Access);

        Assert.Multiple(() =>
        {
            Assert.That(rows, Has.Count.EqualTo(1));
            Assert.That(rows[0].Task, Is.SameAs(task));
            Assert.That(rows[0].Cell(ApprovalTaskListRowBuilder.kId).Text, Does.Contain("7").And.Contain("9"));
            Assert.That(rows[0].Cell(ApprovalTaskListRowBuilder.kTitle).Values, Has.Count.EqualTo(1));
            Assert.That(rows[0].Cell(ApprovalTaskListRowBuilder.kSource).Values, Has.Count.EqualTo(3));
            Assert.That(rows[0].Cell(ApprovalTaskListRowBuilder.kReason).Values,
                Is.EqualTo(new List<string> { "business need", "second reason" }));
        });
    }

    [Test]
    public void VisibleColumns_ComplianceDirectlyAfterIdOnlyInAccessTable()
    {
        AccessTaskFieldVisibilityConfig visibility = new();

        List<string> access = [.. ApprovalTaskListRowBuilder.VisibleColumns(ApprovalTaskListTable.Access, visibility, true).Select(column => column.Key)];
        List<string> address = [.. ApprovalTaskListRowBuilder.VisibleColumns(ApprovalTaskListTable.Address, visibility, true).Select(column => column.Key)];

        Assert.Multiple(() =>
        {
            Assert.That(access.Take(3), Is.EqualTo(new List<string>
            {
                ApprovalTaskListRowBuilder.kId,
                ApprovalTaskListRowBuilder.kCompliance,
                ApprovalTaskListRowBuilder.kSourceZone
            }));
            Assert.That(address, Does.Not.Contain(ApprovalTaskListRowBuilder.kCompliance));
            Assert.That(AccessColumnKeys(visibility), Does.Not.Contain(ApprovalTaskListRowBuilder.kCompliance));
        });
    }

    [Test]
    public void Build_MergesComplianceOfBundledTasks()
    {
        WfReqTask task = CreateAccessTask();
        WfReqTask bundledTask = new() { Id = 9, TaskType = WfTaskType.access.ToString() };
        WfReqTask uncheckedTask = new() { Id = 10, TaskType = WfTaskType.access.ToString() };
        Dictionary<long, RequestTaskComplianceResult> results = new()
        {
            [7] = new(7, true, ApprovalComplianceTestHelper.NoReasons),
            [9] = new(9, false, kViolationReasons)
        };
        ApprovalTaskListRowBuilder builder = CreateBuilder(task, kNoFields, complianceResults: results);
        List<List<WfReqTask>> groups = [[task, bundledTask], [uncheckedTask]];

        List<ApprovalTaskListRow> rows = builder.Build(groups, ApprovalTaskListTable.Access);

        Assert.Multiple(() =>
        {
            Assert.That(rows[0].Compliance, Is.Not.Null);
            Assert.That(rows[0].Compliance!.IsCompliant, Is.False);
            Assert.That(rows[0].Compliance!.Reasons, Is.EqualTo(kViolationReasons));
            Assert.That(rows[1].Compliance, Is.Null);
        });
    }

    [Test]
    public void Build_OrdersObjectTasksBeforeGroupTasks()
    {
        WfReqTask groupTask = new()
        {
            Id = 2,
            TaskType = WfTaskType.group_create.ToString(),
            Elements = [new WfReqElement { Field = ElemFieldType.source.ToString(), Name = "m", IpString = "10.0.0.9" }]
        };
        WfReqTask laterObjectTask = ObjectTask(5, ElemFieldType.source);
        WfReqTask objectTask = ObjectTask(3, ElemFieldType.source);
        ApprovalTaskListRowBuilder builder = CreateBuilder(groupTask, kNoFields);

        List<long> ids = [.. builder.Build(Groups(groupTask, laterObjectTask, objectTask), ApprovalTaskListTable.Address)
            .Select(row => row.Task.Id)];

        Assert.That(ids, Is.EqualTo(new List<long> { 3, 5, 2 }));
    }

    private static List<string> AccessColumnKeys(AccessTaskFieldVisibilityConfig visibility)
    {
        return [.. ApprovalTaskListRowBuilder.VisibleColumns(ApprovalTaskListTable.Access, visibility).Select(column => column.Key)];
    }

    private static WfReqTask ObjectTask(long id, ElemFieldType field)
    {
        return new WfReqTask
        {
            Id = id,
            TaskType = WfTaskType.object_create.ToString(),
            Elements = [new WfReqElement { Field = field.ToString(), RequestAction = RequestAction.create.ToString() }]
        };
    }

    private static List<List<WfReqTask>> Groups(params List<WfReqTask> tasks)
    {
        return [.. tasks.Select(task => new List<WfReqTask> { task })];
    }

    private static WfReqTask CreateAccessTask()
    {
        return new WfReqTask
        {
            Id = 7,
            StateId = 2,
            Title = "web access",
            TaskType = WfTaskType.access.ToString(),
            Reason = "business need",
            CurrentHandler = new UiUser { Name = "alice" },
            Elements =
            [
                new WfReqElement { Field = ElemFieldType.source.ToString(), Name = "client-a", IpString = "10.0.0.1" },
                new WfReqElement { Field = ElemFieldType.source.ToString(), Name = "client-b", IpString = "10.0.0.2" },
                new WfReqElement { Field = ElemFieldType.destination.ToString(), Name = "web-server", IpString = "192.168.1.10" },
                new WfReqElement { Field = ElemFieldType.service.ToString(), Name = "https", Port = 443, ProtoId = 6 }
            ]
        };
    }

    private ApprovalTaskListRowBuilder CreateBuilder(WfReqTask task, List<string> allowedFields, bool canEditTimeObject = false,
        IReadOnlyDictionary<long, RequestTaskComplianceResult>? complianceResults = null)
    {
        SimulatedUserConfig userConfig = new();
        WfHandler handler = new() { ActTicket = new WfTicket { Tasks = [task] } };
        ApprovalTaskDisplayLogic displayLogic = new(handler, userConfig, kNoRuleActions, kNoTrackings, kProtocols, kZones,
            ApprovalTaskElementDisplayMode.Resolved);
        ApprovalTaskViewActions actions = new()
        {
            CanEdit = (_, fieldKey) => allowedFields.Contains(fieldKey),
            CanEditTimeObject = _ => canEditTimeObject,
            EditableTimeObjectField = _ => "valid_to",
            EditOwner = _ => Record("owner"),
            EditRuleAction = _ => Record("rule_action"),
            EditTracking = _ => Record("track"),
            EditManagement = _ => Record("management"),
            EditField = (_, fieldKey) => Record($"field:{fieldKey}"),
            EditGroupMembers = _ => Record("group_members"),
            EditObjectTask = _ => Record("object_task"),
            EditText = (_, fieldKey, value) => Record($"text:{fieldKey}:{value}"),
            EditTargetDates = (_, fieldKey) => Record($"dates:{fieldKey}")
        };
        ApprovalTaskViewContext context = new(displayLogic, new AccessTaskFieldVisibilityConfig(), kProtocols, actions);
        return new ApprovalTaskListRowBuilder(context, userConfig, new WfStateDict { Name = { [2] = "Approval" } })
        {
            ComplianceResults = complianceResults
        };
    }

    private Task Record(string call)
    {
        editCalls.Add(call);
        return Task.CompletedTask;
    }
}
