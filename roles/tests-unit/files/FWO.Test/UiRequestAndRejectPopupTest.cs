using Bunit;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Middleware.Client;
using FWO.Services.Modelling;
using FWO.Ui.Pages.NetworkModelling;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Security.Claims;

namespace FWO.Test
{
    [TestFixture]
    internal class UiRequestAndRejectPopupTest
    {
        [Test]
        public void RequestInterfacePopup_OnInitialized_SetsDefaultOwnerAndInterfaceName()
        {
            using BunitContext context = CreateContext(Roles.Modeller);
            SimulatedUserConfig userConfig = (SimulatedUserConfig)context.Services.GetRequiredService<UserConfig>();
            userConfig.ModReqInterfaceName = "req-interface";
            FwoOwner selectedApp = new() { Id = 11, Name = "Selected" };
            FwoOwner requestingOwner = new() { Id = 12, Name = "Requester" };

            IRenderedComponent<RequestInterfacePopup> component = RenderRequestInterfacePopup(
                context,
                selectedApp,
                requestingOwner);

            Assert.Multiple(() =>
            {
                Assert.That(component.FindAll("label").Any(label => label.TextContent.Contains(selectedApp.Name, StringComparison.Ordinal)), Is.True);
                Assert.That(component.Find("input[type='text']").GetAttribute("value"), Is.EqualTo("req-interface"));
            });
        }

        [Test]
        public async Task RequestInterfacePopup_SendRequest_RejectsMissingData()
        {
            using BunitContext context = CreateContext(Roles.Modeller);
            SimulatedUserConfig userConfig = (SimulatedUserConfig)context.Services.GetRequiredService<UserConfig>();
            userConfig.ModReqInterfaceName = "";
            FwoOwner selectedApp = new() { Id = 11, Name = "Selected" };
            FwoOwner requestingOwner = new() { Id = 12, Name = "Requester" };
            List<(Exception? Exception, string Title, string Message, bool IsError)> messages = [];

            IRenderedComponent<RequestInterfacePopup> component = RenderRequestInterfacePopup(
                context,
                selectedApp,
                requestingOwner,
                messageSink: (exception, title, message, isError) => messages.Add((exception, title, message, isError)));

            component.Find("input[type='text']").Change("");
            component.Find("textarea").Change("");
            await component.InvokeAsync(() => component.FindAll("button.btn-primary").Single().Click());

            Assert.Multiple(() =>
            {
                Assert.That(messages, Has.Count.EqualTo(1));
                Assert.That(messages[0].Title, Is.EqualTo(userConfig.GetText("add_new_request")));
                Assert.That(messages[0].Message, Is.EqualTo(userConfig.GetText("E5102")));
                Assert.That(messages[0].IsError, Is.True);
                Assert.That(component.Instance.Display, Is.True);
            });
        }

        [Test]
        public async Task RequestInterfacePopup_SendRequest_RejectsWhenSelectedOwnerMatchesRequestingOwner()
        {
            using BunitContext context = CreateContext(Roles.Modeller);
            SimulatedUserConfig userConfig = (SimulatedUserConfig)context.Services.GetRequiredService<UserConfig>();
            userConfig.ModReqInterfaceName = "req-interface";
            FwoOwner owner = new() { Id = 11, Name = "Owner" };

            IRenderedComponent<RequestInterfacePopup> component = RenderRequestInterfacePopup(
                context,
                owner,
                owner);

            component.Find("input[type='text']").Change("branch-if");
            component.Find("textarea").Change("needed");
            await component.InvokeAsync(() => component.FindAll("button.btn-primary").Single().Click());

            Assert.That(component.Instance.Display, Is.True);
        }

