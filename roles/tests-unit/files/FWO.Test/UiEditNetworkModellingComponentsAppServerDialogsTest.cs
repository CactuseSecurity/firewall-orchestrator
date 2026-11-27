using AngleSharp.Dom;
using Bunit;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Services.Modelling;
using FWO.Ui.Pages.NetworkModelling;
using FWO.Ui.Shared;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Text.Json;

namespace FWO.Test
{
    /// <summary>
    /// Tests of the list of manually maintained app servers and of the app server editor.
    /// </summary>
    internal partial class UiEditNetworkModellingComponentsTest
    {
        private const long kInUseServerId = 202;

        private static readonly string kAppServerTypes = JsonSerializer.Serialize<List<AppServerType>>(
            [new AppServerType { Id = 1, Name = "Physical" }, new AppServerType { Id = 2, Name = "Virtual" }]);

        /// <summary>
        /// Manual app servers of an application in every state: deleted and unused, deleted but in use, active and manual,
        /// active and in use, and active from a CSV import.
        /// </summary>
        private static LibraryApiConnection CreateAppServerListApiConn()
        {
            ModellingAppServer deletedServer = CreateServer(201, "alpha-server", "10.2.0.1/32", deleted: true);
            deletedServer.ImportSource = GlobalConst.kManual;
            ModellingAppServer deletedUsedServer = CreateServer(kInUseServerId, "bravo-server", "10.2.0.2/32", deleted: true);
            deletedUsedServer.ImportSource = GlobalConst.kManual;
            ModellingAppServer activeServer = CreateServer(203, "charlie-server", "10.2.0.3/32");
            activeServer.ImportSource = GlobalConst.kManual;
            ModellingAppServer usedServer = CreateServer(204, "delta-server", "10.2.0.4/32");
            usedServer.ImportSource = GlobalConst.kManual;
            ModellingAppServer csvServer = CreateServer(205, "echo-server", "10.2.0.5/32");
            csvServer.ImportSource = GlobalConst.kCSV_ + "import";
            List<ModellingAppServer> manualServers = [deletedServer, deletedUsedServer, activeServer, usedServer];
            List<ModellingAppServer> csvServers = [csvServer];
            List<ModellingAppRole> usingAppRoles = [new() { Id = 1, Name = "role" }];
            List<ModellingAppRole> noAppRoles = [];
            HashSet<long> deletedIds = [];
            LibraryApiConnection apiConn = new();
            // copies, as the list handler appends the csv servers to the answered manual ones
            apiConn.Answers[ModellingQueries.getAppServersBySource] = variables =>
                (string?)variables?.GetType().GetProperty("importSource")?.GetValue(variables) == GlobalConst.kManual
                    ? manualServers.Where(server => !deletedIds.Contains(server.Id)).ToList()
                    : csvServers.ToList();
            apiConn.Answers[ModellingQueries.getAppRolesForAppServer] = variables =>
                variables?.GetType().GetProperty("id")?.GetValue(variables) is long id && (id == kInUseServerId || id == 204)
                    ? usingAppRoles.ToList()
                    : noAppRoles.ToList();
            apiConn.Answers[ModellingQueries.deleteAppServer] = variables =>
            {
                deletedIds.Add((long)variables!.GetType().GetProperty("id")!.GetValue(variables)!);
                return new ReturnId { AffectedRows = 1 };
            };
            return apiConn;
        }

        private static IRenderedComponent<ManualAppServer> RenderAppServerList(BunitContext context, List<UiMessage> messages)
        {
            return RenderWithMessages<ManualAppServer>(context, messages, component => component
                .Add(p => p.Display, true)
                .Add(p => p.Application, new FwoOwner { Id = 9, Name = "app" }));
        }

        private static List<IElement> ButtonsWithText<TComponent>(IRenderedComponent<TComponent> component, string text) where TComponent : Microsoft.AspNetCore.Components.IComponent
        {
            return [.. component.FindAll("button").Where(button => button.TextContent.Trim() == text)];
        }

        private static IElement RowOf<TComponent>(IRenderedComponent<TComponent> component, string serverName) where TComponent : Microsoft.AspNetCore.Components.IComponent
        {
            return component.FindAll("tbody tr").First(row => row.TextContent.Contains(serverName));
        }

