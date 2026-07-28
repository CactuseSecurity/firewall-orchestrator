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
            SimulatedUserConfig.DummyTranslate["U9004"] = "Delete ";
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
    }
}
