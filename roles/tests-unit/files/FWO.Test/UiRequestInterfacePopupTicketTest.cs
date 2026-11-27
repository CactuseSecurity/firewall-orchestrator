using AngleSharp.Dom;
using Bunit;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Middleware.Client;
using FWO.Test.Mocks;
using FWO.Ui.Pages.NetworkModelling;
using FWO.Ui.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using RestSharp;
using System.Net;

namespace FWO.Test
{
    /// <summary>
    /// Tests of requesting a new interface from another application: the ticket, the requested interface, its
    /// notification email and the handling of failures.
    /// </summary>
    [TestFixture]
    internal class UiRequestInterfacePopupTicketTest
    {
        private const int kTargetAppId = 7;
        private const int kRequestingAppId = 9;
        private const int kNewConnectionId = 500;
        private const int kDummyAppRoleId = 999;

        private sealed record UiMessage(string Title, string Text, bool Error);

        public enum NotificationOutcome
        {
            Delivered,
            Failed,
            NoRecipients,
            Suppressed,
            HttpError,
            Unreachable
        }

        /// <summary>
        /// Answers the queries of creating the requested interface and, through its base, of creating the ticket.
        /// </summary>
        private sealed class InterfaceRequestApiConn : TicketCreatorTest.TicketCreatorTestApiConn
        {
            public List<string> RequestQueries { get; } = [];
            public List<object?> NewConnectionVariables { get; } = [];
            public HashSet<string> FailingQueries { get; } = [];

            public override Task<T> SendQueryAsync<T>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                RequestQueries.Add(query);
                if (FailingQueries.Contains(query))
                {
                    throw new InvalidOperationException($"query failed: {query}");
                }
                object? answer = query switch
                {
                    _ when query == ModellingQueries.getDummyAppRole => new List<ModellingAppRole> { new() { Id = kDummyAppRoleId, IdString = "DUMMY", Name = "Dummy" } },
                    _ when query == ModellingQueries.newConnection => RecordNewConnection(variables),
                    _ when query == ModellingQueries.addNwGroupToConnection => new ReturnIdWrapper { ReturnIds = [new ReturnId { NewIdLong = 1 }] },
                    _ when query == ModellingQueries.addSelectedConnection => new ReturnIdWrapper { ReturnIds = [new ReturnId { NewIdLong = 1 }] },
                    _ => null
                };
                return answer != null ? Task.FromResult((T)answer) : base.SendQueryAsync<T>(query, variables, operationName, chunkingOptions);
            }

            private ReturnIdWrapper RecordNewConnection(object? variables)
            {
                NewConnectionVariables.Add(variables);
                return new ReturnIdWrapper { ReturnIds = [new ReturnId { NewId = kNewConnectionId }] };
            }
        }

        /// <summary>
        /// Answers the notification about the requested interface with a configurable outcome, without a middleware.
        /// </summary>
        private sealed class NotificationMiddlewareClient : TestMiddlewareClient
        {
            public NotificationOutcome Outcome { get; set; } = NotificationOutcome.Delivered;
            public List<int> NotifiedConnectionIds { get; } = [];

            public override Task<RestResponse<NotificationDeliveryResult>> SendInterfaceRequestNotification(int connectionId)
            {
                NotifiedConnectionIds.Add(connectionId);
                if (Outcome == NotificationOutcome.Unreachable)
                {
                    throw new HttpRequestException("middleware not reachable");
                }
                bool successful = Outcome != NotificationOutcome.HttpError;
                return Task.FromResult(new RestResponse<NotificationDeliveryResult>(new RestRequest())
                {
                    IsSuccessStatusCode = successful,
                    ResponseStatus = ResponseStatus.Completed,
                    StatusCode = successful ? HttpStatusCode.OK : HttpStatusCode.InternalServerError,
                    Content = successful ? null : "notification service down",
                    Data = Outcome switch
                    {
                        NotificationOutcome.Failed => NotificationDeliveryResult.Failed,
                        NotificationOutcome.NoRecipients => NotificationDeliveryResult.NoRecipients,
                        NotificationOutcome.Suppressed => NotificationDeliveryResult.Suppressed,
                        _ => NotificationDeliveryResult.Delivered
                    }
                });
            }
        }

        private sealed class RequestTestContext(BunitContext context, SimulatedUserConfig userConfig, NotificationMiddlewareClient middleware) : IAsyncDisposable
        {
            public BunitContext Context { get; } = context;
            public SimulatedUserConfig UserConfig { get; } = userConfig;
            public NotificationMiddlewareClient Middleware { get; } = middleware;
            public List<UiMessage> Messages { get; } = [];
            public List<bool> DisplayChanges { get; } = [];
            public int RefreshCalls { get; set; }

