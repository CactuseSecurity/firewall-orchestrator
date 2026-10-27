using FWO.Config.Api;
using FWO.Config.Api.DeviceSpecificSettings;
using FWO.Data;
using FWO.Data.DeviceSpecificSettings;
using FWO.Ui.Pages.Settings;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace FWO.Test;

/// <summary>Guards the Guardicore provisioning contract, defaults and inheritance.</summary>
[TestFixture]
[Parallelizable]
internal class DeviceSpecificSettingsGuardicoreTest
{
    private static readonly DeviceType kDeviceType = new() { Id = 10, Name = "Guardicore" };
    private static readonly List<string> kExpectedKeys =
    [
        "installOn", "positioningAlgorithm", "ruleLoggingOverride",
        "serviceObjectCreation", "addressObjectCreation", "timeObjectCreation", "labelCreation",
        "ruleType", "commonServiceSeparation", "ruleZoneFromOverride", "ruleZoneToOverride",
        "implementationMode", "implementationModeOverrideForRules", "rerouteToExternalTicketSystem",
        "securityProfiles", "ownerIdCustomFieldName", "changeIdCustomFieldName", "importerHostName",
        "autoPlanImplementation", "autoPlanImplementationForRules", "modellingCreateAppZones",
        "modellingImplementNetworkAreaObjects", "modellingDynIpLabelsVisibleAsGroups",
        "modellingConnectionIdField", "modellingConnectionIdTemplate", "templates",
        .. FirewallWriteKeys()
    ];
    private static readonly HashSet<string> kManagerConfigKeys =
    [
        "ownerIdCustomFieldName", "changeIdCustomFieldName", "importerHostName",
        "modellingCreateAppZones", "modellingImplementNetworkAreaObjects", "modellingDynIpLabelsVisibleAsGroups",
        "modellingConnectionIdField", "modellingConnectionIdTemplate"
    ];
    private static readonly HashSet<string> kManagerUiKeys =
    [
        "serviceObjectCreation", "addressObjectCreation", "timeObjectCreation", "labelCreation",
        "ruleType", "commonServiceSeparation", "ruleZoneFromOverride", "ruleZoneToOverride", "securityProfiles"
    ];
    private static readonly List<DeviceSpecificScopeType> kAllLevels =
        [DeviceSpecificScopeType.Global, DeviceSpecificScopeType.DeviceType, DeviceSpecificScopeType.Management, DeviceSpecificScopeType.Gateway];
    private static readonly List<DeviceSpecificScopeType> kManagerLevels =
        [DeviceSpecificScopeType.Global, DeviceSpecificScopeType.DeviceType, DeviceSpecificScopeType.Management];
    private static readonly List<DeviceSpecificScopeType> kDeviceLevels =
        [DeviceSpecificScopeType.DeviceType, DeviceSpecificScopeType.Management, DeviceSpecificScopeType.Gateway];
    private static readonly List<DeviceSpecificScopeType> kDeviceUiLevels =
        [DeviceSpecificScopeType.DeviceType, DeviceSpecificScopeType.Management];
    private static readonly List<DeviceSpecificScopeType> kDeviceTypeLevel = [DeviceSpecificScopeType.DeviceType];
    private const string kTicketSystems = "[{\"Id\":3,\"Name\":\"Ticketing\"}]";
    private static readonly List<JValue> kInvalidTicketSystemIds = [new("3"), new(3.5), new(long.MaxValue)];
    private static readonly List<(string Value, string Name)> kConfiguredTicketSystemOptions = [("3", "Ticketing (3)")];
    private static readonly List<(string Value, string Name)> kOptionsWithRemovedTicketSystem = [("3", "Ticketing (3)"), ("9", "9")];

