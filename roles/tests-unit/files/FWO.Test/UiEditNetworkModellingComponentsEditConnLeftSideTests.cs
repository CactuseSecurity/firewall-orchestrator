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
    }
}
