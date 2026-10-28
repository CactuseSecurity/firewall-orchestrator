using Bunit;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Config.Api.Data;
using FWO.Ui.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Security.Claims;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class UiUncheckedCertificatesWarningTest
    {
        private const string kWarningSelector = "[data-testid='unchecked-certificates-warning']";
        private static readonly List<string> kAllSettingsPages = ["/settings/importer", "/settings/exttickettemplates", "/settings/email"];
        private static readonly List<string> kTicketSystemSettingsPage = ["/settings/exttickettemplates"];

        [Test]
        public async Task ShowsNothingWhileAllChecksAreOn()
        {
            await using BunitContext context = new();
            IRenderedComponent<CascadingAuthenticationState> rendered = Render(context, new SimulatedGlobalConfig(), Roles.Admin);

            Assert.That(rendered.FindAll(kWarningSelector), Is.Empty);
        }

        [Test]
        public async Task ShowsEveryUncheckedConnectionTypeToAdmins()
        {
            await using BunitContext context = new();
            SimulatedGlobalConfig globalConfig = new()
            {
                ImportCheckCertificates = false,
                ExtTicketSystemsCheckCertificates = false,
                EmailCheckCertificates = false
            };

            IRenderedComponent<CascadingAuthenticationState> rendered = Render(context, globalConfig, Roles.Admin);

            List<string?> links = rendered.Find(kWarningSelector).QuerySelectorAll("a").Select(link => link.GetAttribute("href")).ToList();
            Assert.That(links, Is.EqualTo(kAllSettingsPages));
        }

        [Test]
        public async Task HidesTheWarningFromOtherRoles()
        {
            await using BunitContext context = new();
            SimulatedGlobalConfig globalConfig = new() { ImportCheckCertificates = false };

            IRenderedComponent<CascadingAuthenticationState> rendered = Render(context, globalConfig, Roles.Reporter);

            Assert.That(rendered.FindAll(kWarningSelector), Is.Empty);
        }

        [Test]
        public async Task FollowsConfigChanges()
        {
            await using BunitContext context = new();
            SimulatedGlobalConfig globalConfig = new() { ImportCheckCertificates = false, ExtTicketSystemsCheckCertificates = false };
            IRenderedComponent<CascadingAuthenticationState> rendered = Render(context, globalConfig, Roles.Admin);
            List<ConfigItem> firewallChecksOn =
            [
                new() { Key = ConfigData.kImportCheckCertificatesKey, Value = "True" },
                new() { Key = ConfigData.kExtTicketSystemsCheckCertificatesKey, Value = "False" },
                new() { Key = ConfigData.kEmailCheckCertificatesKey, Value = "True" }
            ];
            List<ConfigItem> allChecksOn =
            [
                new() { Key = ConfigData.kImportCheckCertificatesKey, Value = "True" },
                new() { Key = ConfigData.kExtTicketSystemsCheckCertificatesKey, Value = "True" },
                new() { Key = ConfigData.kEmailCheckCertificatesKey, Value = "True" }
            ];

            globalConfig.MergeSubscriptionUpdateHandler(firewallChecksOn.ToArray());

            rendered.WaitForAssertion(() =>
            {
                List<string?> links = rendered.Find(kWarningSelector).QuerySelectorAll("a").Select(link => link.GetAttribute("href")).ToList();
                Assert.That(links, Is.EqualTo(kTicketSystemSettingsPage));
            });

            globalConfig.MergeSubscriptionUpdateHandler(allChecksOn.ToArray());

            rendered.WaitForAssertion(() => Assert.That(rendered.FindAll(kWarningSelector), Is.Empty));
        }

        private static IRenderedComponent<CascadingAuthenticationState> Render(BunitContext context, SimulatedGlobalConfig globalConfig, string role)
        {
            SimulatedUserConfig userConfig = new();
            userConfig.User.Roles = [role];
            context.Services.AddSingleton<GlobalConfig>(globalConfig);
            context.Services.AddSingleton<UserConfig>(userConfig);
            context.Services.AddAuthorizationCore();
            // the admin check under test is the role check of ExecutionModeAuthorizeView against the user config
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton<AuthenticationStateProvider>(new RoleAuthStateProvider(role));
            return context.Render<CascadingAuthenticationState>(parameters => parameters.AddChildContent<UncheckedCertificatesWarning>());
        }

        private sealed class RoleAuthStateProvider : AuthenticationStateProvider
        {
            private readonly AuthenticationState authenticationState;

            public RoleAuthStateProvider(string role)
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
