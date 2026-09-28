using System.Security.Claims;
using AngleSharp.Dom;
using Bunit;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data.Provisioning;
using FWO.Ui.Pages.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FWO.Test;

/// <summary>
/// Covers the role-dependent editability of the provisioning settings page: admins can edit the fields,
/// auditors see the same values with every editor disabled.
/// </summary>
[TestFixture]
internal sealed class UiSettingsFwConfigProvisioningTest
{
    private const long kGlobalNodeId = 1;

    private static readonly string kLoggingSwitchSelector = $"#prov-override-{ProvisioningSettingKeys.Logging.DatabaseKey}";
    private static readonly string kLoggingSelectSelector = $"#prov-field-{ProvisioningSettingKeys.Logging.DatabaseKey} select";

    [Test]
    public async Task Auditor_SeesTheStoredValuesWithAllEditorsDisabled()
    {
        await using BunitContext context = CreateContext(Roles.Auditor);

        IRenderedComponent<CascadingAuthenticationState> page = RenderPage(context);

        List<IElement> editors = [.. page.FindAll("form input, form select, form textarea, form button")];
        using (Assert.EnterMultipleScope())
        {
            Assert.That(page.Find(kLoggingSwitchSelector).HasAttribute("checked"), Is.True);
            Assert.That(page.Find(kLoggingSelectSelector).GetAttribute("value"), Is.EqualTo(nameof(ProvisioningLoggingMode.LogTrack)));
            Assert.That(editors, Is.Not.Empty);
            Assert.That(editors.Where(editor => !editor.HasAttribute("disabled")).Select(editor => editor.Id ?? editor.TagName),
                Is.Empty);
        }
    }

    [Test]
    public async Task Admin_CanEditOverridesAndSwitchFields()
    {
        await using BunitContext context = CreateContext(Roles.Admin);

        IRenderedComponent<CascadingAuthenticationState> page = RenderPage(context);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(page.Find(kLoggingSwitchSelector).HasAttribute("disabled"), Is.False);
            Assert.That(page.Find(kLoggingSelectSelector).HasAttribute("disabled"), Is.False);
        }
    }

    [Test]
    public async Task Auditor_TogglingASwitchLeavesTheFormUnchanged()
    {
        await using BunitContext context = CreateContext(Roles.Auditor);
        IRenderedComponent<CascadingAuthenticationState> page = RenderPage(context);

        await page.Find(kLoggingSwitchSelector).ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = false });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(page.Find(kLoggingSwitchSelector).HasAttribute("checked"), Is.True);
            Assert.That(page.FindAll(".alert-warning"), Is.Empty);
        }
    }

    private static IRenderedComponent<CascadingAuthenticationState> RenderPage(BunitContext context)
    {
        IRenderedComponent<CascadingAuthenticationState> page = context.Render<CascadingAuthenticationState>(parameters => parameters
            .AddChildContent<SettingsFwConfigProvisioning>());
        page.WaitForAssertion(() => page.Find(kLoggingSwitchSelector));
        return page;
    }

    private static BunitContext CreateContext(string role)
    {
        InMemoryProvisioningApiConnection api = new();
        api.Managements.AddRange(ProvisioningSettingsDataTest.SampleManagements());
        api.AddNode(kGlobalNodeId, ProvisioningScopeType.Global, "global", displayName: "Global");
        api.SetValue(kGlobalNodeId, ProvisioningSettingKeys.Logging, ProvisioningLoggingMode.LogTrack);

        SimulatedUserConfig userConfig = new();
        userConfig.User.Roles = new List<string> { role };

        BunitContext context = new();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddAuthorizationCore();
        context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
        context.Services.AddSingleton<AuthenticationStateProvider>(new RoleAuthStateProvider(role));
        context.Services.AddSingleton<FWO.Api.Client.ApiConnection>(api);
        context.Services.AddSingleton<UserConfig>(userConfig);
        return context;
    }

    private sealed class RoleAuthStateProvider(string role) : AuthenticationStateProvider
    {
        private readonly List<Claim> claims = new() { new Claim(ClaimTypes.Role, role) };

        private AuthenticationState authenticationState => new(new ClaimsPrincipal(new ClaimsIdentity(
            claims, authenticationType: "Test", nameType: ClaimTypes.Name, roleType: ClaimTypes.Role)));

        public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(authenticationState);
    }
}