        [Test]
        public async Task ManualAppServer_OffersTheActionsOfEachServerState()
        {
            await using BunitContext context = CreateContext(out _, out SimulatedUserConfig userConfig);
            userConfig.ModIconify = false;
            context.Services.AddSingleton<FWO.Api.Client.ApiConnection>(CreateAppServerListApiConn());

            IRenderedComponent<ManualAppServer> component = RenderAppServerList(context, []);

            component.WaitForAssertion(() =>
            {
                string reactivate = userConfig.GetText("reactivate");
                string delete = userConfig.GetText("delete");
                string edit = userConfig.GetText("edit");
                Assert.That(RowOf(component, "alpha-server").TextContent, Does.Contain(reactivate).And.Contain(delete));
                Assert.That(RowOf(component, "bravo-server").TextContent, Does.Contain(reactivate).And.Not.Contain(delete));
                Assert.That(RowOf(component, "charlie-server").TextContent, Does.Contain(edit).And.Contain(delete));
                Assert.That(RowOf(component, "delta-server").TextContent, Does.Contain(edit).And.Contain(userConfig.GetText("deactivate")));
                Assert.That(RowOf(component, "echo-server").TextContent, Does.Not.Contain(edit).And.Contain(delete));
            });
        }

        [Test]
        public async Task ManualAppServer_EditButtonOpensTheEditor()
        {
            await using BunitContext context = CreateContext(out _, out SimulatedUserConfig userConfig);
            userConfig.ModIconify = false;
            context.Services.AddSingleton<FWO.Api.Client.ApiConnection>(CreateAppServerListApiConn());
            IRenderedComponent<ManualAppServer> component = RenderAppServerList(context, []);
            component.WaitForAssertion(() => Assert.That(component.Markup, Does.Contain("charlie-server")));

            RowOf(component, "charlie-server").QuerySelectorAll("button").First(button => button.TextContent.Trim() == userConfig.GetText("edit")).Click();

            ModellingAppServerListHandler handler = GetPrivateField<ModellingAppServerListHandler>(component.Instance, "appServerListHandler");
            Assert.Multiple(() =>
            {
                Assert.That(handler.EditAppServerMode, Is.True);
                Assert.That(handler.AppServerHandler?.ActAppServer.Name, Is.EqualTo("charlie-server"));
            });
        }

        [Test]
        public async Task ManualAppServer_DeletingAnUnusedServerRemovesIt()
        {
            await using BunitContext context = CreateContext(out _, out SimulatedUserConfig userConfig);
            userConfig.ModIconify = false;
            LibraryApiConnection apiConn = CreateAppServerListApiConn();
            context.Services.AddSingleton<FWO.Api.Client.ApiConnection>(apiConn);
            IRenderedComponent<ManualAppServer> component = RenderAppServerList(context, []);
            component.WaitForAssertion(() => Assert.That(component.Markup, Does.Contain("charlie-server")));

            RowOf(component, "charlie-server").QuerySelectorAll("button").First(button => button.TextContent.Trim() == userConfig.GetText("delete")).Click();
            await component.InvokeAsync(() => component.FindComponent<ConfirmDelete>().Instance.PerformAction());

            component.WaitForAssertion(() =>
            {
                Assert.That(apiConn.VariableOf(ModellingQueries.deleteAppServer, "id"), Is.EqualTo(203L));
                Assert.That(component.Markup, Does.Not.Contain("charlie-server"));
                Assert.That(component.FindComponent<InProgress>().Instance.Display, Is.False);
            });
        }

        [Test]
        public async Task ManualAppServer_DeletedUnusedServerCanBeRemovedForGood()
        {
            await using BunitContext context = CreateContext(out _, out SimulatedUserConfig userConfig);
            userConfig.ModIconify = false;
            context.Services.AddSingleton<FWO.Api.Client.ApiConnection>(CreateAppServerListApiConn());
            IRenderedComponent<ManualAppServer> component = RenderAppServerList(context, []);
            component.WaitForAssertion(() => Assert.That(component.Markup, Does.Contain("alpha-server")));

            RowOf(component, "alpha-server").QuerySelectorAll("button").First(button => button.TextContent.Trim() == userConfig.GetText("delete")).Click();

            ModellingAppServerListHandler handler = GetPrivateField<ModellingAppServerListHandler>(component.Instance, "appServerListHandler");
            Assert.Multiple(() =>
            {
                Assert.That(handler.DeleteAppServerMode, Is.True);
                Assert.That(handler.Message, Does.EndWith("alpha-server?"));
            });
        }

