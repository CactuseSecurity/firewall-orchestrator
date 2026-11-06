using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Flow;
using FWO.Data.Workflow;
using FWO.Services.Workflow;
using FWO.Ui.Services;
using NUnit.Framework;

namespace FWO.Test;

[TestFixture]
internal class UiApprovalTaskDisplayLogicTest
{
    [Test]
    public void RuleElementValues_ResolvesTicketObjectReferencesInEveryMode()
    {
        WfReqTask objectTask = new()
        {
            Id = 100,
            TaskType = WfTaskType.object_create.ToString(),
            Elements = [new WfReqElement
            {
                Field = ElemFieldType.source.ToString(),
                Name = "current-server",
                IpString = "192.0.2.20",
                RequestAction = RequestAction.create.ToString()
            }]
        };
        WfReqTask accessTask = new()
        {
            Id = 101,
            TaskType = WfTaskType.access.ToString(),
            Elements = [new WfReqElement
            {
                Field = ElemFieldType.source.ToString(),
                Name = "stale-server",
                IpString = "192.0.2.10",
                ObjectTaskId = objectTask.Id
            }]
        };
        WfHandler handler = new() { ActTicket = new WfTicket { Tasks = [objectTask, accessTask] } };

        List<string> resolved = GetValues(handler, accessTask, ApprovalTaskElementDisplayMode.Resolved);
        List<string> technical = GetValues(handler, accessTask, ApprovalTaskElementDisplayMode.Technical);
        List<string> unresolved = GetValues(handler, accessTask, ApprovalTaskElementDisplayMode.UnresolvedGroups);

        Assert.Multiple(() =>
        {
            Assert.That(resolved, Is.EqualTo(new List<string> { "current-server (192.0.2.20)" }));
            Assert.That(technical, Is.EqualTo(new List<string> { "192.0.2.20" }));
            Assert.That(unresolved, Is.EqualTo(new List<string> { "current-server (192.0.2.20)" }));
        });
    }

    [Test]
    public void RuleElementValues_DeduplicatesBeforeFormattingSoModesKeepObjectCount()
    {
        WfReqTask accessTask = new()
        {
            Id = 102,
            TaskType = WfTaskType.access.ToString(),
            Elements =
            [
                new WfReqElement
                {
                    Field = ElemFieldType.source.ToString(),
                    Name = "server-a",
                    IpString = "192.0.2.30"
                },
                new WfReqElement
                {
                    Field = ElemFieldType.source.ToString(),
                    Name = "server-b",
                    IpString = "192.0.2.30"
                }
            ]
        };
        WfHandler handler = new() { ActTicket = new WfTicket { Tasks = [accessTask] } };

        List<string> resolved = GetValues(handler, accessTask, ApprovalTaskElementDisplayMode.Resolved);
        List<string> technical = GetValues(handler, accessTask, ApprovalTaskElementDisplayMode.Technical);
        List<string> unresolved = GetValues(handler, accessTask, ApprovalTaskElementDisplayMode.UnresolvedGroups);

        Assert.Multiple(() =>
        {
            Assert.That(resolved, Has.Count.EqualTo(2));
            Assert.That(technical, Has.Count.EqualTo(2));
            Assert.That(unresolved, Has.Count.EqualTo(2));
        });
    }

    [Test]
    public void RuleElementValues_DoesNotExpandConcreteObjectWithMatchingGroupName()
    {
        WfReqTask groupTask = new()
        {
            Id = 103,
            TaskType = WfTaskType.group_create.ToString(),
            Elements = [new WfReqElement
            {
                Field = ElemFieldType.source.ToString(),
                GroupName = "server",
                Name = "group-member",
                IpString = "192.0.2.40"
            }]
        };
        groupTask.SetAddInfo(AdditionalInfoKeys.GrpName, "server");
        WfReqTask accessTask = new()
        {
            Id = 104,
            TaskType = WfTaskType.access.ToString(),
            Elements = [new WfReqElement
            {
                Field = ElemFieldType.source.ToString(),
                Name = "server",
                IpString = "192.0.2.41",
                FlowNetworkObjectId = 11
            }]
        };
        WfHandler handler = new() { ActTicket = new WfTicket { Tasks = [groupTask, accessTask] } };

        List<string> values = GetValues(handler, accessTask, ApprovalTaskElementDisplayMode.Resolved);

        Assert.That(values, Is.EqualTo(new List<string> { "server (192.0.2.41)" }));
    }

