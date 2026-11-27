using AngleSharp.Dom;
using Bunit;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api.Data;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Middleware.Client;
using FWO.Services.Modelling;
using FWO.Test.Mocks;
using FWO.Ui.Pages.NetworkModelling;
using FWO.Ui.Shared;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using RestSharp;
using System.Net;
using System.Text.Json;

namespace FWO.Test
{
    /// <summary>
    /// Tests of decommissioning an interface, of searching network objects for the library and of the service library of
    /// the service group editor.
    /// </summary>
    internal partial class UiEditNetworkModellingComponentsTest
    {
        private const int kDecommissionedInterfaceId = 701;
        private const int kAlternativeInterfaceId = 801;
        private const int kUsingAppId = 5;

        /// <summary>
        /// Records the notification about a decommissioned interface instead of sending it to a middleware.
        /// </summary>
        private sealed class DecommissionMiddlewareClient : TestMiddlewareClient
        {
            public List<(int ConnectionId, int? ReplacementId, string Reason)> Notifications { get; } = [];

            public override Task<RestResponse<NotificationDeliveryResult>> SendInterfaceDecommissionNotification(int connectionId, int? replacementConnectionId, string reason)
            {
                Notifications.Add((connectionId, replacementConnectionId, reason));
                return Task.FromResult(new RestResponse<NotificationDeliveryResult>(new RestRequest())
                {
                    IsSuccessStatusCode = true,
                    ResponseStatus = ResponseStatus.Completed,
                    StatusCode = HttpStatusCode.OK,
                    Data = NotificationDeliveryResult.Delivered
                });
            }
        }

        private static ModellingConnectionHandler CreateDecommissionHandler(ApiConnection apiConn, SimulatedUserConfig userConfig)
        {
            ModellingConnection decommissionedInterface = new()
            {
                Id = kDecommissionedInterfaceId,
                AppId = 1,
                Name = "old-interface",
                Reason = "reason",
                IsInterface = true,
                IsPublished = true
            };
            ModellingConnectionHandler handler = CreateConnectionHandler(apiConn, userConfig, decommissionedInterface);
            handler.UsingConnections =
            [
                new ModellingConnection { Id = 711, AppId = kUsingAppId, Name = "using-connection", App = new FwoOwner { Id = kUsingAppId, Name = "using-app" } }
            ];
            return handler;
        }

        private static List<ModellingConnection> PossibleAlternatives()
        {
            return
            [
                new ModellingConnection { Id = kAlternativeInterfaceId, Name = "alternative-interface", IsInterface = true, IsPublished = true, InterfacePermission = InterfacePermissions.Public.ToString() },
                new ModellingConnection { Id = 802, Name = "private-interface", IsInterface = true, IsPublished = true, InterfacePermission = InterfacePermissions.Private.ToString() },
                new ModellingConnection { Id = 803, Name = "unpublished-interface", IsInterface = true, IsPublished = false },
                new ModellingConnection { Id = kDecommissionedInterfaceId, Name = "old-interface", IsInterface = true, IsPublished = true }
            ];
        }

        private static IRenderedComponent<DecommissionInterfacePopup> RenderDecommissionPopup(BunitContext context, ModellingConnectionHandler handler,
            List<UiMessage> messages, Action<bool>? displayChanged = null)
        {
            return RenderWithMessages<DecommissionInterfacePopup>(context, messages, component => component
                .Add(p => p.Display, true)
                .Add(p => p.DisplayChanged, value => displayChanged?.Invoke(value))
                .Add(p => p.ConnHandler, handler)
                .Add(p => p.PossibleInterfaces, PossibleAlternatives()));
        }

