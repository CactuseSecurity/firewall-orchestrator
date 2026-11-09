using AngleSharp.Dom;
using Bunit;
using FWO.Api.Client;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Middleware.Client;
using FWO.Test.Mocks;
using FWO.Ui.Pages.Settings;
using FWO.Ui.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Tests that admins and auditors can open the tenant dialogs, while only admins can save.
    /// </summary>
    [TestFixture]
    internal class UiSettingsTenantsTest
    {
        [TestCase(Roles.Auditor, false)]
        [TestCase(Roles.Admin, true)]
        public async Task AddTenantOpensForAdminAndAuditorButOnlyAdminCanSave(string role, bool writeAllowed)
        {
            await using BunitContext context = CreateContext(role, out SimulatedUserConfig userConfig);

            IRenderedComponent<SettingsTenants> page = context.Render<CascadingAuthenticationState>(parameters =>
                parameters.AddChildContent<SettingsTenants>()).FindComponent<SettingsTenants>();

            IElement addButton = page.WaitForElement("button.btn-success");
            Assert.That(addButton.TextContent.Trim(), Is.EqualTo(userConfig.GetText("add_tenant")));
            Assert.That(addButton.HasAttribute("disabled"), Is.False);

            await page.InvokeAsync(() => addButton.Click());

            page.WaitForAssertion(() =>
            {
                List<IElement> saveButtons = [.. page.FindAll("button").Where(button => button.TextContent.Trim() == userConfig.GetText("save"))];
                Assert.That(saveButtons, Has.Count.EqualTo(1));
                Assert.That(saveButtons[0].HasAttribute("disabled"), Is.EqualTo(!writeAllowed));
                Assert.That(page.FindAll("button").Any(button => button.TextContent.Trim() == userConfig.GetText("delete_tenant")), Is.False);
            });
        }

        private static BunitContext CreateContext(string role, out SimulatedUserConfig userConfig)
        {
            BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddLocalization();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton<AuthenticationStateProvider>(new MonitoringTestAuthStateProvider(role));
            context.Services.AddSingleton<ApiConnection>(new SimulatedApiConnection());
            context.Services.AddSingleton<MiddlewareClient>(new TestMiddlewareClient());
            context.Services.AddScoped<DomEventService>();
            userConfig = new SimulatedUserConfig { ModIconify = false };
            userConfig.User.Roles = [role];
            context.Services.AddSingleton<UserConfig>(userConfig);
            return context;
        }
    }
}