        [Test]
        public void RejectInterfacePopup_OnParametersSet_SetsMessageAndAdminReason()
        {
            using BunitContext context = CreateContext(Roles.Admin, Roles.Modeller);
            SimulatedUserConfig userConfig = (SimulatedUserConfig)context.Services.GetRequiredService<UserConfig>();
            userConfig.User.Roles = [Roles.Admin, Roles.Modeller];
            userConfig.SetExecutionMode(Roles.Admin);
            SimulatedUserConfig.DummyTranslate["U9017"] = "Reject interface ";
            SimulatedUserConfig.DummyTranslate["U9036"] = "Admin default reason";
            ModellingConnectionHandler handler = CreateConnectionHandler(
                new RejectInterfacePopupTestApiConn(),
                userConfig,
                new ModellingConnection { Id = 21, Name = "iface21", Reason = "reason", IsInterface = true });

            IRenderedComponent<RejectInterfacePopup> component = RenderRejectInterfacePopup(
                context,
                handler,
                allowAdminReject: true);

            Assert.Multiple(() =>
            {
                Assert.That(component.Markup, Does.Contain("Reject interface iface21?"));
                Assert.That(component.Markup, Does.Contain("Admin default reason"));
            });
        }

        [Test]
        public async Task RejectInterfacePopup_Reject_SavesPropertiesAndRemovesSelection()
        {
            using BunitContext context = CreateContext(Roles.Admin, Roles.Modeller);
            SimulatedUserConfig userConfig = (SimulatedUserConfig)context.Services.GetRequiredService<UserConfig>();
            userConfig.User.Roles = [Roles.Admin, Roles.Modeller];
            userConfig.SetExecutionMode(Roles.Admin);
            SimulatedUserConfig.DummyTranslate["U9017"] = "Reject interface ";
            RejectInterfacePopupTestApiConn apiConn = new();
            ModellingConnection actConn = new()
            {
                Id = 21,
                Name = "iface21",
                Reason = "reason",
                IsInterface = true
            };
            ModellingConnectionHandler handler = CreateConnectionHandler(apiConn, userConfig, actConn);
            bool displayChanged = true;
            int refreshCalls = 0;

            IRenderedComponent<RejectInterfacePopup> component = RenderRejectInterfacePopup(
                context,
                handler,
                allowAdminReject: true,
                displayChanged: value => displayChanged = value,
                refreshParent: () =>
                {
                    refreshCalls++;
                    return Task.CompletedTask;
                });

            component.Find("textarea").Change("planned removal");
            await component.InvokeAsync(() => component.FindAll("button.btn-primary").Single().Click());

            Assert.Multiple(() =>
            {
                Assert.That(apiConn.UpdateConnectionPropertiesCalls, Is.EqualTo(1));
                Assert.That(apiConn.RemoveSelectedConnectionCalls, Is.EqualTo(1));
                Assert.That(refreshCalls, Is.EqualTo(1));
                Assert.That(displayChanged, Is.False);
                Assert.That(component.Instance.Display, Is.False);
                Assert.That(handler.ActConn.GetBoolProperty(ConState.Rejected.ToString()), Is.True);
            });
        }

        private static BunitContext CreateContext(params string[] roles)
        {
            BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddLocalization();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton<AuthenticationStateProvider>(new PopupAuthStateProvider(roles));
            context.Services.AddSingleton<ApiConnection>(new SimulatedApiConnection());
            context.Services.AddSingleton(new MiddlewareClient("http://localhost/"));
            context.Services.AddSingleton<UserConfig>(CreateUserConfig());
            return context;
        }

        private static SimulatedUserConfig CreateUserConfig()
        {
            return new SimulatedUserConfig
            {
                ModNamingConvention = "{}",
                ModReqInterfaceName = "req-interface",
                User = { Roles = [Roles.Modeller, Roles.Admin], Name = "tester" }
            };
        }

        private static ModellingConnectionHandler CreateConnectionHandler(
            ApiConnection apiConn,
            SimulatedUserConfig userConfig,
            ModellingConnection actConn)
        {
            return new ModellingConnectionHandler(
                apiConn,
                userConfig,
                new FwoOwner { Id = 77, Name = "owner" },
                [actConn],
                actConn,
                addMode: false,
                readOnly: false,
                displayMessageInUi: (_, _, _, _) => { },
                refreshParent: () => Task.CompletedTask,
                isOwner: true);
        }

