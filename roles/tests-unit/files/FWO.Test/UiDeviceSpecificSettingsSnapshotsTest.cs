using Bunit;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data.DeviceSpecificSettings;
using FWO.Services.DeviceSpecificSettings;
using FWO.Ui.Pages.Settings;
using FWO.Ui.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FWO.Test;

/// <summary>
/// Covers the snapshot dialog of the device specific settings: listing, storing the current settings, applying,
/// downloading and deleting snapshots, the file name of a download, and the error messages shown on failures.
/// </summary>
[TestFixture]
internal class UiDeviceSpecificSettingsSnapshotsTest
{
    private const string kDownloadFunction = "DownloadFile";
    private const string kScUrl = "https://securechange.example";
    private const string kExistingName = "before upgrade";
    private const string kNewName = "after upgrade";

    private sealed class SnapshotsTestSetup(InMemoryDeviceSpecificSettingsApiConnection api)
    {
        public InMemoryDeviceSpecificSettingsApiConnection Api { get; } = api;
        public IRenderedComponent<DeviceSpecificSettingsSnapshots> Dialog { get; set; } = null!;
        public List<(Exception? Exception, string Title, string Message, bool Error)> Messages { get; } = [];
        public List<(DeviceSpecificSettingsPackage Package, string Name)> Applied { get; } = [];
        public List<bool> DisplayChanges { get; } = [];
    }

    [Test]
    public async Task OpeningTheDialogListsTheStoredSnapshots()
    {
        InMemoryDeviceSpecificSettingsApiConnection api = await CreateApiWithSnapshot();
        using BunitContext context = new();

        SnapshotsTestSetup setup = Render(context, api, Roles.Admin);

        Assert.Multiple(() =>
        {
            Assert.That(setup.Dialog.Markup, Does.Contain(kExistingName));
            Assert.That(setup.Dialog.Markup, Does.Contain("9.9.4"));
            Assert.That(setup.Dialog.FindAll("#dss-snapshot-name"), Has.Count.EqualTo(1), "admins can store snapshots");
            Assert.That(setup.Dialog.FindAll("table button.btn-danger"), Has.Count.EqualTo(1), "admins can delete snapshots");
            Assert.That(setup.Messages, Is.Empty);
        });
    }

    [Test]
    public async Task NonAdminsCanOnlyApplyAndDownload()
    {
        InMemoryDeviceSpecificSettingsApiConnection api = await CreateApiWithSnapshot();
        using BunitContext context = new();

        SnapshotsTestSetup setup = Render(context, api, Roles.Auditor);

        Assert.Multiple(() =>
        {
            Assert.That(setup.Dialog.FindAll("#dss-snapshot-name"), Is.Empty);
            Assert.That(setup.Dialog.FindAll("table button.btn-danger"), Is.Empty);
            Assert.That(setup.Dialog.FindAll("table button.btn-primary"), Has.Count.EqualTo(2));
        });
    }

    [Test]
    public void WithoutSnapshotsANoteIsShown()
    {
        using BunitContext context = new();

        SnapshotsTestSetup setup = Render(context, CreateApi(), Roles.Admin);

        Assert.Multiple(() =>
        {
            Assert.That(setup.Dialog.Markup, Does.Contain("dss_no_snapshots"));
            Assert.That(setup.Dialog.FindAll("table"), Is.Empty);
        });
    }

    [Test]
    public void AFailedLoadShowsAnErrorAndAnEmptyList()
    {
        InMemoryDeviceSpecificSettingsApiConnection api = CreateApi();
        api.FailingQuery = DeviceSpecificSettingsQueries.getSnapshots;
        using BunitContext context = new();

        SnapshotsTestSetup setup = Render(context, api, Roles.Admin);

        Assert.Multiple(() =>
        {
            Assert.That(setup.Messages.Single().Error, Is.True);
            Assert.That(setup.Messages.Single().Exception, Is.InstanceOf<InvalidOperationException>());
            Assert.That(setup.Dialog.Markup, Does.Contain("dss_no_snapshots"));
        });
    }

