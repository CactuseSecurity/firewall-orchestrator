using AngleSharp.Dom;
using Bunit;
using FWO.Basics;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Services.Modelling;
using FWO.Ui.Pages.NetworkModelling;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Text.Json;

namespace FWO.Test
{
    /// <summary>
    /// Tests of the read-only display, the buttons and the drag and drop handling of the connection editor.
    /// </summary>
    internal partial class UiEditNetworkModellingComponentsTest
    {
        private sealed record UiMessage(string Title, string Text, bool Error);

        private static readonly string[] kFilledElementNames =
            ["src-group", "src-area", "src-role", "src-server", "svc-group", "svc", "dst-group", "dst-area", "dst-role", "dst-server"];

        private static readonly string[] kDisplayButtonKeys = ["display_app_role", "display_app_server", "display_service_group"];

        private static BunitContext CreateEditConnContext(out RecordingApiConnection apiConn, out SimulatedUserConfig userConfig)
        {
            BunitContext context = CreateContext(out apiConn, out userConfig);
            userConfig.ModIconify = false;
            return context;
        }

        private static List<UiMessage> CaptureEditConnMessages(IRenderedComponent<EditConn> component)
        {
            List<UiMessage> messages = [];
            SetPrivateProperty(component.Instance, "DisplayMessageInUi",
                new Action<Exception?, string, string, bool>((_, title, text, error) => messages.Add(new(title, text, error))));
            return messages;
        }

        private static List<UiMessage> CaptureHandlerMessages(ModellingConnectionHandler handler)
        {
            List<UiMessage> messages = [];
            SetPrivateProperty(handler, "DisplayMessageInUi",
                new Action<Exception?, string, string, bool>((_, title, text, error) => messages.Add(new(title, text, error))));
            return messages;
        }

        private static async Task DropAsync(IRenderedComponent<EditConn> component, string dropMethod, ModellingDnDContainer container)
        {
            SetPrivateProperty(component.Instance, "Container", container);
            await component.InvokeAsync(() => (Task)GetPrivateMethod(typeof(EditConn), dropMethod).Invoke(component.Instance, null)!);
        }

        private static string DropMethod(bool source)
        {
            return source ? "HandleSrcDrop" : "HandleDstDrop";
        }

        private static ModellingAppRole CreateAppRole(long id, string name)
        {
            return new ModellingAppRole { Id = id, Name = name, IdString = $"AR{id}" };
        }

        private static ModellingNwGroup CreateNwGroup(long id, string name)
        {
            return new ModellingNwGroup { Id = id, Name = name, IdString = $"GRP{id}" };
        }

        private static IElement FindEditConnButton(IRenderedComponent<EditConn> component, string text)
        {
            return component.FindAll("button").First(button => button.TextContent.Trim() == text);
        }

        private static ModellingConnection CreateFullyFilledConnection()
        {
            return new ModellingConnection
            {
                Id = 120,
                Name = "full-conn",
                Reason = "<b>full</b> reason",
                IsInterface = true,
                InterfacePermission = InterfacePermissions.Restricted.ToString(),
                PermittedOwners = [new FwoOwner { Id = 31, Name = "permitted-app" }],
                ExtraConfigs = [new ModellingExtraConfig { Id = 1, ExtraConfigType = "cfg-a", ExtraConfigText = "own-config" }],
                ExtraConfigsFromInterface = [new ModellingExtraConfig { Id = 2, ExtraConfigType = "cfg-a", ExtraConfigText = "interface-config" }],
                SourceOtherGroups = [new ModellingNwGroupWrapper { Content = CreateNwGroup(1, "src-group") }],
                SourceAreas = [new ModellingNetworkAreaWrapper { Content = CreateArea(2, "NA2", "src-area", "10.2.0.0", "10.2.0.255") }],
                SourceAppRoles = [new ModellingAppRoleWrapper { Content = CreateAppRole(3, "src-role") }],
                SourceAppServers = [new ModellingAppServerWrapper { Content = CreateServer(4, "src-server", "10.4.0.1/32") }],
                ServiceGroups = [new ModellingServiceGroupWrapper { Content = new ModellingServiceGroup { Id = 5, Name = "svc-group" } }],
                Services = [new ModellingServiceWrapper { Content = new ModellingService { Id = 6, Name = "svc", ProtoId = 6, Port = 443 } }],
                DestinationOtherGroups = [new ModellingNwGroupWrapper { Content = CreateNwGroup(7, "dst-group") }],
                DestinationAreas = [new ModellingNetworkAreaWrapper { Content = CreateArea(8, "NA8", "dst-area", "10.8.0.0", "10.8.0.255") }],
                DestinationAppRoles = [new ModellingAppRoleWrapper { Content = CreateAppRole(9, "dst-role") }],
                DestinationAppServers = [new ModellingAppServerWrapper { Content = CreateServer(10, "dst-server", "10.10.0.1/32") }]
            };
        }

