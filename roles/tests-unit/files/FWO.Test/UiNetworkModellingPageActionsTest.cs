using AngleSharp.Dom;
using Bunit;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Data.Workflow;
using FWO.Ui.Pages.NetworkModelling;
using FWO.Ui.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using NUnit.Framework;
using System.Reflection;

namespace FWO.Test
{
    /// <summary>
    /// Tests of the actions, links and request states of the network modelling page.
    /// </summary>
    internal partial class UiNetworkModellingPageTest
    {
        private const string kLatestTicketMessage = "workflow lookup ok";

        private sealed class MessageRecorder
        {
            public List<string> Messages { get; } = [];
            public bool FailFirstMessage { get; set; }

            public void Record(Exception? exception, string title, string text, bool isError)
            {
                Messages.Add($"{title}|{text}|{exception?.Message}{(isError ? "" : "|info")}");
                if (FailFirstMessage)
                {
                    FailFirstMessage = false;
                    throw new InvalidOperationException("message display failed");
                }
            }
        }

        private static IRenderedComponent<NetworkModelling> RenderPage(BunitContext context, MessageRecorder recorder, string? appId = null, string? connId = null)
        {
            Action<Exception?, string, string, bool> displayMessage = recorder.Record;
            IRenderedComponent<CascadingAuthenticationState> wrapper = context.Render<CascadingAuthenticationState>(parameters => parameters
                .AddChildContent<CascadingValue<Action<Exception?, string, string, bool>>>(cascade => cascade
                    .Add(value => value.Value, displayMessage)
                    .AddChildContent<NetworkModelling>(child =>
                    {
                        if (appId != null)
                        {
                            child.Add(page => page.AppId, appId);
                        }
                        if (connId != null)
                        {
                            child.Add(page => page.ConnId, connId);
                        }
                    })));
            return wrapper.FindComponent<NetworkModelling>();
        }

        private static readonly SimulatedUserConfig kTexts = new();

        /// <summary>
        /// Text the simulated user config shows for the given key, so expectations follow its dictionary.
        /// </summary>
        private static string Text(string key)
        {
            return kTexts.GetText(key);
        }

        private static object? InvokePrivate(IRenderedComponent<NetworkModelling> page, string methodName, params object?[] arguments)
        {
            MethodInfo method = typeof(NetworkModelling).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMethodException(typeof(NetworkModelling).FullName, methodName);
            return method.Invoke(page.Instance, arguments);
        }

        private static async Task InvokePrivateAsync(IRenderedComponent<NetworkModelling> page, string methodName, params object?[] arguments)
        {
            await page.InvokeAsync(async () => await (Task)InvokePrivate(page, methodName, arguments)!);
        }

        private static T GetPrivateField<T>(IRenderedComponent<NetworkModelling> page, string fieldName)
        {
            FieldInfo field = typeof(NetworkModelling).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingFieldException(typeof(NetworkModelling).FullName, fieldName);
            return (T)field.GetValue(page.Instance)!;
        }

        private static void ClickTab(IRenderedComponent<NetworkModelling> page, string title)
        {
            page.FindAll("a.nav-link").First(link => link.TextContent.Trim() == title).Click();
        }

        [Test]
        public async Task Render_ShowsLoadingUntilTheStatesAreLoaded()
        {
            await using BunitContext context = CreateContext([Roles.Admin], out NetworkModellingPageTestApiConn apiConn, out _);
            apiConn.StatesGate = new TaskCompletionSource();

            IRenderedComponent<NetworkModelling> page = RenderPage(context, new MessageRecorder(), appId: "APP-A");

            Assert.That(page.FindComponents<Loading>(), Has.Count.EqualTo(1));
            apiConn.StatesGate.SetResult();
            page.WaitForAssertion(() =>
            {
                Assert.That(page.Markup, Does.Contain("Alpha App"));
                Assert.That(page.FindComponents<Loading>(), Is.Empty);
            });
        }

