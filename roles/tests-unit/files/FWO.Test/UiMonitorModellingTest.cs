using Bunit;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Middleware.Client;
using FWO.Ui.Pages.Monitoring;
using FWO.Ui.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

namespace FWO.Test
{
    [TestFixture]
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    public class UiMonitorModellingTest
    {
        private static MethodInfo GetPrivateMethod(string name, params Type[] parameterTypes)
        {
            return typeof(MonitorModelling).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance, null, parameterTypes, null)
                ?? throw new MissingMethodException(typeof(MonitorModelling).FullName, name);
        }

        private static T GetPrivateField<T>(MonitorModelling component, string name)
        {
            FieldInfo? field = typeof(MonitorModelling).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
            {
                throw new MissingFieldException(typeof(MonitorModelling).FullName, name);
            }
            return (T)field.GetValue(component)!;
        }

        private static void SetPrivateField<T>(MonitorModelling component, string name, T value)
        {
            FieldInfo? field = typeof(MonitorModelling).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
            {
                throw new MissingFieldException(typeof(MonitorModelling).FullName, name);
            }
            field.SetValue(component, value);
        }

        private static Task InvokePrivateTask(MonitorModelling component, string name, params object[] args)
        {
            return (Task)GetPrivateMethod(name).Invoke(component, args)!;
        }

        private static MonitorModelling RenderComponent(BunitContext context, ApiConnection apiConnection)
        {
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddLocalization();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton<AuthenticationStateProvider>(new MonitoringTestAuthStateProvider(Roles.Admin));
            context.Services.AddSingleton(apiConnection);
            context.Services.AddSingleton(new MiddlewareClient("http://localhost/"));
            context.Services.AddSingleton<UserConfig>(new SimulatedUserConfig());
            context.Services.AddScoped<DomEventService>();

            IRenderedComponent<CascadingAuthenticationState> component = context.Render<CascadingAuthenticationState>(parameters =>
                parameters.AddChildContent<MonitorModelling>());
            return component.FindComponent<MonitorModelling>().Instance;
        }

        [Test]
        public async Task RemoveOrphanedAppRole_ReturnsCountAndCallsApi()
        {
            MonitorModellingTestApiConn apiConn = new();
            await using BunitContext context = new();
            MonitorModelling component = RenderComponent(context, apiConn);

            ModellingConnection connection = new()
            {
                Id = 21,
                SourceAppRoles =
                [
                    new ModellingAppRoleWrapper { Content = new ModellingAppRole { Id = 501 } }
                ]
            };

            Task<int> removeTask = (Task<int>)GetPrivateMethod("RemoveOrphanedAppRole", typeof(ModellingConnection), typeof(bool))
                .Invoke(component, new object[] { connection, false })!;
            int removed = await removeTask;

            Assert.That(removed, Is.EqualTo(1));
            Assert.That(apiConn.NwGroupRemovals, Has.Count.EqualTo(1));
            Assert.That(apiConn.NwGroupRemovals[0].NwGroupId, Is.EqualTo(501));
            Assert.That(apiConn.NwGroupRemovals[0].ConnectionId, Is.EqualTo(21));
            Assert.That(apiConn.NwGroupRemovals[0].Field, Is.EqualTo((int)ModellingTypes.ConnectionField.Source));
        }

        [Test]
        public async Task RemoveOrphanedServiceGroup_ReturnsCountAndCallsApi()
        {
            MonitorModellingTestApiConn apiConn = new();
            await using BunitContext context = new();
            MonitorModelling component = RenderComponent(context, apiConn);

            ModellingConnection connection = new()
            {
                Id = 33,
                ServiceGroups =
                [
                    new ModellingServiceGroupWrapper { Content = new ModellingServiceGroup { Id = 7 } },
                    new ModellingServiceGroupWrapper { Content = new ModellingServiceGroup { Id = 8 } }
                ]
            };

            Task<int> removeTask = (Task<int>)GetPrivateMethod("RemoveOrphanedServiceGroup", typeof(ModellingConnection), typeof(bool))
                .Invoke(component, new object[] { connection, false })!;
            int removed = await removeTask;

            Assert.That(removed, Is.EqualTo(2));
            Assert.That(apiConn.ServiceGroupRemovals, Has.Count.EqualTo(2));
            Assert.That(apiConn.ServiceGroupRemovals[0].ServiceGroupId, Is.EqualTo(7));
            Assert.That(apiConn.ServiceGroupRemovals[1].ServiceGroupId, Is.EqualTo(8));
        }

