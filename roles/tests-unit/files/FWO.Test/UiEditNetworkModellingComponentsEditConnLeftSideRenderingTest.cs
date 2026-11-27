using AngleSharp.Dom;
using Bunit;
using FWO.Api.Client;
using FWO.Basics;
using FWO.Config.Api.Data;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Services.EventMediator.Events;
using FWO.Services.EventMediator.Interfaces;
using FWO.Services.Modelling;
using FWO.Ui.Pages.NetworkModelling;
using FWO.Ui.Services;
using FWO.Ui.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Tests of the rendered object library of the connection editor: what it lists and what its buttons do.
    /// </summary>
    internal partial class UiEditNetworkModellingComponentsTest
    {
        private const long kLibRoleId = 101;
        private const long kEmptyRoleId = 102;
        private const long kCommonAreaId = 103;
        private const long kSelectedAreaId = 104;
        private const long kAppZoneId = 105;
        private const long kLibServerId = 106;
        private const long kLibGroupId = 107;
        private const int kGlobalGroupId = 201;
        private const int kOwnGroupId = 202;
        private const int kOwnServiceId = 203;
        private const int kLibInterfaceId = 301;

        /// <summary>
        /// Answers every query with an empty result, so actions that load or store data can run. Single queries can be
        /// answered explicitly, can fail, and all queries can be held back until a gate is opened.
        /// </summary>
        private sealed class LibraryApiConnection : SimulatedApiConnection
        {
            public List<string> Queries { get; } = [];
            public List<object?> Variables { get; } = [];
            public Dictionary<string, Func<object?, object>> Answers { get; } = [];
            public HashSet<string> FailingQueries { get; } = [];
            public TaskCompletionSource? Gate { get; set; }

            public override async Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null,
                QueryChunkingOptions? chunkingOptions = null)
            {
                Queries.Add(query);
                Variables.Add(variables);
                if (Gate != null)
                {
                    await Gate.Task;
                }
                if (FailingQueries.Contains(query))
                {
                    throw new InvalidOperationException($"query failed: {query}");
                }
                if (Answers.TryGetValue(query, out Func<object?, object>? answer))
                {
                    return (QueryResponseType)answer(variables);
                }
                object? emptyAnswer = typeof(QueryResponseType).IsArray
                    ? Array.CreateInstance(typeof(QueryResponseType).GetElementType()!, 0)
                    : Activator.CreateInstance<QueryResponseType>();
                return (QueryResponseType)emptyAnswer!;
            }

            public object? VariableOf(string query, string name)
            {
                int index = Queries.IndexOf(query);
                return index < 0 ? null : Variables[index]?.GetType().GetProperty(name)?.GetValue(Variables[index]);
            }
        }

        /// <summary>
        /// Renders a component with a cascading message sink, so also messages of its initialization are captured.
        /// </summary>
        private static IRenderedComponent<TComponent> RenderWithMessages<TComponent>(BunitContext context, List<UiMessage> messages,
            Action<ComponentParameterCollectionBuilder<TComponent>> parameters) where TComponent : IComponent
        {
            Action<Exception?, string, string, bool> displayMessage = (_, title, text, error) => messages.Add(new(title, text, error));
            return context.Render<CascadingAuthenticationState>(wrapper => wrapper
                .AddChildContent<CascadingValue<Action<Exception?, string, string, bool>>>(cascade => cascade
                    .Add(value => value.Value, displayMessage)
                    .AddChildContent(parameters)))
                .FindComponent<TComponent>();
        }

        private static ModellingConnectionHandler CreateLibraryHandler(LibraryApiConnection apiConn, SimulatedUserConfig userConfig, ModellingConnection? connection = null)
        {
            userConfig.ModIconify = false;
            userConfig.AllowServiceInConn = true;
            userConfig.User.Roles = [Roles.Modeller];
            ModellingConnectionHandler handler = CreateConnectionHandler(apiConn, userConfig, connection ?? new ModellingConnection { Id = 150, Name = "conn", Reason = "reason" });
            handler.Application.Name = "library-app";
            handler.Application.CommSvcPossible = true;
            handler.AvailableAppRoles =
            [
                new ModellingAppRole
                {
                    Id = kLibRoleId,
                    Name = "lib-role",
                    IdString = "AR101",
                    AppServers = [new ModellingAppServerWrapper { Content = CreateServer(111, "role-server", "10.1.1.1/32") }]
                },
                new ModellingAppRole { Id = kEmptyRoleId, Name = "empty-role", IdString = "AR102" }
            ];
            handler.AvailableCommonAreas = [new ModellingNetworkAreaWrapper { Content = CreateArea(kCommonAreaId, "NA103", "common-area", "10.3.0.0", "10.3.0.255") }];
            handler.AvailableSelectedObjects =
            [
                new ModellingNwGroupWrapper { Content = new ModellingNwGroup { Id = kSelectedAreaId, Name = "selected-area", IdString = "NA104" } },
                new ModellingNwGroupWrapper { Content = new ModellingNwGroup { Id = kAppZoneId, Name = "app-zone", IdString = "AZ105" } }
            ];
            handler.AvailableNwGroups = [new ModellingNwGroupWrapper { Content = new ModellingNwGroup { Id = kLibGroupId, Name = "lib-group", IdString = "AZ107" } }];
            handler.AvailableAppServers = [CreateServer(kLibServerId, "lib-server", "10.1.0.6/32")];
            handler.AvailableNwElems =
            [
                new((int)ModellingTypes.ModObjectType.AppRole, kLibRoleId),
                new((int)ModellingTypes.ModObjectType.AppRole, kEmptyRoleId),
                new((int)ModellingTypes.ModObjectType.NetworkArea, kCommonAreaId),
                new((int)ModellingTypes.ModObjectType.NetworkArea, kSelectedAreaId),
                new((int)ModellingTypes.ModObjectType.AppZone, kAppZoneId),
                new((int)ModellingTypes.ModObjectType.AppZone, kLibGroupId),
                new((int)ModellingTypes.ModObjectType.AppServer, kLibServerId)
            ];
            handler.AvailableServiceGroups =
            [
                new ModellingServiceGroup
                {
                    Id = kGlobalGroupId,
                    Name = "global-group",
                    IsGlobal = true,
                    Services = [new ModellingServiceWrapper { Content = new ModellingService { Id = 204, Name = "group-svc" } }]
                },
                new ModellingServiceGroup { Id = kOwnGroupId, Name = "own-group", IsGlobal = false }
            ];
            handler.AvailableServices = [new ModellingService { Id = kOwnServiceId, Name = "own-svc", IsGlobal = false, ProtoId = 6, Port = 8080 }];
            handler.AvailableSvcElems =
            [
                new((int)ModellingTypes.ModObjectType.ServiceGroup, kGlobalGroupId),
                new((int)ModellingTypes.ModObjectType.ServiceGroup, kOwnGroupId),
                new((int)ModellingTypes.ModObjectType.Service, kOwnServiceId)
            ];
            handler.PreselectedInterfaces = [new ModellingConnection { Id = kLibInterfaceId, AppId = 17, Name = "lib-interface", IsInterface = true }];
            return handler;
        }

        private static IRenderedComponent<EditConnLeftSide> RenderLibrary(BunitContext context, ModellingConnectionHandler handler,
            bool overviewMode = false, Action<ModellingConnectionHandler>? handlerChanged = null)
        {
            // the sidebar shows its content only after the height of the navbar is known
            context.Services.GetRequiredService<DomEventService>().InvokeNavbarHeightChanged(50);
            return context.Render<CascadingAuthenticationState>(parameters => parameters.AddChildContent<EditConnLeftSide>(component => component
                .Add(p => p.Container, new ModellingDnDContainer())
                .Add(p => p.Width, GlobalConst.kObjLibraryWidth)
                .Add(p => p.ConnHandler, handler)
                .Add(p => p.OverviewMode, overviewMode)
                .Add(p => p.ConnHandlerChanged, value => handlerChanged?.Invoke(value))))
                .FindComponent<EditConnLeftSide>();
        }

        private static void SelectLibraryItem(IRenderedComponent<EditConnLeftSide> component, string name)
        {
            component.FindAll("div.draggable-item").First(item => item.TextContent.Contains(name)).Click();
        }

        private static List<IElement> LibraryButtons(IRenderedComponent<EditConnLeftSide> component, string text)
        {
            return [.. component.FindAll("button").Where(button => button.TextContent.Trim() == text)];
        }

        /// <summary>
        /// Clicks a button of the library. The library is rendered before the dialogs it opens, so an opened dialog with
        /// a button of the same text, e.g. the service group editor, does not hide the library button.
        /// </summary>
        private static async Task ClickLibraryButton(IRenderedComponent<EditConnLeftSide> component, string text)
        {
            await component.InvokeAsync(() => LibraryButtons(component, text)[0].Click());
        }

        private static void SelectNwElements(IRenderedComponent<EditConnLeftSide> component, params (ModellingTypes.ModObjectType Type, long Id)[] elements)
        {
            SetPrivateField(component.Instance, "selectedNwElems", elements.Select(element => new KeyValuePair<int, long>((int)element.Type, element.Id)).ToList());
            component.Render();
        }

        [Test]
        public async Task EditConnLeftSide_ListsTheLibraryObjects()
        {
            await using BunitContext context = CreateContext(out _, out SimulatedUserConfig userConfig);
            ModellingConnectionHandler handler = CreateLibraryHandler(new LibraryApiConnection(), userConfig);

            IRenderedComponent<EditConnLeftSide> component = RenderLibrary(context, handler);

            Assert.Multiple(() =>
            {
                foreach (string name in kLibraryObjectNames)
                {
                    Assert.That(component.Markup, Does.Contain(name), name);
                }
                Assert.That(component.Markup, Does.Contain("library-app"));
                Assert.That(handler.AvailableAppRoles[0].AppServers[0].Content.TooltipText, Is.EqualTo(userConfig.GetText("C9000")),
                    "the app servers of an app role get a tooltip");
                Assert.That(component.FindAll("div.col-sm-10"), Is.Not.Empty);
            });
        }

        private static readonly string[] kLibraryObjectNames =
            ["lib-role", "empty-role", "common-area", "selected-area", "app-zone", "lib-group", "lib-server", "global-group", "own-group", "own-svc", "lib-interface"];

        [Test]
        public async Task EditConnLeftSide_DisplayFunctionsHandleUnknownObjects()
        {
            await using BunitContext context = CreateContext(out _, out SimulatedUserConfig userConfig);
            ModellingConnectionHandler handler = CreateLibraryHandler(new LibraryApiConnection(), userConfig);
            handler.AvailableNwElems.Add(new((int)ModellingTypes.ModObjectType.Network, 999));
            handler.AvailableSvcElems.Add(new((int)ModellingTypes.ModObjectType.AppServer, 998));
            IRenderedComponent<EditConnLeftSide> component = RenderLibrary(context, handler);

            string notInLibrary = component.Instance.DisplayNwObject(new((int)ModellingTypes.ModObjectType.AppRole, 12345));
            string unknownNwKind = component.Instance.DisplayNwObject(new((int)ModellingTypes.ModObjectType.Network, 999));
            string unknownSvcKind = component.Instance.DisplaySvcObject(new((int)ModellingTypes.ModObjectType.AppServer, 998));
            handler.AvailableNwElems = [];
            string emptyLibrary = component.Instance.DisplayNwObject(new((int)ModellingTypes.ModObjectType.AppRole, kLibRoleId));

            Assert.Multiple(() =>
            {
                Assert.That(notInLibrary, Is.Empty);
                Assert.That(emptyLibrary, Is.Empty);
                Assert.That(unknownNwKind, Is.EqualTo(new ModellingNwObject().DisplayWithIcon()));
                Assert.That(unknownSvcKind, Is.EqualTo(new ModellingSvcObject().DisplayWithIcon()));
            });
        }

        [Test]
        public async Task EditConnLeftSide_SelectedAppRoleCanBeUsedEditedAndDeleted()
        {
            await using BunitContext context = CreateContext(out _, out SimulatedUserConfig userConfig);
            LibraryApiConnection apiConn = new();
            ModellingConnectionHandler handler = CreateLibraryHandler(apiConn, userConfig);
            int handlerChanges = 0;
            IRenderedComponent<EditConnLeftSide> component = RenderLibrary(context, handler, handlerChanged: _ => handlerChanges++);

            SelectLibraryItem(component, "lib-role");
            await ClickLibraryButton(component, userConfig.GetText("to_source"));
            SelectLibraryItem(component, "lib-role");
            await ClickLibraryButton(component, userConfig.GetText("edit"));
            SelectLibraryItem(component, "lib-role");
            await ClickLibraryButton(component, userConfig.GetText("delete"));

            Assert.Multiple(() =>
            {
                Assert.That(handler.SrcAppRolesToAdd.Select(role => role.Id), Is.EqualTo(new List<long> { kLibRoleId }));
                Assert.That(handler.EditAppRoleMode, Is.True);
                Assert.That(handler.DeleteAppRoleMode, Is.True);
                Assert.That(handler.DeleteAllowed, Is.False, "the app role was just added to the connection");
                Assert.That(handlerChanges, Is.EqualTo(3));
                Assert.That(GetPrivateField<List<KeyValuePair<int, long>>>(component.Instance, "selectedNwElems"), Is.Empty);
            });
        }

        [Test]
        public async Task EditConnLeftSide_SelectedObjectsMoveToTheDestination()
        {
            await using BunitContext context = CreateContext(out _, out SimulatedUserConfig userConfig);
            ModellingConnectionHandler handler = CreateLibraryHandler(new LibraryApiConnection(), userConfig);
            handler.CommonAreaConfigItems = [new CommonAreaConfig { AreaId = kSelectedAreaId, UseInSrc = true, UseInDst = true }];
            IRenderedComponent<EditConnLeftSide> component = RenderLibrary(context, handler);
            SelectNwElements(component,
                (ModellingTypes.ModObjectType.AppRole, kLibRoleId),
                (ModellingTypes.ModObjectType.NetworkArea, kSelectedAreaId),
                (ModellingTypes.ModObjectType.AppZone, kAppZoneId),
                (ModellingTypes.ModObjectType.AppServer, kLibServerId));

            await ClickLibraryButton(component, userConfig.GetText("to_dest"));

            Assert.Multiple(() =>
            {
                Assert.That(handler.DstAppRolesToAdd.Select(role => role.Id), Is.EqualTo(new List<long> { kLibRoleId }));
                Assert.That(handler.DstAreasToAdd.Select(area => area.Id), Is.EqualTo(new List<long> { kSelectedAreaId }));
                Assert.That(handler.DstNwGroupsToAdd.Select(group => group.Id), Is.EqualTo(new List<long> { kAppZoneId }));
                Assert.That(handler.DstAppServerToAdd.Select(server => server.Id), Is.EqualTo(new List<long> { kLibServerId }));
            });
        }

        [TestCase(ModellingTypes.ModObjectType.AppRole, kLibRoleId, "U9031")]
        [TestCase(ModellingTypes.ModObjectType.NetworkArea, kSelectedAreaId, "U9031")]
        public async Task EditConnLeftSide_CommonServiceRejectsMixingAppRolesAndAreas(ModellingTypes.ModObjectType type, long id, string expectedText)
        {
            await using BunitContext context = CreateContext(out _, out SimulatedUserConfig userConfig);
            ModellingConnection connection = new() { Id = 151, Name = "common", Reason = "reason", IsCommonService = true };
            if (type == ModellingTypes.ModObjectType.AppRole)
            {
                connection.SourceAreas = [new ModellingNetworkAreaWrapper { Content = CreateArea(8, "NA8", "area", "10.8.0.0", "10.8.0.255") }];
            }
            else
            {
                connection.SourceAppRoles = [new ModellingAppRoleWrapper { Content = CreateAppRole(3, "role") }];
            }
            ModellingConnectionHandler handler = CreateLibraryHandler(new LibraryApiConnection(), userConfig, connection);
            IRenderedComponent<EditConnLeftSide> component = RenderLibrary(context, handler);
            List<UiMessage> messages = CaptureLeftSideMessages(component);
            SelectNwElements(component, (type, id));

            await ClickLibraryButton(component, userConfig.GetText("to_source"));

            Assert.Multiple(() =>
            {
                Assert.That(messages, Is.EqualTo(new List<UiMessage> { new(userConfig.GetText("edit_connection"), userConfig.GetText(expectedText), true) }));
                Assert.That(GetPrivateField<List<KeyValuePair<int, long>>>(component.Instance, "selectedNwElems"), Is.Empty);
            });
        }

        [Test]
        public async Task EditConnLeftSide_AreasAreRejectedForAConnectionUsingAForeignInterface()
        {
            await using BunitContext context = CreateContext(out _, out SimulatedUserConfig userConfig);
            ModellingConnectionHandler handler = CreateLibraryHandler(new LibraryApiConnection(), userConfig,
                new ModellingConnection { Id = 152, AppId = 17, Name = "conn", Reason = "reason", UsedInterfaceId = 99 });
            IRenderedComponent<EditConnLeftSide> component = RenderLibrary(context, handler);
            List<UiMessage> messages = CaptureLeftSideMessages(component);
            SelectNwElements(component, (ModellingTypes.ModObjectType.NetworkArea, kSelectedAreaId));

            await ClickLibraryButton(component, userConfig.GetText("to_source"));

            Assert.That(messages, Is.EqualTo(new List<UiMessage> { new(userConfig.GetText("edit_connection"), userConfig.GetText("U9024"), true) }));
        }

        [Test]
        public async Task EditConnLeftSide_AreasAreRejectedOppositeToAnArea()
        {
            await using BunitContext context = CreateContext(out _, out SimulatedUserConfig userConfig);
            ModellingConnectionHandler handler = CreateLibraryHandler(new LibraryApiConnection(), userConfig, new ModellingConnection
            {
                Id = 153,
                Name = "conn",
                Reason = "reason",
                SourceAreas = [new ModellingNetworkAreaWrapper { Content = CreateArea(8, "NA8", "area", "10.8.0.0", "10.8.0.255") }]
            });
            IRenderedComponent<EditConnLeftSide> component = RenderLibrary(context, handler);
            List<UiMessage> messages = CaptureLeftSideMessages(component);
            SelectNwElements(component, (ModellingTypes.ModObjectType.NetworkArea, kSelectedAreaId));

            await ClickLibraryButton(component, userConfig.GetText("to_dest"));

            Assert.That(messages, Is.EqualTo(new List<UiMessage> { new(userConfig.GetText("edit_connection"), userConfig.GetText("U9022"), true) }));
        }

        private static List<UiMessage> CaptureLeftSideMessages(IRenderedComponent<EditConnLeftSide> component)
        {
            List<UiMessage> messages = [];
            SetPrivateProperty(component.Instance, "DisplayMessageInUi",
                new Action<Exception?, string, string, bool>((_, title, text, error) => messages.Add(new(title, text, error))));
            return messages;
        }

        [TestCase("selected-area")]
        [TestCase("app-zone")]
        public async Task EditConnLeftSide_SelectedObjectCanBeRemovedFromTheLibrary(string name)
        {
            await using BunitContext context = CreateContext(out _, out SimulatedUserConfig userConfig);
            ModellingConnectionHandler handler = CreateLibraryHandler(new LibraryApiConnection(), userConfig);
            IRenderedComponent<EditConnLeftSide> component = RenderLibrary(context, handler);

            SelectLibraryItem(component, name);
            await ClickLibraryButton(component, userConfig.GetText("remove"));

            Assert.That(handler.RemoveNwObjectMode, Is.True);
        }

        [TestCase("common-area")]
        [TestCase("lib-group")]
        public async Task EditConnLeftSide_PredefinedObjectCannotBeRemovedFromTheLibrary(string name)
        {
            await using BunitContext context = CreateContext(out _, out SimulatedUserConfig userConfig);
            ModellingConnectionHandler handler = CreateLibraryHandler(new LibraryApiConnection(), userConfig);
            IRenderedComponent<EditConnLeftSide> component = RenderLibrary(context, handler);

            SelectLibraryItem(component, name);

            Assert.That(LibraryButtons(component, userConfig.GetText("remove")), Is.Empty);
        }

        [Test]
        public async Task EditConnLeftSide_ServicesCanBeUsedEditedAndDeleted()
        {
            await using BunitContext context = CreateContext(out _, out SimulatedUserConfig userConfig);
            LibraryApiConnection apiConn = new();
            ModellingConnectionHandler handler = CreateLibraryHandler(apiConn, userConfig);
            IRenderedComponent<EditConnLeftSide> component = RenderLibrary(context, handler);

            SelectLibraryItem(component, "own-svc");
            await ClickLibraryButton(component, userConfig.GetText("to_service"));
            SelectLibraryItem(component, "own-svc");
            await ClickLibraryButton(component, userConfig.GetText("edit_service"));
            SelectLibraryItem(component, "own-svc");
            await ClickLibraryButton(component, userConfig.GetText("delete"));

            Assert.Multiple(() =>
            {
                Assert.That(handler.SvcToAdd.Select(service => service.Id), Is.EqualTo(new List<int> { kOwnServiceId }));
                Assert.That(handler.EditServiceMode, Is.True);
                Assert.That(GetPrivateField<List<KeyValuePair<int, int>>>(component.Instance, "selectedSvcElems"), Is.Empty);
            });
        }

        [Test]
        public async Task EditConnLeftSide_ServiceGroupsCanBeUsedDisplayedEditedAndDeleted()
        {
            await using BunitContext context = CreateContext(out _, out SimulatedUserConfig userConfig);
            ModellingConnectionHandler handler = CreateLibraryHandler(new LibraryApiConnection(), userConfig);
            IRenderedComponent<EditConnLeftSide> component = RenderLibrary(context, handler);

            SetPrivateField(component.Instance, "selectedSvcElems", new List<KeyValuePair<int, int>>
            {
                new((int)ModellingTypes.ModObjectType.ServiceGroup, kGlobalGroupId),
                new((int)ModellingTypes.ModObjectType.Service, kOwnServiceId)
            });
            component.Render();
            await ClickLibraryButton(component, userConfig.GetText("to_service"));
            SelectLibraryItem(component, "global-group");
            await ClickLibraryButton(component, userConfig.GetText("display_service_group"));
            bool displayedGlobalGroup = handler.DisplaySvcGrpMode;
            SelectLibraryItem(component, "own-group");
            await ClickLibraryButton(component, userConfig.GetText("edit_service_group"));
            SelectLibraryItem(component, "own-group");
            await ClickLibraryButton(component, userConfig.GetText("delete"));

            Assert.Multiple(() =>
            {
                Assert.That(handler.SvcGrpToAdd.Select(group => group.Id), Is.EqualTo(new List<long> { kGlobalGroupId }));
                Assert.That(handler.SvcToAdd.Select(service => service.Id), Is.EqualTo(new List<int> { kOwnServiceId }));
                Assert.That(displayedGlobalGroup, Is.True);
                Assert.That(handler.EditSvcGrpMode, Is.True);
            });
        }

        [Test]
        public async Task EditConnLeftSide_CreateButtonsOpenTheEditors()
        {
            await using BunitContext context = CreateContext(out _, out SimulatedUserConfig userConfig);
            ModellingConnectionHandler handler = CreateLibraryHandler(new LibraryApiConnection(), userConfig);
            IRenderedComponent<EditConnLeftSide> component = RenderLibrary(context, handler);

            await ClickLibraryButton(component, userConfig.GetText("add_app_role"));
            bool appRoleEditorOpened = handler.AddAppRoleMode;
            await ClickLibraryButton(component, userConfig.GetText("add_service_group"));
            bool serviceGroupEditorOpened = handler.AddSvcGrpMode;
            await ClickLibraryButton(component, userConfig.GetText("add_service"));
            await ClickLibraryButton(component, userConfig.GetText("search_nw_object"));
            await ClickLibraryButton(component, userConfig.GetText("search"));
            await ClickLibraryButton(component, userConfig.GetText("request_interface"));

            Assert.Multiple(() =>
            {
                Assert.That(appRoleEditorOpened, Is.True);
                Assert.That(serviceGroupEditorOpened, Is.True);
                Assert.That(handler.EditServiceMode, Is.True);
                Assert.That(GetPrivateField<bool>(component.Instance, "SearchNwObjectMode"), Is.True);
                Assert.That(GetPrivateField<bool>(component.Instance, "SearchInterfaceMode"), Is.True);
                Assert.That(GetPrivateField<bool>(component.Instance, "SelectAppMode"), Is.True);
            });
        }

        [Test]
        public async Task EditConnLeftSide_SelectedInterfaceCanBeUsed()
        {
            await using BunitContext context = CreateContext(out _, out SimulatedUserConfig userConfig);
            ModellingConnectionHandler handler = CreateLibraryHandler(new LibraryApiConnection(), userConfig);
            IRenderedComponent<EditConnLeftSide> component = RenderLibrary(context, handler);

            SelectLibraryItem(component, "lib-interface");
            await ClickLibraryButton(component, userConfig.GetText("use"));

            Assert.That(handler.ActConn.UsedInterfaceId, Is.EqualTo(kLibInterfaceId));
        }

        [Test]
        public async Task EditConnLeftSide_SelectedInterfaceCanBeDisplayedAndRemoved()
        {
            await using BunitContext context = CreateContext(out _, out SimulatedUserConfig userConfig);
            ModellingConnectionHandler handler = CreateLibraryHandler(new LibraryApiConnection(), userConfig);
            IRenderedComponent<EditConnLeftSide> component = RenderLibrary(context, handler);

            SelectLibraryItem(component, "lib-interface");
            await ClickLibraryButton(component, userConfig.GetText("display_interface"));
            SelectLibraryItem(component, "lib-interface");
            await ClickLibraryButton(component, userConfig.GetText("remove"));

            Assert.Multiple(() =>
            {
                Assert.That(handler.DisplaySelectedInterfaceMode, Is.True);
                Assert.That(handler.RemovePreselectedInterfaceMode, Is.True);
                Assert.That(GetPrivateField<List<ModellingConnection>>(component.Instance, "selectedInterfaces"), Is.Empty);
            });
        }

        [Test]
        public async Task EditConnLeftSide_RequestedInterfaceCanBeReplacedByAnOwnInterface()
        {
            await using BunitContext context = CreateContext(out _, out SimulatedUserConfig userConfig);
            ModellingConnectionHandler handler = CreateLibraryHandler(new LibraryApiConnection(), userConfig,
                new ModellingConnection { Id = 154, Name = "requested", Reason = "reason", IsInterface = true, IsRequested = true });
            handler.Connections.Add(new ModellingConnection { Id = 155, AppId = 17, Name = "own-published", IsInterface = true });
            IRenderedComponent<EditConnLeftSide> component = RenderLibrary(context, handler);

            SelectLibraryItem(component, "own-published");
            await ClickLibraryButton(component, userConfig.GetText("replace"));

            Assert.Multiple(() =>
            {
                Assert.That(handler.ReplaceMode, Is.True);
                Assert.That(handler.IntConnHandler?.ActConn.Id, Is.EqualTo(155));
            });
        }

        [Test]
        public async Task EditConnLeftSide_OverviewModeOnlyListsTheLibrary()
        {
            await using BunitContext context = CreateContext(out _, out SimulatedUserConfig userConfig);
            ModellingConnectionHandler handler = CreateLibraryHandler(new LibraryApiConnection(), userConfig);

            IRenderedComponent<EditConnLeftSide> component = RenderLibrary(context, handler, overviewMode: true);
            SelectLibraryItem(component, "lib-server");

            Assert.Multiple(() =>
            {
                Assert.That(LibraryButtons(component, userConfig.GetText("to_source")), Is.Empty);
                Assert.That(component.FindAll("div.col-sm-12 > div.bg-secondary"), Is.Not.Empty);
            });
        }

        [Test]
        public async Task EditConnLeftSide_RequestInterfacePopupOffersOnlyRequestableApps()
        {
            await using BunitContext context = CreateContext(out _, out SimulatedUserConfig userConfig);
            ModellingConnectionHandler handler = CreateLibraryHandler(new LibraryApiConnection(), userConfig);
            handler.AllApps = [new FwoOwner { Id = 2, Name = "WithIps" }, new FwoOwner { Id = 3, Name = "WithoutIps" }];
            handler.RequestableApps = [handler.AllApps[0]];

            IRenderedComponent<EditConnLeftSide> component = RenderLibrary(context, handler);

            Assert.That(component.FindComponent<RequestInterfacePopup>().Instance.Apps, Is.SameAs(handler.RequestableApps));
        }

        [Test]
        public async Task EditConnLeftSide_ConfirmationsAndRefreshesReportTheChangedHandler()
        {
            await using BunitContext context = CreateContext(out _, out SimulatedUserConfig userConfig);
            ModellingConnectionHandler handler = CreateLibraryHandler(new LibraryApiConnection(), userConfig);
            int handlerChanges = 0;
            IRenderedComponent<EditConnLeftSide> component = RenderLibrary(context, handler, handlerChanged: _ => handlerChanges++);

            foreach (IRenderedComponent<ConfirmDelete> confirmDelete in component.FindComponents<ConfirmDelete>())
            {
                await component.InvokeAsync(() => confirmDelete.Instance.PerformAction());
            }
            await component.InvokeAsync(() => component.FindComponent<RequestInterfacePopup>().Instance.RefreshParent());
            await component.InvokeAsync(() => component.FindComponent<EditAppRole>().Instance.RefreshParent());
            await component.InvokeAsync(() => component.FindComponent<EditService>().Instance.RefreshParent());

            Assert.Multiple(() =>
            {
                Assert.That(handlerChanges, Is.EqualTo(8));
                Assert.That(handler.ActConnNeedsRefresh, Is.True);
            });
        }

        [Test]
        public async Task EditConnLeftSide_ReloadsTheLibraryWhenAppServersChangeOrTheSearchCloses()
        {
            await using BunitContext context = CreateContext(out _, out SimulatedUserConfig userConfig);
            LibraryApiConnection apiConn = new();
            ModellingConnectionHandler handler = CreateLibraryHandler(apiConn, userConfig);
            IRenderedComponent<EditConnLeftSide> component = RenderLibrary(context, handler);

            await component.InvokeAsync(() => context.Services.GetRequiredService<IEventMediator>()
                .Publish("AppserversCollectionChanged", new CollectionChangedEvent()));
            int queriesAfterRefresh = apiConn.Queries.Count;
            handler.AvailableNwElems = [new((int)ModellingTypes.ModObjectType.AppServer, kLibServerId)];
            await component.InvokeAsync(() => component.FindComponent<SearchNwObject>().Instance.OnClose.InvokeAsync());

            Assert.Multiple(() =>
            {
                Assert.That(queriesAfterRefresh, Is.GreaterThan(0), "a changed app server collection reloads the library objects");
                Assert.That(apiConn.Queries, Has.Count.EqualTo(queriesAfterRefresh), "closing the search does not query again");
                Assert.That(GetPrivateField<List<KeyValuePair<int, long>>>(component.Instance, "AvailableNwElements"), Has.Count.EqualTo(1));
            });
        }
    }
}