        [Test]
        public async Task DecommissionInterfacePopup_ProposesAnAlternativeToTheUsingApps()
        {
            await using BunitContext context = CreateContext(out _, out SimulatedUserConfig userConfig);
            userConfig.ModIconify = false;
            userConfig.User.Roles = [Roles.Modeller];
            DecommissionMiddlewareClient middleware = new();
            context.Services.AddSingleton<MiddlewareClient>(middleware);
            LibraryApiConnection apiConn = new();
            ModellingConnectionHandler handler = CreateDecommissionHandler(apiConn, userConfig);
            // the users are reloaded while the interface is saved as decommissioned
            List<ModellingConnection> usingConnections = handler.UsingConnections;
            apiConn.Answers[ModellingQueries.getInterfaceUsers] = _ => usingConnections.ToList();
            bool displayChanged = true;
            IRenderedComponent<DecommissionInterfacePopup> component = RenderDecommissionPopup(context, handler, [], value => displayChanged = value);
            Assert.That(component.Markup, Does.Contain("using-app"));

            component.Find("textarea").Change("replaced");
            component.Find("input[type='checkbox']").Change(true);
            component.Find("input[id^='dropdown-input-']").Focus();
            List<string> offered = [.. component.FindAll("button.dropdown-item").Select(option => option.TextContent.Trim())];
            component.FindAll("button.dropdown-item").First(option => option.TextContent.Trim() == "alternative-interface").Click();
            await component.InvokeAsync(() => ButtonsWithText(component, userConfig.GetText("decommission")).Single().Click());

            Assert.Multiple(() =>
            {
                Assert.That(offered, Is.EqualTo(new List<string> { "alternative-interface" }), "only other published, not private interfaces are offered");
                Assert.That(middleware.Notifications, Is.EqualTo(new List<(int, int?, string)> { (kDecommissionedInterfaceId, kAlternativeInterfaceId, "replaced") }));
                Assert.That(apiConn.VariableOf(ModellingQueries.addSelectedConnection, "connectionId"), Is.EqualTo(kAlternativeInterfaceId));
                Assert.That(apiConn.VariableOf(ModellingQueries.addSelectedConnection, "appId"), Is.EqualTo(kUsingAppId));
                Assert.That(displayChanged, Is.False);
            });
        }

        [Test]
        public async Task DecommissionInterfacePopup_ReportsAFailedDecommissioning()
        {
            await using BunitContext context = CreateContext(out _, out SimulatedUserConfig userConfig);
            userConfig.ModIconify = false;
            userConfig.User.Roles = [Roles.Modeller];
            LibraryApiConnection apiConn = new();
            apiConn.FailingQueries.Add(ModellingQueries.removeSelectedConnection);
            ModellingConnectionHandler handler = CreateDecommissionHandler(apiConn, userConfig);
            List<UiMessage> messages = [];
            bool displayChanged = true;
            IRenderedComponent<DecommissionInterfacePopup> component = RenderDecommissionPopup(context, handler, messages, value => displayChanged = value);

            await component.InvokeAsync(() => ButtonsWithText(component, userConfig.GetText("decommission")).Single().Click());

            Assert.Multiple(() =>
            {
                Assert.That(messages, Has.Some.Matches<UiMessage>(message => message.Title == userConfig.GetText("decomm_interface") && message.Error));
                Assert.That(displayChanged, Is.True, "the popup stays open");
                Assert.That(ButtonsWithText(component, userConfig.GetText("decommission")).Single().HasAttribute("disabled"), Is.False);
            });
        }

        [Test]
        public async Task DecommissionInterfacePopup_RunningDecommissioningDisablesTheButtonAndIgnoresAnotherClick()
        {
            await using BunitContext context = CreateContext(out _, out SimulatedUserConfig userConfig);
            userConfig.ModIconify = false;
            userConfig.User.Roles = [Roles.Modeller];
            context.Services.AddSingleton<MiddlewareClient>(new DecommissionMiddlewareClient());
            LibraryApiConnection apiConn = new() { Gate = new TaskCompletionSource() };
            ModellingConnectionHandler handler = CreateDecommissionHandler(apiConn, userConfig);
            IRenderedComponent<DecommissionInterfacePopup> component = RenderDecommissionPopup(context, handler, []);

            Task decommissioning = component.InvokeAsync(() => ButtonsWithText(component, userConfig.GetText("decommission")).Single().ClickAsync(new MouseEventArgs()));
            component.WaitForAssertion(() => Assert.That(ButtonsWithText(component, userConfig.GetText("decommission")).Single().HasAttribute("disabled"), Is.True));
            int queriesWhileRunning = apiConn.Queries.Count;
            await component.InvokeAsync(() => (Task)GetPrivateMethod(typeof(DecommissionInterfacePopup), "Decommission").Invoke(component.Instance, null)!);
            int queriesAfterSecondCall = apiConn.Queries.Count;
            apiConn.Gate.SetResult();
            await decommissioning;

            Assert.That(queriesAfterSecondCall, Is.EqualTo(queriesWhileRunning), "the second call does nothing");
        }