        [Test]
        public async Task Render_WithoutAppsReportsThatNoAppWasFound()
        {
            await using BunitContext context = CreateContext([Roles.Admin], out NetworkModellingPageTestApiConn apiConn, out SimulatedUserConfig userConfig);
            apiConn.Owners = [];
            MessageRecorder recorder = new();

            IRenderedComponent<NetworkModelling> page = RenderPage(context, recorder);

            page.WaitForAssertion(() =>
            {
                Assert.That(recorder.Messages, Does.Contain($"{Text("fetch_data")}|{Text("E9001")}|"));
                Assert.That(apiConn.ConnectionQueryAppIds, Is.Empty);
                Assert.That(page.Markup, Does.Not.Contain("Communication profile"));
            });
        }

        [Test]
        public async Task Render_ReportsAFailureWhileLoadingTheApps()
        {
            await using BunitContext context = CreateContext([Roles.Admin], out NetworkModellingPageTestApiConn apiConn, out SimulatedUserConfig userConfig);
            apiConn.Owners = [];
            MessageRecorder recorder = new() { FailFirstMessage = true };

            IRenderedComponent<NetworkModelling> page = RenderPage(context, recorder);

            page.WaitForAssertion(() =>
            {
                Assert.That(recorder.Messages, Has.Count.EqualTo(2));
                Assert.That(recorder.Messages[1], Is.EqualTo($"{Text("fetch_data")}||message display failed"));
            });
        }

        [TestCase(null)]
        [TestCase("UNKNOWN-APP")]
        public async Task Render_WithoutKnownAppIdSelectsTheFirstApp(string? appId)
        {
            await using BunitContext context = CreateContext([Roles.Admin], out NetworkModellingPageTestApiConn apiConn, out _);

            IRenderedComponent<NetworkModelling> page = RenderPage(context, new MessageRecorder(), appId: appId);

            page.WaitForAssertion(() =>
            {
                Assert.That(page.Markup, Does.Contain("Alpha App"));
                Assert.That(apiConn.ConnectionQueryAppIds, Is.EqualTo(new List<int> { 10 }));
            });
        }

        [Test]
        public async Task Render_ReportsAFailureWhileInitialisingTheApp()
        {
            await using BunitContext context = CreateContext([Roles.Admin], out NetworkModellingPageTestApiConn apiConn, out _);
            apiConn.FailingQueries.Add(ExtRequestQueries.getLatestTicketId);
            MessageRecorder recorder = new();

            IRenderedComponent<NetworkModelling> page = RenderPage(context, recorder, appId: "APP-A");

            page.WaitForAssertion(() =>
                Assert.That(recorder.Messages, Has.Some.StartsWith($"{Text("fetch_data")}||query failed")));
        }

        [Test]
        public async Task SelectOwner_LoadsAnotherAppOnlyWhenTheSelectionChanges()
        {
            await using BunitContext context = CreateContext([Roles.Admin], out NetworkModellingPageTestApiConn apiConn, out _);
            IRenderedComponent<NetworkModelling> page = RenderPage(context, new MessageRecorder(), appId: "APP-A");
            page.WaitForAssertion(() => Assert.That(apiConn.ConnectionQueryAppIds, Is.EqualTo(new List<int> { 10 })));
            SelectOwner selectOwner = page.FindComponent<SelectOwner>().Instance;

            await page.InvokeAsync(() => selectOwner.SelectedOwnerChanged.InvokeAsync(new FwoOwner { Id = 10 }));
            await page.InvokeAsync(() => selectOwner.SelectedOwnerChanged.InvokeAsync(apiConn.Owners[1]));

            page.WaitForAssertion(() =>
            {
                Assert.That(apiConn.ConnectionQueryAppIds, Is.EqualTo(new List<int> { 10, 20 }));
                Assert.That(page.Markup, Does.Contain("Connection 20"));
            });
        }

