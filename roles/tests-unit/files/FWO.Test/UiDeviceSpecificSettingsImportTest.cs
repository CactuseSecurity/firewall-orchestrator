using System.Reflection;
using Bunit;
using FWO.Api.Client;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data.DeviceSpecificSettings;
using FWO.Services.DeviceSpecificSettings;
using FWO.Ui.Pages.Settings;
using FWO.Ui.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FWO.Test;

/// <summary>
/// Covers the import dialog of the device specific settings: reading a file or a given package, the preview of a
/// check, errors and no-op imports, applying the checked changes, the mode change, and the role gate of the apply button.
/// </summary>
[TestFixture]
internal class UiDeviceSpecificSettingsImportTest
{
    private const string kScUrl = "https://securechange.example";
    private const string kSnapshotName = "before upgrade";
    private const string kCheckButton = "button.btn-primary.mb-3";
    private const string kApplyButton = ".btn-group button.btn-primary";

    private sealed class ImportTestSetup(InMemoryDeviceSpecificSettingsApiConnection api)
    {
        public InMemoryDeviceSpecificSettingsApiConnection Api { get; } = api;
        public IRenderedComponent<DeviceSpecificSettingsImport> Dialog { get; set; } = null!;
        public List<(Exception? Exception, string Title, string Message, bool Error)> Messages { get; } = [];
        public List<bool> DisplayChanges { get; } = [];
        public int ImportedCount { get; set; }
    }

    [Test]
    public async Task AGivenPackageIsShownWithoutFileInput()
    {
        DeviceSpecificSettingsPackage package = await ExportSource();
        await using BunitContext context = new();

        ImportTestSetup setup = Render(context, CreateTarget(), Roles.Admin, package);

        Assert.Multiple(() =>
        {
            Assert.That(setup.Dialog.FindAll("#dss-import-file"), Is.Empty);
            Assert.That(setup.Dialog.Markup, Does.Contain(kSnapshotName));
            Assert.That(setup.Dialog.Find(kApplyButton).HasAttribute("disabled"), Is.True, "nothing is checked yet");
        });
    }

    [Test]
    public async Task CheckingShowsTheChangesWithoutWritingThem()
    {
        InMemoryDeviceSpecificSettingsApiConnection target = CreateTarget();
        await using BunitContext context = new();
        ImportTestSetup setup = Render(context, target, Roles.Admin, await ExportSource());

        setup.Dialog.Find(kCheckButton).Click();

        Assert.Multiple(() =>
        {
            Assert.That(setup.Dialog.Markup, Does.Contain("dss_import_preview"));
            Assert.That(setup.Dialog.Markup, Does.Contain("dss_change_added"));
            Assert.That(setup.Dialog.Markup, Does.Contain(kScUrl));
            Assert.That(target.ReadValue(1, DeviceSpecificSettingKeys.ScUrl), Is.Null, "a check writes nothing");
            Assert.That(setup.Dialog.Find(kApplyButton).HasAttribute("disabled"), Is.False);
            Assert.That(setup.Messages, Is.Empty);
        });
    }

    [Test]
    public async Task ApplyingWritesTheChangesAndClosesTheDialog()
    {
        InMemoryDeviceSpecificSettingsApiConnection target = CreateTarget();
        await using BunitContext context = new();
        ImportTestSetup setup = Render(context, target, Roles.Admin, await ExportSource());

        setup.Dialog.Find(kCheckButton).Click();
        setup.Dialog.Find(kApplyButton).Click();

        Assert.Multiple(() =>
        {
            Assert.That(target.ReadValue(1, DeviceSpecificSettingKeys.ScUrl)?.ToString(), Is.EqualTo(kScUrl));
            Assert.That(setup.ImportedCount, Is.EqualTo(1));
            Assert.That(setup.DisplayChanges, Is.EqualTo(new List<bool> { false }));
            Assert.That(setup.Messages.Single().Error, Is.False);
            Assert.That(setup.Messages.Single().Message, Does.StartWith("dss_import_done 1 dss_changes, 1 dss_levels"));
        });
    }