        private static IRenderedComponent<SearchNwObject> RenderObjectSearch(BunitContext context, List<UiMessage> messages, List<ModellingNwGroupWrapper> objectList,
            List<ModellingNwGroup> addedObjects, Action<bool>? displayChanged = null, bool specUserMode = false, bool updObjMode = false)
        {
            return RenderWithMessages<SearchNwObject>(context, messages, component => component
                .Add(p => p.Display, true)
                .Add(p => p.DisplayChanged, value => displayChanged?.Invoke(value))
                .Add(p => p.ObjectList, objectList)
                .Add(p => p.Application, new FwoOwner { Id = 9, Name = "app" })
                .Add(p => p.Add, added =>
                {
                    addedObjects.Add(added);
                    return true;
                })
                .Add(p => p.SpecUserMode, specUserMode)
                .Add(p => p.UpdObjMode, updObjMode)
                .Add(p => p.HiddenObjectIds, new HashSet<long> { 4 }));
        }

        /// <summary>
        /// Network areas 1 to 5: area 1 is configured for the respective mode, area 2 is already selected and area 4 is hidden.
        /// </summary>
        private static LibraryApiConnection CreateObjectSearchApiConn()
        {
            LibraryApiConnection apiConn = new();
            apiConn.Answers[ModellingQueries.getNwGroupObjects] = _ => Enumerable.Range(1, 5)
                .Select(id => new ModellingNwGroup { Id = id, Name = $"area{id}", IdString = $"NA{id}" }).ToList();
            return apiConn;
        }

        private static List<string> OfferedObjects(IRenderedComponent<SearchNwObject> component)
        {
            component.Find("input[id^='dropdown-input-']").Focus();
            return [.. component.FindAll("button.dropdown-item").Select(option => option.TextContent.Trim())];
        }

        [Test]
        public async Task SearchNwObject_SelectedAreaIsAddedToTheApplication()
        {
            await using BunitContext context = CreateContext(out _, out SimulatedUserConfig userConfig);
            userConfig.ModIconify = false;
            userConfig.User.Roles = [Roles.Modeller];
            userConfig.ModCommonAreas = JsonSerializer.Serialize(new List<CommonAreaConfig> { new() { AreaId = 1 } });
            LibraryApiConnection apiConn = CreateObjectSearchApiConn();
            context.Services.AddSingleton<ApiConnection>(apiConn);
            List<ModellingNwGroupWrapper> objectList = [new() { Content = new ModellingNwGroup { Id = 2, Name = "area2" } }];
            List<ModellingNwGroup> addedObjects = [];
            bool displayChanged = true;
            IRenderedComponent<SearchNwObject> component = RenderObjectSearch(context, [], objectList, addedObjects, value => displayChanged = value);

            List<string> offered = OfferedObjects(component);
            component.FindAll("button.dropdown-item").First(option => option.TextContent.Contains("area3")).Click();
            await component.InvokeAsync(() => ButtonsWithText(component, userConfig.GetText("select")).Single().Click());

            Assert.Multiple(() =>
            {
                Assert.That(offered.Select(option => option.Contains("area3") || option.Contains("area5")), Has.All.True, "common, selected and hidden areas are not offered");
                Assert.That(offered, Has.Count.EqualTo(2));
                Assert.That(apiConn.VariableOf(ModellingQueries.addSelectedNwGroupObject, "nwGroupId"), Is.EqualTo(3L));
                Assert.That(objectList.Select(wrapper => wrapper.Content.Id), Is.EqualTo(new List<long> { 2, 3 }));
                Assert.That(addedObjects.Select(added => added.Id), Is.EqualTo(new List<long> { 3 }));
                Assert.That(displayChanged, Is.False);
            });
        }

