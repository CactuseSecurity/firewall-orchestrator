using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Services;
using FWO.Ui.Services;
using FWO.Ui.Shared;
using Bunit;
using NUnit.Framework;
using System.Collections.Generic;
using System.Reflection;

namespace FWO.Test
{
    [TestFixture]
    public class ModellingAppHandlerTest
    {
        private static readonly string[] PermissionDeniedActionTitles = ["add_connection", "add_interface", "add_common_service", "delete_connection"];

        private static ModellingAppHandler CreateHandler(List<ModellingConnection> connections, UserConfig? userConfig = null,
            bool isOwner = true, Action<Exception?, string, string, bool>? displayMessageInUi = null, ApiConnection? apiConnection = null)
        {
            UserConfig config = userConfig ?? new SimulatedUserConfig();
            if (string.IsNullOrWhiteSpace(config.ModNamingConvention))
            {
                config.ModNamingConvention = "{}";
            }
            if (string.IsNullOrWhiteSpace(config.ModAppServerTypes))
            {
                config.ModAppServerTypes = "[]";
            }
            ModellingAppHandler handler = new(
                apiConnection ?? new SimulatedApiConnection(),
                config,
                new FwoOwner { Id = 1 },
                displayMessageInUi ?? DefaultInit.DoNothing,
                isOwner);
            handler.Connections = connections;
            SetPrivateField(handler, "dummyAppRoleId", 0L);
            return handler;
        }

        private static void SetPrivateField<TValue>(ModellingAppHandler handler, string fieldName, TValue value)
        {
            FieldInfo? field = typeof(ModellingAppHandler).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null);
            field!.SetValue(handler, value);
        }

        private static void SetComponentParameter<TValue>(object component, string parameterName, TValue value)
        {
            PropertyInfo? parameter = component.GetType().GetProperty(parameterName, BindingFlags.Public | BindingFlags.Instance);
            Assert.That(parameter, Is.Not.Null);
            parameter!.SetValue(component, value);
        }

        private static MethodInfo GetPrivateMethod(string name, params Type[] parameterTypes)
        {
            MethodInfo? method = typeof(ModellingAppHandler).GetMethod(
                name,
                BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                parameterTypes,
                null);
            Assert.That(method, Is.Not.Null);
            return method!;
        }

        [Test]
        public void GetInterfaces_ExcludesRejectedAndDecommissioned_WhenNotRequested()
        {
            ModellingConnection visible = new()
            {
                Id = 1,
                IsInterface = true,
                Props = new Dictionary<string, string>()
            };
            ModellingConnection rejected = new()
            {
                Id = 2,
                IsInterface = true,
                Props = new Dictionary<string, string>
                {
                    { ConState.Rejected.ToString(), "true" }
                }
            };
            ModellingConnection decommissioned = new()
            {
                Id = 3,
                IsInterface = true,
                Props = new Dictionary<string, string>
                {
                    { ConState.Decommissioned.ToString(), "true" }
                }
            };

            ModellingAppHandler handler = CreateHandler([visible, rejected, decommissioned]);

            List<ModellingConnection> interfaces = handler.GetInterfaces();

            Assert.That(interfaces, Is.EqualTo([visible]));
        }

        [Test]
        public void GetInterfaces_IncludesRejectedAndDecommissioned_WhenRequested()
        {
            ModellingConnection visible = new() { Id = 1, IsInterface = true };
            ModellingConnection rejected = new()
            {
                Id = 2,
                IsInterface = true,
                Props = new Dictionary<string, string> { { ConState.Rejected.ToString(), "true" } }
            };
            ModellingConnection decommissioned = new()
            {
                Id = 3,
                IsInterface = true,
                Props = new Dictionary<string, string> { { ConState.Decommissioned.ToString(), "true" } }
            };

            ModellingAppHandler handler = CreateHandler([visible, rejected, decommissioned]);

            List<ModellingConnection> interfaces = handler.GetInterfaces(true);

            Assert.That(interfaces, Is.EquivalentTo([visible, rejected, decommissioned]));
        }

        [Test]
        public void GetCommonServices_ReturnsOnlyCommonServices()
        {
            ModellingConnection common = new() { Id = 1, IsCommonService = true };
            ModellingConnection regular = new() { Id = 2 };

            ModellingAppHandler handler = CreateHandler([common, regular]);

            List<ModellingConnection> result = handler.GetCommonServices();

            Assert.That(result, Is.EqualTo([common]));
        }

