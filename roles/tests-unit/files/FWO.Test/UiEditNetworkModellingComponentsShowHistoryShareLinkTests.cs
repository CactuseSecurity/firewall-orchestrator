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
        public async Task ShowHistory_OnParametersSetAsync_LoadsHistoryForSelectedApp()
        {
            await using BunitContext context = CreateContext(out RecordingApiConnection apiConn, out SimulatedUserConfig userConfig);
            ModellingHistoryEntry historyEntry = new()
            {
                Id = 401,
                AppId = 12,
                ChangeType = (int)ModellingTypes.ChangeType.Insert,
                ObjectType = (int)ModellingTypes.ModObjectType.AppServer,
                ObjectId = 99,
                ChangeText = "created",
                Changer = "tester"
            };
            apiConn.HistoryForApp = new List<ModellingHistoryEntry> { historyEntry };

            IRenderedComponent<ShowHistory> component = RenderShowHistory(
                context,
                display: true,
                applications: new List<FwoOwner> { new FwoOwner { Id = 12, Name = "app12" } },
                selectedApp: new FwoOwner { Id = 12, Name = "app12" });

            component.WaitForAssertion(() =>
            {
                List<ModellingHistoryEntry> history = GetPrivateField<List<ModellingHistoryEntry>>(component.Instance, "history");
                Assert.Multiple(() =>
                {
                    Assert.That(apiConn.HistoryForAppCalls, Is.EqualTo(1));
                    Assert.That(history, Has.Count.EqualTo(1));
                    Assert.That(history[0].ObjectId, Is.EqualTo(historyEntry.ObjectId));
                });
            });
        }

        [Test]
        public async Task ShowHistory_Close_ResetsSelectAllAndClosesPopup()
        {
            await using BunitContext context = CreateContext(out _, out _);
            bool displayChanged = true;

            IRenderedComponent<ShowHistory> component = RenderShowHistory(
                context,
                display: true,
                applications: new List<FwoOwner>(),
                selectedApp: new FwoOwner { Id = 1, Name = "app" },
                displayChanged: value => displayChanged = value);

            SetPrivateField(component.Instance, "SelectAll", true);
            GetPrivateMethod(typeof(ShowHistory), "Close").Invoke(component.Instance, null);

            Assert.Multiple(() =>
            {
                Assert.That(displayChanged, Is.False);
                Assert.That(component.Instance.Display, Is.False);
                Assert.That(GetPrivateField<bool>(component.Instance, "SelectAll"), Is.False);
            });
        }

        [Test]
        public async Task ShareLink_OnParametersSet_SetsAppLinkAndCopyClosesPopup()
        {
            await using BunitContext context = CreateContext(out _, out SimulatedUserConfig userConfig);
            userConfig.UiHostName = "https://example.test";
            FwoOwner app = new() { Id = 21, Name = "app21", ExtAppId = "APP21" };
            bool displayChanged = true;

            IRenderedComponent<ShareLink> component = RenderShareLink(
                context,
                display: true,
                application: app,
                displayChanged: value => displayChanged = value);

            component.WaitForAssertion(() =>
            {
                Assert.That(GetPrivateField<string>(component.Instance, "AppLink"), Is.EqualTo("https://example.test/networkmodelling/APP21"));
            });

            Task copyTask = (Task)GetPrivateMethod(typeof(ShareLink), "Copy").Invoke(component.Instance, null)!;
            await copyTask;

            Assert.Multiple(() =>
            {
                Assert.That(displayChanged, Is.False);
                Assert.That(component.Instance.Display, Is.False);
            });
        }
    }
}
