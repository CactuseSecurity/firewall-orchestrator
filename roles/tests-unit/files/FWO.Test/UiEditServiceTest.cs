using Bunit;
using FWO.Api.Client;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Services.Modelling;
using FWO.Ui.Pages.NetworkModelling;
using FWO.Ui.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Reflection;

namespace FWO.Test
{
    [TestFixture]
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    internal class UiEditServiceTest
    {
        private static SimulatedUserConfig CreateUserConfig()
        {
            SimulatedUserConfig userConfig = new();
            SimulatedUserConfig.DummyTranslate.TryAdd("add_service", "Add service");
            SimulatedUserConfig.DummyTranslate.TryAdd("edit_service", "Edit service");
            SimulatedUserConfig.DummyTranslate.TryAdd("protocol", "Protocol");
            SimulatedUserConfig.DummyTranslate.TryAdd("port", "Port");
            SimulatedUserConfig.DummyTranslate.TryAdd("save_service", "Save service");
            SimulatedUserConfig.DummyTranslate.TryAdd("E5102", "Missing protocol or port");
            SimulatedUserConfig.DummyTranslate.TryAdd("E5103", "Port invalid");
            SimulatedUserConfig.DummyTranslate.TryAdd("E5118", "Port range invalid");
            SimulatedUserConfig.DummyTranslate.TryAdd("U0001", "Saved");
            SimulatedUserConfig.DummyTranslate.TryAdd("fetch_data", "Fetch data");
            userConfig.User.Roles = [Roles.Admin];
            return userConfig;
        }

        private static BunitContext CreateContext(RecordingSettingsApiConn apiConn, SimulatedUserConfig userConfig)
        {
            BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton<AuthenticationStateProvider>(new AllowAllAuthStateProvider(Roles.Admin));
            context.Services.AddSingleton<DomEventService>();
            context.Services.AddSingleton<ApiConnection>(apiConn);
            context.Services.AddSingleton<UserConfig>(userConfig);
            return context;
        }

        private static IRenderedComponent<EditService> RenderComponent(
            BunitContext context,
            ModellingServiceHandler handler,
            bool display = true,
            bool asAdmin = true,
            Action<bool>? displayChanged = null,
            Func<Task>? refreshParent = null)
        {
            return context.Render<CascadingAuthenticationState>(parameters => parameters
                .AddChildContent<EditService>(child => child
                    .Add(p => p.Display, display)
                    .Add(p => p.DisplayChanged, value => displayChanged?.Invoke(value))
                    .Add(p => p.ServiceHandler, handler)
                    .Add(p => p.AsAdmin, asAdmin)
                    .Add(p => p.RefreshParent, refreshParent ?? (() => Task.CompletedTask))))
                .FindComponent<EditService>();
        }

        private static ModellingServiceHandler CreateHandler(
            RecordingSettingsApiConn apiConn,
            SimulatedUserConfig userConfig,
            ModellingService service,
            bool addMode = false)
        {
            FwoOwner owner = new() { Id = 11, Name = "Owner A" };
            List<ModellingService> availableServices = [service];
            return new ModellingServiceHandler(
                apiConn,
                userConfig,
                owner,
                service,
                availableServices,
                [],
                addMode,
                (_, _, _, _) => { });
        }

        [Test]
        public async Task EditService_AddMode_SelectsFirstAvailableProtocolAndShowsPortInputs()
        {
            RecordingSettingsApiConn apiConn = new()
            {
                IpProtocols =
                [
                    new() { Id = 17, Name = "udp" },
                    new() { Id = 1, Name = "icmp" },
                    new() { Id = 6, Name = "tcp" }
                ]
            };
            SimulatedUserConfig userConfig = CreateUserConfig();
            userConfig.ReducedProtocolSet = false;
            await using BunitContext context = CreateContext(apiConn, userConfig);
            ModellingService service = new()
            {
                Name = "svc",
                Protocol = new NetworkProtocol { Id = 0 },
                Port = 80,
                PortEnd = 80
            };
            ModellingServiceHandler handler = CreateHandler(apiConn, userConfig, service, addMode: true);

            IRenderedComponent<EditService> component = RenderComponent(context, handler, display: true, asAdmin: true);
            await component.InvokeAsync(() => Task.CompletedTask);

            component.WaitForAssertion(() =>
            {
                Assert.That(handler.ActService.Protocol, Is.Not.Null);
                Assert.That(handler.ActService.Protocol!.Id, Is.EqualTo(6));
                Assert.That(handler.ActService.Protocol.Name, Is.EqualTo("tcp"));
                Assert.That(component.FindAll("input[type=number]"), Has.Count.EqualTo(2));
            });
        }

        [Test]
        public async Task EditService_EditMode_HidesPortInputsForProtocolsWithoutPorts()
        {
            RecordingSettingsApiConn apiConn = new()
            {
                IpProtocols =
                [
                    new() { Id = 1, Name = "icmp" },
                    new() { Id = 6, Name = "tcp" }
                ]
            };
            SimulatedUserConfig userConfig = CreateUserConfig();
            await using BunitContext context = CreateContext(apiConn, userConfig);
            ModellingService service = new()
            {
                Name = "svc",
                Protocol = new NetworkProtocol { Id = 1, Name = "icmp" }
            };
            ModellingServiceHandler handler = CreateHandler(apiConn, userConfig, service, addMode: false);

            IRenderedComponent<EditService> component = RenderComponent(context, handler);
            component.WaitForAssertion(() =>
            {
                Assert.That(handler.ActService.Protocol!.Id, Is.EqualTo(1));
                Assert.That(component.FindAll("input[type=number]"), Is.Empty);
            });
        }

        [Test]
        public async Task EditService_CancelRestoresOriginalServiceAndClosesPopup()
        {
            RecordingSettingsApiConn apiConn = new()
            {
                IpProtocols = [new() { Id = 6, Name = "tcp" }]
            };
            SimulatedUserConfig userConfig = CreateUserConfig();
            await using BunitContext context = CreateContext(apiConn, userConfig);
            ModellingService service = new()
            {
                Name = "original",
                Protocol = new NetworkProtocol { Id = 6, Name = "tcp" },
                Port = 80,
                PortEnd = 80
            };
            ModellingServiceHandler handler = CreateHandler(apiConn, userConfig, service, addMode: false);
            bool displayChanged = true;
            IRenderedComponent<EditService> component = RenderComponent(context, handler, displayChanged: value => displayChanged = value);

            component.WaitForAssertion(() => Assert.That(component.FindAll("input[type=text]"), Is.Not.Empty));
            component.Find("input[type=text]").Change("changed");
            component.FindAll("button").First(button => button.InnerHtml.Contains("Cancel")).Click();

            Assert.Multiple(() =>
            {
                Assert.That(handler.ActService.Name, Is.EqualTo("original"));
                Assert.That(displayChanged, Is.False);
            });
        }

        [Test]
        public async Task EditService_SaveCallsRefreshParentWhenServiceChanged()
        {
            RecordingSettingsApiConn apiConn = new()
            {
                IpProtocols = [new() { Id = 6, Name = "tcp" }]
            };
            SimulatedUserConfig userConfig = CreateUserConfig();
            await using BunitContext context = CreateContext(apiConn, userConfig);
            ModellingService service = new()
            {
                Name = "original",
                Protocol = new NetworkProtocol { Id = 6, Name = "tcp" },
                Port = 80,
                PortEnd = 80
            };
            ModellingServiceHandler handler = CreateHandler(apiConn, userConfig, service, addMode: false);
            int refreshCount = 0;
            bool displayChanged = true;
            IRenderedComponent<EditService> component = RenderComponent(context, handler,
                displayChanged: value => displayChanged = value,
                refreshParent: () =>
                {
                    refreshCount++;
                    return Task.CompletedTask;
                });

            component.WaitForAssertion(() => Assert.That(component.FindAll("input[type=text]"), Is.Not.Empty));
            component.Find("input[type=text]").Change("changed");
            component.FindAll("button").First(button => button.InnerHtml.Contains("Save")).Click();

            component.WaitForAssertion(() =>
            {
                Assert.That(refreshCount, Is.EqualTo(1));
                Assert.That(displayChanged, Is.False);
                Assert.That(apiConn.Queries, Does.Contain(FWO.Api.Client.Queries.ModellingQueries.updateService));
                Assert.That(apiConn.Queries, Does.Contain(FWO.Api.Client.Queries.ModellingQueries.addHistoryEntry));
            });
        }

        [Test]
        public async Task EditService_SaveDoesNotRefreshParentWhenServiceUnchanged()
        {
            RecordingSettingsApiConn apiConn = new()
            {
                IpProtocols = [new() { Id = 6, Name = "tcp" }]
            };
            SimulatedUserConfig userConfig = CreateUserConfig();
            await using BunitContext context = CreateContext(apiConn, userConfig);
            ModellingService service = new()
            {
                Name = "original",
                Protocol = new NetworkProtocol { Id = 6, Name = "tcp" },
                Port = 80,
                PortEnd = 80
            };
            ModellingServiceHandler handler = CreateHandler(apiConn, userConfig, service, addMode: false);
            int refreshCount = 0;
            bool displayChanged = true;
            IRenderedComponent<EditService> component = RenderComponent(context, handler,
                displayChanged: value => displayChanged = value,
                refreshParent: () =>
                {
                    refreshCount++;
                    return Task.CompletedTask;
                });

            component.WaitForAssertion(() => Assert.That(component.FindAll("input[type=text]"), Is.Not.Empty));
            component.FindAll("button").First(button => button.InnerHtml.Contains("Save")).Click();

            component.WaitForAssertion(() =>
            {
                Assert.That(refreshCount, Is.Zero);
                Assert.That(displayChanged, Is.False);
                Assert.That(apiConn.Queries, Does.Contain(FWO.Api.Client.Queries.ModellingQueries.updateService));
            });
        }

        [Test]
        public async Task EditService_SaveButtonIsDisabledForNonOwner()
        {
            RecordingSettingsApiConn apiConn = new()
            {
                IpProtocols = [new() { Id = 6, Name = "tcp" }]
            };
            SimulatedUserConfig userConfig = CreateUserConfig();
            await using BunitContext context = CreateContext(apiConn, userConfig);
            ModellingService service = new()
            {
                Name = "svc",
                Protocol = new NetworkProtocol { Id = 6, Name = "tcp" },
                Port = 80,
                PortEnd = 80
            };
            ModellingServiceHandler handler = CreateHandler(apiConn, userConfig, service, addMode: false);
            handler.IsOwner = false;

            IRenderedComponent<EditService> component = RenderComponent(context, handler, asAdmin: true);

            component.WaitForAssertion(() => Assert.That(component.FindAll("button").Any(button => button.InnerHtml.Contains("Save")), Is.True));
            Assert.That(component.FindAll("button").First(button => button.InnerHtml.Contains("Save")).HasAttribute("disabled"), Is.True);
        }
    }
}