    [Test]
    public void SavingStoresTheCurrentSettingsAsNewSnapshot()
    {
        InMemoryDeviceSpecificSettingsApiConnection api = CreateApi();
        using BunitContext context = new();
        SnapshotsTestSetup setup = Render(context, api, Roles.Admin);

        setup.Dialog.Find("#dss-snapshot-name").Change(kNewName);
        setup.Dialog.Find("#dss-snapshot-description").Change("described");
        setup.Dialog.Find("button.btn-success").Click();

        DeviceSpecificSettingsSnapshotData stored = api.Snapshots.Single();
        DeviceSpecificSettingsPackage package = DeviceSpecificSettingsPackageJson.FromStorage(stored.Content);
        Assert.Multiple(() =>
        {
            Assert.That(stored.Name, Is.EqualTo(kNewName));
            Assert.That(stored.Description, Is.EqualTo("described"));
            Assert.That(package.Nodes.Single().Values[DeviceSpecificSettingKeys.ScUrl.DatabaseKey].GetString(), Is.EqualTo(kScUrl));
            Assert.That(setup.Dialog.Find("#dss-snapshot-name").GetAttribute("value"), Is.Empty, "the input is cleared");
            Assert.That(setup.Dialog.Markup, Does.Contain(kNewName), "the list is reloaded");
            Assert.That(setup.Messages, Is.Empty);
        });
    }

    [Test]
    public async Task SavingADuplicateNameShowsAnError()
    {
        InMemoryDeviceSpecificSettingsApiConnection api = await CreateApiWithSnapshot();
        using BunitContext context = new();
        SnapshotsTestSetup setup = Render(context, api, Roles.Admin);

        setup.Dialog.Find("#dss-snapshot-name").Change(kExistingName);
        setup.Dialog.Find("button.btn-success").Click();

        Assert.Multiple(() =>
        {
            Assert.That(api.Snapshots, Has.Count.EqualTo(1));
            Assert.That(setup.Messages.Single().Message, Is.EqualTo("E5304"));
            Assert.That(setup.Dialog.Find("button.btn-success").HasAttribute("disabled"), Is.False, "the dialog is usable again");
        });
    }

    [Test]
    public async Task ApplyingHandsThePackageToTheImportAndClosesTheDialog()
    {
        InMemoryDeviceSpecificSettingsApiConnection api = await CreateApiWithSnapshot();
        using BunitContext context = new();
        SnapshotsTestSetup setup = Render(context, api, Roles.Admin);

        setup.Dialog.FindAll("table button.btn-primary")[0].Click();

        Assert.Multiple(() =>
        {
            Assert.That(setup.Applied.Single().Name, Is.EqualTo(kExistingName));
            Assert.That(setup.Applied.Single().Package.Nodes.Single().Level, Is.EqualTo("global"));
            Assert.That(setup.DisplayChanges, Is.EqualTo(new List<bool> { false }));
        });
    }

    [Test]
    public async Task ApplyingASnapshotDeletedMeanwhileShowsAnError()
    {
        InMemoryDeviceSpecificSettingsApiConnection api = await CreateApiWithSnapshot();
        using BunitContext context = new();
        SnapshotsTestSetup setup = Render(context, api, Roles.Admin);
        api.Snapshots.Clear();

        setup.Dialog.FindAll("table button.btn-primary")[0].Click();

        Assert.Multiple(() =>
        {
            Assert.That(setup.Applied, Is.Empty);
            Assert.That(setup.Messages.Single().Exception, Is.InstanceOf<KeyNotFoundException>());
            Assert.That(setup.DisplayChanges, Is.Empty, "the dialog stays open");
        });
    }

    [Test]
    public async Task DownloadingSendsThePackageAsJsonFile()
    {
        InMemoryDeviceSpecificSettingsApiConnection api = await CreateApiWithSnapshot();
        using BunitContext context = new();
        SnapshotsTestSetup setup = Render(context, api, Roles.Admin);
        context.JSInterop.SetupVoid(kDownloadFunction, _ => true);

        setup.Dialog.FindAll("table button.btn-primary")[1].Click();

        JSRuntimeInvocation invocation = context.JSInterop.Invocations[kDownloadFunction].Single();
        string content = System.Text.Encoding.UTF8.GetString((byte[])invocation.Arguments[2]!);
        Assert.Multiple(() =>
        {
            Assert.That(invocation.Arguments[0], Is.EqualTo(kExistingName + DeviceSpecificSettingsPackageJson.kFileSuffix));
            Assert.That(invocation.Arguments[1], Is.EqualTo("application/json"));
            Assert.That(DeviceSpecificSettingsPackageJson.Deserialize(content).Nodes.Single().Level, Is.EqualTo("global"));
            Assert.That(setup.Messages, Is.Empty);
        });
    }

