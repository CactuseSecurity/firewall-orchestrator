using System.Reflection;
using System.Text.Json;
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
using FWO.Services.EventMediator;
using FWO.Services.EventMediator.Interfaces;
using FWO.Services.Modelling;
using FWO.Services.Workflow;
using FWO.Ui.Pages.NetworkModelling;
using FWO.Ui.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal partial class UiEditNetworkModellingComponentsTest
    {
        private static BunitContext CreateContext(out RecordingApiConnection apiConn, out SimulatedUserConfig userConfig)
        {
            BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton<AuthenticationStateProvider>(new AllowAllAuthStateProvider(Roles.Admin, Roles.Modeller));

            apiConn = new RecordingApiConnection();
            userConfig = new SimulatedUserConfig();
            userConfig.ModNamingConvention = "{}";
            userConfig.ModExtraConfigs = "[]";
            userConfig.AvailableModules = "[]";
            userConfig.ModAppServerTypes = "[]";
            userConfig.User.Name = "tester";
            userConfig.User.Dn = "uid=tester,ou=people,dc=example,dc=com";
            context.Services.AddSingleton<ApiConnection>(apiConn);
            context.Services.AddSingleton<UserConfig>(userConfig);
            context.Services.AddSingleton(new MiddlewareClient("http://localhost/"));
            RegisterBlazorTableLocalizationStub(context.Services);
            context.Services.AddSingleton<DomEventService>();
            context.Services.AddSingleton<KeyboardInputService>(new KeyboardInputService());
            context.Services.AddSingleton<IEventMediator>(new EventMediator());
            return context;
        }

        private static ModellingAppRoleHandler CreateAppRoleHandler(
            ApiConnection apiConn,
            SimulatedUserConfig userConfig,
            bool networkAreaRequired,
            List<ModellingAppServer> availableAppServers,
            ModellingAppRole appRole,
            bool addMode = false,
            Action<Exception?, string, string, bool>? messageSink = null)
        {
            if (string.IsNullOrWhiteSpace(userConfig.ModExtraConfigs))
            {
                userConfig.ModExtraConfigs = "[]";
            }
            if (string.IsNullOrWhiteSpace(userConfig.AvailableModules))
            {
                userConfig.AvailableModules = "[]";
            }
            userConfig.ModNamingConvention = JsonSerializer.Serialize<ModellingNamingConvention>(new ModellingNamingConvention
            {
                NetworkAreaRequired = networkAreaRequired,
                FixedPartLength = 4,
                FreePartLength = 5,
                NetworkAreaPattern = "NA",
                AppRolePattern = "AR"
            });

            return new ModellingAppRoleHandler(
                apiConn,
                userConfig,
                new FwoOwner { Id = 9 },
                [],
                appRole,
                availableAppServers,
                [],
                addMode,
                messageSink ?? ((_, _, _, _) => { }),
                isOwner: true,
                readOnly: false);
        }

        private static ModellingServiceGroupHandler CreateServiceGroupHandler(
            ApiConnection apiConn,
            SimulatedUserConfig userConfig,
            ModellingServiceGroup serviceGroup,
            List<ModellingService> availableServices,
            List<KeyValuePair<int, int>> availableSvcElems,
            bool addMode,
            Action<Exception?, string, string, bool>? messageSink = null,
            Func<Task>? refreshParent = null,
            bool isOwner = true,
            bool readOnly = false)
        {
            if (string.IsNullOrWhiteSpace(userConfig.ModExtraConfigs))
            {
                userConfig.ModExtraConfigs = "[]";
            }
            if (string.IsNullOrWhiteSpace(userConfig.AvailableModules))
            {
                userConfig.AvailableModules = "[]";
            }

            return new ModellingServiceGroupHandler(
                apiConn,
                userConfig,
                new FwoOwner { Id = 19 },
                [],
                serviceGroup,
                availableServices,
                availableSvcElems,
                addMode,
                messageSink ?? ((_, _, _, _) => { }),
                refreshParent ?? (() => Task.CompletedTask),
                isOwner,
                readOnly);
        }

        private static ModellingConnectionHandler CreateConnectionHandler(
            ApiConnection apiConn,
            SimulatedUserConfig userConfig,
            ModellingConnection connection,
            bool readOnly = false,
            bool isOwner = true,
            bool addMode = false)
        {
            return new ModellingConnectionHandler(
                apiConn,
                userConfig,
                new FwoOwner { Id = 17 },
                [connection],
                connection,
                addMode,
                readOnly,
                (_, _, _, _) => { },
                () => Task.CompletedTask,
                isOwner);
        }

        private static ModellingAppServerHandler CreateAppServerHandler(
            ApiConnection apiConn,
            SimulatedUserConfig userConfig,
            ModellingAppServer appServer,
            List<ModellingAppServer> availableAppServers,
            bool addMode)
        {
            return new ModellingAppServerHandler(
                apiConn,
                userConfig,
                new FwoOwner { Id = 42 },
                appServer,
                availableAppServers,
                addMode,
                (_, _, _, _) => { },
                false,
                false);
        }

        private static IRenderedComponent<EditAppRole> RenderEditAppRole(
            BunitContext context,
            ModellingAppRoleHandler handler,
            Action<bool>? displayChanged = null,
            Action<ModellingAppRoleHandler>? handlerChanged = null)
        {
            return context.Render<CascadingAuthenticationState>(parameters => parameters.AddChildContent<EditAppRole>(component => component
                .Add(p => p.Display, true)
                .Add(p => p.DisplayChanged, value => displayChanged?.Invoke(value))
                .Add(p => p.AppRoleHandler, handler)
                .Add(p => p.AppRoleHandlerChanged, value => handlerChanged?.Invoke(value))
                .Add(p => p.RefreshParent, () => Task.CompletedTask)))
                .FindComponent<EditAppRole>();
        }

        private static IRenderedComponent<EditAppRoleLeftSide> RenderEditAppRoleLeftSide(
            BunitContext context,
            ModellingAppRoleHandler handler,
            ModellingDnDContainer? container = null)
        {
            return context.Render<CascadingAuthenticationState>(parameters => parameters.AddChildContent<EditAppRoleLeftSide>(component => component
                .Add(p => p.Container, container ?? new ModellingDnDContainer())
                .Add(p => p.Width, GlobalConst.kObjLibraryWidth)
                .Add(p => p.AppRoleHandler, handler)))
                .FindComponent<EditAppRoleLeftSide>();
        }

        private static IRenderedComponent<EditServiceGroup> RenderEditServiceGroup(
            BunitContext context,
            ModellingServiceGroupHandler handler,
            bool display = true,
            Action<bool>? displayChanged = null,
            Action<ModellingServiceGroupHandler>? handlerChanged = null)
        {
            return context.Render<CascadingAuthenticationState>(parameters => parameters.AddChildContent<EditServiceGroup>(component => component
                .Add(p => p.Display, display)
                .Add(p => p.DisplayChanged, value => displayChanged?.Invoke(value))
                .Add(p => p.SvcGroupHandler, handler)
                .Add(p => p.SvcGroupHandlerChanged, value => handlerChanged?.Invoke(value))))
                .FindComponent<EditServiceGroup>();
        }

        private static IRenderedComponent<EditServiceGroupLeftSide> RenderEditServiceGroupLeftSide(
            BunitContext context,
            ModellingServiceGroupHandler handler,
            ModellingDnDContainer? container = null)
        {
            return context.Render<CascadingAuthenticationState>(parameters => parameters.AddChildContent<EditServiceGroupLeftSide>(component => component
                .Add(p => p.Container, container ?? new ModellingDnDContainer())
                .Add(p => p.Width, GlobalConst.kObjLibraryWidth)
                .Add(p => p.SvcGroupHandler, handler)))
                .FindComponent<EditServiceGroupLeftSide>();
        }

        private static IRenderedComponent<EditConn> RenderEditConn(
            BunitContext context,
            ModellingConnectionHandler handler,
            bool display,
            Action<bool>? displayChanged = null,
            Func<bool>? closingAction = null)
        {
            return context.Render<CascadingAuthenticationState>(parameters => parameters.AddChildContent<EditConn>(component => component
                .Add(p => p.Display, display)
                .Add(p => p.DisplayChanged, value => displayChanged?.Invoke(value))
                .Add(p => p.ConnHandler, handler)
                .Add(p => p.ClosingAction, closingAction ?? (() => true))))
                .FindComponent<EditConn>();
        }

        private static IRenderedComponent<EditConnLeftSide> RenderEditConnLeftSide(
            BunitContext context,
            ModellingDnDContainer? container = null,
            Action<ModellingConnectionHandler>? handlerChanged = null)
        {
            return context.Render<CascadingAuthenticationState>(parameters => parameters.AddChildContent<EditConnLeftSide>(component => component
                .Add(p => p.Container, container ?? new ModellingDnDContainer())
                .Add(p => p.Width, GlobalConst.kObjLibraryWidth)
                .Add(p => p.ConnHandlerChanged, value => handlerChanged?.Invoke(value))))
                .FindComponent<EditConnLeftSide>();
        }

        private static IRenderedComponent<EditConnPopup> RenderEditConnPopup(
            BunitContext context,
            bool display,
            bool replaceMode,
            Func<Task>? replace = null,
            Action<bool>? displayChanged = null,
            ModellingConnectionHandler? connHandler = null,
            bool showLogData = false)
        {
            return context.Render<CascadingAuthenticationState>(parameters => parameters.AddChildContent<EditConnPopup>(component => component
                .Add(p => p.Display, display)
                .Add(p => p.DisplayChanged, value => displayChanged?.Invoke(value))
                .Add(p => p.ConnHandler, connHandler)
                .Add(p => p.ReplaceMode, replaceMode)
                .Add(p => p.Replace, replace ?? (() => Task.CompletedTask))
                .Add(p => p.ShowLogData, showLogData)))
                .FindComponent<EditConnPopup>();
        }

        private static IRenderedComponent<EditAppServer> RenderEditAppServer(
            BunitContext context,
            ModellingAppServerHandler handler,
            bool display,
            Action<bool>? displayChanged = null,
            Action<ModellingAppServerHandler>? handlerChanged = null)
        {
            return context.Render<CascadingAuthenticationState>(parameters => parameters.AddChildContent<EditAppServer>(component => component
                .Add(p => p.Display, display)
                .Add(p => p.DisplayChanged, value => displayChanged?.Invoke(value))
                .Add(p => p.AppServerHandler, handler)
                .Add(p => p.AppServerHandlerChanged, value => handlerChanged?.Invoke(value))))
                .FindComponent<EditAppServer>();
        }

        private static IRenderedComponent<ShowHistory> RenderShowHistory(
            BunitContext context,
            bool display,
            List<FwoOwner> applications,
            FwoOwner selectedApp,
            Action<bool>? displayChanged = null)
        {
            return context.Render<CascadingAuthenticationState>(parameters => parameters.AddChildContent<ShowHistory>(component => component
                .Add(p => p.Display, display)
                .Add(p => p.DisplayChanged, value => displayChanged?.Invoke(value))
                .Add(p => p.Applications, applications)
                .Add(p => p.SelectedApp, selectedApp)))
                .FindComponent<ShowHistory>();
        }

        private static IRenderedComponent<ShareLink> RenderShareLink(
            BunitContext context,
            bool display,
            FwoOwner application,
            Action<bool>? displayChanged = null)
        {
            return context.Render<CascadingAuthenticationState>(parameters => parameters.AddChildContent<ShareLink>(component => component
                .Add(p => p.Display, display)
                .Add(p => p.DisplayChanged, value => displayChanged?.Invoke(value))
                .Add(p => p.Application, application)))
                .FindComponent<ShareLink>();
        }

        private static IRenderedComponent<SearchNwObject> RenderSearchNwObject(
            BunitContext context,
            bool display,
            List<ModellingNwGroupWrapper> objectList,
            FwoOwner application,
            Func<bool> refresh,
            Func<ModellingNwGroup, bool> add,
            Action<bool>? displayChanged = null,
            bool commonAreaMode = false,
            bool specUserMode = false,
            bool updObjMode = false)
        {
            return context.Render<CascadingAuthenticationState>(parameters => parameters.AddChildContent<SearchNwObject>(component => component
                .Add(p => p.Display, display)
                .Add(p => p.DisplayChanged, value => displayChanged?.Invoke(value))
                .Add(p => p.ObjectList, objectList)
                .Add(p => p.Application, application)
                .Add(p => p.Refresh, refresh)
                .Add(p => p.Add, add)
                .Add(p => p.CommonAreaMode, commonAreaMode)
                .Add(p => p.SpecUserMode, specUserMode)
                .Add(p => p.UpdObjMode, updObjMode)))
                .FindComponent<SearchNwObject>();
        }

        private static IRenderedComponent<SearchInterface> RenderSearchInterface(
            BunitContext context,
            bool display,
            List<ModellingConnection>? preselectedInterfaces,
            FwoOwner application,
            Action<bool>? displayChanged = null)
        {
            return context.Render<CascadingAuthenticationState>(parameters => parameters.AddChildContent<SearchInterface>(component => component
                .Add(p => p.Display, display)
                .Add(p => p.DisplayChanged, value => displayChanged?.Invoke(value))
                .Add(p => p.PreselectedInterfaces, preselectedInterfaces)
                .Add(p => p.Application, application)))
                .FindComponent<SearchInterface>();
        }

        private static IRenderedComponent<AddExtraConfig> RenderAddExtraConfig(
            BunitContext context,
            ModellingConnectionHandler handler,
            bool display,
            List<string> availableExtraConfigTypes,
            Action<bool>? displayChanged = null,
            Action<ModellingConnectionHandler>? connectionHandlerChanged = null)
        {
            return context.Render<CascadingAuthenticationState>(parameters => parameters.AddChildContent<AddExtraConfig>(component => component
                .Add(p => p.Display, display)
                .Add(p => p.DisplayChanged, value => displayChanged?.Invoke(value))
                .Add(p => p.ConnectionHandler, handler)
                .Add(p => p.ConnectionHandlerChanged, value => connectionHandlerChanged?.Invoke(value))
                .Add(p => p.AvailableExtraConfigTypes, availableExtraConfigTypes)))
                .FindComponent<AddExtraConfig>();
        }

        private static IRenderedComponent<DecommissionInterfacePopup> RenderDecommissionInterfacePopup(
            BunitContext context,
            bool display,
            ModellingConnectionHandler connHandler,
            List<ModellingConnection> possibleInterfaces,
            Action<bool>? displayChanged = null,
            Func<Task>? refreshParent = null)
        {
            return context.Render<CascadingAuthenticationState>(parameters => parameters.AddChildContent<DecommissionInterfacePopup>(component => component
                .Add(p => p.Display, display)
                .Add(p => p.DisplayChanged, value => displayChanged?.Invoke(value))
                .Add(p => p.ConnHandler, connHandler)
                .Add(p => p.PossibleInterfaces, possibleInterfaces)
                .Add(p => p.RefreshParent, refreshParent ?? (() => Task.CompletedTask))))
                .FindComponent<DecommissionInterfacePopup>();
        }

        private static IRenderedComponent<InterfaceUsersPopup> RenderInterfaceUsersPopup(
            BunitContext context,
            bool display,
            string interfaceName,
            List<ModellingConnection> usingConnections,
            FwoOwner app,
            Action<bool>? displayChanged = null)
        {
            RenderFragment fragment = builder =>
            {
                builder.OpenComponent<CascadingAuthenticationState>(0);
                builder.AddAttribute(1, "ChildContent", (RenderFragment)(childBuilder =>
                {
                    childBuilder.OpenComponent<InterfaceUsersPopup>(0);
                    childBuilder.AddAttribute(1, "Display", display);
                    childBuilder.AddAttribute(2, "DisplayChanged", EventCallback.Factory.Create<bool>(context, value => displayChanged?.Invoke(value)));
                    childBuilder.AddAttribute(3, "InterfaceName", interfaceName);
                    childBuilder.AddAttribute(4, "UsingConnections", usingConnections);
                    childBuilder.AddAttribute(5, "App", app);
                    childBuilder.CloseComponent();
                }));
                builder.CloseComponent();
            };

            return context.Render(fragment).FindComponent<InterfaceUsersPopup>();
        }

        private static IRenderedComponent<ManualAppServer> RenderManualAppServer(
            BunitContext context,
            FwoOwner application,
            bool display,
            Action<bool>? displayChanged = null)
        {
            return context.Render<CascadingAuthenticationState>(parameters => parameters.AddChildContent<ManualAppServer>(component => component
                .Add(p => p.Display, display)
                .Add(p => p.DisplayChanged, value => displayChanged?.Invoke(value))
                .Add(p => p.Application, application)))
                .FindComponent<ManualAppServer>();
        }

        private static IRenderedComponent<PermittedOwnersSelection> RenderPermittedOwnersSelection(
            BunitContext context,
            List<FwoOwner> allOwners,
            List<FwoOwner> permittedOwners,
            List<FwoOwner> ownersToAdd,
            List<FwoOwner> ownersToDelete,
            bool readonlyMode)
        {
            return context.Render<PermittedOwnersSelection>(parameters => parameters
                .Add(p => p.AllOwners, allOwners)
                .Add(p => p.PermittedOwners, permittedOwners)
                .Add(p => p.OwnersToAdd, ownersToAdd)
                .Add(p => p.OwnersToDelete, ownersToDelete)
                .Add(p => p.Readonly, readonlyMode));
        }

        private static IRenderedComponent<PredefServices> RenderPredefServices(
            BunitContext context,
            bool display,
            Action<bool>? displayChanged = null)
        {
            return context.Render<CascadingAuthenticationState>(parameters => parameters.AddChildContent<PredefServices>(component => component
                .Add(p => p.Display, display)
                .Add(p => p.DisplayChanged, value => displayChanged?.Invoke(value))))
                .FindComponent<PredefServices>();
        }

        private static void RegisterBlazorTableLocalizationStub(IServiceCollection services)
        {
            services.AddSingleton(typeof(IStringLocalizer<>), typeof(EmptyStringLocalizer<>));
        }

        private static MethodInfo GetPrivateMethod(Type type, string name)
        {
            return type.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMethodException(type.FullName, name);
        }

        private static T GetPrivateField<T>(object instance, string fieldName)
        {
            FieldInfo field = instance.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingFieldException(instance.GetType().FullName, fieldName);
            return (T)field.GetValue(instance)!;
        }

        private static T GetPrivateProperty<T>(object instance, string propertyName)
        {
            PropertyInfo property = instance.GetType().GetProperty(propertyName, BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMemberException(instance.GetType().FullName, propertyName);
            return (T)property.GetValue(instance)!;
        }

        private static void SetComponentProperty(object instance, string propertyName, object? value)
        {
            PropertyInfo property = instance.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance)
                ?? throw new MissingMemberException(instance.GetType().FullName, propertyName);
            property.SetValue(instance, value);
        }

        private static void SetPrivateProperty(object instance, string propertyName, object? value)
        {
            PropertyInfo property = instance.GetType().GetProperty(propertyName, BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMemberException(instance.GetType().FullName, propertyName);
            property.SetValue(instance, value);
        }

        private static void SetPrivateField(object instance, string fieldName, object? value)
        {
            FieldInfo field = instance.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingFieldException(instance.GetType().FullName, fieldName);
            field.SetValue(instance, value);
        }

        private static ModellingNetworkArea CreateArea(long id, string idString, string name, string ip, string ipEnd)
        {
            return new ModellingNetworkArea
            {
                Id = id,
                IdString = idString,
                Name = name,
                IpData =
                [
                    new NetworkDataWrapper
                    {
                        Content = new NetworkSubnet
                        {
                            Id = (int)id,
                            Name = name,
                            Ip = ip,
                            IpEnd = ipEnd
                        }
                    }
                ]
            };
        }

        private static ModellingAppServer CreateServer(long id, string name, string ip, bool deleted = false)
        {
            return new ModellingAppServer
            {
                Id = id,
                AppId = 7,
                Name = name,
                Ip = ip,
                IpEnd = ip,
                ImportSource = GlobalConst.kManual,
                CustomType = 1,
                IsDeleted = deleted
            };
        }

        private sealed class RecordingApiConnection : SimulatedApiConnection
        {
            private readonly string stateMatrix = JsonSerializer.Serialize(new GlobalStateMatrix
            {
                GlobalMatrix =
                {
                    [WorkflowPhases.request] = CreateMatrix(0, 1, 49, true),
                    [WorkflowPhases.approval] = CreateMatrix(49, 60, 99, false),
                    [WorkflowPhases.planning] = CreateMatrix(99, 110, 149, false),
                    [WorkflowPhases.verification] = CreateMatrix(149, 160, 199, false),
                    [WorkflowPhases.implementation] = CreateMatrix(49, 210, 249, false),
                    [WorkflowPhases.review] = CreateMatrix(249, 260, 299, false),
                    [WorkflowPhases.recertification] = CreateMatrix(299, 310, 349, false)
                }
            });
            public List<ModellingNetworkArea> Areas { get; set; } = new List<ModellingNetworkArea>();
            public List<ModellingAppServer> ManualServers { get; set; } = new List<ModellingAppServer>();
            public List<ModellingAppServer> CsvServers { get; set; } = new List<ModellingAppServer>();
            public List<ModellingServiceGroup> GlobalServiceGroups { get; set; } = new List<ModellingServiceGroup>();
            public List<ModellingService> GlobalServices { get; set; } = new List<ModellingService>();
            public List<ModellingConnection> ConnectionsForServiceGroup { get; set; } = new List<ModellingConnection>();
            public List<ModellingHistoryEntry> HistoryForApp { get; set; } = new List<ModellingHistoryEntry>();
            public List<ModellingHistoryEntry> HistoryAll { get; set; } = new List<ModellingHistoryEntry>();
            public List<ModellingConnection> PublishedInterfaces { get; set; } = new List<ModellingConnection>();
            public List<ModellingNwGroup> NwGroupObjects { get; set; } = new List<ModellingNwGroup>();
            public WfTicket TicketById { get; set; } = new();
            public List<WfState> States { get; set; } = new()
            {
                new WfState { Id = 0 },
                new WfState { Id = 1 },
                new WfState { Id = 7 },
                new WfState { Id = 49 },
                new WfState { Id = 249 }
            };
            public List<WfExtState> ExtStates { get; set; } = new();
            public int UpdateConnectionCalls { get; private set; }
            public int UpdateTicketStateCalls { get; private set; }
            public int UpdateRequestTaskStateCalls { get; private set; }
            public int UpdateImplTaskStateCalls { get; private set; }
            public int GetTicketByIdCalls { get; private set; }
            public int GetStatesCalls { get; private set; }
            public int GetExtStatesCalls { get; private set; }
            public int NewAppServerCalls { get; private set; }
            public int NewAppRoleCalls { get; private set; }
            public int HistoryEntryCalls { get; private set; }
            public int DeleteServiceGroupCalls { get; private set; }
            public int NewServiceGroupCalls { get; private set; }
            public int AddServiceToServiceGroupCalls { get; private set; }
            public int HistoryForAppCalls { get; private set; }
            public int HistoryCalls { get; private set; }
            public int PublishedInterfaceCalls { get; private set; }
            public int AddSelectedConnectionCalls { get; private set; }
            public int AddSelectedNwGroupObjectCalls { get; private set; }
            public int RemoveSelectedConnectionCalls { get; private set; }
            public int UpdateConnectionDecommissionCalls { get; private set; }
            public int UpdateConnectionPropertiesCalls { get; private set; }
            public int NwGroupObjectCalls { get; private set; }
            public int NewConnectionCalls { get; private set; }

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                if (typeof(QueryResponseType) == typeof(List<WfState>) && query == RequestQueries.getStates)
                {
                    GetStatesCalls++;
                    return Task.FromResult((QueryResponseType)(object)States);
                }

                if (typeof(QueryResponseType) == typeof(List<WorkflowConfiguration>) && query == RequestQueries.getActiveStateMatrixConfiguration)
                {
                    return Task.FromResult((QueryResponseType)(object)StateMatrixConfigurationTestHelper.FromLegacyJson(stateMatrix));
                }

                if (typeof(QueryResponseType) == typeof(List<ConfigItem>) && query == ConfigQueries.getConfigItemsByUser)
                {
                    return Task.FromResult((QueryResponseType)(object)Array.Empty<ConfigItem>());
                }

                if (typeof(QueryResponseType) == typeof(List<Device>) && query == DeviceQueries.getDeviceDetails)
                {
                    return Task.FromResult((QueryResponseType)(object)new List<Device>());
                }

                if (typeof(QueryResponseType) == typeof(List<FwoOwner>) && query == OwnerQueries.getOwners)
                {
                    return Task.FromResult((QueryResponseType)(object)new List<FwoOwner>());
                }

                if (typeof(QueryResponseType) == typeof(List<WfExtState>) && query == RequestQueries.getExtStates)
                {
                    GetExtStatesCalls++;
                    return Task.FromResult((QueryResponseType)(object)ExtStates);
                }

                if (typeof(QueryResponseType) == typeof(WfTicket) && query == RequestQueries.getTicketById)
                {
                    GetTicketByIdCalls++;
                    return Task.FromResult((QueryResponseType)(object)TicketById);
                }

                if (typeof(QueryResponseType) == typeof(ReturnId) && query == RequestQueries.updateTicketState)
                {
                    UpdateTicketStateCalls++;
                    return Task.FromResult((QueryResponseType)(object)new ReturnId { UpdatedIdLong = GetVariable<long>(variables, "id") });
                }

                if (typeof(QueryResponseType) == typeof(ReturnId) && query == RequestQueries.updateRequestTaskState)
                {
                    UpdateRequestTaskStateCalls++;
                    return Task.FromResult((QueryResponseType)(object)new ReturnId { UpdatedIdLong = GetVariable<long>(variables, "id") });
                }

                if (typeof(QueryResponseType) == typeof(ReturnId) && query == RequestQueries.updateImplementationTaskState)
                {
                    UpdateImplTaskStateCalls++;
                    return Task.FromResult((QueryResponseType)(object)new ReturnId { UpdatedIdLong = GetVariable<long>(variables, "id") });
                }

                if (typeof(QueryResponseType) == typeof(List<ModellingNetworkArea>) && query == ModellingQueries.getAreas)
                {
                    return Task.FromResult((QueryResponseType)(object)Areas);
                }

                if (typeof(QueryResponseType) == typeof(List<ModellingHistoryEntry>) && query == ModellingQueries.getHistoryForApp)
                {
                    HistoryForAppCalls++;
                    return Task.FromResult((QueryResponseType)(object)HistoryForApp);
                }

                if (typeof(QueryResponseType) == typeof(List<ModellingHistoryEntry>) && query == ModellingQueries.getHistory)
                {
                    HistoryCalls++;
                    return Task.FromResult((QueryResponseType)(object)HistoryAll);
                }

                if (typeof(QueryResponseType) == typeof(List<ModellingConnection>) && query == ModellingQueries.getPublishedInterfaces)
                {
                    PublishedInterfaceCalls++;
                    return Task.FromResult((QueryResponseType)(object)PublishedInterfaces);
                }

                if (typeof(QueryResponseType) == typeof(List<ModellingNwGroup>) && query == ModellingQueries.getNwGroupObjects)
                {
                    NwGroupObjectCalls++;
                    return Task.FromResult((QueryResponseType)(object)NwGroupObjects);
                }

                if (typeof(QueryResponseType) == typeof(List<ModellingAppServer>) && query == ModellingQueries.getAppRolesForAppServer)
                {
                    return Task.FromResult((QueryResponseType)(object)new List<ModellingAppServer>());
                }

                if (typeof(QueryResponseType) == typeof(List<ModellingConnection>) && query == ModellingQueries.getConnectionIdsForAppServer)
                {
                    return Task.FromResult((QueryResponseType)(object)new List<ModellingConnection>());
                }

                if (typeof(QueryResponseType) == typeof(List<ModellingAppServer>) && query == ModellingQueries.getAppServersByIp)
                {
                    string ip = GetVariable<string>(variables, "ip");
                    string ipEnd = GetVariable<string>(variables, "ipEnd");
                    List<ModellingAppServer> sameIpServers = ManualServers
                        .Concat(CsvServers)
                        .Where(server => server.Ip.IpAsCidr() == ip && server.IpEnd.IpAsCidr() == ipEnd)
                        .ToList();
                    return Task.FromResult((QueryResponseType)(object)sameIpServers);
                }

                if (typeof(QueryResponseType) == typeof(List<ModellingAppServer>) && query == ModellingQueries.getAppServersBySource)
                {
                    string importSource = GetVariable<string>(variables, "importSource");
                    if (importSource == GlobalConst.kManual)
                    {
                        return Task.FromResult((QueryResponseType)(object)ManualServers);
                    }
                    if (importSource.StartsWith(GlobalConst.kCSV_))
                    {
                        return Task.FromResult((QueryResponseType)(object)CsvServers);
                    }
                    return Task.FromResult((QueryResponseType)(object)new List<ModellingAppServer>());
                }

                if (typeof(QueryResponseType) == typeof(List<ModellingAppServer>) && query == ModellingQueries.getAppServersByName)
                {
                    return Task.FromResult((QueryResponseType)(object)new List<ModellingAppServer>());
                }

                if (typeof(QueryResponseType) == typeof(List<ModellingAppRole>) && query == ModellingQueries.getNewestAppRoles)
                {
                    return Task.FromResult((QueryResponseType)(object)new List<ModellingAppRole>());
                }

                if (typeof(QueryResponseType) == typeof(List<ModellingServiceGroup>) && query == ModellingQueries.getGlobalServiceGroups)
                {
                    return Task.FromResult((QueryResponseType)(object)GlobalServiceGroups);
                }

                if (typeof(QueryResponseType) == typeof(List<ModellingService>) && query == ModellingQueries.getGlobalServices)
                {
                    return Task.FromResult((QueryResponseType)(object)GlobalServices);
                }

                if (typeof(QueryResponseType) == typeof(List<ModellingConnection>) && query == ModellingQueries.getConnectionIdsForServiceGroup)
                {
                    return Task.FromResult((QueryResponseType)(object)ConnectionsForServiceGroup);
                }

                if (typeof(QueryResponseType) == typeof(ReturnIdWrapper) && query == ModellingQueries.addSelectedConnection)
                {
                    AddSelectedConnectionCalls++;
                    return Task.FromResult((QueryResponseType)(object)new ReturnIdWrapper());
                }

                if (typeof(QueryResponseType) == typeof(ReturnIdWrapper) && query == ModellingQueries.newConnection)
                {
                    NewConnectionCalls++;
                    return Task.FromResult((QueryResponseType)(object)NewConnectionWrapper);
                }

                if (typeof(QueryResponseType) == typeof(ReturnId) && query == ModellingQueries.removeSelectedConnection)
                {
                    RemoveSelectedConnectionCalls++;
                    return Task.FromResult((QueryResponseType)(object)new ReturnId { AffectedRows = 1 });
                }

                if (typeof(QueryResponseType) == typeof(ReturnIdWrapper) && query == ModellingQueries.addSelectedNwGroupObject)
                {
                    AddSelectedNwGroupObjectCalls++;
                    return Task.FromResult((QueryResponseType)(object)new ReturnIdWrapper());
                }

                if (typeof(QueryResponseType) == typeof(ReturnIdWrapper) && query == ModellingQueries.newAppServer)
                {
                    NewAppServerCalls++;
                    return Task.FromResult((QueryResponseType)(object)NewAppServerWrapper);
                }

                if (typeof(QueryResponseType) == typeof(ReturnIdWrapper) && query == ModellingQueries.newAppRole)
                {
                    NewAppRoleCalls++;
                    return Task.FromResult((QueryResponseType)(object)NewAppRoleWrapper);
                }

                if (typeof(QueryResponseType) == typeof(ReturnIdWrapper) && query == ModellingQueries.newServiceGroup)
                {
                    NewServiceGroupCalls++;
                    return Task.FromResult((QueryResponseType)(object)new ReturnIdWrapper
                    {
                        ReturnIds = new List<ReturnId> { new ReturnId { NewId = 77 } }.ToArray()
                    });
                }

                if (typeof(QueryResponseType) == typeof(ReturnId) && query == ModellingQueries.addServiceToServiceGroup)
                {
                    AddServiceToServiceGroupCalls++;
                    return Task.FromResult((QueryResponseType)(object)new ReturnId());
                }

                if (typeof(QueryResponseType) == typeof(ReturnId) && query == ModellingQueries.updateConnectionDecommission)
                {
                    UpdateConnectionDecommissionCalls++;
                    return Task.FromResult((QueryResponseType)(object)new ReturnId { AffectedRows = 1 });
                }

                if (typeof(QueryResponseType) == typeof(ReturnId) && query == ModellingQueries.updateConnection)
                {
                    UpdateConnectionCalls++;
                    return Task.FromResult((QueryResponseType)(object)new ReturnId { AffectedRows = 1 });
                }

                if (typeof(QueryResponseType) == typeof(ReturnId) && query == ModellingQueries.updateConnectionProperties)
                {
                    UpdateConnectionPropertiesCalls++;
                    return Task.FromResult((QueryResponseType)(object)new ReturnId { AffectedRows = 1 });
                }

                if (typeof(QueryResponseType) == typeof(ReturnId) && query == ModellingQueries.deleteServiceGroup)
                {
                    DeleteServiceGroupCalls++;
                    return Task.FromResult((QueryResponseType)(object)new ReturnId { AffectedRows = 1 });
                }

                if (typeof(QueryResponseType) == typeof(ReturnIdWrapper) && query == ModellingQueries.addHistoryEntry)
                {
                    HistoryEntryCalls++;
                    return Task.FromResult((QueryResponseType)(object)new ReturnIdWrapper());
                }

                throw new AssertionException($"Unexpected query: {query}");
            }

            private static StateMatrix CreateMatrix(int input, int started, int end, bool active)
            {
                return new StateMatrix
                {
                    Matrix =
                    {
                        [0] = [0, 1, 7, 49],
                        [1] = [1, 7, 49],
                        [7] = [7, 49],
                        [49] = [49],
                        [249] = [249]
                    },
                    DerivedStates =
                    {
                        [0] = 0,
                        [1] = 1,
                        [7] = 7,
                        [49] = 49,
                        [249] = 249
                    },
                    LowestInputState = input,
                    LowestStartedState = started,
                    LowestEndState = end,
                    Active = active
                };
            }

            private static T GetVariable<T>(object? variables, string propertyName)
            {
                object? value = variables?.GetType().GetProperties().First(p => p.Name == propertyName).GetValue(variables, null);
                return value is T typedValue ? typedValue : throw new AssertionException($"Variable {propertyName} missing");
            }
        }

        private sealed class EmptyStringLocalizer<T> : IStringLocalizer<T>
        {
            public LocalizedString this[string name] => new(name, name, resourceNotFound: true);

            public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(name, arguments), resourceNotFound: true);

            public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => new List<LocalizedString>();

            public EmptyStringLocalizer<T> WithCulture(System.Globalization.CultureInfo culture) => this;
        }
    }
}
