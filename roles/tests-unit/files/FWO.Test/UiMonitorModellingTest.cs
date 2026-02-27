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

        private static MonitorModelling RenderComponent(Bunit.TestContext context, ApiConnection apiConnection)
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
            await using Bunit.TestContext context = new();
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
            await using Bunit.TestContext context = new();
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
        public async Task ExtractOrphanedServices_FiltersByInterfaceAndKeepsServices()
        {
            MonitorModellingTestApiConn apiConn = new();
            await using Bunit.TestContext context = new();
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
        public List<NwGroupRemoval> NwGroupRemovals { get; } = [];
        public List<ServiceGroupRemoval> ServiceGroupRemovals { get; } = [];

        public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null)
        {
            if (typeof(QueryResponseType) == typeof(List<FwoOwner>))
            {
                if (query == OwnerQueries.getOwners)
                {
                    return Task.FromResult((QueryResponseType)(object)new List<FwoOwner>());
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
