using Bunit;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Ui.Services;
using FWO.Ui.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Reflection;
using System.Linq;
using System.Security.Claims;

namespace FWO.Test
{
    [TestFixture]
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    internal class UiSettingsLayoutTest
    {
        private static readonly FieldInfo NavbarHeightSubscribersField = typeof(DomEventService).GetField("_navbarHeightSubscribers", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(typeof(DomEventService).FullName, "_navbarHeightSubscribers");

        private static readonly List<string> PrivilegedRoles = new()
        {
            Roles.Admin,
            Roles.FwAdmin,
            Roles.Auditor,
            Roles.Modeller,
            Roles.Recertifier,
            Roles.Reporter,
            Roles.ReporterViewAll,
            Roles.WorkflowRolesList
        };

        private static readonly List<string> WorkflowOnlyRoles = new()
        {
            Roles.WorkflowRolesList
        };

        [Test]
        public async Task SettingsLayout_RendersSidebarSections_ForPrivilegedInternalUser()
        {
            await using BunitContext context = CreateContext(PrivilegedRoles, CreateInternalDn());
            SimulatedUserConfig userConfig = context.Services.GetRequiredService<UserConfig>() as SimulatedUserConfig
                ?? throw new InvalidOperationException("Test user config missing.");
            userConfig.SetExecutionMode(Roles.Admin);

            IRenderedComponent<CascadingAuthenticationState> wrapper = RenderLayout(context);
            IRenderedComponent<SettingsLayout> layout = wrapper.FindComponent<SettingsLayout>();
            DomEventService eventService = context.Services.GetRequiredService<DomEventService>();

            layout.WaitForAssertion(() => Assert.That(GetNavbarHeightSubscriberCount(eventService), Is.EqualTo(1)));
            await layout.InvokeAsync(() => eventService.InvokeNavbarHeightChanged(50));

            layout.WaitForAssertion(() =>
            {
                Assert.That(layout.FindAll("a[href='settings/modelling']"), Has.Count.EqualTo(1));
                Assert.That(layout.FindAll("a[href='settings/modellingnotifications']"), Has.Count.EqualTo(1));
                Assert.That(layout.FindAll("a[href='settings/logging']"), Has.Count.EqualTo(1));
                Assert.That(layout.FindAll("a[href='settings/personal']"), Has.Count.EqualTo(1));
                Assert.That(layout.FindAll("a[href='settings/password']"), Has.Count.EqualTo(1));
                Assert.That(layout.Markup, Does.Contain("Modelling"));
                Assert.That(layout.Markup, Does.Contain("Personal settings"));
            });
        }

        [Test]
        public async Task SettingsLayout_HidesRoleGatedSections_ForWorkflowOnlyUser()
        {
            await using BunitContext context = CreateContext(WorkflowOnlyRoles, CreateInternalDn());

            IRenderedComponent<CascadingAuthenticationState> wrapper = RenderLayout(context);
            IRenderedComponent<SettingsLayout> layout = wrapper.FindComponent<SettingsLayout>();
            DomEventService eventService = context.Services.GetRequiredService<DomEventService>();

            layout.WaitForAssertion(() => Assert.That(GetNavbarHeightSubscriberCount(eventService), Is.EqualTo(1)));
            await layout.InvokeAsync(() => eventService.InvokeNavbarHeightChanged(50));

            layout.WaitForAssertion(() =>
            {
                Assert.That(layout.FindAll("a[href='settings/modelling']"), Is.Empty);
                Assert.That(layout.FindAll("a[href='settings/modellingnotifications']"), Is.Empty);
                Assert.That(layout.FindAll("a[href='settings/logging']"), Is.Empty);
                Assert.That(layout.FindAll("a[href='settings/defaults']"), Is.Empty);
                Assert.That(layout.FindAll("a[href='settings/personal']"), Has.Count.EqualTo(1));
            });
        }

        [Test]
        public async Task SettingsLayout_HidesPasswordLink_ForExternalUser()
        {
            await using BunitContext context = CreateContext(PrivilegedRoles, "uid=tester,ou=people,dc=example,dc=org");
            SimulatedUserConfig userConfig = context.Services.GetRequiredService<UserConfig>() as SimulatedUserConfig
                ?? throw new InvalidOperationException("Test user config missing.");
            userConfig.SetExecutionMode(Roles.Admin);

            IRenderedComponent<CascadingAuthenticationState> wrapper = RenderLayout(context);
            IRenderedComponent<SettingsLayout> layout = wrapper.FindComponent<SettingsLayout>();
            DomEventService eventService = context.Services.GetRequiredService<DomEventService>();

            layout.WaitForAssertion(() => Assert.That(GetNavbarHeightSubscriberCount(eventService), Is.EqualTo(1)));
            await layout.InvokeAsync(() => eventService.InvokeNavbarHeightChanged(50));

            layout.WaitForAssertion(() =>
            {
                Assert.That(layout.FindAll("a[href='settings/password']"), Is.Empty);
                Assert.That(layout.FindAll("a[href='settings/personal']"), Has.Count.EqualTo(1));
            });
        }

        [Test]
        public async Task SettingsLayout_Dispose_UnsubscribesFromUserConfigChanges()
        {
            await using BunitContext context = CreateContext(PrivilegedRoles, CreateInternalDn());
            SimulatedUserConfig userConfig = context.Services.GetRequiredService<UserConfig>() as SimulatedUserConfig
                ?? throw new InvalidOperationException("Test user config missing.");

            IRenderedComponent<CascadingAuthenticationState> wrapper = RenderLayout(context);
            IRenderedComponent<SettingsLayout> layout = wrapper.FindComponent<SettingsLayout>();
            DomEventService eventService = context.Services.GetRequiredService<DomEventService>();

            layout.WaitForAssertion(() => Assert.That(GetNavbarHeightSubscriberCount(eventService), Is.EqualTo(1)));
            await layout.InvokeAsync(() => eventService.InvokeNavbarHeightChanged(50));
            layout.WaitForAssertion(() => Assert.That(layout.FindAll("a[href='settings/personal']"), Has.Count.EqualTo(1)));

            layout.Instance.Dispose();

            Assert.DoesNotThrow(() => userConfig.SetExecutionMode(Roles.Admin));
        }

        private static BunitContext CreateContext(IEnumerable<string> roles, string userDn)
        {
            SimulatedUserConfig userConfig = new()
            {
                User =
                {
                    Dn = userDn,
                    Roles = roles.ToList()
                }
            };

            BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddScoped(_ => context.JSInterop.JSRuntime);
            context.Services.AddSingleton<AuthenticationStateProvider>(new SettingsLayoutAuthStateProvider(roles));
            context.Services.AddSingleton<DomEventService>();
            context.Services.AddSingleton<UserConfig>(userConfig);
            return context;
        }

        private static IRenderedComponent<CascadingAuthenticationState> RenderLayout(BunitContext context)
        {
            return context.Render<CascadingAuthenticationState>(parameters => parameters
                .AddChildContent<SettingsLayout>());
        }

        private static string CreateInternalDn()
        {
            return $"uid=tester,ou=people,{GlobalConst.kLdapInternalPostfix}";
        }

        private static int GetNavbarHeightSubscriberCount(DomEventService eventService)
        {
            MulticastDelegate? subscribers = NavbarHeightSubscribersField.GetValue(eventService) as MulticastDelegate;
            return subscribers?.GetInvocationList().Length ?? 0;
        }

        private sealed class SettingsLayoutAuthStateProvider : AuthenticationStateProvider
        {
            private readonly AuthenticationState authenticationState;

            public SettingsLayoutAuthStateProvider(IEnumerable<string> roles)
            {
                ClaimsIdentity identity = new(
                    roles.Select(role => new Claim(ClaimTypes.Role, role)),
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