        [TestCase(true, false)]
        [TestCase(false, true)]
        public async Task SearchNwObject_SettingsModesFilterTheirConfiguredAreas(bool specUserMode, bool updObjMode)
        {
            await using BunitContext context = CreateContext(out _, out SimulatedUserConfig userConfig);
            userConfig.ModIconify = false;
            userConfig.User.Roles = [Roles.Admin];
            string configuredAreas = JsonSerializer.Serialize(new List<CommonAreaConfig> { new() { AreaId = 1 } });
            userConfig.ModSpecUserAreas = configuredAreas;
            userConfig.ModUpdatableObjAreas = configuredAreas;
            LibraryApiConnection apiConn = CreateObjectSearchApiConn();
            context.Services.AddSingleton<ApiConnection>(apiConn);
            List<ModellingNwGroupWrapper> objectList = [];
            IRenderedComponent<SearchNwObject> component = RenderObjectSearch(context, [], objectList, [], specUserMode: specUserMode, updObjMode: updObjMode);

            List<string> offered = OfferedObjects(component);
            component.FindAll("button.dropdown-item").First(option => option.TextContent.Contains("area2")).Click();
            await component.InvokeAsync(() => ButtonsWithText(component, userConfig.GetText("select")).Single().Click());

            Assert.Multiple(() =>
            {
                Assert.That(offered.Any(option => option.Contains("area1")), Is.False);
                Assert.That(offered, Has.Count.EqualTo(3));
                Assert.That(objectList.Select(wrapper => wrapper.Content.Id), Is.EqualTo(new List<long> { 2 }));
                Assert.That(apiConn.Queries, Does.Not.Contain(ModellingQueries.addSelectedNwGroupObject), "settings only collect the objects");
            });
        }

        [Test]
        public async Task SearchNwObject_SettingsModeNeedsTheAdminRole()
        {
            await using BunitContext context = CreateContext(out _, out SimulatedUserConfig userConfig);
            userConfig.ModIconify = false;
            userConfig.User.Roles = [Roles.Modeller];
            context.Services.AddSingleton<ApiConnection>(CreateObjectSearchApiConn());
            IRenderedComponent<SearchNwObject> component = RenderObjectSearch(context, [], [], [], specUserMode: true);

            OfferedObjects(component);
            component.FindAll("button.dropdown-item").First(option => option.TextContent.Contains("area2")).Click();

            Assert.That(ButtonsWithText(component, userConfig.GetText("select")).Single().HasAttribute("disabled"), Is.True);
        }

        [Test]
        public async Task SearchNwObject_ReportsAFailedSearch()
        {
            await using BunitContext context = CreateContext(out _, out SimulatedUserConfig userConfig);
            LibraryApiConnection apiConn = CreateObjectSearchApiConn();
            apiConn.FailingQueries.Add(ModellingQueries.getNwGroupObjects);
            context.Services.AddSingleton<ApiConnection>(apiConn);
            List<UiMessage> messages = [];

            RenderObjectSearch(context, messages, [], []);

            Assert.That(messages, Has.Some.Matches<UiMessage>(message => message.Title == userConfig.GetText("fetch_data") && message.Error));
        }

        [Test]
        public async Task SearchNwObject_ReportsAFailedSelection()
        {
            await using BunitContext context = CreateContext(out _, out SimulatedUserConfig userConfig);
            userConfig.ModIconify = false;
            userConfig.User.Roles = [Roles.Modeller];
            LibraryApiConnection apiConn = CreateObjectSearchApiConn();
            apiConn.FailingQueries.Add(ModellingQueries.addSelectedNwGroupObject);
            context.Services.AddSingleton<ApiConnection>(apiConn);
            List<UiMessage> messages = [];
            List<ModellingNwGroupWrapper> objectList = [];
            IRenderedComponent<SearchNwObject> component = RenderObjectSearch(context, messages, objectList, []);

            OfferedObjects(component);
            component.FindAll("button.dropdown-item").First(option => option.TextContent.Contains("area3")).Click();
            await component.InvokeAsync(() => ButtonsWithText(component, userConfig.GetText("select")).Single().Click());

            Assert.Multiple(() =>
            {
                Assert.That(messages, Has.Some.Matches<UiMessage>(message => message.Title == userConfig.GetText("fetch_data") && message.Error));
                Assert.That(objectList, Is.Empty);
            });
        }