        private static IRenderedComponent<RequestInterfacePopup> RenderRequestInterfacePopup(
            BunitContext context,
            FwoOwner selectedApp,
            FwoOwner requestingOwner,
            Action<Exception?, string, string, bool>? messageSink = null)
        {
            RenderFragment fragment = builder =>
            {
                builder.OpenComponent<CascadingValue<Action<Exception?, string, string, bool>>>(0);
                builder.AddAttribute(1, "Value", messageSink ?? ((_, _, _, _) => { }));
                builder.AddAttribute(2, "IsFixed", true);
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(childBuilder =>
                {
                    childBuilder.OpenComponent<CascadingAuthenticationState>(0);
                    childBuilder.AddAttribute(1, "ChildContent", (RenderFragment)(popupBuilder =>
                    {
                        popupBuilder.OpenComponent<RequestInterfacePopup>(0);
                        popupBuilder.AddAttribute(1, "Display", true);
                        popupBuilder.AddAttribute(2, "Apps", new List<FwoOwner> { selectedApp });
                        popupBuilder.AddAttribute(3, "RequestingOwner", requestingOwner);
                        popupBuilder.AddAttribute(4, "RefreshParent", (Func<Task>)(() => Task.CompletedTask));
                        popupBuilder.CloseComponent();
                    }));
                    childBuilder.CloseComponent();
                }));
                builder.CloseComponent();
            };

            return context.Render(fragment).FindComponent<RequestInterfacePopup>();
        }

        private static IRenderedComponent<RejectInterfacePopup> RenderRejectInterfacePopup(
            BunitContext context,
            ModellingConnectionHandler handler,
            bool allowAdminReject = false,
            Action<bool>? displayChanged = null,
            Func<Task>? refreshParent = null,
            Action<Exception?, string, string, bool>? messageSink = null)
        {
            RenderFragment fragment = builder =>
            {
                builder.OpenComponent<CascadingValue<Action<Exception?, string, string, bool>>>(0);
                builder.AddAttribute(1, "Value", messageSink ?? ((_, _, _, _) => { }));
                builder.AddAttribute(2, "IsFixed", true);
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(childBuilder =>
                {
                    childBuilder.OpenComponent<CascadingAuthenticationState>(0);
                    childBuilder.AddAttribute(1, "ChildContent", (RenderFragment)(popupBuilder =>
                    {
                        popupBuilder.OpenComponent<RejectInterfacePopup>(0);
                        popupBuilder.AddAttribute(1, "Display", true);
                        popupBuilder.AddAttribute(2, "DisplayChanged", EventCallback.Factory.Create<bool>(context, value => displayChanged?.Invoke(value)));
                        popupBuilder.AddAttribute(3, "ConnHandler", handler);
                        popupBuilder.AddAttribute(4, "RefreshParent", refreshParent ?? (() => Task.CompletedTask));
                        popupBuilder.AddAttribute(5, "AllowAdminReject", allowAdminReject);
                        popupBuilder.CloseComponent();
                    }));
                    childBuilder.CloseComponent();
                }));
                builder.CloseComponent();
            };

            return context.Render(fragment).FindComponent<RejectInterfacePopup>();
        }

        private sealed class PopupAuthStateProvider : AuthenticationStateProvider
        {
            private readonly ClaimsPrincipal principal;

            public PopupAuthStateProvider(params string[] roles)
            {
                principal = new ClaimsPrincipal(new ClaimsIdentity(
                    roles.Select(role => new Claim(ClaimTypes.Role, role)),
                    "Test",
                    ClaimTypes.Name,
                    ClaimTypes.Role));
            }

            public override Task<AuthenticationState> GetAuthenticationStateAsync()
            {
                return Task.FromResult(new AuthenticationState(principal));
            }
        }

        private sealed class RejectInterfacePopupTestApiConn : SimulatedApiConnection
        {
            public int UpdateConnectionPropertiesCalls { get; private set; }
            public int RemoveSelectedConnectionCalls { get; private set; }

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                if (query == ModellingQueries.updateConnectionProperties)
                {
                    UpdateConnectionPropertiesCalls++;
                    return Task.FromResult((QueryResponseType)(object)new ReturnId { AffectedRows = 1 });
                }
                if (query == ModellingQueries.removeSelectedConnection)
                {
                    RemoveSelectedConnectionCalls++;
                    return Task.FromResult((QueryResponseType)(object)new ReturnId { AffectedRows = 1 });
                }
                throw new AssertionException($"Unexpected query: {query}");
            }
        }
    }
}