        [Test]
        public async Task EditConn_ReadOnly_ShowsEveryElementWithPermissionsAndExtraConfigs()
        {
            await using BunitContext context = CreateEditConnContext(out _, out SimulatedUserConfig userConfig);
            userConfig.ModExtraConfigs = JsonSerializer.Serialize(new List<string> { "cfg-a" });
            ModellingConnectionHandler handler = CreateConnectionHandler(new SimulatedApiConnection(), userConfig, CreateFullyFilledConnection(), readOnly: true);

            IRenderedComponent<EditConn> component = RenderEditConn(context, handler, display: true);

            Assert.Multiple(() =>
            {
                foreach (string name in kFilledElementNames)
                {
                    Assert.That(component.Markup, Does.Contain(name), name);
                }
                Assert.That(component.Markup, Does.Contain("own-config"));
                Assert.That(component.Markup, Does.Contain("interface-config"));
                Assert.That(component.Markup, Does.Contain(userConfig.GetText(InterfacePermissions.Restricted.ToString())));
                Assert.That(component.FindComponents<PermittedOwnersSelection>(), Has.Count.EqualTo(1));
                Assert.That(component.FindComponents<EditConnLeftSide>(), Is.Empty);
            });
        }

        [Test]
        public async Task EditConn_ReadOnly_DisplayButtonsOpenTheElementDetails()
        {
            await using BunitContext context = CreateEditConnContext(out _, out SimulatedUserConfig userConfig);
            ModellingConnectionHandler handler = CreateConnectionHandler(new SimulatedApiConnection(), userConfig, CreateFullyFilledConnection(), readOnly: true);
            List<ModellingConnectionHandler> changedHandlers = [];
            IRenderedComponent<EditConn> component = context.Render<Microsoft.AspNetCore.Components.Authorization.CascadingAuthenticationState>(parameters => parameters
                .AddChildContent<EditConn>(child => child
                    .Add(p => p.Display, true)
                    .Add(p => p.ConnHandler, handler)
                    .Add(p => p.ConnHandlerChanged, changedHandlers.Add)))
                .FindComponent<EditConn>();

            foreach (string buttonKey in kDisplayButtonKeys)
            {
                int buttonCount = component.FindAll("button").Count(button => button.TextContent.Trim() == userConfig.GetText(buttonKey));
                for (int index = 0; index < buttonCount; index++)
                {
                    int buttonIndex = index;
                    await component.InvokeAsync(() => component.FindAll("button")
                        .Where(button => button.TextContent.Trim() == userConfig.GetText(buttonKey))
                        .ElementAt(buttonIndex).Click());
                }
            }

            Assert.Multiple(() =>
            {
                Assert.That(handler.DisplayAppRoleMode, Is.True);
                Assert.That(handler.DisplayAppServerMode, Is.True);
                Assert.That(handler.DisplaySvcGrpMode, Is.True);
                Assert.That(changedHandlers, Has.Count.EqualTo(5));
            });
        }

        [Test]
        public async Task EditConn_ReadOnly_ShowsTheRequestOfARequestedConnection()
        {
            await using BunitContext context = CreateEditConnContext(out _, out SimulatedUserConfig userConfig);
            ModellingConnection connection = CreateFullyFilledConnection();
            connection.IsInterface = false;
            connection.IsRequested = true;
            connection.TicketId = 4711;
            ModellingConnectionHandler handler = CreateConnectionHandler(new SimulatedApiConnection(), userConfig, connection, readOnly: true);

            IRenderedComponent<EditConn> component = RenderEditConn(context, handler, display: true);

            Assert.Multiple(() =>
            {
                Assert.That(component.Markup, Does.Contain("4711"));
                Assert.That(component.Markup, Does.Not.Contain("src-server"));
                Assert.That(component.Markup, Does.Not.Contain("dst-server"));
                Assert.That(component.Markup, Does.Not.Contain("svc-group"));
            });
        }

        [Test]
        public async Task EditConn_ReadOnly_OkClosesTheEditor()
        {
            await using BunitContext context = CreateEditConnContext(out _, out SimulatedUserConfig userConfig);
            ModellingConnectionHandler handler = CreateConnectionHandler(new SimulatedApiConnection(), userConfig,
                new ModellingConnection { Id = 121, Name = "conn", Reason = "reason" }, readOnly: true);
            bool displayChanged = true;
            IRenderedComponent<EditConn> component = RenderEditConn(context, handler, display: true, displayChanged: value => displayChanged = value);

            FindEditConnButton(component, userConfig.GetText("ok")).Click();

            Assert.That(displayChanged, Is.False);
        }

