using BlazorTable;
using Bunit;
using FWO.Api.Client;
using FWO.Basics.Exceptions;
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
        private static readonly DateTime kTimeStart = new(2026, 11, 1, 8, 0, 0);
        private static readonly DateTime kTimeEnd = new(2026, 11, 30, 18, 0, 0);

        private sealed class SearchApiConnection : SimulatedApiConnection
        {
            public object Result { get; set; } = new List<NetworkObject>();
            public Exception? QueryException { get; set; }
            public int QueryCount { get; private set; }

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                QueryCount++;
                if (QueryException != null)
                {
                    return Task.FromException<QueryResponseType>(QueryException);
                }
                return Task.FromResult((QueryResponseType)Result);
            }
        }

        private sealed class DeferredGroupApiConnection : SimulatedApiConnection
        {
            public Dictionary<long, TaskCompletionSource<List<NetworkObject>>> Responses { get; } = [];

            public override async Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                PropertyInfo groupIdProperty = variables?.GetType().GetProperty("groupId")
                    ?? throw new AssertionException("The group id was not passed to the group-member query.");
                long groupId = Convert.ToInt64(groupIdProperty.GetValue(variables));
                List<NetworkObject> result = await Responses[groupId].Task;
                return (QueryResponseType)(object)result;
            }
        }

        private sealed class LocalizedUserConfig(string groupLimitText) : SimulatedUserConfig
        {
            private readonly string localizedGroupLimitText = groupLimitText;

            public override string GetText(string key)
            {
                return key == "E5130" ? localizedGroupLimitText : base.GetText(key);
            }
        }

        private static BunitContext CreateContext(ApiConnection? apiConnection = null, UserConfig? userConfig = null)
        {
            BunitContext context = new();
            context.Services.AddSingleton<UserConfig>(userConfig ?? new SimulatedUserConfig());
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
            await InvokePrivate(component, "SetKind", ObjectTaskKind.Service);
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
            await InvokePrivate(component, "SetKind", ObjectTaskKind.Service);
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

            await InvokePrivate(component, "SetKind", ObjectTaskKind.Service);

            Assert.That(GetMember<string>(component.Instance, "objectName"), Is.EqualTo("new_object"));
        }

        [Test]
        public async Task SelectService_PrefillsTheServiceValues()
        {
            await using BunitContext context = CreateContext();
            IRenderedComponent<DisplayObjectTaskElement> component = RenderElement(context, new WfReqTask { TaskType = WfTaskType.object_modify.ToString() }, true, kManagementId);
            await InvokePrivate(component, "SetKind", ObjectTaskKind.Service);
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
        public async Task CreateTask_IsShownWithNameAndContentInReadOnlyMode()
        {
            await using BunitContext context = CreateContext();
            WfReqTask task = new() { TaskType = WfTaskType.object_create.ToString() };
            task.Elements.Add(new()
            {
                Field = ElemFieldType.source.ToString(),
                RequestAction = RequestAction.create.ToString(),
                Name = "srv_web01",
                IpString = "10.1.1.5"
            });

            IRenderedComponent<DisplayObjectTaskElement> component = RenderElement(context, task, false, kManagementId, false);

            Assert.Multiple(() =>
            {
                Assert.That(component.Markup, Does.Contain("srv_web01"));
                Assert.That(component.Markup, Does.Contain("10.1.1.5"));
                Assert.That(component.Markup, Does.Not.Contain("requested_state"));
                Assert.That(component.FindAll(".list-group-item"), Has.Count.EqualTo(1));
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
            // the click reaches the callback asynchronously, which can take longer on a slow machine
            component.WaitForAssertion(() => Assert.That(selected?.Id, Is.EqualTo(4711)));

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
                .Add(p => p.Kind, ObjectTaskKind.Service)
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

        [Test]
        public async Task ObjectSelector_SearchesNetworkGroupsInGroupMode()
        {
            SearchApiConnection apiConnection = new() { Result = new List<NetworkObject> { new() { Id = 12, Name = "web_group", MemberNames = "web01" } } };
            await using BunitContext context = CreateContext(apiConnection);
            IRenderedComponent<ObjectSelector> component = context.Render<ObjectSelector>(parameters => parameters
                .AddCascadingValue(Task.FromResult(new AuthenticationState(new ClaimsPrincipal())))
                .Add(p => p.ManagementId, kManagementId)
                .Add(p => p.GroupMode, true)
                .Add(p => p.IpProtos, kIpProtos));
            MethodInfo search = typeof(ObjectSelector).GetMethod("Search", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMethodException(typeof(ObjectSelector).FullName, "Search");

            List<object?> arguments = ["web"];
            await component.InvokeAsync(() => (Task)search.Invoke(component.Instance, arguments.ToArray())!);

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.QueryCount, Is.EqualTo(1));
                Assert.That(component.FindComponent<FlowObjectTable<NetworkObject>>().Instance.FilteredItems.Single().Name, Is.EqualTo("web_group"));
                Assert.That(component.Markup, Does.Contain("Members"));
                Assert.That(component.Markup, Does.Contain("search_network_object"));
            });
        }

        [Test]
        public async Task ObjectSelector_GroupSelectionIgnoresAnOlderMemberLoad()
        {
            DeferredGroupApiConnection apiConnection = new();
            apiConnection.Responses[1] = new(TaskCreationOptions.RunContinuationsAsynchronously);
            apiConnection.Responses[2] = new(TaskCreationOptions.RunContinuationsAsynchronously);
            await using BunitContext context = CreateContext(apiConnection);
            List<long> selectedIds = [];
            IRenderedComponent<ObjectSelector> component = context.Render<ObjectSelector>(parameters => parameters
                .AddCascadingValue(Task.FromResult(new AuthenticationState(new ClaimsPrincipal())))
                .Add(p => p.ManagementId, kManagementId)
                .Add(p => p.GroupMode, true)
                .Add(p => p.SelectedNetworkObjectChanged, (NetworkObject? selected) => selectedIds.Add(selected?.Id ?? -1)));
            MethodInfo select = typeof(ObjectSelector).GetMethod("SelectNetworkObject", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMethodException(typeof(ObjectSelector).FullName, "SelectNetworkObject");
            NetworkObject firstGroup = new() { Id = 1, Name = "first" };
            NetworkObject secondGroup = new() { Id = 2, Name = "second" };

            List<object?> firstArguments = [firstGroup];
            Task firstSelection = (Task)select.Invoke(component.Instance, firstArguments.ToArray())!;
            List<object?> secondArguments = [secondGroup];
            Task secondSelection = (Task)select.Invoke(component.Instance, secondArguments.ToArray())!;

            NetworkObject loadedSecondGroup = new() { Id = secondGroup.Id, Name = secondGroup.Name };
            loadedSecondGroup.ObjectGroups = [new Group<NetworkObject> { Id = 20, Object = new NetworkObject { Id = 20, Name = "second-member" } }];
            List<NetworkObject> loadedSecondResult = [loadedSecondGroup];
            apiConnection.Responses[2].SetResult(loadedSecondResult);
            await secondSelection;
            NetworkObject loadedFirstGroup = new() { Id = firstGroup.Id, Name = firstGroup.Name };
            loadedFirstGroup.ObjectGroups = [new Group<NetworkObject> { Id = 10, Object = new NetworkObject { Id = 10, Name = "first-member" } }];
            List<NetworkObject> loadedFirstResult = [loadedFirstGroup];
            apiConnection.Responses[1].SetResult(loadedFirstResult);
            await firstSelection;

            Assert.Multiple(() =>
            {
                Assert.That(component.Instance.SelectedNetworkObject?.Id, Is.EqualTo(2));
                Assert.That(selectedIds, Has.Count.EqualTo(1));
                Assert.That(selectedIds[0], Is.EqualTo(2));
            });
        }

        [TestCase("The selected group has too many members to edit.")]
        [TestCase("Die ausgewählte Gruppe enthält zu viele Mitglieder und kann nicht bearbeitet werden.")]
        public async Task ObjectSelector_DisplaysLocalizedMessageWhenGroupIsTooLarge(string expectedMessage)
        {
            NetworkObject group = new() { Id = 41, Name = "large-group" };
            group.ObjectGroups = Enumerable.Range(1, WfObjectTaskHelper.kGroupMemberLimit + 1)
                .Select(id => new Group<NetworkObject> { Id = id, Object = new NetworkObject { Id = id } })
                .ToArray();
            SearchApiConnection apiConnection = new() { Result = new List<NetworkObject> { group } };
            List<(Exception? Exception, string Title, string Message, bool IsError)> displayedMessages = [];
            UserConfig userConfig = new LocalizedUserConfig(expectedMessage);
            await using BunitContext context = CreateContext(apiConnection, userConfig);
            IRenderedComponent<ObjectSelector> component = context.Render<ObjectSelector>(parameters => parameters
                .AddCascadingValue(Task.FromResult(new AuthenticationState(new ClaimsPrincipal())))
                .AddCascadingValue((Action<Exception?, string, string, bool>)((exception, title, message, isError) =>
                    displayedMessages.Add((exception, title, message, isError))))
                .Add(p => p.ManagementId, kManagementId)
                .Add(p => p.GroupMode, true)
                .Add(p => p.IpProtos, kIpProtos));
            MethodInfo select = typeof(ObjectSelector).GetMethod("SelectNetworkObject", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMethodException(typeof(ObjectSelector).FullName, "SelectNetworkObject");

            List<object?> arguments = [group];
            Task selection = (Task)select.Invoke(component.Instance, arguments.ToArray())!;
            await selection;

            Assert.Multiple(() =>
            {
                Assert.That(displayedMessages, Has.Count.EqualTo(1));
                Assert.That(displayedMessages[0].Exception, Is.Null);
                Assert.That(displayedMessages[0].Message, Is.EqualTo(expectedMessage));
                Assert.That(displayedMessages[0].IsError, Is.True);
            });
        }

        [Test]
        public async Task ObjectSelector_DisplaysLocalizedMessageWhenServiceGroupIsTooLarge()
        {
            NetworkService group = new() { Id = 42, Name = "large-service-group" };
            group.ServiceGroups = Enumerable.Range(1, WfObjectTaskHelper.kGroupMemberLimit + 1)
                .Select(id => new Group<NetworkService> { Id = id, Object = new NetworkService { Id = id } })
                .ToArray();
            SearchApiConnection apiConnection = new() { Result = new List<NetworkService> { group } };
            List<(Exception? Exception, string Title, string Message, bool IsError)> displayedMessages = [];
            await using BunitContext context = CreateContext(apiConnection, new LocalizedUserConfig("localized group limit"));
            IRenderedComponent<ObjectSelector> component = context.Render<ObjectSelector>(parameters => parameters
                .AddCascadingValue(Task.FromResult(new AuthenticationState(new ClaimsPrincipal())))
                .AddCascadingValue((Action<Exception?, string, string, bool>)((exception, title, message, isError) =>
                    displayedMessages.Add((exception, title, message, isError))))
                .Add(p => p.ManagementId, kManagementId)
                .Add(p => p.Kind, ObjectTaskKind.Service)
                .Add(p => p.GroupMode, true)
                .Add(p => p.IpProtos, kIpProtos));
            MethodInfo select = typeof(ObjectSelector).GetMethod("SelectService", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMethodException(typeof(ObjectSelector).FullName, "SelectService");

            List<object?> arguments = [group];
            Task selection = (Task)select.Invoke(component.Instance, arguments.ToArray())!;
            await selection;

            Assert.That(displayedMessages.Single().Message, Is.EqualTo("localized group limit"));
            Assert.That(displayedMessages.Single().Exception, Is.Null);
        }

        [Test]
        public async Task ObjectSelector_ReportsNetworkGroupLoadErrors()
        {
            InvalidOperationException expectedException = new("network group lookup failed");
            SearchApiConnection apiConnection = new() { QueryException = expectedException };
            List<(Exception? Exception, string Title, string Message, bool IsError)> displayedMessages = [];
            await using BunitContext context = CreateContext(apiConnection);
            IRenderedComponent<ObjectSelector> component = context.Render<ObjectSelector>(parameters => parameters
                .AddCascadingValue(Task.FromResult(new AuthenticationState(new ClaimsPrincipal())))
                .AddCascadingValue((Action<Exception?, string, string, bool>)((exception, title, message, isError) =>
                    displayedMessages.Add((exception, title, message, isError))))
                .Add(p => p.ManagementId, kManagementId)
                .Add(p => p.GroupMode, true)
                .Add(p => p.IpProtos, kIpProtos));
            MethodInfo select = typeof(ObjectSelector).GetMethod("SelectNetworkObject", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMethodException(typeof(ObjectSelector).FullName, "SelectNetworkObject");

            List<object?> arguments = [new NetworkObject { Id = 41, Name = "network-group" }];
            await (Task)select.Invoke(component.Instance, arguments.ToArray())!;

            Assert.Multiple(() =>
            {
                Assert.That(displayedMessages.Single().Exception, Is.SameAs(expectedException));
                Assert.That(displayedMessages.Single().Message, Is.Empty);
                Assert.That(component.Instance.SelectedNetworkObject, Is.Null);
            });
        }

        [Test]
        public async Task ObjectSelector_ReportsServiceGroupLoadErrors()
        {
            InvalidOperationException expectedException = new("service group lookup failed");
            SearchApiConnection apiConnection = new() { QueryException = expectedException };
            List<(Exception? Exception, string Title, string Message, bool IsError)> displayedMessages = [];
            await using BunitContext context = CreateContext(apiConnection);
            IRenderedComponent<ObjectSelector> component = context.Render<ObjectSelector>(parameters => parameters
                .AddCascadingValue(Task.FromResult(new AuthenticationState(new ClaimsPrincipal())))
                .AddCascadingValue((Action<Exception?, string, string, bool>)((exception, title, message, isError) =>
                    displayedMessages.Add((exception, title, message, isError))))
                .Add(p => p.ManagementId, kManagementId)
                .Add(p => p.Kind, ObjectTaskKind.Service)
                .Add(p => p.GroupMode, true)
                .Add(p => p.IpProtos, kIpProtos));
            MethodInfo select = typeof(ObjectSelector).GetMethod("SelectService", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMethodException(typeof(ObjectSelector).FullName, "SelectService");

            List<object?> arguments = [new NetworkService { Id = 42, Name = "service-group" }];
            await (Task)select.Invoke(component.Instance, arguments.ToArray())!;

            Assert.Multiple(() =>
            {
                Assert.That(displayedMessages.Single().Exception, Is.SameAs(expectedException));
                Assert.That(displayedMessages.Single().Message, Is.Empty);
                Assert.That(component.Instance.SelectedService, Is.Null);
            });
        }

        [Test]
        public async Task ObjectSelector_SearchesServiceGroupsInGroupMode()
        {
            SearchApiConnection apiConnection = new() { Result = new List<NetworkService> { new() { Id = 13, Name = "https_group", MemberNames = "https" } } };
            await using BunitContext context = CreateContext(apiConnection);
            IRenderedComponent<ObjectSelector> component = context.Render<ObjectSelector>(parameters => parameters
                .AddCascadingValue(Task.FromResult(new AuthenticationState(new ClaimsPrincipal())))
                .Add(p => p.ManagementId, kManagementId)
                .Add(p => p.Kind, ObjectTaskKind.Service)
                .Add(p => p.GroupMode, true)
                .Add(p => p.IpProtos, kIpProtos));
            MethodInfo search = typeof(ObjectSelector).GetMethod("Search", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMethodException(typeof(ObjectSelector).FullName, "Search");

            List<object?> arguments = ["https"];
            await component.InvokeAsync(() => (Task)search.Invoke(component.Instance, arguments.ToArray())!);

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.QueryCount, Is.EqualTo(1));
                Assert.That(component.FindComponent<FlowObjectTable<NetworkService>>().Instance.FilteredItems.Single().Name, Is.EqualTo("https_group"));
                Assert.That(component.Markup, Does.Contain("Members"));
                Assert.That(component.Markup, Does.Contain("search_service_object"));
            });
        }

        [Test]
        public async Task ApplyToTask_CreatesATimeElement()
        {
            await using BunitContext context = CreateContext();
            WfReqTask task = new() { TaskType = WfTaskType.object_create.ToString() };
            IRenderedComponent<DisplayObjectTaskElement> component = RenderElement(context, task, false, kManagementId);
            component.Find("#objKindTime").Change(true);
            SetMember(component.Instance, "objectName", " maintenance ");
            component.Find("#objStartDate").Input("2026-11-01");
            component.Find("#objStartTime").Input("08:00");
            component.Find("#objEndDate").Input("2026-11-30");
            component.Find("#objEndTime").Input("18:00");

            string? error = component.Instance.ApplyToTask();

            Assert.Multiple(() =>
            {
                Assert.That(error, Is.Null);
                Assert.That(task.Elements, Has.Count.EqualTo(1));
                Assert.That(task.Elements[0].Field, Is.EqualTo(ElemFieldType.time.ToString()));
                Assert.That(task.Elements[0].RequestAction, Is.EqualTo(RequestAction.create.ToString()));
                Assert.That(task.Elements[0].Name, Is.EqualTo("maintenance"));
                Assert.That(task.Elements[0].StartTime, Is.EqualTo(kTimeStart));
                Assert.That(task.Elements[0].EndTime, Is.EqualTo(kTimeEnd));
                Assert.That(task.Elements[0].TimeObjId, Is.Null);
                Assert.That(WfObjectTaskHelper.HasValidElementStructure(task), Is.True);
            });
        }

        [Test]
        public async Task ApplyToTask_RejectsATimeObjectEndingBeforeItStarts()
        {
            await using BunitContext context = CreateContext();
            WfReqTask task = new() { TaskType = WfTaskType.object_create.ToString() };
            IRenderedComponent<DisplayObjectTaskElement> component = RenderElement(context, task, false, kManagementId);
            component.Find("#objKindTime").Change(true);
            SetMember(component.Instance, "objectName", "maintenance");
            component.Find("#objStartDate").Input("2026-11-30");
            component.Find("#objEndDate").Input("2026-11-01");

            Assert.Multiple(() =>
            {
                Assert.That(component.Instance.ApplyToTask(), Is.EqualTo("E5124"));
                Assert.That(task.Elements, Is.Empty);
                Assert.That(component.Find("#objEndDate").ClassList, Does.Contain("is-invalid"));
                Assert.That(component.Find("#objEndTime").ClassList, Does.Contain("is-invalid"));
                Assert.That(component.Find("#objStartDate").ClassList, Does.Not.Contain("is-invalid"));
            });
        }

        [Test]
        public async Task KindButtons_SwitchToTheTimeInputs()
        {
            await using BunitContext context = CreateContext();
            IRenderedComponent<DisplayObjectTaskElement> component = RenderElement(context, new WfReqTask(), false, kManagementId);

            component.Find("#objKindTime").Change(true);

            Assert.Multiple(() =>
            {
                Assert.That(component.FindAll("input.btn-check[name='objectKind']"), Has.Count.EqualTo(3));
                Assert.That(component.Find("#objKindTime").HasAttribute("checked"), Is.True);
                Assert.That(component.Find("#objKindNetwork").HasAttribute("checked"), Is.False);
                Assert.That(component.FindAll("#objStartDate"), Has.Count.EqualTo(1));
                Assert.That(component.FindAll("input[placeholder='ip_addresses']"), Is.Empty);
            });
        }

        [Test]
        public async Task TimeInputs_KeepATypedDateAndTakeTheDefaultTime()
        {
            await using BunitContext context = CreateContext();
            WfReqTask task = new() { TaskType = WfTaskType.object_create.ToString() };
            IRenderedComponent<DisplayObjectTaskElement> component = RenderElement(context, task, false, kManagementId);
            component.Find("#objKindTime").Change(true);
            SetMember(component.Instance, "objectName", "maintenance");

            component.Find("#objStartDate").Input("2026-11-01");
            component.Find("#objEndDate").Input("2026-11-30");
            component.Find("#objEndTime").Input("18:00");
            string? error = component.Instance.ApplyToTask();

            Assert.Multiple(() =>
            {
                Assert.That(component.Find("#objStartDate").GetAttribute("value"), Is.EqualTo("2026-11-01"));
                Assert.That(component.Find("#objStartTime").GetAttribute("value"), Is.EqualTo(WfObjectTaskHelper.kDefaultStartTimeInput));
                Assert.That(component.Find("#objEndTime").GetAttribute("value"), Is.EqualTo("18:00"));
                Assert.That(error, Is.Null);
                Assert.That(task.Elements[0].StartTime, Is.EqualTo(kTimeStart.Date));
                Assert.That(task.Elements[0].EndTime, Is.EqualTo(kTimeEnd));
            });
        }

        [Test]
        public async Task TimeInputs_RejectATimeWithoutDate()
        {
            await using BunitContext context = CreateContext();
            WfReqTask task = new() { TaskType = WfTaskType.object_create.ToString() };
            IRenderedComponent<DisplayObjectTaskElement> component = RenderElement(context, task, false, kManagementId);
            component.Find("#objKindTime").Change(true);
            SetMember(component.Instance, "objectName", "maintenance");
            component.Find("#objEndDate").Input("2026-11-30");

            component.Find("#objStartTime").Input("08:00");

            Assert.Multiple(() =>
            {
                Assert.That(component.Find("#objStartTime").GetAttribute("value"), Is.EqualTo("08:00"));
                Assert.That(component.Find("#objStartDate").ClassList, Does.Contain("is-invalid"));
                Assert.That(component.Instance.ApplyToTask(), Is.EqualTo("E5124"));
                Assert.That(task.Elements, Is.Empty);
            });
        }

        [Test]
        public async Task ApplyToTask_StoresPreviousAndRequestedStateOfAModifiedTimeObject()
        {
            await using BunitContext context = CreateContext();
            WfReqTask task = new() { Id = 5, TaskType = WfTaskType.object_modify.ToString() };
            IRenderedComponent<DisplayObjectTaskElement> component = RenderElement(context, task, true, kManagementId);
            component.Find("#objKindTime").Change(true);
            TimeObject imported = new() { Id = 77, Name = "maintenance", StartTime = kTimeStart, EndTime = kTimeEnd };
            await InvokePrivate(component, "SelectTimeObject", imported);

            string? unchangedError = component.Instance.ApplyToTask();
            component.Find("#objEndDate").Input("2026-12-01");
            string? error = component.Instance.ApplyToTask();

            Assert.Multiple(() =>
            {
                Assert.That(component.Find("#objStartDate").GetAttribute("value"), Is.EqualTo("2026-11-01"));
                Assert.That(component.Find("#objStartTime").GetAttribute("value"), Is.EqualTo("08:00"));
                Assert.That(unchangedError, Is.EqualTo("E5125"));
                Assert.That(error, Is.Null);
                Assert.That(WfObjectTaskHelper.HasValidElementStructure(task), Is.True);
                Assert.That(WfObjectTaskHelper.GetOriginalElement(task)?.TimeObjId, Is.EqualTo(77));
                Assert.That(WfObjectTaskHelper.GetOriginalElement(task)?.EndTime, Is.EqualTo(kTimeEnd));
                Assert.That(WfObjectTaskHelper.GetRequestedElement(task)?.TimeObjId, Is.EqualTo(77));
                Assert.That(WfObjectTaskHelper.GetRequestedElement(task)?.EndTime, Is.EqualTo(kTimeEnd.AddDays(1)));
            });
        }

        [Test]
        public async Task ExistingTimeTask_IsPrefilledAndShownReadOnly()
        {
            await using BunitContext context = CreateContext();
            WfReqTask task = new() { Id = 5, TaskType = WfTaskType.object_modify.ToString(), ManagementId = kManagementId };
            task.Elements.Add(new() { Id = 1, Field = ElemFieldType.time.ToString(), RequestAction = RequestAction.unchanged.ToString(), Name = "maintenance", TimeObjId = 77, StartTime = kTimeStart, EndTime = kTimeEnd });
            task.Elements.Add(new() { Id = 2, Field = ElemFieldType.time.ToString(), RequestAction = RequestAction.modify.ToString(), Name = "maintenance", TimeObjId = 77, StartTime = kTimeStart, EndTime = kTimeEnd.AddDays(1) });

            IRenderedComponent<DisplayObjectTaskElement> editComponent = RenderElement(context, task, true, kManagementId);
            IRenderedComponent<DisplayObjectTaskElement> readOnlyComponent = RenderElement(context, task, true, kManagementId, false);

            Assert.Multiple(() =>
            {
                Assert.That(GetMember<ObjectTaskKind>(editComponent.Instance, "kind"), Is.EqualTo(ObjectTaskKind.Time));
                Assert.That(GetMember<DateTime?>(editComponent.Instance, "endTime"), Is.EqualTo(kTimeEnd.AddDays(1)));
                Assert.That(GetMember<TimeObject>(editComponent.Instance, "selectedTimeObject")?.Id, Is.EqualTo(77));
                Assert.That(readOnlyComponent.Markup, Does.Contain("maintenance 2026-11-01 08:00 - 2026-11-30 18:00"));
                Assert.That(readOnlyComponent.Markup, Does.Contain("maintenance 2026-11-01 08:00 - 2026-12-01 18:00"));
            });
        }

        [Test]
        public async Task ObjectSelector_SearchesAndReportsTimeObjectsInTimeMode()
        {
            SearchApiConnection apiConnection = new() { Result = new List<TimeObject> { new() { Id = 77, Name = "maintenance", StartTime = kTimeStart, EndTime = kTimeEnd } } };
            await using BunitContext context = CreateContext(apiConnection);
            TimeObject? selected = null;
            IRenderedComponent<ObjectSelector> component = context.Render<ObjectSelector>(parameters => parameters
                .AddCascadingValue(Task.FromResult(new AuthenticationState(new ClaimsPrincipal())))
                .Add(p => p.ManagementId, kManagementId)
                .Add(p => p.Kind, ObjectTaskKind.Time)
                .Add(p => p.SelectedTimeObjectChanged, (TimeObject? timeObject) => selected = timeObject));
            MethodInfo search = typeof(ObjectSelector).GetMethod("Search", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMethodException(typeof(ObjectSelector).FullName, "Search");
            List<object?> arguments = ["maint"];
            await component.InvokeAsync(() => (Task)search.Invoke(component.Instance, arguments.ToArray())!);

            component.Find("tbody button").Click();
            // the click reaches the callback asynchronously, which can take longer on a slow machine
            component.WaitForAssertion(() => Assert.That(selected?.Id, Is.EqualTo(77)));

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.QueryCount, Is.EqualTo(1));
                Assert.That(component.FindComponent<FlowObjectTable<TimeObject>>().Instance.FilteredItems.Count(), Is.EqualTo(1));
                Assert.That(component.Markup, Does.Contain("search_time_object"));
                Assert.That(component.Markup, Does.Contain("2026-11-01 08:00 - 2026-11-30 18:00"));
                Assert.That(selected?.Id, Is.EqualTo(77));
            });
        }
    }
}