        [Test]
        public async Task LoadConnectionsForSelectedObjects_EmptyCollections_ClearSelections()
        {
            MonitorModellingTestApiConn apiConn = new();
            await using BunitContext context = new();
            MonitorModelling component = RenderComponent(context, apiConn);

            SetPrivateField(component, "AppRoles", new List<ModellingAppRole>());
            SetPrivateField(component, "AppServers", new List<ModellingAppServer>());
            SetPrivateField(component, "NetworkAreas", new List<ModellingNetworkArea>());
            SetPrivateField(component, "ServiceGroups", new List<ModellingServiceGroup>());
            SetPrivateField(component, "Services", new List<ModellingService>());

            await InvokePrivateTask(component, "LoadAppRoleConnections");
            await InvokePrivateTask(component, "LoadAppServerConnections");
            await InvokePrivateTask(component, "LoadNetworkAreaConnections");
            await InvokePrivateTask(component, "LoadServiceGroupConnections");
            await InvokePrivateTask(component, "LoadServiceConnections");

            Assert.That(GetPrivateField<ModellingAppRole?>(component, "SelectedAppRole"), Is.Null);
            Assert.That(GetPrivateField<List<ModellingConnection>>(component, "FoundConnections"), Is.Empty);
            Assert.That(GetPrivateField<ModellingAppServer?>(component, "SelectedAppServer"), Is.Null);
            Assert.That(GetPrivateField<List<ModellingConnection>>(component, "FoundAppServerConnections"), Is.Empty);
            Assert.That(GetPrivateField<ModellingNetworkArea?>(component, "SelectedNetworkArea"), Is.Null);
            Assert.That(GetPrivateField<List<ModellingConnection>>(component, "FoundNetworkAreaConnections"), Is.Empty);
            Assert.That(GetPrivateField<ModellingServiceGroup?>(component, "SelectedServiceGroup"), Is.Null);
            Assert.That(GetPrivateField<List<ModellingConnection>>(component, "FoundServiceGroupConnections"), Is.Empty);
            Assert.That(GetPrivateField<ModellingService?>(component, "SelectedService"), Is.Null);
            Assert.That(GetPrivateField<List<ModellingConnection>>(component, "FoundServiceConnections"), Is.Empty);
        }

        [Test]
        public async Task RemoveAllOrphans_WithOwners_QueriesAndRestoresOriginalOwner()
        {
            MonitorModellingTestApiConn apiConn = new();
            await using BunitContext context = new();
            MonitorModelling component = RenderComponent(context, apiConn);
            await InvokePrivateTask(component, "OnInitializedAsync");

            FwoOwner originalOwner = GetPrivateField<FwoOwner>(component, "SelectedOwner");
            List<FwoOwner> owners = new()
            {
                originalOwner,
                new FwoOwner { Id = 12, Name = "Owner 2" }
            };
            SetPrivateField(component, "Owners", owners);
            SetPrivateField(component, "RemoveAllOrphansMode", true);

            await InvokePrivateTask(component, "RemoveAllOrphans");

            Assert.That(GetPrivateField<bool>(component, "RemoveAllOrphansMode"), Is.False);
            Assert.That(GetPrivateField<FwoOwner>(component, "SelectedOwner").Id, Is.EqualTo(originalOwner.Id));
            Assert.That(apiConn.AppRoleQueryCount, Is.EqualTo(2));
            Assert.That(apiConn.AppServerQueryCount, Is.EqualTo(2));
            Assert.That(apiConn.AreaQueryCount, Is.EqualTo(2));
            Assert.That(apiConn.ServiceGroupQueryCount, Is.EqualTo(2));
            Assert.That(apiConn.ServiceQueryCount, Is.EqualTo(2));
            Assert.That(apiConn.RemoveNwGroupCalls, Is.Zero);
            Assert.That(apiConn.RemoveServiceGroupCalls, Is.Zero);
            Assert.That(apiConn.RemoveServiceCalls, Is.Zero);
        }