        [Test]
        public async Task EditConn_ExtraConfigs_CanBeChangedDeletedAndAdded()
        {
            await using BunitContext context = CreateEditConnContext(out _, out SimulatedUserConfig userConfig);
            userConfig.ModExtraConfigs = JsonSerializer.Serialize(new List<string> { "cfg-a" });
            ModellingConnection connection = new()
            {
                Id = 122,
                Name = "conn",
                Reason = "reason",
                ExtraConfigs =
                [
                    new ModellingExtraConfig { Id = 1, ExtraConfigType = $"{GlobalConst.kDoku_}note", ExtraConfigText = "documentation" },
                    new ModellingExtraConfig { Id = 2, ExtraConfigType = "cfg-a", ExtraConfigText = "old" }
                ]
            };
            ModellingConnectionHandler handler = CreateConnectionHandler(new SimulatedApiConnection(), userConfig, connection);
            IRenderedComponent<EditConn> component = RenderEditConn(context, handler, display: true);

            component.Find("input[value='old']").Change("new");
            Assert.That(handler.ActConn.ExtraConfigs.Single(config => config.Id == 2).ExtraConfigText, Is.EqualTo("new"));

            component.FindAll("button").First(button => button.TextContent.Trim() == userConfig.GetText("delete_extra_config")).Click();
            FindEditConnButton(component, userConfig.GetText("add_extra_config")).Click();

            Assert.Multiple(() =>
            {
                Assert.That(handler.ActConn.ExtraConfigs.Select(config => config.Id), Is.EqualTo(new List<int> { 2 }));
                Assert.That(handler.AddExtraConfigMode, Is.True);
            });
        }

        [Test]
        public async Task EditConn_RemovingAddedElementsRecalculatesTheVisibility()
        {
            await using BunitContext context = CreateEditConnContext(out _, out SimulatedUserConfig userConfig);
            ModellingConnectionHandler handler = CreateConnectionHandler(new SimulatedApiConnection(), userConfig,
                new ModellingConnection { Id = 123, Name = "conn", Reason = "reason" });
            handler.SrcAreasToAdd.Add(CreateArea(1, "NA1", "src-area", "10.1.0.0", "10.1.0.255"));
            handler.SrcNwGroupsToAdd.Add(CreateNwGroup(2, "src-group"));
            handler.SrcAppRolesToAdd.Add(CreateAppRole(3, "src-role"));
            handler.SrcAppServerToAdd.Add(CreateServer(4, "src-server", "10.4.0.1/32"));
            handler.SvcGrpToAdd.Add(new ModellingServiceGroup { Id = 5, Name = "svc-group" });
            handler.SvcToAdd.Add(new ModellingService { Id = 6, Name = "svc" });
            handler.DstNwGroupsToAdd.Add(CreateNwGroup(7, "dst-group"));
            handler.DstAreasToAdd.Add(CreateArea(8, "NA8", "dst-area", "10.8.0.0", "10.8.0.255"));
            handler.DstAppRolesToAdd.Add(CreateAppRole(9, "dst-role"));
            handler.DstAppServerToAdd.Add(CreateServer(10, "dst-server", "10.10.0.1/32"));
            IRenderedComponent<EditConn> component = RenderEditConn(context, handler, display: true);
            Assert.That(component.Markup, Does.Contain("dst-area"));

            int removed = 0;
            while (component.FindAll("li.draggable-selected span.badge.bg-danger").Count > 0 && removed < 20)
            {
                component.FindAll("li.draggable-selected span.badge.bg-danger")[0].Click();
                removed++;
            }

            Assert.Multiple(() =>
            {
                Assert.That(removed, Is.EqualTo(10));
                Assert.That(handler.SrcAreasToAdd, Is.Empty);
                Assert.That(handler.DstAppServerToAdd, Is.Empty);
                Assert.That(handler.SvcToAdd, Is.Empty);
            });
        }

        [Test]
        public async Task EditConn_RequestedInterfaceOffersTicketActionsAndRejection()
        {
            await using BunitContext context = CreateEditConnContext(out _, out SimulatedUserConfig userConfig);
            userConfig.AvailableModules = string.Empty;
            ModellingConnection connection = new()
            {
                Id = 124,
                Name = "requested",
                Reason = "reason",
                IsInterface = true,
                IsRequested = true,
                TicketId = 777
            };
            ModellingConnectionHandler handler = CreateConnectionHandler(new SimulatedApiConnection(), userConfig, connection);
            bool displayChanged = true;
            IRenderedComponent<EditConn> component = RenderEditConn(context, handler, display: true, displayChanged: value => displayChanged = value);

            Assert.Multiple(() =>
            {
                Assert.That(component.Markup, Does.Contain(userConfig.GetText("display_ticket")));
                Assert.That(component.Markup, Does.Contain(userConfig.GetText("edit_ticket")));
            });
            FindEditConnButton(component, userConfig.GetText("reject")).Click();
            RejectInterfacePopup popup = component.FindComponent<RejectInterfacePopup>().Instance;
            Assert.That(popup.Display, Is.True);

            await component.InvokeAsync(() => popup.RefreshParent());

            Assert.That(displayChanged, Is.False);
        }

