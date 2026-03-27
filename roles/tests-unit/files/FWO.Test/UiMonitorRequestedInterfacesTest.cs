using Bunit;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Config.Api.Data;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Data.Workflow;
using FWO.Middleware.Client;
using FWO.Services.Workflow;
using FWO.Ui.Pages.Monitoring;
using FWO.Ui.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace FWO.Test
{
    [TestFixture]
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    public class UiMonitorRequestedInterfacesTest
    {
        private static MethodInfo GetPrivateMethod(string name, params Type[] parameterTypes)
        {
            return typeof(MonitorRequestedInterfaces).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance, null, parameterTypes, null)
                ?? throw new MissingMethodException(typeof(MonitorRequestedInterfaces).FullName, name);
        }

        private static PropertyInfo GetPrivateProperty(string name)
        {
            return typeof(MonitorRequestedInterfaces).GetProperty(name, BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMemberException(typeof(MonitorRequestedInterfaces).FullName, name);
        }

        private static void SetPrivateField<T>(MonitorRequestedInterfaces component, string fieldName, T value)
        {
            FieldInfo? field = typeof(MonitorRequestedInterfaces).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(component, value);
                return;
            }

            PropertyInfo? property = typeof(MonitorRequestedInterfaces).GetProperty(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (property == null)
            {
                throw new MissingFieldException(typeof(MonitorRequestedInterfaces).FullName, fieldName);
            }
            property.SetValue(component, value);
        }

        private static T GetPrivateField<T>(MonitorRequestedInterfaces component, string fieldName)
        {
            FieldInfo? field = typeof(MonitorRequestedInterfaces).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
            {
                return (T)(field.GetValue(component) ?? throw new InvalidOperationException($"Field {fieldName} is null."));
            }

            PropertyInfo? property = typeof(MonitorRequestedInterfaces).GetProperty(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (property == null)
            {
                throw new MissingFieldException(typeof(MonitorRequestedInterfaces).FullName, fieldName);
            }
            return (T)(property.GetValue(component) ?? throw new InvalidOperationException($"Property {fieldName} is null."));
        }

        private static T GetPrivatePropertyValue<T>(MonitorRequestedInterfaces component, string propertyName)
        {
            PropertyInfo property = GetPrivateProperty(propertyName);
            return (T)(property.GetValue(component) ?? throw new InvalidOperationException($"Property {propertyName} is null."));
        }

        private static MonitorRequestedInterfaces RenderComponent(Bunit.TestContext context, ApiConnection apiConnection)
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
                parameters.AddChildContent<MonitorRequestedInterfaces>());
            return component.FindComponent<MonitorRequestedInterfaces>().Instance;
        }

        [Test]
        public async Task ShowRejectRemovedTicketsConfirm_SetsPopupStateAndDefaultReason()
        {
            MonitorRequestedInterfacesTestApiConn apiConn = new();
            await using Bunit.TestContext context = new();
            MonitorRequestedInterfaces component = RenderComponent(context, apiConn);

            PrepareRows(component, new List<ModellingConnection>
            {
                new() { Id = 1, Name = "if-1", Removed = true, TicketId = 501 },
                new() { Id = 2, Name = "if-2", Removed = true, TicketId = 777 },
                new() { Id = 3, Name = "if-3", Removed = false, TicketId = 888 }
            },
            new Dictionary<int, int>
            {
                { 1, 10 },
                { 2, 20 },
                { 3, 10 }
            },
            lowestEndState: 100);

            GetPrivateMethod("ShowRejectRemovedTicketsConfirm").Invoke(component, null);

            bool confirmOpen = GetPrivateField<bool>(component, "ConfirmRejectRemovedTickets");
            string reason = GetPrivateField<string>(component, "RejectRemovedTicketsReason");
            string message = GetPrivatePropertyValue<string>(component, "RejectRemovedTicketsMessage");

            Assert.That(confirmOpen, Is.True);
            Assert.That(reason, Is.EqualTo("Rejected by Admin"));
            Assert.That(message, Does.Contain("2"));
        }

        [Test]
        public async Task OpenRemovedTicketIds_ContainsOnlyOpenRemovedDistinctTickets()
        {
            MonitorRequestedInterfacesTestApiConn apiConn = new();
            await using Bunit.TestContext context = new();
            MonitorRequestedInterfaces component = RenderComponent(context, apiConn);

            PrepareRows(component, new List<ModellingConnection>
            {
                new() { Id = 1, Name = "if-a", Removed = true, TicketId = 1001 },
                new() { Id = 2, Name = "if-b", Removed = true, TicketId = 1001 },
                new() { Id = 3, Name = "if-c", Removed = true, TicketId = 2002 },
                new() { Id = 4, Name = "if-d", Removed = false, TicketId = 3003 }
            },
            new Dictionary<int, int>
            {
                { 1, 10 },
                { 2, 10 },
                { 3, 150 },
                { 4, 10 }
            },
            lowestEndState: 100);

            List<long> ticketIds = GetPrivatePropertyValue<List<long>>(component, "OpenRemovedTicketIds");

            Assert.That(ticketIds, Is.EqualTo(new List<long> { 1001 }));
        }

        [Test]
        public async Task OpenRemovedTicketIds_RespectsSelectedTicketStateFilter()
        {
            MonitorRequestedInterfacesTestApiConn apiConn = new();
            await using Bunit.TestContext context = new();
            MonitorRequestedInterfaces component = RenderComponent(context, apiConn);

            PrepareRows(component, new List<ModellingConnection>
            {
                new() { Id = 1, Name = "if-a", Removed = true, TicketId = 1001 },
                new() { Id = 2, Name = "if-b", Removed = true, TicketId = 2002 }
            },
            new Dictionary<int, int>
            {
                { 1, 10 },
                { 2, 20 }
            },
            lowestEndState: 100);

            SetPrivateField(component, "SelectedTicketStateFilter", "10");
            List<long> filteredTicketIds = GetPrivatePropertyValue<List<long>>(component, "OpenRemovedTicketIds");

            Assert.That(filteredTicketIds, Is.EqualTo(new List<long> { 1001 }));
        }

        [Test]
        public async Task OpenRemovedTicketIds_ExcludesInterfacesUsedByConnections()
        {
            MonitorRequestedInterfacesTestApiConn apiConn = new();
            await using Bunit.TestContext context = new();
            MonitorRequestedInterfaces component = RenderComponent(context, apiConn);

            List<ModellingConnection> connections =
            [
                new() { Id = 1, Name = "if-free", Removed = true, TicketId = 1001 },
                new() { Id = 2, Name = "if-used", Removed = true, TicketId = 2002 }
            ];
            SetPrivateField(component, "RequestedInterfaces", connections);
            SetPrivateField(component, "TicketStateIdByConnectionId", new Dictionary<int, int>
            {
                { 1, 10 },
                { 2, 10 }
            });
            SetPrivateField(component, "UsedByConnectionIdsByInterfaceId", new Dictionary<int, string>
            {
                { 1, "-" },
                { 2, "77, 88" }
            });
            SetPrivateField(component, "NewInterfaceStateMatrix", new StateMatrix { LowestEndState = 100 });
            GetPrivateMethod("BuildRequestedInterfaceRows").Invoke(component, null);

            List<long> ticketIds = GetPrivatePropertyValue<List<long>>(component, "OpenRemovedTicketIds");

            Assert.That(ticketIds, Is.EqualTo(new List<long> { 1001 }));
        }

        [Test]
        public async Task CloseRejectRemovedTicketsConfirm_ResetsPopupStateAndReason()
        {
            MonitorRequestedInterfacesTestApiConn apiConn = new();
            await using Bunit.TestContext context = new();
            MonitorRequestedInterfaces component = RenderComponent(context, apiConn);

            SetPrivateField(component, "ConfirmRejectRemovedTickets", true);
            SetPrivateField(component, "RejectRemovedTicketsReason", "custom reason");

            GetPrivateMethod("CloseRejectRemovedTicketsConfirm").Invoke(component, null);

            Assert.That(GetPrivateField<bool>(component, "ConfirmRejectRemovedTickets"), Is.False);
            Assert.That(GetPrivateField<string>(component, "RejectRemovedTicketsReason"), Is.EqualTo(string.Empty));
        }

        [Test]
        public async Task ResolveRequestingAppsFromTickets_IgnoresMissingTickets()
        {
            MonitorRequestedInterfacesTestApiConn apiConn = new()
            {
                SafeTicketById =
                {
                    [501] = new ApiResponse<WfTicket>("missing ticket")
                }
            };
            await using Bunit.TestContext context = new();
            MonitorRequestedInterfaces component = RenderComponent(context, apiConn);

            SetPrivateField(component, "RequestedInterfaces", new List<ModellingConnection>
            {
                new() { Id = 1, Name = "if-1", TicketId = 501 }
            });
            SetPrivateField(component, "OwnersById", new Dictionary<int, FwoOwner>
            {
                { 7, new FwoOwner { Id = 7, Name = "Owner 7" } }
            });

            Task resolveTask = (Task)GetPrivateMethod("ResolveRequestingAppsFromTickets").Invoke(component, null)!;
            await resolveTask;

            Assert.That(GetPrivateField<Dictionary<int, FwoOwner>>(component, "RequestingAppByConnectionId"), Is.Empty);
            Assert.That(GetPrivateField<Dictionary<int, int>>(component, "TicketStateIdByConnectionId"), Is.Empty);
            Assert.That(GetPrivateField<Dictionary<int, DateTime>>(component, "TicketCreationDateByConnectionId"), Is.Empty);
        }

        private static void PrepareRows(
            MonitorRequestedInterfaces component,
            List<ModellingConnection> connections,
            Dictionary<int, int> ticketStateByConnectionId,
            int lowestEndState)
        {
            SetPrivateField(component, "RequestedInterfaces", connections);
            SetPrivateField(component, "TicketStateIdByConnectionId", ticketStateByConnectionId);
            SetPrivateField(component, "UsedByConnectionIdsByInterfaceId", connections.ToDictionary(c => c.Id, _ => "-"));
            SetPrivateField(component, "NewInterfaceStateMatrix", new StateMatrix { LowestEndState = lowestEndState });
            GetPrivateMethod("BuildRequestedInterfaceRows").Invoke(component, null);
        }
    }

    internal sealed class MonitorRequestedInterfacesTestApiConn : SimulatedApiConnection
    {
        private static readonly string stateMatrixJson = BuildStateMatrixJson();
        public Dictionary<long, ApiResponse<WfTicket>> SafeTicketById { get; } = [];

        public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null)
        {
            if (typeof(QueryResponseType) == typeof(List<GlobalStateMatrixHelper>) && query == ConfigQueries.getConfigItemByKey)
            {
                List<GlobalStateMatrixHelper> config =
                [
                    new() { ConfData = stateMatrixJson }
                ];
                return Task.FromResult((QueryResponseType)(object)config);
            }

            if (typeof(QueryResponseType) == typeof(List<ModellingConnection>) && query == ModellingQueries.getRequestedInterfaces)
            {
                return Task.FromResult((QueryResponseType)(object)new List<ModellingConnection>());
            }

            if (typeof(QueryResponseType) == typeof(List<FwoOwner>) && query == OwnerQueries.getOwners)
            {
                return Task.FromResult((QueryResponseType)(object)new List<FwoOwner>());
            }

            if (typeof(QueryResponseType) == typeof(List<WfState>) && query == RequestQueries.getStates)
            {
                return Task.FromResult((QueryResponseType)(object)new List<WfState>());
            }

            if (typeof(QueryResponseType) == typeof(List<ModellingConnection>) && query == ModellingQueries.getInterfaceUsers)
            {
                return Task.FromResult((QueryResponseType)(object)new List<ModellingConnection>());
            }

            throw new NotImplementedException();
        }

        public override Task<ApiResponse<QueryResponseType>> SendQuerySafeAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null)
        {
            if (typeof(QueryResponseType) == typeof(WfTicket) && query == RequestQueries.getTicketById)
            {
                long ticketId = Convert.ToInt64(variables?.GetType().GetProperty("id")?.GetValue(variables) ?? 0L);
                if (SafeTicketById.TryGetValue(ticketId, out ApiResponse<WfTicket>? response))
                {
                    return Task.FromResult((ApiResponse<QueryResponseType>)(object)response);
                }

                return Task.FromResult((ApiResponse<QueryResponseType>)(object)new ApiResponse<WfTicket>("ticket not configured"));
            }

            return base.SendQuerySafeAsync<QueryResponseType>(query, variables, operationName);
        }

        private static string BuildStateMatrixJson()
        {
            Dictionary<WorkflowPhases, StateMatrix> matrixByPhase = [];
            foreach (WorkflowPhases phase in Enum.GetValues(typeof(WorkflowPhases)))
            {
                matrixByPhase[phase] = new StateMatrix
                {
                    Matrix = new Dictionary<int, List<int>> { { 0, new List<int> { 0 } } },
                    DerivedStates = new Dictionary<int, int>(),
                    LowestInputState = 1,
                    LowestStartedState = 2,
                    LowestEndState = 100,
                    Active = true
                };
            }

            GlobalStateMatrix matrix = new()
            {
                GlobalMatrix = matrixByPhase
            };
            return JsonSerializer.Serialize(matrix);
        }
    }
}
