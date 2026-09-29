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
    public async Task Admin_CanClearDormantOverrideAfterVendorMove()
    {
        InMemoryProvisioningApiConnection api = CreateApi();
        api.AddNode(2, ProvisioningScopeType.DeviceType, "9", kGlobalNodeId, "Check Point R8x");
        api.AddNode(5, ProvisioningScopeType.DeviceType, "11", kGlobalNodeId, "FortiADOM 5ff");
        api.AddNode(3, ProvisioningScopeType.Management, "100", 5, "cp-mgr");
        api.SetValue(3, ProvisioningSettingKeys.ZoneFrom, "internal");
        await using BunitContext context = CreateContext(Roles.Admin, api);
        IRenderedComponent<CascadingAuthenticationState> page = RenderPage(context);

        await TreeButton(page, "Check Point R8x").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        page.WaitForAssertion(() => TreeButton(page, "cp-mgr"));
        await TreeButton(page, "cp-mgr").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        page.WaitForAssertion(() => page.Find(".prov-clear-dormant"));

        IElement dormantValue = page.Find($"#prov-field-{ProvisioningSettingKeys.ZoneFrom.DatabaseKey} input[type=text]");
        IElement clearButton = page.Find(".prov-clear-dormant");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(dormantValue.GetAttribute("value"), Is.EqualTo("internal"));
            Assert.That(dormantValue.HasAttribute("disabled"), Is.True);
            Assert.That(clearButton.HasAttribute("disabled"), Is.False);
        }

        await clearButton.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        await Button(page, "Save").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(api.ReadValue(3, ProvisioningSettingKeys.ZoneFrom), Is.Null);
            Assert.That(page.FindAll(".prov-clear-dormant"), Is.Empty);
        }
    }

    [Test]
    public async Task Saving_DisablesTheEditorAndTreeAndIgnoresQueuedEdits()
    {
        InMemoryProvisioningApiConnection api = CreateApi();
        await using BunitContext context = CreateContext(Roles.Admin, api);
        IRenderedComponent<CascadingAuthenticationState> page = RenderPage(context);
        await page.Find(kLoggingSelectSelector).ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs
        {
            Value = nameof(ProvisioningLoggingMode.None)
        });

        TaskCompletionSource<bool> saveQueryStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> continueSave = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int queryCount = 0;
        api.BeforeQueryAsync = async _ =>
        {
            if (Interlocked.Increment(ref queryCount) == 1)
            {
                saveQueryStarted.TrySetResult(true);
                await continueSave.Task;
            }
        };

        Task saveTask = Button(page, "Save").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        try
        {
            await saveQueryStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            page.WaitForAssertion(() =>
            {
                Assert.That(page.FindAll("form input, form select, form textarea, form button")
                    .All(control => control.HasAttribute("disabled")), Is.True);
                Assert.That(page.FindAll(".col-sm-3 button").All(button => button.HasAttribute("disabled")), Is.True);
                Assert.That(Button(page, "Save").HasAttribute("disabled"), Is.True);
                Assert.That(Button(page, "Cancel").HasAttribute("disabled"), Is.True);
            });

            await page.Find(kLoggingSelectSelector).ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs
            {
                Value = nameof(ProvisioningLoggingMode.Log)
            });
            await page.Find(kLoggingSwitchSelector).ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = false });
            await page.Find(".col-sm-3 button.btn-primary")
                .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        }
        finally
        {
            continueSave.TrySetResult(true);
        }
        await saveTask;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(api.ReadValue(kGlobalNodeId, ProvisioningSettingKeys.Logging)?.ToString(),
                Is.EqualTo(nameof(ProvisioningLoggingMode.None)));
            Assert.That(page.Find(kLoggingSwitchSelector).HasAttribute("checked"), Is.True);
            Assert.That(page.Find(kLoggingSelectSelector).GetAttribute("value"),
                Is.EqualTo(nameof(ProvisioningLoggingMode.None)));
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

    private static IElement Button(IRenderedComponent<CascadingAuthenticationState> page, string text) =>
        page.FindAll("button").Single(button => button.TextContent.Trim() == text ||
            button.QuerySelectorAll("[title]").Any(element => element.GetAttribute("title") == text));

    private static IElement TreeButton(IRenderedComponent<CascadingAuthenticationState> page, string text) =>
        page.FindAll(".col-sm-3 button").Single(button => button.TextContent.Trim() == text);

    private static InMemoryProvisioningApiConnection CreateApi()
    {
        InMemoryProvisioningApiConnection api = new();
        api.Managements.AddRange(ProvisioningSettingsDataTest.SampleManagements());
        api.AddNode(kGlobalNodeId, ProvisioningScopeType.Global, "global", displayName: "Global");
        api.SetValue(kGlobalNodeId, ProvisioningSettingKeys.Logging, ProvisioningLoggingMode.LogTrack);
        return api;
    }

    private static BunitContext CreateContext(string role, InMemoryProvisioningApiConnection? api = null)
    {
        api ??= CreateApi();

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
