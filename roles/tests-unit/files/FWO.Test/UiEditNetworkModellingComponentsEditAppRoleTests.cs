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

    }
}