    [Test]
    public void MetadataAndTimeDisplay_DeduplicatesValuesAndUsesConfiguredVisibility()
    {
        DateTime begin = new(2026, 8, 1, 8, 0, 0);
        DateTime end = new(2026, 8, 1, 17, 0, 0);
        WfReqTask first = new()
        {
            Id = 110,
            TaskType = WfTaskType.access.ToString(),
            CurrentHandler = new UiUser { Name = "handler" },
            AssignedGroup = "approvers",
            ManagementId = 8,
            RuleAction = 3,
            Tracking = 4,
            Elements = [new WfReqElement
            {
                Field = ElemFieldType.time.ToString(),
                Name = "maintenance",
                StartTime = begin,
                EndTime = end
            }]
        };
        WfReqTask second = new()
        {
            Id = 111,
            TaskType = WfTaskType.access.ToString(),
            CurrentHandler = new UiUser { Name = "handler" },
            AssignedGroup = "approvers",
            ManagementId = 8,
            RuleAction = 3,
            Tracking = 4,
            Elements = [new WfReqElement
            {
                Field = ElemFieldType.time.ToString(),
                Name = "maintenance",
                StartTime = begin,
                EndTime = end
            }]
        };
        ApprovalTaskDisplayLogic logic = new(
            new WfHandler { ActTicket = new WfTicket { Tasks = [first, second] } },
            new SimulatedUserConfig(),
            [new RuleAction { Id = 3, Name = "allow" }],
            [new Tracking { Id = 4, Name = "log" }],
            [],
            [],
            false);

        Assert.Multiple(() =>
        {
            Assert.That(logic.Handlers([first, second]), Is.EqualTo("handler"));
            Assert.That(logic.AssignedGroups([first, second]), Is.EqualTo("approvers"));
            Assert.That(logic.ManagementIds([first, second]), Is.EqualTo("8"));
            Assert.That(logic.RuleActions([first, second]), Is.EqualTo("allow"));
            Assert.That(logic.Trackings([first, second]), Is.EqualTo("log"));
            Assert.That(logic.TimeObjectDisplay([first, second], new AccessTaskFieldVisibilityConfig
            {
                ShowValidFrom = true,
                ShowValidTo = false
            }), Is.EqualTo("maintenance (01.08.2026 08:00:00)"));
        });
    }

    [Test]
    public void RuleElementValues_ExpandsTicketGroupsAndKeepsUnresolvedGroupView()
    {
        WfReqTask groupTask = new()
        {
            Id = 120,
            TaskType = WfTaskType.group_modify.ToString(),
            Elements = [new WfReqElement
            {
                Field = ElemFieldType.source.ToString(),
                GroupName = "web-group",
                Name = "web-1",
                IpString = "192.0.2.51",
                RequestAction = RequestAction.create.ToString()
            }]
        };
        groupTask.SetAddInfo(AdditionalInfoKeys.GrpName, "web-group");
        WfReqTask accessTask = new()
        {
            Id = 121,
            TaskType = WfTaskType.access.ToString(),
            Elements = [new WfReqElement
            {
                Field = ElemFieldType.source.ToString(),
                GroupName = "web-group",
                Name = "web-group",
                ObjectTaskId = groupTask.Id
            }]
        };
        WfHandler handler = new() { ActTicket = new WfTicket { Tasks = [groupTask, accessTask] } };

        List<string> resolved = GetValues(handler, accessTask, ApprovalTaskElementDisplayMode.Resolved);
        List<string> unresolved = GetValues(handler, accessTask, ApprovalTaskElementDisplayMode.UnresolvedGroups);

        Assert.Multiple(() =>
        {
            Assert.That(resolved, Does.Contain("web-1 (192.0.2.51)"));
            Assert.That(unresolved, Does.Contain("web-group"));
        });
    }