    [Test]
    public async Task APackageWithoutChangesShowsANoteAndCannotBeApplied()
    {
        InMemoryDeviceSpecificSettingsApiConnection source = CreateSource();
        DeviceSpecificSettingsPackage package = (await CreateTransferService(source).ExportAsync()).Package;
        await using BunitContext context = new();
        ImportTestSetup setup = Render(context, source, Roles.Admin, package);

        setup.Dialog.Find(kCheckButton).Click();

        Assert.Multiple(() =>
        {
            Assert.That(setup.Dialog.Markup, Does.Contain("dss_import_no_changes"));
            Assert.That(setup.Dialog.Find(kApplyButton).HasAttribute("disabled"), Is.True);
        });
    }

    [Test]
    public async Task AnInvalidPackageShowsEveryErrorAsText()
    {
        DeviceSpecificSettingsPackage package = new()
        {
            Nodes = [new DeviceSpecificSettingsPackageNode { Level = "<b>unknown</b>" }, new DeviceSpecificSettingsPackageNode { Level = "nowhere" }]
        };
        await using BunitContext context = new();
        ImportTestSetup setup = Render(context, CreateTarget(), Roles.Admin, package);

        setup.Dialog.Find(kCheckButton).Click();

        Assert.Multiple(() =>
        {
            Assert.That(setup.Dialog.FindAll(".alert-danger li"), Has.Count.GreaterThanOrEqualTo(2));
            Assert.That(setup.Dialog.FindAll(".alert-danger b"), Is.Empty, "messages are rendered as text");
            Assert.That(setup.Dialog.Find(kApplyButton).HasAttribute("disabled"), Is.True);
        });
    }

    [Test]
    public async Task AFailedCheckShowsAnErrorAndNoPreview()
    {
        InMemoryDeviceSpecificSettingsApiConnection target = CreateTarget();
        target.BeforeQueryAsync = _ => throw new InvalidOperationException("database down");
        await using BunitContext context = new();
        ImportTestSetup setup = Render(context, target, Roles.Admin, await ExportSource());

        setup.Dialog.Find(kCheckButton).Click();

        Assert.Multiple(() =>
        {
            Assert.That(setup.Messages.Single().Exception?.Message, Is.EqualTo("database down"));
            Assert.That(setup.Dialog.Markup, Does.Not.Contain("dss_import_preview"));
            Assert.That(setup.Dialog.Find(kCheckButton).HasAttribute("disabled"), Is.False, "the dialog is usable again");
        });
    }

    [Test]
    public async Task AnUploadedFileIsReadAndCanBeChecked()
    {
        string json = DeviceSpecificSettingsPackageJson.Serialize(await ExportSource());
        await using BunitContext context = new();
        ImportTestSetup setup = Render(context, CreateTarget(), Roles.Admin, null);

        setup.Dialog.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText(json, "export.fwo-dss.json"));
        setup.Dialog.Find(kCheckButton).Click();

