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
using FWO.Ui.Shared;
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
        public async Task EditConnPopup_ShowsLogDataWhenTheCallerOptsIn()
        {
            await using BunitContext context = CreateContext(out _, out SimulatedUserConfig userConfig);
            userConfig.ShowLogDataInConnections = true;
            ModellingConnectionHandler handler = CreateConnectionHandler(new SimulatedApiConnection(), userConfig,
                new ModellingConnection { Name = "conn", Reason = "reason" }, readOnly: true);

            IRenderedComponent<EditConnPopup> component = RenderEditConnPopup(context, display: true, replaceMode: false,
                connHandler: handler, showLogData: true);

            Assert.That(component.FindComponents<LogDataTable>(), Has.Count.EqualTo(1));
            await Task.CompletedTask;
        }

        [Test]
        public async Task EditConnPopup_HidesLogDataForCallersWithoutOptIn()
        {
            await using BunitContext context = CreateContext(out _, out SimulatedUserConfig userConfig);
            userConfig.ShowLogDataInConnections = true;
            ModellingConnectionHandler handler = CreateConnectionHandler(new SimulatedApiConnection(), userConfig,
                new ModellingConnection { Name = "conn", Reason = "reason" }, readOnly: true);

            IRenderedComponent<EditConnPopup> component = RenderEditConnPopup(context, display: true, replaceMode: false,
                connHandler: handler);

            Assert.That(component.FindComponents<LogDataTable>(), Is.Empty);
            await Task.CompletedTask;
        }

        [Test]
        public async Task EditConnPopup_HidesLogDataForUnresolvedOwner()
        {
            await using BunitContext context = CreateContext(out _, out SimulatedUserConfig userConfig);
            userConfig.ShowLogDataInConnections = true;
            ModellingConnection connection = new() { Name = "conn", Reason = "reason" };
            ModellingConnectionHandler handler = new(new SimulatedApiConnection(), userConfig, new FwoOwner(),
                [connection], connection, false, true, (_, _, _, _) => { }, () => Task.CompletedTask, false);

            IRenderedComponent<EditConnPopup> component = RenderEditConnPopup(context, display: true, replaceMode: false,
                connHandler: handler, showLogData: true);

            Assert.That(component.FindComponents<LogDataTable>(), Is.Empty);
            await Task.CompletedTask;
        }
    }
}