            public ValueTask DisposeAsync()
            {
                return Context.DisposeAsync();
            }
        }

        private static RequestTestContext CreateRequestContext(ApiConnection apiConn, bool modeller = true)
        {
            BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddLocalization();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton<AuthenticationStateProvider>(new AllowAllAuthStateProvider(Roles.Modeller));
            SimulatedUserConfig userConfig = new()
            {
                ModIconify = false,
                ModNamingConvention = "{}",
                ModExtraConfigs = "[]",
                ModReqInterfaceName = "requested interface",
                ModReqTicketTitle = "Interface ticket",
                ModReqTaskTitle = "Interface task",
                ReqPriorities = "[]"
            };
            userConfig.User.DbId = 3;
            userConfig.User.Name = "requester";
            userConfig.User.Roles = modeller ? [Roles.Modeller] : [Roles.Auditor];
            NotificationMiddlewareClient middleware = new();
            context.Services.AddSingleton(apiConn);
            context.Services.AddSingleton<UserConfig>(userConfig);
            context.Services.AddSingleton<MiddlewareClient>(middleware);
            context.Services.AddSingleton<DomEventService>();
            return new RequestTestContext(context, userConfig, middleware);
        }

        private static IRenderedComponent<RequestInterfacePopup> RenderPopup(RequestTestContext test)
        {
            Action<Exception?, string, string, bool> displayMessage = (_, title, text, error) => test.Messages.Add(new(title, text, error));
            return test.Context.Render<CascadingAuthenticationState>(parameters => parameters
                .AddChildContent<CascadingValue<Action<Exception?, string, string, bool>>>(cascade => cascade
                    .Add(value => value.Value, displayMessage)
                    .AddChildContent<RequestInterfacePopup>(popup => popup
                        .Add(p => p.Display, true)
                        .Add(p => p.DisplayChanged, value => test.DisplayChanges.Add(value))
                        .Add(p => p.Apps, [new FwoOwner { Id = kTargetAppId, Name = "Target App" }])
                        .Add(p => p.RequestingOwner, new FwoOwner { Id = kRequestingAppId, Name = "Requesting App" })
                        .Add(p => p.RefreshParent, () =>
                        {
                            test.RefreshCalls++;
                            return Task.CompletedTask;
                        }))))
                .FindComponent<RequestInterfacePopup>();
        }

        private static IElement RequestButton(IRenderedComponent<RequestInterfacePopup> popup, SimulatedUserConfig userConfig)
        {
            return popup.FindAll("button").Single(button => button.TextContent.Trim() == userConfig.GetText("request_interface"));
        }

        private static async Task SendRequest(IRenderedComponent<RequestInterfacePopup> popup, SimulatedUserConfig userConfig, bool asSource = true)
        {
            popup.Find("#txt_reason").Change("we need access");
            popup.Find("#cbx_as_source").Change(asSource);
            await popup.InvokeAsync(() => RequestButton(popup, userConfig).Click());
        }

        private static object? Variable(object? variables, string name)
        {
            return variables?.GetType().GetProperty(name)?.GetValue(variables);
        }

        [Test]
        public async Task RequestCreatesTheTicketAndTheRequestedInterface()
        {
            InterfaceRequestApiConn apiConn = new();
            await using RequestTestContext test = CreateRequestContext(apiConn);
            IRenderedComponent<RequestInterfacePopup> popup = RenderPopup(test);

            await SendRequest(popup, test.UserConfig);

            popup.WaitForAssertion(() =>
            {
                object? connection = apiConn.NewConnectionVariables.Single();
                Assert.That(Variable(connection, "isInterface"), Is.EqualTo(true));
                Assert.That(Variable(connection, "isRequested"), Is.EqualTo(true));
                Assert.That(Variable(connection, "ticketId"), Is.GreaterThan(0L), "the interface refers to the created ticket");
                Assert.That((string?)Variable(connection, "name"), Does.StartWith("requested interface (Ticket: "));
                Assert.That(apiConn.RequestQueries, Does.Contain(ModellingQueries.addNwGroupToConnection), "the dummy app role marks the source as requested");
                Assert.That(apiConn.RequestQueries, Does.Contain(ModellingQueries.addSelectedConnection), "the requesting app gets the interface preselected");
                Assert.That(apiConn.Queries, Does.Contain(FWO.Api.Client.Queries.RequestQueries.updateRequestTaskAdditionalInfo), "the ticket gets the id of the interface");
                Assert.That(test.Middleware.NotifiedConnectionIds, Is.EqualTo(new List<int> { kNewConnectionId }));
                Assert.That(test.Messages, Does.Contain(new UiMessage(test.UserConfig.GetText("send_email"), test.UserConfig.GetText("U9011"), false)));
                Assert.That(test.RefreshCalls, Is.EqualTo(1));
                Assert.That(test.DisplayChanges, Is.EqualTo(new List<bool> { false }));
            });
        }