        [Test]
        public void GetRegularConnections_ExcludesInterfacesAndCommonServices()
        {
            ModellingConnection regular = new() { Id = 1 };
            ModellingConnection common = new() { Id = 2, IsCommonService = true };
            ModellingConnection iface = new() { Id = 3, IsInterface = true };

            ModellingAppHandler handler = CreateHandler([regular, common, iface]);

            List<ModellingConnection> result = handler.GetRegularConnections();

            Assert.That(result, Is.EqualTo([regular]));
        }

        [Test]
        public void GetConnectionsToRequest_OrdersCommonServicesFirst()
        {
            ModellingConnection regular = new() { Id = 1 };
            ModellingConnection common = new() { Id = 2, IsCommonService = true };
            ModellingConnection iface = new() { Id = 3, IsInterface = true };

            ModellingAppHandler handler = CreateHandler([regular, common, iface]);

            List<ModellingConnection> result = handler.GetConnectionsToRequest();

            Assert.That(result, Is.EqualTo([common, regular]));
        }

        [Test]
        public void GetConnectionsToRequest_InNotificationModeKeepsAlreadyRequestedIncludedStateCandidates()
        {
            SimulatedUserConfig userConfig = new()
            {
                ModIntegrationMode = ModIntegrationMode.WorkflowNotifications,
                ModIntegrationStateMarker = "ImplementationState",
                ModIntegrationStates = ModIntegrationStateConfig.ToConfigValue([new() { Name = "Retry", IncludeIntoRequest = true }])
            };
            ModellingConnection requestedRetry = new() { Id = 1, RequestedOnFw = true };
            requestedRetry.AddProperty("ImplementationState", "Retry");
            ModellingConnection requestedNoMarker = new() { Id = 2, RequestedOnFw = true };
            ModellingConnection iface = new() { Id = 3, IsInterface = true };
            ModellingAppHandler handler = CreateHandler([requestedRetry, requestedNoMarker, iface], userConfig);

            List<ModellingConnection> result = handler.GetConnectionsToRequest();

            Assert.That(result, Is.EqualTo([requestedRetry, requestedNoMarker]));
        }

        [Test]
        public void HasModellingIssues_ReturnsTrue_ForInterface()
        {
            ModellingConnection iface = new() { Id = 1, IsInterface = true };

            ModellingAppHandler handler = CreateHandler([iface]);

            Assert.That(handler.HasModellingIssues(iface), Is.True);
        }

        [Test]
        public void HasModellingIssues_ReturnsFalse_ForRegularConnection()
        {
            ModellingConnection regular = new() { Id = 1 };

            ModellingAppHandler handler = CreateHandler([regular]);

            Assert.That(handler.HasModellingIssues(regular), Is.False);
        }

        [Test]
        public async Task PrepareConnections_SyncsInterfaceState()
        {
            SimulatedUserConfig userConfig = new()
            {
                VarianceAnalysisSync = false,
                ModRolloutRemovedAppServers = false
            };
            ModellingConnection conn = new()
            {
                Id = 1,
                IsInterface = true,
                IsRequested = true
            };
            ModellingAppHandler handler = CreateHandler([conn], userConfig);

            MethodInfo prepareConnections = GetPrivateMethod("PrepareConnections", typeof(List<ModellingConnection>));
            Task prepareTask = (Task)prepareConnections.Invoke(handler, new object[] { handler.Connections })!;
            await prepareTask;

            Assert.That(conn.GetBoolProperty(ConState.Requested.ToString()), Is.True);
        }

        [Test]
        public async Task InitActiveTab_SetsInterfaceTab()
        {
            ModellingConnection iface = new() { Id = 1, IsInterface = true };
            ModellingAppHandler handler = CreateHandler([iface]);

            using BunitContext context = new();
            IRenderedComponent<TabSet> renderedTabSet = context.Render<TabSet>();
            TabSet tabSet = renderedTabSet.Instance;
            Tab tab0 = new();
            Tab tab1 = new();
            Tab tab2 = new();
            SetComponentParameter(tab0, nameof(Tab.Position), 0);
            SetComponentParameter(tab1, nameof(Tab.Position), 1);
            SetComponentParameter(tab2, nameof(Tab.Position), 2);
            tabSet.Tabs.AddRange([tab0, tab1, tab2]);
            handler.Tabset = tabSet;

            await renderedTabSet.InvokeAsync(() => handler.InitActiveTab(iface));

            Assert.That(handler.Tabset.ActiveTab, Is.EqualTo(tab1));
        }