        [Test]
        public async Task EditConn_UsedInterfaceCanBeDisplayedAndRemoved()
        {
            await using BunitContext context = CreateEditConnContext(out _, out SimulatedUserConfig userConfig);
            ModellingConnection connection = new()
            {
                Id = 125,
                Name = "conn",
                Reason = "reason",
                UsedInterfaceId = 55,
                SrcFromInterface = true,
                SourceAppServers = [new ModellingAppServerWrapper { Content = CreateServer(4, "interface-server", "10.4.0.1/32") }]
            };
            ModellingConnectionHandler handler = CreateConnectionHandler(new SimulatedApiConnection(), userConfig, connection);
            handler.InterfaceName = "used-interface";
            IRenderedComponent<EditConn> component = RenderEditConn(context, handler, display: true);
            Assert.That(component.Markup, Does.Contain("used-interface"));

            await component.InvokeAsync(() => FindEditConnButton(component, userConfig.GetText("display_interface")).Click());
            FindEditConnButton(component, userConfig.GetText("remove_interface")).Click();

            Assert.Multiple(() =>
            {
                Assert.That(handler.DisplaySelectedInterfaceMode, Is.True);
                Assert.That(handler.InterfaceName, Is.Empty);
                Assert.That(handler.ActConn.SourceAppServers, Is.Empty);
            });
        }

        [Test]
        public async Task EditConn_SaveButtonSavesForModellers()
        {
            await using BunitContext context = CreateEditConnContext(out RecordingApiConnection apiConn, out SimulatedUserConfig userConfig);
            userConfig.User.Roles = [Roles.Modeller];
            ModellingConnectionHandler handler = CreateConnectionHandler(apiConn, userConfig, new ModellingConnection
            {
                Id = 126,
                Name = "conn126",
                Reason = "reason126",
                SrcFromInterface = true,
                DstFromInterface = true,
                UsedInterfaceId = 17
            });
            IRenderedComponent<EditConn> component = RenderEditConn(context, handler, display: true);

            await component.InvokeAsync(() => FindEditConnButton(component, userConfig.GetText("save")).Click());

            Assert.That(apiConn.UpdateConnectionCalls, Is.EqualTo(1));
        }

        [Test]
        public async Task EditConn_CancelResetsTheConnection()
        {
            await using BunitContext context = CreateEditConnContext(out _, out SimulatedUserConfig userConfig);
            ModellingConnectionHandler handler = CreateConnectionHandler(new SimulatedApiConnection(), userConfig,
                new ModellingConnection { Id = 127, Name = "original", Reason = "reason" });
            SetPrivateProperty(handler, "ActConnOrig", new ModellingConnection { Id = 127, Name = "original", Reason = "reason" });
            bool displayChanged = true;
            IRenderedComponent<EditConn> component = RenderEditConn(context, handler, display: true, displayChanged: value => displayChanged = value);
            handler.ActConn.Name = "changed";

            FindEditConnButton(component, userConfig.GetText("cancel")).Click();

            Assert.Multiple(() =>
            {
                Assert.That(handler.ActConn.Name, Is.EqualTo("original"));
                Assert.That(displayChanged, Is.False);
            });
        }

        [Test]
        public async Task EditConn_GotoTicket_WarnsAboutUnsavedChanges()
        {
            await using BunitContext context = CreateEditConnContext(out _, out SimulatedUserConfig userConfig);
            ModellingConnectionHandler handler = CreateConnectionHandler(new SimulatedApiConnection(), userConfig,
                new ModellingConnection { Id = 128, Name = "conn", Reason = "reason", TicketId = 555 });
            IRenderedComponent<EditConn> component = RenderEditConn(context, handler, display: true);
            List<UiMessage> messages = CaptureEditConnMessages(component);
            handler.ActConn.Name = "changed";

            GetPrivateMethod(typeof(EditConn), "GotoTicket").Invoke(component.Instance, null);

            Assert.Multiple(() =>
            {
                Assert.That(messages, Is.EqualTo(new List<UiMessage> { new(userConfig.GetText("E9014"), "", false) }));
                Assert.That(context.Services.GetRequiredService<NavigationManager>().Uri, Does.Not.Contain("/request/tickets/"));
            });
        }