        [Test]
        public async Task ManualAppServer_ReactivatingADeletedServerRestoresIt()
        {
            await using BunitContext context = CreateContext(out _, out SimulatedUserConfig userConfig);
            userConfig.ModIconify = false;
            LibraryApiConnection apiConn = CreateAppServerListApiConn();
            context.Services.AddSingleton<FWO.Api.Client.ApiConnection>(apiConn);
            IRenderedComponent<ManualAppServer> component = RenderAppServerList(context, []);
            component.WaitForAssertion(() => Assert.That(component.Markup, Does.Contain("alpha-server")));

            RowOf(component, "alpha-server").QuerySelectorAll("button").First(button => button.TextContent.Trim() == userConfig.GetText("reactivate")).Click();
            await component.InvokeAsync(() => component.FindComponent<Confirm>().Instance.PerformAction());

            component.WaitForAssertion(() =>
            {
                Assert.That(apiConn.VariableOf(ModellingQueries.setAppServerDeletedState, "id"), Is.EqualTo(201L));
                Assert.That(apiConn.VariableOf(ModellingQueries.setAppServerDeletedState, "deleted"), Is.EqualTo(false));
                Assert.That(component.FindComponent<InProgress>().Instance.Display, Is.False);
            });
        }

        [Test]
        public async Task ManualAppServer_ReportsInvalidAppServerTypes()
        {
            await using BunitContext context = CreateContext(out _, out SimulatedUserConfig userConfig);
            userConfig.ModAppServerTypes = "no json";
            List<UiMessage> messages = [];

            RenderAppServerList(context, messages);

            Assert.That(messages, Has.Some.Matches<UiMessage>(message => message.Title == userConfig.GetText("fetch_data") && message.Error));
        }

        private static IRenderedComponent<EditAppServer> RenderAppServerEditor(BunitContext context, ModellingAppServerHandler handler, List<UiMessage> messages,
            Action<bool>? displayChanged = null)
        {
            return RenderWithMessages<EditAppServer>(context, messages, component => component
                .Add(p => p.Display, true)
                .Add(p => p.DisplayChanged, value => displayChanged?.Invoke(value))
                .Add(p => p.AppServerHandler, handler));
        }

        private static ModellingAppServerHandler CreateEditableAppServerHandler(FWO.Api.Client.ApiConnection apiConn, SimulatedUserConfig userConfig, string ip = "10.0.0.1/32")
        {
            userConfig.ModIconify = false;
            userConfig.ModAppServerTypes = kAppServerTypes;
            return new ModellingAppServerHandler(apiConn, userConfig, new FwoOwner { Id = 42 },
                new ModellingAppServer { Name = "srv1", Ip = ip, IpEnd = ip, CustomType = 1, ImportSource = GlobalConst.kManual },
                [], true, (_, _, _, _) => { }, false, true);
        }

        [Test]
        public async Task EditAppServer_TypeCanBeChosen()
        {
            await using BunitContext context = CreateContext(out _, out SimulatedUserConfig userConfig);
            ModellingAppServerHandler handler = CreateEditableAppServerHandler(new LibraryApiConnection(), userConfig);
            IRenderedComponent<EditAppServer> component = RenderAppServerEditor(context, handler, []);

            component.Find("input[id^='dropdown-input-']").Focus();
            component.FindAll("button.dropdown-item").First(option => option.TextContent.Trim() == "Virtual").Click();

            Assert.That(handler.ActAppServer.CustomType, Is.EqualTo(2));
        }