        [Test]
        public async Task InitActiveTab_SetsCommonServiceTab_WhenNoRegularConnections()
        {
            ModellingConnection common = new() { Id = 2, IsCommonService = true };
            ModellingAppHandler handler = CreateHandler([common]);
            handler.Application.CommSvcPossible = true;

            using BunitContext context = new();
            IRenderedComponent<TabSet> renderedTabSet = context.Render<TabSet>();
            TabSet tabSet = renderedTabSet.Instance;
            Tab tab0 = new();
            Tab tab1 = new();
            Tab tab2 = new();
            SetComponentParameter(tab0, nameof(Tab.Position), 0);
            SetComponentParameter(tab1, nameof(Tab.Position), 1);
            SetComponentParameter(tab2, nameof(Tab.Position), 2);
            tabSet.Tabs.AddRange([tab0, tab1, tab2]);
            handler.Tabset = tabSet;

            await renderedTabSet.InvokeAsync(() => handler.InitActiveTab());

            Assert.That(handler.Tabset.ActiveTab, Is.EqualTo(tab2));
        }

        [Test]
        public async Task InitActiveTab_DefaultsToFirstTab_WhenNothingElseMatches()
        {
            ModellingAppHandler handler = CreateHandler([]);

            using BunitContext context = new();
            IRenderedComponent<TabSet> renderedTabSet = context.Render<TabSet>();
            TabSet tabSet = renderedTabSet.Instance;
            Tab tab0 = new();
            Tab tab1 = new();
            Tab tab2 = new();
            SetComponentParameter(tab0, nameof(Tab.Position), 0);
            SetComponentParameter(tab1, nameof(Tab.Position), 1);
            SetComponentParameter(tab2, nameof(Tab.Position), 2);
            tabSet.Tabs.AddRange([tab0, tab1, tab2]);
            handler.Tabset = tabSet;

            await renderedTabSet.InvokeAsync(() => handler.InitActiveTab());

            Assert.That(handler.Tabset.ActiveTab, Is.EqualTo(tab0));
        }

        [Test]
        public async Task RestoreTab_UsesStoredTabPosition()
        {
            ModellingAppHandler handler = CreateHandler([]);

            using BunitContext context = new();
            IRenderedComponent<TabSet> renderedTabSet = context.Render<TabSet>();
            TabSet tabSet = renderedTabSet.Instance;
            Tab tab0 = new();
            Tab tab2 = new();
            SetComponentParameter(tab0, nameof(Tab.Position), 0);
            SetComponentParameter(tab2, nameof(Tab.Position), 2);
            tabSet.Tabs.AddRange([tab0, tab2]);
            await renderedTabSet.InvokeAsync(() => tabSet.SetActiveTab(tab0));
            handler.Tabset = tabSet;

            Tab actTab = new();
            SetComponentParameter(actTab, nameof(Tab.Position), 2);
            SetPrivateField(handler, "ActTab", actTab);

            await renderedTabSet.InvokeAsync(() => handler.RestoreTab());

            Assert.That(handler.Tabset.ActiveTab, Is.EqualTo(tab2));
        }

        [Test]
        public async Task RestoreTab_UsesProvidedConnectionTab()
        {
            ModellingConnection common = new() { Id = 2, IsCommonService = true };
            ModellingAppHandler handler = CreateHandler([common]);

            using BunitContext context = new();
            IRenderedComponent<TabSet> renderedTabSet = context.Render<TabSet>();
            TabSet tabSet = renderedTabSet.Instance;
            Tab tab0 = new();
            Tab tab1 = new();
            Tab tab2 = new();
            SetComponentParameter(tab0, nameof(Tab.Position), 0);
            SetComponentParameter(tab1, nameof(Tab.Position), 1);
            SetComponentParameter(tab2, nameof(Tab.Position), 2);
            tabSet.Tabs.AddRange([tab0, tab1, tab2]);
            await renderedTabSet.InvokeAsync(() => tabSet.SetActiveTab(tab0));
            handler.Tabset = tabSet;

            await renderedTabSet.InvokeAsync(() => handler.RestoreTab(common));

            Assert.That(handler.Tabset.ActiveTab, Is.EqualTo(tab2));
        }