        [TestCase(Roles.Admin, false)]
        [TestCase(Roles.ReporterViewAll, true)]
        public async Task Render_WithConnectionLinkOpensTheConnection(string role, bool expectedReadOnly)
        {
            await using BunitContext context = CreateContext([role], out _, out _);

            IRenderedComponent<NetworkModelling> page = RenderPage(context, new MessageRecorder(), appId: "APP-A", connId: "11");

            page.WaitForAssertion(() =>
            {
                IRenderedComponent<EditConn> editConn = page.FindComponent<EditConn>();
                Assert.That(editConn.Instance.ConnHandler?.ActConn.Id, Is.EqualTo(11));
                Assert.That(editConn.Instance.ConnHandler?.ReadOnly, Is.EqualTo(expectedReadOnly));
            });
        }

        [TestCase("99")]
        [TestCase("no-number")]
        public async Task Render_WithUnknownConnectionLinkStaysOnTheOverview(string connId)
        {
            await using BunitContext context = CreateContext([Roles.Admin], out _, out _);

            IRenderedComponent<NetworkModelling> page = RenderPage(context, new MessageRecorder(), appId: "APP-A", connId: connId);

            page.WaitForAssertion(() =>
            {
                Assert.That(page.Markup, Does.Contain("Connection 10"));
                Assert.That(page.FindComponents<EditConn>(), Is.Empty);
            });
        }

        [Test]
        public async Task Buttons_OpenShareLinkHistoryAndAppServerDialogs()
        {
            await using BunitContext context = CreateContext([Roles.Admin], out _, out _);
            IRenderedComponent<NetworkModelling> page = RenderPage(context, new MessageRecorder(), appId: "APP-A");
            page.WaitForAssertion(() => Assert.That(page.Markup, Does.Contain("Alpha App")));

            FindButton(page, Text("share_link")).Click();
            FindButton(page, Text("show_history")).Click();
            FindButton(page, Text("edit_app_server")).Click();

            page.WaitForAssertion(() =>
            {
                Assert.That(page.FindComponent<FWO.Ui.Pages.NetworkModelling.ShareLink>().Instance.Display, Is.True);
                Assert.That(page.FindComponent<FWO.Ui.Pages.NetworkModelling.ShowHistory>().Instance.Display, Is.True);
                Assert.That(page.FindComponent<FWO.Ui.Pages.NetworkModelling.ManualAppServer>().Instance.Display, Is.True);
            });
        }

        [Test]
        public async Task RefreshState_InWorkflowNotificationModeReloadsTheApp()
        {
            await using BunitContext context = CreateContext([Roles.Admin], out NetworkModellingPageTestApiConn apiConn, out SimulatedUserConfig userConfig);
            userConfig.VarianceAnalysisRefresh = true;
            userConfig.ModIntegrationMode = ModIntegrationMode.WorkflowNotifications;
            IRenderedComponent<NetworkModelling> page = RenderPage(context, new MessageRecorder(), appId: "APP-A");
            page.WaitForAssertion(() => Assert.That(apiConn.ConnectionQueryAppIds, Has.Count.EqualTo(1)));

            FindButton(page, Text("refresh_state")).Click();

            page.WaitForAssertion(() =>
            {
                Assert.That(apiConn.ConnectionQueryAppIds, Is.EqualTo(new List<int> { 10, 10 }));
                Assert.That(page.FindComponent<InProgress>().Instance.Display, Is.False);
            });
        }

