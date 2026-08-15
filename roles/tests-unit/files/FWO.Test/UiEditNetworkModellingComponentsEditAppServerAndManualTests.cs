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
            await using BunitContext context = CreateContext(out _, out SimulatedUserConfig userConfig);
            SimulatedUserConfig.DummyTranslate["U9007"] = "Cannot delete used ";
            ModellingAppServer appServer = CreateServer(101, "manual", "10.0.0.101");
            appServer.InUse = true;

            IRenderedComponent<ManualAppServer> component = RenderManualAppServer(context, new FwoOwner { Id = 9, Name = "app" }, true);
            ModellingAppServerListHandler handler = GetPrivateField<ModellingAppServerListHandler>(component.Instance, "appServerListHandler");

            handler.RequestDeleteAppServer(appServer);

            Assert.Multiple(() =>
            {
                Assert.That(handler.DeleteAppServerMode, Is.True);
                Assert.That(handler.Message, Is.EqualTo("Cannot delete used manual?"));
            });
        }

        [Test]
        public async Task ManualAppServer_RequestReactivateAppServer_SetsConfirmationMessage()
        {
            await using BunitContext context = CreateContext(out _, out SimulatedUserConfig userConfig);
            SimulatedUserConfig.DummyTranslate["U9005"] = "Reactivate ";
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

    }
}