        [Test]
        public async Task AddMethods_CreateEditors_WhenOwner()
        {
            ModellingAppHandlerTestApiConn apiConn = new();
            List<Exception?> exceptions = [];
            List<string> titles = [];
            List<string> messages = [];
            ModellingAppHandler handler = CreateHandler([], isOwner: true,
                displayMessageInUi: (exception, title, message, _) =>
                {
                    exceptions.Add(exception);
                    titles.Add(title);
                    messages.Add(message);
                },
                apiConnection: apiConn);

            await handler.AddConnection();
            Assert.Multiple(() =>
            {
                Assert.That(handler.AddConnMode, Is.True);
                Assert.That(handler.ReadOnly, Is.False);
                Assert.That(handler.EditConnMode, Is.True);
                Assert.That(handler.ConnHandler, Is.Not.Null);
                Assert.That(handler.ConnHandler!.ActConn.IsInterface, Is.False);
                Assert.That(handler.ConnHandler.ActConn.IsCommonService, Is.False);
            });

            await handler.AddInterface();
            Assert.Multiple(() =>
            {
                Assert.That(handler.ConnHandler, Is.Not.Null);
                Assert.That(handler.ConnHandler!.ActConn.IsInterface, Is.True);
                Assert.That(handler.ConnHandler.ActConn.IsCommonService, Is.False);
            });

            await handler.AddCommonService();
            Assert.Multiple(() =>
            {
                Assert.That(handler.ConnHandler, Is.Not.Null);
                Assert.That(handler.ConnHandler!.ActConn.IsInterface, Is.False);
                Assert.That(handler.ConnHandler.ActConn.IsCommonService, Is.True);
            });

            Assert.That(exceptions, Is.Empty);
            Assert.That(titles, Is.Empty);
            Assert.That(messages, Is.Empty);
            Assert.That(apiConn.Queries, Does.Contain(ModellingQueries.getDummyAppRole));
            Assert.That(apiConn.Queries, Does.Contain(ModellingQueries.getSelectedConnections));
            Assert.That(apiConn.Queries, Does.Contain(ModellingQueries.getAppServersForOwner));
            Assert.That(apiConn.Queries, Does.Contain(ModellingQueries.getServiceGroupsForApp));
            Assert.That(apiConn.Queries, Does.Contain(ModellingQueries.getServicesForApp));
        }

        [Test]
        public async Task EditConn_UsesReadOnlyMode_WhenNotOwner()
        {
            ModellingConnection connection = new() { Id = 1, Name = "Conn1" };
            ModellingAppHandlerTestApiConn apiConn = new();
            ModellingAppHandler handler = CreateHandler([connection], isOwner: false, apiConnection: apiConn);

            await handler.EditConn(connection);

            Assert.Multiple(() =>
            {
                Assert.That(handler.ReadOnly, Is.True);
                Assert.That(handler.AddConnMode, Is.False);
                Assert.That(handler.EditConnMode, Is.True);
                Assert.That(handler.ConnHandler, Is.Not.Null);
                Assert.That(handler.ConnHandler!.ActConn.Id, Is.EqualTo(connection.Id));
            });
        }

        [Test]
        public async Task EditConn_UsesEditableMode_WhenOwner()
        {
            ModellingConnection connection = new() { Id = 1, Name = "Conn1" };
            ModellingAppHandlerTestApiConn apiConn = new();
            ModellingAppHandler handler = CreateHandler([connection], isOwner: true, apiConnection: apiConn);

            await handler.EditConn(connection);

            Assert.Multiple(() =>
            {
                Assert.That(handler.ReadOnly, Is.False);
                Assert.That(handler.AddConnMode, Is.False);
                Assert.That(handler.EditConnMode, Is.True);
                Assert.That(handler.ConnHandler, Is.Not.Null);
                Assert.That(handler.ConnHandler!.ActConn.Id, Is.EqualTo(connection.Id));
            });
        }