        [Test]
        public async Task RefreshState_ReportsAFailure()
        {
            await using BunitContext context = CreateContext([Roles.Admin], out NetworkModellingPageTestApiConn apiConn, out SimulatedUserConfig userConfig);
            userConfig.VarianceAnalysisRefresh = true;
            userConfig.ModIntegrationMode = ModIntegrationMode.WorkflowNotifications;
            MessageRecorder recorder = new();
            IRenderedComponent<NetworkModelling> page = RenderPage(context, recorder, appId: "APP-A");
            page.WaitForAssertion(() => Assert.That(page.Markup, Does.Contain("Alpha App")));
            apiConn.FailingQueries.Add(ExtRequestQueries.getLatestTicketId);

            FindButton(page, Text("refresh_state")).Click();

            page.WaitForAssertion(() =>
            {
                Assert.That(recorder.Messages, Has.Some.StartsWith($"{Text("refresh_state")}||query failed"));
                Assert.That(page.FindComponent<InProgress>().Instance.Display, Is.False);
            });
        }

        [Test]
        public async Task RefreshState_InIntegratedModeAnalysesTheConnections()
        {
            await using BunitContext context = CreateContext([Roles.Admin], out NetworkModellingPageTestApiConn apiConn, out SimulatedUserConfig userConfig);
            userConfig.VarianceAnalysisRefresh = true;
            IRenderedComponent<NetworkModelling> page = RenderPage(context, new MessageRecorder(), appId: "APP-A");
            page.WaitForAssertion(() => Assert.That(page.Markup, Does.Contain("Alpha App")));

            FindButton(page, Text("refresh_state")).Click();

            page.WaitForAssertion(() =>
            {
                Assert.That(apiConn.ConnectionQueryAppIds, Has.Count.EqualTo(1), "the integrated mode analyses without reloading");
                Assert.That(page.FindComponent<InProgress>().Instance.Display, Is.False);
            });
        }

        [Test]
        public async Task Tabs_OfferInterfacesAndCommonServices()
        {
            await using BunitContext context = CreateContext([Roles.Admin], out _, out _);
            IRenderedComponent<NetworkModelling> page = RenderPage(context, new MessageRecorder(), appId: "APP-A");
            page.WaitForAssertion(() => Assert.That(page.Markup, Does.Contain("Alpha App")));

            ClickTab(page, Text("provided_interfaces"));
            page.WaitForAssertion(() => Assert.That(page.Find("#cbx_show_rejected"), Is.Not.Null));
            page.Find("#cbx_show_rejected").Change(true);
            FindButton(page, Text("add_interface")).Click();

            page.WaitForAssertion(() => Assert.That(page.FindComponent<EditConn>().Instance.ConnHandler?.ActConn.IsInterface, Is.True));
        }

        [Test]
        public async Task Tabs_AddCommonServiceOpensTheEditor()
        {
            await using BunitContext context = CreateContext([Roles.Admin], out _, out _);
            IRenderedComponent<NetworkModelling> page = RenderPage(context, new MessageRecorder(), appId: "APP-A");
            page.WaitForAssertion(() => Assert.That(page.Markup, Does.Contain("Alpha App")));

            ClickTab(page, Text("common_services"));
            page.WaitForAssertion(() => FindButton(page, Text("add_common_service")));
            FindButton(page, Text("add_common_service")).Click();

            page.WaitForAssertion(() => Assert.That(page.FindComponent<EditConn>().Instance.ConnHandler?.ActConn.IsCommonService, Is.True));
        }

        [Test]
        public async Task DecommissionInterfacePopup_RefreshKeepsTheOverview()
        {
            await using BunitContext context = CreateContext([Roles.Admin], out NetworkModellingPageTestApiConn apiConn, out _);
            IRenderedComponent<NetworkModelling> page = RenderPage(context, new MessageRecorder(), appId: "APP-A");
            page.WaitForAssertion(() => Assert.That(page.Markup, Does.Contain("Connection 10")));

            await page.InvokeAsync(() => page.FindComponent<DecommissionInterfacePopup>().Instance.RefreshParent());

            Assert.Multiple(() =>
            {
                Assert.That(page.Markup, Does.Contain("Connection 10"));
                Assert.That(apiConn.ConnectionQueryAppIds, Has.Count.EqualTo(1), "the popup refreshes its own data, not the page");
            });
        }

