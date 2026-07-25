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
    internal class UiEditNetworkModellingComponentsTest
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

        [SetUp]
        public void SetUp()
        {
            SimulatedUserConfig.DummyTranslate["connection"] = "Connection";
            SimulatedUserConfig.DummyTranslate["common_service"] = "Common service";
            SimulatedUserConfig.DummyTranslate["requested_interface"] = "Requested interface";
            SimulatedUserConfig.DummyTranslate["interface"] = "Interface";
            SimulatedUserConfig.DummyTranslate["add_app_role"] = "Add app role";
            SimulatedUserConfig.DummyTranslate["edit_app_role"] = "Edit app role";
            SimulatedUserConfig.DummyTranslate["add_app_server"] = "Add app server";
            SimulatedUserConfig.DummyTranslate["edit_app_server"] = "Edit app server";
            SimulatedUserConfig.DummyTranslate["U9005"] = "Reactivate ";
            SimulatedUserConfig.DummyTranslate["U9007"] = "Cannot delete ";
            SimulatedUserConfig.DummyTranslate["U9008"] = "Delete ";
            SimulatedUserConfig.DummyTranslate["app_role"] = "App role";
            SimulatedUserConfig.DummyTranslate["app_server"] = "App server";
            SimulatedUserConfig.DummyTranslate["area"] = "Area";
            SimulatedUserConfig.DummyTranslate["library"] = "Library";
            SimulatedUserConfig.DummyTranslate["to_app_role"] = "To app role";
            SimulatedUserConfig.DummyTranslate["fetch_data"] = "Fetch data";
            SimulatedUserConfig.DummyTranslate["save_app_server"] = "Save app server";
            SimulatedUserConfig.DummyTranslate["save_app_role"] = "Save app role";
            SimulatedUserConfig.DummyTranslate["wrong_ip_address"] = "wrong_ip_address";
            SimulatedUserConfig.DummyTranslate["E9002"] = "E9002";
            SimulatedUserConfig.DummyTranslate["E5102"] = "E5102";
            SimulatedUserConfig.DummyTranslate["U0001"] = "U0001";
            SimulatedUserConfig.DummyTranslate["U9015"] = "Replace interface";
            SimulatedUserConfig.DummyTranslate["save"] = "Save";
            SimulatedUserConfig.DummyTranslate["cancel"] = "Cancel";
            SimulatedUserConfig.DummyTranslate["ok"] = "OK";
            SimulatedUserConfig.DummyTranslate["confirm"] = "Confirm";
            SimulatedUserConfig.DummyTranslate["owner"] = "Owner";
            SimulatedUserConfig.DummyTranslate["predef_services"] = "Predefined services";
            SimulatedUserConfig.DummyTranslate["actions"] = "Actions";
            SimulatedUserConfig.DummyTranslate["services_group"] = "Services group";
            SimulatedUserConfig.DummyTranslate["services"] = "Services";
            SimulatedUserConfig.DummyTranslate["comment"] = "Comment";
            SimulatedUserConfig.DummyTranslate["add_service_group"] = "Add service group";
            SimulatedUserConfig.DummyTranslate["edit_service_group"] = "Edit service group";
            SimulatedUserConfig.DummyTranslate["delete_service_group"] = "Delete service group";
            SimulatedUserConfig.DummyTranslate["U9004"] = "Delete ";
            SimulatedUserConfig.DummyTranslate["E9008"] = "Cannot delete ";
            SimulatedUserConfig.DummyTranslate["is_in_use"] = "is_in_use";
            SimulatedUserConfig.DummyTranslate["import_source"] = "import_source";
            SimulatedUserConfig.DummyTranslate["add"] = "Add";
            SimulatedUserConfig.DummyTranslate["show_history"] = "Show history";
            SimulatedUserConfig.DummyTranslate["show_all"] = "Show all";
            SimulatedUserConfig.DummyTranslate["fetch_limit"] = "Fetch limit";
            SimulatedUserConfig.DummyTranslate["application"] = "Application";
            SimulatedUserConfig.DummyTranslate["change_type"] = "Change type";
            SimulatedUserConfig.DummyTranslate["object_type"] = "Object type";
            SimulatedUserConfig.DummyTranslate["object_id"] = "Object id";
            SimulatedUserConfig.DummyTranslate["text"] = "Text";
            SimulatedUserConfig.DummyTranslate["changed_by"] = "Changed by";
            SimulatedUserConfig.DummyTranslate["change_source"] = "Change source";
            SimulatedUserConfig.DummyTranslate["share_link"] = "Share link";
            SimulatedUserConfig.DummyTranslate["copy_to_clipboard"] = "Copy";
            SimulatedUserConfig.DummyTranslate["search_nw_object"] = "Search network object";
            SimulatedUserConfig.DummyTranslate["search_interface"] = "Search interface";
            SimulatedUserConfig.DummyTranslate["using_connections"] = "Using connections";
            SimulatedUserConfig.DummyTranslate["extra_params"] = "Extra parameters";
            SimulatedUserConfig.DummyTranslate["decomm_interface"] = "Decommission interface";
            SimulatedUserConfig.DummyTranslate["decommission"] = "Decommission";
            SimulatedUserConfig.DummyTranslate["reason"] = "Reason";
            SimulatedUserConfig.DummyTranslate["propose_alternative"] = "Propose alternative";
            SimulatedUserConfig.DummyTranslate["U9035"] = "Do you want to decommission";
            SimulatedUserConfig.DummyTranslate["U9032"] = "Please provide a reason.";
            SimulatedUserConfig.DummyTranslate["objects"] = "Objects";
            SimulatedUserConfig.DummyTranslate["created_by"] = "Created by";
            SimulatedUserConfig.DummyTranslate["creation_date"] = "Creation date";
            SimulatedUserConfig.DummyTranslate["generate_name"] = "Generate name";
            SimulatedUserConfig.DummyTranslate["ip"] = "IP";
            SimulatedUserConfig.DummyTranslate["type"] = "Type";
            SimulatedUserConfig.DummyTranslate["name"] = "Name";
        }

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
        public async Task EditConnLeftSide_HandleNwDragStart_PrimesContainerWithAppRole()
        {
            await using BunitContext context = CreateContext(out _, out SimulatedUserConfig userConfig);
            userConfig.ModNamingConvention = "{}";
            userConfig.ModAppServerTypes = "[]";
            ModellingAppRole appRole = new() { Id = 11, Name = "role11" };
            ModellingConnectionHandler handler = CreateConnectionHandler(
                new SimulatedApiConnection(),
                userConfig,
                new ModellingConnection { Name = "conn", Reason = "reason" },
                readOnly: false);
            handler.AvailableAppRoles = [appRole];

            ModellingDnDContainer container = new();
            IRenderedComponent<EditConnLeftSide> component = RenderEditConnLeftSide(context, container);
            SetComponentProperty(component.Instance, nameof(EditConnLeftSide.ConnHandler), handler);

            bool handled = (bool)GetPrivateMethod(typeof(EditConnLeftSide), "HandleNwDragStart")
                .Invoke(component.Instance, [new DragEventArgs(), new KeyValuePair<int, long>((int)ModellingTypes.ModObjectType.AppRole, appRole.Id)])!;

            Assert.Multiple(() =>
            {
                Assert.That(handled, Is.True);
                Assert.That(container.AppRoleElements, Has.Count.EqualTo(1));
                Assert.That(container.AppRoleElements[0].Id, Is.EqualTo(appRole.Id));
                Assert.That(GetPrivateField<List<KeyValuePair<int, long>>>(component.Instance, "selectedNwElems"), Is.Empty);
            });
            await Task.CompletedTask;
        }

        [Test]
        public async Task EditConnLeftSide_HandleNwDragStart_PrimesContainerWithAppServer()
        {
            await using BunitContext context = CreateContext(out _, out SimulatedUserConfig userConfig);
            userConfig.ModNamingConvention = "{}";
            userConfig.ModAppServerTypes = "[]";
            ModellingAppServer appServer = CreateServer(12, "srv12", "10.0.0.12/32");
            ModellingConnectionHandler handler = CreateConnectionHandler(
                new SimulatedApiConnection(),
                userConfig,
                new ModellingConnection { Name = "conn", Reason = "reason" },
                readOnly: false);
            handler.AvailableAppServers = [appServer];

            ModellingDnDContainer container = new();
            IRenderedComponent<EditConnLeftSide> component = RenderEditConnLeftSide(context, container);
            SetComponentProperty(component.Instance, nameof(EditConnLeftSide.ConnHandler), handler);

            bool handled = (bool)GetPrivateMethod(typeof(EditConnLeftSide), "HandleNwDragStart")
                .Invoke(component.Instance, [new DragEventArgs(), new KeyValuePair<int, long>((int)ModellingTypes.ModObjectType.AppServer, appServer.Id)])!;

            Assert.Multiple(() =>
            {
                Assert.That(handled, Is.True);
                Assert.That(container.AppServerElements, Has.Count.EqualTo(1));
                Assert.That(container.AppServerElements[0].Id, Is.EqualTo(appServer.Id));
                Assert.That(GetPrivateField<List<KeyValuePair<int, long>>>(component.Instance, "selectedNwElems"), Is.Empty);
            });
            await Task.CompletedTask;
        }

        [Test]
        public async Task EditConnLeftSide_SearchMethods_SetTheirFlags()
        {
            await using BunitContext context = CreateContext(out _, out SimulatedUserConfig userConfig);
            userConfig.ModNamingConvention = "{}";
            userConfig.ModAppServerTypes = "[]";
            ModellingConnectionHandler handler = CreateConnectionHandler(
                new SimulatedApiConnection(),
                userConfig,
                new ModellingConnection { Name = "conn", Reason = "reason" },
                readOnly: false);

            IRenderedComponent<EditConnLeftSide> component = RenderEditConnLeftSide(context);
            SetComponentProperty(component.Instance, nameof(EditConnLeftSide.ConnHandler), handler);

            GetPrivateMethod(typeof(EditConnLeftSide), "SearchInterface").Invoke(component.Instance, null);
            GetPrivateMethod(typeof(EditConnLeftSide), "SearchNwObject").Invoke(component.Instance, null);

            Assert.Multiple(() =>
            {
                Assert.That(GetPrivateField<bool>(component.Instance, "SearchInterfaceMode"), Is.True);
                Assert.That(GetPrivateField<bool>(component.Instance, "SearchNwObjectMode"), Is.True);
            });
            await Task.CompletedTask;
        }

        [Test]
        public async Task EditConnLeftSide_RequestNewInterface_SetsSelectAppMode()
        {
            await using BunitContext context = CreateContext(out _, out SimulatedUserConfig userConfig);
            userConfig.ModNamingConvention = "{}";
            userConfig.ModAppServerTypes = "[]";
            ModellingConnectionHandler handler = CreateConnectionHandler(
                new SimulatedApiConnection(),
                userConfig,
                new ModellingConnection { Name = "conn", Reason = "reason" },
                readOnly: false);

            IRenderedComponent<EditConnLeftSide> component = RenderEditConnLeftSide(context);
            SetComponentProperty(component.Instance, nameof(EditConnLeftSide.ConnHandler), handler);

            component.Instance.RequestNewInterface();

            Assert.That(GetPrivateField<bool>(component.Instance, "SelectAppMode"), Is.True);
            await Task.CompletedTask;
        }

        [Test]
        public async Task EditConnLeftSide_HandleSvcDragStart_PrimesContainerWithServiceGroup()
        {
            await using BunitContext context = CreateContext(out _, out SimulatedUserConfig userConfig);
            userConfig.ModNamingConvention = "{}";
            userConfig.ModAppServerTypes = "[]";
            ModellingServiceGroup serviceGroup = new() { Id = 21, Name = "svcgrp21" };
            ModellingConnectionHandler handler = CreateConnectionHandler(
                new SimulatedApiConnection(),
                userConfig,
                new ModellingConnection { Name = "conn", Reason = "reason" },
                readOnly: false);
            handler.AvailableServiceGroups = [serviceGroup];

            ModellingDnDContainer container = new();
            IRenderedComponent<EditConnLeftSide> component = RenderEditConnLeftSide(context, container);
            SetComponentProperty(component.Instance, nameof(EditConnLeftSide.ConnHandler), handler);

            bool handled = (bool)GetPrivateMethod(typeof(EditConnLeftSide), "HandleSvcDragStart")
                .Invoke(component.Instance, [new DragEventArgs(), new KeyValuePair<int, int>((int)ModellingTypes.ModObjectType.ServiceGroup, serviceGroup.Id)])!;

            Assert.Multiple(() =>
            {
                Assert.That(handled, Is.True);
                Assert.That(container.SvcGrpElements, Has.Count.EqualTo(1));
                Assert.That(container.SvcGrpElements[0].Id, Is.EqualTo(serviceGroup.Id));
                Assert.That(GetPrivateField<List<KeyValuePair<int, int>>>(component.Instance, "selectedSvcElems"), Is.Empty);
            });
            await Task.CompletedTask;
        }

        [Test]
        public async Task EditConnLeftSide_HandleSvcDragStart_PrimesContainerWithService()
        {
            await using BunitContext context = CreateContext(out _, out SimulatedUserConfig userConfig);
            userConfig.ModNamingConvention = "{}";
            userConfig.ModAppServerTypes = "[]";
            ModellingService service = new() { Id = 22, Name = "svc22" };
            ModellingConnectionHandler handler = CreateConnectionHandler(
                new SimulatedApiConnection(),
                userConfig,
                new ModellingConnection { Name = "conn", Reason = "reason" },
                readOnly: false);
            handler.AvailableServices = [service];

            ModellingDnDContainer container = new();
            IRenderedComponent<EditConnLeftSide> component = RenderEditConnLeftSide(context, container);
            SetComponentProperty(component.Instance, nameof(EditConnLeftSide.ConnHandler), handler);

            bool handled = (bool)GetPrivateMethod(typeof(EditConnLeftSide), "HandleSvcDragStart")
                .Invoke(component.Instance, [new DragEventArgs(), new KeyValuePair<int, int>((int)ModellingTypes.ModObjectType.Service, service.Id)])!;

            Assert.Multiple(() =>
            {
                Assert.That(handled, Is.True);
                Assert.That(container.SvcElements, Has.Count.EqualTo(1));
                Assert.That(container.SvcElements[0].Id, Is.EqualTo(service.Id));
                Assert.That(GetPrivateField<List<KeyValuePair<int, int>>>(component.Instance, "selectedSvcElems"), Is.Empty);
            });
            await Task.CompletedTask;
        }

        [Test]
        public async Task EditConnLeftSide_HandleConnDragStart_PrimesConnectionContainer()
        {
            await using BunitContext context = CreateContext(out _, out SimulatedUserConfig userConfig);
            userConfig.ModNamingConvention = "{}";
            userConfig.ModAppServerTypes = "[]";
            ModellingConnection selectedConn = new() { Id = 44, Name = "interf" };
            ModellingConnectionHandler handler = CreateConnectionHandler(
                new SimulatedApiConnection(),
                userConfig,
                new ModellingConnection { Name = "conn", Reason = "reason" },
                readOnly: false);

            ModellingDnDContainer container = new();
            IRenderedComponent<EditConnLeftSide> component = RenderEditConnLeftSide(context, container);
            SetComponentProperty(component.Instance, nameof(EditConnLeftSide.ConnHandler), handler);

            bool handled = (bool)GetPrivateMethod(typeof(EditConnLeftSide), "HandleConnDragStart")
                .Invoke(component.Instance, [new DragEventArgs(), selectedConn])!;

            Assert.Multiple(() =>
            {
                Assert.That(handled, Is.True);
                Assert.That(container.ConnElement, Is.EqualTo(selectedConn));
                Assert.That(GetPrivateField<List<ModellingConnection>>(component.Instance, "selectedInterfaces"), Is.Empty);
            });
            await Task.CompletedTask;
        }

        [Test]
        public async Task EditConnLeftSide_HandleNwDragStart_PrimesContainerWithNetworkArea()
        {
            await using BunitContext context = CreateContext(out _, out SimulatedUserConfig userConfig);
            userConfig.ModNamingConvention = "{}";
            userConfig.ModAppServerTypes = "[]";
            ModellingNetworkArea area = CreateArea(15, "NA15", "area15", "10.0.0.15", "10.0.0.15");
            ModellingConnectionHandler handler = CreateConnectionHandler(
                new SimulatedApiConnection(),
                userConfig,
                new ModellingConnection { Name = "conn", Reason = "reason" },
                readOnly: false);
            handler.AvailableCommonAreas = [new ModellingNetworkAreaWrapper { Content = area }];

            ModellingDnDContainer container = new();
            IRenderedComponent<EditConnLeftSide> component = RenderEditConnLeftSide(context, container);
            SetComponentProperty(component.Instance, nameof(EditConnLeftSide.ConnHandler), handler);

            bool handled = (bool)GetPrivateMethod(typeof(EditConnLeftSide), "HandleNwDragStart")
                .Invoke(component.Instance, [new DragEventArgs(), new KeyValuePair<int, long>((int)ModellingTypes.ModObjectType.NetworkArea, area.Id)])!;

            Assert.Multiple(() =>
            {
                Assert.That(handled, Is.True);
                Assert.That(container.AreaElements, Has.Count.EqualTo(1));
                Assert.That(container.AreaElements[0].Id, Is.EqualTo(area.Id));
                Assert.That(GetPrivateField<List<KeyValuePair<int, long>>>(component.Instance, "selectedNwElems"), Is.Empty);
            });
            await Task.CompletedTask;
        }

        [Test]
        public async Task EditConnLeftSide_HandleNwDragStart_PrimesContainerWithNwGroup()
        {
            await using BunitContext context = CreateContext(out _, out SimulatedUserConfig userConfig);
            userConfig.ModNamingConvention = "{}";
            userConfig.ModAppServerTypes = "[]";
            ModellingNwGroup nwGroup = new() { Id = 16, Name = "group16", IdString = "NA16" };
            ModellingConnectionHandler handler = CreateConnectionHandler(
                new SimulatedApiConnection(),
                userConfig,
                new ModellingConnection { Name = "conn", Reason = "reason" },
                readOnly: false);
            handler.AvailableSelectedObjects = [new ModellingNwGroupWrapper { Content = nwGroup }];

            ModellingDnDContainer container = new();
            IRenderedComponent<EditConnLeftSide> component = RenderEditConnLeftSide(context, container);
            SetComponentProperty(component.Instance, nameof(EditConnLeftSide.ConnHandler), handler);

            bool handled = (bool)GetPrivateMethod(typeof(EditConnLeftSide), "HandleNwDragStart")
                .Invoke(component.Instance, [new DragEventArgs(), new KeyValuePair<int, long>((int)ModellingTypes.ModObjectType.AppZone, nwGroup.Id)])!;

            Assert.Multiple(() =>
            {
                Assert.That(handled, Is.True);
                Assert.That(container.NwGroupElements, Has.Count.EqualTo(1));
                Assert.That(container.NwGroupElements[0].Id, Is.EqualTo(nwGroup.Id));
                Assert.That(GetPrivateField<List<KeyValuePair<int, long>>>(component.Instance, "selectedNwElems"), Is.Empty);
            });
            await Task.CompletedTask;
        }

        [Test]
        public async Task EditConnLeftSide_LoadNwElements_CopiesAvailableObjectsFromHandler()
        {
            await using BunitContext context = CreateContext(out _, out SimulatedUserConfig userConfig);
            userConfig.ModNamingConvention = "{}";
            userConfig.ModAppServerTypes = "[]";
            ModellingConnectionHandler handler = CreateConnectionHandler(
                new SimulatedApiConnection(),
                userConfig,
                new ModellingConnection { Name = "conn", Reason = "reason" },
                readOnly: false);
            handler.AvailableNwElems =
            [
                new KeyValuePair<int, long>((int)ModellingTypes.ModObjectType.NetworkArea, 41),
                new KeyValuePair<int, long>((int)ModellingTypes.ModObjectType.AppServer, 42)
            ];

            IRenderedComponent<EditConnLeftSide> component = RenderEditConnLeftSide(context);
            SetComponentProperty(component.Instance, nameof(EditConnLeftSide.ConnHandler), handler);

            Task loadTask = (Task)GetPrivateMethod(typeof(EditConnLeftSide), "LoadNwElements")
                .Invoke(component.Instance, [false])!;
            await loadTask;

            Assert.That(GetPrivateField<List<KeyValuePair<int, long>>>(component.Instance, "AvailableNwElements"),
                Is.EquivalentTo(handler.AvailableNwElems));
        }

        [Test]
        public async Task EditConnLeftSide_InterfaceToConn_BlocksWhenAreasAlreadyExist()
        {
            await using BunitContext context = CreateContext(out _, out SimulatedUserConfig userConfig);
            userConfig.ModNamingConvention = "{}";
            userConfig.ModAppServerTypes = "[]";
            List<(string Title, string Message, bool Error)> messages = [];
            ModellingConnection connection = new()
            {
                Name = "conn",
                Reason = "reason",
                SourceAreas = [new ModellingNetworkAreaWrapper { Content = new ModellingNetworkArea { Id = 1, Name = "src", IdString = "NA1" } }]
            };
            ModellingConnectionHandler handler = CreateConnectionHandler(
                new SimulatedApiConnection(),
                userConfig,
                connection,
                readOnly: false);

            ModellingConnection interf = new()
            {
                Id = 7,
                Name = "iface",
                AppId = 99
            };

            IRenderedComponent<EditConnLeftSide> component = RenderEditConnLeftSide(context);
            SetComponentProperty(component.Instance, nameof(EditConnLeftSide.ConnHandler), handler);
            SetPrivateProperty(component.Instance, "DisplayMessageInUi",
                new Action<Exception?, string, string, bool>((_, title, msg, error) => messages.Add((title, msg, error))));

            bool result = (bool)GetPrivateMethod(typeof(EditConnLeftSide), "InterfaceToConn")
                .Invoke(component.Instance, [interf])!;

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False);
                Assert.That(messages, Has.Count.EqualTo(1));
                Assert.That(messages[0].Message, Is.EqualTo(userConfig.GetText("U9024")));
            });
            await Task.CompletedTask;
        }

        [Test]
        public async Task EditConnLeftSide_InterfaceToConn_AllowsCompatibleInterface()
        {
            await using BunitContext context = CreateContext(out _, out SimulatedUserConfig userConfig);
            userConfig.ModNamingConvention = "{}";
            userConfig.ModAppServerTypes = "[]";
            List<(string Title, string Message, bool Error)> messages = [];
            int handlerChangedCalls = 0;
            ModellingConnectionHandler handler = CreateConnectionHandler(
                new SimulatedApiConnection(),
                userConfig,
                new ModellingConnection
                {
                    Name = "conn",
                    Reason = "reason"
                },
                readOnly: false);

            ModellingConnection interf = new()
            {
                Id = 8,
                Name = "iface",
                AppId = handler.Application.Id
            };

            IRenderedComponent<EditConnLeftSide> component = RenderEditConnLeftSide(
                context,
                handlerChanged: _ => handlerChangedCalls++);
            SetComponentProperty(component.Instance, nameof(EditConnLeftSide.ConnHandler), handler);
            SetPrivateProperty(component.Instance, "DisplayMessageInUi",
                new Action<Exception?, string, string, bool>((_, title, msg, error) => messages.Add((title, msg, error))));

            bool result = (bool)GetPrivateMethod(typeof(EditConnLeftSide), "InterfaceToConn")
                .Invoke(component.Instance, [interf])!;

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True);
                Assert.That(messages, Is.Empty);
                Assert.That(handlerChangedCalls, Is.EqualTo(1));
                Assert.That(handler.InterfaceName, Is.EqualTo(interf.Name));
                Assert.That(handler.ActConn.UsedInterfaceId, Is.EqualTo(interf.Id));
                Assert.That(handler.ActConn.DstFromInterface, Is.True);
            });
            await Task.CompletedTask;
        }

        [Test]
        public async Task EditConnLeftSide_NetworkElemsToConn_AddsAppRoleAndServerToSourceAndClearsSelection()
        {
            await using BunitContext context = CreateContext(out _, out SimulatedUserConfig userConfig);
            userConfig.ModNamingConvention = "{}";
            userConfig.ModAppServerTypes = "[]";
            ModellingAppRole appRole = new() { Id = 31, Name = "role31" };
            ModellingAppServer appServer = CreateServer(33, "srv33", "10.0.0.33/32");
            ModellingConnectionHandler handler = CreateConnectionHandler(
                new SimulatedApiConnection(),
                userConfig,
                new ModellingConnection { Name = "conn", Reason = "reason" },
                readOnly: false);
            handler.AvailableAppRoles = [appRole];
            handler.AvailableAppServers = [appServer];

            IRenderedComponent<EditConnLeftSide> component = RenderEditConnLeftSide(context);
            SetComponentProperty(component.Instance, nameof(EditConnLeftSide.ConnHandler), handler);
            SetPrivateField(component.Instance, "selectedNwElems", new List<KeyValuePair<int, long>>
            {
                new((int)ModellingTypes.ModObjectType.AppRole, appRole.Id),
                new((int)ModellingTypes.ModObjectType.AppServer, appServer.Id)
            });

            GetPrivateMethod(typeof(EditConnLeftSide), "NetworkElemsToConn")
                .Invoke(component.Instance, [true]);

            Assert.Multiple(() =>
            {
                Assert.That(handler.SrcAppRolesToAdd.Select(item => item.Id), Is.EquivalentTo(new List<long> { appRole.Id }));
                Assert.That(handler.SrcAppServerToAdd.Select(item => item.Id), Is.EquivalentTo(new List<long> { appServer.Id }));
                Assert.That(GetPrivateField<List<KeyValuePair<int, long>>>(component.Instance, "selectedNwElems"), Is.Empty);
            });
            await Task.CompletedTask;
        }

        [Test]
        public async Task EditConnLeftSide_NetworkElemsToConn_AddsAreasAndGroupsWhenCommonAreaConfigured()
        {
            await using BunitContext context = CreateContext(out _, out SimulatedUserConfig userConfig);
            userConfig.ModNamingConvention = "{}";
            userConfig.ModAppServerTypes = "[]";
            ModellingNwGroup nwGroup = new() { Id = 32, Name = "nwgrp32" };
            ModellingNetworkArea area = new(nwGroup) { Id = nwGroup.Id, Name = "area32" };
            ModellingConnectionHandler handler = CreateConnectionHandler(
                new SimulatedApiConnection(),
                userConfig,
                new ModellingConnection { Name = "conn", Reason = "reason" },
                readOnly: false);
            handler.CommonAreaConfigItems = [new CommonAreaConfig { AreaId = area.Id, UseInSrc = true, UseInDst = true }];
            handler.AvailableSelectedObjects = [new ModellingNwGroupWrapper { Content = nwGroup }];

            IRenderedComponent<EditConnLeftSide> component = RenderEditConnLeftSide(context);
            SetComponentProperty(component.Instance, nameof(EditConnLeftSide.ConnHandler), handler);
            SetPrivateField(component.Instance, "selectedNwElems", new List<KeyValuePair<int, long>>
            {
                new((int)ModellingTypes.ModObjectType.AppZone, nwGroup.Id),
                new((int)ModellingTypes.ModObjectType.NetworkArea, area.Id)
            });

            GetPrivateMethod(typeof(EditConnLeftSide), "NetworkElemsToConn")
                .Invoke(component.Instance, [true]);

            Assert.Multiple(() =>
            {
                Assert.That(handler.SrcNwGroupsToAdd.Select(item => item.Id), Is.EquivalentTo(new List<long> { nwGroup.Id }));
                Assert.That(handler.SrcAreasToAdd.Select(item => item.Id), Is.EquivalentTo(new List<long> { area.Id }));
                Assert.That(GetPrivateField<List<KeyValuePair<int, long>>>(component.Instance, "selectedNwElems"), Is.Empty);
            });
            await Task.CompletedTask;
        }

        [Test]
        public async Task EditConnLeftSide_OverviewMode_PersistsCollapsedWidthAndLastWidth()
        {
            await using BunitContext context = CreateContext(out _, out SimulatedUserConfig userConfig);
            userConfig.ModNamingConvention = "{}";
            userConfig.ModAppServerTypes = "[]";
            ModellingConnectionHandler handler = CreateConnectionHandler(
                new SimulatedApiConnection(),
                userConfig,
                new ModellingConnection { Name = "conn", Reason = "reason" },
                readOnly: false);

            IRenderedComponent<EditConnLeftSide> component = RenderEditConnLeftSide(context);
            SetComponentProperty(component.Instance, nameof(EditConnLeftSide.ConnHandler), handler);
            SetComponentProperty(component.Instance, nameof(EditConnLeftSide.OverviewMode), true);

            PropertyInfo widthProperty = component.Instance.GetType().GetProperty("sidebarLeftWidth", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMemberException(component.Instance.GetType().FullName, "sidebarLeftWidth");

            widthProperty.SetValue(component.Instance, 0);
            Assert.That(handler.LastCollapsed, Is.True);

            widthProperty.SetValue(component.Instance, 214);
            Assert.Multiple(() =>
            {
                Assert.That(handler.LastCollapsed, Is.False);
                Assert.That(handler.LastWidth, Is.EqualTo(214));
            });
            await Task.CompletedTask;
        }

        [Test]
        public async Task EditConnPopup_Close_ClosesPopupWithoutReplace()
        {
            await using BunitContext context = CreateContext(out _, out _);
            bool displayChanged = true;

            IRenderedComponent<EditConnPopup> component = RenderEditConnPopup(
                context,
                display: true,
                replaceMode: false,
                displayChanged: value => displayChanged = value);

            GetPrivateMethod(typeof(EditConnPopup), "Close").Invoke(component.Instance, null);

            Assert.Multiple(() =>
            {
                Assert.That(displayChanged, Is.False);
                Assert.That(component.Instance.Display, Is.False);
            });
            await Task.CompletedTask;
        }

        [Test]
        public async Task EditConnPopup_Save_InvokesReplaceAndCloses()
        {
            await using BunitContext context = CreateContext(out _, out _);
            int replaceCalls = 0;
            bool displayChanged = true;

            IRenderedComponent<EditConnPopup> component = RenderEditConnPopup(
                context,
                display: false,
                replaceMode: true,
                replace: () =>
                {
                    replaceCalls++;
                    return Task.CompletedTask;
                },
                displayChanged: value => displayChanged = value);

            Task saveTask = (Task)GetPrivateMethod(typeof(EditConnPopup), "Save").Invoke(component.Instance, null)!;
            saveTask.GetAwaiter().GetResult();

            Assert.Multiple(() =>
            {
                Assert.That(replaceCalls, Is.EqualTo(1));
                Assert.That(displayChanged, Is.False);
                Assert.That(component.Instance.Display, Is.False);
            });
            await Task.CompletedTask;
        }

        [Test]
        public async Task EditServiceGroup_Save_AddsServiceGroupAndServices()
        {
            await using BunitContext context = CreateContext(out RecordingApiConnection apiConn, out SimulatedUserConfig userConfig);
            ModellingService service = new() { Id = 31, Name = "svc31" };
            List<ModellingService> availableServices = [service];
            List<KeyValuePair<int, int>> availableSvcElems = [];
            ModellingServiceGroup group = new()
            {
                Name = "grp31",
                Comment = "comment",
                IsGlobal = false
            };
            ModellingServiceGroupHandler handler = CreateServiceGroupHandler(
                apiConn,
                userConfig,
                group,
                availableServices,
                availableSvcElems,
                addMode: true);
            handler.SvcToAdd.Add(service);
            bool displayChanged = true;
            int handlerChangedCalls = 0;

            IRenderedComponent<EditServiceGroup> component = RenderEditServiceGroup(context, handler, true,
                displayChanged: value => displayChanged = value,
                handlerChanged: _ => handlerChangedCalls++);

            Task saveTask = (Task)GetPrivateMethod(typeof(EditServiceGroup), "Save").Invoke(component.Instance, null)!;
            await saveTask;

            Assert.Multiple(() =>
            {
                Assert.That(handlerChangedCalls, Is.EqualTo(1));
                Assert.That(displayChanged, Is.False);
                Assert.That(group.Id, Is.EqualTo(77));
                    Assert.That(group.Services.Select(item => item.Content.Id), Is.EquivalentTo(new List<long> { service.Id }));
                Assert.That(availableSvcElems, Has.Count.EqualTo(1));
                Assert.That(apiConn.NewServiceGroupCalls, Is.EqualTo(1));
                Assert.That(apiConn.AddServiceToServiceGroupCalls, Is.EqualTo(1));
                Assert.That(apiConn.HistoryEntryCalls, Is.EqualTo(2));
            });
        }

        [Test]
        public async Task EditServiceGroup_HandleSvcDrop_AddsSelectedServicesAndClearsContainer()
        {
            await using BunitContext context = CreateContext(out _, out SimulatedUserConfig userConfig);
            ModellingService service = new() { Id = 32, Name = "svc32" };
            ModellingServiceGroupHandler handler = CreateServiceGroupHandler(
                new SimulatedApiConnection(),
                userConfig,
                new ModellingServiceGroup { Name = "grp32" },
                [service],
                [],
                addMode: false);

            ModellingDnDContainer container = new();
            container.SvcElements.Add(service);

            IRenderedComponent<EditServiceGroup> component = RenderEditServiceGroup(context, handler);
            ModellingDnDContainer componentContainer = GetPrivateProperty<ModellingDnDContainer>(component.Instance, "Container");
            componentContainer.SvcElements.AddRange(container.SvcElements);

            GetPrivateMethod(typeof(EditServiceGroup), "HandleSvcDrop").Invoke(component.Instance, null);

            Assert.Multiple(() =>
            {
                Assert.That(handler.SvcToAdd.Select(item => item.Id), Is.EquivalentTo(new List<long> { service.Id }));
                Assert.That(componentContainer.SvcElements, Is.Empty);
            });
            await Task.CompletedTask;
        }

        [Test]
        public async Task EditServiceGroupLeftSide_HandleDragStart_PrimesContainerAndClearsSelection()
        {
            await using BunitContext context = CreateContext(out _, out SimulatedUserConfig userConfig);
            ModellingService service = new() { Id = 33, Name = "svc33" };
            ModellingServiceGroupHandler handler = CreateServiceGroupHandler(
                new SimulatedApiConnection(),
                userConfig,
                new ModellingServiceGroup { Name = "grp33" },
                [service],
                [],
                addMode: false);

            ModellingDnDContainer container = new();
            IRenderedComponent<EditServiceGroupLeftSide> component = RenderEditServiceGroupLeftSide(context, handler, container);

            bool handled = (bool)GetPrivateMethod(typeof(EditServiceGroupLeftSide), "HandleDragStart")
                .Invoke(component.Instance, [new DragEventArgs(), service])!;

            Assert.Multiple(() =>
            {
                Assert.That(handled, Is.True);
                Assert.That(container.SvcElements.Select(item => item.Id), Is.EquivalentTo(new List<long> { service.Id }));
                Assert.That(GetPrivateField<List<ModellingService>>(component.Instance, "selectedServices"), Is.Empty);
            });
            await Task.CompletedTask;
        }

        [Test]
        public async Task EditAppRole_OnParametersSetAsync_NetworkAreaRequired_SelectsFirstAreaAndPopulatesServers()
        {
            await using BunitContext context = CreateContext(out RecordingApiConnection apiConn, out SimulatedUserConfig userConfig);
            userConfig.ModNamingConvention = JsonSerializer.Serialize<ModellingNamingConvention>(new ModellingNamingConvention
            {
                NetworkAreaRequired = true,
                FixedPartLength = 4,
                FreePartLength = 5,
                NetworkAreaPattern = "NA",
                AppRolePattern = "AR"
            });

            ModellingNetworkArea area1 = CreateArea(10, "NA10", "Area10", "10.0.0.0", "10.0.0.255");
            ModellingNetworkArea area2 = CreateArea(20, "NA20", "Area20", "10.0.1.0", "10.0.1.255");
            apiConn.Areas = [area1, area2];

            ModellingAppServer matchingServer = CreateServer(1, "match", "10.0.0.10/32");
            ModellingAppServer outsideServer = CreateServer(2, "outside", "10.0.2.10/32");
            ModellingAppRoleHandler handler = CreateAppRoleHandler(
                apiConn,
                userConfig,
                networkAreaRequired: true,
                availableAppServers: [matchingServer, outsideServer],
                appRole: new ModellingAppRole { IdString = "AR10", Name = "role" });

            IRenderedComponent<EditAppRole> component = RenderEditAppRole(context, handler);

            component.WaitForAssertion(() =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(handler.ActAppRole.Area, Is.Not.Null);
                    Assert.That(handler.ActAppRole.Area!.Id, Is.EqualTo(area1.Id));
                Assert.That(handler.AppServersInArea.Select(server => server.Id), Is.EquivalentTo(new List<long> { matchingServer.Id }));
                    Assert.That(area1.MemberCount, Is.EqualTo(1));
                    Assert.That(area2.MemberCount, Is.EqualTo(0));
                });
            });
        }

        [Test]
        public async Task EditAppRole_HandleServerDrop_AddsSelectedServersAndClearsContainer()
        {
            await using BunitContext context = CreateContext(out _, out _);
            ModellingAppServer server = CreateServer(4, "srv4", "10.0.0.4/32");
            ModellingAppRoleHandler handler = CreateAppRoleHandler(
                new SimulatedApiConnection(),
                new SimulatedUserConfig { ModNamingConvention = "{}", ModAppServerTypes = "[]" },
                networkAreaRequired: false,
                availableAppServers: [server],
                appRole: new ModellingAppRole());
            ModellingDnDContainer container = new();
            container.AppServerElements.Add(server);

            IRenderedComponent<EditAppRole> component = RenderEditAppRole(context, handler);
            ModellingDnDContainer componentContainer = GetPrivateProperty<ModellingDnDContainer>(component.Instance, "Container");
            componentContainer.AppServerElements.AddRange(container.AppServerElements);

            GetPrivateMethod(typeof(EditAppRole), "HandleServerDrop").Invoke(component.Instance, null);

            Assert.Multiple(() =>
            {
                Assert.That(handler.AppServerToAdd.Select(item => item.Id), Is.EquivalentTo(new List<long> { server.Id }));
                Assert.That(componentContainer.AppServerElements, Is.Empty);
            });
            await Task.CompletedTask;
        }

        [Test]
        public async Task EditAppRole_NonNetworkAreaRequirement_PopulatesActiveServersInArea()
        {
            await using BunitContext context = CreateContext(out RecordingApiConnection apiConn, out SimulatedUserConfig userConfig);
            userConfig.ModNamingConvention = "{}";

            ModellingAppServer active = CreateServer(1, "active", "10.0.0.1/32");
            ModellingAppServer deleted = CreateServer(2, "deleted", "10.0.0.2/32", deleted: true);
            ModellingAppRoleHandler handler = CreateAppRoleHandler(
                apiConn,
                userConfig,
                networkAreaRequired: false,
                availableAppServers: [active, deleted],
                appRole: new ModellingAppRole { Name = "role", IdString = "APP-1" });

            IRenderedComponent<EditAppRole> component = RenderEditAppRole(context, handler);

            component.WaitForAssertion(() =>
            {
                Assert.That(handler.AppServersInArea, Has.Count.EqualTo(1));
                Assert.That(handler.AppServersInArea[0].Id, Is.EqualTo(active.Id));
            });
        }

        [Test]
        public async Task EditAppRole_Save_AddMode_AddsRoleAndClosesPopup()
        {
            await using BunitContext context = CreateContext(out RecordingApiConnection apiConn, out SimulatedUserConfig userConfig);
            ModellingAppRole appRole = new()
            {
                Name = "role1",
                IdString = "ROLE1"
            };
            ModellingAppRoleHandler handler = CreateAppRoleHandler(
                apiConn,
                userConfig,
                networkAreaRequired: false,
                availableAppServers: [],
                appRole: appRole,
                addMode: true);

            bool displayChanged = true;
            IRenderedComponent<EditAppRole> component = RenderEditAppRole(
                context,
                handler,
                displayChanged: value => displayChanged = value);

            Task saveTask = (Task)GetPrivateMethod(typeof(EditAppRole), "Save").Invoke(component.Instance, null)!;
            await saveTask;

            Assert.Multiple(() =>
            {
                Assert.That(displayChanged, Is.False);
                Assert.That(appRole.Id, Is.EqualTo(89));
                Assert.That(handler.AppRoles.Select(role => role.Id), Is.EquivalentTo(new List<long> { appRole.Id }));
                Assert.That(handler.AvailableNwElems, Has.Count.EqualTo(1));
                Assert.That(apiConn.NewAppRoleCalls, Is.EqualTo(1));
                Assert.That(apiConn.HistoryEntryCalls, Is.EqualTo(1));
            });
        }

        [Test]
        public async Task EditAppRole_OnSelectedAreaChanged_ShowsConfirmationAndReinitializesAfterConfirm()
        {
            await using BunitContext context = CreateContext(out RecordingApiConnection apiConn, out SimulatedUserConfig userConfig);
            userConfig.ModNamingConvention = JsonSerializer.Serialize<ModellingNamingConvention>(new ModellingNamingConvention
            {
                NetworkAreaRequired = true,
                FixedPartLength = 4,
                FreePartLength = 5,
                NetworkAreaPattern = "NA",
                AppRolePattern = "AR"
            });

            ModellingNetworkArea area1 = CreateArea(10, "NA10", "Area10", "10.0.0.0", "10.0.0.255");
            ModellingNetworkArea area2 = CreateArea(20, "NA20", "Area20", "10.0.1.0", "10.0.1.255");
            ModellingAppServer pendingServer = CreateServer(3, "pending", "10.0.0.3/32");
            ModellingAppServer areaServer = CreateServer(4, "area", "10.0.1.4/32");
            apiConn.Areas = [area1, area2];

            ModellingAppRoleHandler handler = CreateAppRoleHandler(
                apiConn,
                userConfig,
                networkAreaRequired: true,
                availableAppServers: [pendingServer, areaServer],
                appRole: new ModellingAppRole { IdString = "AR10", Name = "role" });
            handler.AppServerToAdd.Add(pendingServer);

            IRenderedComponent<EditAppRole> component = RenderEditAppRole(context, handler);

            SetPrivateProperty(component.Instance, "ShowAreaChangeConfirmation", false);
            await (Task)GetPrivateMethod(typeof(EditAppRole), "OnSelectedAreaChanged").Invoke(component.Instance, [area2])!;

            Assert.Multiple(() =>
            {
                Assert.That(GetPrivateProperty<bool>(component.Instance, "ShowAreaChangeConfirmation"), Is.True);
                Assert.That(GetPrivateField<ModellingNetworkArea?>(component.Instance, "LastSelectedNetworkArea"), Is.Not.Null);
                Assert.That(GetPrivateField<ModellingNetworkArea?>(component.Instance, "LastSelectedNetworkArea")!.Id, Is.EqualTo(area2.Id));
            });

            await component.InvokeAsync(() => (Task)GetPrivateMethod(typeof(EditAppRole), "AreaChangeConfirmation").Invoke(component.Instance, null)!);

            Assert.Multiple(() =>
            {
                Assert.That(GetPrivateProperty<bool>(component.Instance, "ShowAreaChangeConfirmation"), Is.False);
                Assert.That(handler.AppServerToAdd, Is.Empty);
                Assert.That(handler.ActAppRole.Area, Is.Not.Null);
                Assert.That(handler.ActAppRole.Area!.Id, Is.EqualTo(area2.Id));
                Assert.That(handler.AppServersInArea.Select(server => server.Id), Is.EquivalentTo(new List<long> { areaServer.Id }));
            });
        }

        [Test]
        public async Task EditAppRoleLeftSide_GetSelectableAppServers_ExcludesExistingAndPending()
        {
            await using BunitContext context = CreateContext(out _, out SimulatedUserConfig userConfig);
            ModellingAppServer server1 = CreateServer(1, "srv1", "10.0.0.1/32");
            ModellingAppServer server2 = CreateServer(2, "srv2", "10.0.0.2/32");
            ModellingAppServer server3 = CreateServer(3, "srv3", "10.0.0.3/32");
            ModellingAppRoleHandler handler = CreateAppRoleHandler(
                new RecordingApiConnection(),
                userConfig,
                networkAreaRequired: false,
                availableAppServers: [server1, server2, server3],
                appRole: new ModellingAppRole());
            handler.AppServersInArea = [server1, server2, server3];
            handler.ActAppRole.AppServers = [new ModellingAppServerWrapper { Content = server2 }];
            handler.AppServerToAdd = [server3];

            IRenderedComponent<EditAppRoleLeftSide> component = RenderEditAppRoleLeftSide(context, handler);
            List<ModellingAppServer> selectable = (List<ModellingAppServer>)GetPrivateMethod(typeof(EditAppRoleLeftSide), "GetSelectableAppServers")
                .Invoke(component.Instance, null)!;

            Assert.That(selectable.Select(server => server.Id), Is.EquivalentTo(new List<long> { 1L }));
        }

        [Test]
        public async Task EditAppRoleLeftSide_HandleDragStart_CopiesSelectionIntoContainer()
        {
            await using BunitContext context = CreateContext(out _, out SimulatedUserConfig userConfig);
            ModellingAppServer server = CreateServer(1, "srv1", "10.0.0.1/32");
            ModellingAppRoleHandler handler = CreateAppRoleHandler(
                new RecordingApiConnection(),
                userConfig,
                networkAreaRequired: false,
                availableAppServers: [server],
                appRole: new ModellingAppRole());

            ModellingDnDContainer container = new();
            IRenderedComponent<EditAppRoleLeftSide> component = RenderEditAppRoleLeftSide(context, handler, container);

            bool handled = (bool)GetPrivateMethod(typeof(EditAppRoleLeftSide), "HandleDragStart")
                .Invoke(component.Instance, [new DragEventArgs(), server])!;

            List<ModellingAppServer> containerServers = container.AppServerElements;
            List<ModellingAppServer> selectedServers = GetPrivateField<List<ModellingAppServer>>(component.Instance, "selectedAppServers");

            Assert.Multiple(() =>
            {
                Assert.That(handled, Is.True);
                Assert.That(containerServers, Has.Count.EqualTo(1));
                Assert.That(containerServers[0].Id, Is.EqualTo(server.Id));
                Assert.That(selectedServers, Is.Empty);
            });
        }

        [Test]
        public async Task EditAppServer_OnParametersSet_InitializesDisplayedFields()
        {
            await using BunitContext context = CreateContext(out _, out SimulatedUserConfig userConfig);
            userConfig.ModAppServerTypes = JsonSerializer.Serialize<List<AppServerType>>([new AppServerType { Id = 2, Name = "TypeA" }]);
            userConfig.ModNamingConvention = "{}";

            ModellingAppServerHandler handler = CreateAppServerHandler(
                new RecordingApiConnection(),
                userConfig,
                new ModellingAppServer
                {
                    Name = "srv1",
                    Ip = "10.0.0.1/32",
                    IpEnd = "10.0.0.1/32",
                    CustomType = 2
                },
                availableAppServers: [],
                addMode: false);

            IRenderedComponent<EditAppServer> component = RenderEditAppServer(context, handler, display: true);

            component.WaitForAssertion(() =>
            {
                AppServerType actType = GetPrivateField<AppServerType>(component.Instance, "actAppServerType");
                string actIpString = GetPrivateField<string>(component.Instance, "actIpString");

                Assert.Multiple(() =>
                {
                    Assert.That(actType.Id, Is.EqualTo(2));
                    Assert.That(actType.Name, Is.EqualTo("TypeA"));
                    Assert.That(actIpString, Is.EqualTo("10.0.0.1"));
                });
            });
        }

        [Test]
        public async Task EditAppServer_Save_AddsAppServerAndClosesPopup()
        {
            await using BunitContext context = CreateContext(out RecordingApiConnection apiConn, out SimulatedUserConfig userConfig);
            userConfig.ModAppServerTypes = JsonSerializer.Serialize<List<AppServerType>>([new AppServerType { Id = 2, Name = "TypeA" }]);
            userConfig.ModNamingConvention = "{}";

            ModellingAppServer appServer = new()
            {
                Name = "srv1",
                Ip = "10.0.0.1/32",
                IpEnd = "10.0.0.1/32",
                CustomType = 2
            };
            List<ModellingAppServer> available = [];
            ModellingAppServerHandler handler = CreateAppServerHandler(apiConn, userConfig, appServer, available, addMode: true);
            bool displayChanged = true;
            int handlerChangedCalls = 0;

            IRenderedComponent<EditAppServer> component = RenderEditAppServer(context, handler, true,
                displayChanged: value => displayChanged = value,
                handlerChanged: _ => handlerChangedCalls++);

            Task saveTask = (Task)GetPrivateMethod(typeof(EditAppServer), "Save").Invoke(component.Instance, null)!;
            await saveTask;

            Assert.Multiple(() =>
            {
                Assert.That(handlerChangedCalls, Is.EqualTo(1));
                Assert.That(displayChanged, Is.False);
                Assert.That(appServer.Id, Is.EqualTo(77));
                Assert.That(available, Has.Count.EqualTo(1));
                Assert.That(apiConn.NewAppServerCalls, Is.EqualTo(1));
                Assert.That(apiConn.HistoryEntryCalls, Is.EqualTo(1));
            });
        }

        [Test]
        public async Task EditAppServer_Save_ReturnsFalseWhenValidationFails()
        {
            await using BunitContext context = CreateContext(out RecordingApiConnection apiConn, out SimulatedUserConfig userConfig);
            userConfig.ModAppServerTypes = "[]";
            userConfig.ModNamingConvention = "{}";

            ModellingAppServer appServer = new()
            {
                Name = "srv1",
                Ip = "",
                IpEnd = "",
                CustomType = 2
            };
            ModellingAppServerHandler handler = CreateAppServerHandler(apiConn, userConfig, appServer, [], addMode: true);
            IRenderedComponent<EditAppServer> component = RenderEditAppServer(context, handler, true);

            Task saveTask = (Task)GetPrivateMethod(typeof(EditAppServer), "Save").Invoke(component.Instance, null)!;
            await saveTask;

            Assert.Multiple(() =>
            {
                Assert.That(apiConn.NewAppServerCalls, Is.EqualTo(0));
                Assert.That(handler.ActAppServer.Id, Is.EqualTo(0));
                Assert.That(component.Instance.Display, Is.True);
            });
        }

        [Test]
        public async Task EditAppServer_Cancel_ResetsAppServerAndClosesPopup()
        {
            await using BunitContext context = CreateContext(out _, out SimulatedUserConfig userConfig);
            userConfig.ModAppServerTypes = JsonSerializer.Serialize<List<AppServerType>>([new AppServerType { Id = 2, Name = "TypeA" }]);
            userConfig.ModNamingConvention = "{}";

            ModellingAppServer appServer = new()
            {
                Name = "srv1",
                Ip = "10.0.0.1/32",
                IpEnd = "10.0.0.1/32",
                CustomType = 2
            };
            List<ModellingAppServer> available = [appServer];
            ModellingAppServerHandler handler = CreateAppServerHandler(new RecordingApiConnection(), userConfig, appServer, available, addMode: false);
            IRenderedComponent<EditAppServer> component = RenderEditAppServer(context, handler, true, displayChanged: value => { });

            handler.ActAppServer.Name = "changed";
            GetPrivateMethod(typeof(EditAppServer), "Cancel").Invoke(component.Instance, null);

            Assert.Multiple(() =>
            {
                Assert.That(component.Instance.Display, Is.False);
                Assert.That(handler.ActAppServer.Name, Is.EqualTo("srv1"));
                Assert.That(available[0].Name, Is.EqualTo("srv1"));
            });
        }

        [Test]
        public async Task ManualAppServer_OnParametersSetAsync_LoadsManualAndCsvServers()
        {
            await using BunitContext context = CreateContext(out RecordingApiConnection apiConn, out SimulatedUserConfig userConfig);
            userConfig.ModAppServerTypes = "[]";

            ModellingAppServer manualServer = CreateServer(101, "manual", "10.0.0.101");
            manualServer.ImportSource = GlobalConst.kManual;
            ModellingAppServer csvServer = CreateServer(102, "csv", "10.0.0.102");
            csvServer.ImportSource = GlobalConst.kCSV_ + "import";
            apiConn.ManualServers = new List<ModellingAppServer> { manualServer };
            apiConn.CsvServers = new List<ModellingAppServer> { csvServer };

            IRenderedComponent<ManualAppServer> component = RenderManualAppServer(context, new FwoOwner { Id = 9, Name = "app" }, true);

            component.WaitForAssertion(() =>
            {
                ModellingAppServerListHandler handler = GetPrivateField<ModellingAppServerListHandler>(component.Instance, "appServerListHandler");
                Assert.Multiple(() =>
                {
                    Assert.That(handler.ManualAppServers.Select(server => server.Id), Is.EquivalentTo(new List<long> { manualServer.Id, csvServer.Id }));
                    Assert.That(handler.ManualAppServers.Any(server => server.ImportSource == GlobalConst.kCSV_ + "import"), Is.True);
                });
            });
        }

        [Test]
        public async Task ManualAppServer_RequestDeleteAppServer_SetsConfirmationMessage()
        {
            await using BunitContext context = CreateContext(out _, out _);
            ModellingAppServer appServer = CreateServer(101, "manual", "10.0.0.101");
            appServer.InUse = true;

            IRenderedComponent<ManualAppServer> component = RenderManualAppServer(context, new FwoOwner { Id = 9, Name = "app" }, true);
            ModellingAppServerListHandler handler = GetPrivateField<ModellingAppServerListHandler>(component.Instance, "appServerListHandler");

            handler.RequestDeleteAppServer(appServer);

            Assert.Multiple(() =>
            {
                Assert.That(handler.DeleteAppServerMode, Is.True);
                Assert.That(handler.Message, Is.EqualTo("Cannot delete manual?"));
            });
        }

        [Test]
        public async Task ManualAppServer_RequestReactivateAppServer_SetsConfirmationMessage()
        {
            await using BunitContext context = CreateContext(out _, out _);
            ModellingAppServer appServer = CreateServer(102, "deleted", "10.0.0.102", deleted: true);

            IRenderedComponent<ManualAppServer> component = RenderManualAppServer(context, new FwoOwner { Id = 9, Name = "app" }, true);
            ModellingAppServerListHandler handler = GetPrivateField<ModellingAppServerListHandler>(component.Instance, "appServerListHandler");

            handler.RequestReactivateAppServer(appServer);

            Assert.Multiple(() =>
            {
                Assert.That(handler.ReactivateAppServerMode, Is.True);
                Assert.That(handler.Message, Is.EqualTo("Reactivate deleted?"));
            });
        }

        [Test]
        public async Task ManualAppServer_CreateAppServer_PrimesHandlerForAddMode()
        {
            await using BunitContext context = CreateContext(out RecordingApiConnection apiConn, out SimulatedUserConfig userConfig);
            userConfig.ModAppServerTypes = "[]";
            apiConn.ManualServers = new List<ModellingAppServer>();
            apiConn.CsvServers = new List<ModellingAppServer>();

            IRenderedComponent<ManualAppServer> component = RenderManualAppServer(context, new FwoOwner { Id = 9, Name = "app" }, true);
            ModellingAppServerListHandler handler = GetPrivateField<ModellingAppServerListHandler>(component.Instance, "appServerListHandler");

            handler.CreateAppServer();

            Assert.Multiple(() =>
            {
                Assert.That(handler.AddAppServerMode, Is.True);
                Assert.That(handler.AppServerHandler, Is.Not.Null);
                Assert.That(handler.AppServerHandler!.ActAppServer.ImportSource, Is.EqualTo(GlobalConst.kManual));
                Assert.That(handler.AppServerHandler.ActAppServer.InUse, Is.False);
            });
        }

        [Test]
        public async Task ManualAppServer_Close_ClosesPopup()
        {
            await using BunitContext context = CreateContext(out _, out _);
            bool displayChanged = true;

            IRenderedComponent<ManualAppServer> component = RenderManualAppServer(
                context,
                new FwoOwner { Id = 9, Name = "app" },
                true,
                displayChanged: value => displayChanged = value);

            GetPrivateMethod(typeof(ManualAppServer), "Close").Invoke(component.Instance, null);

            Assert.Multiple(() =>
            {
                Assert.That(displayChanged, Is.False);
                Assert.That(component.Instance.Display, Is.False);
            });
        }

        [Test]
        public async Task PermittedOwnersSelection_AddOwner_AddsSelectedOwnerAndClearsSelection()
        {
            await using BunitContext context = CreateContext(out _, out _);
            FwoOwner existingOwner = new() { Id = 11, Name = "existing" };
            FwoOwner selectedOwner = new() { Id = 12, Name = "selected" };
            List<FwoOwner> permittedOwners = new List<FwoOwner> { existingOwner };
            List<FwoOwner> ownersToAdd = new List<FwoOwner>();
            List<FwoOwner> ownersToDelete = new List<FwoOwner>();

            IRenderedComponent<PermittedOwnersSelection> component = RenderPermittedOwnersSelection(
                context,
                new List<FwoOwner> { existingOwner, selectedOwner },
                permittedOwners,
                ownersToAdd,
                ownersToDelete,
                readonlyMode: false);

            SetPrivateField(component.Instance, "SelectedOwner", selectedOwner);
            GetPrivateMethod(typeof(PermittedOwnersSelection), "AddOwner").Invoke(component.Instance, null);

            Assert.Multiple(() =>
            {
                Assert.That(ownersToAdd.Select(owner => owner.Id), Is.EquivalentTo(new List<long> { selectedOwner.Id }));
                Assert.That(GetPrivateField<FwoOwner?>(component.Instance, "SelectedOwner"), Is.Null);
            });
        }

        [Test]
        public async Task PermittedOwnersSelection_Readonly_RendersExistingOwners()
        {
            await using BunitContext context = CreateContext(out _, out _);
            FwoOwner owner = new() { Id = 13, Name = "readonly-owner" };

            IRenderedComponent<PermittedOwnersSelection> component = RenderPermittedOwnersSelection(
                context,
                new List<FwoOwner> { owner },
                new List<FwoOwner> { owner },
                new List<FwoOwner>(),
                new List<FwoOwner>(),
                readonlyMode: true);

            Assert.That(component.Markup, Does.Contain("readonly-owner"));
        }

        [Test]
        public async Task PredefServices_Refresh_LoadsServiceGroupsAndServices()
        {
            await using BunitContext context = CreateContext(out RecordingApiConnection apiConn, out SimulatedUserConfig userConfig);
            userConfig.AllowServiceInConn = true;
            ModellingServiceGroup group = new() { Id = 201, Name = "group201" };
            ModellingService service = new() { Id = 202, Name = "service202" };
            apiConn.GlobalServiceGroups = new List<ModellingServiceGroup> { group };
            apiConn.GlobalServices = new List<ModellingService> { service };

            IRenderedComponent<PredefServices> component = RenderPredefServices(context, true);

            component.WaitForAssertion(() =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(component.Instance.PredefServiceGroups.Select(item => item.Id), Is.EquivalentTo(new List<long> { group.Id }));
                    Assert.That(component.Instance.AvailableServices.Select(item => item.Id), Is.EquivalentTo(new List<long> { service.Id }));
                    Assert.That(component.Instance.AvailableSvcElems.Select(item => item.Value), Is.EquivalentTo(new List<int> { (int)group.Id, (int)service.Id }));
                });
            });
        }

        [Test]
        public async Task PredefServices_CreateServiceGroup_PrimesHandlerForAddMode()
        {
            await using BunitContext context = CreateContext(out _, out SimulatedUserConfig userConfig);
            userConfig.AllowServiceInConn = true;
            IRenderedComponent<PredefServices> component = RenderPredefServices(context, true);

            component.Instance.CreateServiceGroup();

            Assert.Multiple(() =>
            {
                Assert.That(component.Instance.AddSvcGrpMode, Is.True);
                Assert.That(component.Instance.EditSvcGrpMode, Is.True);
                Assert.That(component.Instance.SvcGrpHandler, Is.Not.Null);
                Assert.That(component.Instance.SvcGrpHandler!.ActServiceGroup.IsGlobal, Is.True);
            });
        }

        [Test]
        public async Task PredefServices_RequestDeleteServiceGrp_SetsDeleteMessageForUnusedGroup()
        {
            await using BunitContext context = CreateContext(out RecordingApiConnection apiConn, out SimulatedUserConfig userConfig);
            userConfig.AllowServiceInConn = true;
            ModellingServiceGroup group = new() { Id = 301, Name = "group301" };
            apiConn.GlobalServiceGroups = new List<ModellingServiceGroup> { group };
            apiConn.GlobalServices = new List<ModellingService>();
            apiConn.ConnectionsForServiceGroup = new List<ModellingConnection>();

            IRenderedComponent<PredefServices> component = RenderPredefServices(context, true);
            component.WaitForAssertion(() => Assert.That(component.Instance.PredefServiceGroups, Has.Count.EqualTo(1)));

            await component.Instance.RequestDeleteServiceGrp(component.Instance.PredefServiceGroups[0]);

            Assert.Multiple(() =>
            {
                Assert.That(component.Instance.DeleteAllowed, Is.True);
                Assert.That(component.Instance.DeleteSvcGrpMode, Is.True);
                Assert.That(component.Instance.Message, Is.EqualTo("Delete group301?"));
            });
        }

        [Test]
        public async Task PredefServices_DeleteServiceGroup_RemovesGroupAndClosesDialog()
        {
            await using BunitContext context = CreateContext(out RecordingApiConnection apiConn, out SimulatedUserConfig userConfig);
            userConfig.AllowServiceInConn = true;
            ModellingServiceGroup group = new() { Id = 302, Name = "group302" };
            apiConn.GlobalServiceGroups = new List<ModellingServiceGroup> { group };
            apiConn.GlobalServices = new List<ModellingService>();
            apiConn.ConnectionsForServiceGroup = new List<ModellingConnection>();

            IRenderedComponent<PredefServices> component = RenderPredefServices(context, true);
            component.WaitForAssertion(() => Assert.That(component.Instance.PredefServiceGroups, Has.Count.EqualTo(1)));

            await component.Instance.RequestDeleteServiceGrp(component.Instance.PredefServiceGroups[0]);
            await component.Instance.DeleteServiceGroup();

            Assert.Multiple(() =>
            {
                Assert.That(apiConn.DeleteServiceGroupCalls, Is.EqualTo(1));
                Assert.That(apiConn.HistoryEntryCalls, Is.EqualTo(1));
                Assert.That(component.Instance.DeleteSvcGrpMode, Is.False);
                Assert.That(component.Instance.PredefServiceGroups, Is.Empty);
            });
        }

        [Test]
        public async Task ShowHistory_OnParametersSetAsync_LoadsHistoryForSelectedApp()
        {
            await using BunitContext context = CreateContext(out RecordingApiConnection apiConn, out SimulatedUserConfig userConfig);
            ModellingHistoryEntry historyEntry = new()
            {
                Id = 401,
                AppId = 12,
                ChangeType = (int)ModellingTypes.ChangeType.Insert,
                ObjectType = (int)ModellingTypes.ModObjectType.AppServer,
                ObjectId = 99,
                ChangeText = "created",
                Changer = "tester"
            };
            apiConn.HistoryForApp = new List<ModellingHistoryEntry> { historyEntry };

            IRenderedComponent<ShowHistory> component = RenderShowHistory(
                context,
                display: true,
                applications: new List<FwoOwner> { new FwoOwner { Id = 12, Name = "app12" } },
                selectedApp: new FwoOwner { Id = 12, Name = "app12" });

            component.WaitForAssertion(() =>
            {
                List<ModellingHistoryEntry> history = GetPrivateField<List<ModellingHistoryEntry>>(component.Instance, "history");
                Assert.Multiple(() =>
                {
                    Assert.That(apiConn.HistoryForAppCalls, Is.EqualTo(1));
                    Assert.That(history, Has.Count.EqualTo(1));
                    Assert.That(history[0].ObjectId, Is.EqualTo(historyEntry.ObjectId));
                });
            });
        }

        [Test]
        public async Task ShowHistory_Close_ResetsSelectAllAndClosesPopup()
        {
            await using BunitContext context = CreateContext(out _, out _);
            bool displayChanged = true;

            IRenderedComponent<ShowHistory> component = RenderShowHistory(
                context,
                display: true,
                applications: new List<FwoOwner>(),
                selectedApp: new FwoOwner { Id = 1, Name = "app" },
                displayChanged: value => displayChanged = value);

            SetPrivateField(component.Instance, "SelectAll", true);
            GetPrivateMethod(typeof(ShowHistory), "Close").Invoke(component.Instance, null);

            Assert.Multiple(() =>
            {
                Assert.That(displayChanged, Is.False);
                Assert.That(component.Instance.Display, Is.False);
                Assert.That(GetPrivateField<bool>(component.Instance, "SelectAll"), Is.False);
            });
        }

        [Test]
        public async Task ShareLink_OnParametersSet_SetsAppLinkAndCopyClosesPopup()
        {
            await using BunitContext context = CreateContext(out _, out SimulatedUserConfig userConfig);
            userConfig.UiHostName = "https://example.test";
            FwoOwner app = new() { Id = 21, Name = "app21", ExtAppId = "APP21" };
            bool displayChanged = true;

            IRenderedComponent<ShareLink> component = RenderShareLink(
                context,
                display: true,
                application: app,
                displayChanged: value => displayChanged = value);

            component.WaitForAssertion(() =>
            {
                Assert.That(GetPrivateField<string>(component.Instance, "AppLink"), Is.EqualTo("https://example.test/networkmodelling/APP21"));
            });

            Task copyTask = (Task)GetPrivateMethod(typeof(ShareLink), "Copy").Invoke(component.Instance, null)!;
            await copyTask;

            Assert.Multiple(() =>
            {
                Assert.That(displayChanged, Is.False);
                Assert.That(component.Instance.Display, Is.False);
            });
        }

        [Test]
        public async Task SearchNwObject_OnParametersSetAsync_LoadsAndFiltersObjects()
        {
            await using BunitContext context = CreateContext(out RecordingApiConnection apiConn, out SimulatedUserConfig userConfig);
            ModellingNwGroup keepObject = new() { Id = 501, Name = "keep", IdString = "NA501" };
            ModellingNwGroup filteredObject = new() { Id = 502, Name = "filtered", IdString = "NA502" };
            apiConn.NwGroupObjects = new List<ModellingNwGroup> { keepObject, filteredObject };
            List<ModellingNwGroupWrapper> objectList = new List<ModellingNwGroupWrapper>
            {
                new ModellingNwGroupWrapper { Content = filteredObject }
            };
            int addCalls = 0;
            int refreshCalls = 0;

            IRenderedComponent<SearchNwObject> component = RenderSearchNwObject(
                context,
                display: true,
                objectList: objectList,
                application: new FwoOwner { Id = 88, Name = "app88" },
                refresh: () =>
                {
                    refreshCalls++;
                    return true;
                },
                add: _ =>
                {
                    addCalls++;
                    return true;
                });

            component.WaitForAssertion(() =>
            {
                List<ModellingNwGroup> remaining = GetPrivateField<List<ModellingNwGroup>>(component.Instance, "remainingNwObjects");
                Assert.Multiple(() =>
                {
                    Assert.That(apiConn.NwGroupObjectCalls, Is.EqualTo(1));
                    Assert.That(remaining, Has.Count.EqualTo(1));
                    Assert.That(remaining[0].Id, Is.EqualTo(keepObject.Id));
                });
            });

            SetPrivateField(component.Instance, "selectedObject", GetPrivateField<List<ModellingNwGroup>>(component.Instance, "remainingNwObjects")[0]);
            Task addTask = (Task)GetPrivateMethod(typeof(SearchNwObject), "AddObject").Invoke(component.Instance, null)!;
            await addTask;

            Assert.Multiple(() =>
            {
                Assert.That(apiConn.AddSelectedNwGroupObjectCalls, Is.EqualTo(1));
                Assert.That(addCalls, Is.EqualTo(1));
                Assert.That(refreshCalls, Is.EqualTo(1));
                Assert.That(component.Instance.Display, Is.False);
                Assert.That(objectList, Has.Count.EqualTo(2));
            });
        }

        [Test]
        public async Task SearchInterface_OnParametersSetAsync_LoadsSelectableInterfaces_AndSelectInterfaceCloses()
        {
            await using BunitContext context = CreateContext(out RecordingApiConnection apiConn, out SimulatedUserConfig userConfig);
            ModellingConnection firstInterface = new()
            {
                Id = 601,
                Name = "int1",
                AppId = 22,
                InterfacePermission = InterfacePermissions.Public.ToString()
            };
            ModellingConnection secondInterface = new()
            {
                Id = 602,
                Name = "int2",
                AppId = 22,
                InterfacePermission = InterfacePermissions.Public.ToString()
            };
            apiConn.PublishedInterfaces = new List<ModellingConnection> { firstInterface, secondInterface };
            List<ModellingConnection> preselectedInterfaces = new List<ModellingConnection> { firstInterface };
            bool displayChanged = true;

            IRenderedComponent<SearchInterface> component = RenderSearchInterface(
                context,
                display: true,
                preselectedInterfaces: preselectedInterfaces,
                application: new FwoOwner { Id = 22, Name = "app22" },
                displayChanged: value => displayChanged = value);

            component.WaitForAssertion(() =>
            {
                List<ModellingConnection> selectable = GetPrivateProperty<List<ModellingConnection>>(component.Instance, "SelectableInterfaces");
                Assert.Multiple(() =>
                {
                    Assert.That(apiConn.PublishedInterfaceCalls, Is.EqualTo(1));
                    Assert.That(selectable, Has.Count.EqualTo(1));
                    Assert.That(selectable[0].Id, Is.EqualTo(secondInterface.Id));
                });
            });

            SetPrivateProperty(component.Instance, "SelectedInterfaces", new List<ModellingConnection> { secondInterface });
            Task selectTask = (Task)GetPrivateMethod(typeof(SearchInterface), "SelectInterface").Invoke(component.Instance, null)!;
            await selectTask;

            Assert.Multiple(() =>
            {
                Assert.That(apiConn.AddSelectedConnectionCalls, Is.EqualTo(1));
                Assert.That(preselectedInterfaces.Select(item => item.Id), Is.EquivalentTo(new List<long> { firstInterface.Id, secondInterface.Id }));
                Assert.That(component.Instance.Display, Is.False);
                Assert.That(displayChanged, Is.False);
            });
        }

        [Test]
        public async Task AddExtraConfig_OnParametersSet_SelectsFirstTypeAndHidesTextForDokuType()
        {
            await using BunitContext context = CreateContext(out _, out SimulatedUserConfig userConfig);
            ModellingConnectionHandler handler = CreateConnectionHandler(
                new SimulatedApiConnection(),
                userConfig,
                new ModellingConnection { Name = "conn", Reason = "reason" });
            List<string> extraConfigTypes = [$"{GlobalConst.kDoku_}doc", "plain"];

            IRenderedComponent<AddExtraConfig> component = RenderAddExtraConfig(
                context,
                handler,
                display: true,
                availableExtraConfigTypes: extraConfigTypes);

            Assert.Multiple(() =>
            {
                Assert.That(GetPrivateProperty<ModellingExtraConfig>(component.Instance, "ExtraConfig").ExtraConfigType, Is.EqualTo(extraConfigTypes[0]));
                Assert.That(component.FindAll("textarea"), Is.Empty);
            });
            await Task.CompletedTask;
        }

        [Test]
        public async Task AddExtraConfig_Save_AddsSanitizedConfigAndClosesPopup()
        {
            await using BunitContext context = CreateContext(out _, out SimulatedUserConfig userConfig);
            ModellingConnectionHandler handler = CreateConnectionHandler(
                new SimulatedApiConnection(),
                userConfig,
                new ModellingConnection { Name = "conn", Reason = "reason" });
            bool displayChanged = true;
            bool handlerChanged = false;

            IRenderedComponent<AddExtraConfig> component = RenderAddExtraConfig(
                context,
                handler,
                display: true,
                availableExtraConfigTypes: ["plain"],
                displayChanged: value => displayChanged = value,
                connectionHandlerChanged: _ => handlerChanged = true);

            SetPrivateProperty(component.Instance, "ExtraConfig", new ModellingExtraConfig
            {
                ExtraConfigType = "  plain  ",
                ExtraConfigText = "  value  "
            });

            Task saveTask = (Task)GetPrivateMethod(typeof(AddExtraConfig), "Save").Invoke(component.Instance, null)!;
            await saveTask;

            Assert.Multiple(() =>
            {
                Assert.That(handlerChanged, Is.True);
                Assert.That(displayChanged, Is.False);
                Assert.That(component.Instance.Display, Is.False);
                Assert.That(handler.ActConn.ExtraConfigs, Has.Count.EqualTo(1));
                Assert.That(handler.ActConn.ExtraConfigs[0].Id, Is.EqualTo(1));
                Assert.That(handler.ActConn.ExtraConfigs[0].ExtraConfigType, Is.EqualTo("plain"));
                Assert.That(handler.ActConn.ExtraConfigs[0].ExtraConfigText, Is.EqualTo("value"));
            });
        }

        [Test]
        public async Task DecommissionInterfacePopup_OnParametersSet_SetsMessage()
        {
            await using BunitContext context = CreateContext(out _, out SimulatedUserConfig userConfig);
            ModellingConnection actConn = new()
            {
                Id = 701,
                Name = "if701",
                Reason = "reason",
                IsInterface = true,
                IsPublished = true,
                AppId = 1,
                App = new FwoOwner { Id = 1, Name = "app1", ExtAppId = "APP1" }
            };
            ModellingConnectionHandler handler = CreateConnectionHandler(new SimulatedApiConnection(), userConfig, actConn);

            IRenderedComponent<DecommissionInterfacePopup> component = RenderDecommissionInterfacePopup(
                context,
                display: true,
                connHandler: handler,
                possibleInterfaces: [actConn]);

            Assert.That(
                GetPrivateProperty<string>(component.Instance, "Message"),
                Is.EqualTo($"{userConfig.GetText("U9035")} {actConn.Name}?<br>{userConfig.GetText("U9032")}"));
            await Task.CompletedTask;
        }

        [Test]
        public async Task DecommissionInterfacePopup_Decommission_UpdatesHandlerAndClosesPopup()
        {
            await using BunitContext context = CreateContext(out RecordingApiConnection apiConn, out SimulatedUserConfig userConfig);
            userConfig.ModDecommEmailReceiver = nameof(EmailRecipientOption.None);
            ModellingConnection actConn = new()
            {
                Id = 702,
                Name = "if702",
                Reason = "old reason",
                IsInterface = true,
                IsPublished = true,
                AppId = 2,
                App = new FwoOwner { Id = 2, Name = "app2", ExtAppId = "APP2" }
            };
            ModellingConnectionHandler handler = CreateConnectionHandler(apiConn, userConfig, actConn);
            int refreshCalls = 0;
            bool displayChanged = true;

            IRenderedComponent<DecommissionInterfacePopup> component = RenderDecommissionInterfacePopup(
                context,
                display: true,
                connHandler: handler,
                possibleInterfaces: [],
                displayChanged: value => displayChanged = value,
                refreshParent: () =>
                {
                    refreshCalls++;
                    return Task.CompletedTask;
                });

            SetPrivateProperty(component.Instance, "Reason", "planned removal");
            Task decommissionTask = (Task)GetPrivateMethod(typeof(DecommissionInterfacePopup), "Decommission").Invoke(component.Instance, null)!;
            await decommissionTask;

            Assert.Multiple(() =>
            {
                Assert.That(apiConn.UpdateConnectionDecommissionCalls, Is.EqualTo(1));
                Assert.That(apiConn.HistoryEntryCalls, Is.EqualTo(1));
                Assert.That(apiConn.RemoveSelectedConnectionCalls, Is.EqualTo(1));
                Assert.That(refreshCalls, Is.EqualTo(1));
                Assert.That(displayChanged, Is.False);
                Assert.That(component.Instance.Display, Is.False);
                Assert.That(handler.ActConn.Removed, Is.True);
                Assert.That(handler.ActConn.Reason, Does.Contain("planned removal"));
            });
        }

        [Test]
        public async Task InterfaceUsersPopup_TitleIncludesAppDetailsAndCloseClosesPopup()
        {
            await using BunitContext context = CreateContext(out _, out SimulatedUserConfig userConfig);
            bool displayChanged = true;
            FwoOwner app = new() { Id = 3, Name = "app3", ExtAppId = "APP3" };
            List<ModellingConnection> usingConnections =
            [
                new ModellingConnection
                {
                    Id = 703,
                    AppId = 3,
                    App = app,
                    Name = "conn703"
                }
            ];

            IRenderedComponent<InterfaceUsersPopup> component = RenderInterfaceUsersPopup(
                context,
                display: true,
                interfaceName: "if703",
                usingConnections: usingConnections,
                app: app,
                displayChanged: value => displayChanged = value);

            Assert.That(
                GetPrivateProperty<string>(component.Instance, "Title"),
                Is.EqualTo($"{userConfig.GetText("using_connections")} if703 - {app.Name} ({app.ExtAppId})"));

            GetPrivateMethod(typeof(InterfaceUsersPopup), "Close").Invoke(component.Instance, null);

            Assert.Multiple(() =>
            {
                Assert.That(displayChanged, Is.False);
                Assert.That(component.Instance.Display, Is.False);
            });
            await Task.CompletedTask;
        }

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
            ModellingConnectionHandler? connHandler = null)
        {
            return context.Render<CascadingAuthenticationState>(parameters => parameters.AddChildContent<EditConnPopup>(component => component
                .Add(p => p.Display, display)
                .Add(p => p.DisplayChanged, value => displayChanged?.Invoke(value))
                .Add(p => p.ConnHandler, connHandler)
                .Add(p => p.ReplaceMode, replaceMode)
                .Add(p => p.Replace, replace ?? (() => Task.CompletedTask))))
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
