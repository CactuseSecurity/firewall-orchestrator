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
        public async Task SearchNwObject_ComSvcExcludedMode_OffersCommonAreasAndDoesNotStoreSelection()
        {
            await using BunitContext context = CreateContext(out RecordingApiConnection apiConn, out SimulatedUserConfig userConfig);
            ModellingNwGroup commonArea = new() { Id = 511, Name = "common", IdString = "NA511" };
            ModellingNwGroup alreadyExcludedArea = new() { Id = 512, Name = "excluded", IdString = "NA512" };
            apiConn.NwGroupObjects = [commonArea, alreadyExcludedArea];
            userConfig.ModCommonAreas = JsonSerializer.Serialize(new List<CommonAreaConfig> { new() { AreaId = commonArea.Id } });
            List<ModellingNwGroupWrapper> objectList = [new ModellingNwGroupWrapper { Content = alreadyExcludedArea }];
            List<ModellingNwGroup> addedAreas = [];

            IRenderedComponent<SearchNwObject> component = context.Render<CascadingAuthenticationState>(parameters => parameters
                .AddChildContent<SearchNwObject>(search => search
                    .Add(p => p.Display, true)
                    .Add(p => p.ObjectList, objectList)
                    .Add(p => p.Add, area =>
                    {
                        addedAreas.Add(area);
                        return true;
                    })
                    .Add(p => p.ComSvcExcludedMode, true)))
                .FindComponent<SearchNwObject>();

            component.WaitForAssertion(() =>
            {
                List<ModellingNwGroup> remaining = GetPrivateField<List<ModellingNwGroup>>(component.Instance, "remainingNwObjects");
                Assert.That(remaining.Select(o => o.Id), Is.EqualTo(new List<long> { commonArea.Id }));
            });

            SetPrivateField(component.Instance, "selectedObject", commonArea);
            await (Task)GetPrivateMethod(typeof(SearchNwObject), "AddObject").Invoke(component.Instance, null)!;

            Assert.Multiple(() =>
            {
                Assert.That(apiConn.AddSelectedNwGroupObjectCalls, Is.EqualTo(0));
                Assert.That(addedAreas, Is.EqualTo(new List<ModellingNwGroup> { commonArea }));
                Assert.That(component.Instance.Display, Is.False);
            });
        }

        [Test]
        public async Task SearchNwObject_HiddenObjectIds_AreNotOffered()
        {
            await using BunitContext context = CreateContext(out RecordingApiConnection apiConn, out SimulatedUserConfig userConfig);
            ModellingNwGroup offeredArea = new() { Id = 521, Name = "offered", IdString = "NA521" };
            ModellingNwGroup hiddenArea = new() { Id = 522, Name = "hidden", IdString = "NA522" };
            apiConn.NwGroupObjects = [offeredArea, hiddenArea];

            IRenderedComponent<SearchNwObject> component = context.Render<CascadingAuthenticationState>(parameters => parameters
                .AddChildContent<SearchNwObject>(search => search
                    .Add(p => p.Display, true)
                    .Add(p => p.ObjectList, [])
                    .Add(p => p.HiddenObjectIds, new HashSet<long> { hiddenArea.Id })))
                .FindComponent<SearchNwObject>();

            component.WaitForAssertion(() =>
            {
                List<ModellingNwGroup> remaining = GetPrivateField<List<ModellingNwGroup>>(component.Instance, "remainingNwObjects");
                Assert.That(remaining.Select(o => o.Id), Is.EqualTo(new List<long> { offeredArea.Id }));
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
    }
}