        [Test]
        public async Task EditConn_PromoteTicketToDone_ReportsAFailure()
        {
            await using BunitContext context = CreateEditConnContext(out _, out SimulatedUserConfig userConfig);
            ModellingConnectionHandler handler = CreateConnectionHandler(new SimulatedApiConnection(), userConfig,
                new ModellingConnection { Id = 129, Name = "conn", Reason = "reason", TicketId = 556 });
            IRenderedComponent<EditConn> component = RenderEditConn(context, handler, display: true);
            List<UiMessage> messages = CaptureEditConnMessages(component);
            SetPrivateProperty(component.Instance, "authenticationStateTask", null);

            await component.InvokeAsync(() => (Task)GetPrivateMethod(typeof(EditConn), "PromoteTicketToDone").Invoke(component.Instance, null)!);

            Assert.That(messages, Is.EqualTo(new List<UiMessage> { new(userConfig.GetText("publish"), "", true) }));
        }

        [Test]
        public async Task EditConn_ReplaceInterfaceClosesTheEditor()
        {
            await using BunitContext context = CreateEditConnContext(out _, out SimulatedUserConfig userConfig);
            ModellingConnectionHandler handler = CreateConnectionHandler(new SimulatedApiConnection(), userConfig,
                new ModellingConnection { Id = 130, Name = "conn", Reason = "reason", IsInterface = true });
            bool displayChanged = true;
            IRenderedComponent<EditConn> component = RenderEditConn(context, handler, display: true, displayChanged: value => displayChanged = value);

            EditConnPopup popup = component.FindComponents<EditConnPopup>()
                .Single(found => found.Instance.Replace.Target == component.Instance).Instance;

            await component.InvokeAsync(() => popup.Replace());

            Assert.That(displayChanged, Is.False);
        }

        [Test]
        public async Task EditConn_InterfacePermissionCanBeChosen()
        {
            await using BunitContext context = CreateEditConnContext(out _, out SimulatedUserConfig userConfig);
            ModellingConnectionHandler handler = CreateConnectionHandler(new SimulatedApiConnection(), userConfig,
                new ModellingConnection { Id = 142, Name = "interface", Reason = "reason", IsInterface = true });
            IRenderedComponent<EditConn> component = RenderEditConn(context, handler, display: true);

            component.Find("input[id^='dropdown-input-']").Focus();
            string restricted = userConfig.GetText(InterfacePermissions.Restricted.ToString());
            component.FindAll("button.dropdown-item").First(option => option.TextContent.Trim() == restricted).Click();

            Assert.Multiple(() =>
            {
                Assert.That(handler.ActConn.InterfacePermission, Is.EqualTo(InterfacePermissions.Restricted.ToString()));
                Assert.That(component.FindComponents<PermittedOwnersSelection>(), Has.Count.EqualTo(1));
            });
        }

        [Test]
        public async Task EditConn_GetTitle_ReturnsCommonServiceTitle()
        {
            await using BunitContext context = CreateEditConnContext(out _, out SimulatedUserConfig userConfig);
            ModellingConnectionHandler handler = CreateConnectionHandler(new SimulatedApiConnection(), userConfig,
                new ModellingConnection { Name = "conn", Reason = "reason", IsCommonService = true }, readOnly: true);

            IRenderedComponent<EditConn> component = RenderEditConn(context, handler, display: false);
            string title = (string)GetPrivateMethod(typeof(EditConn), "GetTitle").Invoke(component.Instance, null)!;

            Assert.That(title, Is.EqualTo(userConfig.GetText("common_service")));
        }

        [TestCase(true)]
        [TestCase(false)]
        public async Task EditConn_InterfaceForbidsDropsOppositeToTheFilledSide(bool sourceFilled)
        {
            await using BunitContext context = CreateEditConnContext(out _, out SimulatedUserConfig userConfig);
            ModellingConnection connection = new() { Id = 131, Name = "interface", Reason = "reason", IsInterface = true };
            ModellingConnectionHandler handler = CreateConnectionHandler(new SimulatedApiConnection(), userConfig, connection);
            IRenderedComponent<EditConn> component = RenderEditConn(context, handler, display: true);
            ModellingAppServer server = CreateServer(4, "server", "10.4.0.1/32");
            (sourceFilled ? handler.SrcAppServerToAdd : handler.DstAppServerToAdd).Add(server);

            string forbiddenClass = (string)GetPrivateMethod(typeof(EditConn), sourceFilled ? "DestClass" : "SrcClass").Invoke(component.Instance, null)!;
            string allowedClass = (string)GetPrivateMethod(typeof(EditConn), sourceFilled ? "SrcClass" : "DestClass").Invoke(component.Instance, null)!;
            await DropAsync(component, DropMethod(!sourceFilled), new ModellingDnDContainer { AppServerElements = [CreateServer(5, "dropped", "10.5.0.1/32")] });

            Assert.Multiple(() =>
            {
                Assert.That(forbiddenClass, Is.EqualTo("minheight-dropzone bg-white"));
                Assert.That(allowedClass, Is.EqualTo("minheight-dropzone bg-secondary dropzone"));
                Assert.That(handler.SrcAppServerToAdd.Concat(handler.DstAppServerToAdd).Select(added => added.Id), Is.EqualTo(new List<long> { 4 }));
            });
        }