        [Test]
        public async Task ClosingTheEditorRestoresTheOverviewTab()
        {
            await using BunitContext context = CreateContext([Roles.Admin], out NetworkModellingPageTestApiConn apiConn, out _);
            apiConn.Connections =
            [
                new() { Id = 31, AppId = 10, Name = "Interface 31", IsInterface = true }
            ];
            IRenderedComponent<NetworkModelling> page = RenderPage(context, new MessageRecorder(), appId: "APP-A");
            page.WaitForAssertion(() => Assert.That(page.Markup, Does.Contain("Interface 31")));
            FindButton(page, Text("add_interface")).Click();
            IRenderedComponent<EditConn> editConn = page.WaitForComponent<EditConn>();

            await page.InvokeAsync(() =>
            {
                editConn.Instance.ClosingAction();
                editConn.Instance.DisplayChanged.InvokeAsync(false);
            });

            page.WaitForAssertion(() =>
            {
                Assert.That(page.FindComponents<EditConn>(), Is.Empty);
                Assert.That(page.Markup, Does.Contain("Interface 31"));
                Assert.That(GetPrivateField<bool>(page, "restoreTab"), Is.False);
            });
        }

        [Test]
        public async Task ConfirmDelete_DeletesTheRequestedConnection()
        {
            await using BunitContext context = CreateContext([Roles.Admin], out NetworkModellingPageTestApiConn apiConn, out _);
            IRenderedComponent<NetworkModelling> page = RenderPage(context, new MessageRecorder(), appId: "APP-A");
            page.WaitForAssertion(() => Assert.That(page.Markup, Does.Contain("Connection 10")));
            page.FindAll("button").Last(button => button.TextContent.Trim() == Text("delete")).Click();
            IRenderedComponent<ConfirmDelete> confirmDelete = page.FindComponents<ConfirmDelete>()
                .Single(component => component.Instance.Title == Text("delete_connection"));
            page.WaitForAssertion(() => Assert.That(confirmDelete.Instance.Display, Is.True));

            await page.InvokeAsync(() => confirmDelete.Instance.PerformAction());

            page.WaitForAssertion(() =>
            {
                Assert.That(page.Markup, Does.Not.Contain("Connection 10"));
                Assert.That(apiConn.UnexpectedQueries, Is.Empty);
            });
        }

        [Test]
        public async Task Actions_WithoutModellingRightsAreRejected()
        {
            await using BunitContext context = CreateContext([Roles.ReporterViewAll], out _, out SimulatedUserConfig userConfig);
            MessageRecorder recorder = new();
            IRenderedComponent<NetworkModelling> page = RenderPage(context, recorder, appId: "APP-A");
            page.WaitForAssertion(() => Assert.That(page.Markup, Does.Contain("Alpha App")));

            await page.InvokeAsync(() => InvokePrivate(page, "RequestFwChange"));
            await InvokePrivateAsync(page, "AddConnection");
            await InvokePrivateAsync(page, "AddInterface");
            await InvokePrivateAsync(page, "AddCommonService");
            await page.InvokeAsync(() => InvokePrivate(page, "Recertify"));

            Assert.That(recorder.Messages, Is.EqualTo(new List<string>
            {
                $"{Text("request_fw_change")}|{Text("E9104")}|",
                $"{Text("add_connection")}|{Text("E9104")}|",
                $"{Text("add_interface")}|{Text("E9104")}|",
                $"{Text("add_common_service")}|{Text("E9104")}|",
                $"{Text("recertify")}|{Text("E9104")}|"
            }));
        }

        [Test]
        public async Task Render_RequestButtonNeedsAModellingRole()
        {
            await using BunitContext context = CreateContext([Roles.ReporterViewAll], out _, out SimulatedUserConfig userConfig);

            IRenderedComponent<NetworkModelling> page = RenderPage(context, new MessageRecorder(), appId: "APP-A");

            page.WaitForAssertion(() =>
            {
                IElement requestButton = FindButton(page, Text("request_fw_change"));
                Assert.That(requestButton.HasAttribute("disabled"), Is.True);
                Assert.That(requestButton.ParentElement?.GetAttribute("title"), Is.EqualTo(kTexts.PureLine("C9012")));
            });
        }

