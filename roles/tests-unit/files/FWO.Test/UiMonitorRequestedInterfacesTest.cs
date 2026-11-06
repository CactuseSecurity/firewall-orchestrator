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
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Collections;
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

        private static T GetObjectProperty<T>(object instance, string propertyName)
        {
            PropertyInfo property = instance.GetType().GetProperty(propertyName)
                ?? throw new MissingMemberException(instance.GetType().FullName, propertyName);
            return (T)(property.GetValue(instance) ?? throw new InvalidOperationException($"Property {propertyName} is null."));
        }

        private static MonitorRequestedInterfaces RenderComponent(BunitContext context, ApiConnection apiConnection)
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
            await using BunitContext context = new();
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
            await using BunitContext context = new();
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
            await using BunitContext context = new();
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

            GetPrivateMethod("UpdateTicketStateFilter", typeof(string)).Invoke(component, ["10"]);
            List<long> filteredTicketIds = GetPrivatePropertyValue<List<long>>(component, "OpenRemovedTicketIds");

            Assert.That(filteredTicketIds, Is.EqualTo(new List<long> { 1001 }));
        }

        [Test]
        public async Task OpenRemovedTicketIds_ExcludesInterfacesUsedByConnections()
        {
            MonitorRequestedInterfacesTestApiConn apiConn = new();
            await using BunitContext context = new();
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
            GetPrivateMethod("RefreshDisplayedRequestedInterfaceRows").Invoke(component, null);

            List<long> ticketIds = GetPrivatePropertyValue<List<long>>(component, "OpenRemovedTicketIds");

            Assert.That(ticketIds, Is.EqualTo(new List<long> { 1001 }));
        }

        [Test]
        public async Task OpenOrphanedTicketsPopup_SetsPopupState()
        {
            MonitorRequestedInterfacesTestApiConn apiConn = new();
            await using BunitContext context = new();
            MonitorRequestedInterfaces component = RenderComponent(context, apiConn);

            await (Task)GetPrivateMethod("OpenOrphanedTicketsPopup").Invoke(component, null)!;

            Assert.That(GetPrivateField<bool>(component, "ShowOrphanedTicketsPopup"), Is.True);
        }

        [Test]
        public async Task BuildRequestedInterfaceRows_AllowsRejectOnlyForOpenUnusedTickets()
        {
            MonitorRequestedInterfacesTestApiConn apiConn = new();
            await using BunitContext context = new();
            MonitorRequestedInterfaces component = RenderComponent(context, apiConn);

            List<ModellingConnection> connections =
            [
                new() { Id = 1, Name = "open-unused", Removed = true, TicketId = 1001 },
                new() { Id = 2, Name = "open-used", Removed = true, TicketId = 1002 },
                new() { Id = 3, Name = "closed-unused", Removed = true, TicketId = 1003 },
                new() { Id = 4, Name = "missing-ticket", Removed = true }
            ];
            SetPrivateField(component, "RequestedInterfaces", connections);
            SetPrivateField(component, "TicketStateIdByConnectionId", new Dictionary<int, int>
            {
                { 1, 10 },
                { 2, 10 },
                { 3, 150 }
            });
            SetPrivateField(component, "UsedByConnectionIdsByInterfaceId", new Dictionary<int, string>
            {
                { 1, "-" },
                { 2, "77" },
                { 3, "-" },
                { 4, "-" }
            });
            SetPrivateField(component, "NewInterfaceStateMatrix", new StateMatrix { LowestEndState = 100 });

            GetPrivateMethod("BuildRequestedInterfaceRows").Invoke(component, null);

            Assert.That(GetObjectProperty<bool>(GetRequestedInterfaceRow(component, 1), "CanReject"), Is.True);
            Assert.That(GetObjectProperty<bool>(GetRequestedInterfaceRow(component, 2), "CanReject"), Is.False);
            Assert.That(GetObjectProperty<bool>(GetRequestedInterfaceRow(component, 3), "CanReject"), Is.False);
            Assert.That(GetObjectProperty<bool>(GetRequestedInterfaceRow(component, 4), "CanReject"), Is.False);
        }

        [Test]
        public async Task GetFilteredRequestedInterfaces_ShowsAllRowsUntilDeactivatedOwnerFilterIsSelected()
        {
            MonitorRequestedInterfacesTestApiConn apiConn = new();
            await using Bunit.TestContext context = new();
            MonitorRequestedInterfaces component = RenderComponent(context, apiConn);

            List<ModellingConnection> connections =
            [
                new() { Id = 1, Name = "active-if", ProposedAppId = 20, ProposedApp = new FwoOwner { Id = 20, Name = "Active Requested", Active = true, OwnerLifeCycleStateId = 1 }, TicketId = 1001 },
                new() { Id = 2, Name = "import-inactive-if", ProposedAppId = 20, ProposedApp = new FwoOwner { Id = 20, Name = "Active Requested", Active = true, OwnerLifeCycleStateId = 1 }, TicketId = 1002 },
                new() { Id = 3, Name = "lifecycle-inactive-if", ProposedAppId = 30, ProposedApp = new FwoOwner { Id = 30, Name = "Lifecycle Inactive Requested", Active = true, OwnerLifeCycleStateId = 2 }, TicketId = 1003 },
                new() { Id = 4, Name = "closed-import-inactive-if", ProposedAppId = 20, ProposedApp = new FwoOwner { Id = 20, Name = "Active Requested", Active = true, OwnerLifeCycleStateId = 1 }, TicketId = 1004 }
            ];
            SetPrivateField(component, "OwnersById", new Dictionary<int, FwoOwner>
            {
                { 10, new FwoOwner { Id = 10, Name = "Active Requester", Active = true, OwnerLifeCycleStateId = 1 } },
                { 11, new FwoOwner { Id = 11, Name = "Import Inactive Requester", Active = false, OwnerLifeCycleStateId = 1 } },
                { 20, new FwoOwner { Id = 20, Name = "Active Requested", Active = true, OwnerLifeCycleStateId = 1 } },
                { 30, new FwoOwner { Id = 30, Name = "Lifecycle Inactive Requested", Active = true, OwnerLifeCycleStateId = 2 } }
            });
            SetPrivateField(component, "OwnerLifeCycleActiveById", new Dictionary<int, bool>
            {
                { 1, true },
                { 2, false }
            });
            SetPrivateField(component, "RequestingAppByConnectionId", new Dictionary<int, FwoOwner>
            {
                { 1, new FwoOwner { Id = 10, Name = "Active Requester", Active = true, OwnerLifeCycleStateId = 1 } },
                { 2, new FwoOwner { Id = 11, Name = "Import Inactive Requester", Active = false, OwnerLifeCycleStateId = 1 } },
                { 3, new FwoOwner { Id = 10, Name = "Active Requester", Active = true, OwnerLifeCycleStateId = 1 } },
                { 4, new FwoOwner { Id = 11, Name = "Import Inactive Requester", Active = false, OwnerLifeCycleStateId = 1 } }
            });
            PrepareRows(component, connections, new Dictionary<int, int>
            {
                { 1, 10 },
                { 2, 10 },
                { 3, 10 },
                { 4, 150 }
            },
            lowestEndState: 100);

            Assert.That(GetFilteredRequestedInterfaceIds(component), Is.EqualTo(new List<int> { 1, 2, 3, 4 }));

            SetPrivateField(component, "ShowImportDeactivatedRequestedInterfaces", true);
            Assert.That(GetFilteredRequestedInterfaceIds(component), Is.EqualTo(new List<int> { 2, 4 }));

            SetPrivateField(component, "ShowImportDeactivatedRequestedInterfaces", false);
            SetPrivateField(component, "ShowLifecycleDeactivatedRequestedInterfaces", true);
            Assert.That(GetFilteredRequestedInterfaceIds(component), Is.EqualTo(new List<int> { 3 }));

            SetPrivateField(component, "ShowImportDeactivatedRequestedInterfaces", true);
            SetPrivateField(component, "SelectedTicketStateFilter", "all_open");
            Assert.That(GetFilteredRequestedInterfaceIds(component), Is.EqualTo(new List<int> { 2, 3 }));
        }

        [Test]
        public async Task DeactivatedOwnerCheckboxHandlers_RefreshDisplayedRows()
        {
            MonitorRequestedInterfacesTestApiConn apiConn = new();
            await using Bunit.TestContext context = new();
            MonitorRequestedInterfaces component = RenderComponent(context, apiConn);

            SetPrivateField(component, "OwnerLifeCycleActiveById", new Dictionary<int, bool> { { 1, true }, { 2, false } });
            SetPrivateField(component, "RequestingAppByConnectionId", new Dictionary<int, FwoOwner>
            {
                { 1, new FwoOwner { Id = 10, Name = "Active Requester", Active = true, OwnerLifeCycleStateId = 1 } },
                { 2, new FwoOwner { Id = 11, Name = "Inactive Requester", Active = false, OwnerLifeCycleStateId = 1 } },
                { 3, new FwoOwner { Id = 12, Name = "Lifecycle Requester", Active = true, OwnerLifeCycleStateId = 2 } }
            });
            PrepareRows(component, new List<ModellingConnection>
            {
                new() { Id = 1, Name = "active-if", TicketId = 1001, ProposedApp = new FwoOwner { Id = 20, Name = "Active Requested", Active = true, OwnerLifeCycleStateId = 1 } },
                new() { Id = 2, Name = "import-inactive-if", TicketId = 1002, ProposedApp = new FwoOwner { Id = 20, Name = "Active Requested", Active = true, OwnerLifeCycleStateId = 1 } },
                new() { Id = 3, Name = "lifecycle-inactive-if", TicketId = 1003, ProposedApp = new FwoOwner { Id = 20, Name = "Active Requested", Active = true, OwnerLifeCycleStateId = 1 } }
            },
            new Dictionary<int, int> { { 1, 10 }, { 2, 10 }, { 3, 10 } },
            lowestEndState: 100);

            Assert.That(GetDisplayedRequestedInterfaceIds(component), Is.EqualTo(new List<int> { 1, 2, 3 }));

            GetPrivateMethod("UpdateShowImportDeactivatedRequestedInterfaces", typeof(ChangeEventArgs))
                .Invoke(component, [new ChangeEventArgs { Value = true }]);
            Assert.That(GetDisplayedRequestedInterfaceIds(component), Is.EqualTo(new List<int> { 2 }));

            GetPrivateMethod("UpdateShowLifecycleDeactivatedRequestedInterfaces", typeof(ChangeEventArgs))
                .Invoke(component, [new ChangeEventArgs { Value = "true" }]);
            Assert.That(GetDisplayedRequestedInterfaceIds(component), Is.EqualTo(new List<int> { 2, 3 }));

            GetPrivateMethod("UpdateShowImportDeactivatedRequestedInterfaces", typeof(ChangeEventArgs))
                .Invoke(component, [new ChangeEventArgs { Value = false }]);
            Assert.That(GetDisplayedRequestedInterfaceIds(component), Is.EqualTo(new List<int> { 3 }));
        }

        [Test]
        public async Task BuildRequestedInterfaceRows_UsesProposedAppRelationshipAndOwnerFallback()
        {
            MonitorRequestedInterfacesTestApiConn apiConn = new();
            await using Bunit.TestContext context = new();
            MonitorRequestedInterfaces component = RenderComponent(context, apiConn);

            SetPrivateField(component, "OwnersById", new Dictionary<int, FwoOwner>
            {
                { 30, new FwoOwner { Id = 30, Name = "Fallback Requested", ExtAppId = "APP-30", Active = false, OwnerLifeCycleStateId = 1 } }
            });
            SetPrivateField(component, "OwnerLifeCycleActiveById", new Dictionary<int, bool> { { 1, true }, { 2, false } });
            PrepareRows(component, new List<ModellingConnection>
            {
                new()
                {
                    Id = 1,
                    Name = "relationship-if",
                    TicketId = 1001,
                    ProposedAppId = 20,
                    ProposedApp = new FwoOwner { Id = 20, Name = "Lifecycle Requested", ExtAppId = "APP-20", Active = true, OwnerLifeCycleStateId = 2 }
                },
                new() { Id = 2, Name = "fallback-if", TicketId = 1002, ProposedAppId = 30 }
            },
            new Dictionary<int, int> { { 1, 10 }, { 2, 10 } },
            lowestEndState: 100);

            object relationshipRow = GetRequestedInterfaceRow(component, 1);
            Assert.That(GetObjectProperty<string>(relationshipRow, "RequestedApp"), Is.EqualTo("Lifecycle Requested"));
            Assert.That(GetObjectProperty<string>(relationshipRow, "RequestedExtAppId"), Is.EqualTo("APP-20"));
            Assert.That(GetObjectProperty<string>(relationshipRow, "RequestedOwnerState"), Is.EqualTo("Inactive lifecycle state"));
            Assert.That(GetObjectProperty<bool>(relationshipRow, "HasLifecycleDeactivatedOwner"), Is.True);

            object fallbackRow = GetRequestedInterfaceRow(component, 2);
            Assert.That(GetObjectProperty<string>(fallbackRow, "RequestedApp"), Is.EqualTo("Fallback Requested"));
            Assert.That(GetObjectProperty<string>(fallbackRow, "RequestedExtAppId"), Is.EqualTo("APP-30"));
            Assert.That(GetObjectProperty<string>(fallbackRow, "RequestedOwnerState"), Is.EqualTo("Import deactivated"));
            Assert.That(GetObjectProperty<bool>(fallbackRow, "HasImportDeactivatedOwner"), Is.True);
        }

        [Test]
        public async Task RebuildTicketStateFilterOptions_SortsStatesAndResetsInvalidSelection()
        {
            MonitorRequestedInterfacesTestApiConn apiConn = new();
            await using Bunit.TestContext context = new();
            MonitorRequestedInterfaces component = RenderComponent(context, apiConn);

            PrepareRows(component, new List<ModellingConnection>
            {
                new() { Id = 1, Name = "if-a", TicketId = 1001 },
                new() { Id = 2, Name = "if-b", TicketId = 1002 },
                new() { Id = 3, Name = "if-c", TicketId = 1003 }
            },
            new Dictionary<int, int> { { 1, 20 }, { 2, 10 } },
            lowestEndState: 100);
            SetPrivateField(component, "SelectedTicketStateFilter", "obsolete");

            GetPrivateMethod("RebuildTicketStateFilterOptions").Invoke(component, null);

            Assert.That(GetPrivateField<List<string>>(component, "TicketStateFilterOptions"),
                Is.EqualTo(new List<string> { "all", "all_open", "10", "20" }));
            Assert.That(GetPrivateField<string>(component, "SelectedTicketStateFilter"), Is.EqualTo("all"));
        }

        [Test]
        public async Task ResolveRequestingAppsFromTickets_MapsTaskDataAndKeepsFirstTaskForConnection()
        {
            DateTime creationDate = new(2026, 5, 1, 12, 30, 0);
            MonitorRequestedInterfacesTestApiConn apiConn = new()
            {
                TicketOverviews =
                [
                    new WfTicket
                    {
                        Id = 501,
                        StateId = 42,
                        CreationDate = creationDate,
                        Tasks =
                        {
                            CreateTask(1, WfTaskType.new_interface, "if-a", 7),
                            CreateTask(1, WfTaskType.new_interface, "if-a-duplicate", 8),
                            CreateTask(2, WfTaskType.new_interface, "if-b", 9)
                        }
                    }
                ]
            };
            await using Bunit.TestContext context = new();
            MonitorRequestedInterfaces component = RenderComponent(context, apiConn);

            SetPrivateField(component, "RequestedInterfaces", new List<ModellingConnection>
            {
                new() { Id = 1, Name = "if-a", TicketId = 501 },
                new() { Id = 2, Name = "if-b", TicketId = 501 }
            });
            SetPrivateField(component, "OwnersById", new Dictionary<int, FwoOwner>
            {
                { 7, new FwoOwner { Id = 7, Name = "Requester 7" } },
                { 8, new FwoOwner { Id = 8, Name = "Requester 8" } },
                { 9, new FwoOwner { Id = 9, Name = "Requester 9" } }
            });

            await (Task)GetPrivateMethod("ResolveRequestingAppsFromTickets").Invoke(component, null)!;

            Dictionary<int, FwoOwner> requestingOwners = GetPrivateField<Dictionary<int, FwoOwner>>(component, "RequestingAppByConnectionId");
            Dictionary<int, int> ticketStates = GetPrivateField<Dictionary<int, int>>(component, "TicketStateIdByConnectionId");
            Dictionary<int, DateTime> ticketCreationDates = GetPrivateField<Dictionary<int, DateTime>>(component, "TicketCreationDateByConnectionId");

            Assert.That(requestingOwners[1].Id, Is.EqualTo(7));
            Assert.That(requestingOwners[2].Id, Is.EqualTo(9));
            Assert.That(ticketStates[1], Is.EqualTo(42));
            Assert.That(ticketStates[2], Is.EqualTo(42));
            Assert.That(ticketCreationDates[1], Is.EqualTo(creationDate));
            Assert.That(ticketCreationDates[2], Is.EqualTo(creationDate));
            Assert.That(apiConn.RequestedTicketIds, Is.EqualTo(new List<long> { 501 }));
        }

        [Test]
        public async Task ResolveRequestingAppsFromTickets_LoadsAllTicketsWithOneChunkedOverviewQuery()
        {
            MonitorRequestedInterfacesTestApiConn apiConn = new()
            {
                TicketOverviews =
                [
                    new WfTicket { Id = 502, StateId = 20, Tasks = { CreateTask(2, WfTaskType.new_interface, "if-b", 8) } },
                    new WfTicket { Id = 501, StateId = 10, Tasks = { CreateTask(1, WfTaskType.new_interface, "if-a", 7) } }
                ]
            };
            await using BunitContext context = new();
            MonitorRequestedInterfaces component = RenderComponent(context, apiConn);
            apiConn.Queries.Clear();

            SetPrivateField(component, "RequestedInterfaces", new List<ModellingConnection>
            {
                new() { Id = 1, Name = "if-a", TicketId = 501 },
                new() { Id = 2, Name = "if-b", TicketId = 502 },
                new() { Id = 3, Name = "if-c", TicketId = 501 },
                new() { Id = 4, Name = "if-without-ticket" }
            });
            SetPrivateField(component, "OwnersById", new Dictionary<int, FwoOwner>
            {
                { 7, new FwoOwner { Id = 7, Name = "Requester 7" } },
                { 8, new FwoOwner { Id = 8, Name = "Requester 8" } }
            });

            await (Task)GetPrivateMethod("ResolveRequestingAppsFromTickets").Invoke(component, null)!;

            Dictionary<int, FwoOwner> requestingOwners = GetPrivateField<Dictionary<int, FwoOwner>>(component, "RequestingAppByConnectionId");
            Dictionary<int, int> ticketStates = GetPrivateField<Dictionary<int, int>>(component, "TicketStateIdByConnectionId");
            Assert.That(apiConn.Queries, Is.EqualTo(new List<string> { RequestQueries.getTicketOverviewsByIds }));
            Assert.That(apiConn.RequestedTicketIds, Is.EqualTo(new List<long> { 501, 502 }));
            Assert.That(apiConn.TicketChunkingOptions, Is.Not.Null);
            Assert.That(apiConn.TicketChunkingOptions!.Enabled, Is.True);
            Assert.That(apiConn.TicketChunkingOptions.ChunkVariableName, Is.EqualTo("ids"));
            Assert.That(apiConn.TicketChunkingOptions.MergeMode, Is.EqualTo(ChunkMergeMode.TopLevelArrayConcat));
            Assert.That(requestingOwners[1].Id, Is.EqualTo(7));
            Assert.That(requestingOwners[2].Id, Is.EqualTo(8));
            Assert.That(ticketStates[1], Is.EqualTo(10));
            Assert.That(ticketStates[2], Is.EqualTo(20));
        }

        [Test]
        public async Task ResolveRequestingAppsFromTickets_WithoutTickets_DoesNotQuery()
        {
            MonitorRequestedInterfacesTestApiConn apiConn = new();
            await using BunitContext context = new();
            MonitorRequestedInterfaces component = RenderComponent(context, apiConn);
            apiConn.Queries.Clear();

            SetPrivateField(component, "RequestedInterfaces", new List<ModellingConnection>
            {
                new() { Id = 1, Name = "if-without-ticket" }
            });

            await (Task)GetPrivateMethod("ResolveRequestingAppsFromTickets").Invoke(component, null)!;

            Assert.That(apiConn.Queries, Is.Empty);
            Assert.That(GetPrivateField<Dictionary<int, int>>(component, "TicketStateIdByConnectionId"), Is.Empty);
        }

        [Test]
        public async Task ResolveUsedByConnections_StoresDistinctSortedConnectionIdsWithoutQueries()
        {
            MonitorRequestedInterfacesTestApiConn apiConn = new();
            await using Bunit.TestContext context = new();
            MonitorRequestedInterfaces component = RenderComponent(context, apiConn);
            apiConn.Queries.Clear();

            SetPrivateField(component, "RequestedInterfaces", new List<ModellingConnection>
            {
                new ModellingRequestedInterface
                {
                    Id = 5,
                    Name = "used-if",
                    UsedByConnections = [new() { Id = 30 }, new() { Id = 10 }, new() { Id = 30 }]
                },
                new ModellingRequestedInterface { Id = 6, Name = "unused-if" },
                new() { Id = 7, Name = "plain-connection" }
            });

            GetPrivateMethod("ResolveUsedByConnections").Invoke(component, null);

            Dictionary<int, string> usedByConnectionIds = GetPrivateField<Dictionary<int, string>>(component, "UsedByConnectionIdsByInterfaceId");
            Assert.That(usedByConnectionIds[5], Is.EqualTo("10, 30"));
            Assert.That(usedByConnectionIds[6], Is.EqualTo("-"));
            Assert.That(usedByConnectionIds[7], Is.EqualTo("-"));
            Assert.That(apiConn.Queries, Is.Empty);
        }

        [Test]
        public async Task FetchRequestedInterfaces_UsesListQueriesOnlyAndBuildsUsedByColumn()
        {
            MonitorRequestedInterfacesTestApiConn apiConn = new()
            {
                RequestedInterfacesResult =
                [
                    new ModellingRequestedInterface { Id = 1, Name = "if-a", TicketId = 501, UsedByConnections = [new() { Id = 12 }] },
                    new ModellingRequestedInterface { Id = 2, Name = "if-b", TicketId = 502 }
                ],
                TicketOverviews =
                [
                    new WfTicket { Id = 501, StateId = 10, Tasks = { CreateTask(1, WfTaskType.new_interface, "if-a") } },
                    new WfTicket { Id = 502, StateId = 10, Tasks = { CreateTask(2, WfTaskType.new_interface, "if-b") } }
                ]
            };
            await using BunitContext context = new();
            MonitorRequestedInterfaces component = RenderComponent(context, apiConn);
            apiConn.Queries.Clear();

            await (Task)GetPrivateMethod("FetchRequestedInterfaces").Invoke(component, null)!;

            Assert.That(apiConn.Queries.Count(query => query == RequestQueries.getTicketOverviewsByIds), Is.EqualTo(1));
            Assert.That(apiConn.Queries, Has.No.Member(ModellingQueries.getInterfaceUsers));
            Assert.That(apiConn.Queries, Has.No.Member(RequestQueries.getTicketById));
            Assert.That(apiConn.Queries, Has.No.Member(ModellingQueries.getConnectionById));
            object usedRow = GetRequestedInterfaceRow(component, 1);
            object unusedRow = GetRequestedInterfaceRow(component, 2);
            Assert.That(GetObjectProperty<string>(usedRow, "UsedInConnectionIds"), Is.EqualTo("12"));
            Assert.That(GetObjectProperty<bool>(usedRow, "CanReject"), Is.False);
            Assert.That(GetObjectProperty<string>(unusedRow, "UsedInConnectionIds"), Is.EqualTo("-"));
            Assert.That(GetObjectProperty<bool>(unusedRow, "CanReject"), Is.True);
        }

        [Test]
        public async Task LoadFullConnection_ReturnsDetailsAndKeepsOwnersOfListEntry()
        {
            FwoOwner app = new() { Id = 3, Name = "App 3", Active = false };
            FwoOwner proposedApp = new() { Id = 4, Name = "App 4" };
            MonitorRequestedInterfacesTestApiConn apiConn = new()
            {
                ConnectionById =
                {
                    [1] = [new ModellingConnection { Id = 1, Name = "if-a", Reason = "full details", App = new() { Id = 3, Name = "App 3" } }]
                }
            };
            await using BunitContext context = new();
            MonitorRequestedInterfaces component = RenderComponent(context, apiConn);
            ModellingConnection listEntry = new ModellingRequestedInterface { Id = 1, Name = "if-a", App = app, ProposedApp = proposedApp };
            object[] arguments = new object[1];
            arguments[0] = listEntry;

            ModellingConnection fullConnection = await (Task<ModellingConnection>)GetPrivateMethod("LoadFullConnection", typeof(ModellingConnection))
                .Invoke(component, arguments)!;

            Assert.That(fullConnection, Is.Not.SameAs(listEntry));
            Assert.That(fullConnection.Reason, Is.EqualTo("full details"));
            Assert.That(fullConnection.App, Is.SameAs(app));
            Assert.That(fullConnection.ProposedApp, Is.SameAs(proposedApp));
        }

        [Test]
        public async Task LoadFullConnection_FallsBackToListEntryWhenConnectionIsMissing()
        {
            MonitorRequestedInterfacesTestApiConn apiConn = new();
            await using BunitContext context = new();
            MonitorRequestedInterfaces component = RenderComponent(context, apiConn);
            ModellingConnection listEntry = new ModellingRequestedInterface { Id = 9, Name = "if-missing" };
            object[] arguments = new object[1];
            arguments[0] = listEntry;

            ModellingConnection fullConnection = await (Task<ModellingConnection>)GetPrivateMethod("LoadFullConnection", typeof(ModellingConnection))
                .Invoke(component, arguments)!;

            Assert.That(fullConnection, Is.SameAs(listEntry));
        }

        [Test]
        public async Task CloseRejectRemovedTicketsConfirm_ResetsPopupStateAndReason()
        {
            MonitorRequestedInterfacesTestApiConn apiConn = new();
            await using BunitContext context = new();
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
            MonitorRequestedInterfacesTestApiConn apiConn = new();
            await using BunitContext context = new();
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

        [Test]
        public async Task OrphanedRequestedInterfaceTicketsPopup_LoadsTicketsWithInterfaceProblems()
        {
            MonitorRequestedInterfacesTestApiConn apiConn = new()
            {
                TicketsByParametersResult =
                [
                    new WfTicket
                    {
                        Id = 501,
                        StateId = 10,
                        CreationDate = new DateTime(2026, 3, 28, 10, 15, 0),
                        Tasks =
                        {
                            CreateTask(1, WfTaskType.new_interface, "if-removed-a"),
                            CreateTask(2, WfTaskType.new_interface, "if-removed-b"),
                            CreateTask(0, WfTaskType.new_interface, "if-missing-conn")
                        }
                    },
                    new WfTicket
                    {
                        Id = 777,
                        StateId = 10,
                        Tasks =
                        {
                            CreateTask(3, WfTaskType.new_interface, "if-still-exists"),
                            CreateTask(5, WfTaskType.new_interface, "if-wrong-kind")
                        }
                    },
                    new WfTicket
                    {
                        Id = 888,
                        StateId = 10,
                        Tasks =
                        {
                            CreateTask(4, WfTaskType.new_interface, "if-still-exists-2")
                        }
                    }
                ],
                ConnectionById =
                {
                    [1] = [],
                    [2] = [],
                    [3] = [new ModellingConnection { Id = 3, Name = "existing-if", TicketId = 777, IsInterface = true, IsRequested = true }],
                    [4] = [new ModellingConnection { Id = 4, Name = "existing-if-2", TicketId = 888, IsInterface = true, IsRequested = true }],
                    [5] = [new ModellingConnection { Id = 5, Name = "normal-connection", TicketId = 777, IsInterface = false, IsRequested = false }]
                },
                States =
                [
                    new WfState { Id = 10, Name = "In Progress" },
                ]
            };
            await using BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddLocalization();
            context.Services.AddSingleton<ApiConnection>(apiConn);
            context.Services.AddSingleton(new MiddlewareClient("http://localhost/"));
            context.Services.AddSingleton<UserConfig>(new SimulatedUserConfig());

            IRenderedComponent<OrphanedRequestedInterfaceTicketsPopup> component =
                context.Render<OrphanedRequestedInterfaceTicketsPopup>(parameters => parameters
                    .Add(p => p.Display, true));
            component.WaitForAssertion(() =>
            {
                string markup = component.Markup;
                List<string> cellTexts = component.FindAll("tbody td")
                    .Select(cell => cell.TextContent.Trim())
                    .Where(text => !string.IsNullOrWhiteSpace(text))
                    .ToList();
                Assert.That(markup, Does.Contain("501"));
                Assert.That(markup, Does.Contain("In Progress"));
                Assert.That(markup, Does.Contain("if-removed-a"));
                Assert.That(markup, Does.Contain("if-removed-b"));
                Assert.That(markup, Does.Contain("if-missing-conn"));
                Assert.That(markup, Does.Contain("Ticket has multiple request tasks"));
                Assert.That(markup, Does.Contain("Missing connection ID"));
                Assert.That(markup, Does.Contain("Requested interface not found"));
                Assert.That(markup, Does.Contain("777"));
                Assert.That(markup, Does.Contain("if-wrong-kind"));
                Assert.That(markup, Does.Contain("Linked connection is not a requested interface"));
                Assert.That(markup, Does.Contain("Close Tickets as rejected"));
                Assert.That(cellTexts, Does.Not.Contain("888"));
            });
            component.Find("button.btn-danger.me-2").Click();
            component.WaitForAssertion(() =>
            {
                Assert.That(component.Markup, Does.Contain("Are you sure you want to close 2 ticket(s) as rejected?"));
            });
        }

        [Test]
        public async Task OrphanedRequestedInterfaceTicketsPopup_LoadsAlreadyPublishedInterfacesWithDoneAction()
        {
            MonitorRequestedInterfacesTestApiConn apiConn = new()
            {
                TicketsByParametersResult =
                [
                    new WfTicket
                    {
                        Id = 889,
                        StateId = 10,
                        Tasks =
                        {
                            CreateTask(6, WfTaskType.new_interface, "if-published")
                        }
                    }
                ],
                ConnectionById =
                {
                    [6] =
                    [
                        new ModellingConnection
                        {
                            Id = 6,
                            Name = "published-if",
                            TicketId = 889,
                            IsInterface = true,
                            IsRequested = false,
                            IsPublished = true,
                            ProposedAppId = null,
                            Removed = false
                        }
                    ]
                },
                States =
                [
                    new WfState { Id = 10, Name = "In Progress" },
                ]
            };
            await using BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddLocalization();
            context.Services.AddSingleton<ApiConnection>(apiConn);
            context.Services.AddSingleton(new MiddlewareClient("http://localhost/"));
            context.Services.AddSingleton<UserConfig>(new SimulatedUserConfig());

            IRenderedComponent<OrphanedRequestedInterfaceTicketsPopup> component =
                context.Render<OrphanedRequestedInterfaceTicketsPopup>(parameters => parameters
                    .Add(p => p.Display, true));

            component.WaitForAssertion(() =>
            {
                string markup = component.Markup;
                Assert.That(markup, Does.Contain("889"));
                Assert.That(markup, Does.Contain("if-published"));
                Assert.That(markup, Does.Contain("Requested interface is already published"));
                Assert.That(markup, Does.Contain("Close as done"));
                Assert.That(markup, Does.Contain("Close Tickets as done"));
                Assert.That(markup, Does.Not.Contain("Close Tickets as rejected"));
                Assert.That(markup, Does.Not.Contain("Linked connection is not a requested interface"));
            });
            component.Find("button.btn-success.me-2").Click();
            component.WaitForAssertion(() =>
            {
                Assert.That(component.Markup, Does.Contain("Are you sure you want to close 1 ticket(s) as done?"));
            });
        }

        [Test]
        public async Task OrphanedRequestedInterfaceTicketsPopup_LoadsDuplicateLookingTasksWithoutBulkActions()
        {
            MonitorRequestedInterfacesTestApiConn apiConn = new()
            {
                TicketsByParametersResult =
                [
                    new WfTicket
                    {
                        Id = 901,
                        StateId = 10,
                        Tasks =
                        {
                            CreateTask(11, WfTaskType.new_interface, "same interface", reqOwnerId: 7)
                        }
                    },
                    new WfTicket
                    {
                        Id = 902,
                        StateId = 10,
                        Tasks =
                        {
                            CreateTask(12, WfTaskType.new_interface, " same   interface ", reqOwnerId: 7)
                        }
                    }
                ],
                ConnectionById =
                {
                    [11] = [new ModellingConnection { Id = 11, TicketId = 901, IsInterface = true, IsRequested = true }],
                    [12] = [new ModellingConnection { Id = 12, TicketId = 902, IsInterface = true, IsRequested = true }]
                },
                States =
                [
                    new WfState { Id = 10, Name = "In Progress" },
                ]
            };
            await using BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddLocalization();
            context.Services.AddSingleton<ApiConnection>(apiConn);
            context.Services.AddSingleton(new MiddlewareClient("http://localhost/"));
            context.Services.AddSingleton<UserConfig>(new SimulatedUserConfig());

            IRenderedComponent<OrphanedRequestedInterfaceTicketsPopup> component =
                context.Render<OrphanedRequestedInterfaceTicketsPopup>(parameters => parameters
                    .Add(p => p.Display, true));

            component.WaitForAssertion(() =>
            {
                string markup = component.Markup;
                Assert.That(markup, Does.Contain("901"));
                Assert.That(markup, Does.Contain("902"));
                Assert.That(markup, Does.Contain("Duplicate-looking open interface request"));
                Assert.That(markup, Does.Not.Contain("Close Tickets as rejected"));
                Assert.That(markup, Does.Not.Contain("Close Tickets as done"));
            });
        }

        [Test]
        public async Task OrphanedRequestedInterfaceTicketsPopup_LoadsMultipleTasksWithoutBulkActions()
        {
            MonitorRequestedInterfacesTestApiConn apiConn = new()
            {
                TicketsByParametersResult =
                [
                    new WfTicket
                    {
                        Id = 904,
                        StateId = 10,
                        Tasks =
                        {
                            CreateTask(14, WfTaskType.new_interface, "if-a"),
                            CreateTask(15, WfTaskType.new_interface, "if-b")
                        }
                    }
                ],
                ConnectionById =
                {
                    [14] = [new ModellingConnection { Id = 14, TicketId = 904, IsInterface = true, IsRequested = true }],
                    [15] = [new ModellingConnection { Id = 15, TicketId = 904, IsInterface = true, IsRequested = true }]
                },
                States =
                [
                    new WfState { Id = 10, Name = "In Progress" },
                ]
            };
            await using BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddLocalization();
            context.Services.AddSingleton<ApiConnection>(apiConn);
            context.Services.AddSingleton(new MiddlewareClient("http://localhost/"));
            context.Services.AddSingleton<UserConfig>(new SimulatedUserConfig());

            IRenderedComponent<OrphanedRequestedInterfaceTicketsPopup> component =
                context.Render<OrphanedRequestedInterfaceTicketsPopup>(parameters => parameters
                    .Add(p => p.Display, true));

            component.WaitForAssertion(() =>
            {
                string markup = component.Markup;
                Assert.That(markup, Does.Contain("904"));
                Assert.That(markup, Does.Contain("if-a"));
                Assert.That(markup, Does.Contain("if-b"));
                Assert.That(markup, Does.Contain("Ticket has multiple request tasks"));
                Assert.That(markup, Does.Not.Contain("Close Tickets as rejected"));
                Assert.That(markup, Does.Not.Contain("Close Tickets as done"));
            });
        }

        [Test]
        public async Task OrphanedRequestedInterfaceTicketsPopup_DoesNotCloseAsDoneWhenPublishedCriteriaAreIncomplete()
        {
            MonitorRequestedInterfacesTestApiConn apiConn = new()
            {
                TicketsByParametersResult =
                [
                    new WfTicket
                    {
                        Id = 903,
                        StateId = 10,
                        Tasks =
                        {
                            CreateTask(13, WfTaskType.new_interface, "published-but-still-proposed")
                        }
                    }
                ],
                ConnectionById =
                {
                    [13] =
                    [
                        new ModellingConnection
                        {
                            Id = 13,
                            TicketId = 903,
                            IsInterface = true,
                            IsRequested = false,
                            IsPublished = true,
                            ProposedAppId = 20,
                            Removed = false
                        }
                    ]
                },
                States =
                [
                    new WfState { Id = 10, Name = "In Progress" },
                ]
            };
            await using BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddLocalization();
            context.Services.AddSingleton<ApiConnection>(apiConn);
            context.Services.AddSingleton(new MiddlewareClient("http://localhost/"));
            context.Services.AddSingleton<UserConfig>(new SimulatedUserConfig());

            IRenderedComponent<OrphanedRequestedInterfaceTicketsPopup> component =
                context.Render<OrphanedRequestedInterfaceTicketsPopup>(parameters => parameters
                    .Add(p => p.Display, true));

            component.WaitForAssertion(() =>
            {
                string markup = component.Markup;
                Assert.That(markup, Does.Contain("903"));
                Assert.That(markup, Does.Contain("Linked connection is not a requested interface"));
                Assert.That(markup, Does.Contain("Close Tickets as rejected"));
                Assert.That(markup, Does.Not.Contain("Requested interface is already published"));
                Assert.That(markup, Does.Not.Contain("Close Tickets as done"));
            });
        }

        private static WfReqTask CreateTask(int connId, WfTaskType taskType, string title = "", int reqOwnerId = 0)
        {
            WfReqTask task = new() { TaskType = taskType.ToString(), Title = title };
            task.SetAddInfo(AdditionalInfoKeys.ConnId, connId.ToString());
            if (reqOwnerId > 0)
            {
                task.SetAddInfo(AdditionalInfoKeys.ReqOwner, reqOwnerId.ToString());
            }
            return task;
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
            GetPrivateMethod("RefreshDisplayedRequestedInterfaceRows").Invoke(component, null);
        }

        private static List<int> GetFilteredRequestedInterfaceIds(MonitorRequestedInterfaces component)
        {
            IEnumerable rows = (IEnumerable)GetPrivateMethod("GetFilteredRequestedInterfaces").Invoke(component, null)!;
            return rows.Cast<object>()
                .Select(row => (int)(row.GetType().GetProperty("Id")?.GetValue(row) ?? 0))
                .ToList();
        }

        private static List<int> GetDisplayedRequestedInterfaceIds(MonitorRequestedInterfaces component)
        {
            IEnumerable rows = GetPrivateField<IEnumerable>(component, "DisplayedRequestedInterfaceRows");
            return rows.Cast<object>()
                .Select(row => GetObjectProperty<int>(row, "Id"))
                .ToList();
        }

        private static object GetRequestedInterfaceRow(MonitorRequestedInterfaces component, int id)
        {
            IEnumerable rows = GetPrivateField<IEnumerable>(component, "RequestedInterfaceRows");
            return rows.Cast<object>().First(row => GetObjectProperty<int>(row, "Id") == id);
        }
    }

    internal sealed class MonitorRequestedInterfacesTestApiConn : SimulatedApiConnection
    {
        private static readonly string stateMatrixJson = BuildStateMatrixJson();
        public List<WfTicket> TicketOverviews { get; set; } = [];
        public List<long> RequestedTicketIds { get; private set; } = [];
        public FWO.Api.Client.QueryChunkingOptions? TicketChunkingOptions { get; private set; }
        public List<string> Queries { get; } = [];
        public Dictionary<int, List<ModellingConnection>> ConnectionById { get; } = [];
        public List<ModellingRequestedInterface> RequestedInterfacesResult { get; set; } = [];
        public List<WfTicket> TicketsByParametersResult { get; set; } = [];
        public List<WfState> States { get; set; } = [];
        public List<OwnerLifeCycleState> OwnerLifeCycleStates { get; set; } = [];

        public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, FWO.Api.Client.QueryChunkingOptions? chunkingOptions = null)
        {
            Queries.Add(query);
            if (typeof(QueryResponseType) == typeof(List<WorkflowConfiguration>) && query == RequestQueries.getActiveStateMatrixConfiguration)
            {
                List<WorkflowConfiguration> config = StateMatrixConfigurationTestHelper.FromLegacyJson(stateMatrixJson, WfTaskType.new_interface);
                return Task.FromResult((QueryResponseType)(object)config);
            }

            if (typeof(QueryResponseType) == typeof(List<ModellingRequestedInterface>) && query == ModellingQueries.getRequestedInterfaces)
            {
                return Task.FromResult((QueryResponseType)(object)RequestedInterfacesResult);
            }

            if (typeof(QueryResponseType) == typeof(List<ModellingConnection>) && query == ModellingQueries.getConnectionById)
            {
                int connId = Convert.ToInt32(variables?.GetType().GetProperty("id")?.GetValue(variables) ?? 0);
                if (ConnectionById.TryGetValue(connId, out List<ModellingConnection>? connections))
                {
                    return Task.FromResult((QueryResponseType)(object)connections);
                }

                return Task.FromResult((QueryResponseType)(object)new List<ModellingConnection>());
            }

            if (typeof(QueryResponseType) == typeof(List<FwoOwner>) && query == OwnerQueries.getOwners)
            {
                return Task.FromResult((QueryResponseType)(object)new List<FwoOwner>());
            }

            if (typeof(QueryResponseType) == typeof(List<OwnerLifeCycleState>) && query == OwnerQueries.getOwnerLifeCycleStates)
            {
                return Task.FromResult((QueryResponseType)(object)OwnerLifeCycleStates);
            }

            if (typeof(QueryResponseType) == typeof(List<WfState>) && query == RequestQueries.getStates)
            {
                return Task.FromResult((QueryResponseType)(object)States);
            }

            if (typeof(QueryResponseType) == typeof(List<WfTicket>) && query == RequestQueries.getTicketsByParameters)
            {
                return Task.FromResult((QueryResponseType)(object)TicketsByParametersResult);
            }

            if (typeof(QueryResponseType) == typeof(List<WfTicket>) && query == RequestQueries.getTicketOverviewsByIds)
            {
                RequestedTicketIds = [.. (IEnumerable<long>)(variables?.GetType().GetProperty("ids")?.GetValue(variables) ?? new List<long>())];
                TicketChunkingOptions = chunkingOptions;
                List<WfTicket> tickets = [.. TicketOverviews.Where(ticket => RequestedTicketIds.Contains(ticket.Id))];
                return Task.FromResult((QueryResponseType)(object)tickets);
            }

            throw new NotImplementedException();
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