        [TestCase("HandleSrcDrop")]
        [TestCase("HandleDstDrop")]
        [TestCase("HandleSvcDrop")]
        public async Task EditConn_DropWithoutHandlerOnlyClearsTheContainer(string dropMethod)
        {
            await using BunitContext context = CreateEditConnContext(out _, out SimulatedUserConfig userConfig);
            ModellingConnectionHandler handler = CreateConnectionHandler(new SimulatedApiConnection(), userConfig,
                new ModellingConnection { Name = "conn", Reason = "reason" });
            IRenderedComponent<EditConn> component = RenderEditConn(context, handler, display: false);
            SetComponentProperty(component.Instance, nameof(EditConn.ConnHandler), null);
            ModellingDnDContainer container = new() { AppServerElements = [CreateServer(5, "dropped", "10.5.0.1/32")] };

            await DropAsync(component, dropMethod, container);

            Assert.That(container.AppServerElements, Is.Empty);
        }

        [TestCase(true)]
        [TestCase(false)]
        public async Task EditConn_DropOfForeignInterfaceIntoConnectionWithAreasIsRejected(bool source)
        {
            await using BunitContext context = CreateEditConnContext(out _, out SimulatedUserConfig userConfig);
            ModellingConnectionHandler handler = CreateConnectionHandler(new SimulatedApiConnection(), userConfig, new ModellingConnection
            {
                Id = 132,
                AppId = 17,
                Name = "conn",
                Reason = "reason",
                DestinationAreas = [new ModellingNetworkAreaWrapper { Content = CreateArea(8, "NA8", "area", "10.8.0.0", "10.8.0.255") }]
            });
            IRenderedComponent<EditConn> component = RenderEditConn(context, handler, display: true);
            List<UiMessage> messages = CaptureEditConnMessages(component);

            await DropAsync(component, DropMethod(source), new ModellingDnDContainer { ConnElement = new ModellingConnection { Id = 60, AppId = 5, IsInterface = true } });

            Assert.Multiple(() =>
            {
                Assert.That(messages, Is.EqualTo(new List<UiMessage> { new(userConfig.GetText("edit_connection"), userConfig.GetText("U9024"), true) }));
                Assert.That(handler.ActConn.UsedInterfaceId, Is.Null);
            });
        }

        [TestCase("HandleSrcDrop")]
        [TestCase("HandleDstDrop")]
        [TestCase("HandleSvcDrop")]
        public async Task EditConn_DropOfInterfaceIntoConnectionUsesTheInterface(string dropMethod)
        {
            await using BunitContext context = CreateEditConnContext(out _, out SimulatedUserConfig userConfig);
            ModellingConnectionHandler handler = CreateConnectionHandler(new SimulatedApiConnection(), userConfig,
                new ModellingConnection { Id = 133, AppId = 17, Name = "conn", Reason = "reason" });
            IRenderedComponent<EditConn> component = RenderEditConn(context, handler, display: true);
            ModellingConnection interf = new()
            {
                Id = 61,
                AppId = 5,
                Name = "used-interface",
                IsInterface = true,
                SourceAppServers = [new ModellingAppServerWrapper { Content = CreateServer(4, "interface-server", "10.4.0.1/32") }]
            };

            await DropAsync(component, dropMethod, new ModellingDnDContainer { ConnElement = interf });

            Assert.Multiple(() =>
            {
                Assert.That(handler.ActConn.UsedInterfaceId, Is.EqualTo(61));
                Assert.That(handler.InterfaceName, Is.EqualTo("used-interface"));
            });
        }

