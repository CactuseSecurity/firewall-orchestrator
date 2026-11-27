using AngleSharp.Dom;
using Bunit;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Data.Workflow;
using FWO.Middleware.Client;
using FWO.Services.Modelling;
using FWO.Test.Mocks;
using FWO.Ui.Pages.NetworkModelling;
using FWO.Ui.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Tests of rejecting a requested interface: the rejection in its ticket, the lookup of the requesting application
    /// and the handling of failures while rejecting.
    /// </summary>
    [TestFixture]
    internal class UiRejectInterfacePopupTicketTest
    {
        private const int kInterfaceId = 21;
        private const long kTicketId = 42;
        private const int kRequestingAppId = 31;
        private const int kRejectedStateId = 249;

        private sealed record UiMessage(string Title, string Text, bool Error);

        /// <summary>
        /// Answers the queries of the rejection and, through its base, the queries of promoting the ticket.
        /// </summary>
        private sealed class RejectApiConn : TicketCreatorTest.TicketCreatorTestApiConn
        {
            public ModellingConnection StoredInterface { get; set; } = new();
            public HashSet<string> FailingQueries { get; } = [];
            public List<string> RejectQueries { get; } = [];
            public List<int> RequestedOwnerIds { get; } = [];

            /// <summary>Holds the saving of the connection back until completed, so the running rejection can be seen.</summary>
            public TaskCompletionSource? SaveGate { get; set; }

            public override async Task<T> SendQueryAsync<T>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                RejectQueries.Add(query);
                if (FailingQueries.Contains(query))
                {
                    throw new InvalidOperationException($"query failed: {query}");
                }
                if (query == ModellingQueries.getConnectionById)
                {
                    return (T)(object)new List<ModellingConnection> { new(StoredInterface) };
                }
                if (query == ModellingQueries.updateConnectionProperties)
                {
                    if (SaveGate != null)
                    {
                        await SaveGate.Task;
                    }
                    return (T)(object)new ReturnId { AffectedRows = 1 };
                }
                if (query == ModellingQueries.removeSelectedConnection)
                {
                    return (T)(object)new ReturnId { AffectedRows = 1 };
                }
                if (query == OwnerQueries.getOwnerById)
                {
                    int ownerId = variables?.GetType().GetProperty("id")?.GetValue(variables) is int id ? id : 0;
                    RequestedOwnerIds.Add(ownerId);
                    return (T)(object)new FwoOwner { Id = ownerId, Name = "Requesting App", ExtAppId = "APP-31" };
                }
                return await base.SendQueryAsync<T>(query, variables, operationName, chunkingOptions);
            }
        }

        private sealed class RejectTestContext(BunitContext context, SimulatedUserConfig userConfig) : IAsyncDisposable
        {
            public BunitContext Context { get; } = context;
            public SimulatedUserConfig UserConfig { get; } = userConfig;
            public List<UiMessage> Messages { get; } = [];
            public List<bool> DisplayChanges { get; } = [];
            public int RefreshCalls { get; set; }

            public ValueTask DisposeAsync()
            {
                return Context.DisposeAsync();
            }
        }

        private static RejectApiConn CreateApiConn()
        {
            WfImplTask implTask = new() { Id = 811, TicketId = kTicketId, ReqTaskId = 801, StateId = 210, TaskType = WfTaskType.new_interface.ToString() };
            WfReqTask requestTask = new()
            {
                Id = 801,
                TicketId = kTicketId,
                StateId = 210,
                TaskType = WfTaskType.new_interface.ToString(),
                ImplementationTasks = [implTask]
            };
            requestTask.SetAddInfo(AdditionalInfoKeys.ConnId, kInterfaceId.ToString());
            requestTask.SetAddInfo(AdditionalInfoKeys.ReqOwner, kRequestingAppId.ToString());
            return new RejectApiConn
            {
                StoredInterface = new ModellingConnection
                {
                    Id = kInterfaceId,
                    AppId = 77,
                    Name = "requested-iface",
                    Reason = "reason",
                    IsInterface = true,
                    IsRequested = true,
                    TicketId = kTicketId,
                    CreationDate = new DateTime(2026, 6, 1)
                },
                TicketById = new WfTicket { Id = kTicketId, StateId = 210, Tasks = [requestTask] },
                ExtStates = [new WfExtState { Name = ExtStates.Rejected.ToString(), StateId = kRejectedStateId }]
            };
        }

        private static RejectTestContext CreateRejectContext(ApiConnection apiConn)
        {
            BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddLocalization();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton<AuthenticationStateProvider>(new AllowAllAuthStateProvider(Roles.Admin));
            SimulatedUserConfig userConfig = new()
            {
                ModIconify = false,
                ModNamingConvention = "{}",
                ModExtraConfigs = "[]",
                ReqPriorities = "[]",
                UiHostName = "https://fwo.example"
            };
            userConfig.User.DbId = 3;
            userConfig.User.Name = "admin";
            userConfig.User.Roles = [Roles.Admin];
            context.Services.AddSingleton(apiConn);
            context.Services.AddSingleton<UserConfig>(userConfig);
            // fails at once instead of trying to reach a middleware, which can take seconds per call
            context.Services.AddSingleton<MiddlewareClient>(new TestMiddlewareClient());
            context.Services.AddSingleton<DomEventService>();
            return new RejectTestContext(context, userConfig);
        }

        private static IRenderedComponent<RejectInterfacePopup> RenderPopup(RejectTestContext test, RejectApiConn apiConn)
        {
            ModellingConnection actConn = new(apiConn.StoredInterface);
            ModellingConnectionHandler handler = new(apiConn, test.UserConfig, new FwoOwner { Id = 77, Name = "owner", ExtAppId = "APP-77" }, [actConn], actConn,
                addMode: false, readOnly: false, displayMessageInUi: (_, _, _, _) => { }, refreshParent: () => Task.CompletedTask, isOwner: true);
            Action<Exception?, string, string, bool> displayMessage = (_, title, text, error) => test.Messages.Add(new(title, text, error));
            return test.Context.Render<CascadingAuthenticationState>(parameters => parameters
                .AddChildContent<CascadingValue<Action<Exception?, string, string, bool>>>(cascade => cascade
                    .Add(value => value.Value, displayMessage)
                    .AddChildContent<RejectInterfacePopup>(popup => popup
                        .Add(p => p.Display, true)
                        .Add(p => p.DisplayChanged, value => test.DisplayChanges.Add(value))
                        .Add(p => p.ConnHandler, handler)
                        .Add(p => p.AllowAdminReject, true)
                        .Add(p => p.RefreshParent, () =>
                        {
                            test.RefreshCalls++;
                            return Task.CompletedTask;
                        }))))
                .FindComponent<RejectInterfacePopup>();
        }

        private static IElement RejectButton(IRenderedComponent<RejectInterfacePopup> popup, SimulatedUserConfig userConfig)
        {
            return popup.FindAll("button").Single(button => button.TextContent.Trim() == userConfig.GetText("reject"));
        }

        private static List<int> UpdatedImplTaskStates(RejectApiConn apiConn)
        {
            return [.. apiConn.Queries
                .Select((query, index) => new { query, index })
                .Where(entry => entry.query == RequestQueries.updateImplementationTaskState)
                .Select(entry => apiConn.Variables[entry.index]?.GetType().GetProperty("state")?.GetValue(apiConn.Variables[entry.index]) is int state ? state : 0)];
        }

        [Test]
        public async Task RejectingARequestedInterfaceRejectsItsTicket()
        {
            RejectApiConn apiConn = CreateApiConn();
            await using RejectTestContext test = CreateRejectContext(apiConn);
            IRenderedComponent<RejectInterfacePopup> popup = RenderPopup(test, apiConn);
            popup.Find("textarea").Change("no longer needed");

            await popup.InvokeAsync(() => RejectButton(popup, test.UserConfig).Click());

            popup.WaitForAssertion(() =>
            {
                Assert.That(apiConn.RequestedOwnerIds, Is.EqualTo(new List<int> { kRequestingAppId }), "the requesting app is looked up from the ticket");
                Assert.That(UpdatedImplTaskStates(apiConn), Does.Contain(kRejectedStateId));
                Assert.That(apiConn.Queries, Does.Contain(RequestQueries.addCommentToImplTask));
                Assert.That(test.Messages, Does.Contain(new UiMessage(test.UserConfig.GetText("reject_interface"), test.UserConfig.GetText("U9018"), false)));
                Assert.That(test.RefreshCalls, Is.EqualTo(1));
                Assert.That(test.DisplayChanges, Is.EqualTo(new List<bool> { false }));
            });
        }

        [Test]
        public async Task FailedLookupOfTheRequestingAppDoesNotStopTheRejection()
        {
            RejectApiConn apiConn = CreateApiConn();
            apiConn.FailingTicketReads = 1;
            await using RejectTestContext test = CreateRejectContext(apiConn);
            IRenderedComponent<RejectInterfacePopup> popup = RenderPopup(test, apiConn);

            await popup.InvokeAsync(() => RejectButton(popup, test.UserConfig).Click());

            popup.WaitForAssertion(() =>
            {
                Assert.That(apiConn.RequestedOwnerIds, Is.Empty);
                Assert.That(UpdatedImplTaskStates(apiConn), Does.Contain(kRejectedStateId));
                Assert.That(test.Messages, Does.Contain(new UiMessage(test.UserConfig.GetText("reject_interface"), test.UserConfig.GetText("U9018"), false)));
            });
        }

        [Test]
        public async Task FailedSavingKeepsThePopupOpen()
        {
            RejectApiConn apiConn = CreateApiConn();
            apiConn.FailingQueries.Add(ModellingQueries.updateConnectionProperties);
            await using RejectTestContext test = CreateRejectContext(apiConn);
            IRenderedComponent<RejectInterfacePopup> popup = RenderPopup(test, apiConn);

            await popup.InvokeAsync(() => RejectButton(popup, test.UserConfig).Click());

            Assert.Multiple(() =>
            {
                Assert.That(apiConn.RejectQueries, Does.Not.Contain(ModellingQueries.removeSelectedConnection));
                Assert.That(UpdatedImplTaskStates(apiConn), Is.Empty);
                Assert.That(test.RefreshCalls, Is.Zero);
                Assert.That(test.DisplayChanges, Is.Empty);
                Assert.That(RejectButton(popup, test.UserConfig).HasAttribute("disabled"), Is.False, "the rejection can be tried again");
            });
        }

        [Test]
        public async Task FailureWhileRejectingIsReported()
        {
            RejectApiConn apiConn = CreateApiConn();
            apiConn.FailingQueries.Add(ModellingQueries.removeSelectedConnection);
            await using RejectTestContext test = CreateRejectContext(apiConn);
            IRenderedComponent<RejectInterfacePopup> popup = RenderPopup(test, apiConn);

            await popup.InvokeAsync(() => RejectButton(popup, test.UserConfig).Click());

            Assert.Multiple(() =>
            {
                Assert.That(test.Messages, Has.Some.Matches<UiMessage>(message => message.Title == test.UserConfig.GetText("reject_interface") && message.Error));
                Assert.That(UpdatedImplTaskStates(apiConn), Is.Empty);
                Assert.That(test.DisplayChanges, Is.Empty);
                Assert.That(RejectButton(popup, test.UserConfig).HasAttribute("disabled"), Is.False);
            });
        }

        [Test]
        public async Task RunningRejectionDisablesTheButtonAndIgnoresAnotherClick()
        {
            RejectApiConn apiConn = CreateApiConn();
            apiConn.SaveGate = new TaskCompletionSource();
            await using RejectTestContext test = CreateRejectContext(apiConn);
            IRenderedComponent<RejectInterfacePopup> popup = RenderPopup(test, apiConn);

            Task rejecting = popup.InvokeAsync(() => RejectButton(popup, test.UserConfig).ClickAsync(new MouseEventArgs()));
            popup.WaitForAssertion(() => Assert.That(RejectButton(popup, test.UserConfig).HasAttribute("disabled"), Is.True));
            await popup.InvokeAsync(() => (Task)typeof(RejectInterfacePopup)
                .GetMethod("Reject", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(popup.Instance, null)!);
            apiConn.SaveGate.SetResult();
            await rejecting;

            popup.WaitForAssertion(() =>
            {
                Assert.That(apiConn.RejectQueries.Count(query => query == ModellingQueries.updateConnectionProperties), Is.EqualTo(1));
                Assert.That(test.DisplayChanges, Is.EqualTo(new List<bool> { false }));
            });
        }
    }
}
