using System.Text.Json;
using FWO.Api.Client.Queries;
using FWO.Config.Api;
using FWO.Config.Api.Data;
using FWO.Data;
using FWO.Data.DeviceSpecificSettings;
using FWO.Services.DeviceSpecificSettings;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace FWO.Test;

/// <summary>
/// Covers the export and import of device specific settings: natural keys and credential names in the package, the
/// round trip between installations with different database IDs, the complete validation before anything is written,
/// merge and replace, dry runs, and placing new levels along the current device hierarchy.
/// </summary>
[TestFixture]
internal class DeviceSpecificSettingsTransferServiceTest
{
    private const string kFwoVersion = "9.9.4";
    private const string kCheckPointName = "Check Point";
    private const string kCheckPointVersion = "R8x";
    private const string kFortiName = "FortiManager";
    private const string kFortiVersion = "5ff";
    private const string kManagementName = "mgmt-a";
    private const string kGatewayName = "gw-a";
    private const string kCredentialName = "sc-user";
    private const string kKeyPairName = "ssh-key";
    private const string kScUrl = "https://securechange.example";
    private const string kGlobal = "global";
    private const string kDeviceType = "device_type";
    private const string kManagement = "management";
    private const string kGateway = "gateway";

    [Test]
    public async Task ExportAddressesLevelsByNaturalKeysAndCredentialsByName()
    {
        InMemoryDeviceSpecificSettingsApiConnection api = CreateSourceInstallation();

        DeviceSpecificSettingsExportResult result = await CreateService(api).ExportAsync();

        DeviceSpecificSettingsPackage package = result.Package;
        Assert.Multiple(() =>
        {
            Assert.That(result.Warnings, Is.Empty);
            Assert.That(package.Format, Is.EqualTo(DeviceSpecificSettingsPackage.kFormat));
            Assert.That(package.Version, Is.EqualTo(DeviceSpecificSettingsPackage.kCurrentVersion));
            Assert.That(package.FwoVersion, Is.EqualTo(kFwoVersion));
            Assert.That(package.ExportedAt, Is.Not.Null);
            Assert.That(package.Nodes.Select(node => node.Level), Is.EqualTo(new List<string> { kGlobal, kDeviceType, kManagement, kGateway }));
            Assert.That(package.Nodes[1].DeviceTypeName, Is.EqualTo(kCheckPointName));
            Assert.That(package.Nodes[1].DeviceTypeVersion, Is.EqualTo(kCheckPointVersion));
            Assert.That(package.Nodes[2].ManagementName, Is.EqualTo(kManagementName));
            Assert.That(package.Nodes[3].ManagementName, Is.EqualTo(kManagementName));
            Assert.That(package.Nodes[3].GatewayName, Is.EqualTo(kGatewayName));
            Assert.That(package.Nodes[2].Values[DeviceSpecificSettingKeys.ScCredential.DatabaseKey].GetString(), Is.EqualTo(kCredentialName));
            Assert.That(package.Nodes[0].Values[DeviceSpecificSettingKeys.ScUrl.DatabaseKey].GetString(), Is.EqualTo(kScUrl));
            Assert.That(package.Nodes[0].DeviceTypeName, Is.Null);
        });
    }

