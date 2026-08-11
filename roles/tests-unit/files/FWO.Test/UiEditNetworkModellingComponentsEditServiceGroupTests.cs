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
    }
}
