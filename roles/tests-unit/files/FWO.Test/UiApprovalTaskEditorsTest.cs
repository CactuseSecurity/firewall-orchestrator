using AngleSharp.Dom;
using Bunit;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Flow;
using FWO.Data.Workflow;
using FWO.Ui.Pages.Request;
using FWO.Ui.Services;
using FWO.Ui.Shared;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Reflection;

namespace FWO.Test;

[TestFixture]
internal sealed class UiApprovalTaskEditorsTest
{
    private sealed class EditorApiConnection : SimulatedApiConnection
    {
        public List<FlowNwObject> NetworkObjects { get; } = new();
        public List<FlowSvcObject> ServiceObjects { get; } = new();
        public List<FlowNwGroup> NetworkGroups { get; } = new();
        public List<FlowSvcGroup> ServiceGroups { get; } = new();

        public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null,
            string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
        {
            if (typeof(QueryResponseType) == typeof(List<FlowNwObject>))
            {
                return Task.FromResult((QueryResponseType)(object)NetworkObjects);
            }
            if (typeof(QueryResponseType) == typeof(List<FlowSvcObject>))
            {
                return Task.FromResult((QueryResponseType)(object)ServiceObjects);
            }
            if (typeof(QueryResponseType) == typeof(List<FlowNwGroup>))
            {
                return Task.FromResult((QueryResponseType)(object)NetworkGroups);
            }
            if (typeof(QueryResponseType) == typeof(List<FlowSvcGroup>))
            {
                return Task.FromResult((QueryResponseType)(object)ServiceGroups);
            }
            return Task.FromResult(default(QueryResponseType)!);
        }
    }

    private static BunitContext CreateContext(EditorApiConnection apiConnection)
    {
        BunitContext context = new();
        context.Services.AddSingleton<UserConfig>(new SimulatedUserConfig());
        context.Services.AddSingleton<DomEventService>();
        context.Services.AddSingleton<ApiConnection>(apiConnection);
        return context;
    }