        Assert.Multiple(() =>
        {
            Assert.That(setup.Dialog.Markup, Does.Contain("export.fwo-dss.json"));
            Assert.That(setup.Dialog.Markup, Does.Contain("dss_change_added"));
            Assert.That(setup.Messages, Is.Empty);
        });
    }

    [Test]
    public async Task AnUnreadableFileShowsAnError()
    {
        await using BunitContext context = new();
        ImportTestSetup setup = Render(context, CreateTarget(), Roles.Admin, null);

        setup.Dialog.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("no json", "broken.json"));

        Assert.Multiple(() =>
        {
            Assert.That(setup.Messages.Single().Message, Is.EqualTo("E5303"));
            Assert.That(setup.Dialog.FindAll(kCheckButton), Is.Empty, "without a package there is nothing to check");
        });
    }

    [Test]
    public async Task ChangingTheModeDiscardsThePreview()
    {
        await using BunitContext context = new();
        ImportTestSetup setup = Render(context, CreateTarget(), Roles.Admin, await ExportSource());
        setup.Dialog.Find(kCheckButton).Click();
        MethodInfo changeMode = typeof(DeviceSpecificSettingsImport).GetMethod("ChangeMode", BindingFlags.Instance | BindingFlags.NonPublic)!;

        await setup.Dialog.InvokeAsync(() => changeMode.Invoke(setup.Dialog.Instance, [DeviceSpecificSettingsImportMode.Replace]));
        setup.Dialog.Render();

        Assert.Multiple(() =>
        {
            Assert.That(setup.Dialog.Markup, Does.Not.Contain("dss_import_preview"));
            Assert.That(setup.Dialog.Find(kApplyButton).HasAttribute("disabled"), Is.True, "the new mode needs a new check");
        });
    }

    [Test]
    public async Task NonAdminsCanCheckButNotApply()
    {
        InMemoryDeviceSpecificSettingsApiConnection target = CreateTarget();
        await using BunitContext context = new();
        ImportTestSetup setup = Render(context, target, Roles.Auditor, await ExportSource());

        setup.Dialog.Find(kCheckButton).Click();

        Assert.Multiple(() =>
        {
            Assert.That(setup.Dialog.Markup, Does.Contain("dss_change_added"));
            Assert.That(setup.Dialog.Find(kApplyButton).HasAttribute("disabled"), Is.True);
        });
    }

    [Test]
    public async Task CancellingNotifiesTheCaller()
    {
        await using BunitContext context = new();
        ImportTestSetup setup = Render(context, CreateTarget(), Roles.Admin, await ExportSource());

        setup.Dialog.Find(".btn-group button.btn-secondary").Click();

        Assert.That(setup.DisplayChanges, Is.EqualTo(new List<bool> { false }));
    }

    private static InMemoryDeviceSpecificSettingsApiConnection CreateSource()
    {
        InMemoryDeviceSpecificSettingsApiConnection api = new();
        api.AddNode(1, DeviceSpecificScopeType.Global, "global", displayName: "Global");
        api.SetValue(1, DeviceSpecificSettingKeys.ScUrl, kScUrl);
        return api;
    }

    private static InMemoryDeviceSpecificSettingsApiConnection CreateTarget()
    {
        InMemoryDeviceSpecificSettingsApiConnection api = new();
        api.AddNode(1, DeviceSpecificScopeType.Global, "global", displayName: "Global");
        return api;
    }

    private static DeviceSpecificSettingsTransferService CreateTransferService(InMemoryDeviceSpecificSettingsApiConnection api) =>
        new(api, new DeviceSpecificSettingsManager(api), "9.9.5");

    private static async Task<DeviceSpecificSettingsPackage> ExportSource() =>
        (await CreateTransferService(CreateSource()).ExportAsync()).Package;

    private static ImportTestSetup Render(BunitContext context, InMemoryDeviceSpecificSettingsApiConnection api, string role,
        DeviceSpecificSettingsPackage? package)
    {
        ImportTestSetup setup = new(api);
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLocalization();
        context.Services.AddAuthorizationCore();
        context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
        context.Services.AddSingleton<AuthenticationStateProvider>(new MonitoringTestAuthStateProvider(role));
        context.Services.AddSingleton<ApiConnection>(api);
        SimulatedUserConfig userConfig = new();
        userConfig.User.Roles = [role];
        context.Services.AddSingleton<UserConfig>(userConfig);
        context.Services.AddSingleton<GlobalConfig>(new SimulatedGlobalConfig { ProductVersion = "9.9.5" });
        context.Services.AddScoped<DomEventService>();

        Action<Exception?, string, string, bool> displayMessage = (exception, title, message, error) => setup.Messages.Add((exception, title, message, error));
        IRenderedComponent<CascadingAuthenticationState> wrapper = context.Render<CascadingAuthenticationState>(parameters => parameters
            .AddChildContent<DeviceSpecificSettingsImport>(child => child
                .AddCascadingValue(displayMessage)
                .Add(p => p.Display, true)
                .Add(p => p.Package, package)
                .Add(p => p.PackageName, package == null ? "" : kSnapshotName)
                .Add(p => p.DisplayChanged, displayed => setup.DisplayChanges.Add(displayed))
                .Add(p => p.Imported, () => setup.ImportedCount++)));
        setup.Dialog = wrapper.FindComponent<DeviceSpecificSettingsImport>();
        return setup;
    }
}