    [Test]
    public async Task ExportLeavesOutLevelsOfDeletedObjectsAndReferencesToDeletedCredentials()
    {
        InMemoryDeviceSpecificSettingsApiConnection api = CreateSourceInstallation();
        api.Managements.Clear();
        api.Credentials.Clear();

        DeviceSpecificSettingsExportResult result = await CreateService(api).ExportAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result.Package.Nodes.Select(node => node.Level), Is.EqualTo(new List<string> { kGlobal, kDeviceType }));
            Assert.That(result.Warnings, Has.Count.EqualTo(2), "the management and the gateway level are reported");
        });
    }

    [Test]
    public async Task ExportLeavesOutALevelWhoseOnlyValueReferencesADeletedCredential()
    {
        InMemoryDeviceSpecificSettingsApiConnection api = CreateSourceInstallation();
        api.Credentials.Clear();

        DeviceSpecificSettingsExportResult result = await CreateService(api).ExportAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result.Package.Nodes.Any(node => node.Level == kManagement), Is.False);
            Assert.That(result.Warnings.Single(), Does.Contain(DeviceSpecificSettingKeys.ScCredential.DatabaseKey));
        });
    }

    [Test]
    public async Task ExportAndImportRoundTripBetweenInstallationsWithDifferentIds()
    {
        DeviceSpecificSettingsPackage exported = (await CreateService(CreateSourceInstallation()).ExportAsync()).Package;
        InMemoryDeviceSpecificSettingsApiConnection target = CreateTargetInstallation();
        DeviceSpecificSettingsPackage transferred = DeviceSpecificSettingsPackageJson.Deserialize(DeviceSpecificSettingsPackageJson.Serialize(exported));

        DeviceSpecificSettingsImportPlan plan = await CreateService(target).ImportAsync(transferred, DeviceSpecificSettingsImportMode.Replace, dryRun: false);
        DeviceSpecificSettingsPackage reexported = (await CreateService(target).ExportAsync()).Package;

        DeviceSpecificConfigNodeData deviceTypeNode = target.Nodes.Single(node => node.NodeType == kDeviceType);
        DeviceSpecificConfigNodeData managementNode = target.Nodes.Single(node => node.NodeType == kManagement);
        DeviceSpecificConfigNodeData gatewayNode = target.Nodes.Single(node => node.NodeType == kGateway);
        Assert.Multiple(() =>
        {
            Assert.That(plan.HasErrors, Is.False, string.Join("; ", plan.Errors.Select(error => error.Message)));
            Assert.That(Normalize(reexported), Is.EqualTo(Normalize(exported)));
            Assert.That(deviceTypeNode.ObjectKey, Is.EqualTo("19"));
            Assert.That(managementNode.ObjectKey, Is.EqualTo("7"));
            Assert.That(managementNode.ParentId, Is.EqualTo(deviceTypeNode.Id));
            Assert.That(gatewayNode.ObjectKey, Is.EqualTo("71"));
            Assert.That(gatewayNode.ParentId, Is.EqualTo(managementNode.Id));
            Assert.That(target.ReadValue(managementNode.Id, DeviceSpecificSettingKeys.ScCredential)?.Value<int>(), Is.EqualTo(50),
                "the credential is mapped by name to the ID of the target installation");
            Assert.That(target.BulkPatchCallCount, Is.EqualTo(1), "all values are written with one request");
            Assert.That(target.PatchCallCount + target.DeleteCallCount, Is.Zero);
        });
    }

    [Test]
    public async Task ReplaceRemovesOverridesMissingInThePackageAndMergeKeepsThem()
    {
        DeviceSpecificSettingsPackage package = Package(Node(kGlobal, values: Values((DeviceSpecificSettingKeys.RequestMaxAttempts.DatabaseKey, "7"))));

        DeviceSpecificSettingsImportPlan merge = await CreateService(CreateTargetInstallation()).ImportAsync(package, DeviceSpecificSettingsImportMode.Merge, dryRun: true);
        InMemoryDeviceSpecificSettingsApiConnection replaced = CreateTargetInstallation();
        DeviceSpecificSettingsImportPlan replace = await CreateService(replaced).ImportAsync(package, DeviceSpecificSettingsImportMode.Replace, dryRun: false);

        Assert.Multiple(() =>
        {
            Assert.That(Changes(merge), Is.EqualTo(new List<string> { "requestMaxAttempts:Changed:2:7" }));
            Assert.That(Changes(replace), Is.EqualTo(new List<string> { "requestMaxAttempts:Changed:2:7", "scLookupRequesterId:Removed:true:" }));
            Assert.That(replaced.ReadValue(1, DeviceSpecificSettingKeys.ScLookupRequesterId), Is.Null);
            Assert.That(replaced.ReadValue(1, DeviceSpecificSettingKeys.RequestMaxAttempts)?.Value<int>(), Is.EqualTo(7));
        });
    }

    [Test]
    public async Task DryRunOnlyPreviewsTheChanges()
    {
        InMemoryDeviceSpecificSettingsApiConnection target = CreateTargetInstallation();
        DeviceSpecificSettingsPackage package = Package(Node(kManagement, managementName: kManagementName,
            values: Values((DeviceSpecificSettingKeys.ScCredential.DatabaseKey, $"\"{kCredentialName}\""))));

        DeviceSpecificSettingsImportPlan plan = await CreateService(target).ImportAsync(package, DeviceSpecificSettingsImportMode.Merge, dryRun: true);

        Assert.Multiple(() =>
        {
            Assert.That(Changes(plan), Is.EqualTo(new List<string> { $"scCredential:Added::\"{kCredentialName}\"" }), "the credential is shown by name");
            Assert.That(plan.Nodes.Single().Name, Is.EqualTo(kManagementName));
            Assert.That(target.Nodes, Has.Count.EqualTo(1), "no level is created");
            Assert.That(target.BulkPatchCallCount, Is.Zero);
        });
    }

    [Test]
    public async Task APackageMatchingTheStoredSettingsWritesNothing()
    {
        InMemoryDeviceSpecificSettingsApiConnection target = CreateTargetInstallation();
        DeviceSpecificSettingsPackage package = Package(Node(kGlobal, values: Values(
            (DeviceSpecificSettingKeys.RequestMaxAttempts.DatabaseKey, "2"),
            (DeviceSpecificSettingKeys.ScLookupRequesterId.DatabaseKey, "true"))));

        DeviceSpecificSettingsImportPlan plan = await CreateService(target).ImportAsync(package, DeviceSpecificSettingsImportMode.Replace, dryRun: false);

        Assert.Multiple(() =>
        {
            Assert.That(plan.HasChanges, Is.False);
            Assert.That(plan.Nodes.Single().UnchangedCount, Is.EqualTo(2));
            Assert.That(target.BulkPatchCallCount, Is.Zero);
        });
    }

    [Test]
    public async Task AnInvalidPackageReportsEveryErrorAndWritesNothing()
    {
        InMemoryDeviceSpecificSettingsApiConnection target = CreateTargetInstallation();
        DeviceSpecificSettingsPackage package = Package(
            Node(kGlobal, values: Values(
                ("unknownKey", "1"),
                (DeviceSpecificSettingKeys.Templates.DatabaseKey, "\"x\""),
                (DeviceSpecificSettingKeys.RequestMaxAttempts.DatabaseKey, "0"),
                (DeviceSpecificSettingKeys.ScUrl.DatabaseKey, "5"),
                (DeviceSpecificSettingKeys.InstallOn.DatabaseKey, "\"Undefined\""))),
            Node(kManagement, managementName: "missing", values: Values()),
            Node(kGateway, managementName: kManagementName, gatewayName: "missing", values: Values()),
            Node(kDeviceType, deviceTypeName: "Unknown", deviceTypeVersion: "1", values: Values()),
            Node(kManagement, managementName: kManagementName, values: Values((DeviceSpecificSettingKeys.ScCredential.DatabaseKey, "\"nobody\""))),
            Node(kGateway, managementName: kManagementName, gatewayName: kGatewayName,
                values: Values((DeviceSpecificSettingKeys.ScCredential.DatabaseKey, $"\"{kCredentialName}\""))),
            Node(kGlobal, values: Values()),
            Node(kManagement, managementName: kManagementName, values: Values()));
        package.Nodes.Add(Node(kDeviceType, deviceTypeName: kCheckPointName, deviceTypeVersion: kCheckPointVersion,
            values: Values((DeviceSpecificSettingKeys.ScCredential.DatabaseKey, $"\"{kKeyPairName}\""))));

        DeviceSpecificSettingsImportPlan plan = await CreateService(target).ImportAsync(package, DeviceSpecificSettingsImportMode.Replace, dryRun: false);

        Assert.Multiple(() =>
        {
            Assert.That(plan.Errors.Select(error => error.Path), Is.EquivalentTo(
            [
                "nodes[0].values.unknownKey",
                "nodes[0].values.templates",
                "nodes[0].values.requestMaxAttempts",
                "nodes[0].values.scUrl",
                "nodes[0].values.installOn",
                "nodes[1]",
                "nodes[2]",
                "nodes[3]",
                "nodes[4].values.scCredential",
                "nodes[5].values.scCredential",
                "nodes[6]",
                "nodes[7]",
                "nodes[8].values.scCredential"
            ]));
            Assert.That(plan.Errors.Single(error => error.Path == "nodes[1]").Message, Does.Contain("missing"));
            Assert.That(plan.Errors.Single(error => error.Path == "nodes[8].values.scCredential").Message, Does.Contain("key pair"));
            Assert.That(plan.Nodes, Is.Empty);
            Assert.That(target.UpsertCallCount + target.BulkPatchCallCount, Is.Zero, "nothing is written");
        });
    }

    [Test]
    public async Task NameFieldsMustMatchTheLevel()
    {
        DeviceSpecificSettingsPackage package = Package(
            Node(kManagement, values: Values()),
            Node(kDeviceType, deviceTypeName: kCheckPointName, deviceTypeVersion: kCheckPointVersion, gatewayName: kGatewayName, values: Values()),
            Node("tenant", values: Values()),
            Node(kGlobal, managementName: kManagementName, values: Values()));
        package.Nodes.Add(null!);
        package.Nodes.Add(new DeviceSpecificSettingsPackageNode { Level = kGlobal, Values = null! });

        DeviceSpecificSettingsImportPlan plan = await CreateService(CreateTargetInstallation()).ImportAsync(package, DeviceSpecificSettingsImportMode.Merge, dryRun: true);

        Assert.That(plan.Errors.Select(error => error.Path), Is.EquivalentTo(
        [
            "nodes[0].management_name",
            "nodes[1].gateway_name",
            "nodes[2].level",
            "nodes[3].management_name",
            "nodes[4]",
            "nodes[5].values"
        ]));
    }

    [Test]
    public async Task AmbiguousManagementAndGatewayNamesAreRejected()
    {
        InMemoryDeviceSpecificSettingsApiConnection target = CreateTargetInstallation();
        target.Managements.Add(new Management { Id = 8, Name = kManagementName, DeviceType = target.DeviceTypes[0] });
        target.Managements.Add(new Management
        {
            Id = 9,
            Name = "mgmt-b",
            DeviceType = target.DeviceTypes[0],
            Devices = [new Device { Id = 91, Name = kGatewayName }, new Device { Id = 92, Name = kGatewayName }]
        });
        DeviceSpecificSettingsPackage package = Package(
            Node(kManagement, managementName: kManagementName, values: Values()),
            Node(kGateway, managementName: "mgmt-b", gatewayName: kGatewayName, values: Values()));

        DeviceSpecificSettingsImportPlan plan = await CreateService(target).ImportAsync(package, DeviceSpecificSettingsImportMode.Merge, dryRun: true);

        Assert.That(plan.Errors.Select(error => error.Message), Has.All.Contain("ambiguous"));
        Assert.That(plan.Errors, Has.Count.EqualTo(2));
    }

    [Test]
    public void StructureErrorsAreReportedTogether()
    {
        DeviceSpecificSettingsPackage wrongFormat = new() { Format = "other", Version = 2, Nodes = null! };
        DeviceSpecificSettingsPackage tooLarge = new()
        {
            Nodes = [.. Enumerable.Range(0, DeviceSpecificSettingsPackage.kMaxNodes + 1).Select(_ => new DeviceSpecificSettingsPackageNode())]
        };

        Assert.Multiple(() =>
        {
            Assert.That(DeviceSpecificSettingsPackageValidator.ValidateStructure(wrongFormat).Select(error => error.Path),
                Is.EqualTo(new List<string> { "format", "version", "nodes" }));
            Assert.That(DeviceSpecificSettingsPackageValidator.ValidateStructure(tooLarge).Single().Path, Is.EqualTo("nodes"));
            Assert.That(DeviceSpecificSettingsPackageValidator.ValidateStructure(null).Single().Path, Is.Empty);
        });
    }

    [Test]
    public async Task AMisplacedManagementLevelIsMovedBelowItsCurrentDeviceType()
    {
        InMemoryDeviceSpecificSettingsApiConnection target = CreateTargetInstallation();
        target.AddNode(2, DeviceSpecificScopeType.DeviceType, "20", 1, $"{kFortiName} {kFortiVersion}");
        target.AddNode(3, DeviceSpecificScopeType.Management, "7", 2, kManagementName);
        DeviceSpecificSettingsPackage package = Package(Node(kManagement, managementName: kManagementName,
            values: Values((DeviceSpecificSettingKeys.ImporterHostName.DatabaseKey, "\"importer-2\""))));

        DeviceSpecificSettingsImportPlan plan = await CreateService(target).ImportAsync(package, DeviceSpecificSettingsImportMode.Merge, dryRun: false);

        DeviceSpecificConfigNodeData checkPointNode = target.Nodes.Single(node => node.NodeType == kDeviceType && node.ObjectKey == "19");
        Assert.Multiple(() =>
        {
            Assert.That(plan.HasErrors, Is.False);
            Assert.That(target.Nodes.Single(node => node.Id == 3).ParentId, Is.EqualTo(checkPointNode.Id));
            Assert.That(target.ReadValue(3, DeviceSpecificSettingKeys.ImporterHostName)?.Value<string>(), Is.EqualTo("importer-2"));
        });
    }

    [Test]
    public async Task RemovalsOnlyDoNotCreateLevels()
    {
        InMemoryDeviceSpecificSettingsApiConnection target = CreateTargetInstallation();
        DeviceSpecificSettingsPackage package = Package(
            Node(kGlobal, values: Values()),
            Node(kManagement, managementName: kManagementName, values: Values()));

        DeviceSpecificSettingsImportPlan plan = await CreateService(target).ImportAsync(package, DeviceSpecificSettingsImportMode.Replace, dryRun: false);

        Assert.Multiple(() =>
        {
            Assert.That(plan.Nodes[0].Changes.Select(change => change.Kind), Has.All.EqualTo(DeviceSpecificSettingChangeKind.Removed));
            Assert.That(plan.Nodes[1].Changes, Is.Empty);
            Assert.That(target.Nodes, Has.Count.EqualTo(1));
            Assert.That(target.Nodes[0].Values, Is.Empty);
            Assert.That(target.UpsertCallCount, Is.Zero);
        });
    }

    [Test]
    public async Task ValuesAreCheckedByTheRulesOfTheSettingsPage()
    {
        string endpointKey = FirewallWriteSettingKeys.Endpoints[FirewallWriteOperation.HostCreate].DatabaseKey;
        DeviceSpecificSettingsPackage valid = Package(Node(kDeviceType, deviceTypeName: kCheckPointName, deviceTypeVersion: kCheckPointVersion,
            values: Values((endpointKey, "\"post /web_api/add-host\""))));
        DeviceSpecificSettingsPackage invalid = Package(Node(kGlobal, values: Values(
            (DeviceSpecificSettingKeys.CheckPointLayerGuardSectionNameTemplate.DatabaseKey, "\"@@BROKEN\""))));

        DeviceSpecificSettingsImportPlan validPlan = await CreateService(CreateTargetInstallation()).ImportAsync(valid, DeviceSpecificSettingsImportMode.Merge, dryRun: true);
        DeviceSpecificSettingsImportPlan invalidPlan = await CreateService(CreateTargetInstallation()).ImportAsync(invalid, DeviceSpecificSettingsImportMode.Merge, dryRun: true);

        Assert.Multiple(() =>
        {
            Assert.That(validPlan.Nodes.Single().Changes.Single().NewValue, Is.EqualTo("\"POST /web_api/add-host\""), "the endpoint is stored in its canonical form");
            Assert.That(invalidPlan.Errors.Single().Path, Is.EqualTo("nodes[0].values.checkPointLayerGuardSectionNameTemplate"));
        });
    }

    [Test]
    public void ExportFileNameContainsTheExportTime()
    {
        string fileName = DeviceSpecificSettingsExporter.FileName(new DateTimeOffset(2026, 11, 21, 15, 30, 0, TimeSpan.Zero));

        Assert.That(fileName, Is.EqualTo("device-specific-settings-20261121-1530.fwo-dss.json"));
    }

    private static DeviceSpecificSettingsTransferService CreateService(InMemoryDeviceSpecificSettingsApiConnection api) =>
        new(api, new DeviceSpecificSettingsManager(api), kFwoVersion);

    /// <summary>Installation with settings on all four levels.</summary>
    private static InMemoryDeviceSpecificSettingsApiConnection CreateSourceInstallation()
    {
        InMemoryDeviceSpecificSettingsApiConnection api = new();
        DeviceType checkPoint = new() { Id = 9, Name = kCheckPointName, Version = kCheckPointVersion };
        api.DeviceTypes.Add(checkPoint);
        api.DeviceTypes.Add(new DeviceType { Id = 13, Name = kFortiName, Version = kFortiVersion });
        api.Managements.Add(new Management { Id = 1, Name = kManagementName, DeviceType = checkPoint, Devices = [new Device { Id = 11, Name = kGatewayName }] });
        api.Credentials.Add(new Credential { Id = 5, Name = kCredentialName });
        api.Credentials.Add(new Credential { Id = 6, Name = kKeyPairName, IsKeyPair = true });

        api.AddNode(1, DeviceSpecificScopeType.Global, kGlobal, displayName: "Global");
        api.SetValue(1, DeviceSpecificSettingKeys.ScUrl, kScUrl);
        api.SetValue(1, DeviceSpecificSettingKeys.RequestMaxAttempts, 4);
        api.AddNode(2, DeviceSpecificScopeType.DeviceType, "9", 1, $"{kCheckPointName} {kCheckPointVersion}");
        api.SetValue(2, DeviceSpecificSettingKeys.InstallOn, ProvisioningInstallOn.GatewaysInPath);
        api.AddNode(3, DeviceSpecificScopeType.Management, "1", 2, kManagementName);
        api.SetValue(3, DeviceSpecificSettingKeys.ScCredential, (int?)5);
        api.AddNode(4, DeviceSpecificScopeType.Gateway, "11", 3, kGatewayName);
        api.SetValue(4, DeviceSpecificSettingKeys.AutoPlanImplementation, true);
        return api;
    }

    /// <summary>Installation with the same names but other IDs, and only global settings.</summary>
    private static InMemoryDeviceSpecificSettingsApiConnection CreateTargetInstallation()
    {
        InMemoryDeviceSpecificSettingsApiConnection api = new();
        DeviceType checkPoint = new() { Id = 19, Name = kCheckPointName, Version = kCheckPointVersion };
        api.DeviceTypes.Add(checkPoint);
        api.DeviceTypes.Add(new DeviceType { Id = 20, Name = kFortiName, Version = kFortiVersion });
        api.Managements.Add(new Management { Id = 7, Name = kManagementName, DeviceType = checkPoint, Devices = [new Device { Id = 71, Name = kGatewayName }] });
        api.Credentials.Add(new Credential { Id = 50, Name = kCredentialName });
        api.Credentials.Add(new Credential { Id = 60, Name = kKeyPairName, IsKeyPair = true });

        api.AddNode(1, DeviceSpecificScopeType.Global, kGlobal, displayName: "Global");
        api.SetValue(1, DeviceSpecificSettingKeys.RequestMaxAttempts, 2);
        api.SetValue(1, DeviceSpecificSettingKeys.ScLookupRequesterId, true);
        return api;
    }

    private static DeviceSpecificSettingsPackage Package(params DeviceSpecificSettingsPackageNode[] nodes) => new() { Nodes = [.. nodes] };

    private static DeviceSpecificSettingsPackageNode Node(string level, string? deviceTypeName = null, string? deviceTypeVersion = null,
        string? managementName = null, string? gatewayName = null, Dictionary<string, JsonElement>? values = null) => new()
        {
            Level = level,
            DeviceTypeName = deviceTypeName,
            DeviceTypeVersion = deviceTypeVersion,
            ManagementName = managementName,
            GatewayName = gatewayName,
            Values = values ?? []
        };

    private static Dictionary<string, JsonElement> Values(params (string Key, string Json)[] values) =>
        values.ToDictionary(value => value.Key, value => JsonDocument.Parse(value.Json).RootElement.Clone());

    private static List<string> Changes(DeviceSpecificSettingsImportPlan plan) =>
        [.. plan.Nodes.SelectMany(node => node.Changes).Select(change => $"{change.Key}:{change.Kind}:{change.OldValue}:{change.NewValue}")];

    /// <summary>The package as JSON without the export time, for comparing packages.</summary>
    private static string Normalize(DeviceSpecificSettingsPackage package)
    {
        JObject json = JObject.Parse(DeviceSpecificSettingsPackageJson.Serialize(package));
        json.Remove("exported_at");
        return json.ToString(Newtonsoft.Json.Formatting.None);
    }
}