        [TestCase("HandleSrcDrop")]
        [TestCase("HandleDstDrop")]
        [TestCase("HandleSvcDrop")]
        public async Task EditConn_DropOfInterfaceIntoInterfaceRequestsReplacement(string dropMethod)
        {
            await using BunitContext context = CreateEditConnContext(out _, out SimulatedUserConfig userConfig);
            ModellingConnectionHandler handler = CreateConnectionHandler(new SimulatedApiConnection(), userConfig,
                new ModellingConnection { Id = 134, Name = "interface", Reason = "reason", IsInterface = true });
            List<UiMessage> handlerMessages = CaptureHandlerMessages(handler);
            IRenderedComponent<EditConn> component = RenderEditConn(context, handler, display: true);
            ModellingConnection otherInterface = new()
            {
                Id = 62,
                IsInterface = true,
                SourceAppServers = [new ModellingAppServerWrapper { Content = CreateServer(4, "interface-server", "10.4.0.1/32") }]
            };

            await DropAsync(component, dropMethod, new ModellingDnDContainer { ConnElement = otherInterface });

            Assert.That(handlerMessages, Is.EqualTo(new List<UiMessage> { new(userConfig.GetText("replace"), userConfig.GetText("E9017"), true) }));
        }

        [TestCase(true)]
        [TestCase(false)]
        public async Task EditConn_DropOfAppRolesIntoCommonServiceWithAreasIsRejected(bool source)
        {
            await using BunitContext context = CreateEditConnContext(out _, out SimulatedUserConfig userConfig);
            ModellingNetworkAreaWrapper area = new() { Content = CreateArea(8, "NA8", "area", "10.8.0.0", "10.8.0.255") };
            ModellingConnection connection = new() { Id = 135, Name = "common", Reason = "reason", IsCommonService = true };
            if (source)
            {
                connection.SourceAreas = [area];
            }
            else
            {
                connection.DestinationAreas = [area];
            }
            ModellingConnectionHandler handler = CreateConnectionHandler(new SimulatedApiConnection(), userConfig, connection);
            IRenderedComponent<EditConn> component = RenderEditConn(context, handler, display: true);
            List<UiMessage> messages = CaptureEditConnMessages(component);

            await DropAsync(component, DropMethod(source), new ModellingDnDContainer { AppRoleElements = [CreateAppRole(3, "role")] });

            Assert.Multiple(() =>
            {
                Assert.That(messages, Is.EqualTo(new List<UiMessage> { new(userConfig.GetText("edit_connection"), userConfig.GetText("U9031"), true) }));
                Assert.That(handler.SrcAppRolesToAdd.Concat(handler.DstAppRolesToAdd), Is.Empty);
            });
        }

        [TestCase(true)]
        [TestCase(false)]
        public async Task EditConn_DropOfAreasIntoCommonServiceWithAppRolesIsRejected(bool source)
        {
            await using BunitContext context = CreateEditConnContext(out _, out SimulatedUserConfig userConfig);
            ModellingAppRoleWrapper appRole = new() { Content = CreateAppRole(3, "role") };
            ModellingConnection connection = new() { Id = 136, Name = "common", Reason = "reason", IsCommonService = true };
            if (source)
            {
                connection.SourceAppRoles = [appRole];
            }
            else
            {
                connection.DestinationAppRoles = [appRole];
            }
            ModellingConnectionHandler handler = CreateConnectionHandler(new SimulatedApiConnection(), userConfig, connection);
            IRenderedComponent<EditConn> component = RenderEditConn(context, handler, display: true);
            List<UiMessage> messages = CaptureEditConnMessages(component);

            await DropAsync(component, DropMethod(source), new ModellingDnDContainer { AreaElements = [CreateArea(8, "NA8", "area", "10.8.0.0", "10.8.0.255")] });

            Assert.That(messages, Is.EqualTo(new List<UiMessage> { new(userConfig.GetText("edit_connection"), userConfig.GetText("U9031"), true) }));
        }

        [TestCase(true)]
        [TestCase(false)]
        public async Task EditConn_DropOfAreasIntoConnectionUsingAForeignInterfaceIsRejected(bool source)
        {
            await using BunitContext context = CreateEditConnContext(out _, out SimulatedUserConfig userConfig);
            ModellingConnectionHandler handler = CreateConnectionHandler(new SimulatedApiConnection(), userConfig,
                new ModellingConnection { Id = 137, AppId = 17, Name = "conn", Reason = "reason", UsedInterfaceId = 99 });
            IRenderedComponent<EditConn> component = RenderEditConn(context, handler, display: true);
            List<UiMessage> messages = CaptureEditConnMessages(component);

            await DropAsync(component, DropMethod(source), new ModellingDnDContainer { AreaElements = [CreateArea(8, "NA8", "area", "10.8.0.0", "10.8.0.255")] });

            Assert.That(messages, Is.EqualTo(new List<UiMessage> { new(userConfig.GetText("edit_connection"), userConfig.GetText("U9024"), true) }));
        }