    /// <summary>Database keys of the firewall write settings: connection settings, then endpoint and template per operation.</summary>
    private static List<string> FirewallWriteKeys()
    {
        List<string> keys = ["fwWriteCheckCertificates", "fwWriteInstallPolicy"];
        foreach (FirewallWriteOperation operation in Enum.GetValues<FirewallWriteOperation>())
        {
            keys.Add($"fwWriteEndpoint{operation}");
            keys.Add($"fwWriteTemplate{operation}");
        }
        return keys;
    }

    private static bool IsFirewallWriteRequestKey(DeviceSpecificSettingKey key) =>
        key.DatabaseKey.StartsWith("fwWriteEndpoint", StringComparison.Ordinal) || key.DatabaseKey.StartsWith("fwWriteTemplate", StringComparison.Ordinal);

    /// <summary>Levels at which the API accepts an override, per the issue's config column.</summary>
    private static List<DeviceSpecificScopeType> ConfigLevels(DeviceSpecificSettingKey key) => key.DatabaseKey switch
    {
        "templates" => kDeviceTypeLevel,
        "securityProfiles" => kDeviceLevels,
        "fwWriteCheckCertificates" => kManagerLevels,
        _ when IsFirewallWriteRequestKey(key) => kDeviceLevels,
        _ => kManagerConfigKeys.Contains(key.DatabaseKey) ? kManagerLevels : kAllLevels
    };

    /// <summary>Levels at which the settings page offers the field, per the issue's UI max level column.</summary>
    private static List<DeviceSpecificScopeType> UiLevels(DeviceSpecificSettingKey key) => key.DatabaseKey switch
    {
        "templates" => kDeviceTypeLevel,
        "securityProfiles" => kDeviceUiLevels,
        "fwWriteCheckCertificates" => kManagerLevels,
        _ when IsFirewallWriteRequestKey(key) => kDeviceLevels,
        _ => kManagerConfigKeys.Contains(key.DatabaseKey) || kManagerUiKeys.Contains(key.DatabaseKey) ? kManagerLevels : kAllLevels
    };

    /// <summary>Creates a real four-level locator hierarchy for persistence tests.</summary>
    private static InMemoryDeviceSpecificSettingsApiConnection CreateHierarchy()
    {
        InMemoryDeviceSpecificSettingsApiConnection api = new();
        api.AddNode(1, DeviceSpecificScopeType.Global, "global");
        api.AddNode(2, DeviceSpecificScopeType.DeviceType, "10", 1);
        api.AddNode(3, DeviceSpecificScopeType.Management, "100", 2);
        api.AddNode(4, DeviceSpecificScopeType.Gateway, "200", 3);
        return api;
    }

    /// <summary>Each key is tested at every level independently of the catalog scope definitions.</summary>
    private static IEnumerable<TestCaseData> ScopeCases() =>
        from key in DeviceSpecificSettingKeys.All
        from scope in Enum.GetValues<DeviceSpecificScopeType>()
        select new TestCaseData(key, scope);

    /// <summary>The persisted keys match the issue contract.</summary>
    [Test]
    public void CatalogMatchesContract()
    {
        Assert.That(DeviceSpecificSettingKeys.All.Select(key => key.DatabaseKey), Is.EquivalentTo(kExpectedKeys));
        Assert.That(DeviceSpecificSettingsData.Fields.Select(field => field.Key), Is.EquivalentTo(DeviceSpecificSettingKeys.All));
    }

    /// <summary>Writes reject forbidden scopes, and editors are offered no deeper than the UI max level.</summary>
    [TestCaseSource(nameof(ScopeCases))]
    public void ScopeRestrictionsMatchContract(DeviceSpecificSettingKey key, DeviceSpecificScopeType scope)
    {
        Assert.That(key.IsAllowedAt(scope), Is.EqualTo(ConfigLevels(key).Contains(scope)), $"{key}@{scope}");
        Assert.That(DeviceSpecificSettingsData.FindField(key)!.AppliesToLevel(scope), Is.EqualTo(UiLevels(key).Contains(scope)), $"UI {key}@{scope}");
    }

