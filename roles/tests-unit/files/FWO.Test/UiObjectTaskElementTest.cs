using BlazorTable;
using Bunit;
using FWO.Api.Client;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Workflow;
using FWO.Ui.Pages.Request;
using FWO.Ui.Services;
using FWO.Ui.Shared;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Reflection;
using System.Security.Claims;

namespace FWO.Test
{
    [TestFixture]
    internal class UiObjectTaskElementTest
    {
        private const int kManagementId = 3;
        private const int kTcp = 6;
        private static readonly List<long> kKeptElementIds = [1, 2];
        private static readonly List<IpProtocol> kIpProtos = [new() { Id = kTcp, Name = "tcp" }, new() { Id = 1, Name = "icmp" }];

        private sealed class SearchApiConnection : SimulatedApiConnection
        {
            public object Result { get; set; } = new List<NetworkObject>();
            public int QueryCount { get; private set; }

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                QueryCount++;
                return Task.FromResult((QueryResponseType)Result);
            }
        }

        private static BunitContext CreateContext(ApiConnection? apiConnection = null)
        {
            BunitContext context = new();
            context.Services.AddSingleton<UserConfig>(new SimulatedUserConfig());
            context.Services.AddSingleton<DomEventService>();
            context.Services.AddBlazorTable();
            context.Services.AddSingleton(apiConnection ?? new SearchApiConnection());
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            return context;
        }

        private static IRenderedComponent<DisplayObjectTaskElement> RenderElement(BunitContext context, WfReqTask task, bool isModify, int? managementId, bool editMode = true)
        {
            return context.Render<DisplayObjectTaskElement>(parameters => parameters
                .Add(p => p.ReqTask, task)
                .Add(p => p.IsModify, isModify)
                .Add(p => p.ManagementId, managementId)
                .Add(p => p.EditMode, editMode)
                .Add(p => p.SelectableIpProtos, kIpProtos)
                .Add(p => p.DisplayIpProtos, kIpProtos));
        }

