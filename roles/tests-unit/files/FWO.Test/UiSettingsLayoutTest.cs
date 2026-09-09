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
        // Sidebar renders its child content only once a navbar height arrived, so every test has to
        // publish one before the settings navigation exists in the rendered markup.
        private const int kNavbarHeight = 50;

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

        private static readonly List<string> ManagementsOnly = new() { "settings/managements" };
        private static readonly List<string> DevicesHeadingOnly = new() { "Devices" };
        private static readonly List<string> PersonalNavigation = new() { "settings/password", "settings/personal" };

        // Every settings page an admin can reach, in navigation order. Adding a settings page
        // is expected to extend this list.
        private static readonly List<string> AdminNavigation = new()
        {
            "settings/credentials",
            "settings/managements",
            "settings/gateways",
            "/settings/matrix",
            "/settings/internet",
            "settings/ldap",
            "settings/tenants",
            "settings/users",
            "settings/groups",
            "settings/roles",
            "settings/owners",
            "settings/owners/responsibles",
            "settings/owners/lifecycles",
            "settings/owners/appdataimport",
            "settings/reportgeneral",
            "settings/recertificationgeneral",
            "settings/compliance",
            "settings/modelling",
            "settings/modellingnotifications",
            "settings/logging",
            "settings/stateactions",
            "settings/statedefinitions",
            "settings/statematrix",
            "settings/workflowcustomizing",
            "settings/flows/general",
            "settings/flows/networkobjects",
            "settings/flows/networkgroups",
            "settings/flows/serviceobjects",
            "settings/flows/servicegroups",
            "settings/flows/timeobjects",
            "settings/defaults",
            "settings/email",
            "settings/importer",
            "settings/changetrigger",
            "settings/notifications",
            "settings/passwordpolicy",
            "settings/customtexts",
            "settings/fwconfigchangegeneral",
            "settings/exttickettemplates",
            "settings/password",
            "settings/personal"
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
            await layout.InvokeAsync(() => eventService.InvokeNavbarHeightChanged(kNavbarHeight));

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
            await layout.InvokeAsync(() => eventService.InvokeNavbarHeightChanged(kNavbarHeight));

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
            await layout.InvokeAsync(() => eventService.InvokeNavbarHeightChanged(kNavbarHeight));

            layout.WaitForAssertion(() =>
            {
                Assert.That(layout.FindAll("a[href='settings/password']"), Is.Empty);
                Assert.That(layout.FindAll("a[href='settings/personal']"), Has.Count.EqualTo(1));
            });
        }

        [Test]
        public async Task SettingsLayout_SearchInput_FiltersRenderedNavigation()
        {
            await using BunitContext context = CreateContext(PrivilegedRoles, CreateInternalDn());
            IRenderedComponent<SettingsLayout> layout = await RenderNavigation(context, Roles.Admin);

            await layout.Find("#settingsSearch").InputAsync(new ChangeEventArgs { Value = "manage" });

            Assert.Multiple(() =>
            {
                Assert.That(NavigationHrefs(layout), Is.EqualTo(ManagementsOnly));
                Assert.That(layout.FindAll("h5").Select(heading => heading.TextContent.Trim()),
                    Is.EqualTo(DevicesHeadingOnly));
            });
        }

        [Test]
        public async Task SettingsLayout_SearchInput_ClearingTheTermRestoresTheFullNavigation()
        {
            await using BunitContext context = CreateContext(PrivilegedRoles, CreateInternalDn());
            IRenderedComponent<SettingsLayout> layout = await RenderNavigation(context, Roles.Admin);
            int fullCount = NavigationHrefs(layout).Count;

            await layout.Find("#settingsSearch").InputAsync(new ChangeEventArgs { Value = "manage" });
            await layout.Find("#settingsSearch").InputAsync(new ChangeEventArgs { Value = "" });

            Assert.That(NavigationHrefs(layout), Has.Count.EqualTo(fullCount));
        }

        [Test]
        public async Task SettingsLayout_SearchInput_ReportsWhenNothingMatches()
        {
            await using BunitContext context = CreateContext(PrivilegedRoles, CreateInternalDn());
            IRenderedComponent<SettingsLayout> layout = await RenderNavigation(context, Roles.Admin);

            await layout.Find("#settingsSearch").InputAsync(new ChangeEventArgs { Value = "qqzzxx" });

            Assert.Multiple(() =>
            {
                Assert.That(NavigationHrefs(layout), Is.Empty);
                Assert.That(layout.Find("[role='status']").TextContent.Trim(), Is.EqualTo("no_search_results"));
            });
        }

        [Test]
        public async Task SettingsLayout_SearchInput_SurvivesAConfigTriggeredRerender()
        {
            await using BunitContext context = CreateContext(PrivilegedRoles, CreateInternalDn());
            SimulatedUserConfig userConfig = TestUserConfig(context);
            IRenderedComponent<SettingsLayout> layout = await RenderNavigation(context, Roles.Admin);

            await layout.Find("#settingsSearch").InputAsync(new ChangeEventArgs { Value = "manage" });
            await layout.InvokeAsync(() => userConfig.SetExecutionMode(Roles.Admin));

            // The filter is part of the render tree, so a re-render may not bring unfiltered entries back.
            layout.WaitForAssertion(() =>
                Assert.That(NavigationHrefs(layout), Is.EqualTo(ManagementsOnly)));
        }

        [Test]
        public async Task SettingsLayout_RendersTheDocumentedNavigation_ForAdmin()
        {
            await using BunitContext context = CreateContext(PrivilegedRoles, CreateInternalDn());
            IRenderedComponent<SettingsLayout> layout = await RenderNavigation(context, Roles.Admin);

            Assert.That(NavigationHrefs(layout), Is.EqualTo(AdminNavigation));
        }

        [Test]
        public async Task SettingsLayout_HidesFlowSection_ForAuditor()
        {
            await using BunitContext context = CreateContext(SingleRole(Roles.Auditor), CreateInternalDn());
            IRenderedComponent<SettingsLayout> layout = await RenderNavigation(context, Roles.Auditor);
            List<string> hrefs = NavigationHrefs(layout);

            Assert.Multiple(() =>
            {
                Assert.That(hrefs.Where(href => href.StartsWith("settings/flows/")), Is.Empty);
                Assert.That(hrefs, Contains.Item("settings/users"));
                Assert.That(hrefs, Contains.Item("settings/customtexts"));
            });
        }

        [Test]
        public async Task SettingsLayout_HidesUserAdministration_ForFwAdmin()
        {
            await using BunitContext context = CreateContext(SingleRole(Roles.FwAdmin), CreateInternalDn());
            IRenderedComponent<SettingsLayout> layout = await RenderNavigation(context, Roles.FwAdmin);
            List<string> hrefs = NavigationHrefs(layout);

            Assert.Multiple(() =>
            {
                Assert.That(hrefs, Contains.Item("settings/tenants"));
                Assert.That(hrefs, Does.Not.Contain("settings/users"));
                Assert.That(hrefs, Does.Not.Contain("settings/ldap"));
                Assert.That(hrefs, Does.Not.Contain("settings/defaults"));
                Assert.That(hrefs, Contains.Item("settings/managements"));
            });
        }

        [Test]
        public async Task SettingsLayout_RendersOnlyPersonalSection_ForImporter()
        {
            await using BunitContext context = CreateContext(SingleRole(Roles.Importer), CreateInternalDn());
            IRenderedComponent<SettingsLayout> layout = await RenderNavigation(context, Roles.Importer);

            Assert.That(NavigationHrefs(layout),
                Is.EqualTo(PersonalNavigation));
        }

        [Test]
        public async Task SettingsLayout_RendersNoEmptyListItem_ForExternalUser()
        {
            await using BunitContext context = CreateContext(PrivilegedRoles, "uid=tester,ou=people,dc=example,dc=org");
            IRenderedComponent<SettingsLayout> layout = await RenderNavigation(context, Roles.Admin);

            Assert.That(layout.FindAll("ul.navbar-nav > li").Where(item => item.TextContent.Trim().Length == 0
                && item.QuerySelector("hr") == null), Is.Empty);
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
            await layout.InvokeAsync(() => eventService.InvokeNavbarHeightChanged(kNavbarHeight));
            layout.WaitForAssertion(() => Assert.That(layout.FindAll("a[href='settings/personal']"), Has.Count.EqualTo(1)));

            layout.Instance.Dispose();

            Assert.DoesNotThrow(() => userConfig.SetExecutionMode(Roles.Admin));
        }

        private static List<string> SingleRole(string role)
        {
            return new() { role };
        }

        private static SimulatedUserConfig TestUserConfig(BunitContext context)
        {
            return context.Services.GetRequiredService<UserConfig>() as SimulatedUserConfig
                ?? throw new InvalidOperationException("Test user config missing.");
        }

        private static async Task<IRenderedComponent<SettingsLayout>> RenderNavigation(BunitContext context, string executionMode)
        {
            TestUserConfig(context).SetExecutionMode(executionMode);

            IRenderedComponent<CascadingAuthenticationState> wrapper = RenderLayout(context);
            IRenderedComponent<SettingsLayout> layout = wrapper.FindComponent<SettingsLayout>();
            DomEventService eventService = context.Services.GetRequiredService<DomEventService>();

            layout.WaitForAssertion(() => Assert.That(GetNavbarHeightSubscriberCount(eventService), Is.EqualTo(1)));
            await layout.InvokeAsync(() => eventService.InvokeNavbarHeightChanged(kNavbarHeight));
            return layout;
        }

        private static List<string> NavigationHrefs(IRenderedComponent<SettingsLayout> layout)
        {
            return layout.FindAll("ul.navbar-nav a")
                .Select(anchor => anchor.GetAttribute("href") ?? "")
                .ToList();
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