    /// <summary>New settings have the documented defaults.</summary>
    [Test]
    public void DefaultsMatchContract()
    {
        GlobalDeviceSpecificSettings settings = new();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(settings.ImplementationMode, Is.EqualTo(ProvisioningImplementationMode.Automatic));
            Assert.That(settings.TimeObjectCreation, Is.EqualTo(ProvisioningObjectCreationMode.Supermanager));
            Assert.That(settings.LabelCreation, Is.EqualTo(ProvisioningObjectCreationMode.Supermanager));
            Assert.That(settings.CommonServiceSeparation, Is.False);
            Assert.That(settings.ImplementationModeOverrideForRules, Is.EqualTo(ProvisioningRuleImplementationMode.Automatic));
            Assert.That(settings.RerouteToExternalTicketSystem, Is.Null);
            Assert.That(settings.OwnerIdCustomFieldName, Is.EqualTo("OwnerId"));
            Assert.That(settings.ChangeIdCustomFieldName, Is.EqualTo("ChangeId"));
            Assert.That(settings.ImporterHostName, Is.Empty);
            Assert.That(settings.AutoPlanImplementation, Is.True);
            Assert.That(settings.AutoPlanImplementationForRules, Is.False);
            Assert.That(settings.ModellingCreateAppZones, Is.True);
            Assert.That(settings.ModellingImplementNetworkAreaObjects, Is.False);
            Assert.That(settings.ModellingDynIpLabelsVisibleAsGroups, Is.True);
            Assert.That(settings.ModellingConnectionIdField, Is.EqualTo(ProvisioningConnectionIdField.Name));
            Assert.That(settings.ModellingConnectionIdTemplate, Is.EqualTo("FWOC{ID}"));
            Assert.That(settings.Templates, Is.EqualTo("{}"));
        }
    }

    /// <summary>Every allowed UI value travels through persistence and the typed mapper.</summary>
    private static IEnumerable<TestCaseData> FieldCases() =>
        DeviceSpecificSettingsData.Fields.Select(field => new TestCaseData(field));

    /// <summary>All fields resolve at gateway even when their overrides are limited to ancestors.</summary>
    [TestCaseSource(nameof(FieldCases))]
    public async Task ValuesInheritThroughAllLevels(DeviceSpecificFieldDefinition field)
    {
        InMemoryDeviceSpecificSettingsApiConnection api = CreateHierarchy();
        DeviceSpecificScopeType scope = field.MinLevel == DeviceSpecificScopeType.DeviceType
            ? DeviceSpecificScopeType.DeviceType : DeviceSpecificScopeType.Global;
        long nodeId = scope == DeviceSpecificScopeType.Global ? 1 : 2;
        string editorValue = field.Kind switch
        {
            DeviceSpecificFieldKind.SingleSelect => field.Options.Last().Value,
            DeviceSpecificFieldKind.ExtTicketSystem => "7",
            DeviceSpecificFieldKind.StringList => "profile1,profile2",
            DeviceSpecificFieldKind.FirewallEndpoint => "POST custom-endpoint",
            _ => "custom value"
        };
        DeviceSpecificSettingsChangeSet changes = new(new DeviceSpecificSettingsScope
        {
            ScopeType = scope,
            ObjectKey = scope == DeviceSpecificScopeType.Global ? "global" : "10",
            NodeId = nodeId,
            ParentNodeId = scope == DeviceSpecificScopeType.Global ? null : 1
        });
        field.SetOverride(changes, editorValue);
        DeviceSpecificSettingsManager manager = new(api);
        await manager.ApplyChangesAsync(changes);
        Management management = new() { Id = 100, DeviceType = kDeviceType };
        Device gateway = new() { Id = 200, Management = management };
        DeviceSpecificSettingsLevel<GatewayDeviceSpecificSettings> result =
            await manager.LoadLevelAsync<GatewayDeviceSpecificSettings>(DeviceSpecificScopePath.ForGateway(management, gateway));
        Assert.That(field.Read(result.Settings), Is.EqualTo(editorValue));
        Assert.That(result.ValueSources[field.Key].Scope?.NodeId, Is.EqualTo(nodeId));
        Assert.That(result.DirectOverrides, Does.Not.Contain(field.Key));
    }

    /// <summary>Reads of settings with restricted write scopes still inherit at gateway.</summary>
    [Test]
    public async Task EffectiveReadSupportsManagerOnlyKeysAtGateway()
    {
        InMemoryDeviceSpecificSettingsApiConnection api = CreateHierarchy();
        api.SetValue(1, DeviceSpecificSettingKeys.ModellingConnectionIdTemplate, "CONN-{ID}");
        Management management = new() { Id = 100, DeviceType = kDeviceType };
        Device gateway = new() { Id = 200, Management = management };
        DeviceSpecificSettingsManager manager = new(api);
        ResolvedDeviceSpecificValue<string> value = await manager.LoadEffectiveValueAsync(DeviceSpecificScopePath.ForGateway(management, gateway),
            DeviceSpecificSettingKeys.ModellingConnectionIdTemplate);
        Assert.That(value.Value, Is.EqualTo("CONN-{ID}"));
        Assert.That(value.Source.Scope?.NodeId, Is.EqualTo(1));
    }

    /// <summary>An undefined ticket system resolves to null from the compiled default.</summary>
    [Test]
    public async Task EffectiveReadOfUndefinedTicketSystemIsNull()
    {
        DeviceSpecificSettingsManager manager = new(CreateHierarchy());
        ResolvedDeviceSpecificValue<int?> value = await manager.LoadEffectiveValueAsync(DeviceSpecificScopePath.Global(),
            DeviceSpecificSettingKeys.RerouteToExternalTicketSystem);
        Assert.That(value.Value, Is.Null);
        Assert.That(value.Source.UsesCompiledDefault, Is.True);
    }

    /// <summary>Booleans are stored as JSON booleans only.</summary>
    [Test]
    public void BooleansUseJsonBooleans()
    {
        Assert.That(DeviceSpecificSettingValueSerializer.Serialize(DeviceSpecificSettingKeys.AutoPlanImplementation, false), Is.EqualTo(new JValue(false)));
        Assert.That(DeviceSpecificSettingValueSerializer.Deserialize(DeviceSpecificSettingKeys.AutoPlanImplementation, new JValue(true)), Is.True);
        Assert.Throws<InvalidOperationException>(() => DeviceSpecificSettingValueSerializer.Deserialize(
            DeviceSpecificSettingKeys.ModellingCreateAppZones, new JValue("Yes")));
    }

    /// <summary>Ticket system IDs are positive JSON integers; undefined is never stored.</summary>
    [TestCase(0)]
    [TestCase(-1)]
    public void TicketSystemRejectsNonPositiveIds(int id)
    {
        Assert.Throws<ArgumentException>(() => DeviceSpecificSettingValueSerializer.Serialize(
            DeviceSpecificSettingKeys.RerouteToExternalTicketSystem, (int?)id));
        Assert.Throws<InvalidOperationException>(() => DeviceSpecificSettingValueSerializer.Deserialize(
            DeviceSpecificSettingKeys.RerouteToExternalTicketSystem, new JValue(id)));
    }

    /// <summary>Strings, fractions and values outside the integer range are not ticket system IDs.</summary>
    [Test]
    public void TicketSystemRejectsNonIntegerValues()
    {
        Assert.Throws<ArgumentNullException>(() => DeviceSpecificSettingValueSerializer.Serialize(
            DeviceSpecificSettingKeys.RerouteToExternalTicketSystem, null));
        foreach (JValue stored in kInvalidTicketSystemIds)
        {
            Assert.Throws<InvalidOperationException>(() => DeviceSpecificSettingValueSerializer.Deserialize(
                DeviceSpecificSettingKeys.RerouteToExternalTicketSystem, stored));
        }
    }

    /// <summary>Selecting undefined in the editor removes the override instead of storing a value.</summary>
    [Test]
    public void TicketSystemEditorTreatsEmptyAsInherit()
    {
        DeviceSpecificSettingsChangeSet changes = new(new GlobalDeviceSpecificSettings().Scope);
        DeviceSpecificSettingsData.FindField(DeviceSpecificSettingKeys.RerouteToExternalTicketSystem)!.SetOverride(changes, "");
        Assert.That(changes.Upserts, Is.Empty);
        Assert.That(changes.Removals, Does.Contain(DeviceSpecificSettingKeys.RerouteToExternalTicketSystem));
    }

    /// <summary>Configured ticket systems are offered by name; a stored but removed ID stays selectable.</summary>
    [Test]
    public void TicketSystemOptionsListConfiguredSystems()
    {
        Assert.That(DeviceSpecificSettingsData.ExtTicketSystemOptions(kTicketSystems, ""),
            Is.EqualTo(kConfiguredTicketSystemOptions));
        Assert.That(DeviceSpecificSettingsData.ExtTicketSystemOptions(kTicketSystems, "9"),
            Is.EqualTo(kOptionsWithRemovedTicketSystem));
        Assert.That(DeviceSpecificSettingsData.ExtTicketSystemOptions("", ""), Is.Empty);
    }

    /// <summary>Wire values are independent of enum display names.</summary>
    [TestCase("installOn", "Any", "ANY")]
    [TestCase("installOn", "GatewaysInPath", "gatewaysInPath")]
    [TestCase("labelCreation", "Supermanager", "supermanager")]
    [TestCase("labelCreation", "Submanager", "submanager")]
    [TestCase("timeObjectCreation", "Ignore", "ignore")]
    [TestCase("labelCreation", "Ignore", "ignore")]
    [TestCase("ruleLoggingOverride", "LogTrack", "log_track")]
    [TestCase("ruleType", "AppSecurity", "AppSecurity")]
    [TestCase("ruleType", "HandleAccessAndIps", "accessAndIPS")]
    [TestCase("positioningAlgorithm", "AllowCategoryEndOfAppSectionDistinguishCommonServices", "allowCategoryEndOfAppSectionDistinguishCommonServices")]
    [TestCase("implementationMode", "RerouteToExternalTicketSystem", "reroute_to_external_ticket_system")]
    [TestCase("implementationModeOverrideForRules", "RerouteToExternalTicketSystem", "rerouteToExternalTicketSystem")]
    [TestCase("implementationMode", "Automatic", "Automatic")]
    [TestCase("modellingConnectionIdField", "CustomField", "CustomField")]
    public void SerializedValuesMatchContract(string databaseKey, string enumName, string wireValue)
    {
        DeviceSpecificSettingKey key = DeviceSpecificSettingKeys.ByDatabaseKey[databaseKey];
        object value = Enum.Parse(key.ValueType, enumName);
        Assert.That(DeviceSpecificSettingValueSerializer.Serialize(key, value), Is.EqualTo(new JValue(wireValue)));
        Assert.That(DeviceSpecificSettingValueSerializer.Deserialize(key, new JValue(wireValue)), Is.EqualTo(value));
    }

    /// <summary>Ignore is forbidden for service and address object creation.</summary>
    [Test]
    public void IgnoreIsLimitedToTimesAndLabels()
    {
        Assert.Throws<ArgumentException>(() => DeviceSpecificSettingValueSerializer.Serialize(
            DeviceSpecificSettingKeys.ServiceObjectCreation, ProvisioningObjectCreationMode.Ignore));
        Assert.Throws<InvalidOperationException>(() => DeviceSpecificSettingValueSerializer.Deserialize(
            DeviceSpecificSettingKeys.AddressObjectCreation, new JValue("ignore")));
    }
}
