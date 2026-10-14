using FWO.Data;
using FWO.Data.Workflow;
using FWO.Services.Workflow;
using FWO.Ui.Pages.Request;
using NUnit.Framework;
using System.Reflection;

namespace FWO.Test;

[TestFixture]
internal class UiRequestTaskElementEditorTest
{
    private static readonly long[] kRemovedElementIds = [1L, 2L, 3L];

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
    public void DisplayObjectElement_PrefersGroupNameThenNameThenIp()
    {
        RequestTaskElementEditor component = CreateComponent(new WfReqTask());

        Assert.Multiple(() =>
        {
            Assert.That(InvokePrivate<string>(component, "DisplayObjectElement", new NwObjectElement { GroupName = "group", Name = "name" }), Is.EqualTo("group"));
            Assert.That(InvokePrivate<string>(component, "DisplayObjectElement", new NwObjectElement { Name = "name" }), Is.EqualTo("name"));
            Assert.That(InvokePrivate<string>(component, "DisplayObjectElement", new NwObjectElement { IpString = "10.0.0.1", IpEndString = "10.0.0.2" }), Does.Contain("10.0.0.1").And.Contain("10.0.0.2"));
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
                new NwServiceElement { GroupName = "service-group", Name = "service" }), Is.EqualTo("service-group"));
            Assert.That(InvokePrivate<string>(component, "DisplayServiceElement",
                new NwServiceElement { Name = "service", Port = 0, ProtoId = 6 }), Is.EqualTo("service"));
            Assert.That(InvokePrivate<string>(component, "DisplayServiceElement",
                new NwServiceElement { Name = "service", Port = 443, ProtoId = 6 }), Is.EqualTo("443/TCP"));
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
}