        private static void SetMember(object instance, string memberName, object? value)
        {
            Type type = instance.GetType();
            PropertyInfo? property = type.GetProperty(memberName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (property != null)
            {
                property.SetValue(instance, value);
                return;
            }
            FieldInfo field = type.GetField(memberName, BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingFieldException(type.FullName, memberName);
            field.SetValue(instance, value);
        }

        private static T? GetMember<T>(object instance, string memberName)
        {
            Type type = instance.GetType();
            PropertyInfo? property = type.GetProperty(memberName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (property != null)
            {
                return (T?)property.GetValue(instance);
            }
            FieldInfo field = type.GetField(memberName, BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingFieldException(type.FullName, memberName);
            return (T?)field.GetValue(instance);
        }

        private static async Task InvokePrivate(IRenderedComponent<DisplayObjectTaskElement> component, string methodName, object? argument)
        {
            MethodInfo method = typeof(DisplayObjectTaskElement).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMethodException(typeof(DisplayObjectTaskElement).FullName, methodName);
            List<object?> arguments = [argument];
            await component.InvokeAsync(() => method.Invoke(component.Instance, arguments.ToArray()));
        }

        [Test]
        public async Task ApplyToTask_RequiresAManagementForModify()
        {
            await using BunitContext context = CreateContext();
            IRenderedComponent<DisplayObjectTaskElement> component = RenderElement(context, new WfReqTask(), true, -1);

            Assert.That(component.Instance.ApplyToTask(), Is.EqualTo("E5127"));
        }

        [TestCase(-1)]
        [TestCase(null)]
        public async Task ApplyToTask_AcceptsAllManagementsForCreate(int? managementId)
        {
            await using BunitContext context = CreateContext();
            WfReqTask task = new() { TaskType = WfTaskType.object_create.ToString() };
            IRenderedComponent<DisplayObjectTaskElement> component = RenderElement(context, task, false, managementId);
            SetMember(component.Instance, "NetworkInput", "10.1.1.5");

            Assert.Multiple(() =>
            {
                Assert.That(component.Instance.ApplyToTask(), Is.Null);
                Assert.That(task.Elements, Has.Count.EqualTo(1));
            });
        }

        [Test]
        public async Task ApplyToTask_CreatesOneNetworkElement()
        {
            await using BunitContext context = CreateContext();
            WfReqTask task = new() { TaskType = WfTaskType.object_create.ToString() };
            IRenderedComponent<DisplayObjectTaskElement> component = RenderElement(context, task, false, kManagementId);
            SetMember(component.Instance, "NetworkInput", "10.1.2.0/24");
            SetMember(component.Instance, "objectName", " net_app ");

            string? error = component.Instance.ApplyToTask();

            Assert.Multiple(() =>
            {
                Assert.That(error, Is.Null);
                Assert.That(task.Elements, Has.Count.EqualTo(1));
                Assert.That(task.Elements[0].Field, Is.EqualTo(ElemFieldType.source.ToString()));
                Assert.That(task.Elements[0].RequestAction, Is.EqualTo(RequestAction.create.ToString()));
                Assert.That(task.Elements[0].Name, Is.EqualTo("net_app"));
                Assert.That(task.Elements[0].NetworkId, Is.Null);
                Assert.That(WfObjectTaskHelper.HasValidElementStructure(task), Is.True);
            });
        }

        [Test]
        public async Task ApplyToTask_RejectsAnInvalidAddress()
        {
            await using BunitContext context = CreateContext();
            WfReqTask task = new() { TaskType = WfTaskType.object_create.ToString() };
            IRenderedComponent<DisplayObjectTaskElement> component = RenderElement(context, task, false, kManagementId);
            component.Find("input[placeholder='ip_addresses']").Input("10.1.300.1");

            Assert.Multiple(() =>
            {
                Assert.That(component.Instance.ApplyToTask(), Is.EqualTo("E5124"));
                Assert.That(task.Elements, Is.Empty);
                Assert.That(component.Markup, Does.Contain("is-invalid"));
            });
        }

        [Test]
        public async Task ApplyToTask_CreatesAServiceElement()
        {
            await using BunitContext context = CreateContext();
            WfReqTask task = new() { TaskType = WfTaskType.object_create.ToString() };
            IRenderedComponent<DisplayObjectTaskElement> component = RenderElement(context, task, false, kManagementId);
            await InvokePrivate(component, "SetKind", true);
            SetMember(component.Instance, "ProtoId", kTcp);
            SetMember(component.Instance, "PortInput", "8000-8080");

            string? error = component.Instance.ApplyToTask();

            Assert.Multiple(() =>
            {
                Assert.That(error, Is.Null);
                Assert.That(task.Elements, Has.Count.EqualTo(1));
                Assert.That(task.Elements[0].Field, Is.EqualTo(ElemFieldType.service.ToString()));
                Assert.That(task.Elements[0].Port, Is.EqualTo(8000));
                Assert.That(task.Elements[0].PortEnd, Is.EqualTo(8080));
                Assert.That(task.Elements[0].ProtoId, Is.EqualTo(kTcp));
            });
        }

        [Test]
        public async Task ApplyToTask_RejectsATcpServiceWithoutPort()
        {
            await using BunitContext context = CreateContext();
            IRenderedComponent<DisplayObjectTaskElement> component = RenderElement(context, new WfReqTask(), false, kManagementId);
            await InvokePrivate(component, "SetKind", true);
            SetMember(component.Instance, "ProtoId", kTcp);

            Assert.That(component.Instance.ApplyToTask(), Is.EqualTo("E5124"));
        }

        [Test]
        public async Task ApplyToTask_RequiresTheObjectToModify()
        {
            await using BunitContext context = CreateContext();
            IRenderedComponent<DisplayObjectTaskElement> component = RenderElement(context, new WfReqTask(), true, kManagementId);
            SetMember(component.Instance, "NetworkInput", "10.1.1.6");

            Assert.That(component.Instance.ApplyToTask(), Is.EqualTo("E5126"));
        }

        [Test]
        public async Task ApplyToTask_StoresPreviousAndRequestedStateOfAModifiedObject()
        {
            await using BunitContext context = CreateContext();
            WfReqTask task = new() { Id = 5, TaskType = WfTaskType.object_modify.ToString() };
            IRenderedComponent<DisplayObjectTaskElement> component = RenderElement(context, task, true, kManagementId);
            NetworkObject imported = new() { Id = 4711, Name = "srv_web01", IP = "10.1.1.5/32", IpEnd = "10.1.1.5/32" };
            await InvokePrivate(component, "SelectNetworkObject", imported);

            string? unchangedError = component.Instance.ApplyToTask();
            SetMember(component.Instance, "NetworkInput", "10.1.1.6");
            string? error = component.Instance.ApplyToTask();

            Assert.Multiple(() =>
            {
                Assert.That(GetMember<string>(component.Instance, "NetworkInput"), Is.EqualTo("10.1.1.6"));
                Assert.That(unchangedError, Is.EqualTo("E5125"));
                Assert.That(error, Is.Null);
                Assert.That(WfObjectTaskHelper.HasValidElementStructure(task), Is.True);
                Assert.That(WfObjectTaskHelper.GetOriginalElement(task)?.NetworkId, Is.EqualTo(4711));
                Assert.That(WfObjectTaskHelper.GetOriginalElement(task)?.Name, Is.EqualTo("srv_web01"));
                Assert.That(WfObjectTaskHelper.GetRequestedElement(task)?.NetworkId, Is.EqualTo(4711));
                Assert.That(WfObjectTaskHelper.GetRequestedElement(task)?.Name, Is.EqualTo("srv_web01"));
            });
        }

        [Test]
        public async Task ChangingTheManagement_DropsTheSelectedObject()
        {
            await using BunitContext context = CreateContext();
            WfReqTask task = new() { Id = 5, TaskType = WfTaskType.object_modify.ToString() };
            IRenderedComponent<DisplayObjectTaskElement> component = RenderElement(context, task, true, kManagementId);
            NetworkObject imported = new() { Id = 4711, Name = "srv_web01", IP = "10.1.1.5/32", IpEnd = "10.1.1.5/32" };
            await InvokePrivate(component, "SelectNetworkObject", imported);

            component.Render(parameters => parameters.Add(p => p.ManagementId, kManagementId + 1));
            SetMember(component.Instance, "NetworkInput", "10.1.1.6");

            Assert.Multiple(() =>
            {
                Assert.That(GetMember<WfReqElement>(component.Instance, "original"), Is.Null);
                Assert.That(GetMember<string>(component.Instance, "objectName"), Is.Null);
                Assert.That(component.Instance.ApplyToTask(), Is.EqualTo("E5126"));
                Assert.That(task.Elements, Is.Empty);
            });
        }

        [Test]
        public async Task ChangingTheManagement_DropsTheObjectOfAnExistingTask()
        {
            await using BunitContext context = CreateContext();
            WfReqTask task = new() { Id = 5, TaskType = WfTaskType.object_modify.ToString(), ManagementId = kManagementId };
            task.Elements.Add(new() { Id = 1, Field = ElemFieldType.source.ToString(), RequestAction = RequestAction.unchanged.ToString(), Name = "srv_web01", NetworkId = 4711, Cidr = new("10.1.1.5") });
            task.Elements.Add(new() { Id = 2, Field = ElemFieldType.source.ToString(), RequestAction = RequestAction.modify.ToString(), Name = "srv_web01", NetworkId = 4711, Cidr = new("10.1.1.6") });
            IRenderedComponent<DisplayObjectTaskElement> component = RenderElement(context, task, true, kManagementId);

            component.Render(parameters => parameters.Add(p => p.ManagementId, kManagementId + 1));
            SetMember(component.Instance, "NetworkInput", "10.1.1.7");

            Assert.Multiple(() =>
            {
                Assert.That(component.Instance.ApplyToTask(), Is.EqualTo("E5126"));
                Assert.That(task.Elements.ConvertAll(element => element.Id), Is.EquivalentTo(kKeptElementIds));
            });
        }

        [Test]
        public async Task SetKind_KeepsTheTypedNameOfAnObjectToCreate()
        {
            await using BunitContext context = CreateContext();
            IRenderedComponent<DisplayObjectTaskElement> component = RenderElement(context, new WfReqTask(), false, kManagementId);
            SetMember(component.Instance, "objectName", "new_object");

            await InvokePrivate(component, "SetKind", true);

            Assert.That(GetMember<string>(component.Instance, "objectName"), Is.EqualTo("new_object"));
        }

        [Test]
        public async Task SelectService_PrefillsTheServiceValues()
        {
            await using BunitContext context = CreateContext();
            IRenderedComponent<DisplayObjectTaskElement> component = RenderElement(context, new WfReqTask { TaskType = WfTaskType.object_modify.ToString() }, true, kManagementId);
            await InvokePrivate(component, "SetKind", true);
            NetworkService imported = new() { Id = 815, Name = "https_alt", ProtoId = kTcp, DestinationPort = 8443 };

            await InvokePrivate(component, "SelectService", imported);

            Assert.Multiple(() =>
            {
                Assert.That(GetMember<string>(component.Instance, "PortInput"), Is.EqualTo("8443"));
                Assert.That(GetMember<string>(component.Instance, "objectName"), Is.EqualTo("https_alt"));
                Assert.That(component.Instance.ApplyToTask(), Is.EqualTo("E5125"));
            });
        }

        [Test]
        public async Task ExistingTask_IsShownWithPreviousAndRequestedStateInReadOnlyMode()
        {
            await using BunitContext context = CreateContext();
            WfReqTask task = new() { TaskType = WfTaskType.object_modify.ToString() };
            task.Elements.Add(new() { Id = 1, Field = ElemFieldType.source.ToString(), RequestAction = RequestAction.unchanged.ToString(), Name = "srv_web01", NetworkId = 4711, Cidr = new("10.1.1.5") });
            task.Elements.Add(new() { Id = 2, Field = ElemFieldType.source.ToString(), RequestAction = RequestAction.modify.ToString(), Name = "srv_web01", NetworkId = 4711, Cidr = new("10.1.1.6") });

            IRenderedComponent<DisplayObjectTaskElement> component = RenderElement(context, task, true, kManagementId, false);

            Assert.Multiple(() =>
            {
                Assert.That(component.Markup, Does.Contain("10.1.1.5"));
                Assert.That(component.Markup, Does.Contain("10.1.1.6"));
                Assert.That(component.Markup, Does.Contain("previous_state"));
                Assert.That(component.Markup, Does.Contain("requested_state"));
                Assert.That(component.FindComponents<ObjectSelector>(), Is.Empty);
            });
        }

        [Test]
        public async Task ExistingTask_PrefillsTheInputsAndKeepsTheElementIds()
        {
            await using BunitContext context = CreateContext();
            WfReqTask task = new() { Id = 5, TaskType = WfTaskType.object_modify.ToString() };
            task.Elements.Add(new() { Id = 1, Field = ElemFieldType.source.ToString(), RequestAction = RequestAction.unchanged.ToString(), Name = "srv_web01", NetworkId = 4711, Cidr = new("10.1.1.5") });
            task.Elements.Add(new() { Id = 2, Field = ElemFieldType.source.ToString(), RequestAction = RequestAction.modify.ToString(), Name = "srv_web01", NetworkId = 4711, Cidr = new("10.1.1.6") });
            IRenderedComponent<DisplayObjectTaskElement> component = RenderElement(context, task, true, kManagementId);

            SetMember(component.Instance, "NetworkInput", "10.1.1.7");
            string? error = component.Instance.ApplyToTask();

            Assert.Multiple(() =>
            {
                Assert.That(error, Is.Null);
                Assert.That(task.Elements.ConvertAll(element => element.Id), Is.EquivalentTo(kKeptElementIds));
                Assert.That(task.RemovedElements, Is.Empty);
                Assert.That(component.FindComponents<ObjectSelector>(), Has.Count.EqualTo(1));
            });
        }

        [Test]
        public async Task ModifyWithoutManagement_AsksForTheManagementInsteadOfSearching()
        {
            await using BunitContext context = CreateContext();

            IRenderedComponent<DisplayObjectTaskElement> component = RenderElement(context, new WfReqTask(), true, null);

            Assert.Multiple(() =>
            {
                Assert.That(component.FindComponents<ObjectSelector>(), Is.Empty);
                Assert.That(component.Markup, Does.Contain("select_management_first"));
            });
        }

        [Test]
        public async Task ObjectSelector_ShowsTheSearchHitsAndTheRefineHintAtTheLimit()
        {
            List<NetworkObject> hits = [.. Enumerable.Range(1, WfObjectTaskHelper.kSearchLimit).Select(id => new NetworkObject { Id = id, Name = $"srv_{id}", IP = "10.1.1.1/32" })];
            SearchApiConnection apiConnection = new() { Result = hits };
            await using BunitContext context = CreateContext(apiConnection);
            IRenderedComponent<ObjectSelector> component = context.Render<ObjectSelector>(parameters => parameters
                .AddCascadingValue(Task.FromResult(new AuthenticationState(new ClaimsPrincipal())))
                .Add(p => p.ManagementId, kManagementId)
                .Add(p => p.IpProtos, kIpProtos));
            MethodInfo search = typeof(ObjectSelector).GetMethod("Search", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMethodException(typeof(ObjectSelector).FullName, "Search");
            List<object?> arguments = ["srv"];

            await component.InvokeAsync(() => (Task)search.Invoke(component.Instance, arguments.ToArray())!);

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.QueryCount, Is.EqualTo(1));
                Assert.That(component.FindComponent<FlowObjectTable<NetworkObject>>().Instance.FilteredItems.Count(), Is.EqualTo(WfObjectTaskHelper.kSearchLimit));
                Assert.That(component.Markup, Does.Contain("refine_search"));
                Assert.That(component.Markup, Does.Contain("search_network_object"));
            });
        }

        [Test]
        public async Task ObjectSelector_ReportsTheSelectedObject()
        {
            List<NetworkObject> hits = [new() { Id = 4711, Name = "srv_web01", IP = "10.1.1.5/32", IpEnd = "10.1.1.5/32" }];
            SearchApiConnection apiConnection = new() { Result = hits };
            await using BunitContext context = CreateContext(apiConnection);
            NetworkObject? selected = null;
            IRenderedComponent<ObjectSelector> component = context.Render<ObjectSelector>(parameters => parameters
                .AddCascadingValue(Task.FromResult(new AuthenticationState(new ClaimsPrincipal())))
                .Add(p => p.ManagementId, kManagementId)
                .Add(p => p.IpProtos, kIpProtos)
                .Add(p => p.SelectedNetworkObjectChanged, (NetworkObject? networkObject) => selected = networkObject));
            MethodInfo search = typeof(ObjectSelector).GetMethod("Search", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMethodException(typeof(ObjectSelector).FullName, "Search");
            List<object?> arguments = ["srv"];
            await component.InvokeAsync(() => (Task)search.Invoke(component.Instance, arguments.ToArray())!);

            component.Find("tbody button").Click();

            Assert.Multiple(() =>
            {
                Assert.That(selected?.Id, Is.EqualTo(4711));
                Assert.That(component.Markup, Does.Contain("srv_web01"));
                Assert.That(component.Find("tbody button").ClassList, Does.Contain("btn-success"));
            });
        }

        [Test]
        public async Task ObjectSelector_SearchesServicesInServiceMode()
        {
            SearchApiConnection apiConnection = new() { Result = new List<NetworkService> { new() { Id = 1, Name = "https", ProtoId = kTcp, DestinationPort = 443 } } };
            await using BunitContext context = CreateContext(apiConnection);
            IRenderedComponent<ObjectSelector> component = context.Render<ObjectSelector>(parameters => parameters
                .AddCascadingValue(Task.FromResult(new AuthenticationState(new ClaimsPrincipal())))
                .Add(p => p.ManagementId, kManagementId)
                .Add(p => p.IsService, true)
                .Add(p => p.IpProtos, kIpProtos));
            MethodInfo search = typeof(ObjectSelector).GetMethod("Search", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMethodException(typeof(ObjectSelector).FullName, "Search");
            List<object?> arguments = ["https"];

            await component.InvokeAsync(() => (Task)search.Invoke(component.Instance, arguments.ToArray())!);

            Assert.Multiple(() =>
            {
                Assert.That(component.FindComponent<FlowObjectTable<NetworkService>>().Instance.FilteredItems.Count(), Is.EqualTo(1));
                Assert.That(component.Markup, Does.Not.Contain("refine_search"));
                Assert.That(component.Markup, Does.Contain("search_service_object"));
            });
        }
    }
}
