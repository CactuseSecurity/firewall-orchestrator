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

    }
}