    [Test]
    public void RuleElementValues_ResolvedGroupViewOmitsMembersMarkedForDeletion()
    {
        WfReqTask groupTask = new()
        {
            Id = 122,
            TaskType = WfTaskType.group_modify.ToString(),
            Elements =
            [
                new WfReqElement
                {
                    Field = ElemFieldType.source.ToString(),
                    GroupName = "web-group",
                    Name = "old-web-1",
                    IpString = "192.0.2.52",
                    RequestAction = RequestAction.delete.ToString()
                },
                new WfReqElement
                {
                    Field = ElemFieldType.source.ToString(),
                    GroupName = "web-group",
                    Name = "new-web-1",
                    IpString = "192.0.2.53",
                    RequestAction = RequestAction.create.ToString()
                }
            ]
        };
        groupTask.SetAddInfo(AdditionalInfoKeys.GrpName, "web-group");
        WfReqTask accessTask = new()
        {
            Id = 123,
            TaskType = WfTaskType.access.ToString(),
            Elements = [new WfReqElement
            {
                Field = ElemFieldType.source.ToString(),
                GroupName = "web-group",
                Name = "web-group",
                ObjectTaskId = groupTask.Id
            }]
        };
        WfHandler handler = new() { ActTicket = new WfTicket { Tasks = [groupTask, accessTask] } };

        List<string> resolved = GetValues(handler, accessTask, ApprovalTaskElementDisplayMode.Resolved);
        List<string> technical = GetValues(handler, accessTask, ApprovalTaskElementDisplayMode.Technical);
        List<string> unresolved = GetValues(handler, accessTask, ApprovalTaskElementDisplayMode.UnresolvedGroups);

        Assert.Multiple(() =>
        {
            Assert.That(resolved, Is.EqualTo(new List<string> { "new-web-1 (192.0.2.53)" }));
            Assert.That(resolved, Does.Not.Contain("old-web-1 (192.0.2.52)"));
            Assert.That(technical, Is.EqualTo(new List<string> { "192.0.2.53" }));
            Assert.That(technical, Does.Not.Contain("192.0.2.52"));
            Assert.That(unresolved, Does.Contain("web-group"));
        });
    }

    [Test]
    public void RuleElementValues_ResolvesServicesAndTechnicalFallbacks()
    {
        WfReqTask task = new()
        {
            Id = 130,
            TaskType = WfTaskType.access.ToString(),
            Elements = [new WfReqElement
            {
                Field = ElemFieldType.service.ToString(),
                Name = "https",
                Port = 443,
                ProtoId = 6
            }]
        };
        WfHandler handler = new() { ActTicket = new WfTicket { Tasks = [task] } };
        ApprovalTaskDisplayLogic logic = new(
            handler,
            new SimulatedUserConfig(),
            [],
            [],
            [new IpProtocol { Id = 6, Name = "tcp" }],
            [],
            ApprovalTaskElementDisplayMode.Technical);

        List<string> values = [.. logic.RuleElementValues([task], ElemFieldType.service)];

        Assert.That(values, Has.Count.EqualTo(1));
        Assert.That(values[0], Does.Contain("443"));
    }