        [Test]
        public async Task Render_ModellerOfAnotherAppCannotRequestChanges()
        {
            await using BunitContext context = CreateContext([Roles.Modeller, Roles.ReporterViewAll], out _, out SimulatedUserConfig userConfig);
            userConfig.User.Ownerships = [20];

            IRenderedComponent<NetworkModelling> page = RenderPage(context, new MessageRecorder(), appId: "APP-A");

            page.WaitForAssertion(() =>
            {
                Assert.That(page.Markup, Does.Contain("Alpha App"));
                Assert.That(FindButton(page, Text("request_fw_change")).HasAttribute("disabled"), Is.True);
                Assert.That(FindButton(page, Text("add_connection")).HasAttribute("disabled"), Is.True);
            });
        }

        [Test]
        public async Task Guards_WithoutSelectedAppDoNothing()
        {
            await using BunitContext context = CreateContext([Roles.Admin], out NetworkModellingPageTestApiConn apiConn, out _);
            apiConn.Owners = [];
            IRenderedComponent<NetworkModelling> page = RenderPage(context, new MessageRecorder());
            page.WaitForAssertion(() => Assert.That(page.FindComponents<Loading>(), Is.Empty));

            await InvokePrivateAsync(page, "DeleteConnection");
            bool recertAllowed = (bool)InvokePrivate(page, "IsRecertificationAllowedForSelectedApp")!;

            Assert.That(recertAllowed, Is.False);
        }

        [Test]
        public async Task ChangeStatus_ShowsNeverRequested()
        {
            await using BunitContext context = CreateContext([Roles.Admin], out NetworkModellingPageTestApiConn apiConn, out _);
            apiConn.LatestTicketIds = [];

            IRenderedComponent<NetworkModelling> page = RenderPage(context, new MessageRecorder(), appId: "APP-A");

            page.WaitForAssertion(() =>
            {
                Assert.That(GetPrivateField<string>(page, "ChangeStatus"), Is.EqualTo(Text("never_requested")));
                Assert.That(GetPrivateField<string>(page, "LastRequestDate"), Is.EqualTo("-"));
            });
        }

        [Test]
        public async Task ChangeStatus_ShowsTheImplementedRequest()
        {
            await using BunitContext context = CreateContext([Roles.Admin], out NetworkModellingPageTestApiConn apiConn, out SimulatedUserConfig userConfig);
            apiConn.TicketStateId = 90;

            IRenderedComponent<NetworkModelling> page = RenderPage(context, new MessageRecorder(), appId: "APP-A");

            page.WaitForAssertion(() =>
            {
                Assert.That(GetPrivateField<string>(page, "ChangeStatus"), Does.StartWith($"{Text("last_successful")}: 2026-07-01 10:00:00"));
                Assert.That(GetPrivateField<string>(page, "ChangeStatus"), Does.EndWith("requester"));
                Assert.That(GetPrivateField<string>(page, "LastRequestDate"), Is.EqualTo("01.07.2026"));
            });
        }

        [TestCase("ACCESS_DENIED_ERROR on firewall", true)]
        [TestCase(kLatestTicketMessage, false)]
        public async Task ChangeStatus_ShowsTheRequestInProgress(string lastResponse, bool temporaryError)
        {
            string expectedStatus = temporaryError
                ? $"{Text("temp_error")}: {Text("temp_error_extended")}: {lastResponse}"
                : Text("in_process");
            await using BunitContext context = CreateContext([Roles.Admin], out NetworkModellingPageTestApiConn apiConn, out SimulatedUserConfig userConfig);
            apiConn.LastRequests = [new() { TicketId = 42, LastProcessingResponse = lastResponse }];

            IRenderedComponent<NetworkModelling> page = RenderPage(context, new MessageRecorder(), appId: "APP-A");

            page.WaitForAssertion(() => Assert.That(GetPrivateField<string>(page, "ChangeStatus"), Is.EqualTo(expectedStatus)));
        }