        [Test]
        public async Task OnInitializedAsync_LoadsOwnerDataAndInitialSelections()
        {
            MonitorModellingTestApiConn apiConn = new();
            await using BunitContext context = new();
            MonitorModelling component = RenderComponent(context, apiConn);

            Assert.That(apiConn.OwnerQueryCount, Is.EqualTo(1));
            Assert.That(apiConn.AppRoleQueryCount, Is.EqualTo(1));
            Assert.That(apiConn.AppServerQueryCount, Is.EqualTo(1));
            Assert.That(apiConn.AreaQueryCount, Is.EqualTo(1));
            Assert.That(apiConn.ServiceGroupQueryCount, Is.EqualTo(1));
            Assert.That(apiConn.ServiceQueryCount, Is.EqualTo(1));
            Assert.That(GetPrivateField<List<FwoOwner>>(component, "Owners"), Has.Count.EqualTo(1));
            Assert.That(GetPrivateField<FwoOwner?>(component, "SelectedOwner"), Is.Not.Null);
            Assert.That(GetPrivateField<List<ModellingAppRole>>(component, "AppRoles"), Has.Count.EqualTo(1));
            Assert.That(GetPrivateField<ModellingAppRole?>(component, "SelectedAppRole"), Is.Not.Null);
            Assert.That(GetPrivateField<List<ModellingAppServer>>(component, "AppServers"), Has.Count.EqualTo(1));
            Assert.That(GetPrivateField<ModellingAppServer?>(component, "SelectedAppServer"), Is.Not.Null);
            Assert.That(GetPrivateField<List<ModellingNetworkArea>>(component, "NetworkAreas"), Has.Count.EqualTo(1));
            Assert.That(GetPrivateField<ModellingNetworkArea?>(component, "SelectedNetworkArea"), Is.Not.Null);
            Assert.That(GetPrivateField<List<ModellingServiceGroup>>(component, "ServiceGroups"), Has.Count.EqualTo(1));
            Assert.That(GetPrivateField<ModellingServiceGroup?>(component, "SelectedServiceGroup"), Is.Not.Null);
            Assert.That(GetPrivateField<List<ModellingService>>(component, "Services"), Has.Count.EqualTo(1));
            Assert.That(GetPrivateField<ModellingService?>(component, "SelectedService"), Is.Not.Null);
        }

        [Test]
        public async Task OwnerChanged_SameOwner_DoesNotReloadData()
        {
            MonitorModellingTestApiConn apiConn = new();
            await using BunitContext context = new();
            MonitorModelling component = RenderComponent(context, apiConn);

            await InvokePrivateTask(component, "OnInitializedAsync");
            FwoOwner selectedOwner = GetPrivateField<FwoOwner>(component, "SelectedOwner");
            int ownerQueriesBefore = apiConn.OwnerQueryCount;
            int roleQueriesBefore = apiConn.AppRoleQueryCount;

            Task ownerChangedTask = (Task)GetPrivateMethod("OwnerChanged", typeof(FwoOwner)).Invoke(component, new object[] { selectedOwner })!;
            await ownerChangedTask;

            Assert.That(apiConn.OwnerQueryCount, Is.EqualTo(ownerQueriesBefore));
            Assert.That(apiConn.AppRoleQueryCount, Is.EqualTo(roleQueriesBefore));
        }

        [Test]
        public async Task RequestRemoveAllOrphans_SetsConfirmationMode()
        {
            MonitorModellingTestApiConn apiConn = new();
            await using BunitContext context = new();
            MonitorModelling component = RenderComponent(context, apiConn);

            GetPrivateMethod("RequestRemoveAllOrphans").Invoke(component, null);

            Assert.That(GetPrivateField<bool>(component, "RemoveAllOrphansMode"), Is.True);
        }

        [Test]
        public async Task RemoveAllOrphans_NoOwners_ReturnsWithoutQueries()
        {
            MonitorModellingTestApiConn apiConn = new();
            await using BunitContext context = new();
            MonitorModelling component = RenderComponent(context, apiConn);
            SetPrivateField(component, "Owners", new List<FwoOwner>());
            SetPrivateField(component, "RemoveAllOrphansMode", true);

            await InvokePrivateTask(component, "RemoveAllOrphans");

            Assert.That(apiConn.RemoveNwGroupCalls, Is.Zero);
            Assert.That(apiConn.RemoveServiceGroupCalls, Is.Zero);
            Assert.That(apiConn.RemoveServiceCalls, Is.Zero);
            Assert.That(GetPrivateField<bool>(component, "RemoveAllOrphansMode"), Is.False);
        }