    [Test]
    public async Task AFailedDownloadShowsAnError()
    {
        InMemoryDeviceSpecificSettingsApiConnection api = await CreateApiWithSnapshot();
        using BunitContext context = new();
        SnapshotsTestSetup setup = Render(context, api, Roles.Admin);
        context.JSInterop.SetupVoid(kDownloadFunction, _ => true).SetException(new InvalidOperationException("blocked"));

        setup.Dialog.FindAll("table button.btn-primary")[1].Click();

        Assert.That(setup.Messages.Single().Exception?.Message, Is.EqualTo("blocked"));
    }

    [Test]
    public async Task DeletingAsksForConfirmationAndRemovesTheSnapshot()
    {
        InMemoryDeviceSpecificSettingsApiConnection api = await CreateApiWithSnapshot();
        using BunitContext context = new();
        SnapshotsTestSetup setup = Render(context, api, Roles.Admin);

        setup.Dialog.Find("table button.btn-danger").Click();
        string confirmMessage = setup.Dialog.Markup;
        setup.Dialog.FindAll("button.btn-danger").Last().Click();

        Assert.Multiple(() =>
        {
            Assert.That(confirmMessage, Does.Contain("U5123" + kExistingName + "?"));
            Assert.That(api.Snapshots, Is.Empty);
            Assert.That(setup.Dialog.Markup, Does.Contain("dss_no_snapshots"));
        });
    }

    [Test]
    public async Task AFailedDeleteShowsAnErrorAndKeepsTheSnapshot()
    {
        InMemoryDeviceSpecificSettingsApiConnection api = await CreateApiWithSnapshot();
        api.FailingQuery = DeviceSpecificSettingsQueries.deleteSnapshot;
        using BunitContext context = new();
        SnapshotsTestSetup setup = Render(context, api, Roles.Admin);

        setup.Dialog.Find("table button.btn-danger").Click();
        setup.Dialog.FindAll("button.btn-danger").Last().Click();

        Assert.Multiple(() =>
        {
            Assert.That(api.Snapshots, Has.Count.EqualTo(1));
            Assert.That(setup.Messages.Single().Error, Is.True);
        });
    }

    [Test]
    public void ClosingNotifiesTheCaller()
    {
        using BunitContext context = new();
        SnapshotsTestSetup setup = Render(context, CreateApi(), Roles.Admin);

        setup.Dialog.Find("button.btn-secondary").Click();

        Assert.That(setup.DisplayChanges, Is.EqualTo(new List<bool> { false }));
    }

    [TestCase("daily", "daily.fwo-dss.json")]
    [TestCase("a/b\\c:d*e?f\"g<h>i|j", "a_b_c_d_e_f_g_h_i_j.fwo-dss.json")]
    [TestCase("tab\there", "tab_here.fwo-dss.json")]
    public void TheFileNameReplacesCharactersNotAllowedInFileNames(string snapshotName, string expected)
    {
        string fileName = DeviceSpecificSettingsSnapshots.SnapshotFileName(snapshotName);

        Assert.That(fileName, Is.EqualTo(expected));
    }

    private static InMemoryDeviceSpecificSettingsApiConnection CreateApi()
    {
        InMemoryDeviceSpecificSettingsApiConnection api = new();
        api.AddNode(1, DeviceSpecificScopeType.Global, "global", displayName: "Global");
        api.SetValue(1, DeviceSpecificSettingKeys.ScUrl, kScUrl);
        return api;
    }

    private static async Task<InMemoryDeviceSpecificSettingsApiConnection> CreateApiWithSnapshot()
    {
        InMemoryDeviceSpecificSettingsApiConnection api = CreateApi();
        DeviceSpecificSettingsPackage package = new() { FwoVersion = "9.9.4", Nodes = [new DeviceSpecificSettingsPackageNode { Level = "global" }] };
        await new DeviceSpecificSettingsSnapshotService(api).AddSnapshotAsync(kExistingName, "taken before the upgrade", package);
        return api;
    }

    private static SnapshotsTestSetup Render(BunitContext context, InMemoryDeviceSpecificSettingsApiConnection api, string role)
    {
        SnapshotsTestSetup setup = new(api);
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
            .AddChildContent<DeviceSpecificSettingsSnapshots>(child => child
                .AddCascadingValue(displayMessage)
                .Add(p => p.Display, true)
                .Add(p => p.DisplayChanged, displayed => setup.DisplayChanges.Add(displayed))
                .Add(p => p.ApplySnapshot, snapshot => setup.Applied.Add(snapshot))));
        setup.Dialog = wrapper.FindComponent<DeviceSpecificSettingsSnapshots>();
        return setup;
    }
}