        [Test]
        public async Task ChangeStatus_ReportsAFailedRequestOnce()
        {
            await using BunitContext context = CreateContext([Roles.Admin], out NetworkModellingPageTestApiConn apiConn, out SimulatedUserConfig userConfig);
            userConfig.ModRolloutErrorText = "Please contact the firewall team";
            apiConn.TicketStateId = 91;
            MessageRecorder recorder = new();
            IRenderedComponent<NetworkModelling> page = RenderPage(context, recorder, appId: "APP-A");
            page.WaitForAssertion(() =>
                Assert.That(GetPrivateField<string>(page, "ChangeStatus"), Is.EqualTo($"{Text("implementation_error")}: Rejected: {kLatestTicketMessage}")));

            apiConn.TicketUpdateHandler!([new WfTicket { Id = 42, StateId = 91, CreationDate = new DateTime(2026, 7, 1) }]);

            page.WaitForAssertion(() => Assert.That(recorder.Messages, Is.EqualTo(new List<string>
            {
                $"{Text("implementation_error")}|Please contact the firewall team|"
            })));
        }

        [Test]
        public async Task ChangeStatus_ShowsAFailedRequestWithoutResponse()
        {
            await using BunitContext context = CreateContext([Roles.Admin], out NetworkModellingPageTestApiConn apiConn, out SimulatedUserConfig userConfig);
            apiConn.TicketStateId = 91;
            apiConn.LastRequests = [];

            IRenderedComponent<NetworkModelling> page = RenderPage(context, new MessageRecorder(), appId: "APP-A");

            page.WaitForAssertion(() => Assert.That(GetPrivateField<string>(page, "ChangeStatus"), Is.EqualTo(Text("implementation_error"))));
        }

        [Test]
        public async Task ChangeStatus_IgnoresAFailingLookupOfTheLastResponse()
        {
            await using BunitContext context = CreateContext([Roles.Admin], out NetworkModellingPageTestApiConn apiConn, out SimulatedUserConfig userConfig);
            apiConn.FailingQueries.Add(ExtRequestQueries.getLastRequest);

            IRenderedComponent<NetworkModelling> page = RenderPage(context, new MessageRecorder(), appId: "APP-A");

            page.WaitForAssertion(() => Assert.That(GetPrivateField<string>(page, "ChangeStatus"), Is.EqualTo(Text("in_process"))));
        }

        [Test]
        public async Task ChangeStatus_ReportsAnUnknownTicketState()
        {
            await using BunitContext context = CreateContext([Roles.Admin], out NetworkModellingPageTestApiConn apiConn, out SimulatedUserConfig userConfig);
            apiConn.TicketStateId = 95;
            MessageRecorder recorder = new();

            IRenderedComponent<NetworkModelling> page = RenderPage(context, recorder, appId: "APP-A");

            page.WaitForAssertion(() => Assert.That(recorder.Messages, Has.Some.StartsWith($"{Text("refresh_state")}||")));
        }