        [Test]
        public async Task ExtractOrphanedServices_FiltersByInterfaceAndKeepsServices()
        {
            MonitorModellingTestApiConn apiConn = new();
            await using BunitContext context = new();
            MonitorModelling component = RenderComponent(context, apiConn);

            ModellingConnection ignored = new()
            {
                Id = 1,
                UsedInterfaceId = null,
                Services = [new ModellingServiceWrapper { Content = new ModellingService { Id = 100 } }]
            };
            ModellingConnection included = new()
            {
                Id = 2,
                UsedInterfaceId = 5,
                Services = [new ModellingServiceWrapper { Content = new ModellingService { Id = 200 } }],
                ServiceGroups = [new ModellingServiceGroupWrapper { Content = new ModellingServiceGroup { Id = 300 } }]
            };

            Task<List<ModellingConnection>> extractTask = (Task<List<ModellingConnection>>)GetPrivateMethod("ExtractOrphanedServices", typeof(List<ModellingConnection>))
                .Invoke(component, new object[] { new List<ModellingConnection> { ignored, included } })!;
            List<ModellingConnection> result = await extractTask;

            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result[0].Id, Is.EqualTo(2));
            Assert.That(result[0].Services, Has.Count.EqualTo(1));
            Assert.That(result[0].ServiceGroups, Is.Empty);
        }
    }

    internal sealed class MonitorModellingTestApiConn : SimulatedApiConnection
    {
        public List<NwGroupRemoval> NwGroupRemovals { get; } = new();
        public List<ServiceGroupRemoval> ServiceGroupRemovals { get; } = new();
        public int OwnerQueryCount { get; private set; }
        public int AppRoleQueryCount { get; private set; }
        public int AppServerQueryCount { get; private set; }
        public int AreaQueryCount { get; private set; }
        public int ServiceGroupQueryCount { get; private set; }
        public int ServiceQueryCount { get; private set; }
        public int RemoveNwGroupCalls => NwGroupRemovals.Count;
        public int RemoveServiceGroupCalls => ServiceGroupRemovals.Count;
        public int RemoveServiceCalls { get; private set; }
        private static readonly List<FwoOwner> kOwners = new() { new FwoOwner { Id = 11, Name = "Owner 1" } };
        private static readonly List<ModellingAppRole> kAppRoles = new() { new ModellingAppRole { Id = 21, Name = "AppRole 1" } };
        private static readonly List<ModellingAppServer> kAppServers = new() { new ModellingAppServer { Id = 31, Name = "AppServer 1", Ip = "192.0.2.31/32" } };
        private static readonly List<ModellingNetworkArea> kNetworkAreas = new() { new ModellingNetworkArea { Id = 41, Name = "Area 1" } };
        private static readonly List<ModellingServiceGroup> kServiceGroups = new() { new ModellingServiceGroup { Id = 51, Name = "ServiceGroup 1" } };
        private static readonly List<ModellingService> kServices = new() { new ModellingService { Id = 61, Name = "Service 1" } };

        public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, FWO.Api.Client.QueryChunkingOptions? chunkingOptions = null)
        {
            if (typeof(QueryResponseType) == typeof(List<FwoOwner>))
            {
                if (query == OwnerQueries.getOwners)
                {
                    OwnerQueryCount++;
                    return Task.FromResult((QueryResponseType)(object)new List<FwoOwner>(kOwners));
                }
            }

            if (typeof(QueryResponseType) == typeof(List<ModellingAppRole>))
            {
                if (query == ModellingQueries.getAppRoles)
                {
                    AppRoleQueryCount++;
                    return Task.FromResult((QueryResponseType)(object)new List<ModellingAppRole>(kAppRoles));
                }
            }

            if (typeof(QueryResponseType) == typeof(List<ModellingAppServer>))
            {
                if (query == ModellingQueries.getAppServersForOwner)
                {
                    AppServerQueryCount++;
                    return Task.FromResult((QueryResponseType)(object)new List<ModellingAppServer>(kAppServers));
                }
            }

            if (typeof(QueryResponseType) == typeof(List<ModellingNetworkArea>))
            {
                if (query == ModellingQueries.getAreas)
                {
                    AreaQueryCount++;
                    return Task.FromResult((QueryResponseType)(object)new List<ModellingNetworkArea>(kNetworkAreas));
                }
            }

            if (typeof(QueryResponseType) == typeof(List<ModellingServiceGroup>))
            {
                if (query == ModellingQueries.getServiceGroupsForApp)
                {
                    ServiceGroupQueryCount++;
                    return Task.FromResult((QueryResponseType)(object)new List<ModellingServiceGroup>(kServiceGroups));
                }
            }

            if (typeof(QueryResponseType) == typeof(List<ModellingService>))
            {
                if (query == ModellingQueries.getServicesForApp)
                {
                    ServiceQueryCount++;
                    return Task.FromResult((QueryResponseType)(object)new List<ModellingService>(kServices));
                }
            }

            if (typeof(QueryResponseType) == typeof(List<ModellingConnectionWrapper>))
            {
                if (query == ModellingQueries.getConnectionsForNwGroup)
                {
                    return Task.FromResult((QueryResponseType)(object)new List<ModellingConnectionWrapper>());
                }
            }

            string queryTypeName = typeof(QueryResponseType).FullName ?? "";
            if (queryTypeName.Contains("ConnectionIdWrapper", StringComparison.Ordinal))
            {
                if (query == ModellingQueries.getConnectionIdsForAppServer
                    || query == ModellingQueries.getConnectionIdsForServiceGroup
                    || query == ModellingQueries.getConnectionIdsForService)
                {
                    Type wrapperType = typeof(MonitorModelling).GetNestedType("ConnectionIdWrapper", BindingFlags.NonPublic)
                        ?? throw new MissingMemberException(typeof(MonitorModelling).FullName, "ConnectionIdWrapper");
                    object result = Activator.CreateInstance(typeof(List<>).MakeGenericType(wrapperType))
                        ?? throw new InvalidOperationException("Could not create connection id list.");
                    return Task.FromResult((QueryResponseType)result);
                }
            }

            if (typeof(QueryResponseType) == typeof(List<ModellingConnection>))
            {
                if (query == ModellingQueries.getConnections || query == ModellingQueries.getConnectionsResolved)
                {
                    return Task.FromResult((QueryResponseType)(object)new List<ModellingConnection>());
                }
            }

            if (typeof(QueryResponseType) == typeof(ReturnId))
            {
                if (query == ModellingQueries.removeNwGroupFromConnection)
                {
                    long nwGroupId = GetLong(variables, "nwGroupId");
                    long connectionId = GetLong(variables, "connectionId");
                    int field = GetInt(variables, "connectionField");
                    NwGroupRemovals.Add(new NwGroupRemoval(nwGroupId, connectionId, field));
                    return Task.FromResult((QueryResponseType)(object)new ReturnId());
                }

                if (query == ModellingQueries.removeServiceGroupFromConnection)
                {
                    long serviceGroupId = GetLong(variables, "serviceGroupId");
                    long connectionId = GetLong(variables, "connectionId");
                    ServiceGroupRemovals.Add(new ServiceGroupRemoval(serviceGroupId, connectionId));
                    return Task.FromResult((QueryResponseType)(object)new ReturnId());
                }

                if (query == ModellingQueries.removeServiceFromConnection)
                {
                    RemoveServiceCalls++;
                    return Task.FromResult((QueryResponseType)(object)new ReturnId());
                }
            }

            throw new NotImplementedException();
        }

        private static long GetLong(object? variables, string propertyName)
        {
            object? value = variables?.GetType().GetProperty(propertyName)?.GetValue(variables);
            return value != null ? Convert.ToInt64(value) : 0;
        }

        private static int GetInt(object? variables, string propertyName)
        {
            object? value = variables?.GetType().GetProperty(propertyName)?.GetValue(variables);
            return value != null ? Convert.ToInt32(value) : 0;
        }
    }

    internal record NwGroupRemoval(long NwGroupId, long ConnectionId, int Field);
    internal record ServiceGroupRemoval(long ServiceGroupId, long ConnectionId);
}