        [Test]
        public async Task ShowUsingConnections_HandlesInterfaceAndNonInterfaceConnections()
        {
            ModellingConnection interfaceConn = new() { Id = 11, Name = "Iface", IsInterface = true };
            ModellingConnection plainConn = new() { Id = 12, Name = "Plain" };
            ModellingAppHandlerTestApiConn apiConn = new()
            {
                InterfaceUsers = [new() { Id = 100 }]
            };
            ModellingAppHandler handler = CreateHandler([interfaceConn, plainConn], apiConnection: apiConn);

            await handler.ShowUsingConnections(plainConn);
            Assert.Multiple(() =>
            {
                Assert.That(handler.ShowUsingConnectionsMode, Is.True);
                Assert.That(handler.InterfaceName, Is.Empty);
                Assert.That(apiConn.Queries, Does.Not.Contain(ModellingQueries.getInterfaceUsers));
            });

            await handler.ShowUsingConnections(interfaceConn);
            Assert.Multiple(() =>
            {
                Assert.That(handler.ShowUsingConnectionsMode, Is.True);
                Assert.That(handler.InterfaceName, Is.EqualTo("Iface"));
                Assert.That(handler.UsingConnections, Has.Count.EqualTo(1));
                Assert.That(apiConn.Queries, Does.Contain(ModellingQueries.getInterfaceUsers));
            });
        }

        [Test]
        public async Task RequestDeleteConnection_SetsDeleteMode_ForInterfaceAndRegularConnections()
        {
            SimulatedUserConfig userConfig = new();
            ModellingConnection interfaceConn = new() { Id = 11, Name = "Iface", IsInterface = true };
            ModellingConnection regularConn = new() { Id = 12, Name = "Regular" };
            ModellingAppHandlerTestApiConn apiConn = new();
            ModellingAppHandler handler = CreateHandler([interfaceConn, regularConn], userConfig, apiConnection: apiConn);

            await handler.RequestDeleteConnection(regularConn);
            Assert.Multiple(() =>
            {
                Assert.That(handler.DeleteConnMode, Is.True);
                Assert.That(handler.DeleteAllowed, Is.True);
                Assert.That(handler.Message, Is.EqualTo(userConfig.GetText("U9001") + "Regular?"));
            });

            handler.DeleteConnMode = false;
            await handler.RequestDeleteConnection(interfaceConn);
            Assert.Multiple(() =>
            {
                Assert.That(handler.DeleteConnMode, Is.True);
                Assert.That(handler.DeleteAllowed, Is.True);
                Assert.That(handler.Message, Is.EqualTo(userConfig.GetText("U9014") + "Iface?"));
            });
        }

        [Test]
        public async Task DeleteConnection_RemovesConnectionAndLogsHistory()
        {
            ModellingConnection connection = new() { Id = 42, Name = "Conn42" };
            ModellingAppHandlerTestApiConn apiConn = new();
            ModellingAppHandler handler = CreateHandler([connection], apiConnection: apiConn);
            handler.ConnToDelete = connection;

            await handler.DeleteConnection();

            Assert.Multiple(() =>
            {
                Assert.That(handler.Connections, Is.Empty);
                Assert.That(handler.DeleteConnMode, Is.False);
                Assert.That(apiConn.Queries, Does.Contain(ModellingQueries.deleteConnection));
                Assert.That(apiConn.Queries, Does.Contain(ModellingQueries.addHistoryEntry));
            });
        }

        [Test]
        public async Task PermissionDeniedMethods_KeepStateUnchanged()
        {
            SimulatedUserConfig userConfig = new();
            List<(string Title, string Message, bool ErrorFlag)> messages = [];
            ModellingAppHandler handler = CreateHandler([], userConfig, isOwner: false,
                displayMessageInUi: (_, title, message, errorFlag) => messages.Add((title, message, errorFlag)));

            await handler.AddConnection();
            await handler.AddInterface();
            await handler.AddCommonService();
            await handler.DeleteConnection();

            Assert.Multiple(() =>
            {
                Assert.That(handler.AddConnMode, Is.False);
                Assert.That(handler.DeleteConnMode, Is.False);
                Assert.That(messages, Has.Count.EqualTo(4));
                Assert.That(messages.Select(m => m.Title), Is.EquivalentTo(PermissionDeniedActionTitles));
                Assert.That(messages.All(m => m.Message == userConfig.GetText("E9104")), Is.True);
                Assert.That(messages.All(m => m.ErrorFlag), Is.True);
            });
        }