    [Test]
    public async Task ApprovalObjectTaskEditor_OnlyShowsTheFixedServiceEditor()
    {
        await using BunitContext context = CreateContext(new EditorApiConnection());
        List<IpProtocol> protocols = new() { new IpProtocol { Id = 6, Name = "tcp" } };
        IRenderedComponent<ApprovalObjectTaskEditor> cut = context.Render<ApprovalObjectTaskEditor>(parameters => parameters
            .Add(parameter => parameter.Display, true)
            .Add(parameter => parameter.ObjectKind, ObjectTaskKind.Service)
            .Add(parameter => parameter.SelectableIpProtos, protocols)
            .Add(parameter => parameter.Task, new WfReqTask()));

        Assert.Multiple(() =>
        {
            Assert.That(cut.FindAll("#objKindNetwork"), Is.Empty);
            Assert.That(cut.FindAll("#objKindService"), Is.Empty);
            Assert.That(cut.FindAll("#objKindTime"), Is.Empty);
            Assert.That(cut.FindAll("input[id^='dropdown-input-']"), Has.Count.EqualTo(1));
            Assert.That(cut.FindAll("input[placeholder='port_range']"), Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task ApprovalObjectTaskEditor_SavesValidServiceTaskAndCloses()
    {
        WfReqTask task = new()
        {
            TaskType = WfTaskType.object_create.ToString(),
            Elements =
            [
                new WfReqElement
                {
                    Field = ElemFieldType.service.ToString(),
                    Name = "https",
                    ProtoId = 6,
                    Port = 443,
                    RequestAction = RequestAction.create.ToString()
                }
            ]
        };
        WfReqTask? saved = null;
        bool? displayChanged = null;

        await using BunitContext context = CreateContext(new EditorApiConnection());
        IRenderedComponent<ApprovalObjectTaskEditor> cut = context.Render<ApprovalObjectTaskEditor>(parameters => parameters
            .Add(parameter => parameter.Display, true)
            .Add(parameter => parameter.ObjectKind, ObjectTaskKind.Service)
            .Add(parameter => parameter.SelectableIpProtos, new List<IpProtocol> { new() { Id = 6, Name = "tcp" } })
            .Add(parameter => parameter.DisplayIpProtos, new List<IpProtocol> { new() { Id = 6, Name = "tcp" } })
            .Add(parameter => parameter.Task, task)
            .Add(parameter => parameter.Saved, value =>
            {
                saved = value;
                return Task.CompletedTask;
            })
            .Add(parameter => parameter.DisplayChanged, value =>
            {
                displayChanged = value;
                return Task.CompletedTask;
            }));

        await cut.InvokeAsync(() => cut.Find("button.btn-primary").Click());

        Assert.Multiple(() =>
        {
            Assert.That(saved, Is.SameAs(task));
            Assert.That(displayChanged, Is.False);
            Assert.That(task.Elements.Single().Port, Is.EqualTo(443));
        });
    }

    [Test]
    public async Task ApprovalObjectTaskEditor_DoesNotSaveIncompleteTask()
    {
        WfReqTask? saved = null;
        bool? displayChanged = null;

        await using BunitContext context = CreateContext(new EditorApiConnection());
        IRenderedComponent<ApprovalObjectTaskEditor> cut = context.Render<ApprovalObjectTaskEditor>(parameters => parameters
            .Add(parameter => parameter.Display, true)
            .Add(parameter => parameter.ObjectKind, ObjectTaskKind.Service)
            .Add(parameter => parameter.SelectableIpProtos, new List<IpProtocol> { new() { Id = 6, Name = "tcp" } })
            .Add(parameter => parameter.Task, new WfReqTask())
            .Add(parameter => parameter.Saved, value =>
            {
                saved = value;
                return Task.CompletedTask;
            })
            .Add(parameter => parameter.DisplayChanged, value =>
            {
                displayChanged = value;
                return Task.CompletedTask;
            }));

        await cut.InvokeAsync(() => cut.Find("button.btn-primary").Click());

        Assert.Multiple(() =>
        {
            Assert.That(saved, Is.Null);
            Assert.That(displayChanged, Is.Null);
        });
    }

    [TestCase(ObjectTaskKind.Network)]
    [TestCase(ObjectTaskKind.Time)]
    public async Task ApprovalObjectTaskEditor_RendersTheRequestedFixedTaskKind(ObjectTaskKind kind)
    {
        await using BunitContext context = CreateContext(new EditorApiConnection());
        IRenderedComponent<ApprovalObjectTaskEditor> cut = context.Render<ApprovalObjectTaskEditor>(parameters => parameters
            .Add(parameter => parameter.Display, true)
            .Add(parameter => parameter.ObjectKind, kind)
            .Add(parameter => parameter.Task, new WfReqTask()));

        Assert.Multiple(() =>
        {
            Assert.That(cut.Markup, Does.Not.Contain("objKindNetwork"));
            Assert.That(cut.Markup, Does.Not.Contain("objKindService"));
            Assert.That(cut.Markup, Does.Not.Contain("objKindTime"));
            Assert.That(cut.FindAll("button.btn-primary"), Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task ApprovalObjectTaskEditor_CloseInvokesDisplayChanged()
    {
        bool? displayChanged = null;
        await using BunitContext context = CreateContext(new EditorApiConnection());
        IRenderedComponent<ApprovalObjectTaskEditor> cut = context.Render<ApprovalObjectTaskEditor>(parameters => parameters
            .Add(parameter => parameter.Display, true)
            .Add(parameter => parameter.ObjectKind, ObjectTaskKind.Network)
            .Add(parameter => parameter.Task, new WfReqTask())
            .Add(parameter => parameter.DisplayChanged, value =>
            {
                displayChanged = value;
                return Task.CompletedTask;
            }));

        await cut.InvokeAsync(() => cut.Find("button.btn-secondary").Click());

        Assert.That(displayChanged, Is.False);
    }

    [Test]
    public async Task ApprovalObjectTaskEditor_UsesObjectSpecificTitle()
    {
        WfReqTask objectTask = new()
        {
            TaskType = WfTaskType.object_modify.ToString(),
            Elements =
            [
                new WfReqElement
                {
                    Field = ElemFieldType.source.ToString(),
                    Name = "server"
                }
            ]
        };

        await using BunitContext context = CreateContext(new EditorApiConnection());
        IRenderedComponent<ApprovalObjectTaskEditor> cut = context.Render<ApprovalObjectTaskEditor>(parameters => parameters
            .Add(parameter => parameter.Display, true)
            .Add(parameter => parameter.IsModify, true)
            .Add(parameter => parameter.ObjectKind, ObjectTaskKind.Network)
            .Add(parameter => parameter.Task, objectTask));

        Assert.Multiple(() =>
        {
            Assert.That(cut.Markup, Does.Contain("server"));
            Assert.That(cut.FindComponents<ObjectSelector>(), Is.Empty);
        });
    }

    [Test]
    public async Task ApprovalTaskElementsEditor_ListsTicketBeforeFlowDbAndCopiesTicketElementToCurrentTask()
    {
        EditorApiConnection apiConnection = new();
        apiConnection.NetworkObjects.Add(new FlowNwObject
        {
            Id = 15,
            Name = "flow-source",
            IpStart = "198.51.100.15",
            ShowInRequestModule = true,
            State = FlowState.Requested
        });
        WfReqTask currentTask = new() { Id = 10 };
        WfReqTask ticketTask = new() { Id = 11 };
        ticketTask.Elements.Add(new NwObjectElement("192.0.2.11", ticketTask.Id)
        {
            Name = "ticket-source"
        }.ToReqElement(ElemFieldType.source));

        await using BunitContext context = CreateContext(apiConnection);
        IRenderedComponent<ApprovalTaskElementsEditor> cut = context.Render<ApprovalTaskElementsEditor>(parameters => parameters
            .Add(parameter => parameter.Display, true)
            .Add(parameter => parameter.Task, currentTask)
            .Add(parameter => parameter.FieldKey, WorkflowEditableFieldKeys.Source)
            .Add(parameter => parameter.TicketTasks, new List<WfReqTask> { currentTask, ticketTask }));

        await cut.InvokeAsync(() => cut.Find("input[id^='dropdown-input-']").Focus());
        IElement[] choices = cut.FindAll("button.dropdown-item").ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(choices, Has.Length.EqualTo(2));
            Assert.That(choices[0].TextContent, Does.Contain("ticket-source (192.0.2.11)"));
            Assert.That(choices[1].TextContent, Does.Contain("flow-source (198.51.100.15)"));
        });

        IElement searchInput = cut.Find("input[id^='dropdown-input-']");
        searchInput.Input("flow-source");
        Assert.That(cut.FindAll("button.dropdown-item"), Has.Count.EqualTo(1));
        Assert.That(cut.Find("button.dropdown-item").TextContent, Does.Contain("flow-source (198.51.100.15)"));
        searchInput.Input("");

        cut.FindAll("button.dropdown-item")[0].Click();
        List<NwObjectElement> additions = UiRequestWorkflowTest.GetMember<List<NwObjectElement>>(cut.Instance, "sourcesToAdd");

        Assert.Multiple(() =>
        {
            Assert.That(additions, Has.Count.EqualTo(1));
            Assert.That(additions[0].TaskId, Is.EqualTo(currentTask.Id));
            Assert.That(additions[0].Name, Is.EqualTo("ticket-source"));
            Assert.That(cut.Markup, Does.Contain("ticket-source (192.0.2.11)"));
        });
    }

    [Test]
    public async Task ApprovalTaskElementsEditor_RemovesPendingTicketElementFromTheList()
    {
        EditorApiConnection apiConnection = new();
        WfReqTask currentTask = new() { Id = 20 };
        WfReqTask ticketTask = new() { Id = 21 };
        ticketTask.Elements.Add(new NwObjectElement("192.0.2.21", ticketTask.Id)
        {
            Name = "ticket-source"
        }.ToReqElement(ElemFieldType.source));

        await using BunitContext context = CreateContext(apiConnection);
        IRenderedComponent<ApprovalTaskElementsEditor> cut = context.Render<ApprovalTaskElementsEditor>(parameters => parameters
            .Add(parameter => parameter.Display, true)
            .Add(parameter => parameter.Task, currentTask)
            .Add(parameter => parameter.FieldKey, WorkflowEditableFieldKeys.Source)
            .Add(parameter => parameter.TicketTasks, new List<WfReqTask> { currentTask, ticketTask }));

        await cut.InvokeAsync(() => cut.Find("input[id^='dropdown-input-']").Focus());
        cut.FindAll("button.dropdown-item")
            .Single(option => option.TextContent.Contains("ticket-source", StringComparison.Ordinal))
            .Click();
        cut.Find("button.btn-outline-danger").Click();

        List<NwObjectElement> additions = UiRequestWorkflowTest.GetMember<List<NwObjectElement>>(cut.Instance, "sourcesToAdd");
        Assert.That(additions, Is.Empty);
    }

    [Test]
    public async Task ApprovalTaskElementsEditor_ResolvesTicketGroupMembersInChoices()
    {
        WfReqTask currentTask = new() { Id = 30 };
        WfReqTask groupTask = new() { Id = 31, TaskType = WfTaskType.group_create.ToString() };
        groupTask.SetAddInfo(AdditionalInfoKeys.GrpName, "web-group");
        groupTask.Elements.Add(new NwObjectElement("192.0.2.31", groupTask.Id)
        {
            Name = "member-one",
            GroupName = "web-group"
        }.ToReqElement(ElemFieldType.source));
        WfReqTask accessTask = new() { Id = 32 };
        accessTask.Elements.Add(new NwObjectElement("", accessTask.Id)
        {
            Name = "web-group",
            GroupName = "web-group"
        }.ToReqElement(ElemFieldType.source));

        await using BunitContext context = CreateContext(new EditorApiConnection());
        IRenderedComponent<ApprovalTaskElementsEditor> cut = context.Render<ApprovalTaskElementsEditor>(parameters => parameters
            .Add(parameter => parameter.Display, true)
            .Add(parameter => parameter.Task, currentTask)
            .Add(parameter => parameter.FieldKey, WorkflowEditableFieldKeys.Source)
            .Add(parameter => parameter.TicketTasks, new List<WfReqTask> { currentTask, groupTask, accessTask }));

        await cut.InvokeAsync(() => cut.Find("input[id^='dropdown-input-']").Focus());
        Assert.That(cut.FindAll("button.dropdown-item").Any(option => option.TextContent.Contains("web-group (member-one (192.0.2.31))", StringComparison.Ordinal)), Is.True);
    }

    [Test]
    public async Task ApprovalTaskElementsEditor_ResolvesTicketServiceGroupMembersWithoutRecursion()
    {
        WfReqTask currentTask = new() { Id = 33 };
        WfReqTask groupTask = new() { Id = 34, TaskType = WfTaskType.group_create.ToString() };
        groupTask.SetAddInfo(AdditionalInfoKeys.GrpName, "web-services");
        groupTask.Elements.Add(new NwServiceElement
        {
            Name = "https",
            GroupName = "web-services",
            ProtoId = 6,
            HasProtocol = true,
            Port = 443
        }.ToReqElement());
        WfReqTask accessTask = new() { Id = 35 };
        accessTask.Elements.Add(new NwServiceElement
        {
            Name = "web-services",
            GroupName = "web-services"
        }.ToReqElement());

        await using BunitContext context = CreateContext(new EditorApiConnection());
        IRenderedComponent<ApprovalTaskElementsEditor> cut = context.Render<ApprovalTaskElementsEditor>(parameters => parameters
            .Add(parameter => parameter.Display, true)
            .Add(parameter => parameter.Task, currentTask)
            .Add(parameter => parameter.FieldKey, WorkflowEditableFieldKeys.Services)
            .Add(parameter => parameter.IpProtos, new List<IpProtocol> { new() { Id = 6, Name = "tcp" } })
            .Add(parameter => parameter.TicketTasks, new List<WfReqTask> { currentTask, groupTask, accessTask }));

        await cut.InvokeAsync(() => cut.Find("input[id^='dropdown-input-']").Focus());
        Assert.That(cut.FindAll("button.dropdown-item").Any(option => option.TextContent.Contains("web-services (https", StringComparison.Ordinal)), Is.True);
    }

    [Test]
    public async Task ApprovalTaskElementsEditor_ResolvesFlowGroupMembersInChoicesAndList()
    {
        EditorApiConnection apiConnection = new();
        apiConnection.NetworkGroups.Add(new FlowNwGroup
        {
            Id = 41,
            Name = "flow-group",
            ShowInRequestModule = true,
            State = FlowState.Requested,
            NwGroupMembers =
            [
                new FlowNwGroupMember
                {
                    NwObject = new FlowNwObject { Id = 42, Name = "flow-member", IpStart = "192.0.2.42" }
                }
            ]
        });
        WfReqTask currentTask = new() { Id = 40 };

        await using BunitContext context = CreateContext(apiConnection);
        IRenderedComponent<ApprovalTaskElementsEditor> cut = context.Render<ApprovalTaskElementsEditor>(parameters => parameters
            .Add(parameter => parameter.Display, true)
            .Add(parameter => parameter.Task, currentTask)
            .Add(parameter => parameter.FieldKey, WorkflowEditableFieldKeys.Source));

        await cut.InvokeAsync(() => cut.Find("input[id^='dropdown-input-']").Focus());
        cut.FindAll("button.dropdown-item")
            .Single(option => option.TextContent.Contains("flow-group (flow-member (192.0.2.42))", StringComparison.Ordinal))
            .Click();

        Assert.That(cut.Markup, Does.Contain("flow-group (flow-member (192.0.2.42))"));
    }

    [Test]
    public async Task ApprovalTaskElementsEditor_AddsServiceAndSavesTheCurrentLists()
    {
        EditorApiConnection apiConnection = new();
        apiConnection.ServiceObjects.Add(new FlowSvcObject
        {
            Id = 51,
            Name = "https",
            ProtoId = 6,
            PortStart = 443,
            PortEnd = 443,
            ShowInRequestModule = true,
            State = FlowState.Requested
        });
        WfReqTask currentTask = new() { Id = 50 };
        string? savedField = null;
        IReadOnlyList<NwServiceElement>? savedServices = null;

        await using BunitContext context = CreateContext(apiConnection);
        IRenderedComponent<ApprovalTaskElementsEditor> cut = context.Render<ApprovalTaskElementsEditor>(parameters => parameters
            .Add(parameter => parameter.Display, true)
            .Add(parameter => parameter.Task, currentTask)
            .Add(parameter => parameter.FieldKey, WorkflowEditableFieldKeys.Services)
            .Add(parameter => parameter.IpProtos, new List<IpProtocol> { new() { Id = 6, Name = "tcp" } })
            .Add(parameter => parameter.Saved, (field, _, _, services) =>
            {
                savedField = field;
                savedServices = services;
                return Task.CompletedTask;
            }));

        await cut.InvokeAsync(() => cut.Find("input[id^='dropdown-input-']").Focus());
        cut.Find("button.dropdown-item").Click();
        await cut.InvokeAsync(() => cut.Find("button.btn-primary").Click());

        Assert.Multiple(() =>
        {
            Assert.That(savedField, Is.EqualTo(WorkflowEditableFieldKeys.Services));
            Assert.That(savedServices, Has.Count.EqualTo(1));
            Assert.That(savedServices![0].FlowServiceObjectId, Is.EqualTo(51));
        });
    }

    [Test]
    public async Task ApprovalTaskElementsEditor_CloseInvokesDisplayChanged()
    {
        bool? displayChanged = null;
        await using BunitContext context = CreateContext(new EditorApiConnection());
        IRenderedComponent<ApprovalTaskElementsEditor> cut = context.Render<ApprovalTaskElementsEditor>(parameters => parameters
            .Add(parameter => parameter.Display, true)
            .Add(parameter => parameter.Task, new WfReqTask())
            .Add(parameter => parameter.FieldKey, WorkflowEditableFieldKeys.Source)
            .Add(parameter => parameter.DisplayChanged, value =>
            {
                displayChanged = value;
                return Task.CompletedTask;
            }));

        await cut.InvokeAsync(() => cut.Find("button.btn-secondary").Click());

        Assert.That(displayChanged, Is.False);
    }

    [Test]
    public async Task ApprovalTaskElementsEditor_HidesCreateObjectTaskButtonWhenDisabled()
    {
        await using BunitContext context = CreateContext(new EditorApiConnection());
        IRenderedComponent<ApprovalTaskElementsEditor> cut = context.Render<ApprovalTaskElementsEditor>(parameters => parameters
            .Add(parameter => parameter.Display, true)
            .Add(parameter => parameter.Task, new WfReqTask { TaskType = WfTaskType.access.ToString() })
            .Add(parameter => parameter.FieldKey, WorkflowEditableFieldKeys.Source)
            .Add(parameter => parameter.ShowCreateObjectTask, false));

        Assert.That(cut.FindAll("button.btn-success"), Is.Empty);
    }

    [Test]
    public async Task ApprovalTaskElementsEditor_UsesContextualGroupTitle()
    {
        WfReqTask groupTask = new() { Id = 70, TaskType = WfTaskType.group_modify.ToString() };
        groupTask.SetAddInfo(AdditionalInfoKeys.GrpName, "web-group");

        await using BunitContext context = CreateContext(new EditorApiConnection());
        IRenderedComponent<ApprovalTaskElementsEditor> cut = context.Render<ApprovalTaskElementsEditor>(parameters => parameters
            .Add(parameter => parameter.Display, true)
            .Add(parameter => parameter.Task, groupTask)
            .Add(parameter => parameter.FieldKey, WorkflowEditableFieldKeys.Source));

        Assert.That(cut.Markup, Does.Contain("web-group"));
    }

    [Test]
    public async Task ApprovalTaskElementsEditor_UsesAccessTaskTitleInsteadOfFieldName()
    {
        WfReqTask accessTask = new()
        {
            Id = 71,
            TaskType = WfTaskType.access.ToString(),
            Title = "Allow web traffic"
        };

        await using BunitContext context = CreateContext(new EditorApiConnection());
        IRenderedComponent<ApprovalTaskElementsEditor> cut = context.Render<ApprovalTaskElementsEditor>(parameters => parameters
            .Add(parameter => parameter.Display, true)
            .Add(parameter => parameter.Task, accessTask)
            .Add(parameter => parameter.FieldKey, WorkflowEditableFieldKeys.Source));

        Assert.Multiple(() =>
        {
            Assert.That(cut.Markup, Does.Contain("Allow web traffic"));
            Assert.That(cut.Markup, Does.Not.Contain("Edit access task: Source"));
        });
    }

    [Test]
    public async Task ApprovalTaskElementsEditor_TogglesNetworkRemovalOnAndOff()
    {
        NwObjectElement element = new("192.0.2.60", 60) { Name = "server" };
        WfReqTask task = new() { Id = 60, Elements = [element.ToReqElement(ElemFieldType.source)] };
        await using BunitContext context = CreateContext(new EditorApiConnection());
        IRenderedComponent<ApprovalTaskElementsEditor> cut = context.Render<ApprovalTaskElementsEditor>(parameters => parameters
            .Add(parameter => parameter.Display, true)
            .Add(parameter => parameter.Task, task)
            .Add(parameter => parameter.FieldKey, WorkflowEditableFieldKeys.Source));

        MethodInfo toggle = UiRequestWorkflowTest.GetPrivateMethod(typeof(ApprovalTaskElementsEditor), "ToggleNetworkRemoval");
        toggle.Invoke(cut.Instance, [element]);
        Assert.That(UiRequestWorkflowTest.GetMember<List<NwObjectElement>>(cut.Instance, "sourcesToDelete"), Has.Count.EqualTo(1));
        toggle.Invoke(cut.Instance, [element]);
        Assert.That(UiRequestWorkflowTest.GetMember<List<NwObjectElement>>(cut.Instance, "sourcesToDelete"), Is.Empty);
    }

    [Test]
    public async Task ApprovalTaskElementsEditor_TogglesServiceRemovalAndBuildsFlowGroupChoice()
    {
        NwServiceElement service = new() { Name = "https", ProtoId = 6, HasProtocol = true, Port = 443 };
        WfReqTask task = new() { Id = 61, Elements = [service.ToReqElement()] };
        await using BunitContext context = CreateContext(new EditorApiConnection());
        IRenderedComponent<ApprovalTaskElementsEditor> cut = context.Render<ApprovalTaskElementsEditor>(parameters => parameters
            .Add(parameter => parameter.Display, true)
            .Add(parameter => parameter.Task, task)
            .Add(parameter => parameter.FieldKey, WorkflowEditableFieldKeys.Services)
            .Add(parameter => parameter.IpProtos, new List<IpProtocol> { new() { Id = 6, Name = "tcp" } }));

        MethodInfo toggle = UiRequestWorkflowTest.GetPrivateMethod(typeof(ApprovalTaskElementsEditor), "ToggleServiceRemoval");
        toggle.Invoke(cut.Instance, [service]);
        Assert.That(UiRequestWorkflowTest.GetMember<List<NwServiceElement>>(cut.Instance, "servicesToDelete"), Has.Count.EqualTo(1));
        toggle.Invoke(cut.Instance, [service]);
        Assert.That(UiRequestWorkflowTest.GetMember<List<NwServiceElement>>(cut.Instance, "servicesToDelete"), Is.Empty);

        FlowSvcGroup group = new()
        {
            Id = 62,
            Name = "web-services",
            SvcGroupMembers =
            [
                new FlowSvcGroupMember
                {
                    SvcObject = new FlowSvcObject
                    {
                        Id = 63,
                        Name = "https",
                        ProtoId = 6,
                        PortStart = 443,
                        PortEnd = 443
                    }
                }
            ]
        };
        MethodInfo buildChoice = UiRequestWorkflowTest.GetPrivateMethod(typeof(ApprovalTaskElementsEditor), "BuildFlowServiceGroupChoice");
        object choice = buildChoice.Invoke(cut.Instance, [group])!;

        Assert.Multiple(() =>
        {
            Assert.That(UiRequestWorkflowTest.GetMember<string>(choice, "Label"), Does.Contain("web-services"));
            Assert.That(UiRequestWorkflowTest.GetMember<string>(choice, "Label"), Does.Contain("https"));
            Assert.That(UiRequestWorkflowTest.GetMember<NwServiceElement>(choice, "ServiceElement").FlowServiceGroupId, Is.EqualTo(62));
        });
    }
}
