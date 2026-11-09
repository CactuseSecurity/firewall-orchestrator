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
    }
}