        private sealed class ModellingAppHandlerTestApiConn : SimulatedApiConnection
        {
            public List<string> Queries { get; } = [];
            public List<ModellingConnectionWrapper> SelectedConnections { get; set; } = [];
            public List<ModellingAppServer> AppServers { get; set; } = [];
            public List<ModellingAppRole> AppRoles { get; set; } = [];
            public List<ModellingNetworkArea> NetworkAreas { get; set; } = [];
            public List<ModellingNwGroupWrapper> SelectedNwGroupObjects { get; set; } = [];
            public List<ModellingServiceGroup> GlobalServiceGroups { get; set; } = [];
            public List<ModellingServiceGroup> ServiceGroupsForApp { get; set; } = [];
            public List<ModellingService> GlobalServices { get; set; } = [];
            public List<ModellingService> ServicesForApp { get; set; } = [];
            public List<FwoOwner> OwnersWithConn { get; set; } = [];
            public List<ModellingAppRole> DummyAppRoles { get; set; } = [new() { Id = 99, Name = "dummy" }];
            public List<FwoOwner> PermittedOwners { get; set; } = [];
            public List<ModellingConnection> InterfaceUsers { get; set; } = [];

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, FWO.Api.Client.QueryChunkingOptions? chunkingOptions = null)
            {
                Queries.Add(query);

                if (query == ModellingQueries.getSelectedConnections)
                {
                    return Task.FromResult((QueryResponseType)(object)SelectedConnections);
                }
                if (query == ModellingQueries.getAppServersForOwner)
                {
                    return Task.FromResult((QueryResponseType)(object)AppServers);
                }
                if (query == ModellingQueries.getAppRoles)
                {
                    return Task.FromResult((QueryResponseType)(object)AppRoles);
                }
                if (query == ModellingQueries.getNwGroupObjects)
                {
                    return Task.FromResult((QueryResponseType)(object)NetworkAreas);
                }
                if (query == ModellingQueries.getSelectedNwGroupObjects)
                {
                    return Task.FromResult((QueryResponseType)(object)SelectedNwGroupObjects);
                }
                if (query == ModellingQueries.getGlobalServiceGroups)
                {
                    return Task.FromResult((QueryResponseType)(object)GlobalServiceGroups);
                }
                if (query == ModellingQueries.getServiceGroupsForApp)
                {
                    return Task.FromResult((QueryResponseType)(object)ServiceGroupsForApp);
                }
                if (query == ModellingQueries.getGlobalServices)
                {
                    return Task.FromResult((QueryResponseType)(object)GlobalServices);
                }
                if (query == ModellingQueries.getServicesForApp)
                {
                    return Task.FromResult((QueryResponseType)(object)ServicesForApp);
                }
                if (query == OwnerQueries.getOwnersWithConn)
                {
                    return Task.FromResult((QueryResponseType)(object)OwnersWithConn);
                }
                if (query == ModellingQueries.getDummyAppRole)
                {
                    return Task.FromResult((QueryResponseType)(object)DummyAppRoles);
                }
                if (query == ModellingQueries.getPermittedOwnersForConnection)
                {
                    return Task.FromResult((QueryResponseType)(object)PermittedOwners);
                }
                if (query == ModellingQueries.getInterfaceUsers)
                {
                    return Task.FromResult((QueryResponseType)(object)InterfaceUsers);
                }
                if (query == ModellingQueries.deleteConnection)
                {
                    return Task.FromResult((QueryResponseType)(object)new ReturnId { DeletedId = ReadInt(variables, "id") });
                }
                if (query == ModellingQueries.addHistoryEntry)
                {
                    return Task.FromResult((QueryResponseType)(object)new ReturnIdWrapper { ReturnIds = [] });
                }

                throw new AssertionException($"Unexpected query: {query}");
            }

            private static int ReadInt(object? variables, string propertyName)
            {
                PropertyInfo? property = variables?.GetType().GetProperty(propertyName);
                object? value = property?.GetValue(variables);
                return value != null ? Convert.ToInt32(value) : 0;
            }
        }
    }
}