        [TestCase(true)]
        [TestCase(false)]
        public async Task EditConn_DropOfAreasOppositeToAnAreaIsRejected(bool source)
        {
            await using BunitContext context = CreateEditConnContext(out _, out SimulatedUserConfig userConfig);
            ModellingNetworkAreaWrapper area = new() { Content = CreateArea(8, "NA8", "area", "10.8.0.0", "10.8.0.255") };
            ModellingConnection connection = new() { Id = 138, Name = "conn", Reason = "reason" };
            if (source)
            {
                connection.DestinationAreas = [area];
            }
            else
            {
                connection.SourceAreas = [area];
            }
            ModellingConnectionHandler handler = CreateConnectionHandler(new SimulatedApiConnection(), userConfig, connection);
            IRenderedComponent<EditConn> component = RenderEditConn(context, handler, display: true);
            List<UiMessage> messages = CaptureEditConnMessages(component);

            await DropAsync(component, DropMethod(source), new ModellingDnDContainer { AreaElements = [CreateArea(9, "NA9", "other", "10.9.0.0", "10.9.0.255")] });

            Assert.That(messages, Is.EqualTo(new List<UiMessage> { new(userConfig.GetText("edit_connection"), userConfig.GetText("U9022"), true) }));
        }

        [TestCase(true)]
        [TestCase(false)]
        public async Task EditConn_DropAddsAreasGroupsAndServersToACommonService(bool source)
        {
            await using BunitContext context = CreateEditConnContext(out _, out SimulatedUserConfig userConfig);
            ModellingConnectionHandler handler = CreateConnectionHandler(new SimulatedApiConnection(), userConfig,
                new ModellingConnection { Id = 139, Name = "common", Reason = "reason", IsCommonService = true });
            IRenderedComponent<EditConn> component = RenderEditConn(context, handler, display: true);
            List<UiMessage> messages = CaptureEditConnMessages(component);

            await DropAsync(component, DropMethod(source), new ModellingDnDContainer
            {
                AreaElements = [CreateArea(8, "NA8", "area", "10.8.0.0", "10.8.0.255")],
                NwGroupElements = [CreateNwGroup(2, "group")],
                AppServerElements = [CreateServer(4, "server", "10.4.0.1/32")]
            });

            Assert.Multiple(() =>
            {
                Assert.That(messages, Is.Empty);
                Assert.That((source ? handler.SrcAreasToAdd : handler.DstAreasToAdd).Select(area => area.Id), Is.EqualTo(new List<long> { 8 }));
                Assert.That((source ? handler.SrcNwGroupsToAdd : handler.DstNwGroupsToAdd).Select(group => group.Id), Is.EqualTo(new List<long> { 2 }));
                Assert.That((source ? handler.SrcAppServerToAdd : handler.DstAppServerToAdd).Select(server => server.Id), Is.EqualTo(new List<long> { 4 }));
            });
        }

        [TestCase(true)]
        [TestCase(false)]
        public async Task EditConn_DropAddsAppRolesToAConnection(bool source)
        {
            await using BunitContext context = CreateEditConnContext(out _, out SimulatedUserConfig userConfig);
            ModellingConnectionHandler handler = CreateConnectionHandler(new SimulatedApiConnection(), userConfig,
                new ModellingConnection { Id = 140, Name = "conn", Reason = "reason" });
            IRenderedComponent<EditConn> component = RenderEditConn(context, handler, display: true);

            await DropAsync(component, DropMethod(source), new ModellingDnDContainer { AppRoleElements = [CreateAppRole(3, "role")] });

            Assert.That((source ? handler.SrcAppRolesToAdd : handler.DstAppRolesToAdd).Select(role => role.Id), Is.EqualTo(new List<long> { 3 }));
        }

        [Test]
        public async Task EditConn_DropAddsServicesAndServiceGroups()
        {
            await using BunitContext context = CreateEditConnContext(out _, out SimulatedUserConfig userConfig);
            ModellingConnectionHandler handler = CreateConnectionHandler(new SimulatedApiConnection(), userConfig,
                new ModellingConnection { Id = 141, Name = "conn", Reason = "reason" });
            IRenderedComponent<EditConn> component = RenderEditConn(context, handler, display: true);

            await DropAsync(component, "HandleSvcDrop", new ModellingDnDContainer
            {
                SvcElements = [new ModellingService { Id = 6, Name = "svc" }],
                SvcGrpElements = [new ModellingServiceGroup { Id = 5, Name = "svc-group" }]
            });

            Assert.Multiple(() =>
            {
                Assert.That(handler.SvcToAdd.Select(service => service.Id), Is.EqualTo(new List<int> { 6 }));
                Assert.That(handler.SvcGrpToAdd.Select(group => group.Id), Is.EqualTo(new List<long> { 5 }));
            });
        }
    }
}