        [Test]
        public async Task EditAppServer_AdminSavesWithTheButton()
        {
            await using BunitContext context = CreateContext(out RecordingApiConnection apiConn, out SimulatedUserConfig userConfig);
            userConfig.User.Roles = [Roles.Admin];
            ModellingAppServerHandler handler = CreateEditableAppServerHandler(apiConn, userConfig);
            bool displayChanged = true;
            IRenderedComponent<EditAppServer> component = RenderAppServerEditor(context, handler, [], value => displayChanged = value);

            await component.InvokeAsync(() => ButtonsWithText(component, userConfig.GetText("save")).Single().Click());

            Assert.Multiple(() =>
            {
                Assert.That(apiConn.NewAppServerCalls, Is.EqualTo(1));
                Assert.That(displayChanged, Is.False);
            });
        }

        [Test]
        public async Task EditAppServer_GeneratesTheNameOfAnIpRange()
        {
            await using BunitContext context = CreateContext(out _, out SimulatedUserConfig userConfig);
            userConfig.DnsLookup = true;
            ModellingAppServerHandler handler = CreateEditableAppServerHandler(new LibraryApiConnection(), userConfig);
            IRenderedComponent<EditAppServer> component = RenderAppServerEditor(context, handler, []);
            // a range has no reverse dns entry, so the name is built from the naming convention
            component.Find("input[type='text']").Change("10.0.0.1-10.0.0.9");

            await component.InvokeAsync(() => ButtonsWithText(component, userConfig.GetText("generate_name")).Single().Click());

            Assert.Multiple(() =>
            {
                Assert.That(handler.ActAppServer.Ip, Does.StartWith("10.0.0.1"));
                Assert.That(handler.ActAppServer.IpEnd, Does.StartWith("10.0.0.9"));
                Assert.That(handler.ActAppServer.Name, Is.Not.EqualTo("srv1"));
                Assert.That(component.FindComponent<InProgress>().Instance.Display, Is.False);
            });
        }

        [Test]
        public async Task EditAppServer_ReportsAFailedNameLookupWhileSaving()
        {
            await using BunitContext context = CreateContext(out _, out SimulatedUserConfig userConfig);
            userConfig.DnsLookup = true;
            userConfig.User.Roles = [Roles.Admin];
            ModellingAppServerHandler handler = CreateEditableAppServerHandler(new LibraryApiConnection(), userConfig);
            List<UiMessage> messages = [];
            IRenderedComponent<EditAppServer> component = RenderAppServerEditor(context, handler, messages);
            component.Find("input[type='text']").Change("10.0.0.1/255.255.0.999");

            await component.InvokeAsync(() => ButtonsWithText(component, userConfig.GetText("save")).Single().Click());

            Assert.That(messages, Has.Some.Matches<UiMessage>(message => message.Title == userConfig.GetText("dns_lookup") && message.Error));
        }

        [Test]
        public async Task EditAppServer_ReportsAnInvalidIpWhileSaving()
        {
            await using BunitContext context = CreateContext(out RecordingApiConnection apiConn, out SimulatedUserConfig userConfig);
            userConfig.User.Roles = [Roles.Admin];
            ModellingAppServerHandler handler = CreateEditableAppServerHandler(apiConn, userConfig);
            List<UiMessage> messages = [];
            bool displayChanged = true;
            IRenderedComponent<EditAppServer> component = RenderAppServerEditor(context, handler, messages, value => displayChanged = value);
            component.Find("input[type='text']").Change("10.0.0.1/255.255.0.999");

            await component.InvokeAsync(() => ButtonsWithText(component, userConfig.GetText("save")).Single().Click());

            Assert.Multiple(() =>
            {
                Assert.That(messages, Has.Some.Matches<UiMessage>(message => message.Title == userConfig.GetText("save_app_server") && message.Error));
                Assert.That(apiConn.NewAppServerCalls, Is.Zero);
                Assert.That(displayChanged, Is.True, "the editor stays open");
            });
        }

        [Test]
        public async Task EditAppServer_ReportsInvalidAppServerTypes()
        {
            await using BunitContext context = CreateContext(out _, out SimulatedUserConfig userConfig);
            ModellingAppServerHandler handler = CreateEditableAppServerHandler(new LibraryApiConnection(), userConfig);
            userConfig.ModAppServerTypes = "no json";
            List<UiMessage> messages = [];

            RenderAppServerEditor(context, handler, messages);

            Assert.That(messages, Has.Some.Matches<UiMessage>(message => message.Title == userConfig.GetText("fetch_data") && message.Error));
        }
    }
}