        [TestCase(NotificationOutcome.Failed, "E9011")]
        [TestCase(NotificationOutcome.NoRecipients, "E9011")]
        [TestCase(NotificationOutcome.Suppressed, null)]
        public async Task NotificationResultIsReported(NotificationOutcome outcome, string? expectedErrorKey)
        {
            InterfaceRequestApiConn apiConn = new();
            await using RequestTestContext test = CreateRequestContext(apiConn);
            test.Middleware.Outcome = outcome;
            IRenderedComponent<RequestInterfacePopup> popup = RenderPopup(test);

            await SendRequest(popup, test.UserConfig);

            popup.WaitForAssertion(() =>
            {
                List<UiMessage> emailMessages = [.. test.Messages.Where(message => message.Title == test.UserConfig.GetText("send_email"))];
                Assert.That(emailMessages, expectedErrorKey == null
                    ? Is.Empty
                    : Is.EqualTo(new List<UiMessage> { new(test.UserConfig.GetText("send_email"), test.UserConfig.GetText(expectedErrorKey), true) }));
                Assert.That(test.DisplayChanges, Is.EqualTo(new List<bool> { false }), "the request itself is done");
            });
        }

        [TestCase(NotificationOutcome.HttpError)]
        [TestCase(NotificationOutcome.Unreachable)]
        public async Task FailingNotificationIsReported(NotificationOutcome outcome)
        {
            InterfaceRequestApiConn apiConn = new();
            await using RequestTestContext test = CreateRequestContext(apiConn);
            test.Middleware.Outcome = outcome;
            IRenderedComponent<RequestInterfacePopup> popup = RenderPopup(test);

            await SendRequest(popup, test.UserConfig);

            popup.WaitForAssertion(() =>
            {
                Assert.That(test.Messages, Has.Some.Matches<UiMessage>(message => message.Title == test.UserConfig.GetText("send_email") && message.Error));
                Assert.That(apiConn.Queries, Does.Contain(FWO.Api.Client.Queries.RequestQueries.updateRequestTaskAdditionalInfo), "the ticket is completed anyway");
                Assert.That(test.DisplayChanges, Is.EqualTo(new List<bool> { false }));
            });
        }

        [Test]
        public async Task RequestAsDestinationMarksTheDestination()
        {
            InterfaceRequestApiConn apiConn = new();
            await using RequestTestContext test = CreateRequestContext(apiConn);
            IRenderedComponent<RequestInterfacePopup> popup = RenderPopup(test);

            await SendRequest(popup, test.UserConfig, asSource: false);

            popup.WaitForAssertion(() =>
            {
                Assert.That(apiConn.NewConnectionVariables, Has.Count.EqualTo(1));
                Assert.That(apiConn.RequestQueries, Does.Contain(ModellingQueries.addNwGroupToConnection));
                Assert.That(test.DisplayChanges, Is.EqualTo(new List<bool> { false }));
            });
        }

        [Test]
        public async Task FailedTicketCreationIsReported()
        {
            InterfaceRequestApiConn apiConn = new();
            apiConn.FailingQueries.Add(FWO.Api.Client.Queries.RequestQueries.newTicket);
            await using RequestTestContext test = CreateRequestContext(apiConn);
            IRenderedComponent<RequestInterfacePopup> popup = RenderPopup(test);

            await SendRequest(popup, test.UserConfig);

            Assert.Multiple(() =>
            {
                Assert.That(apiConn.NewConnectionVariables, Is.Empty, "no interface without a ticket");
                Assert.That(test.Middleware.NotifiedConnectionIds, Is.Empty);
            });
        }

        [Test]
        public async Task UserWithoutModellerRoleCannotRequest()
        {
            InterfaceRequestApiConn apiConn = new();
            await using RequestTestContext test = CreateRequestContext(apiConn, modeller: false);

            IRenderedComponent<RequestInterfacePopup> popup = RenderPopup(test);

            Assert.That(RequestButton(popup, test.UserConfig).HasAttribute("disabled"), Is.True);
        }
    }
}