        [Test]
        public async Task TicketSubscription_UpdatesTheStatusAndReportsErrors()
        {
            await using BunitContext context = CreateContext([Roles.Admin], out NetworkModellingPageTestApiConn apiConn, out SimulatedUserConfig userConfig);
            MessageRecorder recorder = new();
            IRenderedComponent<NetworkModelling> page = RenderPage(context, recorder, appId: "APP-A");
            page.WaitForAssertion(() => Assert.That(apiConn.TicketUpdateHandler, Is.Not.Null));

            apiConn.TicketUpdateHandler!([]);
            apiConn.TicketUpdateHandler!([new WfTicket { Id = 42, StateId = 90, CreationDate = new DateTime(2026, 8, 2) }]);
            apiConn.TicketSubscriptionErrorHandler!(new InvalidOperationException("connection lost"));

            page.WaitForAssertion(() =>
            {
                Assert.That(GetPrivateField<string>(page, "ChangeStatus"), Does.StartWith($"{Text("last_successful")}: 2026-08-02"));
                Assert.That(recorder.Messages, Does.Contain($"{Text("ticket")}|{Text("update_subscription_error")}|connection lost"));
            });
        }

        [Test]
        public async Task RequestFwChangePopup_RefreshesTheAppAfterARequestInWorkflowNotificationMode()
        {
            await using BunitContext context = CreateContext([Roles.Admin], out NetworkModellingPageTestApiConn apiConn, out SimulatedUserConfig userConfig);
            userConfig.ModIntegrationMode = ModIntegrationMode.WorkflowNotifications;
            IRenderedComponent<NetworkModelling> page = RenderPage(context, new MessageRecorder(), appId: "APP-A");
            page.WaitForAssertion(() => Assert.That(page.Markup, Does.Contain("Alpha App")));
            FindButton(page, Text("request_fw_change")).Click();
            RequestFwChangePopup popup = page.FindComponent<RequestFwChangePopup>().Instance;

            await page.InvokeAsync(() => popup.RefreshParent(new WfTicket { Id = 43, StateId = 90, CreationDate = new DateTime(2026, 9, 3) }));

            page.WaitForAssertion(() =>
            {
                Assert.That(apiConn.ConnectionQueryAppIds, Is.EqualTo(new List<int> { 10, 10 }));
                Assert.That(GetPrivateField<string>(page, "ChangeStatus"), Does.StartWith($"{Text("last_successful")}: 2026-09-03"));
            });
        }

        [Test]
        public async Task RequestRecertPopup_UpdatesTheRecertificationStatus()
        {
            await using BunitContext context = CreateContext([Roles.Admin], out _, out _);
            IRenderedComponent<NetworkModelling> page = RenderPage(context, new MessageRecorder(), appId: "APP-A");
            page.WaitForAssertion(() => Assert.That(GetPrivateField<string>(page, "RecertStatus"), Does.Contain("10.01.2026")));
            FindButton(page, Text("recertify")).Click();
            RequestRecertPopup popup = page.FindComponent<RequestRecertPopup>().Instance;

            bool refreshed = await page.InvokeAsync(() => popup.RefreshParent(new FwoOwner
            {
                LastRecertified = new DateTime(2026, 11, 20),
                LastRecertifierDn = "cn=new.recertifier,ou=users,dc=test,dc=local",
                NextRecertDate = new DateTime(2027, 5, 20)
            }));

            Assert.Multiple(() =>
            {
                Assert.That(refreshed, Is.True);
                Assert.That(GetPrivateField<string>(page, "RecertStatus"),
                    Is.EqualTo($"{Text("last_recertified")}: 20.11.2026, {Text("last_recertifier")}: new.recertifier, {Text("next_recertification")}: 20.05.2027"));
            });
        }

        [Test]
        public async Task Dispose_StopsReactingToConfigChanges()
        {
            await using BunitContext context = CreateContext([Roles.Admin], out _, out SimulatedUserConfig userConfig);
            IRenderedComponent<NetworkModelling> page = RenderPage(context, new MessageRecorder(), appId: "APP-A");
            page.WaitForAssertion(() => Assert.That(page.Markup, Does.Contain("Alpha App")));
            int renderCount = page.RenderCount;

            page.Instance.Dispose();
            InvokePrivate(page, "OnUserConfigChanged", userConfig, Array.Empty<FWO.Config.Api.Data.ConfigItem>());

            Assert.That(page.RenderCount, Is.EqualTo(renderCount));
        }
    }
}
