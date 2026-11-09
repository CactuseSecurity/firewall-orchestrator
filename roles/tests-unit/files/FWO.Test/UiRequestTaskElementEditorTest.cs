using FWO.Data;
using FWO.Data.Workflow;
using FWO.Services.Workflow;
using FWO.Ui.Pages.Request;
using Microsoft.AspNetCore.Components;
using NUnit.Framework;
using System.Reflection;

namespace FWO.Test;

[TestFixture]
internal class UiRequestTaskElementEditorTest
{
    private static readonly long[] kRemovedElementIds = [1L, 2L, 3L];
    private static readonly string[] kPersistedNetworkMemberNames = ["network-member"];
    private static readonly string[] kPersistedServiceMemberNames = ["service-member"];

    [Test]
    public void InitializeElements_LoadsAllRequestElementKinds()
    {
        WfReqTask reqTask = new()
        {
            Id = 42,
            Elements =
            [
                new WfReqElement { Field = ElemFieldType.source.ToString(), Name = "source" },
                new WfReqElement { Field = ElemFieldType.destination.ToString(), Name = "destination" },
                new WfReqElement { Field = ElemFieldType.service.ToString(), Name = "service", ProtoId = 6, Port = 443 },
                new WfReqElement { Field = ElemFieldType.rule.ToString(), RuleUid = "rule-1" }
            ]
        };
        RequestTaskElementEditor component = CreateComponent(reqTask);

        component.InitializeElements();

        Assert.Multiple(() =>
        {
            Assert.That(component.SourceCount, Is.EqualTo(1));
            Assert.That(component.DestinationCount, Is.EqualTo(1));
            Assert.That(component.ServiceCount, Is.EqualTo(1));
            Assert.That(component.RuleCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void InitializeElements_UsesDisplayTaskForBundledReadOnlyView()
    {
        WfReqTask activeTask = new()
        {
            Id = 42,
            Elements = [new WfReqElement { Field = ElemFieldType.destination.ToString(), Name = "active" }]
        };
        WfReqTask displayTask = new()
        {
            Id = 42,
            Elements =
            [
                new WfReqElement { Field = ElemFieldType.source.ToString(), Name = "merged source" },
                new WfReqElement { Field = ElemFieldType.destination.ToString(), Name = "merged destination" }
            ]
        };
        RequestTaskElementEditor component = CreateComponent(activeTask);
        SetMember(component, nameof(RequestTaskElementEditor.DisplayTask), displayTask);

        component.InitializeElements();

        Assert.Multiple(() =>
        {
            Assert.That(component.SourceCount, Is.EqualTo(1));
            Assert.That(component.DestinationCount, Is.EqualTo(1));
            Assert.That(InvokePrivate<bool>(component, "IsEditable"), Is.False);
            Assert.That(activeTask.Elements, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void ApplyChanges_ReconcilesAddedRemovedAndRuleEntries()
    {
        WfReqTask reqTask = new()
        {
            Id = 42,
            RequestAction = RequestAction.modify.ToString(),
            Elements =
            [
                new WfReqElement { Id = 1, TaskId = 42, Field = ElemFieldType.source.ToString(), Name = "old src" },
                new WfReqElement { Id = 2, TaskId = 42, Field = ElemFieldType.destination.ToString(), Name = "old dst" },
                new WfReqElement { Id = 3, TaskId = 42, Field = ElemFieldType.service.ToString(), Port = 443, ProtoId = 6, Name = "old svc" },
                new WfReqElement { Id = 4, TaskId = 42, Field = ElemFieldType.rule.ToString(), RuleUid = "rule-old", DeviceId = 9 }
            ]
        };
        RequestTaskElementEditor component = CreateComponent(reqTask);
        NwObjectElement oldSource = new() { ElemId = 1, TaskId = 42, Name = "old src" };
        NwObjectElement oldDestination = new() { ElemId = 2, TaskId = 42, Name = "old dst" };
        NwServiceElement oldService = new() { ElemId = 3, TaskId = 42, Port = 443, ProtoId = 6, Name = "old svc" };
        SetMember(component, "actSources", new List<NwObjectElement> { oldSource });
        SetMember(component, "sourcesToDelete", new List<NwObjectElement> { oldSource });
        SetMember(component, "sourcesToAdd", new List<NwObjectElement> { new NwObjectElement { ElemId = 5, Name = "new src" } });
        SetMember(component, "actDestinations", new List<NwObjectElement> { oldDestination });
        SetMember(component, "destinationsToDelete", new List<NwObjectElement> { oldDestination });
        SetMember(component, "destinationsToAdd", new List<NwObjectElement> { new NwObjectElement { ElemId = 6, Name = "new dst" } });
        SetMember(component, "actServices", new List<NwServiceElement> { oldService });
        SetMember(component, "servicesToDelete", new List<NwServiceElement> { oldService });
        SetMember(component, "servicesToAdd", new List<NwServiceElement> { new NwServiceElement { ElemId = 7, Port = 80, ProtoId = 6, Name = "new svc" } });
        SetMember(component, "actRules", new List<NwRuleElement> { new NwRuleElement { ElemId = 4, RuleUid = "rule-new" } });
        SetMember(component, nameof(RequestTaskElementEditor.RuleDevice), new Device { Id = 77 });

        component.ApplyChanges();

        Assert.Multiple(() =>
        {
            Assert.That(reqTask.RemovedElements.Select(element => element.Id), Is.EquivalentTo(kRemovedElementIds));
            Assert.That(reqTask.Elements.Any(element => element.Id is 1 or 2 or 3), Is.False);
            Assert.That(reqTask.Elements.Any(element => element.Id == 5 && element.Field == ElemFieldType.source.ToString()), Is.True);
            Assert.That(reqTask.Elements.Any(element => element.Id == 6 && element.Field == ElemFieldType.destination.ToString()), Is.True);
            Assert.That(reqTask.Elements.Any(element => element.Id == 7 && element.Field == ElemFieldType.service.ToString()), Is.True);
            Assert.That(reqTask.Elements.Single(element => element.Field == ElemFieldType.rule.ToString()).DeviceId, Is.EqualTo(77));
        });
    }

    [Test]
    public void ApplyChanges_RemovesOrdinaryAccessElementsAndAddsAllElementKinds()
    {
        WfReqTask reqTask = new()
        {
            Id = 42,
            RequestAction = RequestAction.create.ToString(),
            Elements =
            [
                new WfReqElement { Id = 1, Field = ElemFieldType.source.ToString(), Name = "source" },
                new WfReqElement { Id = 2, Field = ElemFieldType.destination.ToString(), Name = "destination" },
                new WfReqElement { Id = 3, Field = ElemFieldType.service.ToString(), Name = "service", ProtoId = 6, Port = 443 }
            ]
        };
        RequestTaskElementEditor component = CreateComponent(reqTask);
        NwObjectElement source = new() { ElemId = 1, Name = "source" };
        NwObjectElement destination = new() { ElemId = 2, Name = "destination" };
        NwServiceElement service = new() { ElemId = 3, Name = "service", ProtoId = 6, Port = 443 };
        SetMember(component, "actSources", new List<NwObjectElement> { source });
        SetMember(component, "sourcesToDelete", new List<NwObjectElement> { source });
        SetMember(component, "actDestinations", new List<NwObjectElement> { destination });
        SetMember(component, "destinationsToDelete", new List<NwObjectElement> { destination });
        SetMember(component, "actServices", new List<NwServiceElement> { service });
        SetMember(component, "servicesToDelete", new List<NwServiceElement> { service });
        SetMember(component, "sourcesToAdd", new List<NwObjectElement> { new() { Name = "new source" } });
        SetMember(component, "destinationsToAdd", new List<NwObjectElement> { new() { Name = "new destination" } });
        SetMember(component, "servicesToAdd", new List<NwServiceElement> { new() { Name = "new service", ProtoId = 17, Port = 53 } });

        component.ApplyChanges();

        Assert.Multiple(() =>
        {
            Assert.That(reqTask.RemovedElements.Select(element => element.Id), Is.EquivalentTo(kRemovedElementIds));
            Assert.That(reqTask.Elements.Select(element => element.Name), Is.EquivalentTo(new List<string?> { "new source", "new destination", "new service" }));
            Assert.That(reqTask.Elements.All(element => element.TaskId == 42), Is.True);
            Assert.That(component.SourceCount, Is.EqualTo(1));
            Assert.That(component.DestinationCount, Is.EqualTo(1));
            Assert.That(component.ServiceCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void ApplyObjectTask_ReturnsValidationErrorUntilObjectEditorExists()
    {
        RequestTaskElementEditor component = CreateComponent(new WfReqTask());

        Assert.That(component.ApplyObjectTask(), Is.EqualTo("E5124"));
    }

    [Test]
    public void ApplyChanges_GroupCreateNetworkGroup_DoesNotWriteServices()
    {
        WfReqTask reqTask = new() { Id = 42 };
        RequestTaskElementEditor component = CreateComponent(reqTask, WfTaskType.group_create);
        SetMember(component, "grpTypeSvc", false);
        SetMember(component, "sourcesToAdd", new List<NwObjectElement> { new("10.0.0.1", 1) });
        SetMember(component, "servicesToAdd", new List<NwServiceElement> { new() { Name = "ignored service", ProtoId = 6, Port = 443 } });

        component.ApplyChanges();

        Assert.Multiple(() =>
        {
            Assert.That(reqTask.Elements.Count(element => element.Field == ElemFieldType.source.ToString()), Is.EqualTo(1));
            Assert.That(reqTask.Elements.Any(element => element.Field == ElemFieldType.service.ToString()), Is.False);
        });
    }

    [Test]
    public void ApplyChanges_GroupModifyServiceGroupAddsMembersWithGroupName()
    {
        WfReqTask reqTask = new() { Id = 42, TaskType = WfTaskType.group_modify.ToString() };
        RequestTaskElementEditor component = CreateComponent(reqTask, WfTaskType.group_modify);
        SetMember(component, "groupName", "service-group");
        SetMember(component, "grpTypeSvc", true);
        SetMember(component, "servicesToAdd", new List<NwServiceElement> { new() { Name = "new service", ProtoId = 6, Port = 443 } });

        component.ApplyChanges();

        Assert.Multiple(() =>
        {
            Assert.That(reqTask.Elements.Single().GroupName, Is.EqualTo("service-group"));
            Assert.That(reqTask.Elements.Single().RequestAction, Is.EqualTo(RequestAction.create.ToString()));
            Assert.That(GetMember<List<NwServiceElement>>(component, "servicesToAdd"), Is.Empty);
        });
    }

    [Test]
    public void GroupCreate_StoresAndEditsGroupNameInElementEditor()
    {
        WfReqTask reqTask = new() { Id = 42, TaskType = WfTaskType.group_create.ToString() };
        reqTask.SetAddInfo(AdditionalInfoKeys.GrpName, "initial-group");
        RequestTaskElementEditor component = CreateComponent(reqTask, WfTaskType.group_create);

        component.InitializeElements();
        InvokePrivate<Task>(component, "GroupNameChangedFromInput", new ChangeEventArgs { Value = "changed-group" }).GetAwaiter().GetResult();

        Assert.That(component.CurrentGroupName, Is.EqualTo("changed-group"));
    }

    [Test]
    public void GroupModify_UsesGroupNameAndPreservesDeleteAction()
    {
        WfReqTask reqTask = new() { Id = 42, TaskType = WfTaskType.group_modify.ToString() };
        reqTask.SetAddInfo(AdditionalInfoKeys.GrpName, "existing-group");
        reqTask.Elements.Add(new WfReqElement
        {
            Id = 7,
            Field = ElemFieldType.source.ToString(),
            Name = "member",
            IpString = "10.0.0.1",
            RequestAction = RequestAction.unchanged.ToString()
        });
        RequestTaskElementEditor component = CreateComponent(reqTask, WfTaskType.group_modify);

        component.InitializeElements();
        SetMember(component, "sourcesToDelete", new List<NwObjectElement>
        {
            new() { ElemId = 7, Name = "member", IpString = "10.0.0.1" }
        });
        component.ApplyChanges();

        Assert.Multiple(() =>
        {
            Assert.That(component.CurrentGroupName, Is.EqualTo("existing-group"));
            Assert.That(reqTask.Elements.Single().RequestAction, Is.EqualTo(RequestAction.delete.ToString()));
        });
    }

    [Test]
    public void GroupModify_InitializeElements_RestoresPersistedMemberDeletions()
    {
        WfReqTask reqTask = new() { Id = 42, TaskType = WfTaskType.group_modify.ToString() };
        reqTask.Elements =
        [
            new WfReqElement
            {
                Field = ElemFieldType.source.ToString(),
                Name = "network-member",
                IpString = "10.0.0.1",
                RequestAction = RequestAction.delete.ToString()
            },
            new WfReqElement
            {
                Field = ElemFieldType.service.ToString(),
                Name = "service-member",
                ServiceId = 17,
                ProtoId = 6,
                Port = 443,
                RequestAction = RequestAction.delete.ToString()
            }
        ];
        RequestTaskElementEditor component = CreateComponent(reqTask, WfTaskType.group_modify);

        component.InitializeElements();

        Assert.Multiple(() =>
        {
            Assert.That(GetMember<List<NwObjectElement>>(component, "sourcesToDelete").Select(element => element.Name),
                Is.EqualTo(kPersistedNetworkMemberNames));
            Assert.That(GetMember<List<NwServiceElement>>(component, "servicesToDelete").Select(element => element.Name),
                Is.EqualTo(kPersistedServiceMemberNames));
            Assert.That(component.SourceCount, Is.Zero);
            Assert.That(component.ServiceCount, Is.Zero);
        });
    }

    [Test]
    public void GroupModify_ResolvingSelectedGroups_PreservesPersistedMemberDeletions()
    {
        WfReqTask reqTask = new() { Id = 42, TaskType = WfTaskType.group_modify.ToString() };
        reqTask.Elements =
        [
            new WfReqElement
            {
                Field = ElemFieldType.source.ToString(),
                NetworkId = 11,
                RequestAction = RequestAction.delete.ToString()
            },
            new WfReqElement
            {
                Field = ElemFieldType.service.ToString(),
                ServiceId = 17,
                RequestAction = RequestAction.delete.ToString()
            }
        ];
        RequestTaskElementEditor component = CreateComponent(reqTask, WfTaskType.group_modify);
        NetworkObject networkGroup = new()
        {
            ObjectGroups = [new Group<NetworkObject> { Object = new NetworkObject { Id = 11, Name = "network-member", IP = "10.0.0.1" } }]
        };
        NetworkService serviceGroup = new()
        {
            ServiceGroups = [new Group<NetworkService> { Object = new NetworkService { Id = 17, Name = "service-member", ProtoId = 6, DestinationPort = 443 } }]
        };

        List<NwObjectElement> networkMembers = InvokePrivate<List<NwObjectElement>>(component, "ResolveNetworkGroupMembers", networkGroup);
        List<NwServiceElement> serviceMembers = InvokePrivate<List<NwServiceElement>>(component, "ResolveServiceGroupMembers", serviceGroup);

        Assert.Multiple(() =>
        {
            Assert.That(networkMembers.Single().RequestAction, Is.EqualTo(RequestAction.delete.ToString()));
            Assert.That(serviceMembers.Single().RequestAction, Is.EqualTo(RequestAction.delete.ToString()));
        });
    }

    [Test]
    public void ResolveGroupMembers_FiltersMissingMembersAndUsesUnchangedActionByDefault()
    {
        RequestTaskElementEditor component = CreateComponent(new WfReqTask(), WfTaskType.group_modify);
        NetworkObject networkGroup = new()
        {
            Name = "network-group",
            ObjectGroups =
            [
                new Group<NetworkObject> { Object = null },
                new Group<NetworkObject> { Object = new NetworkObject { Id = 11, Name = "member", IP = "10.0.0.1" } }
            ]
        };
        NetworkService serviceGroup = new()
        {
            Name = "service-group",
            ServiceGroups =
            [
                new Group<NetworkService> { Object = null },
                new Group<NetworkService> { Object = new NetworkService { Id = 17, Name = "service", ProtoId = 6, DestinationPort = 443 } }
            ]
        };

        List<NwObjectElement> networkMembers = InvokePrivate<List<NwObjectElement>>(component, "ResolveNetworkGroupMembers", networkGroup);
        List<NwServiceElement> serviceMembers = InvokePrivate<List<NwServiceElement>>(component, "ResolveServiceGroupMembers", serviceGroup);

        Assert.Multiple(() =>
        {
            Assert.That(networkMembers, Has.Count.EqualTo(1));
            Assert.That(networkMembers[0].RequestAction, Is.EqualTo(RequestAction.unchanged.ToString()));
            Assert.That(networkMembers[0].GroupName, Is.EqualTo("network-group"));
            Assert.That(serviceMembers, Has.Count.EqualTo(1));
            Assert.That(serviceMembers[0].RequestAction, Is.EqualTo(RequestAction.unchanged.ToString()));
            Assert.That(serviceMembers[0].GroupName, Is.EqualTo("service-group"));
        });
    }

    [Test]
    public void SelectNetworkGroup_ReplacesMembersAndPreservesMatchingIds()
    {
        WfReqTask reqTask = new() { Id = 42, TaskType = WfTaskType.group_modify.ToString() };
        reqTask.Elements =
        [
            new WfReqElement { Id = 7, TaskId = 42, Field = ElemFieldType.source.ToString(), NetworkId = 11, Name = "old-name", IpString = "10.0.0.1" },
            new WfReqElement { Id = 8, TaskId = 42, Field = ElemFieldType.source.ToString(), NetworkId = 99, Name = "removed", IpString = "10.0.0.99" }
        ];
        RequestTaskElementEditor component = CreateComponent(reqTask, WfTaskType.group_modify);
        NetworkObject group = new()
        {
            Id = 100,
            Name = "network-group",
            ObjectGroups =
            [
                new Group<NetworkObject> { Object = new NetworkObject { Id = 11, Name = "new-name", IP = "10.0.0.1" } }
            ]
        };

        InvokePrivate<Task>(component, "SelectNetworkGroup", group).GetAwaiter().GetResult();

        Assert.Multiple(() =>
        {
            Assert.That(component.CurrentGroupName, Is.EqualTo("network-group"));
            Assert.That(GetMember<List<NwObjectElement>>(component, "actSources").Single().ElemId, Is.EqualTo(7));
            Assert.That(reqTask.Elements.Single(element => element.NetworkId == 11).Id, Is.EqualTo(7));
            Assert.That(reqTask.RemovedElements.Single().Id, Is.EqualTo(8));
            Assert.That(reqTask.Elements.Single().GroupName, Is.EqualTo("network-group"));
        });
    }

    [Test]
    public void SelectServiceGroup_ReplacesMembersAndKeepsUnmatchedElementsRemoved()
    {
        WfReqTask reqTask = new() { Id = 42, TaskType = WfTaskType.group_modify.ToString() };
        reqTask.Elements =
        [
            new WfReqElement { Id = 7, TaskId = 42, Field = ElemFieldType.service.ToString(), ServiceId = 17, Name = "old-service", ProtoId = 6, Port = 443 },
            new WfReqElement { Id = 8, TaskId = 42, Field = ElemFieldType.service.ToString(), ServiceId = 99, Name = "removed", ProtoId = 6, Port = 80 }
        ];
        RequestTaskElementEditor component = CreateComponent(reqTask, WfTaskType.group_modify);
        NetworkService group = new()
        {
            Id = 100,
            Name = "service-group",
            ServiceGroups =
            [
                new Group<NetworkService> { Object = new NetworkService { Id = 17, Name = "new-service", ProtoId = 6, DestinationPort = 443 } }
            ]
        };

        InvokePrivate<Task>(component, "SelectServiceGroup", group).GetAwaiter().GetResult();

        Assert.Multiple(() =>
        {
            Assert.That(component.CurrentGroupName, Is.EqualTo("service-group"));
            Assert.That(GetMember<List<NwServiceElement>>(component, "actServices").Single().ElemId, Is.EqualTo(7));
            Assert.That(reqTask.Elements.Single(element => element.ServiceId == 17).Id, Is.EqualTo(7));
            Assert.That(reqTask.RemovedElements.Single().Id, Is.EqualTo(8));
            Assert.That(reqTask.Elements.Single().GroupName, Is.EqualTo("service-group"));
        });
    }

    [Test]
    public void SelectServiceGroup_RemovesPersistedNetworkMembersWhenSwitchingGroupKind()
    {
        WfReqTask reqTask = new() { Id = 42, TaskType = WfTaskType.group_modify.ToString() };
        reqTask.Elements =
        [
            new WfReqElement { Id = 7, TaskId = 42, Field = ElemFieldType.source.ToString(), NetworkId = 11, Name = "network-member" }
        ];
        RequestTaskElementEditor component = CreateComponent(reqTask, WfTaskType.group_modify);
        NetworkService group = new()
        {
            Id = 100,
            Name = "service-group",
            ServiceGroups =
            [
                new Group<NetworkService>
                {
                    Object = new NetworkService { Id = 17, Name = "service-member", ProtoId = 6, DestinationPort = 443 }
                }
            ]
        };

        InvokePrivate<Task>(component, "SelectServiceGroup", group).GetAwaiter().GetResult();

        Assert.Multiple(() =>
        {
            Assert.That(reqTask.Elements, Has.Count.EqualTo(1));
            Assert.That(reqTask.Elements, Has.All.Property(nameof(WfReqElement.Field)).EqualTo(ElemFieldType.service.ToString()));
            Assert.That(reqTask.RemovedElements.Select(element => element.Id), Is.EquivalentTo(new List<long> { 7L }));
            Assert.That(reqTask.Elements.Single().ServiceId, Is.EqualTo(17));
        });
    }

    [Test]
    public void SelectNetworkGroup_RemovesPersistedServiceMembersWhenSwitchingGroupKind()
    {
        WfReqTask reqTask = new() { Id = 42, TaskType = WfTaskType.group_modify.ToString() };
        reqTask.Elements =
        [
            new WfReqElement { Id = 8, TaskId = 42, Field = ElemFieldType.service.ToString(), ServiceId = 17, Name = "service-member", ProtoId = 6, Port = 443 }
        ];
        RequestTaskElementEditor component = CreateComponent(reqTask, WfTaskType.group_modify);
        SetMember(component, "grpTypeSvc", true);
        NetworkObject group = new()
        {
            Id = 100,
            Name = "network-group",
            ObjectGroups =
            [
                new Group<NetworkObject>
                {
                    Object = new NetworkObject { Id = 11, Name = "network-member", IP = "10.0.0.1" }
                }
            ]
        };

        InvokePrivate<Task>(component, "SelectNetworkGroup", group).GetAwaiter().GetResult();

        Assert.Multiple(() =>
        {
            Assert.That(reqTask.Elements, Has.Count.EqualTo(1));
            Assert.That(reqTask.Elements, Has.All.Property(nameof(WfReqElement.Field)).EqualTo(ElemFieldType.source.ToString()));
            Assert.That(reqTask.RemovedElements.Select(element => element.Id), Is.EquivalentTo(new List<long> { 8L }));
            Assert.That(reqTask.Elements.Single().NetworkId, Is.EqualTo(11));
        });
    }

    [Test]
    public void SelectingANullGroupDoesNotChangeTheEditor()
    {
        RequestTaskElementEditor component = CreateComponent(new WfReqTask(), WfTaskType.group_modify);
        SetMember(component, "groupName", "existing-group");

        InvokePrivate<Task>(component, "SelectNetworkGroup", (NetworkObject?)null).GetAwaiter().GetResult();
        InvokePrivate<Task>(component, "SelectServiceGroup", (NetworkService?)null).GetAwaiter().GetResult();

        Assert.That(component.CurrentGroupName, Is.EqualTo("existing-group"));
    }

    [Test]
    public void GroupModify_NewExistingMemberKeepsDeleteActionWithoutElementId()
    {
        WfReqTask reqTask = new() { Id = 0, TaskType = WfTaskType.group_modify.ToString() };
        RequestTaskElementEditor component = CreateComponent(reqTask, WfTaskType.group_modify);
        component.InitializeElements();
        SetMember(component, "sourcesToDelete", new List<NwObjectElement>
        {
            new() { Name = "member", IpString = "10.0.0.1" }
        });
        reqTask.Elements.Add(new WfReqElement
        {
            Field = ElemFieldType.source.ToString(),
            Name = "member",
            IpString = "10.0.0.1",
            RequestAction = RequestAction.unchanged.ToString()
        });

        component.ApplyChanges();

        Assert.That(reqTask.Elements.Single().RequestAction, Is.EqualTo(RequestAction.delete.ToString()));
    }

    [Test]
    public void GroupModify_NewExistingMemberUsesObjectIdWhenDisplayValuesDiffer()
    {
        WfReqTask reqTask = new() { Id = 0, TaskType = WfTaskType.group_modify.ToString() };
        RequestTaskElementEditor component = CreateComponent(reqTask, WfTaskType.group_modify);
        component.InitializeElements();
        SetMember(component, "sourcesToDelete", new List<NwObjectElement>
        {
            new() { NetworkId = 17, Name = "member-from-search", IpString = "192.0.2.17" }
        });
        reqTask.Elements.Add(new WfReqElement
        {
            Field = ElemFieldType.source.ToString(),
            NetworkId = 17,
            Name = "member-from-request",
            IpString = "192.0.2.17/32",
            RequestAction = RequestAction.unchanged.ToString()
        });

        component.ApplyChanges();

        Assert.That(reqTask.Elements.Single().RequestAction, Is.EqualTo(RequestAction.delete.ToString()));
    }

    [Test]
    public void ClearSelectedGroup_IgnoresNonGroupTasks()
    {
        WfReqTask reqTask = new() { Id = 42, TaskType = WfTaskType.access.ToString() };
        reqTask.Elements.Add(new WfReqElement { Id = 1, Field = ElemFieldType.source.ToString() });
        RequestTaskElementEditor component = CreateComponent(reqTask, WfTaskType.access);

        component.ClearSelectedGroup();

        Assert.That(reqTask.Elements, Has.Count.EqualTo(1));
    }

    [Test]
    public void ClearSelectedGroup_RemovesCurrentMembersAndTracksPersistedElements()
    {
        WfReqTask reqTask = new() { Id = 42, TaskType = WfTaskType.group_modify.ToString() };
        reqTask.Elements =
        [
            new WfReqElement { Id = 1, Field = ElemFieldType.source.ToString(), Name = "source" },
            new WfReqElement { Id = 2, Field = ElemFieldType.service.ToString(), Name = "service" },
            new WfReqElement { Id = 0, Field = ElemFieldType.source.ToString(), Name = "new source" }
        ];
        reqTask.RemovedElements.Add(new WfReqElement { Id = 1 });
        RequestTaskElementEditor component = CreateComponent(reqTask, WfTaskType.group_modify);
        SetMember(component, "groupName", "existing-group");

        component.ClearSelectedGroup();

        Assert.Multiple(() =>
        {
            Assert.That(reqTask.Elements, Is.Empty);
            Assert.That(reqTask.RemovedElements.Select(element => element.Id), Is.EquivalentTo(new List<long> { 1L, 2L }));
            Assert.That(component.CurrentGroupName, Is.Null);
            Assert.That(component.SourceCount, Is.Zero);
            Assert.That(component.ServiceCount, Is.Zero);
        });
    }

    [Test]
    public void ResetPendingChanges_ClearsAllPendingCollections()
    {
        RequestTaskElementEditor component = CreateComponent(new WfReqTask { Id = 42 });
        SetMember(component, "sourcesToAdd", new List<NwObjectElement> { new() });
        SetMember(component, "sourcesToDelete", new List<NwObjectElement> { new() });
        SetMember(component, "destinationsToAdd", new List<NwObjectElement> { new() });
        SetMember(component, "destinationsToDelete", new List<NwObjectElement> { new() });
        SetMember(component, "servicesToAdd", new List<NwServiceElement> { new() });
        SetMember(component, "servicesToDelete", new List<NwServiceElement> { new() });

        component.ResetPendingChanges();

        Assert.Multiple(() =>
        {
            Assert.That(GetMember<List<NwObjectElement>>(component, "sourcesToAdd"), Is.Empty);
            Assert.That(GetMember<List<NwObjectElement>>(component, "sourcesToDelete"), Is.Empty);
            Assert.That(GetMember<List<NwObjectElement>>(component, "destinationsToAdd"), Is.Empty);
            Assert.That(GetMember<List<NwObjectElement>>(component, "destinationsToDelete"), Is.Empty);
            Assert.That(GetMember<List<NwServiceElement>>(component, "servicesToAdd"), Is.Empty);
            Assert.That(GetMember<List<NwServiceElement>>(component, "servicesToDelete"), Is.Empty);
        });
    }

    [Test]
    public void DisplayObjectElement_PrefersMemberNameThenIp()
    {
        RequestTaskElementEditor component = CreateComponent(new WfReqTask());

        Assert.Multiple(() =>
        {
            Assert.That(InvokePrivate<string>(component, "DisplayObjectElement", new NwObjectElement { GroupName = "group", Name = "name" }), Is.EqualTo("name"));
            Assert.That(InvokePrivate<string>(component, "DisplayObjectElement", new NwObjectElement { Name = "name" }), Is.EqualTo("name"));
            Assert.That(InvokePrivate<string>(component, "DisplayObjectElement", new NwObjectElement { IpString = "10.0.0.1", IpEndString = "10.0.0.2" }), Does.Contain("10.0.0.1").And.Contain("10.0.0.2"));
        });
    }

    [Test]
    public void DisplayElements_FallBackToValuesWhenTheGroupNameIsTheMemberName()
    {
        RequestTaskElementEditor component = CreateComponent(new WfReqTask());
        SetMember(component, nameof(RequestTaskElementEditor.DisplayIpProtos), new List<IpProtocol>
        {
            new() { Id = 6, Name = "TCP" }
        });

        Assert.Multiple(() =>
        {
            Assert.That(InvokePrivate<string>(component, "DisplayObjectElement", new NwObjectElement
            {
                GroupName = "same",
                Name = "same",
                IpString = "10.0.0.1"
            }), Does.Contain("10.0.0.1"));
            Assert.That(InvokePrivate<string>(component, "DisplayServiceElement", new NwServiceElement
            {
                GroupName = "same",
                Name = "same",
                Port = 443,
                ProtoId = 6
            }), Is.EqualTo("443/TCP"));
        });
    }

    [Test]
    public void GroupMemberAction_MapsDeleteUnchangedAndAddActions()
    {
        RequestTaskElementEditor component = CreateComponent(new WfReqTask());
        SetMember(component, "userConfig", new UiRequestWorkflowTest.RequestWorkflowUserConfig());

        Assert.Multiple(() =>
        {
            Assert.That(InvokePrivate<string>(component, "GroupMemberAction", RequestAction.delete.ToString()), Does.Not.Empty);
            Assert.That(InvokePrivate<string>(component, "GroupMemberAction", RequestAction.unchanged.ToString()), Does.Not.Empty);
            Assert.That(InvokePrivate<string>(component, "GroupMemberAction", RequestAction.create.ToString()), Does.Not.Empty);
            Assert.That(InvokePrivateStatic<string>("GroupMemberBadgeClass", RequestAction.delete.ToString()), Is.EqualTo("text-bg-danger"));
            Assert.That(InvokePrivateStatic<string>("GroupMemberBadgeClass", RequestAction.unchanged.ToString()), Is.EqualTo("text-bg-secondary"));
            Assert.That(InvokePrivateStatic<string>("GroupMemberBadgeClass", RequestAction.create.ToString()), Is.EqualTo("text-bg-success"));
        });
    }

    [Test]
    public void DisplayServiceElement_HandlesGroupNameNameAndFormattedService()
    {
        RequestTaskElementEditor component = CreateComponent(new WfReqTask());
        SetMember(component, nameof(RequestTaskElementEditor.DisplayIpProtos), new List<IpProtocol>
        {
            new() { Id = 6, Name = "TCP" }
        });

        Assert.Multiple(() =>
        {
            Assert.That(InvokePrivate<string>(component, "DisplayServiceElement",
                new NwServiceElement { GroupName = "service-group", Name = "service" }), Is.EqualTo("service"));
            Assert.That(InvokePrivate<string>(component, "DisplayServiceElement",
                new NwServiceElement { Name = "service", Port = 0, ProtoId = 6 }), Is.EqualTo("service"));
            Assert.That(InvokePrivate<string>(component, "DisplayServiceElement",
                new NwServiceElement { Port = 443, ProtoId = 6 }), Is.EqualTo("443/TCP"));
        });
    }

    private static RequestTaskElementEditor CreateComponent(WfReqTask reqTask, WfTaskType taskType = WfTaskType.access)
    {
        RequestTaskElementEditor component = new();
        SetMember(component, nameof(RequestTaskElementEditor.WfHandler), new WfHandler { ActReqTask = reqTask });
        SetMember(component, nameof(RequestTaskElementEditor.TaskType), taskType);
        return component;
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

    private static T GetMember<T>(object instance, string memberName)
    {
        PropertyInfo? property = instance.GetType().GetProperty(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (property != null)
        {
            return (T)property.GetValue(instance)!;
        }
        FieldInfo? field = instance.GetType().GetField(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        return (T)(field?.GetValue(instance) ?? throw new MissingMemberException(instance.GetType().FullName, memberName));
    }

    private static T InvokePrivate<T>(object instance, string methodName, params object?[] parameters)
    {
        MethodInfo method = instance.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new MissingMethodException(instance.GetType().FullName, methodName);
        return (T)(method.Invoke(instance, parameters) ?? throw new AssertionException($"Method '{methodName}' returned null."));
    }

    private static T InvokePrivateStatic<T>(string methodName, params object?[] parameters)
    {
        MethodInfo method = typeof(RequestTaskElementEditor).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new MissingMethodException(typeof(RequestTaskElementEditor).FullName, methodName);
        return (T)(method.Invoke(null, parameters) ?? throw new AssertionException($"Method '{methodName}' returned null."));
    }
}
