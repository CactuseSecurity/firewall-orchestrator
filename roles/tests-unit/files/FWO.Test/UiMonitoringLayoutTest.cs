using Bunit;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Ui.Services;
using FWO.Ui.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Reflection;
using System.Security.Claims;

namespace FWO.Test
{
    [TestFixture]
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    internal class UiMonitoringLayoutTest
    {
        [Test]
        public async Task MonitoringLayout_ShowsPrivilegedSectionsAndNotificationsForAdmin()
        {
            await using BunitContext context = CreateContext(Roles.Admin);

            IRenderedComponent<MonitoringLayout> layout = RenderLayout(context);
            DomEventService eventService = context.Services.GetRequiredService<DomEventService>();
            layout.WaitForAssertion(() => Assert.That(GetNavbarHeightSubscriberCount(eventService), Is.EqualTo(1)));
            await layout.InvokeAsync(() => eventService.InvokeNavbarHeightChanged(50));
            layout.WaitForAssertion(() => Assert.That(layout.FindAll("a[href='monitoring/email_log']"), Has.Count.EqualTo(1)));

            Assert.Multiple(() =>
            {
                Assert.That(layout.FindAll("h5").Select(element => element.TextContent), Does.Contain("Notifications"));
                Assert.That(layout.FindAll("a[href='monitoring/email_log']"), Has.Count.EqualTo(1));
                Assert.That(layout.FindAll("a[href='monitoring/system_usage']"), Has.Count.EqualTo(1));
                Assert.That(layout.FindAll("a[href='monitoring/ui_messages']"), Has.Count.EqualTo(1));
            });
        }

        [Test]
        public async Task MonitoringLayout_HidesPrivilegedSectionsForRegularUser()
        {
            await using BunitContext context = CreateContext(Roles.Requester);

            IRenderedComponent<MonitoringLayout> layout = RenderLayout(context);
            DomEventService eventService = context.Services.GetRequiredService<DomEventService>();
            layout.WaitForAssertion(() => Assert.That(GetNavbarHeightSubscriberCount(eventService), Is.EqualTo(1)));
            await layout.InvokeAsync(() => eventService.InvokeNavbarHeightChanged(50));
            layout.WaitForAssertion(() => Assert.That(layout.FindAll("a[href='monitoring/ui_messages']"), Has.Count.EqualTo(1)));

            Assert.Multiple(() =>
            {
                Assert.That(layout.FindAll("a[href='monitoring/email_log']"), Is.Empty);
                Assert.That(layout.FindAll("a[href='monitoring/system_usage']"), Is.Empty);
                Assert.That(layout.FindAll("a[href='monitoring/ui_messages']"), Has.Count.EqualTo(1));
                Assert.That(layout.FindAll("a[href='monitoring/monitor_all']"), Is.Empty);
            });
        }

        [Test]
        public async Task MonitoringLayout_DisposeUnsubscribesFromUserConfigChanges()
        {
            await using BunitContext context = CreateContext(Roles.Admin);
            SimulatedUserConfig userConfig = (SimulatedUserConfig)context.Services.GetRequiredService<UserConfig>();
            IRenderedComponent<MonitoringLayout> layout = RenderLayout(context);

            layout.Instance.Dispose();

            Assert.DoesNotThrow(() => userConfig.SetExecutionMode(Roles.Auditor));
        }

        private static BunitContext CreateContext(string role)
        {
            SimulatedUserConfig userConfig = new();
            userConfig.User.Roles = new List<string> { role };
            userConfig.SetExecutionMode(role);

            BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton<AuthenticationStateProvider>(new MonitoringLayoutAuthStateProvider(role));
            context.Services.AddSingleton<DomEventService>(new TestDomEventService());
            context.Services.AddSingleton<UserConfig>(userConfig);
            return context;
        }

        private static IRenderedComponent<MonitoringLayout> RenderLayout(BunitContext context)
        {
            return context.Render<CascadingAuthenticationState>(parameters => parameters
                .AddChildContent<MonitoringLayout>())
                .FindComponent<MonitoringLayout>();
        }

        private static int GetNavbarHeightSubscriberCount(DomEventService eventService)
        {
            FieldInfo? field = typeof(DomEventService).GetField("_navbarHeightSubscribers", BindingFlags.Instance | BindingFlags.NonPublic);
            MulticastDelegate? subscribers = field?.GetValue(eventService) as MulticastDelegate;
            return subscribers?.GetInvocationList().Length ?? 0;
        }

        private sealed class TestDomEventService : DomEventService, IDisposable
        {
            public void Dispose()
            {
            }
        }

        private sealed class MonitoringLayoutAuthStateProvider : AuthenticationStateProvider
        {
            private readonly AuthenticationState authenticationState;

            public MonitoringLayoutAuthStateProvider(string role)
            {
                ClaimsIdentity identity = new(
                    new List<Claim> { new(ClaimTypes.Role, role) },
                    authenticationType: "Test",
                    nameType: ClaimTypes.Name,
                    roleType: ClaimTypes.Role);
                authenticationState = new AuthenticationState(new ClaimsPrincipal(identity));
            }

            public override Task<AuthenticationState> GetAuthenticationStateAsync()
            {
                return Task.FromResult(authenticationState);
            }
        }
    }
}