        private static IRenderedComponent<EditServiceGroupLeftSide> RenderServiceLibrary(BunitContext context, ModellingServiceGroupHandler handler,
            Action<ModellingServiceGroupHandler> handlerChanged)
        {
            return RenderWithMessages<EditServiceGroupLeftSide>(context, [], component => component
                .Add(p => p.SvcGroupHandler, handler)
                .Add(p => p.SvcGroupHandlerChanged, handlerChanged));
        }

        [Test]
        public async Task EditServiceGroupLeftSide_SelectedServiceCanBeUsedEditedAndDeleted()
        {
            await using BunitContext context = CreateContext(out _, out SimulatedUserConfig userConfig);
            userConfig.ModIconify = false;
            LibraryApiConnection apiConn = new();
            ModellingService service = new() { Id = 31, Name = "lib-service", ProtoId = 6, Port = 8443 };
            ModellingServiceGroupHandler handler = CreateServiceGroupHandler(apiConn, userConfig, new ModellingServiceGroup { Id = 40, Name = "group" },
                [service], [new((int)ModellingTypes.ModObjectType.Service, service.Id)], addMode: false);
            int handlerChanges = 0;
            IRenderedComponent<EditServiceGroupLeftSide> component = RenderServiceLibrary(context, handler, _ => handlerChanges++);

            component.FindAll("div.draggable-item").First(item => item.TextContent.Contains("lib-service")).Click();
            await component.InvokeAsync(() => ButtonsWithText(component, userConfig.GetText("to_services_group")).Single().Click());
            component.FindAll("div.draggable-item").First(item => item.TextContent.Contains("lib-service")).Click();
            await component.InvokeAsync(() => ButtonsWithText(component, userConfig.GetText("edit_service")).Single().Click());
            bool editorOpened = handler.EditServiceMode;
            component.FindAll("div.draggable-item").First(item => item.TextContent.Contains("lib-service")).Click();
            await component.InvokeAsync(() => ButtonsWithText(component, userConfig.GetText("delete"))[0].Click());

            Assert.Multiple(() =>
            {
                Assert.That(handler.SvcToAdd.Select(added => added.Id), Is.EqualTo(new List<int> { 31 }));
                Assert.That(editorOpened, Is.True);
                Assert.That(handler.DeleteServiceMode, Is.True);
                Assert.That(handlerChanges, Is.EqualTo(3));
            });
        }

        [Test]
        public async Task EditServiceGroupLeftSide_ServiceDialogsRefreshTheServiceGroup()
        {
            await using BunitContext context = CreateContext(out _, out SimulatedUserConfig userConfig);
            LibraryApiConnection apiConn = new();
            apiConn.Answers[ModellingQueries.getServiceGroupById] = _ => new ModellingServiceGroup { Id = 40, Name = "reloaded-group" };
            ModellingServiceGroupHandler handler = CreateServiceGroupHandler(apiConn, userConfig, new ModellingServiceGroup { Id = 40, Name = "group" }, [], [], addMode: false);
            int handlerChanges = 0;
            IRenderedComponent<EditServiceGroupLeftSide> component = RenderServiceLibrary(context, handler, _ => handlerChanges++);

            await component.InvokeAsync(() => component.FindComponent<EditService>().Instance.RefreshParent());
            await component.InvokeAsync(() => component.FindComponent<ConfirmDelete>().Instance.PerformAction());

            Assert.Multiple(() =>
            {
                Assert.That(handler.ActServiceGroup.Name, Is.EqualTo("reloaded-group"));
                Assert.That(handlerChanges, Is.EqualTo(2));
            });
        }
    }
}
