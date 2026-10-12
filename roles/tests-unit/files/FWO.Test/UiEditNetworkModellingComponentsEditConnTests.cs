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
using FWO.Ui.Shared;
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
        private static readonly ReturnIdWrapper NewAppServerWrapper = new()
        {
            ReturnIds = [new ReturnId { NewIdLong = 77 }]
        };

        private static readonly ReturnIdWrapper NewConnectionWrapper = new()
        {
            ReturnIds = [new ReturnId { NewId = 88 }]
        };
        private static readonly ReturnIdWrapper NewAppRoleWrapper = new()
        {
            ReturnIds = [new ReturnId { NewIdLong = 89 }]
        };

        [Test]
        public async Task EditConn_GetTitle_ReturnsRequestedInterfaceTitle()
        {
            await using BunitContext context = CreateContext(out _, out SimulatedUserConfig userConfig);
            userConfig.ModNamingConvention = "{}";
            userConfig.ModAppServerTypes = "[]";
            ModellingConnection connection = new()
            {
                Name = "conn",
                Reason = "reason",
                IsRequested = true
            };
            ModellingConnectionHandler handler = CreateConnectionHandler(new SimulatedApiConnection(), userConfig, connection, readOnly: true);

            IRenderedComponent<EditConn> component = RenderEditConn(context, handler, display: false);
            string title = (string)GetPrivateMethod(typeof(EditConn), "GetTitle").Invoke(component.Instance, null)!;

            Assert.That(title, Is.EqualTo(userConfig.GetText("requested_interface")));
            await Task.CompletedTask;
        }

        [Test]
        public async Task EditConn_GetTitle_ReturnsConnectionTitleForPlainConnection()
        {
            await using BunitContext context = CreateContext(out _, out SimulatedUserConfig userConfig);
            userConfig.ModNamingConvention = "{}";
            userConfig.ModAppServerTypes = "[]";
            ModellingConnection connection = new()
            {
                Name = "conn",
                Reason = "reason"
            };
            ModellingConnectionHandler handler = CreateConnectionHandler(new SimulatedApiConnection(), userConfig, connection, readOnly: true);

            IRenderedComponent<EditConn> component = RenderEditConn(context, handler, display: false);
            string title = (string)GetPrivateMethod(typeof(EditConn), "GetTitle").Invoke(component.Instance, null)!;

            Assert.That(title, Is.EqualTo(userConfig.GetText("connection")));
            await Task.CompletedTask;
        }

        [Test]
        public async Task EditConn_GetTitle_ReturnsInterfaceTitle()
        {
            await using BunitContext context = CreateContext(out _, out SimulatedUserConfig userConfig);
            userConfig.ModNamingConvention = "{}";
            userConfig.ModAppServerTypes = "[]";
            ModellingConnection connection = new()
            {
                Name = "conn",
                Reason = "reason",
                IsInterface = true
            };
            ModellingConnectionHandler handler = CreateConnectionHandler(new SimulatedApiConnection(), userConfig, connection, readOnly: true);

            IRenderedComponent<EditConn> component = RenderEditConn(context, handler, display: false);
            string title = (string)GetPrivateMethod(typeof(EditConn), "GetTitle").Invoke(component.Instance, null)!;

            Assert.That(title, Is.EqualTo(userConfig.GetText("interface")));
            await Task.CompletedTask;
        }

        [Test]
        public async Task EditConn_Close_ResetsHandlerStateAndInvokesCallbacks()
        {
            await using BunitContext context = CreateContext(out _, out _);
            ModellingConnection connection = new()
            {
                Name = "conn",
                Reason = "reason"
            };
            ModellingConnectionHandler handler = CreateConnectionHandler(new SimulatedApiConnection(), new SimulatedUserConfig(), connection, readOnly: true);
            handler.SvcToAdd.Add(new ModellingService { Id = 5, Name = "svc5" });
            bool displayChanged = true;
            int closingCalls = 0;

            IRenderedComponent<EditConn> component = RenderEditConn(
                context,
                handler,
                display: true,
                displayChanged: value => displayChanged = value,
                closingAction: () =>
                {
                    closingCalls++;
                    return true;
                });

            GetPrivateMethod(typeof(EditConn), "Close").Invoke(component.Instance, null);

            Assert.Multiple(() =>
            {
                Assert.That(displayChanged, Is.False);
                Assert.That(closingCalls, Is.EqualTo(1));
                Assert.That(component.Instance.Display, Is.False);
                Assert.That(handler.SvcToAdd, Is.Empty);
            });
            await Task.CompletedTask;
        }

        [Test]
        public async Task EditConn_OnParametersSet_ClearsInterfacePermissionForNewInterface()
        {
            await using BunitContext context = CreateContext(out _, out SimulatedUserConfig userConfig);
            userConfig.ModNamingConvention = "{}";
            userConfig.ModAppServerTypes = "[]";
            ModellingConnection connection = new()
            {
                Name = "conn",
                Reason = "reason",
                IsInterface = true,
                InterfacePermission = "Private"
            };
            ModellingConnectionHandler handler = CreateConnectionHandler(
                new SimulatedApiConnection(),
                userConfig,
                connection,
                readOnly: false,
                isOwner: true,
                addMode: true);

            _ = RenderEditConn(context, handler, display: true);

            Assert.That(handler.ActConn.InterfacePermission, Is.Empty);
        }

        [Test]
        public async Task EditConn_OnParametersSet_DoesNotClearInterfacePermissionForExistingInterface()
        {
            await using BunitContext context = CreateContext(out _, out SimulatedUserConfig userConfig);
            userConfig.ModNamingConvention = "{}";
            userConfig.ModAppServerTypes = "[]";
            ModellingConnection connection = new()
            {
                Name = "conn",
                Reason = "reason",
                IsInterface = true,
                InterfacePermission = "Private"
            };
            ModellingConnectionHandler handler = CreateConnectionHandler(
                new SimulatedApiConnection(),
                userConfig,
                connection,
                readOnly: false,
                isOwner: true,
                addMode: false);

            _ = RenderEditConn(context, handler, display: true);

            Assert.That(handler.ActConn.InterfacePermission, Is.EqualTo("Private"));
        }

        [Test]
        public async Task EditConn_Save_UpdatesConnectionAndClosesWhenValid()
        {
            await using BunitContext context = CreateContext(out RecordingApiConnection apiConn, out SimulatedUserConfig userConfig);
            userConfig.ModNamingConvention = "{}";
            userConfig.ModAppServerTypes = "[]";
            ModellingConnection connection = new()
            {
                Id = 81,
                Name = "conn81",
                Reason = "reason81",
                SrcFromInterface = true,
                DstFromInterface = true,
                UsedInterfaceId = 17
            };
            ModellingConnectionHandler handler = CreateConnectionHandler(apiConn, userConfig, connection, readOnly: false);
            bool displayChanged = true;
            int closingCalls = 0;

            IRenderedComponent<EditConn> component = RenderEditConn(
                context,
                handler,
                display: true,
                displayChanged: value => displayChanged = value,
                closingAction: () =>
                {
                    closingCalls++;
                    return true;
                });

            Task<bool> saveTask = (Task<bool>)GetPrivateMethod(typeof(EditConn), "Save").Invoke(component.Instance, null)!;
            bool result = await saveTask;

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True);
                Assert.That(apiConn.UpdateConnectionCalls, Is.EqualTo(1));
                Assert.That(apiConn.HistoryEntryCalls, Is.EqualTo(1));
                Assert.That(displayChanged, Is.False);
                Assert.That(closingCalls, Is.EqualTo(1));
                Assert.That(component.Instance.Display, Is.False);
                Assert.That(handler.ActConn.UsedInterfaceId, Is.EqualTo(17));
            });
        }

        [Test]
        public async Task EditConn_Save_PublishesRequestedInterfaceAndPromotesTicket()
        {
            await using BunitContext context = CreateContext(out RecordingApiConnection apiConn, out SimulatedUserConfig userConfig);
            userConfig.ModNamingConvention = "{}";
            userConfig.ModAppServerTypes = "[]";
            userConfig.ReqPriorities = "[]";
            apiConn.ExtStates = [new WfExtState { Name = ExtStates.Done.ToString(), StateId = 249 }];
            ModellingConnection connection = new()
            {
                Id = 82,
                Name = "requested-if",
                Reason = "reason82",
                IsInterface = true,
                IsRequested = true,
                IsPublished = false,
                InterfacePermission = InterfacePermissions.Public.ToString(),
                TicketId = 555,
                ProposedAppId = 17,
                SourceAppServers = [new ModellingAppServerWrapper { Content = CreateServer(102, "srv102", "10.0.0.102/32") }],
                Services = [new ModellingServiceWrapper { Content = new ModellingService { Id = 202, Name = "svc202" } }]
            };
            ModellingConnectionHandler handler = CreateConnectionHandler(apiConn, userConfig, connection, readOnly: false);
            bool displayChanged = true;
            int closingCalls = 0;
            List<(string Title, string Message, bool Error)> messages = [];

            IRenderedComponent<EditConn> component = RenderEditConn(
                context,
                handler,
                display: true,
                displayChanged: value => displayChanged = value,
                closingAction: () =>
                {
                    closingCalls++;
                    return true;
                });
            SetPrivateProperty(component.Instance, "DisplayMessageInUi",
                new Action<Exception?, string, string, bool>((_, title, msg, error) => messages.Add((title, msg, error))));

            apiConn.TicketById = new WfTicket
            {
                Id = 555,
                StateId = 210,
                Requester = new UiUser { DbId = 3, Name = "Requester", Dn = "uid=requester,ou=people,dc=example,dc=com" },
                Tasks =
                [
                    new WfReqTask
                    {
                        Id = 901,
                        TicketId = 555,
                        StateId = 210,
                        TaskType = WfTaskType.new_interface.ToString(),
                        ImplementationTasks = [new WfImplTask
                        {
                            Id = 902,
                            TicketId = 555,
                            ReqTaskId = 901,
                            StateId = 210,
                            TaskType = WfTaskType.new_interface.ToString()
                        }]
                    }
                ]
            };

            Task<bool> saveTask = (Task<bool>)GetPrivateMethod(typeof(EditConn), "Save").Invoke(component.Instance, null)!;
            bool result = await saveTask;

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True);
                Assert.That(apiConn.UpdateConnectionCalls, Is.EqualTo(1));
                Assert.That(apiConn.UpdateImplTaskStateCalls, Is.GreaterThan(0));
                Assert.That(apiConn.UpdateRequestTaskStateCalls, Is.GreaterThan(0));
                Assert.That(apiConn.UpdateTicketStateCalls, Is.GreaterThan(0));
                Assert.That(messages, Has.Count.GreaterThanOrEqualTo(1));
                Assert.That(messages.Any(message => message.Title == userConfig.GetText("publish") && message.Message == userConfig.GetText("U9013") && !message.Error), Is.True);
                Assert.That(displayChanged, Is.False);
                Assert.That(closingCalls, Is.EqualTo(1));
                Assert.That(component.Instance.Display, Is.False);
                Assert.That(handler.ActConn.IsRequested, Is.False);
                Assert.That(handler.ActConn.IsPublished, Is.True);
                Assert.That(handler.ActConn.AppId, Is.EqualTo(17));
                Assert.That(handler.ActConn.ProposedAppId, Is.Null);
            });
        }

        [Test]
        public async Task EditConn_OnInitialized_LoadsExtraConfigsAndDefaultModules()
        {
            await using BunitContext context = CreateContext(out _, out SimulatedUserConfig userConfig);
            userConfig.ModNamingConvention = "{}";
            userConfig.ModExtraConfigs = JsonSerializer.Serialize(new List<string> { "cfg-a", "cfg-b" });
            userConfig.AvailableModules = string.Empty;
            userConfig.ModAppServerTypes = "[]";
            ModellingConnectionHandler handler = CreateConnectionHandler(
                new SimulatedApiConnection(),
                userConfig,
                new ModellingConnection { Name = "conn", Reason = "reason" },
                readOnly: false);

            IRenderedComponent<EditConn> component = RenderEditConn(context, handler, display: true);

            Assert.Multiple(() =>
            {
                Assert.That(GetPrivateField<List<string>>(component.Instance, "availableExtraConfigs"), Is.EquivalentTo(new List<string> { "cfg-a", "cfg-b" }));
                Assert.That(GetPrivateProperty<List<FWO.Basics.Module>>(component.Instance, "availableModules"), Does.Contain(FWO.Basics.Module.Workflow));
                Assert.That(GetPrivateField<int>(component.Instance, "sidebarLeftWidth"), Is.EqualTo(GlobalConst.kGlobLibraryWidth));
            });
            await Task.CompletedTask;
        }

        [Test]
        public async Task EditConn_Save_ReturnsFalseWhenNotOwner()
        {
            await using BunitContext context = CreateContext(out RecordingApiConnection apiConn, out SimulatedUserConfig userConfig);
            userConfig.ModNamingConvention = "{}";
            userConfig.ModAppServerTypes = "[]";
            ModellingConnection connection = new()
            {
                Id = 90,
                Name = "conn90",
                Reason = "reason90"
            };
            ModellingConnectionHandler handler = CreateConnectionHandler(apiConn, userConfig, connection, readOnly: false, isOwner: false);
            IRenderedComponent<EditConn> component = RenderEditConn(context, handler, display: true);

            Task<bool> saveTask = (Task<bool>)GetPrivateMethod(typeof(EditConn), "Save").Invoke(component.Instance, null)!;
            bool result = await saveTask;

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False);
                Assert.That(apiConn.UpdateConnectionCalls, Is.Zero);
                Assert.That(component.Instance.Display, Is.True);
            });
        }

        [Test]
        public async Task EditConn_Save_ReturnsFalseForInvalidConnection()
        {
            await using BunitContext context = CreateContext(out RecordingApiConnection apiConn, out SimulatedUserConfig userConfig);
            userConfig.ModNamingConvention = "{}";
            userConfig.ModAppServerTypes = "[]";
            ModellingConnection connection = new()
            {
                Id = 91,
                Name = "conn91",
                Reason = string.Empty
            };
            ModellingConnectionHandler handler = CreateConnectionHandler(apiConn, userConfig, connection, readOnly: false);
            List<(string Title, string Message, bool Error)> messages = [];
            SetPrivateProperty(handler, "DisplayMessageInUi",
                new Action<Exception?, string, string, bool>((_, title, message, error) => messages.Add((title, message, error))));

            IRenderedComponent<EditConn> component = RenderEditConn(context, handler, display: true);

            Task<bool> saveTask = (Task<bool>)GetPrivateMethod(typeof(EditConn), "Save").Invoke(component.Instance, null)!;
            bool result = await saveTask;

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False);
                Assert.That(apiConn.UpdateConnectionCalls, Is.Zero);
                Assert.That(messages.Any(message => message.Message == userConfig.GetText("E5102") && message.Error), Is.True);
                Assert.That(component.Instance.Display, Is.True);
            });
        }

        [Test]
        public async Task EditConn_Save_AddMode_AddsConnectionAndCloses()
        {
            await using BunitContext context = CreateContext(out RecordingApiConnection apiConn, out SimulatedUserConfig userConfig);
            userConfig.ModNamingConvention = "{}";
            userConfig.ModAppServerTypes = "[]";
            ModellingConnection connection = new()
            {
                Name = "new-conn",
                Reason = "new-reason",
                SourceAppRoles = [new ModellingAppRoleWrapper { Content = new ModellingAppRole { Id = 11, Name = "role11" } }],
                DestinationAppServers = [new ModellingAppServerWrapper { Content = CreateServer(12, "srv12", "10.0.0.12/32") }],
                Services = [new ModellingServiceWrapper { Content = new ModellingService { Id = 13, Name = "svc13", ProtoId = 6, Port = 443 } }]
            };
            ModellingConnectionHandler handler = CreateConnectionHandler(apiConn, userConfig, connection, readOnly: false, addMode: true);
            bool displayChanged = true;

            IRenderedComponent<EditConn> component = RenderEditConn(
                context,
                handler,
                display: true,
                displayChanged: value => displayChanged = value);

            Task<bool> saveTask = (Task<bool>)GetPrivateMethod(typeof(EditConn), "Save").Invoke(component.Instance, null)!;
            bool result = await saveTask;

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True);
                Assert.That(apiConn.UpdateConnectionCalls, Is.Zero);
                Assert.That(apiConn.NewConnectionCalls, Is.EqualTo(1));
                Assert.That(handler.ActConn.Id, Is.EqualTo(88));
                Assert.That(displayChanged, Is.False);
                Assert.That(component.Instance.Display, Is.False);
            });
        }

        [Test]
        public async Task EditConn_GotoTicket_NavigatesWhenNoUnsavedChanges()
        {
            await using BunitContext context = CreateContext(out _, out SimulatedUserConfig userConfig);
            userConfig.ModNamingConvention = "{}";
            userConfig.ModAppServerTypes = "[]";
            ModellingConnection connection = new()
            {
                Id = 92,
                Name = "conn92",
                Reason = "reason92",
                TicketId = 555
            };
            ModellingConnectionHandler handler = CreateConnectionHandler(new SimulatedApiConnection(), userConfig, connection, readOnly: false);
            IRenderedComponent<EditConn> component = RenderEditConn(context, handler, display: true);
            NavigationManager navigationManager = context.Services.GetRequiredService<NavigationManager>();

            GetPrivateMethod(typeof(EditConn), "GotoTicket").Invoke(component.Instance, null);

            Assert.That(navigationManager.Uri, Does.Contain("/request/tickets/555"));
            await Task.CompletedTask;
        }

        [Test]
        public async Task EditConn_DisplayTicket_LoadsWorkflowTicket()
        {
            await using BunitContext context = CreateContext(out RecordingApiConnection apiConn, out SimulatedUserConfig userConfig);
            userConfig.ModNamingConvention = "{}";
            userConfig.ModAppServerTypes = "[]";
            ModellingConnection connection = new()
            {
                Id = 93,
                Name = "conn93",
                Reason = "reason93",
                TicketId = 777
            };
            WfReqTask reqTask = new()
            {
                Id = 701,
                TicketId = 777,
                TaskType = WfTaskType.new_interface.ToString(),
                StateId = 49,
                ImplementationTasks = [new WfImplTask { Id = 702, TicketId = 777, ReqTaskId = 701, StateId = 49, TaskType = WfTaskType.new_interface.ToString() }]
            };
            apiConn.TicketById = new WfTicket
            {
                Id = 777,
                Requester = new UiUser { DbId = 1, Name = "Requester" },
                Tasks = [reqTask]
            };
            ModellingConnectionHandler handler = CreateConnectionHandler(apiConn, userConfig, connection, readOnly: false);
            IRenderedComponent<EditConn> component = RenderEditConn(context, handler, display: true);

            await component.InvokeAsync(() => (Task)GetPrivateMethod(typeof(EditConn), "DisplayTicket").Invoke(component.Instance, null)!);

            Assert.Multiple(() =>
            {
                Assert.That(apiConn.GetTicketByIdCalls, Is.EqualTo(1));
                Assert.That(GetPrivateField<bool>(component.Instance, "workInProgress"), Is.False);
                Assert.That(GetPrivateField<WfHandler>(component.Instance, "wfHandler").ReadOnlyMode, Is.True);
                Assert.That(GetPrivateField<WfHandler>(component.Instance, "wfHandler").ActReqTask.Id, Is.EqualTo(reqTask.Id));
            });
        }

        [Test]
        public async Task EditConn_ShowsLogDataWhenEnabled()
        {
            await using BunitContext context = CreateContext(out _, out SimulatedUserConfig userConfig);
            userConfig.ModNamingConvention = "{}";
            userConfig.ModAppServerTypes = "[]";
            userConfig.ShowLogDataInConnections = true;
            ModellingConnectionHandler handler = CreateConnectionHandler(
                new SimulatedApiConnection(),
                userConfig,
                new ModellingConnection { Name = "conn", Reason = "reason" },
                readOnly: false);

            IRenderedComponent<EditConn> component = RenderEditConn(context, handler, display: true);

            Assert.That(component.FindComponents<LogDataTable>(), Has.Count.EqualTo(1));
            await Task.CompletedTask;
        }

        [Test]
        public async Task EditConn_HidesLogDataWhenDisabled()
        {
            await using BunitContext context = CreateContext(out _, out SimulatedUserConfig userConfig);
            userConfig.ModNamingConvention = "{}";
            userConfig.ModAppServerTypes = "[]";
            userConfig.ShowLogDataInConnections = false;
            ModellingConnectionHandler handler = CreateConnectionHandler(
                new SimulatedApiConnection(),
                userConfig,
                new ModellingConnection { Name = "conn", Reason = "reason" },
                readOnly: false);

            IRenderedComponent<EditConn> component = RenderEditConn(context, handler, display: true);

            Assert.That(component.FindComponents<LogDataTable>(), Is.Empty);
            await Task.CompletedTask;
        }
    }
}