    [Test]
    public void ElementRowsExposeColumnsForEachAccessDisplayMode()
    {
        WfReqTask task = new()
        {
            Id = 135,
            TaskType = WfTaskType.access.ToString(),
            Elements =
            [
                new WfReqElement
                {
                    Field = ElemFieldType.source.ToString(),
                    Name = "server",
                    IpString = "192.0.2.35"
                },
                new WfReqElement
                {
                    Field = ElemFieldType.service.ToString(),
                    Name = "https",
                    Port = 443,
                    ProtoId = 6
                }
            ]
        };
        WfHandler handler = new() { ActTicket = new WfTicket { Tasks = [task] } };
        SimulatedUserConfig config = new();

        ApprovalTaskDisplayLogic resolved = CreateDisplayLogic(handler, config, ApprovalTaskElementDisplayMode.Resolved);
        ApprovalTaskDisplayLogic technical = CreateDisplayLogic(handler, config, ApprovalTaskElementDisplayMode.Technical);
        ApprovalTaskDisplayLogic unresolved = CreateDisplayLogic(handler, config, ApprovalTaskElementDisplayMode.UnresolvedGroups);

        ApprovalTaskNetworkElementRow resolvedNetwork = resolved.NetworkElementRows([task], ElemFieldType.source).Single();
        ApprovalTaskNetworkElementRow technicalNetwork = technical.NetworkElementRows([task], ElemFieldType.source).Single();
        ApprovalTaskNetworkElementRow unresolvedNetwork = unresolved.NetworkElementRows([task], ElemFieldType.source).Single();
        ApprovalTaskServiceElementRow resolvedService = resolved.ServiceElementRows([task], ElemFieldType.service).Single();
        ApprovalTaskServiceElementRow technicalService = technical.ServiceElementRows([task], ElemFieldType.service).Single();
        ApprovalTaskServiceElementRow unresolvedService = unresolved.ServiceElementRows([task], ElemFieldType.service).Single();

        Assert.Multiple(() =>
        {
            Assert.That(resolvedNetwork, Is.EqualTo(new ApprovalTaskNetworkElementRow("server", "192.0.2.35", "")));
            Assert.That(technicalNetwork, Is.EqualTo(new ApprovalTaskNetworkElementRow("", "192.0.2.35", "")));
            Assert.That(unresolvedNetwork, Is.EqualTo(new ApprovalTaskNetworkElementRow("server", "", "")));
            Assert.That(resolvedService.Name, Is.EqualTo("https"));
            Assert.That(resolvedService.Content, Does.Contain("443"));
            Assert.That(technicalService.Name, Is.Empty);
            Assert.That(technicalService.Content, Does.Contain("443"));
            Assert.That(unresolvedService, Is.EqualTo(new ApprovalTaskServiceElementRow("", "https")));
        });
    }

    [Test]
    public void TimeObjectDisplay_UsesTaskDatesWhenElementIsMissingAndHonorsPrecision()
    {
        WfReqTask task = new()
        {
            Id = 140,
            TaskType = WfTaskType.access.ToString(),
            TargetBeginDate = new DateTime(2026, 8, 1, 8, 15, 30),
            TargetEndDate = new DateTime(2026, 8, 2, 17, 45, 30)
        };
        WfHandler handler = new() { ActTicket = new WfTicket { Tasks = [task] } };

        foreach ((string precision, string expected) in new List<(string precision, string expected)>
        {
            (FlowIntegrationTimePrecisionOptions.Date, "2026-08-01 - 2026-08-02 (01.08.2026 - 02.08.2026)"),
            (FlowIntegrationTimePrecisionOptions.Hours, "2026-08-01 08 h - 2026-08-02 17 h (01.08.2026 8 h - 02.08.2026 17 h)"),
            (FlowIntegrationTimePrecisionOptions.Minutes, "2026-08-01 08:15 - 2026-08-02 17:45 (01.08.2026 08:15 - 02.08.2026 17:45)"),
            (FlowIntegrationTimePrecisionOptions.Seconds, "2026-08-01 08:15:30 - 2026-08-02 17:45:30 (01.08.2026 08:15:30 - 02.08.2026 17:45:30)")
        })
        {
            SimulatedUserConfig config = new()
            {
                ReqFlowIntegration = new FlowIntegrationConfig { TimeObjectPrecision = precision }.ToConfigValue()
            };
            ApprovalTaskDisplayLogic logic = new(handler, config, [], [], [], [], false);
            Assert.That(logic.TimeObjectDisplay([task], new AccessTaskFieldVisibilityConfig()), Is.EqualTo(expected));
        }
    }

    private static List<string> GetValues(WfHandler handler, WfReqTask task, ApprovalTaskElementDisplayMode mode)
    {
        ApprovalTaskDisplayLogic logic = new(
            handler,
            new SimulatedUserConfig(),
            [],
            [],
            [],
            [],
            mode);
        return [.. logic.RuleElementValues([task], ElemFieldType.source)];
    }

    private static ApprovalTaskDisplayLogic CreateDisplayLogic(WfHandler handler, SimulatedUserConfig config,
        ApprovalTaskElementDisplayMode mode) => new(handler, config, [], [], [new IpProtocol { Id = 6, Name = "tcp" }], [], mode);
}
